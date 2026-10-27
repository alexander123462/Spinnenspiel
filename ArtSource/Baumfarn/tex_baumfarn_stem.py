# MODELL15b: Stamm-Albedo fuer den neuen Baumfarn (Selbstkritik Runde 2: das
# alte 256x1024-Bild las die Narbenringe als Schachbrett aussehen).
# UV des neuen Meshes: u = Ringwinkel (0..1, muss oben/unten-stetig sein und in
# u periodisch), v = 0..1 ueber die Stammhoehe. Daher: waagerechte Narben-
# bander uber v, Farb-Rauschen nur mit ganzzahligen u-Harmonischen (kein Naht-
# sprung). Aufruf: python tex_baumfarn_stem.py
import numpy as np
from PIL import Image

W, H = 256, 1024   # u-Breite, v-Hoehe wie das Original
OUTS = [r"C:/Users/vorau/dev/Spinnenspiel/ArtSource/Baumfarn/Textures/Baumfarn_Stem_Albedo.png",
        r"C:/Users/vorau/dev/Spinnenspiel/Assets/_Project/Art/Plants/Baumfarn/Textures/Baumfarn_Stem_Albedo.png"]

rng = np.random.default_rng(1505)
u, v = np.meshgrid(np.arange(W) / W, np.arange(H) / H)

def vnoise(nu, nv, seed_rng):
    """Wert-Rauschen mit Weichtritt: in u periodisch (Zellrueckgriff), in v
    Randklemmen. Ersetzt die alte Wellen-Zerlegung, die als Spiralnaht aussah."""
    g = seed_rng.standard_normal((nv, nu))
    fu, fv = u * nu, v * nv
    i0 = np.floor(fu).astype(int) % nu
    i1 = (i0 + 1) % nu
    j0 = np.clip(np.floor(fv).astype(int), 0, nv - 1)
    j1 = np.clip(j0 + 1, 0, nv - 1)
    tx = fu - np.floor(fu); ty = fv - np.floor(fv)
    tx = tx * tx * (3 - 2 * tx); ty = ty * ty * (3 - 2 * ty)   # softstep
    a = g[j0, i0] * (1 - tx) + g[j0, i1] * tx
    b = g[j1, i0] * (1 - tx) + g[j1, i1] * tx
    return a * (1 - ty) + b * ty

rng2 = np.random.default_rng(77)
n_fine = sum(vnoise(8 * 2 ** o, 32 * 2 ** o, rng2) / (2 ** o) for o in range(3))
n_big = vnoise(3, 5, np.random.default_rng(99))

# Blattnarben-Baender: ~12 uber die ganze Hoehe, fast waagerecht (u-Phasen-
# versatz nur 0.2, damit sie als Ringe und nicht als Diagonale lesen), weiche
# Rillenschatten; darueber leicht hellere Blattkissen.
s = (12.0 * v + 0.2 * np.sin(2 * np.pi * u)) % 1.0
band = np.exp(-((s - 0.5) / 0.13) ** 2)

base = np.array([0.30, 0.22, 0.13])          # Braun wie Slot-Farbe
lum = 1.0 + 0.10 * n_big + 0.045 * n_fine
shade = 1.0 - 0.38 * band + 0.12 * (1 - band)
rgb = np.clip(base[None, None, :] * lum[:, :, None] * shade[:, :, None], 0, 1)

img = Image.fromarray((rgb * 255).astype(np.uint8), "RGB")
for p in OUTS:
    img.save(p)

# Selbstkontrolle: Werte -> Kriterium -> Ergebnis
mean = rgb.mean(axis=(0, 1))
print("Stamm-Albedo Selbstkontrolle:")
print("  Mittel RGB %.3f %.3f %.3f -> Braun (R>G>B), nicht schwarz/weiss -> %s"
      % (*mean, "OK" if 0.05 < mean[0] < 0.6 and mean[0] > mean[1] >= mean[2] else "NOK"))
top = rgb[:30].mean(); bot = rgb[-30:].mean()
print("  Bandkontrast (shade max-min) %.3f -> >=0.30 -> %s"
      % (shade.max() - shade.min(), "OK" if shade.max() - shade.min() >= 0.30 else "NOK"))
edge = np.abs(rgb[:, 0].astype(int) - rgb[:, -1].astype(int)).mean()
print("  u-Naht Differenz %.2f/255 -> <=4 -> %s" % (edge, "OK" if edge <= 4 else "NOK"))
for p in OUTS:
    print("  geschrieben:", p)
