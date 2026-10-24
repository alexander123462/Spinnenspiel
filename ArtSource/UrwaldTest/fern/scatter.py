# U4.1: deterministischer Poisson-Disc-Scatter der 5 Farn-Varianten ueber ein 8x8 m Patch.
# Aufruf: blender -b -P scatter.py -- [outdir] [seed]
# Output: fern_scatter.fbx (alle Instances merged, mats Leaf/Stem) + fern_scatter.json (per-Instance Transforms).
import bpy, os, sys, json, math, random
from mathutils import Matrix

A = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
out = os.path.abspath(A[0]) if A else os.path.dirname(os.path.abspath(__file__))
SEED = int(A[1]) if len(A) > 1 else 17
PATCH = 8.0          # Patch-Side in m
MIN_D = 0.42         # Poisson-Disc Minimum-Distance in m
SCALE_LO, SCALE_HI = 0.8, 1.2

# ---- Deterministic Poisson-Disc (grid-based rejection sampling) ----
def poisson(seed):
    rng = random.Random(seed)
    cell = MIN_D / math.sqrt(2.0)
    grid = {}
    pts = []
    def too_close(x, z):
        gx, gz = int(x // cell), int(z // cell)
        for dx in range(-2, 3):
            for dy in range(-2, 3):
                for (px, pz) in grid.get((gx + dx, gz + dy), ()):
                    if (px - x) ** 2 + (pz - z) ** 2 < MIN_D * MIN_D:
                        return True
        return False
    first = (rng.random() * PATCH, rng.random() * PATCH)
    pts.append(first)
    grid.setdefault((int(first[0] // cell), int(first[1] // cell)), []).append(first)
    active = [first]
    while active:
        i = rng.randrange(len(active))
        bx, bz = active[i]
        placed = False
        for _ in range(30):
            ang = rng.random() * 2 * math.pi
            r = MIN_D * (1.0 + rng.random())
            x, z = bx + r * math.cos(ang), bz + r * math.sin(ang)
            if not (0 <= x < PATCH and 0 <= z < PATCH):
                continue
            if too_close(x, z):
                continue
            pts.append((x, z))
            grid.setdefault((int(x // cell), int(z // cell)), []).append((x, z))
            active.append((x, z))
            placed = True
            break
        if not placed:
            active.pop(i)
    return pts

# ---- Load die 5 Variant-FBX (each: <fern_i>_leaves + <fern_i>_stem) ----
def load_variant(path):
    # Each FBX brings uniquely named meshes (fern_<i>_stem/leaves) -> no clearing needed.
    bpy.ops.import_scene.fbx(filepath=path)

def all_meshes():
    out_obs = []
    def walk(col):
        for o in col.objects:
            if o.type == "MESH":
                out_obs.append(o)
        for ch in col.children:
            walk(ch)
    walk(bpy.context.scene.collection)
    return out_obs

FERN_DIR = os.path.dirname(os.path.abspath(__file__))
variants = []
for i in range(1, 6):
    fbx = os.path.join(FERN_DIR, f"fern_{i}.fbx")
    load_variant(fbx)
    parts = {}
    for o in all_meshes():
        if f"fern_{i}_leaves" in o.name:
            parts["leaves"] = o
        elif f"fern_{i}_stem" in o.name:
            parts["stem"] = o
    if len(parts) == 2:
        variants.append(parts)
        print(f"variant {i}: parts={sorted(parts)}")

assert len(variants) == 5, f"expected 5 fern variants, got {len(variants)}"

# ---- Place instances: deterministic Poisson-Disc ueber ein 8x8 m Patch ----
points = poisson(SEED)
rng = random.Random(SEED * 7919)
instances = []
for (x, z) in points:
    vi = rng.randrange(5)
    rot = rng.random() * 2 * math.pi
    sc = SCALE_LO + rng.random() * (SCALE_HI - SCALE_LO)
    instances.append({"variant": vi + 1, "x": round(x, 3), "z": round(z, 3),
                      "rot_z": round(rot, 4), "scale": round(sc, 3)})
print(f"poisson: {len(points)} Instances (seed={SEED}, min_d={MIN_D}, patch={PATCH})")

# ---- Clone each variant at its Place: one joined Mesh-Object per Instance ----
placed = []
for idx, inst in enumerate(instances):
    parts = variants[inst["variant"] - 1]
    mat = Matrix.Translation((inst["x"], inst["z"], 0.0)) @ Matrix.Rotation(inst["rot_z"], 4, "Z") @ Matrix.Scale(inst["scale"], 4)
    clones = []
    for key in ("stem", "leaves"):
        src = parts[key]
        cp = src.copy()               # new Object, but shares Mesh-Data
        cp.data = bpy.data.meshes.new_from_object(src)  # fresh unique Mesh -> no shared mesh in FBX
        cp.matrix_world = mat @ src.matrix_world
        bpy.context.scene.collection.objects.link(cp)
        clones.append(cp)
    bpy.ops.object.select_all(action="DESELECT")
    for o in clones:
        o.select_set(True)
    bpy.context.view_layer.objects.active = clones[0]
    bpy.ops.object.join()
    placed.append(bpy.context.view_layer.objects.active)
print(f"placed: {len(placed)} joined Instance-Meshes")

# ---- Export: one FBX with baked Transforms + JSON with the instance layout ----
fp = os.path.join(out, "fern_scatter.fbx")
bpy.ops.object.select_all(action="DESELECT")
for o in placed:
    o.select_set(True)
bpy.context.view_layer.objects.active = placed[0]
bpy.ops.export_scene.fbx(filepath=fp, use_selection=True,
    apply_scale_options="FBX_SCALE_ALL", axis_forward="-Z", axis_up="Y", bake_space_transform=True,
    mesh_smooth_type="FACE", path_mode="STRIP", use_mesh_modifiers=True)
print(f"Export: {os.path.basename(fp)}")

jp = os.path.join(out, "fern_scatter.json")
with open(jp, "w", encoding="utf-8") as f:
    json.dump({"seed": SEED, "patch": PATCH, "min_dist": MIN_D,
               "count": len(instances), "instances": instances}, f)
print(f"JSON: {os.path.basename(jp)} ({len(instances)} instances)")
print("FERTIGO")
