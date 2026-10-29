# Riesenblatt (Alocasia-artig) prozedural bauen, FBX fuer Unity exportieren, Vorschau rendern.
# Aufruf: blender -b -P riesenblatt.py -- <outdir> <seed> [render]   (Texturen vorher: python tex_leaf.py <outdir>/Textures)
import bpy, math, sys, os
import numpy as np
from mathutils import Matrix, Vector

A = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else sys.argv[1:]
out = os.path.abspath(A[0]); seed = int(A[1]); do_render = len(A) > 2
tex = os.path.join(out, "Textures")
rng = np.random.default_rng(seed)
bpy.ops.wm.read_factory_settings(use_empty=True)

# ---------------- Geometrie ----------------
NS, NT = 34, 17
def leaf(L, W, fold, droop, wave, lobe):
    s = np.linspace(0, 1, NS); t = np.linspace(-1, 1, NT)
    g = (1 - s) ** 0.75 * (0.8 + 1.4 * s); g /= g.max()
    ph = rng.uniform(0, 6.28); fr = rng.uniform(1.5, 3)
    verts, uvs = [], []
    for i, si in enumerate(s):
        for tj in t:
            at = abs(tj)
            x = tj * W * g[i]
            y = L * si - lobe * L * at ** 1.6 * (1 - si) ** 3
            z = fold * abs(x) - droop * L * (max(y, 0) / L) ** 2
            z += wave * 0.02 * L * math.sin(si * fr * 6.283 + ph + tj * 2) * at ** 3
            z -= 0.12 * L * max(0.0, si - 0.82) ** 2 * 10
            verts.append((x, y, z)); uvs.append(((tj + 1) / 2, si))
    faces = []
    for i in range(NS - 1):
        for j in range(NT - 1):
            a = i * NT + j
            faces.append((a, a + 1, a + NT + 1, a + NT))
    return verts, faces, uvs

def tube(P0, C, P1, r0, r1, rings=18, seg=8):
    verts, uvs, faces = [], [], []
    P0, C, P1 = map(np.array, (P0, C, P1))
    pts = [(1 - k) ** 2 * P0 + 2 * (1 - k) * k * C + k ** 2 * P1 for k in np.linspace(0, 1, rings)]
    for i, p in enumerate(pts):
        tan = pts[min(i + 1, rings - 1)] - pts[max(i - 1, 0)]; tan /= np.linalg.norm(tan)
        side = np.cross(tan, [0, 0, 1.0]);
        if np.linalg.norm(side) < 1e-4: side = np.array([1.0, 0, 0])
        side /= np.linalg.norm(side); up = np.cross(side, tan)
        r = r0 + (r1 - r0) * i / (rings - 1)
        for j in range(seg + 1):
            a = j / seg * 6.283
            verts.append(tuple(p + r * (math.cos(a) * side + math.sin(a) * up)))
            uvs.append((j / seg, i / (rings - 1)))
    for i in range(rings - 1):
        for j in range(seg):
            a = i * (seg + 1) + j
            faces.append((a, a + seg + 1, a + seg + 2, a + 1))
    return verts, faces, uvs

V, F, UV, MAT = [], [], [], []
def add(v, f, uv, m):
    o = len(V); V.extend(v); UV.extend(uv)
    for fc in f: F.append(tuple(x + o for x in fc)); MAT.append(m)

n = rng.integers(5, 8)
for k in range(n):
    yaw = k / n * 6.283 + rng.uniform(-0.3, 0.3)
    L = rng.uniform(0.75, 1.15); W = L * rng.uniform(0.42, 0.5)
    h = rng.uniform(0.55, 1.05); r = rng.uniform(0.18, 0.38)
    pitch = math.radians(rng.uniform(-50, -20))
    d = np.array([math.cos(yaw), math.sin(yaw), 0])
    P1 = d * r + np.array([0, 0, h]); C = d * r * 0.15 + np.array([0, 0, h * 0.85])
    sv, sf, su = tube((0, 0, 0), C, P1, 0.034, 0.015)
    add(sv, sf, su, 1)
    lv, lf, lu = leaf(L, W, rng.uniform(0.10, 0.22), rng.uniform(0.15, 0.35), rng.uniform(0.5, 1.2), rng.uniform(0.30, 0.42))
    M = Matrix.Translation(Vector(P1)) @ Matrix.Rotation(yaw - math.pi / 2, 4, 'Z') @ Matrix.Rotation(pitch, 4, 'X') \
        @ Matrix.Rotation(rng.uniform(-0.15, 0.15), 4, 'Y')
    lv = [tuple(M @ Vector(p)) for p in lv]
    add(lv, lf, lu, 0)

me = bpy.data.meshes.new("Riesenblatt")
me.from_pydata(V, [], F); me.update()
uvl = me.uv_layers.new(name="UVMap")
for poly in me.polygons:
    poly.material_index = MAT[poly.index]; poly.use_smooth = True
    for li in poly.loop_indices:
        uvl.data[li].uv = UV[me.loops[li].vertex_index]
ob = bpy.data.objects.new(f"Riesenblatt_{seed}", me)
bpy.context.scene.collection.objects.link(ob)

def img(name, colour=True):
    im = bpy.data.images.load(os.path.join(tex, name))
    if not colour: im.colorspace_settings.name = "Non-Color"
    return im

def mat_leaf():
    m = bpy.data.materials.new("M_Riesenblatt_Leaf"); nt = m.node_tree; N = nt.nodes
    bsdf = N["Principled BSDF"]
    a = N.new("ShaderNodeTexImage"); a.image = img("Riesenblatt_Leaf_Albedo.png")
    nm = N.new("ShaderNodeTexImage"); nm.image = img("Riesenblatt_Leaf_Normal.png", False)
    mk = N.new("ShaderNodeTexImage"); mk.image = img("Riesenblatt_Leaf_Mask.png", False)
    nmap = N.new("ShaderNodeNormalMap"); inv = N.new("ShaderNodeMath"); inv.operation = "SUBTRACT"; inv.inputs[0].default_value = 1
    nt.links.new(a.outputs[0], bsdf.inputs["Base Color"])
    nt.links.new(nm.outputs[0], nmap.inputs["Color"]); nt.links.new(nmap.outputs[0], bsdf.inputs["Normal"])
    nt.links.new(mk.outputs["Alpha"], inv.inputs[1]); nt.links.new(inv.outputs[0], bsdf.inputs["Roughness"])
    tr = N.new("ShaderNodeBsdfTranslucent"); nt.links.new(a.outputs[0], tr.inputs[0])
    mix = N.new("ShaderNodeMixShader"); mix.inputs[0].default_value = 0.3
    nt.links.new(bsdf.outputs[0], mix.inputs[1]); nt.links.new(tr.outputs[0], mix.inputs[2])
    nt.links.new(mix.outputs[0], N["Material Output"].inputs[0])
    return m

def mat_stem():
    m = bpy.data.materials.new("M_Riesenblatt_Stem"); nt = m.node_tree
    a = nt.nodes.new("ShaderNodeTexImage"); a.image = img("Riesenblatt_Stem_Albedo.png")
    b = nt.nodes["Principled BSDF"]; b.inputs["Roughness"].default_value = 0.45
    nt.links.new(a.outputs[0], b.inputs["Base Color"])
    return m

me.materials.append(mat_leaf()); me.materials.append(mat_stem())

os.makedirs(out, exist_ok=True)
bpy.ops.object.select_all(action="DESELECT"); ob.select_set(True); bpy.context.view_layer.objects.active = ob
bpy.ops.export_scene.fbx(filepath=os.path.join(out, f"Riesenblatt_{seed}.fbx"), use_selection=True,
    apply_scale_options="FBX_SCALE_ALL", axis_forward="-Z", axis_up="Y", bake_space_transform=True,
    mesh_smooth_type="FACE", path_mode="STRIP", use_mesh_modifiers=True)
print("Polys", len(me.polygons), "Verts", len(me.vertices))

if do_render:
    sc = bpy.context.scene
    # Boden
    bpy.ops.mesh.primitive_plane_add(size=12)
    g = bpy.context.object; gm = bpy.data.materials.new("Ground"); nt = gm.node_tree
    nz = nt.nodes.new("ShaderNodeTexNoise"); nz.inputs["Scale"].default_value = 9; nz.inputs["Detail"].default_value = 8
    cr = nt.nodes.new("ShaderNodeValToRGB"); cr.color_ramp.elements[0].color = (0.035, 0.022, 0.012, 1); cr.color_ramp.elements[1].color = (0.11, 0.075, 0.035, 1)
    nt.links.new(nz.outputs[0], cr.inputs[0]); nt.links.new(cr.outputs[0], nt.nodes["Principled BSDF"].inputs["Base Color"])
    nt.nodes["Principled BSDF"].inputs["Roughness"].default_value = 0.85
    g.data.materials.append(gm)
    # Licht
    w = bpy.data.worlds.new("W"); sc.world = w; wn = w.node_tree
    sky = wn.nodes.new("ShaderNodeTexSky"); sky.sky_type = "MULTIPLE_SCATTERING"
    sky.sun_elevation = math.radians(35); sky.sun_rotation = math.radians(140)
    wn.links.new(sky.outputs[0], wn.nodes["Background"].inputs[0]); wn.nodes["Background"].inputs[1].default_value = 0.35
    bpy.ops.object.light_add(type="SUN"); sun = bpy.context.object; sun.data.energy = 3.0; sun.data.angle = math.radians(2)
    sun.rotation_euler = (math.radians(55), 0, math.radians(140 + 180 - 90))
    sc.render.engine = "CYCLES"; sc.cycles.device = "CPU"; sc.cycles.samples = 48; sc.cycles.use_denoising = True
    sc.render.resolution_x, sc.render.resolution_y = 960, 540
    sc.view_settings.look = "AgX - Medium High Contrast"
    cam = bpy.data.cameras.new("C"); co = bpy.data.objects.new("Cam", cam); sc.collection.objects.link(co); sc.camera = co
    def shot(loc, tgt, lens, name):
        co.location = loc; cam.lens = lens
        co.rotation_euler = (Vector(tgt) - Vector(loc)).to_track_quat("-Z", "Y").to_euler()
        sc.render.filepath = os.path.join(out, name); bpy.ops.render.render(write_still=True)
    shot((2.3, -2.6, 1.3), (0, 0, 0.55), 40, f"preview_{seed}_overview.png")
    shot((0.35, -0.75, 0.04), (0.1, 0.2, 0.8), 16, f"preview_{seed}_spinne.png")
print("fertig")
