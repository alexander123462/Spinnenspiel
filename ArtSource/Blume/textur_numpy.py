# -*- coding: utf-8 -*-
# MODELL18 Blume: Albedo (zwei Varianten: Rosa/Vinca und Gelb/Orange) plus eine
# gemeinsame Normalmap. UV-Atlas 8x5-Zellen wie in build_blume.py:
# 0..2 Stengel, 3..17 Blaetter, 18..32 Kronblaetter, 33..35 Bluetenmitte.
import math, random, colorsys
import numpy as np
from PIL import Image

W, H = 1024, 640
COLS, ROWS = 8, 5
PAD = 0.03                     # identisch zu build_blume.py
OUT_A = 'Assets/_Project/Art/Plants/Blume/Textures/Blume_Albedo_Rosa.png'
OUT_B = 'Assets/_Project/Art/Plants/Blume/Textures/Blume_Albedo_Gelb.png'
OUT_N = 'Assets/_Project/Art/Plants/Blume/Textures/Blume_Normal.png'


def hsv(h, s, v):
    return np.array(colorsys.hsv_to_rgb(h % 1.0, min(1.0, max(0.0, s)),
                                        min(1.0, max(0.0, v))), dtype=np.float32)


def grid(cw, chh):
    xs = np.linspace(0.0, 1.0, cw, dtype=np.float32)[None, :]
    vs = np.linspace(1.0, 0.0, chh, dtype=np.float32)[:, None]  # oben = t=1
    return xs, vs


def noise_field(cw, chh, rng, nb=16):
    nz = np.array([[rng.uniform(-1, 1) for _ in range(nb)] for _ in range(nb)],
                  dtype=np.float32)
    big = Image.fromarray(((nz + 1) * 127).astype(np.uint8)).resize(
        (cw, chh), Image.Resampling.BICUBIC)
    return np.asarray(big, dtype=np.float32) / 127.0 - 1.0


def stem_pattern(cw, chh, seed):
    """Stengelzelle: gruenes Band mit laengsfeiner Faserung, unten dunkler."""
    rng = random.Random(seed)
    xs, vs = grid(cw, chh)
    fb = 0.5 + 0.5 * np.sin(xs * math.pi * 16.0 + rng.uniform(0, 6.28))
    edge = np.exp(-((np.abs(xs - 0.5) - 0.44) / 0.09) ** 2)
    dark = 0.55 + 0.45 * vs                      # Basis dunkler
    col = hsv(0.27, 0.45, 0.24)[None, :] * (0.9 + 0.5 * vs)   # (chh,3)
    shade = (1.0 + 0.22 * fb - 0.30 * edge) * dark
    albedo = col[:, None, :] * shade[..., None]
    height = (0.4 * fb - 0.5 * edge) * np.ones_like(vs)   # auf volle Hoehe strecken
    return np.clip(albedo, 0.0, 0.32), height


def leaf_pattern(cw, chh, seed):
    """Lanzenblatt: Mittelrippe, steil abstehende Seitenadern, Fuss dunkel."""
    rng = random.Random(seed)
    xs, vs = grid(cw, chh)
    xr = np.abs(xs - 0.5)
    rib = np.exp(-(xr / 0.045) ** 2)
    vein = np.exp(-(((vs * 9.0 + xr * 2.2) % 1.0 - 0.5) / 0.10) ** 2)
    vein *= np.clip(0.25 + vs * 0.75, 0, 1)
    nz = noise_field(cw, chh, rng)
    ramp = np.array([hsv(0.345 - 0.030 * vv, 0.50 - 0.12 * vv,
                         0.13 + 0.20 * vv ** 1.1) for vv in vs[:, 0]],
                    dtype=np.float32)
    shade = (1.0 + 0.55 * rib - 0.30 * vein + 0.10 * nz)
    albedo = ramp[:, None, :] * shade[..., None]
    height = rib * 0.8 + vein * 0.25 + 0.10 * nz
    return np.clip(albedo, 0.0, 0.40), height


PALETTES = {
    # (h0,s0,v0) Fuss/Kehle  ->  (h1,s1,v1) Spitze   (Vinca-rosa / gelb-orange)
    'rosa': ((0.86, 0.55, 0.38), (0.93, 0.26, 0.72)),
    'gelb': ((0.045, 0.82, 0.46), (0.10, 0.75, 0.58)),
}
EYE = {'rosa': (hsv(0.80, 0.62, 0.15), hsv(0.90, 0.42, 0.50)),
       'gelb': (hsv(0.07, 0.72, 0.22), hsv(0.11, 0.55, 0.62))}


def petal_pattern(cw, chh, seed, var):
    """Kronblatt: Farbverlauf Kehle->Spitze, 5 feine Adern, hochgerollter Rand."""
    rng = random.Random(seed)
    xs, vs = grid(cw, chh)
    (h0, s0, v0), (h1, s1, v1) = PALETTES[var]
    t = vs ** 1.05
    col = np.array([hsv(h0 + (h1 - h0) * vv, s0 + (s1 - s0) * vv,
                        v0 + (v1 - v0) * vv) for vv in t[:, 0]], dtype=np.float32)
    xr = np.abs(xs - 0.5)
    veins = np.zeros_like(xs) + np.zeros_like(vs)
    for k in range(-2, 3):
        pos = 0.5 + k * 0.155 * (0.35 + 0.65 * vs)
        veins += np.exp(-(((xs - pos) / 0.013) ** 2)) * (0.25 + 0.45 * vs)
    edge = np.exp(-(((xr - 0.47) / 0.07) ** 2))
    ribc = np.exp(-((xr / 0.06) ** 2))
    nz = noise_field(cw, chh, rng)
    shade = (1.0 - 0.12 * veins - 0.18 * edge + 0.10 * ribc + 0.06 * nz)
    albedo = col[:, None, :] * shade[..., None]
    height = 0.3 * ribc + 0.30 * veins + 0.25 * edge + 0.08 * nz
    return np.clip(albedo, 0.0, 0.78), height


def center_pattern(cw, chh, seed, var):
    """Bluetenmitte: Trichterwand (t<0.45), Unterband, Augenscheibe, Staubblaetter."""
    rng = random.Random(seed)
    xs, vs = grid(cw, chh)
    pal = PALETTES[var]
    base = hsv(pal[0][0], pal[0][1], pal[0][2] * 0.8)[None, None, :] \
        * np.ones((chh, cw, 1), dtype=np.float32)
    albedo = base.copy()
    height = np.zeros((chh, cw), dtype=np.float32)
    # 1) Trichterwand unten
    fb = 0.5 + 0.5 * np.sin(xs * math.pi * 10.0 + rng.uniform(0, 6.28))
    throat = hsv(pal[0][0], pal[0][1] + 0.05, pal[0][2] * 0.7)
    m_side = np.clip((0.45 - vs) / 0.45, 0, 1)[..., None]
    albedo = albedo * (1 - m_side) + throat[None, None, :] * (1 + 0.2 * fb)[..., None] * m_side
    height += 0.3 * fb * np.clip(0.45 - vs, 0, 1)
    # 2) Unterband (Aussenfarbe des Trichters)
    m_band = ((vs >= 0.455) & (vs <= 0.545))[..., None]
    band = hsv(0.25, 0.30, 0.28)[None, None, :]
    albedo = albedo * (1 - m_band) + band * m_band
    # 3) Augenscheibe (Kap von oben): Ellipse um (xn 0.56, t 0.60)
    e = ((xs - 0.56) / 0.19) ** 2 + ((vs - 0.60) / 0.135) ** 2
    eye_c, ring_c = EYE[var]
    m_ring = ((e >= 0.45) & (e <= 1.0))[..., None]
    m_eye = (e < 0.45)[..., None]
    albedo = albedo * (1 - m_ring) + ring_c[None, None, :] * m_ring
    albedo = albedo * (1 - m_eye) + eye_c[None, None, :] * m_eye
    height += 0.35 * np.clip(1.0 - e, 0, 1)
    # 4) Staubblaetter oben: Staubfaden hell, Anthere pollengelb
    nz = noise_field(cw, chh, rng)
    m_fil = (vs >= 0.80)[..., None]
    fil = hsv(0.19, 0.30, 0.40)[None, None, :]
    albedo = albedo * (1 - m_fil) + fil * m_fil
    m_ant = (vs >= 0.90)[..., None]
    ant = hsv(0.125, 0.50, 0.70)[None, None, :] * (1 + 0.12 * nz)[..., None]
    albedo = albedo * (1 - m_ant) + ant * m_ant
    height += 0.2 * np.clip(vs - 0.80, 0, 1) + 0.5 * np.clip(vs - 0.90, 0, 1)
    return np.clip(albedo, 0.0, 0.75), height


def normal_from_height(hh, strength=2.0):
    """Tangentialraum-Normale aus Hoehe (Bild-y zeigt nach -V)."""
    dh_du = np.gradient(hh, axis=1) * strength
    dh_dv = -np.gradient(hh, axis=0) * strength
    n = np.stack([-dh_du, -dh_dv, np.ones_like(hh)], axis=-1)
    n /= np.linalg.norm(n, axis=-1, keepdims=True)
    return (n * 0.5 + 0.5).astype(np.float32)


def main():
    import os
    for out in (OUT_A, OUT_B, OUT_N):
        os.makedirs(os.path.dirname(out), exist_ok=True)
    albR = np.full((H, W, 3), 0.06, dtype=np.float32)
    albG = np.full((H, W, 3), 0.06, dtype=np.float32)
    nrm = np.stack([np.full((H, W), 0.5, np.float32)] * 2 +
                   [np.ones((H, W), np.float32)], axis=-1)

    def blit(pat, dst, pad_val):
        pt = np.pad(pat, ((py0 - gy0, gy1 - py1), (px0 - gx0, gx1 - px1), (0, 0)),
                    mode='constant', constant_values=pad_val)
        dst[gy0:gy1, gx0:gx1] = pt

    for i in range(36):
        col, row = i % COLS, i // COLS
        px0 = int(round((col + PAD) / COLS * W)); px1 = int(round((col + 1 - PAD) / COLS * W))
        vtop = (row + 1 - PAD) / ROWS; vbot = (row + PAD) / ROWS
        py0 = int(round((1 - vtop) * H)); py1 = int(round((1 - vbot) * H))
        gx0 = int(round(col / COLS * W)); gx1 = int(round((col + 1) / COLS * W))
        gy0 = int(round((1 - (row + 1) / ROWS) * H)); gy1 = int(round((1 - row / ROWS) * H))
        if i < 3:
            a, h = stem_pattern(px1 - px0, py1 - py0, seed=100 + i)
            aR = aG = a
        elif i < 18:
            a, h = leaf_pattern(px1 - px0, py1 - py0, seed=300 + i)
            aR = aG = a
        elif i < 33:
            aR, h = petal_pattern(px1 - px0, py1 - py0, 600 + i, 'rosa')
            aG, _ = petal_pattern(px1 - px0, py1 - py0, 600 + i, 'gelb')
        else:
            aR, h = center_pattern(px1 - px0, py1 - py0, 900 + i, 'rosa')
            aG, _ = center_pattern(px1 - px0, py1 - py0, 900 + i, 'gelb')
        nx = normal_from_height(h, strength=1.2)
        blit(aR, albR, 0.05); blit(aG, albG, 0.05)
        blit(nx[:, :, :1], nrm[:, :, :1], 0.5)   # X/Y flach, Z bleibt 1
    Image.fromarray((albR * 255).astype(np.uint8)).save(OUT_A)
    Image.fromarray((albG * 255).astype(np.uint8)).save(OUT_B)
    Image.fromarray((nrm * 255).astype(np.uint8)).save(OUT_N)
    print('Albedo Rosa:', OUT_A, albR.shape, 'max', round(float(albR.max()), 3))
    print('Albedo Gelb:', OUT_B, albG.shape, 'max', round(float(albG.max()), 3))
    print('Normal:', OUT_N, nrm.shape)


if __name__ == '__main__':
    main()
