# 2k-Texturen der Poly-Haven-Baeume -> 1k fuer Unity/HDRP.
# Aufruf: blender -b -P ph_baum_texturen.py -- <quellordner AssetSource/ph_baeume_2k> <zielordner Assets/.../PH_Baeume>
# Ergebnis je Baum in <ziel>/<id>/Textures: <mat>_Albedo.png (Blaetter mit Alpha), <mat>_Normal.png, <mat>_Mask.png (HDRP: R=Metal 0, G=AO 1, B=Detail 0, A=Glaette)
import bpy, sys, os, glob
import numpy as np
A = sys.argv[sys.argv.index("--") + 1:]
src, dst = os.path.abspath(A[0]), os.path.abspath(A[1])
S = 1024

def load(path):
    im = bpy.data.images.load(path); im.colorspace_settings.name = "Non-Color"
    im.scale(S, S); px = np.array(im.pixels[:], dtype=np.float32).reshape(S, S, 4); bpy.data.images.remove(im); return px

def save(px, path):
    im = bpy.data.images.new(os.path.basename(path), S, S, alpha=True)
    im.colorspace_settings.name = "Non-Color"; im.pixels[:] = np.clip(px, 0, 1).ravel()
    im.filepath_raw = path; im.file_format = "PNG"; im.save(); bpy.data.images.remove(im)

for tdir in sorted(glob.glob(os.path.join(src, "*"))):
    tid = os.path.basename(tdir); out = os.path.join(dst, tid, "Textures"); os.makedirs(out, exist_ok=True)
    for diff in glob.glob(os.path.join(tdir, "*_diff_2k.*")):
        mat = os.path.basename(diff).split("_diff_2k")[0]
        alb = load(diff)
        alpha = glob.glob(os.path.join(tdir, mat + "_alpha_2k.*"))
        alb[..., 3] = load(alpha[0])[..., 0] if alpha else 1.0
        save(alb, os.path.join(out, mat + "_Albedo.png"))
        nor = glob.glob(os.path.join(tdir, mat + "_nor_gl_2k.*"))
        if nor:
            n = load(nor[0]); n[..., 3] = 1.0; save(n, os.path.join(out, mat + "_Normal.png"))
        rough = glob.glob(os.path.join(tdir, mat + "_rough_2k.*"))
        r = load(rough[0])[..., 0] if rough else np.full((S, S), 0.7, np.float32)
        mask = np.zeros((S, S, 4), np.float32); mask[..., 1] = 1.0; mask[..., 3] = 1.0 - r
        save(mask, os.path.join(out, mat + "_Mask.png"))
        print(f"{tid}/{mat}: Albedo{' +Alpha' if alpha else ''}, Normal {'ja' if nor else 'nein'}, Mask aus {'rough' if rough else 'Standard 0.7'}")
print("fertig")
