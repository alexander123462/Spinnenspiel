# MODELL15: Baumfarn-Blatt-Atlas (4x4 Zellen) passend zu baumfarn_geometrie.py.
# Aufruf: python tex_baumfarn_atlas.py <outdir>
# Zellen: Wedel j%16 -> Zelle j (0..12), Innenwedel 14, Kroziers 15.
# Zellinhalt (relativ 0..1 in der Zelle): Rachis-Band v 0.08..0.21,
# linke Fiederbahn Mitte 0.40, rechte 0.78, je +-0.18 hoch, q entlang u.
import numpy as np
from scipy.ndimage import gaussian_filter
from PIL import Image
import sys, os

out = sys.argv[1] if len(sys.argv) > 1 else "tex"
os.makedirs(out, exist_ok=True)

N = 2048            # Atlas-Kantenlaenge
CELLS = 4           # 4x4
CS = N // CELLS     # Zellgroesse in px


def noise(scale, seed, shape):
    r = np.random.default_rng(seed).standard_normal(shape)
    n = gaussian_filter(r, scale, mode="wrap")
    return (n - n.mean()) / (n.std() + 1e-6)


# Atlas-Felder in v-oben-Indexierung (Zeile 0 = v=0), beim Speichern gedreht.
alb = np.zeros((N, N, 4))
hgt = np.zeros((N, N))
mask_ao = np.ones((N, N))

# Grund: dunkles Waldgruen mit weicher Fleckigkeit (WALD-ZIEL: Farbton ~105)
gn = noise(24, 101, (N, N))
gn2 = noise(60, 103, (N, N))
base_g = np.clip(0.55 + 0.35 * gn2, 0, 1)
GREEN_D = np.array([0.055, 0.17, 0.035])
GREEN_L = np.array([0.115, 0.28, 0.065])
bgc = GREEN_D[None, None] * (1 - base_g[..., None]) + \
      GREEN_L[None, None] * base_g[..., None]
bgc = bgc * (1 + 0.10 * gn[..., None])
alb[..., :3] = np.clip(bgc, 0, 1)
alb[..., 3] = 1.0

def paint_frond_cell(idx, seed):
    """ malt Wedel-Muster in Zelle idx (0..12, 14)."""
    cx, cy = idx % CELLS, idx // CELLS
    y0, x0 = cy * CS, cx * CS
    ln = np.random.default_rng(seed)
    uc = (np.arange(CS) + 0.5) / CS
    vc = (np.arange(CS) + 0.5) / CS
    U, V = np.meshgrid(uc, vc)
    # Envelope der Fiederbaender (duenn an der Wedelbasis, satt zur Spitze):
    env = np.clip(np.sin(np.pi * np.clip(U, 0, 1) ** 0.75) ** 0.5 * 1.6, 0, 1)
    e = 0.02 + 0.16 * env
    # Rachis-Mittelband:
    dR = np.abs(V - 0.144) / 0.068
    rach = np.clip(1.0 - dR, 0, 1)
    midR = np.exp(-((V - 0.144) / 0.010) ** 2) * (dR < 1)
    # zwei Fiederbahnen:
    out_col = alb[y0:y0 + CS, x0:x0 + CS, :3].copy()
    out_h = np.zeros((CS, CS))
    for vcm, sgn in ((0.40, 1), (0.78, -1)):
        d = np.abs(V - vcm)
        inside = np.clip((e - d) / 0.015, 0, 1)          # scharfer Rand
        midp = np.exp(-((V - vcm) / 0.008) ** 2) * inside
        # Seitenadern: schraeg zur Wedelspitze (u) verlaufende Grate
        ph = (U * 9.0 + (d / np.maximum(e, 1e-4)) * 0.75) % 1.0
        vein = np.exp(-((ph - 0.5) ** 2) / 0.013) * inside
        edge = np.clip((0.03 - (e - d)) / 0.03, 0, 1)    * inside
        # Farbgebung der Bahn:
        pn = noise(4, seed + sgn, (CS, CS))
        cA = np.array([0.075, 0.225, 0.045])             # Fiederrueckten dunkler
        cB = np.array([0.13, 0.30, 0.075])               # hellere Felder
        cc = cA[None, None] * (1 - pn[..., None] * 0.5) + cB[None, None] * (0.5 + pn[..., None] * 0.5)
        cc = cc * (1 - 0.78 * vein[..., None]) + np.array([0.26, 0.40, 0.15])[None, None] * (0.78 * vein)[..., None]
        cc = cc * (1 - 0.75 * midp[..., None]) + np.array([0.30, 0.44, 0.16])[None, None] * (0.75 * midp)[..., None]
        cc = cc * (1 - 0.55 * edge[..., None])           # Rand abschattiert
        mix = inside[..., None]
        out_col = out_col * (1 - mix) + cc * mix
        # Luecken zwischen den Bahnen fast schwarz (liesst Fiederung lesen):
        gap = 1 - np.clip(inside, 0, 1)
        g2 = (np.abs(V - vcm) < 0.30).astype(float)[..., None] * gap[..., None]
        out_col = out_col * (1 - 0.72 * g2)
        out_h += 0.9 * midp + 0.45 * vein + 0.5 * np.clip(d / np.maximum(e, 1e-4), 0, 1) * inside - 0.6 * gap * g2[..., 0]
    # Rachis: dunkler Grund + heller Grat + Basis schuppig braun:
    rc = np.array([0.06, 0.13, 0.035])[None, None] * np.ones_like(out_col)
    brown = np.clip((0.28 - U) / 0.22, 0, 1) * (dR < 1)
    rc = rc * (1 - brown[..., None]) + np.array([0.22, 0.16, 0.07])[None, None] * brown[..., None]
    rc = rc * (1 - 0.8 * midR[..., None]) + np.array([0.28, 0.40, 0.15])[None, None] * (0.8 * midR)[..., None]
    mR = rach[..., None]
    out_col = out_col * (1 - mR) + rc * mR
    out_h += 1.3 * midR + 0.4 * rach * (1 - brown)
    alb[y0:y0 + CS, x0:x0 + CS, :3] = np.clip(out_col, 0, 1)
    hh = out_h + noise(2, seed + 7, (CS, CS)) * 0.15
    hgt[y0:y0 + CS, x0:x0 + CS] = hh
    mask_ao[y0:y0 + CS, x0:x0 + CS] = np.clip(
        0.92 - 0.10 * np.clip(hh, 0, 1) - 0.05 * np.clip(-hh, 0, 1), 0, 1)


def paint_fiddle_cell(idx, seed):
    """ Zelle 15: Krozier-Rohr — brauner Flaum, Kraeusen-Schuppen, u = Laengsachse."""
    cx, cy = idx % CELLS, idx // CELLS
    y0, x0 = cy * CS, cx * CS
    uc = (np.arange(CS) + 0.5) / CS
    vc = (np.arange(CS) + 0.5) / CS
    U, V = np.meshgrid(uc, vc)
    # Langsverlauf: unten gruenlich-braun, zur Spitze dunkelbraun.
    t = np.clip(U, 0, 1)
    c0 = np.array([0.16, 0.20, 0.07])
    c1 = np.array([0.24, 0.15, 0.07])
    c2 = np.array([0.10, 0.06, 0.03])
    col = c0[None, None] * (1 - t[..., None]) + c1[None, None] * t[..., None]
    late = np.clip((t - 0.55) / 0.45, 0, 1)
    col = col * (1 - late[..., None]) + c2[None, None] * late[..., None]
    # Schuppen/Kraeuse: versetzte Bogenbnder entlang u (Wicklung angedeutet).
    band = np.sin((U * 9.0 + 0.35 * np.sin(V * 6.2832 * 3)) * np.pi * 2) ** 2
    scale_ = np.exp(-((V * 5.0 % 1.0 - 0.5) ** 2) / 0.02)[..., None] * band[..., None]
    col = col * (1 - 0.35 * scale_) + np.array([0.30, 0.22, 0.11])[None, None] * (0.35 * scale_)[..., :3]
    # Flaum: feine, in u gezogene helle Haare.
    hair = np.clip(noise((1.5, 26), seed, (CS, CS)), 0.45, 1.0) ** 2
    col = col + np.array([0.10, 0.09, 0.05])[None, None] * hair[..., None]
    alb[y0:y0 + CS, x0:x0 + CS, :3] = np.clip(col, 0, 1)
    hh = 0.8 * scale_[..., 0] + 0.4 * hair
    hgt[y0:y0 + CS, x0:x0 + CS] = hh
    mask_ao[y0:y0 + CS, x0:x0 + CS] = np.clip(0.85 - 0.12 * hh, 0, 1)


for j in range(13):
    paint_frond_cell(j, 200 + j)
paint_frond_cell(14, 260)
paint_fiddle_cell(15, 270)

# Normal aus Hoehe:
hsm = gaussian_filter(hgt, 2.0)
dy, dx = np.gradient(hsm)
k = 3.2
nx, ny, nz = -dx * k, -dy * k, np.ones_like(hsm)
L = np.sqrt(nx ** 2 + ny ** 2 + nz ** 2)
nrm = np.stack([nx / L, ny / L, nz / L], -1)

# MaskMap: R=Metallic 0, G=AO, B=Detail, A=Smoothness (wie altes Muster).
det = np.clip(0.35 + 0.30 * np.clip(noise(6, 65, (N, N)), 0, 1), 0, 1)
sm = np.clip(0.30 - 0.12 * np.clip(hgt, 0, 1) + 0.03 * noise(20, 63, (N, N)), 0.05, 0.4)
mm = np.stack([np.zeros_like(mask_ao), mask_ao, det, sm], -1)

# --- HSV-Zielband (WALD-ZIEL): Farbton 95..130, S 0.30..0.50, V max 0.45 ---
# Zielwerte als Konstanten, damit Nachlauf und Selbstkontrolle eine Quelle haben.
S_MIN, S_MAX, V_MAX = 0.30, 0.50, 0.45
H_MIN, H_MAX = 95.0, 130.0


def rgb2hsv(rgb):
    """Vektorisierte RGB->HSV-Konvertung (Formelfarbe), Hue in Grad [0,360)."""
    mx = rgb.max(-1)
    mn = rgb.min(-1)
    v = mx
    d = mx - mn
    s = np.where(mx > 1e-9, d / np.maximum(mx, 1e-9), 0.0)
    r0, g0, b0 = rgb[..., 0], rgb[..., 1], rgb[..., 2]
    eps = d + 1e-9
    h = np.where(mx == r0, (g0 - b0) / eps, 6.0)
    h = np.where((mx == g0) & (mx != r0), (b0 - r0) / eps + 2.0, h)
    h = np.where((mx == b0) & (mx != r0) & (mx != g0), (r0 - g0) / eps + 4.0, h)
    return (h * 60.0) % 360.0, s, v


def hsv2rgb(h, s, v):
    """Vektorisierte HSV->RGB-Rueckwandlung, Hue in Grad."""
    i = np.floor(h / 60.0).astype(int) % 6
    f = (h / 60.0) - np.floor(h / 60.0)
    p = v * (1 - s)
    q = v * (1 - f * s)
    t = v * (1 - (1 - f) * s)
    r = np.choose(i, [v, q, p, p, t, v])
    g = np.choose(i, [t, v, v, q, p, p])
    b = np.choose(i, [p, p, t, v, v, q])
    return np.stack([r, g, b], -1)


hsv = rgb2hsv(np.clip(alb[..., :3], 0, 1))
sat = np.clip(hsv[1], S_MIN, S_MAX)     # Sattigung ins Zielband geklemmt
val = np.clip(hsv[2], 0.0, V_MAX)       # Helligkeit gedeckelt
alb[..., :3] = np.clip(hsv2rgb(hsv[0], sat, val), 0, 1)

# --- Selbstkontrolle: Wert -> Kriterium -> OK/NOK (Zeilen fuer den Bericht) ---
h_full, s_full, v_full = rgb2hsv(alb[..., :3])
CSZ = CS


def cell_stats(idx):
    cx, cy = idx % CELLS, idx // CELLS
    sl = (slice(cy * CSZ, (cy + 1) * CSZ), slice(cx * CSZ, (cx + 1) * CSZ))
    hm = float(np.median(h_full[sl]))
    sm = float(np.median(s_full[sl]))
    vm = float(v_full[sl].max())
    return hm, sm, vm


ok_all = True
for j in range(15):                     # Zellen 0..14 = Fiederwedel (gruen)
    hm, sm, vm = cell_stats(j)
    a = H_MIN <= hm <= H_MAX
    b = S_MIN <= sm <= S_MAX
    c = vm <= V_MAX
    ok_all = ok_all and a and b and c
    print(f"Zelle {j:2d}: Farbton {hm:6.1f} -> {H_MIN:.0f}..{H_MAX:.0f} -> "
          f"{'OK' if a else 'NOK'}; S_med {sm:.3f} -> {S_MIN}..{S_MAX} -> "
          f"{'OK' if b else 'NOK'}; V_max {vm:.3f} -> <={V_MAX} -> "
          f"{'OK' if c else 'NOK'}")
print("Atlas-Selbstkontrolle:", "ALLE OK" if ok_all else "NOK (nachschaeffen)")

Image.fromarray((np.clip(alb[::-1], 0, 1) * 255).astype(np.uint8), "RGBA").save(f"{out}/Baumfarn_Leaf_Albedo.png")
Image.fromarray(((nrm[::-1] * 0.5 + 0.5) * 255).astype(np.uint8)).save(f"{out}/Baumfarn_Leaf_Normal.png")
Image.fromarray((mm[::-1] * 255).astype(np.uint8), "RGBA").save(f"{out}/Baumfarn_Leaf_Mask.png")
print("Atlas ok:", out)

