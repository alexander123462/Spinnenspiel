# Prozedurale Blatt-Texturen (Alocasia-artiges Riesenblatt) -> Albedo, Normal (OpenGL), HDRP-MaskMap
import numpy as np
from scipy.ndimage import gaussian_filter
from PIL import Image
import sys, os

out = sys.argv[1] if len(sys.argv) > 1 else "tex"
os.makedirs(out, exist_ok=True)
rng = np.random.default_rng(7)
W, H = 1024, 2048                      # u quer (t -1..1), v laengs (s 0..1, Basis->Spitze)
u = (np.arange(W) + 0.5) / W
v = (np.arange(H) + 0.5) / H
t, s = np.meshgrid(u * 2 - 1, v)       # t: -1 Rand .. 0 Mittelrippe .. 1 Rand
at = np.abs(t)

def noise(scale, seed):
    r = np.random.default_rng(seed).standard_normal((H, W))
    n = gaussian_filter(r, scale, mode="wrap")
    return (n - n.mean()) / (n.std() + 1e-6)

# --- Adern ---------------------------------------------------------------
mid = np.exp(-(t / 0.018) ** 2)                                   # Mittelrippe
N = 8                                                            # Seitenadern
curve = 0.30 * at ** 1.5                                          # Adern biegen Richtung Spitze
jit = 0.006 * noise(220, 3)
phase = (s - curve + jit) * N
d = np.abs(phase - np.round(phase))                               # 0 auf der Ader
vw = 0.035 * (1 - 0.6 * at)                                       # Adern werden zum Rand duenner
lat = np.exp(-(d / vw) ** 2) * np.clip((s - curve - 0.06) / 0.06, 0, 1) * np.clip((at - 0.02) / 0.05, 0, 1) * np.clip((1 - at) / 0.15, 0, 1)
# feine Netzadern
ph2 = (s * 1.0 - 0.15 * at + 0.004 * noise(8, 5)) * N * 6
d2 = np.abs(ph2 - np.round(ph2))
fine = np.exp(-(d2 / 0.05) ** 2) * 0.35
ph3 = (t * 34 + 0.5 * noise(10, 9))
d3 = np.abs(ph3 - np.round(ph3))
fine = np.maximum(fine, np.exp(-(d3 / 0.05) ** 2) * 0.25)
# Randader (Alocasia hat eine Saumader)
rim = np.exp(-((1 - at) / 0.012) ** 2)

# --- Farbe (sRGB) --------------------------------------------------------
dark = np.array([0.045, 0.15, 0.025])
base = np.array([0.11, 0.29, 0.045])
light = np.array([0.18, 0.38, 0.07])
vein = np.array([0.36, 0.50, 0.18])
g = 1 - at ** 1.2                                                 # heller zur Mitte
g = np.clip(g + 0.10 * noise(60, 11) + 0.05 * noise(12, 12), 0, 1)
col = dark[None, None] * (1 - g[..., None]) + base[None, None] * g[..., None]
# Felder zwischen Adern leicht gewoelbt -> Mitte heller
puff = (1 - np.clip(d / 0.5, 0, 1)) ** 0.5 * 0
cell = 0.5 + 0.5 * np.cos(np.clip(d, 0, 0.5) * 2 * np.pi)          # 1 an Ader, 0 zwischen
col = col + (light - base)[None, None] * (0.25 * (1 - cell))[..., None]
col = col * (1 - 0.06 * fine)[..., None]
V = np.clip(np.maximum(mid * 0.9, lat * 0.6) + rim * 0.12, 0, 1)
col = col * (1 - V[..., None]) + vein[None, None] * V[..., None]
# Alterung: ein paar gelbliche/braune Flecken, Spitze & Rand leicht trocken
spots = np.clip(noise(25, 21) - 2.7, 0, 1) * 0.8
col = col * (1 - spots[..., None]) + np.array([0.45, 0.40, 0.12])[None, None] * spots[..., None]
edge = np.clip((at - 0.965) / 0.035, 0, 1) * (0.5 + 0.5 * np.clip(noise(6, 31) + 0.5, 0, 1))
tip = np.clip((s - 0.97) / 0.03, 0, 1)
dry = np.clip(edge * 0.6 + tip * 0.7, 0, 1)
col = col * (1 - dry[..., None]) + np.array([0.33, 0.27, 0.10])[None, None] * dry[..., None]
col = np.clip(col * (1 + 0.04 * noise(2, 41))[..., None], 0, 1)
Image.fromarray((col[::-1] * 255).astype(np.uint8)).save(f"{out}/Riesenblatt_Leaf_Albedo.png")

# --- Hoehe -> Normal ----------------------------------------------------
h = 1.2 * mid - 0.55 * lat - 0.15 * fine + 0.35 * (1 - cell) * np.clip(at * 4, 0, 1) + 0.03 * noise(3, 51)
h = gaussian_filter(h, 1.2)
k = 6.0
dy, dx = np.gradient(h)
nx, ny, nz = -dx * k, -dy * k, np.ones_like(h)
L = np.sqrt(nx ** 2 + ny ** 2 + nz ** 2)
nrm = np.stack([nx / L, ny / L, nz / L], -1)
# Bild wird vertikal gespiegelt gespeichert (v=0 unten) -> Y-Komponente passt (OpenGL, +Y = Bild oben)
Image.fromarray(((nrm[::-1] * 0.5 + 0.5) * 255).astype(np.uint8)).save(f"{out}/Riesenblatt_Leaf_Normal.png")

# --- HDRP MaskMap: R=Metallic 0, G=AO, B=DetailMask, A=Smoothness ----
ao = np.clip(1 - 0.25 * lat - 0.1 * (1 - g), 0, 1)
smooth = np.clip(0.62 - 0.18 * V - 0.25 * dry + 0.04 * noise(20, 61), 0, 1)
mm = np.stack([np.zeros_like(ao), ao, np.zeros_like(ao), smooth], -1)
Image.fromarray((mm[::-1] * 255).astype(np.uint8), "RGBA").save(f"{out}/Riesenblatt_Leaf_Mask.png")

# --- Stiel ---------------------------------------------------------------
SW, SH = 256, 1024
r = np.random.default_rng(3).standard_normal((SH, SW))
streak = gaussian_filter(r, (40, 1.5), mode="wrap")
streak = (streak - streak.mean()) / streak.std()
sv = np.linspace(0, 1, SH)[:, None]
sc = np.array([0.20, 0.38, 0.09])[None, None] * (1 + 0.10 * streak[..., None])
sc = sc * (1 - sv[..., None] * 0) + np.array([0.30, 0.25, 0.12])[None, None] * np.clip(0.25 - sv, 0, 1)[..., None] * 2
Image.fromarray((np.clip(sc[::-1], 0, 1) * 255).astype(np.uint8)).save(f"{out}/Riesenblatt_Stem_Albedo.png")
print("Texturen ok")
