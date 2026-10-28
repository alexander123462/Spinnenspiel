# MODELL23 (Baum_Island01): Blatt-Texturen prozedural mit numpy (TEXTUR1-Pflicht).
# Aufruf: python ArtSource/Baum_Island01/textur_numpy.py [Zielordner]
# UV der Lanzenblatt-Streifen: U ueber Breite (Mittelrippe U=0.5), V entlang Laenge.
# Farbton ~108 Grad (WALD-ZIEL 95..130), Saettigung ~0.4, Helligkeit <=0.45.
import sys, os
import numpy as np

OUT = sys.argv[1] if len(sys.argv) > 1 else r"C:\Users\vorau\dev\Spinnenspiel\ArtSource\Baum_Island01\out"
N = 1024
rng = np.random.default_rng(2302)

u, v = np.meshgrid(np.linspace(0, 1, N), np.linspace(0, 1, N))

# Grundfarbe: warmes Waldgruen mit olivigem Einschlag (Insel-Baum-Vorbild).
base = np.stack([
    np.full_like(u, 0.205),  # R
    np.full_like(u, 0.340),  # G
    np.full_like(u, 0.150),  # B
], axis=-1)
grad = (0.82 + 0.30 * v)[..., None]      # heller zur Spitze
base *= grad
# Farbnebel: weich geblent (kein Kron-Blockkanten-Artefakt).
big = rng.standard_normal((16, 16))
for _ in range(4):                        # Repeat-Kanten sanft ausmitteln
    big = (big + np.roll(big, 1, 0) + np.roll(big, -1, 0)
           + np.roll(big, 1, 1) + np.roll(big, -1, 1)) / 5.0
big = np.kron(big, np.ones((N // 16, N // 16)))[:N, :N]
for _ in range(3):                        # Upsample-Stufen weichzeichnen
    big = (big + np.roll(big, 8, 0) + np.roll(big, -8, 0)
           + np.roll(big, 8, 1) + np.roll(big, -8, 1)) / 4.0
big = (big - big.min()) / (big.max() - big.min())
base[..., 1] += 0.050 * big
base[..., 0] += 0.028 * big
base[..., 2] += 0.010 * big

du = np.abs(u - 0.5)
midrib = np.exp(-(du / 0.016) ** 2)       # helle Mittelrippe
base += (midrib * 0.08)[..., None] * np.array([1.0, 1.0, 0.75])
# Seitenadern: flach abstehend (lanzettlich), duenn und in der Periode variiert,
# damit kein gleichmaessiges Chevron-Muster entsteht.
duw = du * (1.0 + 0.35 * np.sin(v * np.pi * 2.3))
vein = np.abs(np.sin(np.pi * (v * 14.0 - duw * 6.0))) ** 46.0
base += (vein * 0.028)[..., None] * np.array([0.75, 1.0, 0.65])
fine = rng.standard_normal((N, N)) * 0.012
base += fine[..., None]
edge = np.exp(-((du - 0.45) / 0.05) ** 2)
base -= (edge * 0.035)[..., None]

albedo = np.clip(base, 0.0, 1.0)
alpha = np.ones_like(u)

# Normalmap: Hoehen = Mittelrippe + Adern, per Gradient in Normal-RGB.
h = midrib * 1.0 + vein * 0.5
gy, gx = np.gradient(h)
nz = np.ones_like(h)
scale = 2.4
nx = -gx * scale; ny = gy * scale
ln = np.sqrt(nx * nx + ny * ny + nz * nz)
normal = np.stack([nx / ln, ny / ln, nz / ln], axis=-1)
normal = (normal * 0.5 + 0.5)

# Mask (HDRP): R=Metallic 0, G=Subsurface leicht, B=Occlusion, A=Smoothness ~0.2..0.3
occ = np.clip(0.95 - 0.25 * (1 - v), 0, 1)
smooth = 0.22 + 0.06 * vein
mask = np.stack([np.zeros_like(u), np.full_like(u, 0.07), occ, smooth], axis=-1)

def write_png(path, rgba):
    from PIL import Image
    arr = (np.clip(rgba, 0, 1) * 255 + 0.5).astype(np.uint8)
    Image.fromarray(arr, "RGBA").save(path)

os.makedirs(OUT, exist_ok=True)
write_png(os.path.join(OUT, "island_tree_01_leaves_Albedo.png"), np.concatenate([albedo, alpha[..., None]], axis=-1))
write_png(os.path.join(OUT, "island_tree_01_leaves_Normal.png"), np.concatenate([normal, np.ones_like(u)[..., None]], axis=-1))
write_png(os.path.join(OUT, "island_tree_01_leaves_Mask.png"), mask)
print("geschrieben:", OUT)

