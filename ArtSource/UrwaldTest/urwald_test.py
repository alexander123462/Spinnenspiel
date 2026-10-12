# Minimalen Urwald-Testarea: Ground + 5 Baeume + 3 Farn-Faechers, FBX-Export, Vorschau-PNG.
# Aufruf: blender -b -P urwald_test.py -- <outdir> <seed> [render]
import bpy, math, sys, os
import numpy as np
from mathutils import Vector

A = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else sys.argv[1:]
out = os.path.abspath(A[0]); seed = int(A[1]); do_render = len(A) > 2
rng = np.random.default_rng(seed)
bpy.ops.wm.read_factory_settings(use_empty=True)
sc = bpy.context.scene

def name_obj(ob, n):
    ob.name = n
    return ob

# ---------------- Ground: 8x8 m, 32x32 Subdiv, Gently Noise-Displaced (procedural, no bpy.ops.mesh) ----------------
N = 32
gv, gf = [], []
for j in range(N + 1):
    for i in range(N + 1):
        x = -4.0 + 8.0 * i / N; y = -4.0 + 8.0 * j / N
        edge = abs(x) > 3.9 or abs(y) > 3.9
        h = 0.10 * float(rng.uniform(-1, 1)) if not edge else 0.0
        h += 0.08 * math.sin(x * 1.3) * math.cos(y * 1.1)
        gv.append((x, y, round(h, 4)))
for j in range(N):
    for i in range(N):
        a = j * (N + 1) + i
        gf.append((a, a + 1, a + N + 2, a + N + 1))
gme = bpy.data.meshes.new("Ground_8m")
gme.from_pydata(gv, [], gf); gme.update()
ground = bpy.data.objects.new("Ground_8m", gme)
sc.collection.objects.link(ground)

# ---------------- 5 Baeume: Cylinder-Trunk + Icosphere-Crown ----------------
trees = []
for i in range(5):
    x = float(rng.uniform(-3, 3)); y = float(rng.uniform(-3, 3))
    h = float(rng.uniform(1.2, 2.2)); r = float(rng.uniform(0.06, 0.10))
    bpy.ops.mesh.primitive_cylinder_add(vertices=10, radius=r, depth=h, location=(x, y, h/2))
    trunk = bpy.context.object; name_obj(trunk, f"Tree{i+1}_Trunk")
    cr = float(rng.uniform(0.45, 0.8))
    bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=2, radius=cr, location=(x, y, h + cr*0.6))
    crown = bpy.context.object; name_obj(crown, f"Tree{i+1}_Crown")
    trees.append((trunk, crown))

# ---------------- 3 Farn-Faechers: simple Planes fanned out ----------------
ferns = []
for i in range(3):
    x = float(rng.uniform(-2.5, 2.5)); y = float(rng.uniform(-2.5, 2.5))
    for k in range(6):
        yaw = k / 6 * 2 * math.pi + float(rng.uniform(-0.15, 0.15))
        L = float(rng.uniform(0.35, 0.55)); w = 0.12
        bpy.ops.mesh.primitive_plane_add(size=1.0)
        leaf = bpy.context.object
        leaf.scale = (L/2, w/2, 1.0)
        # lay flat-ish, slight tilt: Quaternion(axis, angle)->to_matrix()->to_4x4() (Blender-5.x-safe)
        from mathutils import Matrix
        tilt = Matrix.Rotation(math.radians(25), 4, 'X')
        spin = Matrix.Rotation(yaw, 4, 'Z')
        leaf.matrix_world = spin @ tilt @ Matrix.Translation((x + L/2*math.cos(yaw), y + L/2*math.sin(yaw), 0.05))
        name_obj(leaf, f"Fern{i+1}_Leaf{k+1}")
        ferns.append(leaf)

# ---------------- simple Materials ----------------
def simple_mat(nm, col):
    m = bpy.data.materials.new(nm); m.diffuse_color = (*col, 1.0)
    return m
mg, mt, mc, mf = simple_mat("M_Ground", (.12,.09,.05)), simple_mat("M_Trunk", (.25,.15,.08)), \
     simple_mat("M_Crown", (.10,.30,.10)), simple_mat("M_Fern", (.15,.40,.12))
ground.data.materials.append(mg)
for t, c in trees:
    t.data.materials.append(mt); c.data.materials.append(mc)
for f in ferns:
    f.data.materials.append(mf)

# ---------------- FBX-Export (call-style copied from baumfarn.py) ----------------
all_objs = [ground] + [o for pair in trees for o in pair] + ferns
bpy.ops.object.select_all(action="DESELECT")
for o in all_objs:
    o.select_set(True)
bpy.context.view_layer.objects.active = all_objs[0]
fbx_path = os.path.join(out, "urwald_test.fbx")
os.makedirs(out, exist_ok=True)
bpy.ops.export_scene.fbx(filepath=fbx_path, use_selection=True,
    apply_scale_options="FBX_SCALE_ALL", axis_forward="-Z", axis_up="Y", bake_space_transform=True,
    mesh_smooth_type="FACE", path_mode="STRIP", use_mesh_modifiers=True)
print(f"Objects exported: {len(all_objs)} (1 ground + 5 trunks + 5 crowns + 18 fern leaves)")
print(f"FBX written: {fbx_path}")

# ---------------- Vorschau-Rendering (proven from baumfarn.py) ----------------
if do_render:
    w = bpy.data.worlds.new("W"); sc.world = w; wn = w.node_tree
    sky = wn.nodes.new("ShaderNodeTexSky"); sky.sky_type = "MULTIPLE_SCATTERING"
    sky.sun_elevation = math.radians(35); sky.sun_rotation = math.radians(140)
    wn.links.new(sky.outputs[0], wn.nodes["Background"].inputs[0])
    wn.nodes["Background"].inputs[1].default_value = 0.35
    bpy.ops.object.light_add(type="SUN")
    sun = bpy.context.object; sun.data.energy = 3.0; sun.data.angle = math.radians(2)
    sun.rotation_euler = (math.radians(55), 0, math.radians(230))
    sc.render.engine = "CYCLES"; sc.cycles.device = "CPU"
    sc.cycles.samples = 32; sc.cycles.use_denoising = True
    sc.render.resolution_x, sc.render.resolution_y = 960, 540
    cam = bpy.data.cameras.new("Cam"); co = bpy.data.objects.new("Cam", cam)
    sc.collection.objects.link(co); sc.camera = co
    cam.lens = 35
    loc = np.array([7.0*math.cos(math.radians(-30)), 7.0*math.sin(math.radians(-30)), 2.2])
    tgt = np.array([0.0, 0.0, 0.8])
    co.location = tuple(loc)
    co.rotation_euler = (Vector(tgt) - Vector(loc)).to_track_quat("-Z", "Y").to_euler()
    sc.render.filepath = os.path.join(out, "urwald_test_preview.png")
    bpy.ops.render.render(write_still=True)
    print("Vorschau: urwald_test_preview.png")

print("fertig")
