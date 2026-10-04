# Spinnenkoerper (Abdomen + Cephalothorax) prozedural bauen, FBX exportieren, Vorschau rendern.
# Aufruf: blender -b -P spinne_koerper.py -- <outdir> <seed> [render]
import bpy, math, sys, os
import numpy as np
from mathutils import Vector

A = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else sys.argv[1:]
out = os.path.abspath(A[0]); seed = int(A[1]); do_render = len(A) > 2
rng = np.random.default_rng(seed)
bpy.ops.wm.read_factory_settings(use_empty=True)

# ---------------- Mesh-Helfer (deformierte Kugel, Pole in ±Y) ----------------
def deformed_sphere(n_theta, n_phi, deform):
    """UV-Kugel mit Polen in +Y (hinten) / -Y (vorne); deform(x0,y0,z0,theta,phi)->(x,y,z).
    Gitter (n_theta+1)x(n_phi+1) mit duplizierter Nahtspalte (index-stabil)."""
    V, F, UV = [], [], []
    for i in range(n_theta + 1):
        theta = i / n_theta * math.pi
        s, c = math.sin(theta), math.cos(theta)
        for j in range(n_phi + 1):
            phi = j / n_phi * 2.0 * math.pi
            x0, y0, z0 = s * math.cos(phi), c, s * math.sin(phi)
            x, y, z = deform(x0, y0, z0, theta, phi)
            V.append((float(x), float(y), float(z)))
            UV.append((j / n_phi, i / n_theta))
    for i in range(n_theta):
        for j in range(n_phi):
            a = i * (n_phi + 1) + j
            b = a + 1
            cc = (i + 1) * (n_phi + 1) + j
            d = (i + 1) * (n_phi + 1) + (j + 1)
            F.append((a, cc, d, b))
    return V, F, UV

def make_obj(name, V, F, UV):
    me = bpy.data.meshes.new(name)
    me.from_pydata(V, [], F); me.update()
    uvl = me.uv_layers.new(name="UVMap")
    for poly in me.polygons:
        poly.use_smooth = True
        for li in poly.loop_indices:
            uvl.data[li].uv = UV[me.loops[li].vertex_index]
    ob = bpy.data.objects.new(name, me)
    bpy.context.scene.collection.objects.link(ob)
    return ob

def tube_stump(base, direction, r0, r1, length, seg=8, rings=5):
    """Kurze, abgeflachte Stumpfwarze (z.B. Spinnwarze). Liefert (V,F,UV) im lokalen Raum.
    base: Startpunkt, direction: Richtung (wird normalisiert), r0/r1: Radien."""
    direction = np.array(direction, dtype=float)
    direction /= (np.linalg.norm(direction) + 1e-9)
    side = np.cross(direction, [0, 1, 0])
    if np.linalg.norm(side) < 1e-4: side = np.array([1.0, 0, 0])
    side /= np.linalg.norm(side)
    up = np.cross(side, direction)
    V, F, UV = [], [], []
    for i in range(rings + 1):
        t = i / rings
        c = base + direction * (length * t)
        r = r0 + (r1 - r0) * t
        for j in range(seg + 1):
            a = j / seg * 2.0 * math.pi
            V.append(tuple(c + r * (math.cos(a) * side + math.sin(a) * up)))
            UV.append((j / seg, t))
    for i in range(rings):
        for j in range(seg):
            a = i * (seg + 1) + j
            b = a + 1
            cc = (i + 1) * (seg + 1) + j
            d = (i + 1) * (seg + 1) + (j + 1)
            F.append((a, cc, d, b))
    return V, F, UV

def bounds(ob):
    pts = [ob.matrix_world @ v.co for v in ob.data.vertices]
    lo = np.min(pts, axis=0); hi = np.max(pts, axis=0)
    return (hi - lo)

def normalize(ob):
    """Skaliert die Vertices, so dass die Bounds auf JEDE Achse exakt 1.0 werden."""
    pts = np.array([v.co for v in ob.data.vertices])
    b = (pts.max(axis=0) - pts.min(axis=0))     # Vollspanne je Achse
    pts = pts / b                                # Spann -> 1.0
    for v, p in zip(ob.data.vertices, pts):
        v.co = (float(p[0]), float(p[1]), float(p[2]))
    ob.data.update()

# ---------------- Abdomen (Vogelspinnen-Hinterleib, eifoermig) ----------------
def deform_abdomen(x0, y0, z0, theta, phi):
    s, c = math.sin(theta), math.cos(theta)
    # hinten (c=+1) spitzer als vorne (c=-1): Exponent rueckwaerts groesser
    p = 0.80 + 0.55 * max(c, 0.0)
    f = s ** p                      # Querschnitts-Radius (0 an beiden Polen)
    L = 1.12 + 0.18 * max(c, 0.0)   # Rueckhaelfte etwas laenger
    W, H = 1.00, 0.84              # hoeher als breit
    x = x0 * f * W
    z = z0 * f * H
    if z < 0: z *= 0.70            # Unterseite abgeflacht
    # vordere Einschnuerung (Pedicel)
    if c < -0.45:
        pinch = 1.0 - 0.16 * ((-c - 0.45) / 0.55)
        x *= pinch; z *= pinch
    y = y0 * L
    return (x, y, z)

# ---------------- Cephalothorax (flacher Schild, Ruecken nach +Z) ----------------
def deform_ceph(x0, y0, z0, theta, phi):
    s, c = math.sin(theta), math.cos(theta)
    p = 0.92
    f = s ** p                                  # 0 an beiden Polen (±Y)
    L, W, H = 1.10, 1.05, 0.62                 # flacher Schild (Z klein), breit
    x = x0 * f * W
    z = z0 * f * H
    if z < 0:
        z *= 0.82                               # Unterseite etwas flacher
    y = y0 * L
    # leichte Rueckwaeulbung (Dome)
    if z > 0:
        z += 0.06 * f * (1.0 - abs(y0) * 0.6)
    # Fovea: kleine Mittelgrube (Delle) auf der Ruecken-Mitte
    fx, fy, fz = 0.0, -0.12, 1.0
    d = math.sqrt((x0 - fx) ** 2 + (y0 - fy) ** 2 + (z0 - fz) ** 2)
    if z > 0:
        z -= 0.045 * math.exp(-(d / 0.22) ** 2)
    # Augenhuegel: kleiner Buegel vorne (-Y) oben
    if y0 < -0.62 and z0 > 0.2:
        t = (-y0 - 0.62) / 0.38
        z += 0.05 * t * (z0 / 1.0)
    return (x, y, z)

# ---------------- Objekte bauen ----------------
aV, aF, aUV = deformed_sphere(34, 34, deform_abdomen)
# zwei kurze Spinnwarzen (Spinnerets) am hinteren unteren Pol
for sx in (-1, 1):
    base = np.array([0.11 * sx, 0.93, -0.15])
    dirv = np.array([0.15 * sx, 1.0, -0.35])
    sv, sf, su = tube_stump(base, dirv, r0=0.09, r1=0.02, length=0.12, seg=8, rings=4)
    aV += sv; aF += [tuple(x + len(aV) - len(sv) for x in f) for f in sf]; aUV += su
abdomen = make_obj("Spinne_Abdomen", aV, aF, aUV)
cV, cF, cUV = deformed_sphere(32, 32, deform_ceph)
ceph = make_obj("Spinne_Cephalothorax", cV, cF, cUV)

# ---------------- Normalisieren (Bounds -> 1.0) + ausgeben ----------------
normalize(abdomen)
normalize(ceph)
for ob in (abdomen, ceph):
    b = bounds(ob)
    print(f"{ob.name}: Bounds X={b[0]:.3f} Y={b[1]:.3f} Z={b[2]:.3f} | Faces={len(ob.data.polygons)}")

# ---------------- Material + Texturen ----------------
tex = os.path.join(out, "Textures")
def img(name, colour=True):
    im = bpy.data.images.load(os.path.join(tex, name))
    if not colour: im.colorspace_settings.name = "Non-Color"
    return im

def spider_mat(matname, albedo, normal, mask):
    m = bpy.data.materials.new(matname); nt = m.node_tree; N = nt.nodes
    bsdf = N["Principled BSDF"]
    a = N.new("ShaderNodeTexImage"); a.image = img(albedo)
    nm = N.new("ShaderNodeTexImage"); nm.image = img(normal, False)
    mk = N.new("ShaderNodeTexImage"); mk.image = img(mask, False)
    nmap = N.new("ShaderNodeNormalMap")
    # Smoothness (Mask-Alpha) -> Roughness = 1 - Smoothness
    inv = N.new("ShaderNodeMath"); inv.operation = "SUBTRACT"; inv.inputs[0].default_value = 1
    nt.links.new(a.outputs[0], bsdf.inputs["Base Color"])
    nt.links.new(nm.outputs[0], nmap.inputs["Color"]); nt.links.new(nmap.outputs[0], bsdf.inputs["Normal"])
    nt.links.new(mk.outputs["Alpha"], inv.inputs[1]); nt.links.new(inv.outputs[0], bsdf.inputs["Roughness"])
    nt.links.new(bsdf.outputs[0], N["Material Output"].inputs[0])
    return m

abdomen.data.materials.append(spider_mat("M_Spinne_Abdomen", "Spinne_Abdomen_Albedo.png", "Spinne_Abdomen_Normal.png", "Spinne_Abdomen_Mask.png"))
ceph.data.materials.append(spider_mat("M_Spinne_Cephalothorax", "Spinne_Cephalothorax_Albedo.png", "Spinne_Cephalothorax_Normal.png", "Spinne_Cephalothorax_Mask.png"))

# ---------------- FBX-Export (einzeln, wie Baumfarn) ----------------
os.makedirs(out, exist_ok=True)
for ob in (abdomen, ceph):
    bpy.ops.object.select_all(action="DESELECT"); ob.select_set(True); bpy.context.view_layer.objects.active = ob
    fbx = os.path.join(out, f"{ob.name}.fbx")
    bpy.ops.export_scene.fbx(filepath=fbx, use_selection=True,
        apply_scale_options="FBX_SCALE_ALL", axis_forward="-Z", axis_up="Y", bake_space_transform=True,
        mesh_smooth_type="FACE", path_mode="STRIP", use_mesh_modifiers=True)
    print(f"FBX: {os.path.basename(fbx)}")

# ---------------- Vorschau-Rendering (Spinne wie in Unity angeordnet) ----------------
if do_render:
    import mathutils
    # Unity -> Blender: (x, y, z) -> (x, -z, y)   [+Z vorne = -Y vorne, +Y up = +Z up]
    def to_bl(pos, scale):
        return (pos[0], -pos[2], pos[1]), (scale[0], scale[2], scale[1])
    apos, ascale = to_bl((0.0, 0.008, -0.052), (0.085, 0.068, 0.11))   # Abdomen (hinten, +Y)
    cpos, cscale = to_bl((0.0, 0.0, 0.025), (0.058, 0.04, 0.065))      # Cephalothorax (vorne, -Y)
    abdomen.location = apos; abdomen.scale = ascale
    ceph.location = cpos; ceph.scale = cscale

    sc = bpy.context.scene
    # Boden (hell, neutral)
    bpy.ops.mesh.primitive_plane_add(size=2.0)
    g = bpy.context.object; gm = bpy.data.materials.new("Ground"); nt = gm.node_tree
    nt.nodes["Principled BSDF"].inputs["Base Color"].default_value = (0.30, 0.285, 0.26, 1)
    nt.nodes["Principled BSDF"].inputs["Roughness"].default_value = 0.9
    g.data.materials.append(gm)
    g.location = (0, 0.02, -0.03)

    # Licht: helles Key-Licht (Sonne) + Fuelllicht, damit der dunkle Koerper sichtbar ist
    w = bpy.data.worlds.new("W"); sc.world = w; wn = w.node_tree
    wn.nodes["Background"].inputs[0].default_value = (0.22, 0.22, 0.23, 1)
    wn.nodes["Background"].inputs[1].default_value = 1.0
    bpy.ops.object.light_add(type="SUN")
    sun = bpy.context.object; sun.data.energy = 6.0; sun.data.angle = math.radians(3)
    sun.rotation_euler = (math.radians(50), 0, math.radians(120))
    bpy.ops.object.light_add(type="AREA")
    fill = bpy.context.object; fill.data.energy = 2.5; fill.data.size = 0.6
    fill.location = (-0.25, -0.2, 0.25)
    fill.rotation_euler = (Vector((0, 0.02, 0.01)) - Vector((-0.25, -0.2, 0.25))).to_track_quat("-Z", "Y").to_euler()

    sc.render.engine = "CYCLES"; sc.cycles.device = "CPU"
    sc.cycles.samples = 64; sc.cycles.use_denoising = True
    sc.render.resolution_x, sc.render.resolution_y = 960, 540
    sc.view_settings.look = "AgX - Medium High Contrast"

    # Kamera: schraeg von vorne (-Y) und oben (+Z)
    cam = bpy.data.cameras.new("Cam"); co = bpy.data.objects.new("Cam", cam)
    sc.collection.objects.link(co); sc.camera = co
    cam.lens = 45
    loc = (0.12, -0.22, 0.14)
    tgt = (0.0, 0.02, 0.008)
    co.location = loc
    co.rotation_euler = (Vector(tgt) - Vector(loc)).to_track_quat("-Z", "Y").to_euler()
    sc.render.filepath = os.path.join(out, "Spinne_Vorschau.png")
    bpy.ops.render.render(write_still=True)
    print("Vorschau: Spinne_Vorschau.png")
print("fertig")
