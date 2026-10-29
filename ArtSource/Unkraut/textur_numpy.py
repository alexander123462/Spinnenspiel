# -*- coding: utf-8 -*-
# MODELL3 Unkraut: Albedo + Normalmap pro Blattzelle (4x3-Raster, v je Blatt).
# Albedo: Loewenzahn-Scan (dandelion_01_albedo.png) als Fein-Hochpass, Farb-
# verlauf Fuss dunkel -> Spitze gelbgruen, helle Mittelrippe, dunkle Seitenadern.
# Normalmap: Hoehe aus Rippe + Adern + Rauschen.
import math, random
import numpy as np
from PIL import Image, ImageFilter

W = H = 1024
COLS, ROWS = 4, 3
PAD = 0.03                      # wie im FBX (cell-Padding in UV)
SCAN = 'Assets/_Project/Art/Scans/Models/dandelion_01/dandelion_01_albedo.png'
OUT_A = 'Assets/_Project/Art/Plants/Unkraut/Textures/Unkraut_Albedo.png'
OUT_N = 'Assets/_Project/Art/Plants/Unkraut/Textures/Unkraut_Normal.png'


def ramp_row(t):
    """Ziel-Farbverlauf Fuss->Spitze (WALD-ZIEL: Farbton 95..130, Saett 0.3..0.5)."""
    import colorsys
    h = 0.33 - 0.04 * t             # 119 -> 105 Grad (gelblicher zur Spitze)
    s = 0.50 - 0.12 * t             # Fuss gesaettigter als Spitze
    val = 0.15 + 0.27 * t ** 1.1    # Fuss dunkel, Spitze heller (max ~0.42)
    return np.array(colorsys.hsv_to_rgb(h, s, val), dtype=np.float32)


def cell_pattern(cw, chh, seed):
    """Albedo- und Hoehe-Feld einer Blattzelle (cw x chh Pixel).
    xn: 0..1 ueber die Breite, t: 0..1 entlang der Mittelrippe."""
    rng = random.Random(seed)
    xs = np.linspace(0.0, 1.0, cw, dtype=np.float32)[None, :]
    vs = np.linspace(1.0, 0.0, chh, dtype=np.float32)[:, None]   # oben = Spitze
    # Scan nur als sehr dezentes Feindetail: erst weichzeichnen, dann Hochpass,
    # und nur mittlere Tonwerte zulassen (Blattrand-Kanten = Stern-Artefakte)
    sy = rng.randint(0, scan.shape[0] - 500); sx = rng.randint(0, scan.shape[1] - 500)
    crop = Image.fromarray((scan[sy:sy + 500, sx:sx + 500] * 255).astype(np.uint8))
    crop = crop.resize((cw, chh)).filter(ImageFilter.GaussianBlur(1.5))
    lum = np.asarray(crop.convert('L'), dtype=np.float32) / 255.0
    blur = np.asarray(crop.filter(ImageFilter.GaussianBlur(6)).convert('L'),
                      dtype=np.float32) / 255.0
    hp = np.clip((lum - blur) * 4.0, -1.0, 1.0)
    valid = (blur > 0.22) & (blur < 0.70)          # nur Blatt-Innenflaechen
    hp = np.where(valid, hp, 0.0)

    # Mittlere Rippe: schmaler Streifen mit dunklem Rand links/rechts
    xr = np.abs(xs - 0.5)
    rib = np.exp(-(xr / 0.038) ** 2)
    rib_edge = np.exp(-((xr - 0.045) / 0.018) ** 2)
    # Seitenadern: von der Rippe aus zur Spitze hin gebogen (xn^2-Kruemmung)
    ph = vs * 7.5 + (xs - 0.5) ** 2 * 3.2
    vein = np.exp(-(((ph % 1.0) - 0.5) / 0.075) ** 2)
    vein = vein * np.clip(0.25 + vs * 0.75, 0, 1)          # unten schwacher
    vein = vein * (1.0 - rib)                              # Rippe bleibt glatt
    # sanftes 2D-Fleck-Rauschen
    nb = 16
    nz = np.array([[rng.uniform(-1, 1) for _ in range(nb)] for _ in range(nb)],
                  dtype=np.float32)
    big = Image.fromarray(((nz + 1) * 127).astype(np.uint8)).resize(
        (cw, chh), Image.Resampling.BICUBIC)
    noise = np.asarray(big, dtype=np.float32) / 127.0 - 1.0

    shade = (1.0 + 0.55 * rib - 0.30 * rib_edge - 0.45 * vein
             + 0.04 * hp + 0.10 * noise)   # Scan-Hochpass nur noch minimal
    # Stielzone unten: dezent heller, leicht roetlich (Loewenzahn-Stiele)
    stalk = np.clip((0.07 - vs) / 0.07, 0, 1) ** 2
    ramp = np.array([ramp_row(vv) for vv in vs[:, 0]], dtype=np.float32)
    stalk_col = np.array(colorsys_hsv(0.10, 0.22, 0.28), dtype=np.float32)
    albedo = ramp[:, None, :] * np.expand_dims(shade, -1)
    albedo = albedo * (1 - stalk[..., None]) + stalk_col[None, None, :] * stalk[..., None]

    # Hoehe: Rippe hoch, Adern erhaben, Rauschen
    height = rib * 1.0 + vein * 0.5 + 0.12 * noise + 0.06 * hp
    albedo = np.clip(albedo, 0.0, 0.45)
    return albedo, height


def colorsys_hsv(h, s, v):
    import colorsys
    return colorsys.hsv_to_rgb(h, s, v)


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
    for i in range(12):
        col, row = i % COLS, i // COLS
        # UV-Zelle -> Bildrechteck (Bild-y zeigt nach -V)
        px0 = int(round((col + PAD) / COLS * W)); px1 = int(round((col + 1 - PAD) / COLS * W))
        vtop = (row + 1 - PAD) / ROWS; vbot = (row + PAD) / ROWS
        py0 = int(round((1 - vtop) * H)); py1 = int(round((1 - vbot) * H))
        a, hgt = cell_pattern(px1 - px0, py1 - py0, seed=700 + i)
        nx = normal_from_height(hgt)
        gx0 = int(round(col / COLS * W)); gx1 = int(round((col + 1) / COLS * W))
        gy0 = int(round((1 - (row + 1) / ROWS) * H)); gy1 = int(round((1 - row / ROWS) * H))

        def blit(pat, dst, pad_val):
            # Grabenrand: neutraler Randwert (kein Ausbluten, kein Naht-Artefakt)
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

