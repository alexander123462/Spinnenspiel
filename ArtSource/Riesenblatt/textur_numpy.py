# -*- coding: utf-8 -*-
# MODELL12 Riesenblatt1: Albedo + Normal + Maske fuer EIN grosses Blatt
# (UV: U 0..1 quer ueber die Blatthaelfte gespiegelt, V 0..1 entlang der
# Mittelrippe, V=0 Basis, V=1 Spitze). Muster: helle Mittelrippe, bogige
# Seitenadern zu den Lappen-Raendern, Randabdunkelung, Fuss dunkelgruen ->
# Spitze heller (WALD-ZIEL: Farbton 95..130 Grad, Saett 0.3..0.5, V max 0.45).
import math, random, colorsys
import numpy as np
from PIL import Image

W = H = 1024
OUT_A = 'Assets/_Project/Art/Plants/Riesenblatt/Textures/Riesenblatt_Leaf_Albedo.png'
OUT_N = 'Assets/_Project/Art/Plants/Riesenblatt/Textures/Riesenblatt_Leaf_Normal.png'
OUT_M = 'Assets/_Project/Art/Plants/Riesenblatt/Textures/Riesenblatt_Leaf_Mask.png'
OUT_S = 'Assets/_Project/Art/Plants/Riesenblatt/Textures/Riesenblatt_Stem_Albedo.png'

# Seitenadern: UV-V ist linear ueber Saulen der Geometrie (v_row=i/ROWS),
# nicht Bogenlaenge. Lappenmitte in Zeilen-Raum: k+0.5 geteilt durch NLOBE,
# mit demselben a2-Shift zur Spitze wie margin_w in der Geometrie.
import importlib.util as _iu
_spec = _iu.spec_from_file_location(
    'geo', 'ArtSource/Riesenblatt/riesenblatt1_geometrie.py')
_geo = _iu.module_from_spec(_spec); _spec.loader.exec_module(_geo)
NLOBE = _geo.NLOBE
VEINS = []
for _k in range(NLOBE):
    _lo, _hi = _k / NLOBE, (_k + 1) / NLOBE
    _a = min(max(0.5 + (-0.5) / 0.62, 0.0), 1.0)
    _vm = _lo + _a * (_hi - _lo)
    VEINS.append((round(_vm, 3), round(0.055 - 0.007 * _k, 3)))


def vein_d(u, v):
    """Mindestabstand zu den Seitenadern (Segment-Naeherung) + Abstand zur Mittelrippe.
    Adern bogig zur Spitze: Start bei (0, v0), Ende bei (1, v0+dv)."""
    d_rib = abs(u)
    best = np.full(u.shape, 4.0, dtype=np.float32)
    for v0, dv in VEINS:
        # Quadratischer Bezier: Start (0,v0), Ende (1,v0+dv), Kontrollpunkt
        # ueber der Sehne -> Bogen zur Spitze (echte Bogenadern).
        p0 = np.array([0.0, v0])
        pc = np.array([0.62, v0 + 4.0 * dv])   # deutlicher Bogen zur Spitze
        p1 = np.array([1.0, min(v0 + dv, 1.0)])
        prev = p0
        for i in range(1, 9):
            t = i / 8.0
            pt = ((1 - t) ** 2) * p0 + (2 * (1 - t) * t) * pc + (t * t) * p1
            ex, ey = pt - prev
            L2 = max(ex * ex + ey * ey, 1e-12)
            tt = np.clip(((u - prev[0]) * ex + (v - prev[1]) * ey) / L2, 0.0, 1.0)
            dx = u - (prev[0] + tt * ex)
            dy = v - (prev[1] + tt * ey)
            best = np.minimum(best, np.sqrt(dx * dx + dy * dy))
            prev = pt
    return best, d_rib


def leaf_fields(u, v, seed):
    """Hoehe-Feld (Rippenrelief) und Farbmodulation fuer ein Blattpixel."""
    rng = random.Random(seed)
    dv, du = vein_d(u, v)
    rib = np.exp(-(du / 0.034) ** 2) * 1.0                      # Mittelrippe
    veins = np.exp(-(dv / 0.026) ** 2) * 1.0                    # Seitenadern
    feinstr = 0.10 * np.sin(v * 137.0 + u * 41.0) * (0.4 + 0.6 * u)
    height = rib + veins + feinstr * 0.35
    # Schattierung: Adern deutlich heller, Rand dunkler
    shade = 1.0 + 0.40 * rib + 1.30 * veins - 0.50 * np.clip((u - 0.60) / 0.40, 0, 1)
    return height, shade, veins


def color_at(v, shade, noise):
    """WALD-ZIEL-Farbe entlang V: Fuss dunkelgruen -> Spitze heller."""
    h = 0.342 - 0.030 * v            # 123 -> 112 Grad
    s = 0.44 - 0.08 * v             # 0.44 -> 0.36
    val = (0.150 + 0.20 * v ** 1.15) * shade + noise
    val = float(np.clip(val, 0.05, 0.45))
    s = float(np.clip(s, 0.30, 0.50))
    return np.array(colorsys.hsv_to_rgb(h, s, val), dtype=np.float32)


def height_to_normal(hgt, strength=2.2):
    gx = np.zeros_like(hgt); gy = np.zeros_like(hgt)
    gx[:, 1:-1] = (hgt[:, :-2] - hgt[:, 2:]) * 0.5
    gx[:, 0] = gx[:, 1]; gx[:, -1] = gx[:, -2]
    gy[1:-1, :] = (hgt[:-2, :] - hgt[2:, :]) * 0.5
    gy[0, :] = gy[1, :]; gy[-1, :] = gy[-2, :]
    nx = gx * strength; ny = gy * strength; nz = np.ones_like(hgt)
    inv = 1.0 / np.sqrt(nx * nx + ny * ny + nz * nz)
    return np.stack([(nx * inv + 1) * 0.5, (ny * inv + 1) * 0.5, (nz * inv + 1) * 0.5], axis=-1)


def hsv_rgb(h, s, v):
    """Vektorisierte HSV->RGB-Umrechnung (Arrays 0..1)."""
    h = np.asarray(h, dtype=np.float32) % 1.0
    s = np.asarray(s, dtype=np.float32)
    v = np.asarray(v, dtype=np.float32)
    i = np.floor(h * 6.0)
    f = h * 6.0 - i
    p = v * (1 - s); q = v * (1 - f * s); t = v * (1 - (1 - f) * s)
    m = (i % 6).astype(np.int32)
    r = np.choose(m, [v, q, p, p, t, v])
    g = np.choose(m, [t, v, v, q, p, p])
    b = np.choose(m, [p, p, t, v, v, q])
    return np.stack([r, g, b], axis=-1).astype(np.float32)


def main():
    rng = random.Random(1207)
    # Blatt-Insel: Zeilen = V (0 Basis unten -> 1 Spitze oben), Spalten = U quer.
    vs, us = np.meshgrid(np.linspace(0, 1, H), np.linspace(0, 1, W), indexing='ij')
    vs = vs.astype(np.float32); us = us.astype(np.float32)
    hgt = np.zeros((H, W), dtype=np.float32)
    albedo = np.zeros((H, W, 3), dtype=np.float32)
    noise = np.zeros((H, W), dtype=np.float32)
    nz_img = Image.fromarray(
        (np.abs(np.random.default_rng(7).normal(0, 6, (H // 4, W // 4))) * 4)
        .clip(0, 255).astype(np.uint8)).resize((W, H))
    noise = (np.asarray(nz_img, dtype=np.float32) / 255.0 - 0.5) * 0.05
    hfield, shade, veins = leaf_fields(us, vs, 1207)
    hgt = hfield
    # Adern: deutlich gelblich-heller (Blattgruen-Ader auf dunklerem Grund).
    hue = 0.342 - 0.030 * vs - 0.055 * veins   # 123 Grad, Adern ~113 Grad
    sat = np.clip(0.44 - 0.08 * vs - 0.10 * veins, 0.30, 0.50)
    val = np.clip((0.150 + 0.20 * vs ** 1.15) * shade + noise, 0.05, 0.45)
    albedo = hsv_rgb(hue, sat, val)
    # feine Laengsfasern (heller/dunkler Streifen entlang V)
    fiber = 0.018 * np.sin(us * 900.0 + np.sin(vs * 23.0) * 6.0)
    albedo *= (1.0 + fiber)[..., None]
    albedo = np.clip(albedo, 0.02, 0.45)   # WALD-ZIEL: Helligkeit max 0.45

    # Normal aus Hoehe (Bild-Y = V-Richtung).
    normal = height_to_normal(hgt, strength=4.5)

    # Maske: Form ist echte Geometrie -> durchgaengig deckend (kein Cutout).
    mask = np.ones((H, W), dtype=np.float32)

    def save(arr, path, mode):
        if arr.dtype == np.float32:
            arr = (np.clip(arr, 0, 1) * 255).astype(np.uint8)
        Image.fromarray(arr, mode=mode).save(path)
        print('geschrieben', path)

    save((albedo * 255).astype(np.uint8), OUT_A, 'RGB')
    save((normal * 255).astype(np.uint8), OUT_N, 'RGB')
    save((mask * 255).astype(np.uint8), OUT_M, 'L')

    # Stiel-Albedo: gruenbraune Faserroehre, helle Kantenstreifen.
    sw, sh = 256, 512
    su = np.linspace(0, 1, sw)[None, :] * np.ones((sh, 1))
    sv = np.linspace(0, 1, sh)[:, None] * np.ones((1, sw))
    band = 0.5 + 0.5 * np.cos(su * math.tau * 6.0)
    fb = 0.5 + 0.5 * np.sin(sv * 700.0 + su * 30.0)
    hue = 0.115 - 0.02 * sv                      # braun-gelblich (Faserstiel)
    sat = 0.40 + 0.06 * band
    val = np.clip((0.16 + 0.10 * sv) * (0.75 + 0.35 * band) * (0.9 + 0.2 * fb), 0.05, 0.40)
    stem = hsv_rgb(hue, sat, val)
    save((stem * 255).astype(np.uint8), OUT_S, 'RGB')
    print('fertig')


if __name__ == '__main__':
    main()

