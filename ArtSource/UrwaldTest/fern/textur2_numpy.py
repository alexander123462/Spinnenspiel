# -*- coding: utf-8 -*-
# TEXTUR2 Farn_Grund: Atlas-Albedo + Normalmap (2048 px, 22x22-Zellen wie im FBX).
# Zeile 0 = Stiel-Zellen (Fasern entlang v), Zeilen 1..21 = Fieder-Zellen
# (Mittelrippe bei u=0.5, feine Seitenadern diagonal, Verlauf Fuss dunkel -> Spitze heller).
# Scan-Vorlage: fern_02_albedo.png nur als Hochpass-Feindetail.
import math, random, shutil
import numpy as np
from PIL import Image, ImageFilter

W = H = 2048
G = 22
GUT = 3                       # Graben-Rand in px (UV-PAD 0.03 * 93 px ~ 2.8)
SCAN = 'Assets/_Project/Art/Scans/Models/fern_02/fern_02_albedo.png'
OUT_A = 'ArtSource/UrwaldTest/fern/Textures/Farn_Grund_Atlas_Albedo.png'
OUT_N = 'ArtSource/UrwaldTest/fern/Textures/Farn_Grund_Atlas_Normal.png'

scan = np.asarray(Image.open(SCAN).convert('RGB'), dtype=np.float32) / 255.0


def scan_hp(cw, chh, rng):
    """Hochpass-Ausschnitt des Farn-Scans als Feindetail (~ +/-1)."""
    x0 = rng.randint(0, 1024 - 256); y0 = rng.randint(0, 1024 - 256)
    crop = Image.fromarray((scan[y0:y0 + 256, x0:x0 + 256] * 255).astype(np.uint8))
    crop = crop.resize((cw, chh))
    lum = np.asarray(crop.convert('L'), dtype=np.float32) / 255.0
    blur = np.asarray(crop.filter(ImageFilter.GaussianBlur(4)).convert('L'), dtype=np.float32) / 255.0
    return np.clip((lum - blur) * 4.0, -1.0, 1.0)


def leaf_cell(cw, chh, seed):
    """Albedo (cw x chh x 3) und Hoehe einer Fieder-Zelle; Bild-y=0 ist Spitze (v=1)."""
    import colorsys
    rng = random.Random(seed)
    h_j = rng.uniform(-0.015, 0.015); s_j = rng.uniform(-0.04, 0.04)
    v_j = rng.uniform(0.85, 1.15)
    xs = np.linspace(0.0, 1.0, cw, dtype=np.float32)[None, :]
    vs = np.linspace(1.0, 0.0, chh, dtype=np.float32)[:, None]

    rib = np.exp(-((xs - 0.5) / 0.075) ** 2)          # Mittelrippe (breiter, heller)
    t = np.abs(xs - 0.5)
    nv = 3.0                                          # Seitenadern je Halbflaeche (grober)
    q = (vs + t * 0.9) * nv
    vein = np.exp(-((q - np.round(q)) * nv * 1.3) ** 2)
    hp = scan_hp(cw, chh, rng)

    ramp = np.empty((chh, 3), dtype=np.float32)       # Farbverlauf entlang v
    for i, vv in enumerate(np.linspace(1.0, 0.0, chh)):
        hh = min(0.36, max(0.264, 0.305 + h_j - 0.04 * vv))
        ss = min(0.5, max(0.3, 0.42 + s_j - 0.10 * vv))
        val = (0.24 + 0.38 * vv ** 1.3) * v_j
        ramp[i] = colorsys.hsv_to_rgb(hh, ss, min(0.62, val))
    shade = (1.0 + 0.50 * rib - 0.45 * vein + 0.10 * hp)
    albedo = ramp[:, None, :] * shade[..., None]
    pale = np.array([0.45, 0.52, 0.32], dtype=np.float32)   # Mittelrippe heller und entsaettigter
    albedo = albedo * (1.0 - 0.55 * rib[..., None]) + pale[None, None, :] * (0.55 * rib[..., None])
    albedo = np.clip(albedo, 0.0, 0.70)
    height = 0.35 + 0.65 * rib - 0.35 * vein + 0.12 * hp
    return albedo, height


def stem_cell(cw, chh, seed):
    """Stiel-Zelle: brauener Stiel mit feinen Fasern entlang v (u = Umfang, nahtlos)."""
    import colorsys
    rng = random.Random(seed)
    nb = 24                                           # periodisches Faser-Rauschen ueber u
    vals = np.array([rng.uniform(-1, 1) for _ in range(nb + 1)], dtype=np.float32)
    vals[-1] = vals[0]                                # Wrap -> keine Naht am Rohr
    fu = np.interp(np.linspace(0, nb, cw), np.arange(nb + 1), vals)[None, :]
    xs = np.linspace(0.0, 1.0, cw, dtype=np.float32)[None, :]
    vs = np.linspace(1.0, 0.0, chh, dtype=np.float32)[:, None]
    edge = 1.0 - 0.25 * np.exp(-(np.minimum(xs, 1 - xs) / 0.12) ** 2)  # Rohrschatten an den Randern
    ramp = np.empty((chh, 3), dtype=np.float32)
    for i, vv in enumerate(np.linspace(1.0, 0.0, chh)):
        val = (0.16 + 0.13 * vv) * rng.uniform(0.9, 1.1)   # Spitze heller, Fuss dunkel
        ramp[i] = colorsys.hsv_to_rgb(0.09, 0.45, min(0.42, val))  # braun (Hue 0.09 = orange-braun)
    shade = edge * (1.0 + 0.35 * fu + 0.06 * scan_hp(cw, chh, rng))
    albedo = np.clip(ramp[:, None, :] * shade[..., None], 0.0, 0.42)
    height = 0.5 + 0.5 * np.broadcast_to(fu, (chh, cw))  # Fasern als Hoehe (streift entlang v)
    return albedo, height


def normal_from_height(hh, strength=2.0):
    """Tangentialraum-Normale aus Hoehe (Bild-y zeigt nach -V)."""
    dh_du = np.gradient(hh, axis=1) * strength
    dh_dv = -np.gradient(hh, axis=0) * strength
    n = np.stack([-dh_du, -dh_dv, np.ones_like(hh)], axis=-1)
    n /= np.linalg.norm(n, axis=-1, keepdims=True)
    return (n * 0.5 + 0.5).astype(np.float32)


def main():
    alb = np.zeros((H, W, 3), dtype=np.float32)
    nrm = np.full((H, W, 3), 0.5, dtype=np.float32)
    alb[:, :] = np.array([0.05, 0.10, 0.04], dtype=np.float32)   # neutrale Fuellzelle
    nrm[:, :] = np.array([0.5, 0.5, 1.0], dtype=np.float32)
    for cy in range(G):
        for cx in range(G):
            x0 = int(round(cx * W / G)); x1 = int(round((cx + 1) * W / G))
            # Bild-y zeigt gegen UV-v: UV-Zeile cy liegt in der BILD-Zeile G-1-cy,
            # damit Stiel (UV-Zeile 0, v~0 = Bildunten) nicht die Blattzellen trifft.
            iy = G - 1 - cy
            y0 = int(round(iy * H / G)); y1 = int(round((iy + 1) * H / G))
            cw, chh = x1 - x0 - 2 * GUT, y1 - y0 - 2 * GUT
            if cy == 0:
                a, hgt = stem_cell(cw, chh, seed=700 + cx)
            else:
                a, hgt = leaf_cell(cw, chh, seed=100 + cy * G + cx)
            nx = normal_from_height(hgt)
            ix0, iy0 = x0 + GUT, y0 + GUT
            alb[iy0:iy0 + chh, ix0:ix0 + cw] = a
            nrm[iy0:iy0 + chh, ix0:ix0 + cw] = nx
            # Grabenrand: Randpixel wiederholen (kein Ausbluten der Nachbarzelle).
            alb[iy0:iy0 + chh, x0:ix0] = a[:, :1]
            alb[iy0:iy0 + chh, ix0 + cw:x1] = a[:, -1:]
            nrm[iy0:iy0 + chh, x0:ix0] = nx[:, :1]
            nrm[iy0:iy0 + chh, ix0 + cw:x1] = nx[:, -1:]
    Image.fromarray((alb * 255).astype(np.uint8)).save(OUT_A)
    Image.fromarray((nrm * 255).astype(np.uint8)).save(OUT_N)
    import os
    for dst in ('Assets/_Project/Art/Plants/UrwaldTestFern/Textures/',):
        os.makedirs(dst, exist_ok=True)
        shutil.copy(OUT_A, dst + 'Farn_Grund_Atlas_Albedo.png')
        shutil.copy(OUT_N, dst + 'Farn_Grund_Atlas_Normal.png')
    print('Albedo:', OUT_A, alb.shape, 'max', round(float(alb.max()), 3))
    print('Normal:', OUT_N, nrm.shape)


if __name__ == '__main__':
    main()

