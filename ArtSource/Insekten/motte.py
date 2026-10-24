# Motte prozedural: Koerper (Lathe) + 2 Broadsheet-Wings + Antennalen, FBX-Export, Vorschau.
# Aufruf: blender -b -P motte.py -- <outdir> [render]
import bpy, math, sys, os
import numpy as np

A = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else sys.argv[1:]
out = os.path.abspath(A[0]); do_render = len(A) > 1
rng = np.random.default_rng(7)
bpy.ops.wm.read_factory_settings(use_empty=True)
TAU = 2.0 * math.pi

def make_mesh(name, verts, faces):
    me = bpy.data.meshes.new(name)
    me.from_pydata(verts, [], faces)
    me.update()
    ob = bpy.data.objects.new(name, me)
    bpy.context.scene.collection.objects.link(ob)
    return ob

# Koerper: Lathe um Z (t -1..1), Profil [t, r] – langsamotter Leib, Rumpf thoraix smaller.
BODY_PROFILE = [(0.0,0.002),(0.08,0.005),(0.2,0.009),(0.45,0.011),(0.7,0.009),
                (0.85,0.010),(1.0,0.004)]

def lathe(profile, na=16):
    ts = [p[0] for p in profile]; rs = [p[1] for p in profile]
    verts, faces, uv = [], [], []
    ring_start = []
    for i, (t, r) in enumerate(profile):
        y = 0.05 - 0.10 * t   # 0.10 m lang, Blender -Y = Forward (Unity +Z)
        ring_start.append(len(verts))
        for j in range(na):
            phi = TAU * j / na
            verts.append((r * math.cos(phi), y, 0.9 * r * math.sin(phi)))
    for i in range(len(profile) - 1):
        a0, a1 = ring_start[i], ring_start[i + 1]
        for j in range(na):
            faces.append((a0 + j, a0 + (j + 1) % na, a1 + (j + 1) % na, a1 + j))
    # Polkappen (Facher)
    for pol_i, ring_i, sign in ((len(profile), 0, -1), (len(profile) + 1, len(profile) - 1, 1)):
        c = len(verts); verts.append((0.0, -0.05 if sign < 0 else 0.05, 0.0))
        a0 = ring_start[ring_i]
        for j in range(na):
            f = (c, a0 + j, a0 + (j + 1) % na)
            faces.append(f if sign > 0 else (c, a0 + (j + 1) % na, a0 + j))
    return verts, faces


# Wings: broadsheet (Moths = Schmalen, Wide, Slightly Downward), lie in XY-Plane.
def wing_shape(span, chord):
    # Simple 6-Eck Outline (Root -> Outer Tip -> Trailing Edge)
    pts = [(0.0, 0.0), (0.15 * span, 0.35 * chord), (span, 0.45 * chord),
           (span, -0.15 * chord), (0.35 * span, -0.55 * chord), (0.0, -0.35 * chord)]
    return pts

def add_wing(name, mirror):
    # Spans seitlich (+/-X), Chord = +/-Y (Forward/Back); 2 Facets (Front/Back) -> beidaseite sichtbar.
    pts = wing_shape(0.030, 0.022)
    verts = [(mirror * x, y, 0.002) for (x, y) in pts]
    faces = [(0, 1, 2, 3, 4, 5), (0, 5, 4, 3, 2, 1)]
    ob = make_mesh(name, verts, faces)
    return ob

def add_antenna(name, mirror):
    # Vorwärts = -Y (Blender), Aussen = ±X; Flat Ribbon in XY-Plane.
    pts = [(0.004 * mirror, -0.040), (0.010 * mirror, -0.052), (0.016 * mirror, -0.062)]
    verts, faces = [], []
    r = 0.0008
    for i in range(len(pts) - 1):
        (x0, y0), (x1, y1) = pts[i], pts[i + 1]
        verts += [(x0 - r, y0, 0.004), (x0 + r, y0, 0.004),
                  (x1 + r, y1, 0.004), (x1 - r, y1, 0.004)]
        faces.append((len(verts) - 4, len(verts) - 3, len(verts) - 2, len(verts) - 1))
    return make_mesh(name, verts, faces)


body = make_mesh("Koerper", *lathe(BODY_PROFILE))
wing_l = add_wing("Wing_L", -1.0)
wing_r = add_wing("Wing_R", 1.0)
ant_l = add_antenna("Antenna_L", -1.0)
ant_r = add_antenna("Antenna_R", 1.0)

# Normalen-Robustez: Faecher-Aussen orientieren (Normal * (Centroid-Center) > 0), sonst Flip.
import mathutils
def fix_normals(ob):
    center = sum((mathutils.Vector(v.co) for v in ob.data.vertices), mathutils.Vector((0.0, 0.0, 0.0))) / max(1, len(ob.data.vertices))
    flipped = 0
    for p in ob.data.polygons:
        c = ob.matrix_world @ mathutils.Vector(p.center)
        n = (ob.matrix_world @ mathutils.Vector(p.normal)).normalized()
        if n.dot(c - center) < 0:
            flipped += 1
    if flipped > len(ob.data.polygons) / 2:
        for p in reversed(list(ob.data.polygons)):
            p.vertices = list(reversed(p.vertices))
        print(f"flipped={ob.name}")

for ob in (body, wing_l, wing_r, ant_l, ant_r): fix_normals(ob)

# Checksums: Bounds + Triangle Count (Ziel: clearly larger than a fly ~0.044 m)
all_obs = [body, wing_l, wing_r, ant_l, ant_r]
tris = sum(len(o.data.polygons) for o in all_obs)
xs = []; ys = []; zs = []
for o in all_obs:
    for c in o.bound_box:
        w = o.matrix_world @ mathutils.Vector(c)
        xs.append(w.x); ys.append(w.y); zs.append(w.z)
size = (max(xs) - min(xs), max(ys) - min(ys), max(zs) - min(zs))
print(f"MOTTE tris={tris} size={size[0]:.4f},{size[1]:.4f},{size[2]:.4f}")

os.makedirs(out, exist_ok=True)
fbx_path = os.path.join(out, "Motte.fbx")
bpy.ops.export_scene.fbx(filepath=fbx_path, use_selection=False, apply_scale_options="FBX_SCALE_ALL",
                         axis_forward="-Z", axis_up="Y", bake_space_transform=True,
                         mesh_smooth_type="FACE", path_mode="STRIP", use_mesh_modifiers=True,
                         object_types={"MESH"})
print(f"FBX -> {fbx_path}")
