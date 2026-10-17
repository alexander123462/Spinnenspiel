# Urwald-Farn (Dryopteris-artig, ground-fern) prozedural bauen: 5 FBX-Varianten + Vorschau-PNG.
# Aufruf: blender -b -P fern.py -- [outdir] [seed]   (outdir default = Scriptdir, seed default 17)
# Reuses die proven Techniques from ArtSource/Baumfarn/baumfarn.py (Kreisbogen-Fronds, tube-Rachis,
# tapered Fiederblaettchen-Strips). Masses: 1 unit = 1 m, Plant-Hoehe 0.25-0.45 m.
import bpy, math, sys, os
import numpy as np
from mathutils import Vector

A = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else sys.argv[1:]
out = os.path.abspath(A[0]) if A else os.path.dirname(os.path.abspath(__file__))
base_seed = int(A[1]) if len(A) > 1 else 17
N_VAR = 5

# ---------------- Geometrie-Helfer (from baumfarn.py) ----------------
def _frame(p, tan):
    side = np.cross(tan, [0, 0, 1.0])
    if np.linalg.norm(side) < 1e-4: side = np.array([1.0, 0, 0])
    side /= np.linalg.norm(side)
    up = np.cross(side, tan)
    return side, up

def _tube_pts(pts, r0, r1, seg=4):
    pts = [np.array(p, dtype=float) for p in pts]
    verts, uvs, faces = [], [], []
    n = len(pts)
    for i, p in enumerate(pts):
        tan = pts[min(i + 1, n - 1)] - pts[max(i - 1, 0)]
        tan /= (np.linalg.norm(tan) + 1e-9)
        side, upl = _frame(p, tan)
        r = r0 + (r1 - r0) * i / (n - 1)
        for j in range(seg + 1):
            a = j / seg * 6.283
            verts.append(tuple(p + r * (math.cos(a) * side + math.sin(a) * upl)))
            uvs.append((j / seg, i / (n - 1)))
    for i in range(n - 1):
        for j in range(seg):
            a = i * (seg + 1) + j
            faces.append((a, a + seg + 1, a + seg + 2, a + 1))
    return verts, faces, uvs

def add(Vv, Ff, Uu, v, f, u):
    o = len(Vv); Vv.extend(v); Uu.extend(u)
    for fc in f: Ff.append(tuple(x + o for x in fc))

def make_mesh(name, V, F, UV, mats):
    me = bpy.data.meshes.new(name)
    me.from_pydata(V, [], F); me.update()
    uvl = me.uv_layers.new(name="UVMap")
    for poly in me.polygons:
        poly.use_smooth = True
        for li in poly.loop_indices:
            uvl.data[li].uv = UV[me.loops[li].vertex_index]
    for m in mats: me.materials.append(m)
    ob = bpy.data.objects.new(name, me)
    bpy.context.scene.collection.objects.link(ob)
    return ob

# ---------------- Materials (procedural, no textures here) ----------------
def mat_leaf(tint):
    m = bpy.data.materials.new(f"M_Fern_Leaf_{tint[3]}"); nt = m.node_tree
    b = nt.nodes["Principled BSDF"]
    b.inputs["Base Color"].default_value = (tint[0], tint[1], tint[2], 1.0)
    b.inputs["Roughness"].default_value = 0.55
    try: b.inputs["Subsurface Weight"].default_value = 0.15
    except Exception: pass
    return m

def mat_stem():
    m = bpy.data.materials.new("M_Fern_Stem"); nt = m.node_tree
    b = nt.nodes["Principled BSDF"]
    b.inputs["Base Color"].default_value = (0.13, 0.11, 0.05, 1.0)
    b.inputs["Roughness"].default_value = 0.8
    return m

# ---------------- Build one fern variant ----------------
def build_fern(v, seed):
    rng = np.random.default_rng(seed)
    Vs, Fs, Us = [], [], []   # Rachis/Stem
    Vl, Fl, Ul = [], [], []   # Fiederblaettchen/Leaves
    H = float(rng.uniform(0.25, 0.45))                 # Plant-Hoehe (m)
    n = int(rng.integers(8, 15))                       # Fronds 8-14
    curl = float(rng.uniform(-1, 1))                   # global Curl-Character per Variant
    print(f"Variant {v}: seed={seed} H={H:.3f}m fronds={n} curl={curl:+.2f}")

    upv = np.array([0.0, 0.0, 1.0])
    for k in range(n):
        th0 = math.radians(float(rng.uniform(62, 85)))          # Start-Tangente: steep up
        th1 = math.radians(float(rng.uniform(-35 - 18 * curl, 15 - 12 * curl)))  # End (outward/droop)
        dtheta = th1 - th0
        R = 1.0
        cx = R * math.sin(th0); cz = -R * math.cos(th0)
        start_ang = math.atan2(-cz, -cx)
        n_arc = 12
        arc2d = []
        for i in range(n_arc + 1):
            ang = start_ang + dtheta * (i / n_arc)
            arc2d.append([cx + R * math.cos(ang), cz + R * math.sin(ang)])
        p0 = arc2d[0]
        arc2d = [[p[0] - p0[0], p[1] - p0[1]] for p in arc2d]   # Ansatz at (0,0)
        zmax = max(p[1] for p in arc2d)
        H_f = H * float(rng.uniform(0.72, 1.04))                # Frond-Hoehe-Variation
        s = H_f / max(zmax, 1e-4)
        arc2d = [[p[0] * s, p[1] * s] for p in arc2d]           # Normalized to target height

        yaw = k / n * 6.283 + float(rng.uniform(-math.radians(12), math.radians(12)))
        d = np.array([math.cos(yaw), math.sin(yaw), 0.0])
        base = d * float(rng.uniform(0.0, 0.035))               # Crown-Radius at ground
        arc3d = [base + d * p[0] + upv * p[1] for p in arc2d]

        rv, rf, ru = _tube_pts(arc3d, 0.0042, 0.0016, seg=4)
        add(Vs, Fs, Us, rv, rf, ru)

        nrm_f = np.cross(d, upv); nrm_f /= (np.linalg.norm(nrm_f) + 1e-9)
        Np = int(rng.integers(9, 16))
        l_mid = H_f * float(rng.uniform(0.16, 0.26))
        for side in (-1, 1):
            for k2 in range(Np):
                u = (k2 + (0.10 if side > 0 else 0.55)) / Np
                idx_f = u * n_arc
                idx = min(int(idx_f), n_arc - 1)
                frac = idx_f - idx
                P2 = np.array(arc2d[idx]) * (1 - frac) + np.array(arc2d[idx + 1]) * frac
                q = nrm_f * side
                droop_a = math.radians(8 + 14 * u)
                q = q * math.cos(droop_a) - upv * math.sin(droop_a)
                q /= (np.linalg.norm(q) + 1e-9)
                nrm2 = np.cross(q, upv); nrm2 /= (np.linalg.norm(nrm2) + 1e-9)
                l = l_mid * (math.sin(math.pi * (0.05 + 0.90 * u)) ** 0.7)
                w = 0.34 * l
                P0p = base + d * P2[0] + upv * P2[1]
                m_seg = 3
                verts, uvs2, faces = [], [], []
                for i in range(m_seg + 1):
                    f = i / m_seg
                    half = max(w * 0.5 * math.cos(f * math.pi / 2) ** 0.7, 1e-4)
                    concave = 0.02 * f * f
                    c = P0p + q * (l * f) - upv * concave
                    verts += [tuple(c - nrm2 * half), tuple(c + nrm2 * half)]
                    uvs2 += [(0.0, f), (1.0, f)]
                for i in range(m_seg):
                    a = 2 * i
                    faces.append((a, a + 2, a + 3, a + 1))
                add(Vl, Fl, Ul, verts, faces, uvs2)

    tint = (float(rng.uniform(0.05, 0.12)), float(rng.uniform(0.20, 0.34)), float(rng.uniform(0.04, 0.09)), v)
    ob_stem = make_mesh(f"fern_{v}_stem", Vs, Fs, Us, [mat_stem()])
    ob_leaf = make_mesh(f"fern_{v}_leaves", Vl, Fl, Ul, [mat_leaf(tint)])
    print(f"  fern_{v}: stem {len(Fs)} polys / leaves {len(Fl)} polys")
    return [ob_stem, ob_leaf]

# ---------------- Generate + export the 5 Variants ----------------
all_obs = []
for v in range(1, N_VAR + 1):
    obs = build_fern(v, base_seed * 100 + v)
    bpy.ops.object.select_all(action="DESELECT")
    for o in obs: o.select_set(True)
    bpy.context.view_layer.objects.active = obs[0]
    fp = os.path.join(out, f"fern_{v}.fbx")
    bpy.ops.export_scene.fbx(filepath=fp, use_selection=True,
        apply_scale_options="FBX_SCALE_ALL", axis_forward="-Z", axis_up="Y", bake_space_transform=True,
        mesh_smooth_type="FACE", path_mode="STRIP", use_mesh_modifiers=True)
    print(f"Export: fern_{v}.fbx")
    all_obs.append(obs)

# ---------------- Vorschau-Rendering: alle 5 Variants in a Row ----------------
for i, obs in enumerate(all_obs):
    dx = (i - (N_VAR - 1) / 2) * 0.75
    for o in obs: o.location.x += dx

bpy.ops.mesh.primitive_plane_add(size=20)
g = bpy.context.object; gm = bpy.data.materials.new("Ground"); nt = gm.node_tree
nz = nt.nodes.new("ShaderNodeTexNoise"); nz.inputs["Scale"].default_value = 9; nz.inputs["Detail"].default_value = 8
cr = nt.nodes.new("ShaderNodeValToRGB")
cr.color_ramp.elements[0].color = (0.03, 0.02, 0.012, 1)
cr.color_ramp.elements[1].color = (0.10, 0.07, 0.035, 1)
nt.links.new(nz.outputs[0], cr.inputs[0])
nt.links.new(cr.outputs[0], nt.nodes["Principled BSDF"].inputs["Base Color"])
nt.nodes["Principled BSDF"].inputs["Roughness"].default_value = 0.85
g.data.materials.append(gm)

w = bpy.data.worlds.new("W"); sc = bpy.context.scene; sc.world = w; wn = w.node_tree
sky = wn.nodes.new("ShaderNodeTexSky"); sky.sky_type = "MULTIPLE_SCATTERING"
sky.sun_elevation = math.radians(35); sky.sun_rotation = math.radians(140)
bg = next((nd for nd in wn.nodes if nd.type in ("BACKGROUND", "BSDF_BACKGROUND")), None) or wn.nodes.new("ShaderNodeBackground")
outn = next(nd for nd in wn.nodes if nd.type in ("OUTPUT_WORLD", "OUTPUT"))
wn.links.new(sky.outputs[0], bg.inputs[0])
wn.links.new(bg.outputs[0], outn.inputs[0])
bg.inputs[1].default_value = 0.12
bpy.ops.object.light_add(type="SUN")
sun = bpy.context.object; sun.data.energy = 1.4; sun.data.angle = math.radians(2)
sun.rotation_euler = (math.radians(55), 0, math.radians(140 + 90))

sc.render.engine = "CYCLES"; sc.cycles.device = "CPU"
sc.cycles.samples = 48; sc.cycles.use_denoising = True
sc.render.resolution_x, sc.render.resolution_y = 960, 540
sc.view_settings.look = "AgX - Medium High Contrast"

cam = bpy.data.cameras.new("Cam"); co = bpy.data.objects.new("Cam", cam)
sc.collection.objects.link(co); sc.camera = co
cam.lens = 30
# Frame the row of ferns from their combined bounding box
import itertools
mn = np.array([1e9, 1e9, 1e9]); mx = -mn
for obs in all_obs:
    for o in obs:
        for c in o.bound_box:
            w = o.matrix_world @ Vector(c)
            mn = np.minimum(mn, np.array(w)); mx = np.maximum(mx, np.array(w))
ctr = (mn + mx) / 2
span = float(max(mx[0] - mn[0], mx[1] - mn[1]))
dist = max(1.2, span * 1.15)
az = math.radians(-20)
loc = ctr + np.array([dist * math.cos(az), dist * math.sin(az), span * 0.55 + 0.25])
co.location = tuple(loc)
co.rotation_euler = (Vector(ctr) - Vector(loc)).to_track_quat("-Z", "Y").to_euler()
print(f"Cam: ctr={ctr.round(2)} span={span:.2f} dist={dist:.2f} bbox_z=({mn[2]:.2f},{mx[2]:.2f})")
sc.render.filepath = os.path.join(out, "fern_Vorschau.png")
bpy.ops.render.render(write_still=True)
print("Vorschau: fern_Vorschau.png")

print("fertig")
