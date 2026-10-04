# Baumfarn (Cyathea-artig) prozedural bauen, FBX fuer Unity exportieren, Vorschau rendern.
# Aufruf: blender -b -P baumfarn.py -- <outdir> <seed> [render]   (Texturen vorher: python tex_farn.py <outdir>/Textures)
import bpy, math, sys, os
import numpy as np
from mathutils import Matrix, Vector

A = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else sys.argv[1:]
out = os.path.abspath(A[0]); seed = int(A[1]); do_render = len(A) > 2
tex = os.path.join(out, "Textures")
rng = np.random.default_rng(seed)
bpy.ops.wm.read_factory_settings(use_empty=True)

# ---------------- Geometrie ----------------
V, F, UV, MAT = [], [], [], []
def add(v, f, uv, m):
    o = len(V); V.extend(v); UV.extend(uv)
    for fc in f: F.append(tuple(x + o for x in fc)); MAT.append(m)

def tube(P0, C, P1, r0, r1, rings=18, seg=8):
    verts, uvs, faces = [], [], []
    P0, C, P1 = map(np.array, (P0, C, P1))
    pts = [(1 - k) ** 2 * P0 + 2 * (1 - k) * k * C + k ** 2 * P1 for k in np.linspace(0, 1, rings)]
    for i, p in enumerate(pts):
        tan = pts[min(i + 1, rings - 1)] - pts[max(i - 1, 0)]; tan /= np.linalg.norm(tan)
        side = np.cross(tan, [0, 0, 1.0])
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

# --- Stamm: 2.5-4.5 m, leicht gebogen, raue Faserrinde (Textur) ----------
H = rng.uniform(2.5, 4.5)
r0 = rng.uniform(0.11, 0.14)
bend = np.array([rng.uniform(-0.35, 0.35), rng.uniform(-0.35, 0.35), 0.0])
P0t = np.array([0.0, 0.0, 0.0])
P1t = np.array([0.0, 0.0, H]) + bend
Ct = (P0t + P1t) / 2 + bend * 0.6
sv, sf, su = tube(P0t, Ct, P1t, r0, r0 * 0.55, rings=16, seg=8)
add(sv, sf, su, 1)

# --- Krone: 9-14 Wedel, sternfoermig, erst aufwaerts dann hangend --------
top = P1t
n = int(rng.integers(9, 15))
for k in range(n):
    yaw = k / n * 6.283 + rng.uniform(-0.25, 0.25)
    L = rng.uniform(1.8, 2.6)
    th0 = math.radians(rng.uniform(20, 40))          # zuerst leicht aufwaerts
    th1 = math.radians(-rng.uniform(55, 85))         # dann bogig nach unten
    d = np.array([math.cos(yaw), math.sin(yaw), 0.0])
    upv = np.array([0.0, 0.0, 1.0])
    R0 = L * 0.5
    Cloc = R0 * np.array([math.cos(th0), math.sin(th0)])
    Ploc = 2 * Cloc - R0 * np.array([math.cos(th1), math.sin(th1)])
    def to3(p):
        return top + d * p[0] + upv * p[1]
    # Mittelrippe
    rv, rf, ru = tube(np.array([top[0], top[1], top[2]]), to3(Cloc), to3(Ploc), 0.020, 0.007, rings=12, seg=5)
    add(rv, rf, ru, 0)
    # Fiederblaettchen: beidseitig, schmale Streifen, zur Spitze kuezer
    nrm_f = np.cross(d, upv)                                               # Wedel-Normalenvektor
    nrm_f /= np.linalg.norm(nrm_f)
    N = int(rng.integers(18, 25))
    for side in (-1, 1):
        for k2 in range(N):
            u = (k2 + (0.0 if side > 0 else 0.5)) / N
            s = 0.07 + 0.86 * u
            B = 2 * (1 - s) * s * Cloc + s ** 2 * Ploc                     # Punkt (quadratische Bezier)
            T2 = 2 * (1 - s) * Cloc + 2 * s * (Ploc - Cloc)                # Tangente (2D, Wedelplan)
            T2 /= np.linalg.norm(T2)
            T3 = d * T2[0] + upv * T2[1]                                   # in 3D
            q = np.cross(T3, nrm_f) * side                                 # waagerecht zur Mittelrippe
            q /= np.linalg.norm(q)
            # leichte Verdrehung um die Fiederachse (Nase zur Spitze)
            dl = math.radians(rng.uniform(-10, 25)) * (0.3 + 0.7 * u)
            t2 = math.cos(dl)
            q = q * t2 + nrm_f * side * math.sin(dl)
            q /= np.linalg.norm(q)
            nrm2 = np.cross(q, T3); nrm2 /= np.linalg.norm(nrm2)          # Blattflaechen-Normalenvektor
            l = L * 0.22 * (math.sin(math.pi * (0.08 + 0.84 * u)) ** 0.7)
            w = 0.26 * l
            P0p = to3(B)
            m = 3
            verts, uvs, faces = [], [], []
            for i in range(m + 1):
                f = i / m
                half = max(w * 0.5 * math.cos(f * math.pi / 2) ** 0.7, 1e-4)   # Spitze spitz
                c = P0p + q * (l * f)
                verts += [tuple(c - nrm2 * half), tuple(c + nrm2 * half)]
                uvs += [(0.0, f), (1.0, f)]
            for i in range(m):
                a = 2 * i
                faces.append((a, a + 2, a + 3, a + 1))
            add(verts, faces, uvs, 0)

me = bpy.data.meshes.new("Baumfarn")
me.from_pydata(V, [], F); me.update()
uvl = me.uv_layers.new(name="UVMap")
for poly in me.polygons:
    poly.material_index = MAT[poly.index]; poly.use_smooth = True
    for li in poly.loop_indices:
        uvl.data[li].uv = UV[me.loops[li].vertex_index]
ob = bpy.data.objects.new(f"Baumfarn_{seed}", me)
bpy.context.scene.collection.objects.link(ob)

def img(name, colour=True):
    im = bpy.data.images.load(os.path.join(tex, name))
    if not colour: im.colorspace_settings.name = "Non-Color"
    return im

def mat_leaf():
    m = bpy.data.materials.new("M_Baumfarn_Leaf"); nt = m.node_tree; N = nt.nodes
    bsdf = N["Principled BSDF"]
    a = N.new("ShaderNodeTexImage"); a.image = img("Baumfarn_Leaf_Albedo.png")
    nm = N.new("ShaderNodeTexImage"); nm.image = img("Baumfarn_Leaf_Normal.png", False)
    mk = N.new("ShaderNodeTexImage"); mk.image = img("Baumfarn_Leaf_Mask.png", False)
    nmap = N.new("ShaderNodeNormalMap"); inv = N.new("ShaderNodeMath"); inv.operation = "SUBTRACT"; inv.inputs[0].default_value = 1
    nt.links.new(a.outputs[0], bsdf.inputs["Base Color"])
    nt.links.new(a.outputs["Alpha"], bsdf.inputs["Alpha"])
    nt.links.new(nm.outputs[0], nmap.inputs["Color"]); nt.links.new(nmap.outputs[0], bsdf.inputs["Normal"])
    nt.links.new(mk.outputs["Alpha"], inv.inputs[1]); nt.links.new(inv.outputs[0], bsdf.inputs["Roughness"])
    tr = N.new("ShaderNodeBsdfTranslucent"); nt.links.new(a.outputs[0], tr.inputs[0])
    mix = N.new("ShaderNodeMixShader"); mix.inputs[0].default_value = 0.3
    nt.links.new(bsdf.outputs[0], mix.inputs[1]); nt.links.new(tr.outputs[0], mix.inputs[2])
    nt.links.new(mix.outputs[0], N["Material Output"].inputs[0])
    return m

def mat_stem():
    m = bpy.data.materials.new("M_Baumfarn_Stem"); nt = m.node_tree
    a = nt.nodes.new("ShaderNodeTexImage"); a.image = img("Baumfarn_Stem_Albedo.png")
    b = nt.nodes["Principled BSDF"]; b.inputs["Roughness"].default_value = 0.8
    nt.links.new(a.outputs[0], b.inputs["Base Color"])
    return m

me.materials.append(mat_leaf()); me.materials.append(mat_stem())

os.makedirs(out, exist_ok=True)
bpy.ops.object.select_all(action="DESELECT"); ob.select_set(True); bpy.context.view_layer.objects.active = ob
bpy.ops.export_scene.fbx(filepath=os.path.join(out, f"Baumfarn_{seed}.fbx"), use_selection=True,
    apply_scale_options="FBX_SCALE_ALL", axis_forward="-Z", axis_up="Y", bake_space_transform=True,
    mesh_smooth_type="FACE", path_mode="STRIP", use_mesh_modifiers=True)
print("Polys", len(me.polygons), "Verts", len(me.vertices))

if do_render:
    sc = bpy.context.scene
    # Boden
    bpy.ops.mesh.primitive_plane_add(size=16)
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
    loc = (5.2, -2.2, 1.2)   # ca. 6 m Abstand, 1.2 m hoch
    tgt = (0.0, 0.0, H * 0.5)
    co.location = loc; cam.lens = 35
    co.rotation_euler = (Vector(tgt) - Vector(loc)).to_track_quat("-Z", "Y").to_euler()
    sc.render.filepath = os.path.join(out, "Baumfarn_Vorschau.png"); bpy.ops.render.render(write_still=True)
print("fertig")
