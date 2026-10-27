# -*- coding: utf-8 -*-
# MODELL13 Riesenblatt2: Albedo + Normal + Maske fuer ein Monstera-Blatt.
# UV wie in der Geometrie: U 0..1 quer ueber die Blatthaelfte (gespiegelt),
# V 0..1 linear entlang der Mittelrippe (V=0 Basis, V=1 Spitze).
# Muster: helle Mittelrippe, bogige Seitenadern zu den Lappenmitten,
# helle Schnittkanten an Schlitzen/Fenstern, Randabdunkelung,
# Fuss dunkelgruen -> Spitze heller (WALD-ZIEL: Farbton 95..130 Grad,
# Saett 0.3..0.5, Helligkeit max 0.45).
import math, random, colorsys, importlib.util as iu
import numpy as np
from PIL import Image

W = H = 1024
BASE = 'Assets/_Project/Art/Plants/Riesenblatt/Textures/'
OUT_A = BASE + 'Riesenblatt2_Leaf_Albedo.png'
OUT_N = BASE + 'Riesenblatt2_Leaf_Normal.png'
OUT_M = BASE + 'Riesenblatt2_Leaf_Mask.png'
OUT_S = BASE + 'Riesenblatt2_Stem_Albedo.png'

# Konstanten aus der Geometrie (Adern muessen exakt zu den Lappen passen).
_spec = iu.spec_from_file_location(
    'geo', 'ArtSource/Riesenblatt/riesenblatt2_geometrie.py')
_geo = iu.module_from_spec(_spec); _spec.loader.exec_module(_geo)
NLOBE = _geo.NLOBE
SPLIT_V = _geo.SPLIT_V
SPLIT_AV = _geo.SPLIT_AV
SPLIT_CU = _geo.SPLIT_CU
SPLIT_AU = _geo.SPLIT_AU
HOLES = _geo.HOLES

# Bogenadern je Lappen: Start (0, vm), Ende (1, ve) mit a2-Shift wie in
# lobe_vein_v der Geometrie (Adern bog zur BASIS, wie bei Monstera).
VEINS = []
for k in range(len(_geo.VEIN_V) - 1):
    lo, hi = _geo.VEIN_V[k], _geo.VEIN_V[k + 1]
    vm = lo + 0.5 * (hi - lo)
    ve = lo + 0.74 * (hi - lo)      # a2 bei a=0 (Rand)
    VEINS.append((vm, ve, 1.0))
# kurze Zwischenadern (Monstera: feine Adern zwischen den Hauptadern)
for k in range(len(_geo.VEIN_V) - 2):
    lo = _geo.VEIN_V[k] + 0.5 * (_geo.VEIN_V[k + 1] - _geo.VEIN_V[k])
    hi = _geo.VEIN_V[k + 1]
    vm = lo + 0.5 * (hi - lo)
    ve = lo + 0.74 * (hi - lo)
    VEINS.append((vm, ve, 0.45))


def seg_dist(u, v, p0, p1):
    ex, ey = p1[0] - p0[0], p1[1] - p0[1]
    L2 = max(ex * ex + ey * ey, 1e-12)
    t = np.clip(((u - p0[0]) * ex + (v - p0[1]) * ey) / L2, 0.0, 1.0)
    dx = u - (p0[0] + t * ex)
    dy = v - (p0[1] + t * ey)
    return np.sqrt(dx * dx + dy * dy)


def vein_d(u, v):
    """Mindestabstand zu den Bogenseitenadern (Bezier, 8 Segmente) plus
    Abstand zur Mittelrippe. Gibt (distanz, gewicht) zurueck: Zwischenadern
    sind duenner (Gewicht < 1)."""
    d_rib = abs(u)
    best = np.full(u.shape, 4.0, dtype=np.float32)
    wgt = np.ones(u.shape, dtype=np.float32)
    for vm, ve, w in VEINS:
        p0 = np.array([0.0, vm])
        pc = np.array([0.55, vm + 1.6 * (ve - vm)])
        p1 = np.array([1.0, ve])
        prev = p0
        for i in range(1, 9):
            t = i / 8.0
            pt = ((1 - t) ** 2) * p0 + (2 * (1 - t) * t) * pc + (t * t) * p1
            d = seg_dist(u, v, prev, pt)
            take = d < best
            best = np.where(take, d, best)
            wgt = np.where(take, w, wgt)
            prev = pt
    return best, d_rib, wgt


def cut_d(u, v):
    """Mindestabstand zu Schnittellipsen (Schlitze + Fenster), in (v,u)-Einheit
    ~gemittelt; 0 = auf der Schnittlinie."""
    best = np.full(u.shape, 4.0, dtype=np.float32)
    for k in range(NLOBE):
        a_v, c_u, a_u = SPLIT_AV[k], SPLIT_CU, SPLIT_AU
        # Distanz zur Ellipsgrenze (Naeherung ueber Radius in normierten Koordinaten).
        dv = (v - SPLIT_V[k]) / a_v
        du = (u - c_u) / a_u
        r = np.sqrt(dv * dv + du * du)
        # normierter Abstand zur Kontur, grob in Einheits-Ellipsenradius ~0.3
        d = np.abs(r - 1.0) * min(a_v, a_u)
        best = np.minimum(best, d)
    for (hv, hu, av, au) in HOLES:
        dv = (v - hv) / av
        du = (u - hu) / au
        r = np.sqrt(dv * dv + du * du)
        d = np.abs(r - 1.0) * min(av, au)
        best = np.minimum(best, d)
    return best


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


def height_to_normal(hgt, strength=2.2):
    gx = np.zeros_like(hgt); gy = np.zeros_like(hgt)
    gx[:, 1:-1] = (hgt[:, :-2] - hgt[:, 2:]) * 0.5
    gx[:, 0] = gx[:, 1]; gx[:, -1] = gx[:, -2]
    gy[1:-1, :] = (hgt[:-2, :] - hgt[2:, :]) * 0.5
    gy[0, :] = gy[1, :]; gy[-1, :] = gy[-2, :]
    nx = gx * strength; ny = gy * strength; nz = np.ones_like(hgt)
    inv = 1.0 / np.sqrt(nx * nx + ny * ny + nz * nz)
    return np.stack([(nx * inv + 1) * 0.5, (ny * inv + 1) * 0.5,
                     (nz * inv + 1) * 0.5], axis=-1)


def main():
    # Blatt-Insel: Zeilen = V (0 Basis -> 1 Spitze), Spalten = U quer.
    vs, us = np.meshgrid(np.linspace(0, 1, H), np.linspace(0, 1, W), indexing='ij')
    vs = vs.astype(np.float32); us = us.astype(np.float32)

    dv, du_rib, vw = vein_d(us, vs)
    dcut = cut_d(us, vs)
    rib = np.exp(-(du_rib / 0.030) ** 2) * 1.0            # Mittelrippe
    veins = np.exp(-((dv / (0.034 * vw)) ** 2)) * vw      # Bogenseitenadern
    # helle Schnittkanten (Monstera: Unterseite/Blattdicke hellgruen)
    edgecut = np.exp(-(dcut / 0.012) ** 2) * 1.0
    feinstr = 0.10 * np.sin(vs * 137.0 + us * 41.0) * (0.4 + 0.6 * us)
    height = rib + veins + feinstr * 0.35 + 0.6 * edgecut
    shade = (1.0 + 0.50 * rib + 1.35 * veins + 1.1 * edgecut
             - 0.45 * np.clip((us - 0.55) / 0.45, 0, 1))

    nz_img = Image.fromarray(
        (np.abs(np.random.default_rng(13).normal(0, 6, (H // 4, W // 4))) * 4)
        .clip(0, 255).astype(np.uint8)).resize((W, H))
    noise = (np.asarray(nz_img, dtype=np.float32) / 255.0 - 0.5) * 0.05

    # WALD-ZIEL-Farbe: Fuss dunkelgruen -> Spitze heller, Adern gelblich-heller.
    hue = np.clip(0.340 - 0.028 * vs - 0.055 * veins, 0.264, 0.361)  # 95..130 Grad
    sat = np.clip(0.44 - 0.08 * vs - 0.10 * veins, 0.30, 0.50)
    val = np.clip((0.150 + 0.20 * vs ** 1.15) * shade + noise, 0.05, 0.45)
    albedo = hsv_rgb(hue, sat, val)
    fiber = 0.018 * np.sin(us * 900.0 + np.sin(vs * 23.0) * 6.0)
    albedo *= (1.0 + fiber)[..., None]
    # dunkle Flecken (grobe Noise-Maske, natuerliche Unregelmessigkeit)
    blot = Image.fromarray(
        np.abs(np.random.default_rng(29).normal(128, 34, (H // 8, W // 8)))
        .clip(0, 255).astype(np.uint8)).resize((W, H))
    blot = (np.asarray(blot, dtype=np.float32) / 255.0 - 0.5) * 0.10
    albedo *= (1.0 + blot)[..., None]
    albedo = np.clip(albedo, 0.02, 0.45)   # WALD-ZIEL: Helligkeit max 0.45

    normal = height_to_normal(height, strength=4.5)
    mask = np.ones((H, W), dtype=np.float32)   # Form = echte Geometrie

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
    hue_s = 0.115 - 0.02 * sv
    sat_s = 0.40 + 0.06 * band
    val_s = np.clip((0.16 + 0.10 * sv) * (0.75 + 0.35 * band) * (0.9 + 0.2 * fb),
                    0.05, 0.40)
    save((hsv_rgb(hue_s, sat_s, val_s) * 255).astype(np.uint8), OUT_S, 'RGB')
    print('fertig')


if __name__ == '__main__':
    main()
