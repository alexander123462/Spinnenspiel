# -*- coding: utf-8 -*-
# MODELL14 Riesenblatt3: Albedo + Normal + Maske fuer ein Bananenblatt.
# UV wie in der Geometrie: U 0..1 quer ueber die Blatthaelfte (gespiegelt),
# V 0..1 linear entlang der Mittelrippe (V=0 Basis, V=1 Spitze).
# Muster: helle Mittelrippe, viele parallele Bogenadern (Musa-typisch),
# helle Risskanten an den Einrissen, Randabdunkelung,
# Fuss dunkelgruen -> Spitze heller (WALD-ZIEL: Farbton 95..130 Grad,
# Saett 0.3..0.5, Helligkeit max 0.45).
import math, importlib.util as iu
import numpy as np
from PIL import Image

W = H = 1024
BASE = 'Assets/_Project/Art/Plants/Riesenblatt/Textures/'
OUT_A = BASE + 'Riesenblatt3_Leaf_Albedo.png'
OUT_N = BASE + 'Riesenblatt3_Leaf_Normal.png'
OUT_M = BASE + 'Riesenblatt3_Leaf_Mask.png'
OUT_S = BASE + 'Riesenblatt3_Stem_Albedo.png'

# Konstanten aus der Geometrie (Adern und Risse muessen exakt passen).
_spec = iu.spec_from_file_location(
    'geo', 'ArtSource/Riesenblatt/riesenblatt3_geometrie.py')
_geo = iu.module_from_spec(_spec); _spec.loader.exec_module(_geo)
NVEIN = _geo.NVEIN
TEARS = _geo.TEARS


def seg_dist(u, v, p0, p1):
    ex, ey = p1[0] - p0[0], p1[1] - p0[1]
    L2 = max(ex * ex + ey * ey, 1e-12)
    t = np.clip(((u - p0[0]) * ex + (v - p0[1]) * ey) / L2, 0.0, 1.0)
    dx = u - (p0[0] + t * ex)
    dy = v - (p0[1] + t * ey)
    return np.sqrt(dx * dx + dy * dy)


def vein_d(u, v):
    """Mindestabstand zu den Bogenadern: Musa-Adern verlaufen fast parallel,
    leicht zur Spitze geneigt (Neigung je Ader leicht variiert), am Rand leicht
    aufwaerts gebogen (2 Segmente). Dazu Abstand zur Mittelrippe.
    Rueckgabe (distanz, d_rib)."""
    best = np.full(u.shape, 4.0, dtype=np.float32)
    rng = np.random.default_rng(7)
    for k in range(NVEIN):
        vm = (k + 0.5) / NVEIN + float(rng.uniform(-0.004, 0.004))
        ve = vm + 0.055 + float(rng.uniform(-0.012, 0.012))
        p0 = np.array([0.0, vm])
        pc = np.array([0.72, vm + 0.030])     # frueh flach, spaeter aufwaerts
        p1 = np.array([1.0, ve])
        prev = p0
        for i in (1, 2):
            t = i / 2.0
            pt = ((1 - t) ** 2) * p0 + (2 * (1 - t) * t) * pc + (t * t) * p1
            best = np.minimum(best, seg_dist(u, v, prev, pt))
            prev = pt
    return best, np.abs(u)


def cut_d(u, v):
    """Mindestabstand zu den Riss-Ellipsen (dieselbe Form wie in_cutout der
    Geometrie); 0 = auf der Schnittlinie (helle Risskante)."""
    best = np.full(u.shape, 4.0, dtype=np.float32)
    for half in range(2):
        for (vk, av, u_in, slant) in TEARS[half]:
            a_u = 1.45 - u_in
            vc = vk + slant * (u - 0.7)
            dv = (v - vc) / av
            du = (u - 1.45) / a_u
            r = np.sqrt(dv * dv + du * du)
            d = np.abs(r - 1.0) * min(av, a_u)
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

    dv, du_rib = vein_d(us, vs)
    dcut = cut_d(us, vs)
    rib = np.exp(-(du_rib / 0.034) ** 2) * 1.0            # Mittelrippe (breit)
    # Selbstkritik D1: Adern bei 22 Adern mit Sigma 0.028 fast ueberlappend
    # (Wellpappe) -> scharfe Kernlinie + schwacher Halo, Kontrast erhoeht.
    veins = np.exp(-((dv / 0.011) ** 2)) + 0.35 * np.exp(-((dv / 0.030) ** 2))
    # helle Risskanten (frisch eingerissen: helles Blattinnengewebe)
    edgecut = np.exp(-(dcut / 0.014) ** 2) * 1.0
    feinstr = 0.10 * np.sin(vs * 151.0 + us * 37.0) * (0.4 + 0.6 * us)
    height = rib + veins * 0.8 + feinstr * 0.35 + 0.6 * edgecut
    shade = (1.0 + 0.55 * rib + 1.25 * veins + 1.1 * edgecut
             - 0.45 * np.clip((us - 0.55) / 0.45, 0, 1))

    nz_img = Image.fromarray(
        (np.abs(np.random.default_rng(14).normal(0, 6, (H // 4, W // 4))) * 4)
        .clip(0, 255).astype(np.uint8)).resize((W, H))
    noise = (np.asarray(nz_img, dtype=np.float32) / 255.0 - 0.5) * 0.05

    # WALD-ZIEL-Farbe: Fuss dunkelgruen -> Spitze heller, Adern gelblich-heller.
    # Selbstkritik D1: Saelttigung und Aderkontrast erhoeht (zu blasses Mint).
    hue = np.clip(0.335 - 0.030 * vs - 0.062 * veins, 0.264, 0.361)  # 95..130 Grad
    sat = np.clip(0.48 - 0.09 * vs - 0.13 * veins, 0.30, 0.50)
    val = np.clip((0.140 + 0.215 * vs ** 1.15) * shade + noise, 0.05, 0.45)
    albedo = hsv_rgb(hue, sat, val)
    # feine Fasern parallel zu den Adern (entlang u, bei jeder Ader)
    fiber = 0.020 * np.sin(((vs - 0.028 * us) * NVEIN * math.tau) + np.sin(us * 9.0))
    albedo *= (1.0 + fiber)[..., None]
    # dunkle Flecken (grobe Noise-Maske, natuerliche Unregelmessigkeit)
    blot = Image.fromarray(
        np.abs(np.random.default_rng(31).normal(128, 34, (H // 8, W // 8)))
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
