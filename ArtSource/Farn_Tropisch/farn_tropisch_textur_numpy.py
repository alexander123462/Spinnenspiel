# -*- coding: utf-8 -*-
# MODELL7 Farn_Tropisch: Albedo + Normalmap fuer den UV-Atlas von farn_tropisch_neu.fbx.
# Atlas 8x4 Zellen, V entlang der Fiederaechse (V=0 Grund, V=1 Spitze), U quer.
# Zellen 0,1,2 = Fiedern Variante A, 3 = Stiel A; 4,5,6/7 = B; 8,9,10/11 = C.
# Muster: mittelrippe heller, seitliche aden als hoehe, braune schuppen am
# Fiedergrund, Stiel braun mit laengsrillen.
import math
import random
import colorsys
import numpy as np
from PIL import Image

W = H = 1024
COLS, ROWS = 8, 4
PAD = 0.04                        # wie im FBX (uv_cell-Padding)
LEAF_CELLS = [0, 1, 2]
STEM_CELLS = [3]
OUT_A = 'Assets/_Project/Art/Plants/Farn_Tropisch/Textures/FarnTropisch_Albedo.png'
OUT_N = 'Assets/_Project/Art/Plants/Farn_Tropisch/Textures/FarnTropisch_Normal.png'


def fern_rgb(t, jitter=0.0):
    """WALD-ZIEL: Farbton 95..130 Grad, Saettigung 0.3..0.6, dunkles Unterholzgruen."""
    h = 0.315 - 0.030 * t + 0.006 * jitter
    s = 0.56 - 0.12 * t
    v = 0.085 + 0.195 * t ** 1.05 + 0.02 * jitter
    return np.array(colorsys.hsv_to_rgb(h % 1.0, min(0.75, s), min(0.42, v)),
                    dtype=np.float32)


def leaf_cell(cw, chh, seed):
    """Albedo + Hoehe einer Fiederzelle: U quer, V laengs (0=Grund, 1=Spitze)."""
    rng = random.Random(seed)
    xs = np.linspace(0.0, 1.0, cw, dtype=np.float32)[None, :]
    # Bildzeile 0 = oberster Rand der Zelle = V=1 = Fiederspitze
    vs = np.linspace(1.0, 0.0, chh, dtype=np.float32)[:, None]
    xr = np.abs(xs - 0.5)
    col = np.zeros((chh, cw, 3), dtype=np.float32)
    for j in range(chh):
        col[j, :, :] = fern_rgb((chh - 1 - j) / max(1, chh - 1))
    # Mittrippe (hell, laengs) und seitliche aden, zur Spitze bogend
    rib = np.exp(-((xr - 0.0) / 0.05) ** 2)
    fq = rng.uniform(7.0, 9.5)
    fph = rng.uniform(0.0, 1.0)
    lat = vs * fq + (xr * 2.0) ** 2 * 1.6 + fph
    vein = np.exp(-(((lat % 1.0) - 0.5) / 0.085) ** 2) * (1.0 - rib)
    # Rand abdunkeln (Silhouette), Spitze schmaler machbar ueber hoehe
    edge = np.clip((xr - 0.34) / 0.16, 0.0, 1.0)
    nz = np.array([[rng.uniform(-1, 1) for _ in range(12)] for _ in range(12)],
                  dtype=np.float32)
    blotch = np.asarray(Image.fromarray(((nz + 1) * 127).astype(np.uint8))
                        .resize((cw, chh), Image.BILINEAR), dtype=np.float32) / 255.0 - 0.5
    hgt = 0.30 * rib + 0.22 * vein + 0.10 * blotch - 0.18 * edge
    a = col[None, :, :] if col.ndim == 2 else col
    a = a * (1.0 + 0.16 * blotch)[..., None]
    a = a * (1.0 - 0.45 * rib)[..., None] + np.array([0.090, 0.130, 0.050], np.float32) * rib[..., None]
    a = a * (1.0 - 0.45 * edge)[..., None]
    # Adern als hellere, gruenere Streifen (Sichtbarkeit im Spiel)
    a = a * (1.0 + 0.22 * vein)[..., None] \
        + np.array([0.030, 0.055, 0.018], np.float32) * vein[..., None]
    # braune Schuppen am Fiedergrund
    sca = np.exp(-((vs - 0.05) / 0.055) ** 2) * (0.55 + 0.45 * blotch * 2.0)
    a = a * (1 - sca[..., None]) + np.array([0.115, 0.075, 0.035], np.float32) * sca[..., None]
    return np.clip(a, 0.0, 1.0), np.clip(hgt, 0.0, 1.0)


def stem_cell(cw, chh, seed):
    """Stielzelle: braun, Laengsrillen (U-Rillen, V=entlang Stiel), Schuppenflecken."""
    rng = random.Random(seed + 5)
    xs = np.linspace(0.0, 1.0, cw, dtype=np.float32)[None, :]
    vs = np.linspace(1.0, 0.0, chh, dtype=np.float32)[:, None]
    col = np.zeros((chh, cw, 3), dtype=np.float32)
    for j in range(chh):
        t = (chh - 1 - j) / max(1, chh - 1)      # 0 = Fuss, 1 = Spitze
        h = 0.075 + 0.030 * t
        s = 0.42 - 0.10 * t
        v = 0.075 + 0.105 * t
        col[j, :, :] = np.array(colorsys.hsv_to_rgb(h, s, v), dtype=np.float32)
    grooves = 0.5 + 0.5 * np.cos((xs * rng.uniform(9.0, 13.0)) * 2 * math.pi)
    rib = np.exp(-((np.abs(xs - 0.5)) / 0.10) ** 2)
    nz = np.array([[rng.uniform(-1, 1) for _ in range(10)] for _ in range(10)],
                  dtype=np.float32)
    fleck = np.asarray(Image.fromarray(((nz + 1) * 127).astype(np.uint8))
                       .resize((cw, chh), Image.BILINEAR), dtype=np.float32) / 255.0
    hgt = 0.45 * grooves + 0.25 * rib + 0.15 * fleck
    a = col * (0.80 + 0.35 * grooves)[..., None] * (0.85 + 0.30 * fleck)[..., None]
    a = a + np.array([0.045, 0.030, 0.012], np.float32) * rib[..., None]
    return np.clip(a, 0.0, 1.0), np.clip(hgt, 0.0, 1.0)


def normal_from_height(hh, strength=2.0):
    """Tangentialraum-Normale aus Hoehe (Bild-y zeigt nach -V)."""
    dh_du = np.gradient(hh, axis=1) * strength
    dh_dv = -np.gradient(hh, axis=0) * strength
    n = np.stack([-dh_du, -dh_dv, np.ones_like(hh)], axis=-1)
    n /= np.linalg.norm(n, axis=-1, keepdims=True)
    return (n * 0.5 + 0.5).astype(np.float32)


def main():
    alb = np.full((H, W, 3), 0.045, dtype=np.float32)
    nrm = np.stack([np.full((H, W), 0.5, np.float32)] * 2 +
                   [np.ones((H, W), np.float32)], axis=-1)
    for i in LEAF_CELLS + STEM_CELLS:
        col, row = i % COLS, i // COLS
        px0 = int(round((col + PAD) / COLS * W)); px1 = int(round((col + 1 - PAD) / COLS * W))
        vtop = (row + 1 - PAD) / ROWS; vbot = (row + PAD) / ROWS
        py0 = int(round((1 - vtop) * H)); py1 = int(round((1 - vbot) * H))
        if i in LEAF_CELLS:
            a, hgt = leaf_cell(px1 - px0, py1 - py0, seed=1200 + i)
            st = 1.5
        else:
            a, hgt = stem_cell(px1 - px0, py1 - py0, seed=1200 + i)
            st = 2.2
        nx = normal_from_height(hgt, strength=st)
        gx0 = int(round(col / COLS * W)); gx1 = int(round((col + 1) / COLS * W))
        gy0 = int(round((1 - (row + 1) / ROWS) * H)); gy1 = int(round((1 - row / ROWS) * H))

        def blit(pat, dst, pad_val):
            pt = np.pad(pat, ((py0 - gy0, gy1 - py1), (px0 - gx0, gx1 - px1), (0, 0)),
                        mode='constant', constant_values=pad_val)
            dst[gy0:gy1, gx0:gx1] = pt
        blit(a, alb, 0.04)
        blit(nx[:, :, :1], nrm[:, :, :1], 0.5)   # X/Y flach, Z bleibt 1
    Image.fromarray((alb * 255).astype(np.uint8)).save(OUT_A)
    Image.fromarray((nrm * 255).astype(np.uint8)).save(OUT_N)
    print('Albedo:', OUT_A, alb.shape, 'max', round(float(alb.max()), 3))
    print('Normal:', OUT_N, nrm.shape)


if __name__ == '__main__':
    main()