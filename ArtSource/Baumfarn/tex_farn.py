# Prozedurale Baumfarn-Texturen (Cyathea-artig) -> Fiederblatt (Alpha-Cutout), Rinde, HDRP-MaskMap
# Aufruf: python tex_farn.py <outdir>   (z.B. ArtSource/Baumfarn/Textures)
import numpy as np
from scipy.ndimage import gaussian_filter
from PIL import Image
import sys, os

out = sys.argv[1] if len(sys.argv) > 1 else "tex"
os.makedirs(out, exist_ok=True)

def noise(scale, seed, shape):
    r = np.random.default_rng(seed).standard_normal(shape)
    n = gaussian_filter(r, scale, mode="wrap")
    return (n - n.mean()) / (n.std() + 1e-6)

# --- Fiederblatt (Pinnule): u = Breite (0..1, Mittelrippe bei 0.5), v = Laenge (0 Basis -> 1 Spitze)
W, H = 512, 1024
u = (np.arange(W) + 0.5) / W
v = (np.arange(H) + 0.5) / H
U, V = np.meshgrid(u, v)
du = np.abs(U - 0.5)

# Blattform: elliptisch, zur Spitze und Basis verjuegt
hw = 0.44 * np.sin(np.pi * (0.12 + 0.88 * V)) ** 0.65
hw = np.clip(hw * (1 + 0.10 * noise(8, 11, (H, W))), 0.03, 0.5)
alpha = np.clip((hw - du) / 0.012, 0, 1)
alpha = np.clip(alpha * (0.85 + 0.30 * np.clip(noise(3, 17, (H, W)), 0, 1)), 0, 1)

# Mittelrippe + Seitenadern (naehe Basis fast senkrecht, zur Spitze hin gebogen)
mid = np.exp(-((U - 0.5) / 0.008) ** 2)
vein = np.zeros((H, W))
for k in range(1, 12):
    vk = 0.05 + k * 0.075
    d = V - (vk + 1.6 * du)
    vein = np.maximum(vein, np.exp(-(d / 0.009) ** 2) * (d > -0.12))
# feine Netzstruktur
vein = np.maximum(vein, 0.35 * np.exp(-((V * 55 + 0.5 * noise(5, 23, (H, W))) % 1 - 0.5) ** 2 / 0.02))

# --- Farbe (sRGB) --------------------------------------------------------
base = np.array([0.085, 0.26, 0.05])
light = np.array([0.16, 0.34, 0.08])
vein_c = np.array([0.30, 0.42, 0.14])
mid_c = np.array([0.34, 0.47, 0.16])
col = base[None, None] * (1 + 0.25 * noise(12, 21, (H, W))[..., None])
g = np.clip(0.5 + 0.4 * noise(30, 25, (H, W)), 0, 1)
col = col * (1 - g[..., None]) + light[None, None] * g[..., None]
col = col * (1 - 0.55 * vein)[..., None] + vein_c[None, None] * (0.55 * vein)[..., None]
col = col * (1 - 0.7 * mid)[..., None] + mid_c[None, None] * (0.7 * mid)[..., None]
# Spitze & Rand leicht trocken
dry = np.clip((V - 0.85) / 0.15, 0, 1) * 0.45 + np.clip(du / hw - 0.85, 0, 1) * 0.35
col = col * (1 - dry[..., None]) + np.array([0.28, 0.24, 0.09])[None, None] * dry[..., None]
col = np.clip(col, 0, 1)
rgba = np.dstack([np.clip(col, 0, 1) * 255, (alpha * 255).astype(np.uint8)])
Image.fromarray(rgba[::-1].astype(np.uint8), "RGBA").save(f"{out}/Baumfarn_Leaf_Albedo.png")

# --- Hoehe -> Normal ------------------------------------------------------
h = 1.3 * mid + 0.7 * vein + 0.25 * np.sin(np.pi * du / 0.5) * alpha + 0.05 * noise(3, 51, (H, W))
h = gaussian_filter(h, 1.2)
k = 4.0
dy, dx = np.gradient(h)
nx, ny, nz = -dx * k, -dy * k, np.ones_like(h)
L = np.sqrt(nx ** 2 + ny ** 2 + nz ** 2)
nrm = np.stack([nx / L, ny / L, nz / L], -1)
Image.fromarray(((nrm[::-1] * 0.5 + 0.5) * 255).astype(np.uint8)).save(f"{out}/Baumfarn_Leaf_Normal.png")

# --- HDRP MaskMap: R=Metallic 0, G=AO, B=DetailMask, A=Smoothness ----
ao = np.clip(0.90 - 0.15 * vein + 0.04 * noise(15, 61, (H, W)), 0, 1)
detail = np.clip(0.35 + 0.30 * np.clip(noise(6, 65, (H, W)), 0, 1), 0, 1)
smooth = np.clip(0.65 - 0.20 * vein + 0.03 * noise(20, 63, (H, W)), 0, 1)
mm = np.stack([np.zeros_like(ao), ao, detail, smooth], -1)
Image.fromarray((mm[::-1] * 255).astype(np.uint8), "RGBA").save(f"{out}/Baumfarn_Leaf_Mask.png")

# --- Stamm: dunkelbraune Faserrinde (Basis sRGB ~0.16/0.10/0.06), helle Narben von abgefallenen Wedeln
SW, SH = 256, 1024
rv = (np.arange(SH) + 0.5) / SH
fibers = noise((1.2, 65), 3, (SH, SW))          # feine vertikale Fasern mit stärkerer Ausrichtung
fib_coarse = noise((2.5, 3), 7, (SH, SW))       # grobe vertikale Fasern
fibers = (fibers * 0.4 + fib_coarse * 0.6)      # Mischung
fibers = fibers - fibers.mean()
fibers /= (fibers.std() + 1e-6)
streaks = noise((12, 20), 33, (SH, SW))          # rautenförmige dunkle Längsfasern
streaks = streaks - streaks.mean()
streaks /= (streaks.std() + 1e-6)
# Muster zur Rautenform (45° gedreht)
raute = (np.sin(np.pi * rv[:, None] / 2 + np.pi/4) ** 2 + np.cos(np.pi * rv[:, None] / 2 - np.pi/4) ** 2) ** 0.5
raute = (raute[:, None] - 0.5) / 0.5
raute = np.clip(raute + 0.15 * noise(8, 29, (SH, SW)), -0.8, 0.8)
raute *= streaks
base = np.array([0.16, 0.10, 0.06])             # Ziel-Basisfarbe (sRGB)
dark = np.array([0.07, 0.045, 0.028])
scar_c = np.array([0.34, 0.25, 0.15])           # helle Narben (tann)
col = base[None, None] * (1 + 0.30 * fibers[..., None])
col = col * (1 + 0.18 * streaks[..., None])
col = col * (1 - 0.40 * np.clip(-streaks, 0, 1)[..., None])   # dunkle Streifen
# helle Narben: horizontal, unregelmaeßig verteilt (abgefallene Wedeln)
# Rautenförmig versetzt: waagerecht gestreifter Grund + rautenartige Versätze
row = np.sin(rv * 42.0 + 2.0 * noise(20, 35, (SH, 1)))
narrows = np.exp(-((rv[:, None] - (0.10 + 0.82 * (noise(35, 37, (SH, 1)) * 0.5 + 0.5))) ** 2) / 0.0005)
narrows = np.clip(narrows * (0.4 + 0.6 * noise(4, 52, (SH, 1))), 0, 1)
col = col * (1 - 0.55 * narrows[..., None]) + scar_c[None, None] * (0.55 * narrows)[..., None]
# unten (alt) etwas dunkler, oben leicht gruenlich
bg = np.clip((0.15 - rv[:, None]) / 0.15, 0, 1)
col = col * (1 - 0.25 * bg[..., None])
tg = np.clip((rv[:, None] - 0.85) / 0.15, 0, 1)
col = col * (1 - 0.4 * tg[..., None]) + np.array([0.11, 0.16, 0.07])[None, None] * (0.4 * tg)[..., None]
Image.fromarray((np.clip(col[::-1], 0, 1) * 255).astype(np.uint8)).save(f"{out}/Baumfarn_Stem_Albedo.png")
print("Texturen ok")
