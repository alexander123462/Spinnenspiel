# Spinnenbeine (Femur + Tibia + Tarsus) prozedural bauen, FBX exportieren, Vorschau rendern.
# Aufruf: blender -b -P spinne_beine.py -- <outdir> <seed> [render]
import bpy, math, sys, os
import numpy as np
from mathutils import Vector

A = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else sys.argv[1:]
out = os.path.abspath(A[0]); seed = int(A[1]); do_render = len(A) > 2
rng = np.random.default_rng(seed)
bpy.ops.wm.read_factory_settings(use_empty=True)

TAU = 2.0 * math.pi

PROFILE = {
    "Spinne_Femur":  [(0,0.26),(0.06,0.40),(0.14,0.48),(0.5,0.45),(0.84,0.40),(0.9,0.36),(0.95,0.40),(1,0.24)],
    "Spinne_Tibia":  [(0,0.24),(0.05,0.38),(0.12,0.42),(0.19,0.33),(0.25,0.40),(0.6,0.37),(0.9,0.32),(1,0.22)],
    "Spinne_Tarsus": [(0,0.22),(0.05,0.30),(0.5,0.21),(0.58,0.16),(0.64,0.19),(0.85,0.14),(0.92,0.10),(0.97,0.04)] }
SPINES = {"Spinne_Femur":10, "Spinne_Tibia":12, "Spinne_Tarsus":8}


def radius(name, t):
    ts = [p[0] for p in PROFILE[name]]
    rs = [p[1] for p in PROFILE[name]]
    return float(np.interp(t, ts, rs))


def lathe(name):
    """Revolve der Profil um Z. Return (V, F, UV, n_faces_lathe)."""
    NA = 20; NR = 40
    V, F, UV = [], [], []
    r_raw = [radius(name, i / NR) for i in range(NR + 1)]
    r_glatt = r_raw[:]
    for i in range(1, NR):
        r_glatt[i] = (r_raw[i - 1] + r_raw[i] + r_raw[i + 1]) / 3.0
    ring_start = []
    for i in range(NR + 1):
        t = i / NR
        z = -1.0 + 2.0 * t
        r = r_glatt[i]
        ring_start.append(len(V))
        for j in range(NA + 1):
            phi = TAU * j / NA
            V.append((r * math.cos(phi), 0.88 * r * math.sin(phi), z))
            UV.append((j / NA, t))
    # Quads between rings
    for i in range(NR):
        a0 = ring_start[i]; a1 = ring_start[i + 1]
        for j in range(NA):
            F.append((a0 + j, a0 + j + 1, a1 + j + 1, a1 + j))
    # Polpunkte + Triangle-Faechern
    p_bot = len(V); V.append((0.0, 0.0, -1.0)); UV.append((0.5, 0.0))
    for j in range(NA):
        F.append((p_bot, ring_start[0] + j + 1, ring_start[0] + j))
    p_top = len(V); V.append((0.0, 0.0, 1.0)); UV.append((0.5, 1.0))
    top = ring_start[NR]
    for j in range(NA):
        F.append((p_top, top + j, top + j + 1))
    return V, F, UV


def cone(base, d, length, r0, sides, uv):
    """Kegel: Basisring (sides Punkte, Radius r0, normal d), Apex = base + d*length.
    Alle Vertices UV=uv. Return (V, F)."""
    d = Vector(d).normalized()
    up = Vector((0, 0, 1)) if abs(d.z) < 0.9 else Vector((1, 0, 0))
    u = d.cross(up).normalized()
    v = d.cross(u)
    V, F = [], []
    for k in range(sides):
        a = TAU * k / sides
        p = Vector(base) + (u * math.cos(a) + v * math.sin(a)) * r0
        V.append(tuple(p))
    apex = Vector(base) + d * length
    V.append(tuple(apex))
    ai = len(V) - 1
    for k in range(sides):
        F.append((k, (k + 1) % sides, ai))
    # Basis-Faecher (Facher, normal -d)
    for k in range(1, sides - 1):
        F.append((0, k, k + 1))
    return V, F, [uv] * len(V)


def hair_dir(phi, a_deg):
    a = math.radians(a_deg)
    return (math.cos(a) * math.cos(phi), math.cos(a) * math.sin(phi), math.sin(a))


def hair(name, t, phi, length, r0, sides, a_deg):
    r = radius(name, t)
    base = (r * math.cos(phi), 0.88 * r * math.sin(phi), -1.0 + 2.0 * t)
    d = hair_dir(phi, a_deg)
    if base[2] + d[2] * length > 1.0:
        length = (1.0 - base[2]) / d[2]
    uv = (phi / TAU, t)
    return cone(base, d, length, r0, sides, uv)


def claw(s):
    """Rohre um Polyline P0-P1-P2, 6 Seiten, Radius taper, Apex closed. UV (0.5,1.0)."""
    P = [Vector((0.05 * s, 0.0, 0.92)), Vector((0.075 * s, -0.02, 0.96)), Vector((0.09 * s, -0.06, 1.0))]
    d1 = (P[1] - P[0]).normalized()
    d2 = (P[2] - P[1]).normalized()
    dm = (d1 + d2).normalized()
    centers = [P[0], P[1], P[2] - d2 * 0.006]
    radii = [0.03, 0.015, 0.003]
    dirs = [dm, dm, d2]
    SIDES = 6
    V, F, UV = [], [], []
    ring_start = []
    for i in range(3):
        d = dirs[i]
        up = Vector((0, 0, 1)) if abs(d.z) < 0.9 else Vector((1, 0, 0))
        u = d.cross(up).normalized()
        v = d.cross(u)
        ring_start.append(len(V))
        for k in range(SIDES):
            a = TAU * k / SIDES
            p = centers[i] + (u * math.cos(a) + v * math.sin(a)) * radii[i]
            V.append(tuple(p)); UV.append((0.5, 1.0))
    for i in range(2):
        a0 = ring_start[i]; a1 = ring_start[i + 1]
        for k in range(SIDES):
            F.append((a0 + k, a0 + (k + 1) % SIDES, a1 + (k + 1) % SIDES, a1 + k))
    # Spitze closed at P2
    ti = len(V); V.append(tuple(P[2])); UV.append((0.5, 1.0))
    last = ring_start[2]
    for k in range(SIDES):
        F.append((last + k, last + (k + 1) % SIDES, ti))
    return V, F, UV


# ---------------- Build ----------------
mat = bpy.data.materials.new("Leg")
_mnt = mat.node_tree
_mnt.nodes["Principled BSDF"].inputs["Base Color"].default_value = (0.05, 0.05, 0.05, 1)
_mnt.nodes["Principled BSDF"].inputs["Roughness"].default_value = 0.6

sc = bpy.context.scene
objects = {}
for name in ("Spinne_Femur", "Spinne_Tibia", "Spinne_Tarsus"):
    V, F, UV = [], [], []
    LV, LF, LUV = lathe(name)
    n_smooth = len(LF)  # Lathe-Faces smooth
    max_ring_r = max(radius(name, i / 40.0) for i in range(41))
    V += LV; F += LF; UV += LUV

    def add_part(pv, pf, puv):
        o = len(V)
        V.extend(pv)
        F.extend([tuple(i + o for i in face) for face in pf])
        UV.extend(puv)

    for _ in range(SPINES[name]):
        t = rng.uniform(0.15, 0.9); phi = rng.uniform(0, TAU)
        hv, hf, huv = hair(name, t, phi, 0.35, 0.025, 5, 35)
        add_part(hv, hf, huv)
    for _ in range(40):
        t = rng.uniform(0.1, 0.95); phi = rng.uniform(0, TAU)
        hv, hf, huv = hair(name, t, phi, 0.18, 0.010, 3, 35)
        add_part(hv, hf, huv)
    if name == "Spinne_Tarsus":
        for _ in range(30):
            t = rng.uniform(0.82, 0.9); phi = rng.uniform(0, TAU)
            hv, hf, huv = hair(name, t, phi, 0.18, 0.010, 3, 60)
            add_part(hv, hf, huv)
        for s in (+1, -1):
            cv, cf, cuv = claw(s)
            add_part(cv, cf, cuv)

    me = bpy.data.meshes.new(name)
    me.from_pydata(V, [], F)
    me.update()
    uvl = me.uv_layers.new(name="UVMap")
    for loop in me.loops:
        uvl.data[loop.index].uv = UV[loop.vertex_index]
    for pi, poly in enumerate(me.polygons):
        poly.use_smooth = pi < n_smooth
    ob = bpy.data.objects.new(name, me)
    ob.data.materials.append(mat)
    sc.collection.objects.link(ob)
    objects[name] = ob

    # ---------------- Pruefung ----------------
    arr = np.asarray(V)
    vmin = arr.min(axis=0); vmax = arr.max(axis=0)
    tris = sum(len(f) - 2 for f in F)
    print(f"{name}: Vertices {len(V)}, Dreiecke {tris}, "
          f"min xyz {vmin[0]:.4f} {vmin[1]:.4f} {vmin[2]:.4f}, "
          f"max xyz {vmax[0]:.4f} {vmax[1]:.4f} {vmax[2]:.4f}")
    z_ok = vmin[2] >= -1.0 and vmax[2] <= 1.0
    print(f"{vmin[2]:.4f}/{vmax[2]:.4f} -> min z >= -1.0 und max z <= 1.0 -> {'OK' if z_ok else 'NOK'}")
    print(f"{max_ring_r:.4f} -> max Radius der Lathe-Ringe (vor Haaren) <= 0.5 -> {'OK' if max_ring_r <= 0.5 else 'NOK'}")
    mxy = max(abs(arr[:, 0]).max(), abs(arr[:, 1]).max())
    print(f"{mxy:.4f} -> max |x| und max |y| aller Vertices <= 0.85 -> {'OK' if mxy <= 0.85 else 'NOK'}")
    print(f"{tris} -> Dreiecke <= 4000 -> {'OK' if tris <= 4000 else 'NOK'}")

# ---------------- Export ----------------
bpy.ops.object.select_all(action="DESELECT")
for ob in objects.values():
    ob.select_set(True)
fbx_path = os.path.join(out, "Spinne_Beine.fbx")
bpy.ops.export_scene.fbx(filepath=fbx_path, use_selection=True,
                         apply_scale_options="FBX_SCALE_ALL",
                         axis_forward="-Z", axis_up="Y",
                         bake_space_transform=True, mesh_smooth_type="FACE",
                         path_mode="STRIP", use_mesh_modifiers=True)
print(f"FBX -> {fbx_path}")

# ---------------- Render (optional) ----------------
if do_render:
    script_dir = os.path.dirname(os.path.abspath(__file__))
    tex_path = os.path.join(script_dir, "Textures", "Spinne_Leg_Albedo.png")

    leg_tex = bpy.data.materials.new("LegRender")
    nt = leg_tex.node_tree
    bsdf = nt.nodes["Principled BSDF"]
    bsdf.inputs["Roughness"].default_value = 0.6
    tex_node = nt.nodes.new("ShaderNodeTexImage")
    tex_node.image = bpy.data.images.load(tex_path)
    nt.links.new(tex_node.outputs["Color"], bsdf.inputs["Base Color"])

    L = {"Spinne_Femur": 0.085, "Spinne_Tibia": 0.095, "Spinne_Tarsus": 0.028}
    X = {"Spinne_Femur": -0.04, "Spinne_Tibia": 0.0, "Spinne_Tarsus": 0.04}
    for name, ob in objects.items():
        ob.hide_render = True
        cp = ob.copy()
        cp.hide_render = False
        cp.name = name + "_render"
        cp.data = ob.data.copy()
        cp.data.materials.clear()
        cp.data.materials.append(leg_tex)
        sc.collection.objects.link(cp)
        cp.scale = (0.015, 0.015, L[name] / 2.0)
        cp.location = (X[name], 0.0, L[name] / 2.0)

    w = bpy.data.worlds.new("W")
    sc.world = w
    wn = w.node_tree
    wn.nodes["Background"].inputs[0].default_value = (0.6, 0.6, 0.6, 1)

    bpy.ops.object.light_add(type="SUN")
    sun = bpy.context.object
    sun.data.energy = 3.0
    sun.rotation_euler = (math.radians(50), 0.0, math.radians(30))

    cam = bpy.data.cameras.new("Cam")
    cam.type = "ORTHO"
    cam.ortho_scale = 0.14
    co = bpy.data.objects.new("Cam", cam)
    sc.collection.objects.link(co)
    sc.camera = co
    co.location = (0.0, -0.5, 0.05)
    co.rotation_euler = (math.radians(90), 0.0, 0.0)

    sc.render.engine = "CYCLES"
    sc.cycles.device = "CPU"
    sc.cycles.samples = 32
    sc.render.resolution_x = 960
    sc.render.resolution_y = 540
    png_path = os.path.join(out, "Spinne_Beine_Vorschau.png")
    sc.render.filepath = png_path
    bpy.ops.render.render(write_still=True)
    print(f"Vorschau: {png_path}")
print("fertig")
