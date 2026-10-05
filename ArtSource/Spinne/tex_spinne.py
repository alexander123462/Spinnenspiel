# Prozedurale Spinnenkoerper-Texturen (1024^2): Abdomen + Cephalothorax
# Albedo (sRGB), Normal (OpenGL), HDRP-MaskMap (R=Metallic 0, G=AO, B=Detail, A=Smoothness)
# Aufruf: python tex_spinne.py <outdir>   (z.B. ArtSource/Spinne/Textures)
import numpy as np
from scipy.ndimage import gaussian_filter
from PIL import Image
import sys, os

out = sys.argv[1] if len(sys.argv) > 1 else "tex"
os.makedirs(out, exist_ok=True)
W = H = 1024

def noise(scale, seed, shape):
    r = np.random.default_rng(seed).standard_normal(shape)
    n = gaussian_filter(r, scale, mode="wrap")
    return (n - n.mean()) / (n.std() + 1e-6)

def save_rgb(arr, path):
    Image.fromarray((np.clip(arr, 0, 1) * 255).astype(np.uint8)).save(path)

# ========================================================================
# ABDOMEN (Vogelspinnen-Hinterleib): feine Haarstraeuhnen nach hinten + schwaches Chevron
# UV-Koordinaten der Mesh: u = umlaufend (Ruecken bei u=0.25), v = hinten (v=0) -> vorne (v=1)
# ========================================================================
u = (np.arange(W) + 0.5) / W
v = (np.arange(H) + 0.5) / H
U, V = np.meshgrid(u, v)

# Ruecken bei u=0.25: Abstand zum Ruecken (wrap, 0 auf Ruecken, max 0.5 unten)
du = ((U - 0.25 + 0.75) % 1.0) - 0.5
dorsal = np.exp(-(du / 0.16) ** 2)          # Gewicht: 1 auf Ruecken -> 0 an den Flanken

# --- feine Haarstraeuhnen, die nach hinten (v-Richtung) laufen ---
# Laengsstreifen (v-stabil, in u viele) mit leichter Schraegung; auf Ruecken am staerksten
wave = 1.2 * du * (V - 0.5)
fibers = np.sin((U * 90 + wave) * np.pi)
fibers = np.clip(fibers * 0.5 + 0.5, 0, 1)
fibers_coarse = np.sin((U * 12 + 0.3 * np.sin(V * 5 + du * 3)) * np.pi)
fibers_coarse = np.clip(fibers_coarse * 0.5 + 0.5, 0, 1)
hair = (0.6 * fibers + 0.4 * fibers_coarse) * (0.25 + 0.75 * dorsal)
# zur Spitze (v=0) hin dichter
hair *= (0.5 + 0.5 * (1 - V))

# --- schwaches helleres Chevron-Muster auf dem Ruecken ---
# zwei helle Laengsbander, die sich nach vorne (v=1) zueinander neigen (Chevron)
spread = 0.06 + 0.10 * (1 - V)                 # vorne (v=1) enger
l1 = np.exp(-((du - spread) / 0.018) ** 2)
l2 = np.exp(-((du + spread) / 0.018) ** 2)
chevron = np.maximum(l1, l2) * np.clip((V - 0.15) / 0.3, 0, 1)
chevron = 0.45 * chevron * (0.7 + 0.3 * noise(8, 41, (H, W)))

# --- Grundfarbe (sRGB 0.10, 0.07, 0.05) ---
base = np.array([0.10, 0.07, 0.05])
hair_c = np.array([0.20, 0.12, 0.08])   # heller rotbraun
chev_c = np.array([0.22, 0.16, 0.11])   # helleres Chevron
col = base[None, None] * (1 + 0.30 * noise(15, 11, (H, W))[..., None])
col = col * (1 - 0.45 * hair[..., None]) + hair_c[None, None] * (0.45 * hair)[..., None]
col = col * (1 - 0.50 * chevron[..., None]) + chev_c[None, None] * (0.50 * chevron)[..., None]
col = np.clip(col, 0, 1)
save_rgb(col[::-1], f"{out}/Spinne_Abdomen_Albedo.png")

# --- Hoehe -> Normal (OpenGL) ---
h = 0.5 * hair + 0.3 * chevron + 0.06 * noise(4, 51, (H, W))
h = gaussian_filter(h, 1.3)
k = 3.0
dy, dx = np.gradient(h)
nx, ny, nz = -dx * k, -dy * k, np.ones_like(h)
L = np.sqrt(nx ** 2 + ny ** 2 + nz ** 2)
nrm = np.stack([nx / L, ny / L, nz / L], -1)
Image.fromarray(((nrm[::-1] * 0.5 + 0.5) * 255).astype(np.uint8)).save(f"{out}/Spinne_Abdomen_Normal.png")

# --- HDRP MaskMap: R=Metallic 0, G=AO, B=Detail, A=Smoothness (~0.25) ---
ao = np.clip(0.85 - 0.20 * hair + 0.05 * noise(12, 61, (H, W)), 0, 1)
detail = np.clip(0.40 + 0.35 * np.clip(noise(6, 65, (H, W)), 0, 1), 0, 1)
smooth = np.clip(0.25 + 0.04 * noise(20, 63, (H, W)), 0, 1)
mm = np.stack([np.zeros_like(ao), ao, detail, smooth], -1)
Image.fromarray((mm[::-1] * 255).astype(np.uint8), "RGBA").save(f"{out}/Spinne_Abdomen_Mask.png")
print("Abdomen-Texturen ok")

# ========================================================================
# CEPHALOTHORAX (Kopfbrust-Schild): dunkel mit radialen Linien von der Fovea
# Fovea (Mittelgrube) auf dem Ruecken bei u=0.25, v=0.5
# ========================================================================
VF, UF = 0.5, 0.25                      # Fovea-Position im UV
duf = ((U - UF + 0.75) % 1.0) - 0.5
dvf = V - VF
r = np.sqrt(duf ** 2 + (dvf * 1.6) ** 2)          # radialer Abstand (v gedehnt)
ang = np.arctan2(dvf * 1.6, duf)                  # Winkel um die Fovea

# --- radiale Linien von der Fovea aus ---
spokes = np.sin(ang * 22)
spokes = np.clip(spokes * 0.5 + 0.5, 0, 1)
# radial nur im mittleren Bereich stark, an den Raendern abflachen
radial = spokes * np.exp(-((r - 0.18) / 0.16) ** 2) * (r > 0.05)
radial = 0.6 * radial

# --- Fovea: kleine helle Mittelgrube ---
fovea = np.exp(-(r / 0.035) ** 2)

# --- Grundfarbe (sRGB 0.09, 0.065, 0.045) ---
base_c = np.array([0.09, 0.065, 0.045])
spoke_c = np.array([0.18, 0.13, 0.09])   # hellere radiale Linien
foc_c = np.array([0.14, 0.10, 0.075])    # Fovea leicht heller
colc = base_c[None, None] * (1 + 0.28 * noise(14, 21, (H, W))[..., None])
colc = colc * (1 - 0.45 * radial[..., None]) + spoke_c[None, None] * (0.45 * radial)[..., None]
colc = colc * (1 - 0.30 * fovea[..., None]) + foc_c[None, None] * (0.30 * fovea)[..., None]
colc = np.clip(colc, 0, 1)
save_rgb(colc[::-1], f"{out}/Spinne_Cephalothorax_Albedo.png")

# --- Hoehe -> Normal (OpenGL) ---
hc = 0.6 * radial + 0.4 * fovea + 0.05 * noise(5, 71, (H, W))
hc = gaussian_filter(hc, 1.4)
k = 3.0
dyc, dxc = np.gradient(hc)
nxc, nyc, nzc = -dxc * k, -dyc * k, np.ones_like(hc)
Lc = np.sqrt(nxc ** 2 + nyc ** 2 + nzc ** 2)
nrmc = np.stack([nxc / Lc, nyc / Lc, nzc / Lc], -1)
Image.fromarray(((nrmc[::-1] * 0.5 + 0.5) * 255).astype(np.uint8)).save(f"{out}/Spinne_Cephalothorax_Normal.png")

# --- HDRP MaskMap: R=Metallic 0, G=AO, B=Detail, A=Smoothness (~0.35) ---
ao_c = np.clip(0.88 - 0.18 * radial + 0.05 * noise(12, 81, (H, W)), 0, 1)
detail_c = np.clip(0.42 + 0.32 * np.clip(noise(6, 85, (H, W)), 0, 1), 0, 1)
smooth_c = np.clip(0.35 + 0.04 * noise(20, 83, (H, W)), 0, 1)
mmc = np.stack([np.zeros_like(ao_c), ao_c, detail_c, smooth_c], -1)
Image.fromarray((mmc[::-1] * 255).astype(np.uint8), "RGBA").save(f"{out}/Spinne_Cephalothorax_Mask.png")
print("Cephalothorax-Texturen ok")

# ========================================================================
# BEIN (Kapsel-UV): dichte feine Haarstraeuhnen entlang der Laenge (V) +
# je ein schmaler, gedaeumpter hellerer Ring an den Gelenkenden (V nahe 0 und 1).
# Kapsel-UV: u = umlaufend (Quer), v = entlang der Beinae (Länge).
# Abmessungen: 512 x 1024.
# ========================================================================
W_L = 512
H_L = 1024
uL = (np.arange(W_L) + 0.5) / W_L
vL = (np.arange(H_L) + 0.5) / H_L
UL, VL = np.meshgrid(uL, vL)

# --- dichte feine Haarstraeuhnen entlang der Laenge (v-Richtung) ---
# Viele feine Streifen entlang u (senkrecht zu v) = Fasern, die entlang v laufen
fibersL = np.sin((UL * 140 + 0.6 * np.sin(VL * 3 + UL * 2)) * np.pi)
fibersL = np.clip(fibersL * 0.5 + 0.5, 0, 1)
fibersL_coarse = np.sin((UL * 16 + 0.3 * np.sin(VL * 5)) * np.pi)
fibersL_coarse = np.clip(fibersL_coarse * 0.5 + 0.5, 0, 1)
hairL = (0.7 * fibersL + 0.3 * fibersL_coarse)

# --- gedämpfter Gelenkring an beiden Gelenkenden (v nahe 0 und 1) ---
sig = 0.022
ringL = np.exp(-((VL - 0.0) / sig) ** 2) + np.exp(-((VL - 1.0) / sig) ** 2)
ringL = np.clip(ringL, 0, 1)

# --- Grundfarbe (sRGB 0.13, 0.095, 0.07) — L2: heller und lesbarer ---
baseL = np.array([0.13, 0.095, 0.07])
hairL_c = np.array([0.36, 0.24, 0.15])   # heller roetlichbraun (L2)
ringL_c = np.array([0.30, 0.22, 0.15])   # gedaeumpter hellerer Ring (nicht orange)
colL = baseL[None, None] * (1 + 0.35 * noise(12, 131, (H_L, W_L))[..., None])
colL = colL * (1 - 0.45 * hairL[..., None]) + hairL_c[None, None] * (0.45 * hairL)[..., None]
colL = colL * (1 - 0.55 * ringL[..., None]) + ringL_c[None, None] * (0.55 * ringL)[..., None]
colL = np.clip(colL, 0, 1)
save_rgb(colL[::-1], f"{out}/Spinne_Leg_Albedo.png")

# --- Hoehe -> Normal (OpenGL): feine Laengsfasern ---
hL = 0.55 * hairL + 0.35 * ringL + 0.05 * noise(4, 141, (H_L, W_L))
hL = gaussian_filter(hL, 1.2)
kL = 3.5
dyL, dxL = np.gradient(hL)
nxL, nyL, nzL = -dxL * kL, -dyL * kL, np.ones_like(hL)
LL = np.sqrt(nxL ** 2 + nyL ** 2 + nzL ** 2)
nrmL = np.stack([nxL / LL, nyL / LL, nzL / LL], -1)
Image.fromarray(((nrmL[::-1] * 0.5 + 0.5) * 255).astype(np.uint8)).save(f"{out}/Spinne_Leg_Normal.png")

# --- HDRP MaskMap: R=Metallic 0, G=AO, B=Detail, A=Smoothness (0.2) ---
aoL = np.clip(0.88 - 0.16 * hairL + 0.05 * noise(12, 151, (H_L, W_L)), 0, 1)
detailL = np.clip(0.45 + 0.35 * np.clip(noise(6, 155, (H_L, W_L)), 0, 1), 0, 1)
smoothL = np.clip(0.20 + 0.03 * noise(20, 153, (H_L, W_L)), 0, 1)
mmL = np.stack([np.zeros_like(aoL), aoL, detailL, smoothL], -1)
Image.fromarray((mmL[::-1] * 255).astype(np.uint8), "RGBA").save(f"{out}/Spinne_Leg_Mask.png")
print(f"Leg-Texturen ok ({W_L}x{H_L})")
print("Texturen fertig")
