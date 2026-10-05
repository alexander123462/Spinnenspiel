# Baumfarn (Cyathea-artig) prozedural bauen, FBX exportieren, Vorschau rendern.
# Aufruf: blender -b -P baumfarn.py -- <outdir> <seed> [render]
import bpy, math, sys, os
import numpy as np
from mathutils import Vector

A = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else sys.argv[1:]
out = os.path.abspath(A[0]); seed = int(A[1]); do_render = len(A) > 2
tex = os.path.join(out, "Textures")
rng = np.random.default_rng(seed)
bpy.ops.wm.read_factory_settings(use_empty=True)

# ---------------- Geometrie-Helfer ----------------
V, F, UV, MAT = [], [], [], []
def add(v, f, uv, m):
    o = len(V); V.extend(v); UV.extend(uv)
    for fc in f: F.append(tuple(x + o for x in fc)); MAT.append(m)

def _frame(p, tan):
    side = np.cross(tan, [0, 0, 1.0])
    if np.linalg.norm(side) < 1e-4: side = np.array([1.0, 0, 0])
    side /= np.linalg.norm(side)
    up = np.cross(side, tan)
    return side, up

def tube_bezier(P0, C, P1, r0, r1, rings=16, seg=7, radii=None):
    P0, C, P1 = map(np.array, (P0, C, P1))
    pts = [(1-k)**2*P0 + 2*(1-k)*k*C + k**2*P1 for k in np.linspace(0, 1, rings)]
    return _tube_pts(pts, r0, r1, seg, radii)

def tube_points(pts, r0, r1, seg=7, radii=None):
    pts = [np.array(p, dtype=float) for p in pts]
    return _tube_pts(pts, r0, r1, seg, radii)

def _tube_pts(pts, r0, r1, seg, radii=None):
    verts, uvs, faces = [], [], []
    n = len(pts)
    if radii is not None:
        radii = np.asarray(radii, dtype=float)
    for i, p in enumerate(pts):
        tan = pts[min(i+1, n-1)] - pts[max(i-1, 0)]
        tan /= (np.linalg.norm(tan) + 1e-9)
        side, up = _frame(p, tan)
        r = radii[i] if radii is not None else (r0 + (r1 - r0) * i / (n - 1))
        for j in range(seg + 1):
            a = j / seg * 6.283
            verts.append(tuple(p + r * (math.cos(a)*side + math.sin(a)*up)))
            uvs.append((j/seg, i/(n-1)))
    for i in range(n - 1):
        for j in range(seg):
            a = i*(seg+1) + j
            faces.append((a, a+seg+1, a+seg+2, a+1))
    return verts, faces, uvs

# ---------------- Stamm ----------------
H = float(rng.uniform(2.5, 4.5))
r_bot = 0.24
r_top = 0.11
bend = np.array([float(rng.uniform(-0.3, 0.3)), float(rng.uniform(-0.3, 0.3)), 0.0])
P0t = np.array([0.0, 0.0, 0.0])
P1t = np.array([0.0, 0.0, H]) + bend
Ct = (P0t + P1t) / 2 + bend * 0.5
rings = 14
z = np.linspace(0.0, H, rings)                          # Hoehe entlang der Ringe (Bogen klein)
r_mid = r_bot * 0.90
r = np.where(z < 0.4,
             r_bot * (1.0 - 0.10 * (z / 0.4)),          # Wurzelballen: untere 0.4 m dick halten
             r_mid + (r_top - r_mid) * ((z - 0.4) / max(H - 0.4, 1e-6)))   # dann Taper zur Spitze
r = r * (1.0 + rng.uniform(-0.08, 0.08, size=rings))    # Radius-Rauschen ±8 %
r_max = float(r.max())
sv, sf, su = tube_bezier(P0t, Ct, P1t, r_bot, r_top, rings=rings, seg=7, radii=r)
add(sv, sf, su, 1)
print(f"Stamm: H={H:.2f}m Wurzelballen={r_max:.2f} (untere 0.4 m, ±8%) r_top={r_top} bend={bend.round(2)}")

# ---------------- Wedel (Krone) ----------------
top = P1t
n = int(rng.integers(12, 17))
upv = np.array([0.0, 0.0, 1.0])
print(f"Wedel: {n} Stueck")

# Typen: ~1/4 junge steile Wedel (im Zentrum nach oben), Rest hängende alte Wedel.
# Pro Wedel zusätzlich ±10° Azimut (seitliche Verdrehung) und ±15% Längenstreuung.
n_young = max(1, int(round(n * 0.25)))
young_mask = rng.permutation(n) < n_young
droop_tips = []

for k in range(n):
    if young_mask[k]:
        # Jung/steil: Startwinkel 45-60 Grad, Spitze bleibt nahe/über dem Ansatz
        th0 = math.radians(float(rng.uniform(45, 60)))
        th1 = math.radians(-float(rng.uniform(20, 45)))
        L = 2.0 * float(rng.uniform(0.85, 1.15))      # Längenstreuung ±15%
    else:
        # Alt/hängend: starkes Durchhängen, Spitze im Mittel >= 0.6 m unter dem Ansatz
        th0 = math.radians(float(rng.uniform(20, 35)))
        th1 = math.radians(-float(rng.uniform(70, 95)))
        L = 2.4 * float(rng.uniform(0.85, 1.15))      # Längenstreuung ±15%
    yaw = k / n * 6.283 + float(rng.uniform(-math.radians(10), math.radians(10)))  # ±10° Verdrehung
    d = np.array([math.cos(yaw), math.sin(yaw), 0.0])

    # Kreisbogen: Starttangente th0, Endtangente th1, Bogenlaenge L
    dtheta = th1 - th0
    R = L / abs(dtheta)
    cx = R * math.sin(th0)
    cz = -R * math.cos(th0)
    start_ang = math.atan2(0 - cz, 0 - cx)

    n_arc = 14
    arc2d = []
    for i in range(n_arc + 1):
        t = i / n_arc
        ang = start_ang + dtheta * t
        arc2d.append((cx + R * math.cos(ang), cz + R * math.sin(ang)))

    arc3d = [top + d * p[0] + upv * p[1] for p in arc2d]
    tip = arc3d[-1]
    if not young_mask[k]:
        droop_tips.append(top[2] - tip[2])      # positiv = Spitze unter dem Ansatz
    print(f"  Wedel {k+1}: L={L:.2f}m anfang={math.degrees(th0):.0f}° ende={math.degrees(th1):.0f}° Spitze_Z={tip[2]:.2f}m Ansatz_Z={top[2]:.2f}m")

    # Mittelrippe
    rv, rf, ru = tube_points(arc3d, 0.022, 0.008, seg=4)
    add(rv, rf, ru, 0)

    # Fiederblaettchen
    nrm_f = np.cross(d, upv); nrm_f /= (np.linalg.norm(nrm_f) + 1e-9)
    Np = int(rng.integers(30, 39))
    l_mid = float(rng.uniform(0.50, 0.65))
    print(f"  Fiederblaettchen: {Np} pro Seite ({2 * Np} am Wedel)")

    for side in (-1, 1):
        for k2 in range(Np):
            u = (k2 + (0.08 if side > 0 else 0.55)) / Np
            idx_f = u * n_arc
            idx = min(int(idx_f), n_arc - 1)
            frac = idx_f - idx
            P2 = np.array(arc2d[idx]) * (1 - frac) + np.array(arc2d[idx + 1]) * frac
            T2 = np.array(arc2d[min(idx + 1, n_arc)]) - np.array(arc2d[max(idx - 1, 0)])
            T2 /= (np.linalg.norm(T2) + 1e-9)
            T3 = d * T2[0] + upv * T2[1]

            # Fiederblaettchen: waagerecht (Flaeche zeigt nach unten, nicht hochkant),
            # Laenge radial vom Wedel, dichte Ueberlappung entlang der Mittelrippe
            q = nrm_f * side
            droop = math.radians(10 + 12 * u)
            q = q * math.cos(droop) - upv * math.sin(droop)
            q /= (np.linalg.norm(q) + 1e-9)

            nrm2 = np.cross(q, upv); nrm2 /= (np.linalg.norm(nrm2) + 1e-9)
            l = l_mid * (math.sin(math.pi * (0.05 + 0.90 * u)) ** 0.7)
            w = 0.34 * l
            P0p = top + d * P2[0] + upv * P2[1]

            m = 3
            verts, uvs2, faces = [], [], []
            for i in range(m + 1):
                f = i / m
                half = max(w * 0.5 * math.cos(f * math.pi / 2) ** 0.7, 1e-4)
                concave = 0.03 * f * f
                c = P0p + q * (l * f) - upv * concave
                verts += [tuple(c - nrm2 * half), tuple(c + nrm2 * half)]
                uvs2 += [(0.0, f), (1.0, f)]
            for i in range(m):
                a = 2 * i
                faces.append((a, a+2, a+3, a+1))
            add(verts, faces, uvs2, 0)

print(f"Baumfarn: steile_Wedel={int(sum(young_mask))} haengend={len(droop_tips)} "
      f"mittlere_Spitze_unter_Ansatz_haengend={np.mean(droop_tips):.2f}m")

# ---------------- Trokene Wedel (vertrocknet, steil nach unten am Stamm entlang) ----------------
# Sitzen am unteren Kronenansatz (nahe TOP), hängen steil nach unten.
# Spitze 1.0-1.6 m unter dem Ansatz, etwas kürzer und schmaler als normale Wedel.
# Eigenes Material "Dry" (braun 0.30/0.20/0.10) als 3. Blender-Slot;
# beim FBX-Export dem Stamm zugeordnet (damit Unity nur 2 Submeshes sieht).
n_dry = int(rng.integers(2, 4))   # 2 bis 3
dry_droop = []
print(f"Trockene Wedel: {n_dry} Stueck")
for kd in range(n_dry):
    yaw_d = float(rng.uniform(0, 2 * math.pi))
    d_d = np.array([math.cos(yaw_d), math.sin(yaw_d), 0.0])
    drop_d = float(rng.uniform(1.0, 1.6))   # Spitze 1.0-1.6 m unter dem Ansatz
    n_arc_d = 12
    # Steil nach unten, am Stamm entlang (kleine Außen-Auslenkung, leicht gewölbt).
    arc2d_d = []
    for i in range(n_arc_d + 1):
        t = i / n_arc_d
        z = -drop_d * t                              # z: von 0 (Ansatz) bis -drop_d (Spitze)
        out_d = 0.10 * math.sin(math.pi * t) + 0.04  # max ~0.14 m in der Mitte
        arc2d_d.append((out_d, z))
    arc3d_d = [top + d_d * p[0] + upv * p[1] for p in arc2d_d]
    tip_d = arc3d_d[-1]
    droop_d = top[2] - tip_d[2]
    dry_droop.append(droop_d)
    print(f"  Trocken {kd+1}: Spitze_Z={tip_d[2]:.2f}m Ansatz_Z={top[2]:.2f}m Droop={droop_d:.2f}m")

    # Mittelrippe (schmaler als normale Wedel)
    rv_d, rf_d, ru_d = tube_points(arc3d_d, 0.018, 0.006, seg=4)
    add(rv_d, rf_d, ru_d, 1)                          # 1 = Stamm (damit Unity nur 2 Submeshes sieht)

    # Fiederblättchen (schmaler: l_mid ~0.35 statt 0.55)
    nrm_f_d = np.cross(d_d, upv); nrm_f_d /= (np.linalg.norm(nrm_f_d) + 1e-9)
    Np_d = int(rng.integers(22, 28))
    l_mid_d = float(rng.uniform(0.30, 0.40))
    for side in (-1, 1):
        for k2 in range(Np_d):
            u = (k2 + (0.08 if side > 0 else 0.55)) / Np_d
            idx_f = u * n_arc_d
            idx = min(int(idx_f), n_arc_d - 1)
            frac = idx_f - idx
            P2_d = np.array(arc2d_d[idx]) * (1 - frac) + np.array(arc2d_d[idx + 1]) * frac
            q_d = nrm_f_d * side
            droop_a = math.radians(10 + 12 * u)
            q_d = q_d * math.cos(droop_a) - upv * math.sin(droop_a)
            q_d /= (np.linalg.norm(q_d) + 1e-9)
            nrm2_d = np.cross(q_d, upv); nrm2_d /= (np.linalg.norm(nrm2_d) + 1e-9)
            l_d = l_mid_d * (math.sin(math.pi * (0.05 + 0.90 * u)) ** 0.7)
            w_d = 0.34 * l_d
            P0p_d = top + d_d * P2_d[0] + upv * P2_d[1]
            m_d = 3
            verts_d, uvs2_d, faces_d = [], [], []
            for i in range(m_d + 1):
                f = i / m_d
                half_d = max(w_d * 0.5 * math.cos(f * math.pi / 2) ** 0.7, 1e-4)
                concave_d = 0.03 * f * f
                c_d = P0p_d + q_d * (l_d * f) - upv * concave_d
                verts_d += [tuple(c_d - nrm2_d * half_d), tuple(c_d + nrm2_d * half_d)]
                uvs2_d += [(0.0, f), (1.0, f)]
            for i in range(m_d):
                a_d = 2 * i
                faces_d.append((a_d, a_d+2, a_d+3, a_d+1))
            add(verts_d, faces_d, uvs2_d, 1)        # 1 = Stamm (damit Unity nur 2 Submeshes sieht)

print(f"Trockene: {n_dry} Stueck, mittlere_Spitze_unter_Ansatz={np.mean(dry_droop):.2f}m")

# ---------------- Mesh + Material ----------------
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
    nmap = N.new("ShaderNodeNormalMap")
    inv = N.new("ShaderNodeMath"); inv.operation = "SUBTRACT"; inv.inputs[0].default_value = 1
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
    b = nt.nodes["Principled BSDF"]; b.inputs["Roughness"].default_value = 0.85
    nt.links.new(a.outputs[0], b.inputs["Base Color"])
    return m

def mat_dry():
    m = bpy.data.materials.new("M_Baumfarn_Dry"); nt = m.node_tree
    b = nt.nodes["Principled BSDF"]
    b.inputs["Base Color"].default_value = (0.30, 0.20, 0.10, 1.0)
    b.inputs["Roughness"].default_value = 0.9
    return m

me.materials.append(mat_leaf()); me.materials.append(mat_stem()); me.materials.append(mat_dry())

# ---------------- FBX-Export ----------------
os.makedirs(out, exist_ok=True)
bpy.ops.object.select_all(action="DESELECT"); ob.select_set(True); bpy.context.view_layer.objects.active = ob
bpy.ops.export_scene.fbx(filepath=os.path.join(out, f"Baumfarn_{seed}.fbx"), use_selection=True,
    apply_scale_options="FBX_SCALE_ALL", axis_forward="-Z", axis_up="Y", bake_space_transform=True,
    mesh_smooth_type="FACE", path_mode="STRIP", use_mesh_modifiers=True)
print(f"Polys={len(me.polygons)} Verts={len(me.vertices)}")

# ---------------- Vorschau-Rendering ----------------
if do_render:
    sc = bpy.context.scene
    # Boden
    bpy.ops.mesh.primitive_plane_add(size=20)
    g = bpy.context.object; gm = bpy.data.materials.new("Ground"); nt = gm.node_tree
    nz = nt.nodes.new("ShaderNodeTexNoise"); nz.inputs["Scale"].default_value = 9; nz.inputs["Detail"].default_value = 8
    cr = nt.nodes.new("ShaderNodeValToRGB")
    cr.color_ramp.elements[0].color = (0.035, 0.022, 0.012, 1)
    cr.color_ramp.elements[1].color = (0.11, 0.075, 0.035, 1)
    nt.links.new(nz.outputs[0], cr.inputs[0])
    nt.links.new(cr.outputs[0], nt.nodes["Principled BSDF"].inputs["Base Color"])
    nt.nodes["Principled BSDF"].inputs["Roughness"].default_value = 0.85
    g.data.materials.append(gm)
    # Licht
    w = bpy.data.worlds.new("W"); sc.world = w; wn = w.node_tree
    sky = wn.nodes.new("ShaderNodeTexSky"); sky.sky_type = "MULTIPLE_SCATTERING"
    sky.sun_elevation = math.radians(35); sky.sun_rotation = math.radians(140)
    wn.links.new(sky.outputs[0], wn.nodes["Background"].inputs[0])
    wn.nodes["Background"].inputs[1].default_value = 0.35
    bpy.ops.object.light_add(type="SUN")
    sun = bpy.context.object; sun.data.energy = 3.0; sun.data.angle = math.radians(2)
    sun.rotation_euler = (math.radians(55), 0, math.radians(140 + 90))
    sc.render.engine = "CYCLES"; sc.cycles.device = "CPU"
    sc.cycles.samples = 48; sc.cycles.use_denoising = True
    sc.render.resolution_x, sc.render.resolution_y = 960, 540
    sc.view_settings.look = "AgX - Medium High Contrast"

    # --- Aufnahme 1: Gesamtansicht (9m Abstand, 1.5m hoch, Ziel 2.6m, 35mm) ---
    cam1 = bpy.data.cameras.new("Cam1"); co1 = bpy.data.objects.new("Cam1", cam1)
    sc.collection.objects.link(co1); sc.camera = co1
    cam1.lens = 35
    az1 = math.radians(-30)
    loc1 = np.array([9.0 * math.cos(az1), 9.0 * math.sin(az1), 1.5])
    tgt1 = np.array([0.0, 0.0, 2.6])
    co1.location = tuple(loc1)
    co1.rotation_euler = (Vector(tgt1) - Vector(loc1)).to_track_quat("-Z", "Y").to_euler()
    sc.render.filepath = os.path.join(out, "Baumfarn_Vorschau.png")
    bpy.ops.render.render(write_still=True)
    print("Vorschau gesamt: Baumfarn_Vorschau.png")

    # --- Aufnahme 2: Spinnensicht (1.2m vom Stamm, 0.1m hoch, Blick nach oben, 18mm) ---
    cam2 = bpy.data.cameras.new("Cam2"); co2 = bpy.data.objects.new("Cam2", cam2)
    sc.collection.objects.link(co2); sc.camera = co2
    cam2.lens = 18
    az2 = math.radians(45)
    loc2 = np.array([1.2 * math.cos(az2), 1.2 * math.sin(az2), 0.1])
    tgt2 = np.array([0.0, 0.0, H * 0.85])
    co2.location = tuple(loc2)
    co2.rotation_euler = (Vector(tgt2) - Vector(loc2)).to_track_quat("-Z", "Y").to_euler()
    sc.render.filepath = os.path.join(out, "Baumfarn_Vorschau_unten.png")
    bpy.ops.render.render(write_still=True)
    print("Vorschau unten: Baumfarn_Vorschau_unten.png")

print("fertig")
