# -*- coding: utf-8 -*-
# MODELL9 Busch2: Albedo + Normalmap fuer busch2_neu.fbx (ovale Blaetter, dichter).
# UV-Atlas wie Busch1: 3 Blattzellen (u 0.02..0.31 / 0.35..0.64 / 0.68..0.97,
# V=0 Ansatz, V=1 Spitze, U quer) + Rindenband (V 0.02..0.30 ueber ganze Breite).
# WALD-ZIEL: Farbton 95..130 Grad, Saettigung 0.3..0.5, Helligkeit <=0.45.
import math
import random
import colorsys
import numpy as np
from PIL import Image

W = H = 1024
CELLS = [(0.02, 0.31), (0.35, 0.64), (0.68, 0.97)]
OUT_DIR = 'ArtSource/Busch2'


def blotch(cw, chh, seed, n=12):
    """Bilineares Grossflaechen-Rauschen in -0.5..0.5."""
    rng = random.Random(seed)
    nz = np.array([[rng.uniform(-1, 1) for _ in range(n)] for _ in range(n)],
                  dtype=np.float32)
    return np.asarray(Image.fromarray(((nz + 1) * 127).astype(np.uint8))
                      .resize((cw, chh), Image.BILINEAR), dtype=np.float32) / 255.0 - 0.5


def leaf_rgb(t, cell, jit=0.0):
    """Zellgruen: t 0..1 entlang des Blattes (0=Ansatz). Ovales Laub, drei Toene."""
    h = [0.300, 0.328, 0.276][cell] + 0.016 * t + 0.004 * jit   # 99..122 Grad
    s = 0.44 - 0.10 * t
    v = 0.19 + 0.17 * t + 0.02 * jit
    return np.array(colorsys.hsv_to_rgb(h % 1.0, min(0.50, s), min(0.42, v)),
                    dtype=np.float32)


def leaf_cell(cw, chh, cell, seed):
    """Albedo + Hoehe einer ovalen Blattzelle: U quer (0.5=Mittelrippe), V laengs."""
    xs = np.linspace(0.0, 1.0, cw, dtype=np.float32)[None, :]
    vs = np.linspace(1.0, 0.0, chh, dtype=np.float32)[:, None]   # Bildzeile 0 = V=1
    xr = np.abs(xs - 0.5)
    col = np.zeros((chh, cw, 3), dtype=np.float32)
    for j in range(chh):
        col[j, :, :] = leaf_rgb((chh - 1 - j) / max(1, chh - 1), cell)
    rib = np.exp(-(xr / 0.055) ** 2)                             # Mittelrippe
    fq = random.Random(seed).uniform(7.0, 9.0)
    lat = vs * fq + (xr * 2.0) ** 2 * 1.8                        # bogende Seitenadern
    vein = np.exp(-(((lat % 1.0) - 0.5) / 0.085) ** 2) * (1.0 - rib)
    edge = np.clip((xr - 0.38) / 0.12, 0.0, 1.0)                 # Randabdunklung
    bl = blotch(cw, chh, seed)
    hgt = 0.35 * rib + 0.25 * vein + 0.10 * bl - 0.15 * edge
    a = col * (1.0 + 0.16 * bl)[..., None]
    a = a * (1.0 - 0.55 * rib)[..., None] \
        + np.array([0.11, 0.16, 0.07], np.float32) * rib[..., None]
    a = a * (1.0 - 0.35 * edge)[..., None]
    a = a * (1.0 + 0.22 * vein)[..., None] \
        + np.array([0.025, 0.05, 0.015], np.float32) * vein[..., None]
    stipe = np.exp(-((vs - 0.04) / 0.045) ** 2)                  # heller Stielansatz
    a = a * (1 - stipe[..., None]) + np.array([0.13, 0.15, 0.07], np.float32) * stipe[..., None]
    return np.clip(a, 0.0, 1.0), np.clip(hgt, 0.0, 1.0)


def bark_cell(cw, chh, seed):
    """Rinde: braune Laengsfasern mit Querrissen (V=entlang des Zweigs)."""
    xs = np.linspace(0.0, 1.0, cw, dtype=np.float32)[None, :]
    vs = np.linspace(1.0, 0.0, chh, dtype=np.float32)[:, None]
    fib = blotch(cw, chh, seed, n=96)
    rng = random.Random(seed + 3)
    col = np.zeros((chh, cw, 3), dtype=np.float32)
    for j in range(chh):
        t = (chh - 1 - j) / max(1, chh - 1)
        h = 0.082 + 0.020 * t
        s = 0.42 - 0.08 * t
        v = 0.12 + 0.10 * t
        col[j, :, :] = np.array(colorsys.hsv_to_rgb(h, s, v), dtype=np.float32)
    grooves = 0.5 + 0.5 * np.cos(xs * rng.uniform(14.0, 20.0) * 2 * math.pi)
    crack = np.exp(-(((vs * 7.0 + fib * 3.0) % 1.0 - 0.5) / 0.05) ** 2)
    hgt = 0.55 * grooves + 0.20 * (fib + 0.5) - 0.35 * crack
    a = col * (0.75 + 0.45 * grooves)[..., None] * (0.85 + 0.35 * (fib + 0.5))[..., None]
    a = a * (1.0 - 0.55 * crack)[..., None]
    return np.clip(a, 0.0, 1.0), np.clip(hgt, 0.0, 1.0)


def normal_from_height(hh, strength=2.0):
    """Tangentialraum-Normale aus Hoehe (Bild-y zeigt nach -V)."""
    dh_du = np.gradient(hh, axis=1) * strength
    dh_dv = -np.gradient(hh, axis=0) * strength
    n = np.stack([-dh_du, -dh_dv, np.ones_like(hh)], axis=-1)
    n /= np.linalg.norm(n, axis=-1, keepdims=True)
    return (n * 0.5 + 0.5).astype(np.float32)


def main():
    alb = np.full((H, W, 3), 0.10, dtype=np.float32)
    alb[:, :, 0] = 0.13
    nrm = np.stack([np.full((H, W), 0.5, np.float32)] * 2 +
                   [np.ones((H, W), np.float32)], axis=-1)

    # Rinde nur im V-Band 0.02..0.30 (Ast-UVs laut busch2_build), Blattzellen V 0..1.
    vb0, vb1 = 0.02, 0.30
    gy0 = int(round((1.0 - vb1) * H)); gy1 = int(round((1.0 - vb0) * H))
    a, hgt = bark_cell(W, gy1 - gy0, seed=9700)
    alb[gy0:gy1, :, :] = a
    nx = normal_from_height(hgt, strength=2.2)
    nrm[gy0:gy1, :, :2] = nx[:, :, :2]

    pad = 0.015
    for i, (u0, u1) in enumerate(CELLS):
        gx0 = int(round(u0 * W)); gx1 = int(round(u1 * W))
        gya = int(round((1.0 - (1.0 - pad)) * H)); gyb = int(round((1.0 - pad) * H))
        a, hgt = leaf_cell(gx1 - gx0, gyb - gya, cell=i, seed=900 + i)
        nx = normal_from_height(hgt, strength=1.7)
        alb[gya:gyb, gx0:gx1] = a
        nrm[gya:gyb, gx0:gx1, :2] = nx[:, :, :2]

    Image.fromarray((alb * 255).astype(np.uint8)).save(OUT_DIR + '/Busch2_Albedo.png')
    Image.fromarray((nrm * 255).astype(np.uint8)).save(OUT_DIR + '/Busch2_Normal.png')
    print('Albedo:', OUT_DIR + '/Busch2_Albedo.png', 'max', round(float(alb.max()), 3))
    print('Normal:', OUT_DIR + '/Busch2_Normal.png')


if __name__ == '__main__':
    main()
