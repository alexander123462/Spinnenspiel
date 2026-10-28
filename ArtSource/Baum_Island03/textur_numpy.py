# MODELL25 (Baum_Island03): Blatt-Texturen prozedural mit numpy (TEXTUR1-Pflicht).
# Aufruf: python ArtSource/Baum_Island03/textur_numpy.py [Zielordner]
# UV der eifoermigen Schaufen-Blattchen: U ueber Breite (Mittelrippe U=0.5),
# V entlang Laenge. Farbton ~118 Grad (WALD-ZIEL 95..130), etwas waermer und
# dunkler als Insel02 (Schirmbaum-Lichtgruen), Saettigung ~0.45, Helligkeit <=0.45.
import sys, os
import numpy as np

OUT = sys.argv[1] if len(sys.argv) > 1 else r"C:\Users\vorau\dev\Spinnenspiel\ArtSource\Baum_Island03\out"
N = 1024
rng = np.random.default_rng(2503)

u, v = np.meshgrid(np.linspace(0, 1, N), np.linspace(0, 1, N))

base = np.stack([np.full_like(u, 0.190), np.full_like(u, 0.305),
                 np.full_like(u, 0.165)], axis=-1)
base *= (0.86 + 0.22 * v)[..., None]          # heller zur Spitze
big = rng.standard_normal((16, 16))
for _ in range(4):
    big = (big + np.roll(big, 1, 0) + np.roll(big, -1, 0)
           + np.roll(big, 1, 1) + np.roll(big, -1, 1)) / 5.0
big = np.kron(big, np.ones((N // 16, N // 16)))[:N, :N]
for _ in range(3):
    big = (big + np.roll(big, 8, 0) + np.roll(big, -8, 0)
           + np.roll(big, 8, 1) + np.roll(big, -8, 1)) / 4.0
big = (big - big.min()) / (big.max() - big.min())
base[..., 1] += 0.050 * big                   # Farbnebel: weiche Hell-Dunkel-Blatter
base[..., 0] += 0.030 * big
base[..., 2] += 0.014 * big

du = np.abs(u - 0.5)
midrib = np.exp(-(du / 0.020) ** 2)           # Mittelrippe (Schaufenblatt, breiter)
base += (midrib * 0.070)[..., None] * np.array([0.95, 1.0, 0.8])
duw = du * (1.0 + 0.6 * np.sin(v * np.pi * 2.3))
vein = np.abs(np.sin(np.pi * (v * 16.0 - duw * 3.6))) ** 40.0   # 16 Aderpaare
base += (vein * 0.028)[..., None] * np.array([0.8, 1.0, 0.7])
base += rng.standard_normal((N, N))[..., None] * 0.012          # Zellkorn
base -= (np.exp(-((du - 0.45) / 0.05) ** 2) * 0.030)[..., None]
albedo = np.clip(base, 0.0, 1.0)

h = midrib * 1.0 + vein * 0.5
gy, gx = np.gradient(h)
nx, ny, nz = -gx * 2.4, gy * 2.4, np.ones_like(h)
ln = np.sqrt(nx * nx + ny * ny + nz * nz)
normal = np.stack([nx / ln, ny / ln, nz / ln], axis=-1) * 0.5 + 0.5

occ = np.clip(0.95 - 0.25 * (1 - v), 0, 1)
mask = np.stack([np.zeros_like(u), np.full_like(u, 0.07), occ,
                 0.22 + 0.06 * vein], axis=-1)

def write_png(path, rgba):
    from PIL import Image
    Image.fromarray((np.clip(rgba, 0, 1) * 255 + 0.5).astype(np.uint8), "RGBA").save(path)

os.makedirs(OUT, exist_ok=True)
write_png(os.path.join(OUT, "island_tree_03_leaves_Albedo.png"),
          np.concatenate([albedo, np.ones_like(u)[..., None]], axis=-1))
write_png(os.path.join(OUT, "island_tree_03_leaves_Normal.png"),
          np.concatenate([normal, np.ones_like(u)[..., None]], axis=-1))
write_png(os.path.join(OUT, "island_tree_03_leaves_Mask.png"), mask)

hsv = np.array([np.mean(albedo[..., 0]), np.mean(albedo[..., 1]), np.mean(albedo[..., 2])])
import colorsys
hh, ss, vv = colorsys.rgb_to_hsv(*(hsv.tolist()))
print("gemessen: Farbton %.0f Grad, Sae %.2f, Helligkeit %.2f" % (hh * 360, ss, vv))
print("geschrieben:", OUT)
