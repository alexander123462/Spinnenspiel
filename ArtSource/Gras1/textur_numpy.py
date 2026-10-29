# -*- coding: utf-8 -*-
# TEXTUR1 Gras1: Albedo + Normalmap pro Zelle (Raster aus zellen.json).
# Albedo: Scan-Gras (grass_medium_01) als Faser-Vorlage, ueberblendet mit
# Farbverlauf Fuss dunkelgruen -> Spitze hell gelbgruen, dunklere Mittelrinne.
# Normalmap: abgeleitet aus Hoehe (Rinne + Faserstreifen + Scan-Helligkeit).
import json, math, random
import numpy as np
from PIL import Image, ImageFilter

W = H = 1024
GUT = 6  # Graben-Rand in px (entspricht PAD=0.006)
SCAN = 'Assets/_Project/Art/Scans/Models/grass_medium_01/grass_medium_01_albedo.png'
OUT_A = 'ArtSource/Gras1/Textures/Gras1_Albedo.png'
OUT_N = 'ArtSource/Gras1/Textures/Gras1_Normal.png'

z = json.load(open('ArtSource/Gras1/zellen.json'))
C, R = z['cols'], z['rows']
scan = np.asarray(Image.open(SCAN).convert('RGB'), dtype=np.float32) / 255.0


def ramp_row(v):
    """Ziel-Farbverlauf Fuss->Spitze (WALD-ZIEL: Farbton 95..130, Sae 0.3..0.5)."""
    import colorsys
    h = 0.30 - 0.05 * v          # 108 -> 90 Grad (gelblicher zur Spitze)
    s = 0.46 - 0.12 * v          # entsaettigter zur Spitze
    val = 0.16 + 0.27 * v ** 1.2 # Fuss dunkel, Spitze heller (max 0.43)
    return np.array(colorsys.hsv_to_rgb(h, s, val), dtype=np.float32)


def cell_pattern(cw, chh, seed):
    """Albedo- und Hoehe-Feld einer Zelle (cw x chh Pixel)."""
    rng = random.Random(seed)
    # u (quer) und v (entlang) je Pixel; Bildreihe y=0 ist Spitze (v=1)
    xs = np.linspace(0.0, 1.0, cw, dtype=np.float32)[None, :]
    vs = np.linspace(1.0, 0.0, chh, dtype=np.float32)[:, None]
    # Faserstreifen: feine Adern entlang v (1D-Rauschen mit vielen Kanten ueber u)
    nb = 48
    fu = np.interp(np.linspace(0, nb - 1, cw), np.arange(nb),
                   np.array([rng.uniform(-1, 1) for _ in range(nb)])).astype(np.float32)
    fu = fu[None, :] * (0.5 + 0.5 * vs)          # unten schaerfer

    # Scan nur als Feindetail: Hochpass der Helligkeit (echte Gras-Fasern)
    x0 = rng.randint(0, 1024 - 300); y0 = rng.randint(0, 1024 - 600)
    crop = Image.fromarray((scan[y0:y0 + 600, x0:x0 + 300] * 255).astype(np.uint8))
    crop = crop.resize((cw, chh))
    lum = np.asarray(crop.convert('L'), dtype=np.float32) / 255.0
    blur = np.asarray(crop.filter(ImageFilter.GaussianBlur(6)).convert('L'),
                      dtype=np.float32) / 255.0
    hp = np.clip((lum - blur) * 4.0, -1.0, 1.0)

    # Mittelrinne dunkel, Blattraender leicht aufgehellt
    groove = np.exp(-((xs - 0.5) / 0.11) ** 2)   # 1 in der Mitte
    edge = np.exp(-(np.minimum(xs, 1 - xs) / 0.10) ** 2)
    shade = (1.0 - 0.40 * groove + 0.06 * edge) * (1.0 + 0.10 * fu + 0.08 * hp)

    tip = np.clip((vs - 0.72) / 0.28, 0, 1) ** 2  # helle Spitze
    ramp = np.array([ramp_row(vv) for vv in vs[:, 0]], dtype=np.float32)  # chh x 3
    albedo = ramp[:, None, :] * np.expand_dims(shade * (1.0 + 0.45 * tip), -1)
    albedo = np.clip(albedo, 0.0, 0.45)

    # Hoehe: Rinne (Mitte tief), Faserstreifen, Scan-Feinstruktur
    height = (0.35 + 0.65 * np.minimum(xs, 1 - xs) ** 0.7) * (1.0 - 0.25 * groove)
    height = height + 0.25 * fu + 0.15 * hp
    return albedo, height


def normal_from_height(hh, strength=2.2):
    """Tangentialraum-Normale aus Hoehe (Bild-y zeigt nach -V)."""
    dh_du = np.gradient(hh, axis=1) * strength
    dh_dv = -np.gradient(hh, axis=0) * strength
    n = np.stack([-dh_du, -dh_dv, np.ones_like(hh)], axis=-1)
    n /= np.linalg.norm(n, axis=-1, keepdims=True)
    return (n * 0.5 + 0.5).astype(np.float32)


def main():
    alb = np.zeros((H, W, 3), dtype=np.float32)
    nrm = np.full((H, W, 3), 0.5, dtype=np.float32)
    base = np.array([0.06, 0.12, 0.04], dtype=np.float32)
    alb[:, :] = base; nrm[:, :] = np.array([0.5, 0.5, 1.0], dtype=np.float32)
    for k, (cx, cy) in enumerate(z['cells']):
        x0 = int(round(cx * W / C)); x1 = int(round((cx + 1) * W / C))
        y0 = int(round(cy * H / R)); y1 = int(round((cy + 1) * H / R))
        a, hgt = cell_pattern(x1 - x0 - 2 * GUT, y1 - y0 - 2 * GUT, seed=100 + k)
        nx = normal_from_height(hgt)
        ix0, iy0 = x0 + GUT, y0 + GUT
        cw, chh = a.shape[1], a.shape[0]
        alb[iy0:iy0 + chh, ix0:ix0 + cw] = a
        nrm[iy0:iy0 + chh, ix0:ix0 + cw] = nx
        # Grabenrand: Randpixel wiederholen (kein Ausbluten der Nachbarzelle)
        alb[iy0:iy0 + chh, x0:ix0] = a[:, :1]
        alb[iy0:iy0 + chh, ix0 + cw:x1] = a[:, -1:]
        nrm[iy0:iy0 + chh, x0:ix0] = nx[:, :1]
        nrm[iy0:iy0 + chh, ix0 + cw:x1] = nx[:, -1:]
    Image.fromarray((alb * 255).astype(np.uint8)).save(OUT_A)
    Image.fromarray((nrm * 255).astype(np.uint8)).save(OUT_N)
    print('Albedo:', OUT_A, alb.shape, 'max', round(float(alb.max()), 3))
    print('Normal:', OUT_N, nrm.shape)


if __name__ == '__main__':
    main()

