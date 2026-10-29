# MODELL16: Normal-Maps aus Hohenfeldern (Borke: Risse+Koerner, Holz: Ringrelief).
# Sobel auf 1024/512-Hoehe -> PNG (RGB 0.5-neutral). Aufruf: python tex_normals.py
import numpy as np, os
from PIL import Image
D = "C:/Users/vorau/dev/Spinnenspiel"
def sobel_normal(h, strength):
    gy = (np.roll(h,-1,0)-np.roll(h,1,0))*strength
    gx = (np.roll(h,-1,1)-np.roll(h,1,1))*strength
    nz = 1.0/np.sqrt(gx*gx+gy*gy+1)
    rgb = np.stack([0.5-0.5*gx*nz, 0.5-0.5*gy*nz, nz], -1)
    return (np.clip(rgb,0,1)*255).astype(np.uint8)

def pnoise(u, v, nu, nv, seed_rng):
    g = seed_rng.standard_normal((nv+1, nu))
    fu, fv = u*nu, v*nv
    i0 = np.floor(fu).astype(int); j0 = np.floor(fv).astype(int)
    i1 = (i0+1)%nu; j1 = np.clip(j0+1, 0, nv)
    tx = fu-np.floor(fu); ty = fv-np.floor(fv)
    tx = tx*tx*(3-2*tx); ty = ty*ty*(3-2*ty)
    a = g[j0,i0]*(1-tx)+g[j0,i1]*tx; b = g[j1,i0]*(1-tx)+g[j1,i1]*tx
    return a*(1-ty)+b*ty

W, H = 1024, 512
u, v = np.meshgrid(np.arange(W)/W, np.arange(H)/H)
n_small = sum(pnoise(u,v,16*2**o,8*2**o,np.random.default_rng(1600+o))/2**o for o in range(3))
fm = 0.6 + 0.8*(np.sin((v*7+0.3)*2*np.pi)*0.5+0.5)
fur = np.abs(np.sin(u*np.pi*fm*2 + 0.35*n_small))
crack_long = (1-fur)**4
qphase = pnoise(u,v,24,1,np.random.default_rng(1630))
qv = v*7.0 + qphase*2.2 + 0.35*n_small
qfur = np.abs(np.sin(qv*np.pi))
crack_trans = (1-qfur)**7 * (0.30+0.50*fur)
crack = np.clip(crack_long + crack_trans, 0, 1)
h = 0.5 + 0.10*n_small - 0.50*crack + 0.18*(1-fur)**2
out_b = D + "/ArtSource/Totholz/Textures/Totholz_Bark_Normal.png"
out_bw = D + "/Assets/_Project/Art/Plants/Totholz/Textures/Totholz_Bark_Normal.png"
Image.fromarray(sobel_normal(h, 2.4), "RGB").save(out_b)
Image.fromarray(sobel_normal(h, 2.4), "RGB").save(out_bw)

S = 512
y, x = np.meshgrid(np.arange(S)/S, np.arange(S)/S)
dx, dy = x-0.52, y-0.48
r = np.sqrt(dx*dx+dy*dy); a = np.arctan2(dy, dx)
hw = 0.018*(np.sin(a*6+1.3)*0.5+0.5) + 0.006*np.sin(a*18)
rr = r + hw
hv = 0.5 + 0.30*np.sin((rr*16.0+0.15*np.sin(3*a))*np.pi)**2 - 0.4*np.clip(np.abs(np.sin(a*3.5+0.4))**26+np.abs(np.sin(a*8.3-0.9))**34-0.25,0,1)
out_w = D + "/ArtSource/Totholz/Textures/Totholz_Wood_Normal.png"
out_ww = D + "/Assets/_Project/Art/Plants/Totholz/Textures/Totholz_Wood_Normal.png"
Image.fromarray(sobel_normal(hv, 1.2), "RGB").save(out_w)
Image.fromarray(sobel_normal(hv, 1.2), "RGB").save(out_ww)
for f in (out_b, out_w):
    im = np.asarray(Image.open(f)).astype(int)
    dev = np.abs(im.mean(axis=(0,1)) - [128,128,255*0 + im[:,:,2].mean()]).max() if False else 0
print("Normalmaps geschrieben (Bark 1024x512, Wood 512x512):")
for f in (out_b, out_bw, out_w, out_ww):
    print(" ", os.path.basename(f), os.path.getsize(f)//1024, "KB -> vorhanden -> OK" if os.path.exists(f) else "NOK")
