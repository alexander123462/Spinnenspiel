# MODELL16: Borken-Textur fuer Totholz. UV: u=Umfang (periodisch), v=Laenge.
# Laengsrisse (zu den Geometrie-Furchen synchron), Verwitterungs-Graeu,
# Moosschimmer auf graten. Selbstkontrolle am Ende. Aufruf: python tex_bark.py
import numpy as np
from PIL import Image
W, H = 1024, 512
OUT = ["C:/Users/vorau/dev/Spinnenspiel/ArtSource/Totholz/Textures/Totholz_Bark_Albedo.png",
       "C:/Users/vorau/dev/Spinnenspiel/Assets/_Project/Art/Plants/Totholz/Textures/Totholz_Bark_Albedo.png"]
import os
for o in OUT: os.makedirs(os.path.dirname(o), exist_ok=True)
rng = np.random.default_rng(1601)
u, v = np.meshgrid(np.arange(W)/W, np.arange(H)/H)

def pnoise(nu, nv, seed_rng):
    g = seed_rng.standard_normal((nv+1, nu))
    fu, fv = u*nu, v*nv
    i0 = np.floor(fu).astype(int); j0 = np.floor(fv).astype(int)
    i1 = (i0+1) % nu; j1 = np.clip(j0+1, 0, nv)
    tx = fu-np.floor(fu); ty = fv-np.floor(fv)
    tx = tx*tx*(3-2*tx); ty = ty*ty*(3-2*ty)
    a = g[j0, i0]*(1-tx)+g[j0, i1]*tx
    b = g[j1, i0]*(1-tx)+g[j1, i1]*tx
    return a*(1-ty)+b*ty

n_small = sum(pnoise(16*2**o, 8*2**o, np.random.default_rng(1600+o)) / 2**o for o in range(3))
n_big = pnoise(4, 3, np.random.default_rng(1610))
# Risse: fmod variiert ueber v wie in der Geometrie (0.6+0.8*pnoise1(v,7))
fm = 0.6 + 0.8 * ((np.sin((v*7+0.3)*2*np.pi)*0.5+0.5))
fur = np.abs(np.sin(u*np.pi*fm*2 + 0.35*n_small))
crack_long = (1-fur)**4
# Querrisse: unregelmaessige Patten-Hoehen ueber v, versetzt pro u-Streifen.
qphase = pnoise(24, 1, np.random.default_rng(1630))          # je u-Streifen
qv = v*7.0 + qphase*2.2 + 0.35*n_small
qfur = np.abs(np.sin(qv*np.pi))
crack_trans = (1-qfur)**7 * (0.30+0.50*fur)                  # bricht an Laengsrissen ab
crack = np.clip(crack_long + crack_trans, 0, 1)
ridge = (1-fur)**2
grain = pnoise(8, 96, np.random.default_rng(1620))

base = np.array([0.24, 0.17, 0.11])                       # braune Borke
grey = np.array([0.34, 0.31, 0.27])                        # vergrautes Holz
wmix = np.clip((n_big-0.45)*2.2, 0, 1)                     # Verwitterungsflächen
col = base*(1-wmix[...,None]) + grey*wmix[...,None]
col *= (1 + 0.16*n_small[...,None] + 0.10*grain[...,None])
col = col*(1-0.88*crack[...,None]) + np.array([0.30,0.24,0.17])*(ridge*0.35)[...,None]*(1-wmix[...,None])
# Moosschimmer nur auf graten, feucht-gruen (Farbton ~100 Grad, gedeckt)
moss = np.clip((ridge-0.45)*2.2, 0, 1) * np.clip(n_big*1.6-0.25, 0, 1)
col = col*(1-moss[...,None]*0.55) + np.array([0.16,0.24,0.12])*moss[...,None]*0.55
rgb = np.clip(col, 0, 1)
img = Image.fromarray((rgb*255).astype(np.uint8), "RGB")
for o in OUT: img.save(o)

m = rgb.mean(axis=(0,1))
seam = np.abs(rgb[:,0].astype(int)-rgb[:,-1].astype(int)).mean()
hess = np.abs(rgb - np.roll(rgb,1,axis=0) - np.roll(rgb,1,axis=1) + np.roll(np.roll(rgb,1,axis=0),1,axis=1)).mean()
print("Borke Selbstkontrolle:")
print("  Mittel RGB %.3f %.3f %.3f -> braun, nicht schwarz/weiss -> %s" % (*m, "OK" if m[0]>m[1]>=m[2] and 0.05<m[0]<0.6 else "NOK"))
print("  u-Naht %.2f/255 -> <=6 -> %s" % (seam, "OK" if seam<=6 else "NOK"))
print("  Rissanteil (crack>0.3) %.1f%% -> 4..30%% -> %s" % ((crack>0.3).mean()*100, "OK" if 0.04<(crack>0.3).mean()<0.30 else "NOK"))
print("  Moosanteil %.1f%% -> 3..25%% -> %s" % ((moss>0.3).mean()*100, "OK" if 0.03<(moss>0.3).mean()<0.25 else "NOK"))
