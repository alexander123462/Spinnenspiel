# TEXTUR1 (MODELL22): Blatt-Texturen fuer Baum_Klein02, prozedural mit numpy.
# Aufruf: python ArtSource/Baum_Klein02/textur_numpy.py
# Schreibt Baum_Klein02_leaves_Albedo/Normal/Mask.png in den Zielordner (Argumente: sys.argv[1]).
# UV der Blatt-Streifen: U ueber die Breite (Mittelrippe bei U=0.5), V entlang der Laenge (Basis V=0, Spitze V=1).
import sys, os
import numpy as np

OUT = sys.argv[1] if len(sys.argv) > 1 else r"C:\Users\vorau\dev\Spinnenspiel\ArtSource\Baum_Klein02\out"
N = 1024
rng = np.random.default_rng(2202)

u, v = np.meshgrid(np.linspace(0, 1, N), np.linspace(0, 1, N))  # v=Zeilen (Laenge), u=Spalten (Breite)

# Grundfarbe: Wald-Ziel-Gruen (Farbton ~110 Grad, Saettigung 0.35..0.45, Helligkeit <=0.45)
base = np.stack([
    np.full_like(u, 0.190),  # R  (Moosgruen, Helligkeit ~0.33 fuer Galerie-Lesbarkeit)
    np.full_like(u, 0.330),  # G
    np.full_like(u, 0.170),  # B
], axis=-1)
# Heller zur Spitze, dunkler zur Basis (Blattalter)
grad = (0.80 + 0.35 * v)[..., None]
base *= grad
# Farbnebel: grossflaechige Variation (HSV-artig ueber additive Gruen-/Rot-Nebel)
big = rng.standard_normal((16, 16))
big = np.kron(big, np.ones((N // 16, N // 16)))[:N, :N]
big = (big - big.min()) / (big.max() - big.min())
base[..., 1] += 0.055 * big
base[..., 0] += 0.020 * big
base[..., 2] += 0.012 * big

# Mittelrippe: heller Streifen um U=0.5
du = np.abs(u - 0.5)
midrib = np.exp(-(du / 0.018) ** 2)
base += (midrib * 0.075)[..., None] * np.array([1.0, 1.0, 0.8])

# Seitenadern: diagonal von der Mittelrippe zur Blattrand (Fiederung)
vein = np.abs(np.sin(np.pi * (v * 9.0 - du * 14.0))) ** 22.0
base += (vein * 0.05)[..., None] * np.array([0.8, 1.0, 0.7])

# Feines Rauschen (nicht grell)
fine = rng.standard_normal((N, N)) * 0.012
base += fine[..., None]

# Rand leicht dunkler (AO) und Bluettchen-Saum heller
edge = np.exp(-((du - 0.46) / 0.05) ** 2)
base -= (edge * 0.03)[..., None]

albedo = np.clip(base, 0.0, 1.0)
alpha = np.ones_like(u)

# Normalmap: Hoehen = Mittelrippe + Adern, per Gradient in Normal-RGB
h = midrib * 1.0 + vein * 0.55
gy, gx = np.gradient(h)
nz = np.ones_like(h)
scale = 2.2
nx = -gx * scale; ny = gy * scale
ln = np.sqrt(nx * nx + ny * ny + nz * nz)
normal = np.stack([nx / ln, ny / ln, nz / ln], axis=-1)
normal = (normal * 0.5 + 0.5)

# Mask (HDRP): R=Metallic 0, G=Subsurface leicht, B=Occlusion (Basis dunkler), A=Smoothness ~0.2..0.3
occ = np.clip(0.95 - 0.25 * (1 - v), 0, 1)
smooth = 0.24 + 0.06 * vein
mask = np.stack([np.zeros_like(u), np.full_like(u, 0.06), occ, smooth], axis=-1)

def write_png(path, rgba):
    from PIL import Image
    arr = (np.clip(rgba, 0, 1) * 255 + 0.5).astype(np.uint8)
    Image.fromarray(arr, "RGBA").save(path)

os.makedirs(OUT, exist_ok=True)
write_png(os.path.join(OUT, "Baum_Klein02_leaves_Albedo.png"), np.concatenate([albedo, alpha[..., None]], axis=-1))
write_png(os.path.join(OUT, "Baum_Klein02_leaves_Normal.png"), np.concatenate([normal, np.ones_like(u)[..., None]], axis=-1))
write_png(os.path.join(OUT, "Baum_Klein02_leaves_Mask.png"), mask)
print("geschrieben:", OUT)
