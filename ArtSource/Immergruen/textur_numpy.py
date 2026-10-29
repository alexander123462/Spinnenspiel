# -*- coding: utf-8 -*-
# MODELL4 Immergruen: Albedo + Normalmap je Atlas-Zelle (8x4-Raster, v je Blatt
# bzw. je Rankensegment). Albedo: Periwinkle-Scan als dezenter Hochpass,
# Farbverlauf Fuss dunkel -> Spitze heller, helle Mittelrippe, zur Spitze
# gebogene Seitenadern (Vinca-Typ). Zellen 29..31 = Ranken (braengruen, Fasern).
import math, random, colorsys
import numpy as np
from PIL import Image, ImageFilter

W = H = 1024
COLS, ROWS = 8, 4
PAD = 0.03                      # wie im FBX (cell-Padding in UV)
SCAN = 'Assets/_Project/Art/Scans/Models/periwinkle_plant/periwinkle_plant_albedo.png'
OUT_A = 'Assets/_Project/Art/Plants/Immergruen/Textures/Immergruen_Albedo.png'
OUT_N = 'Assets/_Project/Art/Plants/Immergruen/Textures/Immergruen_Normal.png'
N_LEAF = 29                     # Zellen 0..28 = Blaetter, 29..31 = Ranken


def ramp_row(t):
    """Ziel-Farbverlauf Fuss->Spitze (WALD-ZIEL: Farbton 95..130, Saett 0.3..0.5)."""
    h = 0.345 - 0.030 * t           # 124 -> 113 Grad (etwas gelblicher zur Spitze)
    s = 0.56 - 0.13 * t             # Fuss gesaettigter als Spitze
    val = 0.13 + 0.22 * t ** 1.15   # dunkles Ledergruen, Spitze heller (max ~0.35)
    return np.array(colorsys.hsv_to_rgb(h, s, val), dtype=np.float32)


def leaf_pattern(cw, chh, seed):
    """Albedo- und Hoehe-Feld einer Blattzelle (cw x chh Pixel)."""
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
    valid = (blur > 0.25) & (blur < 0.62)  # nur Blatt-Innenflaechen (keine Blueten)
    hp = np.where(valid, hp, 0.0)
    # Mittlere Rippe: schmaler Streifen mit dunklem Rand links/rechts
    xr = np.abs(xs - 0.5)
    rib = np.exp(-(xr / 0.035) ** 2)
    rib_edge = np.exp(-((xr - 0.042) / 0.016) ** 2)
    # Seitenadern (Vinca): von der Rippe zur Spitze hin stark uebergebogen
    xn2 = (xr * 2.0) ** 2
    ph = vs * 6.0 + xn2 * 3.6
    vein = np.exp(-(((ph % 1.0) - 0.5) / 0.085) ** 2)
    vein = vein * np.clip(0.30 + vs * 0.70, 0, 1)             # unten schwacher
    vein = vein * (1.0 - rib)                                 # Rippe bleibt glatt
    # sanftes 2D-Fleck-Rauschen
    nb = 16
    nz = np.array([[rng.uniform(-1, 1) for _ in range(nb)] for _ in range(nb)],
                  dtype=np.float32)
    big = Image.fromarray(((nz + 1) * 127).astype(np.uint8)).resize(
        (cw, chh), Image.Resampling.BICUBIC)
    noise = np.asarray(big, dtype=np.float32) / 127.0 - 1.0

    shade = (1.0 + 0.62 * rib - 0.28 * rib_edge - 0.40 * vein
             + 0.03 * hp + 0.10 * noise)
    # Stielzone unten: kurz, hellgruenlich (Vinca blattstiele)
    stalk = np.clip((0.06 - vs) / 0.06, 0, 1) ** 2
    ramp = np.array([ramp_row(vv) for vv in vs[:, 0]], dtype=np.float32)
    stalk_col = np.array(colorsys.hsv_to_rgb(0.24, 0.35, 0.18), dtype=np.float32)
    albedo = ramp[:, None, :] * np.expand_dims(shade, -1)
    albedo = albedo * (1 - stalk[..., None]) + stalk_col[None, None, :] * stalk[..., None]
    height = rib * 0.8 + vein * 0.3 + 0.12 * noise + 0.06 * hp
    return np.clip(albedo, 0.0, 0.42), height


def stem_pattern(cw, chh, seed):
    """Rankenzelle: braengruenes Band mit laengsfeiner Faserung."""
    rng = random.Random(seed)
    xs = np.linspace(0.0, 1.0, cw, dtype=np.float32)[None, :]
    base = np.array(colorsys.hsv_to_rgb(0.17, 0.38, 0.17), dtype=np.float32)
    # Faserung: laengs zur V-Achse verlaufende Streifen (Wellen ueber U)
    fb = 0.5 + 0.5 * np.sin(xs * math.pi * 14.0 + rng.uniform(0, 6.28))
    edge = np.exp(-((np.abs(xs - 0.5) - 0.42) / 0.10) ** 2)   # dunklere Raender
    vs1 = np.ones((chh, 1), dtype=np.float32)                 # auf volle Höhe strecken
    shade = (1.0 + 0.25 * fb - 0.35 * edge) * vs1
    albedo = base[None, None, :] * shade[..., None]
    height = (0.4 * fb - 0.5 * edge) * vs1
    return np.clip(albedo, 0.0, 0.30), height


def normal_from_height(hh, strength=2.0):
    """Tangentialraum-Normale aus Hoehe (Bild-y zeigt nach -V)."""
    dh_du = np.gradient(hh, axis=1) * strength
    dh_dv = -np.gradient(hh, axis=0) * strength
    n = np.stack([-dh_du, -dh_dv, np.ones_like(hh)], axis=-1)
    n /= np.linalg.norm(n, axis=-1, keepdims=True)
    return (n * 0.5 + 0.5).astype(np.float32)


def main():
    alb = np.full((H, W, 3), 0.06, dtype=np.float32)
    nrm = np.stack([np.full((H, W), 0.5, np.float32)] * 2 +
                   [np.ones((H, W), np.float32)], axis=-1)
    for i in range(N_LEAF + 3):
        col, row = i % COLS, i // COLS
        px0 = int(round((col + PAD) / COLS * W)); px1 = int(round((col + 1 - PAD) / COLS * W))
        vtop = (row + 1 - PAD) / ROWS; vbot = (row + PAD) / ROWS
        py0 = int(round((1 - vtop) * H)); py1 = int(round((1 - vbot) * H))
        if i < N_LEAF:
            a, hgt = leaf_pattern(px1 - px0, py1 - py0, seed=900 + i)
        else:
            a, hgt = stem_pattern(px1 - px0, py1 - py0, seed=1200 + i)
        nx = normal_from_height(hgt, strength=1.2)
        gx0 = int(round(col / COLS * W)); gx1 = int(round((col + 1) / COLS * W))
        gy0 = int(round((1 - (row + 1) / ROWS) * H)); gy1 = int(round((1 - row / ROWS) * H))

        def blit(pat, dst, pad_val):
            # Grabenrand: neutraler Randwert (kein Ausbluten ueber Zellrander)
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
