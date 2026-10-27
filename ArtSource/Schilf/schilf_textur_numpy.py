# -*- coding: utf-8 -*-
# MODELL11 Schilf: Albedo + Normal fuer schilf_neu.fbx (UV-Atlas):
#  - V 0.03..0.26  Kolben-Streifen (U=Umfang), Kappen-Insel U 0.93..0.98 x V 0.03..0.07
#  - V 0.30..0.52  Halm-Band (U=Umfang, V=Laengs: 0=Fuss, 1=Spitze)
#  - V 0.62..0.95  4 Blattzellen (U 0.03-0.26/0.27-0.50/0.51-0.74/0.75-0.98, V=Blatt)
# Hinweis Helligkeit: MAT_Reed traegt im Builder den Q4-Ton (V 0.42, oliv). Die
# Textur wird DARUEBER multipliziert, daher Texturen-Helligkeit ~0.6-0.85; das
# Endergebnis im Spiel bleibt damit unterhalb des WALD-ZIEL-Werts 0.45.
import math
import random
import colorsys
import numpy as np
from PIL import Image

W = H = 1024
CELLS = [(0.03, 0.26), (0.27, 0.50), (0.51, 0.74), (0.75, 0.98)]
LV0, LV1 = 0.62, 0.95
SV0, SV1 = 0.30, 0.52
KP0, KP1 = 0.03, 0.26
OUT_DIR = 'ArtSource/Schilf'


def blotch(cw, chh, seed, n=12):
    rng = random.Random(seed)
    nz = np.array([[rng.uniform(-1, 1) for _ in range(n)] for _ in range(n)],
                  dtype=np.float32)
    return np.asarray(Image.fromarray(((nz + 1) * 127).astype(np.uint8))
                      .resize((cw, chh), Image.BILINEAR), dtype=np.float32) / 255.0 - 0.5


def leaf_cell(cw, chh, cell, seed):
    """Schmale Schilfklinge: U quer (0.5=Mittelrippe), V laengs (0=Ansatz,1=Spitze),
    Paralleladern, Rand dunkler, Spitze strohiger."""
    xs = np.linspace(0.0, 1.0, cw, dtype=np.float32)[None, :]
    xr = np.abs(xs - 0.5)
    vs = np.linspace(0.0, 1.0, chh, dtype=np.float32)[:, None]
    col = np.zeros((chh, cw, 3), dtype=np.float32)
    for j in range(chh):
        hj = (0.278 + 0.014 * ((cell[0] * 97) % 1.0) + 0.05 * (j / (chh - 1))) % 1.0
        sj = 0.40 - 0.12 * (j / (chh - 1))
        vj = 0.72 + 0.10 * (j / (chh - 1))
        col[j, :, :] = colorsys.hsv_to_rgb(hj, sj, vj)
    rib = np.exp(-(xr / 0.06) ** 2)                                 # Mittelrippe
    fq = random.Random(seed).uniform(14.0, 20.0)
    lat = vs * fq + (xr * 2.0) ** 2 * 1.1                           # Paralleladern
    vein = np.exp(-(((lat % 1.0) - 0.5) / 0.10) ** 2) * (1.0 - rib)  # schaerfer
    edge = np.clip((xr - 0.42) / 0.08, 0.0, 1.0)                    # Randabdunklung
    bl = blotch(cw, chh, seed)
    hgt = 0.55 * rib + 0.35 * vein + 0.10 * bl - 0.20 * edge
    a = col * (1.0 + 0.10 * bl)[..., None]
    a = a * (1.0 - 0.35 * rib)[..., None] \
        + np.array([0.42, 0.48, 0.26], np.float32) * rib[..., None]  # Rippe heller
    a = a * (1.0 - 0.30 * edge)[..., None]
    a = a * (1.0 + 0.30 * vein)[..., None] \
        + np.array([0.06, 0.10, 0.02], np.float32) * vein[..., None]
    straw = np.exp(-((vs - 0.90) / 0.09) ** 2)                      # Spitze strohiger (innen!)
    a = a * (1 - straw[..., None]) + np.array([0.62, 0.58, 0.34], np.float32) * straw[..., None]
    return np.clip(a, 0.0, 1.0), np.clip(hgt, 0.0, 1.0)


def stem_band(cw, chh, seed):
    """Halm: gelblich-gruener Grund, feine Langsstreifen (Fasern), Knotenzone
    nicht texturiert (steht als Geometrie da), Grund hell (Q4-Ton multipliziert)."""
    u = np.linspace(0.0, 1.0, cw, dtype=np.float32)[None, :]
    v = np.linspace(0.0, 1.0, chh, dtype=np.float32)[:, None]
    col = np.zeros((chh, cw, 3), dtype=np.float32)
    for j in range(chh):
        hj = (0.245 + 0.02 * (j / (chh - 1))) % 1.0                 # 88..96 Grad
        col[j, :, :] = colorsys.hsv_to_rgb(hj, 0.26, 0.78 + 0.06 * (j / (chh - 1)))
    fib = np.sin(u * math.pi * 46.0) * 0.5 + blotch(cw, chh, seed, n=24) * 0.7
    hgt = 0.35 * (fib * 0.5 + 0.5)
    a = col * (1.0 + 0.18 * fib)[..., None]   # D2: Halmfasern deutlicher
    dry = np.exp(-((v - 1.0) / 0.22) ** 2)                          # oben strohiger
    a = a * (1 - 0.5 * dry[..., None]) + np.array([0.60, 0.55, 0.32], np.float32) * dry[..., None]
    return np.clip(a, 0.0, 1.0), np.clip(hgt, 0.0, 1.0)


def spike_band(cw, chh, seed):
    """Rohrkolben: dunkelbraun, feine vertikale Fasern, dunkle Sprenkel."""
    u = np.linspace(0.0, 1.0, cw, dtype=np.float32)[None, :]
    v = np.linspace(0.0, 1.0, chh, dtype=np.float32)[:, None]
    base = np.array(colorsys.hsv_to_rgb(0.075, 0.42, 0.62), dtype=np.float32)
    fib = np.sin(u * math.pi * 90.0 + blotch(cw, chh, seed, n=30) * 3.0)
    speck = blotch(cw, chh, seed + 5, n=48)
    hgt = 0.45 * (fib * 0.5 + 0.5) + 0.15 * (speck + 0.5)
    a = base[None, None, :] * (1.0 + 0.35 * fib)[..., None] * (1.0 + 0.30 * speck)[..., None]  # D4: samtiger
    tip = np.exp(-((v - 1.0) / 0.12) ** 2)                          # zur Spitze heller
    a = a * (1 - 0.4 * tip[..., None]) + np.array([0.55, 0.44, 0.26], np.float32) * tip[..., None]
    return np.clip(a, 0.0, 1.0), np.clip(hgt, 0.0, 1.0)


def normal_from_height(hh, strength=2.2):
    gx = (np.roll(hh, -1, axis=1) - np.roll(hh, 1, axis=1)) * strength
    gy = (np.roll(hh, -1, axis=0) - np.roll(hh, 1, axis=0)) * strength
    nz = 1.0 / np.sqrt(1.0 + gx * gx + gy * gy)
    return np.stack([(-gx) * 0.5 + 0.5, (-gy) * 0.5 + 0.5, nz * 0.5 + 0.5], axis=2)


def main():
    alb = np.zeros((H, W, 3), dtype=np.float32)
    hgt = np.zeros((H, W), dtype=np.float32)

    def put(u0, u1, v0, v1, ca, ch_):
        x0, x1 = int(u0 * W), int(u1 * W)
        y0 = int(v0 * H)
        hh_, ww_ = ca.shape[0], ca.shape[1]      # Quellgroesse bestimmt Zielblock
        alb[H - y0 - hh_:H - y0, x0:x0 + ww_, :] = ca[::-1]   # V=0 unten -> PIL-zeilen umdrehen
        hgt[H - y0 - hh_:H - y0, x0:x0 + ww_] = ch_[::-1]

    # Kolben-Streifen (V 0.03..0.26) + Kappen-Insel
    ca, ch_ = spike_band(W, int((KP1 - KP0) * H), 77)
    put(0.0, 1.0, KP0, KP1, ca, ch_)
    capc = np.array(colorsys.hsv_to_rgb(0.07, 0.45, 0.50), dtype=np.float32)
    alb[int((1 - 0.07) * H):int((1 - 0.03) * H), int(0.93 * W):int(0.98 * W), :] = capc

    # Halm-Band (V 0.30..0.52)
    ca, ch_ = stem_band(W, int((SV1 - SV0) * H), 31)
    put(0.0, 1.0, SV0, SV1, ca, ch_)

    # Blattzellen (V 0.62..0.95)
    for ci, (u0, u1) in enumerate(CELLS):
        ca, ch_ = leaf_cell(int((u1 - u0) * W), int((LV1 - LV0) * H), (u0, u1), 100 + ci)
        put(u0, u1, LV0, LV1, ca, ch_)

    # Randpolster: Zellenrander mit Nachbarfarbe anduten? Nein: neutraler Mittelfuell.
    nrm = normal_from_height(hgt, strength=2.4)
    Image.fromarray((alb * 255).astype(np.uint8)).save(OUT_DIR + '/Schilf_Albedo.png')
    Image.fromarray((nrm * 255).astype(np.uint8)).save(OUT_DIR + '/Schilf_Normal.png')
    print('Albedo:', OUT_DIR + '/Schilf_Albedo.png', 'max', round(float(alb.max()), 3))
    print('Normal:', OUT_DIR + '/Schilf_Normal.png')


main()
