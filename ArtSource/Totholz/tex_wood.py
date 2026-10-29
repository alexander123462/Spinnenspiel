# MODELL16: Holz-Schnittflaeche mit Jahresringen (UV = Scheibe, Mitte 0.5/0.5).
# Jahresringe als konzentrische Baelle mit Wuchs-Ungleichmaessigkeit, dunkler
# Markbereich, radiale Trocknungsrisse, Rindenrand. Selbstkontrolle am Ende.
import numpy as np, os
from PIL import Image
S = 512
OUT = ["C:/Users/vorau/dev/Spinnenspiel/ArtSource/Totholz/Textures/Totholz_Wood_Albedo.png",
       "C:/Users/vorau/dev/Spinnenspiel/Assets/_Project/Art/Plants/Totholz/Textures/Totholz_Wood_Albedo.png"]
for o in OUT: os.makedirs(os.path.dirname(o), exist_ok=True)
y, x = np.meshgrid(np.arange(S)/S, np.arange(S)/S)
cx, cy = 0.52, 0.48
dx, dy = (x-cx), (y-cy)
r = np.sqrt(dx*dx+dy*dy); a = np.arctan2(dy, dx)

def anoise(na, seed):
    g = np.random.default_rng(seed).standard_normal(na)
    fa = (a/(2*np.pi))*na
    i0 = np.floor(fa).astype(int) % na; i1 = (i0+1) % na
    t = fa-np.floor(fa); t = t*t*(3-2*t)
    return g[i0]*(1-t)+g[i1]*t
ring_wob = 0.018*anoise(6, 1650) + 0.006*anoise(18, 1651)   # Eier-/Wuchsform
rr = r + ring_wob
rings = np.sin((rr*16.0 + 0.15*np.sin(3*a)) * np.pi) ** 2    # ~8 breite Jahresringe
fine = np.sin(rr*90*np.pi)**2 * 0.10                          # feine Spaetholz-Linien
radcrack = np.abs(np.sin(a*2.5 + 0.4)) ** 40                 # wenige, feine Trocknungsrisse
radial2 = np.abs(np.sin(a*6.1 - 0.9)) ** 48                  # zweiter Riss-Satz (duenner)
light = np.array([0.62, 0.47, 0.28])                         # Spaetholz hellbraun
dark  = np.array([0.30, 0.19, 0.09])                         # Fruehholz dunkel
col = light*rings[...,None] + dark*(1-rings)[...,None]
col *= (1 - 0.10*fine[...,None])
heart = np.clip(1 - rr/0.14, 0, 1)                           # dunkler Markkern
col = col*(1-heart[...,None]*0.5) + np.array([0.22,0.13,0.07])[None,None,:]*heart[...,None]*0.5
cr = np.clip(radial2+radcrack-0.45, 0, 1) * np.clip(rr/0.15, 0, 1)
col = col*(1-cr[...,None]*0.45)   # Risse bleiben hinter den Ringen zurueck
edge = np.clip((rr-0.42)/0.045, 0, 1)                         # dunkler Rindenrand
col = col*(1-edge[...,None]) + np.array([0.16,0.11,0.07])*edge[...,None]
rgb = np.clip(col, 0, 1)
img = Image.fromarray((rgb*255).astype(np.uint8), "RGB")
for o in OUT: img.save(o)
m = rgb.mean(axis=(0,1))
ringband = rings[(rr>0.15)&(rr<0.42)].mean()
print("Holz Selbstkontrolle:")
print("  Mittel RGB %.3f %.3f %.3f -> hellbraun -> %s" % (*m, "OK" if m[0]>m[1]>m[2] and 0.15<m[0]<0.7 else "NOK"))
print("  Ringkontrast (min/max) %.2f/%.2f -> sichtbar -> %s" % (rings.min(), rings.max(), "OK" if rings.max()-rings.min()>0.6 else "NOK"))
print("  Ringband-Mittel %.2f -> 0.35..0.65 -> %s" % (ringband, "OK" if 0.35<ringband<0.65 else "NOK"))
print("  Rindenrand deckt %.1f%% der Scheibenkante -> %s" % (edge[rr>0.44].mean()*100, "OK" if edge[rr>0.44].mean()>0.85 else "NOK"))
