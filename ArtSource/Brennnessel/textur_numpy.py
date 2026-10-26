# -*- coding: utf-8 -*-
# MODELL5 Brennnessel: Albedo + Normalmap je Atlas-Zelle (8x4-Raster, V entlang
# der Mittelrippe). Zellen 0..11 = Blaetter, 12..21 = Blattstiele, 24 = Stengel.
# Brennnessel-Muster: drei Hauptnerven von der Basis, leiternartige Seitenadern,
# helle Nerven auf dunkelgruenem Feld; Nessel-Stengel mit hellen Kantenstreifen.
import math, random, colorsys
import numpy as np
from PIL import Image, ImageFilter

W = H = 1024
COLS, ROWS = 8, 4
PAD = 0.03                      # wie im FBX (cell-Padding in UV)
SCAN = 'Assets/_Project/Art/Scans/Models/nettle_plant/nettle_plant_albedo.png'
OUT_A = 'Assets/_Project/Art/Plants/Brennnessel/Textures/Brennnessel_Albedo.png'
OUT_N = 'Assets/_Project/Art/Plants/Brennnessel/Textures/Brennnessel_Normal.png'
N_LEAF = 12                     # Zellen 0..11 Blaetter, 12..21 Stiele, 24 Stengel


def ramp_row(t):
    """Ziel-Farbverlauf Basis->Spitze (WALD-ZIEL: Farbton 95..130, Saett 0.3..0.5)."""
    h = 0.330 - 0.028 * t           # 119 -> 109 Grad
    s = 0.52 - 0.10 * t             # Basis gesaettigter
    val = 0.115 + 0.20 * t ** 1.1   # dunkles Nesselgruen, Spitze heller (~0.31)
    return np.array(colorsys.hsv_to_rgb(h, s, val), dtype=np.float32)


def leaf_pattern(cw, chh, seed):
    """Albedo- und Hoehe-Feld einer Brennnessel-Blattzelle (cw x chh Pixel)."""
    rng = random.Random(seed)
    xs = np.linspace(0.0, 1.0, cw, dtype=np.float32)[None, :]
    vs = np.linspace(1.0, 0.0, chh, dtype=np.float32)[:, None]   # oben = Spitze
    # Scan nur als dezentes Feindetail (weichzeichnen -> Hochpass)
    sy = rng.randint(0, scan.shape[0] - 500); sx = rng.randint(0, scan.shape[1] - 500)
    crop = Image.fromarray((scan[sy:sy + 500, sx:sx + 500] * 255).astype(np.uint8))
    crop = crop.resize((cw, chh)).filter(ImageFilter.GaussianBlur(1.5))
    lum = np.asarray(crop.convert('L'), dtype=np.float32) / 255.0
    blur = np.asarray(crop.filter(ImageFilter.GaussianBlur(6)).convert('L'),
                      dtype=np.float32) / 255.0
    hp = np.clip((lum - blur) * 4.0, -1.0, 1.0)
    valid = (blur > 0.22) & (blur < 0.66)
    hp = np.where(valid, hp, 0.0)
    xr = np.abs(xs - 0.5)
    rib = np.exp(-(xr / 0.030) ** 2)
    rib_edge = np.exp(-((xr - 0.038) / 0.015) ** 2)
    # Nessel-Nerven: zwei starke Basalnerven (bogenformig von der Anheftung zur
    # Blatthalfte) + leiternartige Seitenadern, Frequenz je Blatt variiert
    mv = np.clip(vs, 0.0, 1.0)
    target = 0.40 * np.sin(math.pi * mv ** 0.85)        # Bogen der Basalnerven
    main2 = np.exp(-((xr - target) / 0.024) ** 2)
    main2 = main2 * np.clip(1.25 - 1.25 * mv, 0.0, 1.0)  # zur Spitze auslaufend
    fq = rng.uniform(6.5, 8.5); fph = rng.uniform(0.0, 1.0)
    lat = mv * fq + (xr * 2.0) ** 2 * 2.2 + fph          # zur Spitze bogend
    vein = np.exp(-(((lat % 1.0) - 0.5) / 0.075) ** 2)
    vein = np.maximum(vein, 1.15 * main2)
    vein = vein * np.clip(0.25 + mv * 0.75, 0, 1)
    vein = vein * (1.0 - rib)
    nb = 16
    nz = np.array([[rng.uniform(-1, 1) for _ in range(nb)] for _ in range(nb)],
                  dtype=np.float32)
    big = Image.fromarray(((nz + 1) * 127).astype(np.uint8)).resize(
        (cw, chh), Image.Resampling.BICUBIC)
    noise = np.asarray(big, dtype=np.float32) / 127.0 - 1.0

    shade = (1.0 + 0.55 * rib - 0.25 * rib_edge + 0.45 * vein
             + 0.03 * hp + 0.10 * noise)
    ramp = np.array([ramp_row(vv) for vv in vs[:, 0]], dtype=np.float32)
    albedo = ramp[:, None, :] * np.expand_dims(shade, -1)
    height = rib * 0.7 + vein * 0.45 + 0.12 * noise + 0.06 * hp
    return np.clip(albedo, 0.0, 0.45), height


def stem_pattern(cw, chh, seed, petiole=False):
    """Stengelzelle: gruen-braunes Band, helle Kantenstreifen (4-kantige Rippen),
    dunklere Felder dazwischen; Blattstiel wie Stengel, nur feiner."""
    rng = random.Random(seed)
    xs = np.linspace(0.0, 1.0, cw, dtype=np.float32)[None, :]
    h = 0.24 if petiole else 0.20
    base = np.array(colorsys.hsv_to_rgb(h, 0.42, 0.16), dtype=np.float32)
    # Laengsfasern: feine Streifen entlang V
    fb = 0.5 + 0.5 * np.sin(xs * math.pi * (10 if petiole else 18) + rng.uniform(0, 6.28))
    rib_l = np.exp(-((xs - 0.14) / 0.05) ** 2)      # helle Rippen (Ecken)
    rib_r = np.exp(-((xs - 0.86) / 0.05) ** 2)
    edge = np.exp(-((np.abs(xs - 0.5) - 0.47) / 0.07) ** 2)  # dunkle Raender
    vs1 = np.ones((chh, 1), dtype=np.float32)                 # auf volle Hoehe strecken
    shade = (1.0 + 0.30 * fb + 0.55 * (rib_l + rib_r) - 0.45 * edge) * vs1
    albedo = base[None, None, :] * shade[..., None]
    height = (0.5 * (rib_l + rib_r) + 0.15 * fb - 0.4 * edge) * vs1
    return np.clip(albedo, 0.0, 0.32), height


def normal_from_height(hh, strength=2.0):
    """Tangentialraum-Normale aus Hoehe (Bild-y zeigt nach -V)."""
    dh_du = np.gradient(hh, axis=1) * strength
    dh_dv = -np.gradient(hh, axis=0) * strength
    n = np.stack([-dh_du, -dh_dv, np.ones_like(hh)], axis=-1)
    n /= np.linalg.norm(n, axis=-1, keepdims=True)
    return (n * 0.5 + 0.5).astype(np.float32)


CELLS = list(range(0, 22)) + [24]   # 0..11 Blaetter, 12..21 Stiele, 24 Stengel


def main():
    alb = np.full((H, W, 3), 0.06, dtype=np.float32)
    nrm = np.stack([np.full((H, W), 0.5, np.float32)] * 2 +
                   [np.ones((H, W), np.float32)], axis=-1)
    for i in CELLS:
        col, row = i % COLS, i // COLS
        px0 = int(round((col + PAD) / COLS * W)); px1 = int(round((col + 1 - PAD) / COLS * W))
        vtop = (row + 1 - PAD) / ROWS; vbot = (row + PAD) / ROWS
        py0 = int(round((1 - vtop) * H)); py1 = int(round((1 - vbot) * H))
        if i < N_LEAF:
            a, hgt = leaf_pattern(px1 - px0, py1 - py0, seed=700 + i)
        else:
            a, hgt = stem_pattern(px1 - px0, py1 - py0, seed=940 + i, petiole=(i < 22))
        nx = normal_from_height(hgt, strength=1.2)
        gx0 = int(round(col / COLS * W)); gx1 = int(round((col + 1) / COLS * W))
        gy0 = int(round((1 - (row + 1) / ROWS) * H)); gy1 = int(round((1 - row / ROWS) * H))

        def blit(pat, dst, pad_val):
            # Grabenrand: neutraler Randwert (kein Ausbluten ueber Zellraender)
            pt = np.pad(pat, ((py0 - gy0, gy1 - py1), (px0 - gx0, gx1 - px1), (0, 0)),
                        mode='constant', constant_values=pad_val)
            dst[gy0:gy1, gx0:gx1] = pt
        blit(a, alb, 0.05)
        blit(nx[:, :, :1], nrm[:, :, :1], 0.5)   # X/Y flach, Z bleibt 1
    Image.fromarray((alb * 255).astype(np.uint8)).save(OUT_A)
    Image.fromarray((nrm * 255).astype(np.uint8)).save(OUT_N)
    print('Albedo:', OUT_A, alb.shape, 'max', round(float(alb.max()), 3))
    print('Normal:', OUT_N, nrm.shape)


scan = np.asarray(Image.open(SCAN).convert('RGB'), dtype=np.float32) / 255.0
if __name__ == '__main__':
    main()
