# MODELL19: Fliege prozedural (Blender 5.1). Vorher/Nachher-Ersatz fuer die
# Primitive-Fliege aus InsectBuilder.CreateFly.
# Blender-Koordinaten: Vorwärts = -Y (Unity +Z), hoch = Z, X = seitlich.
# Ursprung = Mitte Thorax. Spielskalierung: realer 8-mm-Fliegenkoerper
# (Thorax+Hinterleib) auf ~36 mm gestreckt (Faktor ~4.5), wie die alte Fliege.
# Objekte und Slots (feste Reihenfolge):
#   Koerper  -> Slot 0 Fliege_Koerper   (Thorax + 3 segmentierte Hinterleibs-Ringe)
#   Kopf     -> Slot 0 Fliege_Koerper, Slot 1 Fliege_Auge (Kugel + Facettenaugen + Antennen)
#   Beine    -> Slot 0 Fliege_Koerper   (6 Beingespinste)
#   Wing_L / Wing_R -> Slot 0 Fliege_Fluegel (Membran + Langsadern + Queradern)
import bpy, math
import numpy as np

rng = np.random.default_rng(19)
TAU = 2.0 * math.pi


def new_obj(name, verts, faces):
    me = bpy.data.meshes.new(name)
    me.from_pydata(verts, [], faces)
    me.update()
    ob = bpy.data.objects.new(name, me)
    bpy.context.scene.collection.objects.link(ob)
    return ob


def ellipsoid(cx, cy, cz, rx, ry, rz, nseg=14, nrings=8):
    """UV-Kugel-Geruest, Radien pro Achse, je ein Polvertex. Gibt (verts, faces) zurueck."""
    verts = [(cx, cy + ry, cz), (cx, cy - ry, cz)]  # 0=+Y-Pol, 1=-Y-Pol
    faces = []
    ring_start = []
    for i in range(1, nrings):
        th = math.pi * i / nrings
        ring_start.append(len(verts))
        for j in range(nseg):
            ph = TAU * j / nseg
            verts.append((cx + rx * math.sin(th) * math.cos(ph),
                          cy + ry * math.cos(th),
                          cz + rz * math.sin(th) * math.sin(ph)))
    for j in range(nseg):
        faces.append((0, ring_start[0] + (j + 1) % nseg, ring_start[0] + j))
        last = ring_start[-1]
        faces.append((1, last + j, last + (j + 1) % nseg))
    for i in range(len(ring_start) - 1):
        a0, a1 = ring_start[i], ring_start[i + 1]
        for j in range(nseg):
            faces.append((a0 + j, a0 + (j + 1) % nseg,
                          a1 + (j + 1) % nseg, a1 + j))
    return verts, faces


def tube(pts, r0, r1, nseg=6):
    """Sechskant-Rohr entlang Polylinie pts, Radius linear r0->r1. (verts, faces)."""
    verts, faces = [], []
    ring_start = []
    n = len(pts)
    for i, p in enumerate(pts):
        t = i / max(1, n - 1)
        r = r0 + (r1 - r0) * t
        # Tangente -> zwei Normalachsen
        if i == 0:
            d = np.array(pts[1]) - np.array(p)
        elif i == n - 1:
            d = np.array(p) - np.array(pts[-2])
        else:
            d = np.array(pts[i + 1]) - np.array(pts[i - 1])
        d = d / max(1e-9, float(np.linalg.norm(d)))
        up = np.array([0.0, 0.0, 1.0]) if abs(d[2]) < 0.9 else np.array([1.0, 0.0, 0.0])
        u = np.cross(d, up); u = u / max(1e-9, float(np.linalg.norm(u)))
        v = np.cross(d, u)
        ring_start.append(len(verts))
        for j in range(nseg):
            ph = TAU * j / nseg
            off = (u * math.cos(ph) + v * math.sin(ph)) * r
            verts.append(tuple(np.array(p) + off))
    for i in range(n - 1):
        a0, a1 = ring_start[i], ring_start[i + 1]
        for j in range(nseg):
            faces.append((a0 + j, a0 + (j + 1) % nseg, a1 + (j + 1) % nseg, a1 + j))
    # Kappen an beiden Enden
    for end_i, ring_i, rev in ((0, 0, True), (n - 1, n - 1, False)):
        c = len(verts)
        verts.append(tuple(np.array(pts[end_i])))
        a0 = ring_start[ring_i]
        for j in range(nseg):
            f = (c, a0 + j, a0 + (j + 1) % nseg)
            faces.append(tuple(reversed(f)) if rev else f)
    return verts, faces


def join(*parts):
    """Mehrere (verts, faces)-Teile zu einem Mesh verschieben."""
    verts, faces = [], []
    for v, f in parts:
        off = len(verts)
        verts += list(v)
        faces += [tuple(i + off for i in face) for face in f]
    return verts, faces


# ---------------------------------------------------------------- Koerper
# Thorax + 3 segmentierte Hinterleibs-Ringe. Y-Bereich: -0.0115..+0.0245
# = 0.036 m (Unity-Z-Achse; Muecke 0.0216 m = 60 % davon).
def build_koerper():
    # Thorax deutlich wuchtiger als die Hinterleibsringe, Ringe mit
    # sichtbaren Einschnuerungen (keine Perlenkette). Y: -0.0125..+0.0265 = 0.039 m.
    thorax = ellipsoid(0, -0.0045, 0.0018, 0.0072, 0.0080, 0.0066, nseg=14, nrings=8)
    seg1 = ellipsoid(0, 0.0055, -0.0002, 0.0056, 0.0055, 0.0052, nseg=14, nrings=8)
    seg2 = ellipsoid(0, 0.0135, -0.0012, 0.0046, 0.0060, 0.0042, nseg=14, nrings=8)
    # letzter Ring laeuft flacher zu (eifoermig, nicht wespenartig zugespitzt)
    seg3 = ellipsoid(0, 0.0215, -0.0026, 0.0040, 0.0050, 0.0036, nseg=14, nrings=7)
    return join(thorax, seg1, seg2, seg3)


# ---------------------------------------------------------------- Kopf
def build_kopf():
    """Kugel + 2 Facettenaugen (Slot 1) + 2 Antennen. Gibt (verts, faces, n_auge_faces) zurueck."""
    # Kopf etwas kleiner als der Thorax, aber die Augen dominieren das Gesicht.
    schadel = ellipsoid(0, -0.0155, 0.0002, 0.0050, 0.0046, 0.0048, nseg=12, nrings=8)
    # Augen als deutliche Dome: seitlich und unten ueber die Schadelsilhouette.
    auge_l = ellipsoid(-0.0046, -0.0165, 0.0006, 0.0038, 0.0046, 0.0050, nseg=12, nrings=7)
    auge_r = ellipsoid(0.0046, -0.0165, 0.0006, 0.0038, 0.0046, 0.0050, nseg=12, nrings=7)
    ant_l = tube([(-0.0018, -0.0194, 0.0012), (-0.0026, -0.0210, 0.0004),
                  (-0.0032, -0.0222, -0.0006)], 0.0004, 0.00016, nseg=5)
    ant_r = tube([(0.0018, -0.0194, 0.0012), (0.0026, -0.0210, 0.0004),
                  (0.0032, -0.0222, -0.0006)], 0.0004, 0.00016, nseg=5)
    v, f = join(schadel, ant_l, ant_r, auge_l, auge_r)
    # Augen-Gesichtszahlen kommen zuletzt -> Slot 1 = letzte beiden Teile.
    n_auge = len(auge_l[1]) + len(auge_r[1])
    return v, f, n_auge


def facet_eyes(ob, face_start):
    """Facetten-Look: Augenvertices wellig radial versetzen, Faces flach schattieren."""
    zentren = ((-0.0046, -0.0165, 0.0006), (0.0046, -0.0165, 0.0006))
    for v in ob.data.vertices:
        for cz in zentren:
            d = np.array(v.co) - np.array(cz)
            r = float(np.linalg.norm(d))
            if r < 0.0062 and r > 1e-9:
                n = d / r
                a = math.atan2(n[2], n[0])
                b = math.acos(max(-1.0, min(1.0, n[1])))
                w = 0.00035 * math.sin(a * 9.0) * math.sin(b * 9.0)
                v.co.x += float(n[0]) * w
                v.co.y += float(n[1]) * w
                v.co.z += float(n[2]) * w
    for p in ob.data.polygons:
        if p.index >= face_start:
            p.use_smooth = False


# ---------------------------------------------------------------- Beine
# 6 Beine seitlich am Thorax: Huefte -> Knie (raus und hoch) -> Tarsus
# (abwaerts). Kein Zusammenlaufen unter der Koerpermitte.
BEIN_ANKER = [(-0.0052, -0.0075), (-0.0060, -0.0020), (-0.0052, 0.0035)]


def build_beine():
    parts = []
    for sx in (-1.0, 1.0):
        for i, (ax, ay) in enumerate(BEIN_ANKER):
            vx = 1.0 if i == 0 else (-1.0 if i == 2 else 0.0)
            pts = [
                (sx * ax, ay, -0.0040),                      # Huefte am Thoraxrand
                (sx * (ax + 0.0065), ay + 0.0025 * vx, -0.0018),  # Knie: raus, hoch
                (sx * (ax + 0.0065), ay + 0.0100 * vx, -0.0110),  # Schiene: senkrecht runter
            ]
            parts.append(tube(pts, 0.00050, 0.00024, nseg=6))
    return join(*parts)


# ---------------------------------------------------------------- Fluegel
# Membran: langes Runddreieck nach hinten (+Y), leicht nach aussen geneigt,
# mit Woelbung. Adern: 5 Laengsadern + 2 Queradern als duenne Roehrchen.
def wing_outline(t):
    """Breitenprofil entlang Fluegellaenge t 0..1 (0 = Ansatz)."""
    return math.sin(math.pi * min(1.0, t * 1.05)) ** 0.8 * (1.0 - 0.25 * t)


def build_wing(mirror):
    """Ein Fluegel als (verts, faces). Ansatz bei (mirror*0.0055,-0.0030,0.0090)."""
    # Kuerzer als der Hinterleib (Ende +0.020 < Spitze +0.0265), breitere
    # Membran, liegt als Dach ueber dem Koerper. Adern duenner als frueher.
    y0, y1 = -0.0030, 0.0200
    half_max = 0.0075
    z0, z_drop, camber = 0.0078, 0.0035, 0.0010
    rows, cols = 9, 4
    verts, faces = [], []
    for i in range(rows):
        t = i / (rows - 1)
        y = y0 + (y1 - y0) * t
        half = half_max * wing_outline(t)
        outw = mirror * (0.0045 + 0.0060 * t)      # seitlicher Versatz (Dach, Ueberlappung)
        z = z0 - z_drop * t + camber * math.sin(math.pi * t)
        for j in range(cols):
            s = -1.0 + 2.0 * j / (cols - 1)
            verts.append((outw + mirror * half * s, y, z + 0.0006 * (1 - abs(s))))
    for i in range(rows - 1):
        a0, a1 = i * cols, (i + 1) * cols
        for j in range(cols - 1):
            faces.append((a0 + j, a0 + j + 1, a1 + j + 1, a1 + j))

    def wing_pt(t, s):
        half = half_max * wing_outline(t)
        outw = mirror * (0.0045 + 0.0060 * t)
        return (outw + mirror * half * s, y0 + (y1 - y0) * t,
                z0 - z_drop * t + camber * math.sin(math.pi * t) + 0.0003)

    # Laengsadern: bei s-Fraktionen 0..1 ueber der Membran
    vein_parts = [(verts, faces)]
    for sf in (0.05, 0.30, 0.55, 0.78, 0.96):
        pts = [wing_pt(i / (rows - 1), -1.0 + 2.0 * sf) for i in range(0, rows, 2)]
        vein_parts.append(tube(pts, 0.00016, 0.00012, nseg=4))
    # Queradern bei t=0.35 und t=0.65
    for t in (0.35, 0.65):
        pts = [wing_pt(t, s) for s in (-0.85, -0.2, 0.4, 0.9)]
        vein_parts.append(tube(pts, 0.00014, 0.00012, nseg=4))
    return join(*vein_parts)


# ---------------------------------------------------------------- Assembly
def make_materials():
    """Drei Principled-Materialien mit sprechenden Namen (Slot-Reihenfolge pro Objekt)."""
    mats = {}
    for name, col in (("Fliege_Koerper", (0.10, 0.11, 0.13, 1)),
                      ("Fliege_Auge", (0.28, 0.10, 0.07, 1)),
                      ("Fliege_Fluegel", (0.75, 0.78, 0.80, 1))):
        m = bpy.data.materials.get(name) or bpy.data.materials.new(name)
        m.use_nodes = True
        bsdf = m.node_tree.nodes.get("Principled BSDF")
        bsdf.inputs["Base Color"].default_value = col
        bsdf.inputs["Roughness"].default_value = 0.55
        mats[name] = m
    return mats


def create_objects():
    """Alle 5 Fliegenobjekte inkl. Slots bauen. Gibt Dict name->object zurueck."""
    mats = make_materials()
    out = {}

    v, f = build_koerper()
    ob = new_obj("Koerper", v, f)
    ob.data.materials.append(mats["Fliege_Koerper"])
    for p in ob.data.polygons:
        p.use_smooth = True
    out["Koerper"] = ob

    v, f, n_auge = build_kopf()
    ob = new_obj("Kopf", v, f)
    ob.data.materials.append(mats["Fliege_Koerper"])  # Slot 0
    ob.data.materials.append(mats["Fliege_Auge"])     # Slot 1
    for p in ob.data.polygons:
        p.material_index = 1 if p.index >= len(f) - n_auge else 0
    facet_eyes(ob, len(f) - n_auge)
    for p in ob.data.polygons:
        if p.material_index == 0:
            p.use_smooth = True
    out["Kopf"] = ob

    v, f = build_beine()
    ob = new_obj("Beine", v, f)
    ob.data.materials.append(mats["Fliege_Koerper"])
    for p in ob.data.polygons:
        p.use_smooth = True
    out["Beine"] = ob

    ob = new_obj("Wing_L", *build_wing(-1.0))
    ob.data.materials.append(mats["Fliege_Fluegel"])
    out["Wing_L"] = ob
    ob = new_obj("Wing_R", *build_wing(1.0))
    ob.data.materials.append(mats["Fliege_Fluegel"])
    out["Wing_R"] = ob

    # Stats: Dreiecke, Bounds, Slots (Slot-Index = Submesh-Index in Unity).
    import mathutils
    total = 0
    xs, ys, zs = [], [], []
    for name, o in out.items():
        t = sum(len(p.vertices) - 2 for p in o.data.polygons)
        total += t
        print(f"OBJ {name} tris={t} slots={[m.name for m in o.data.materials]}")
        for c in o.bound_box:
            w = o.matrix_world @ mathutils.Vector(c)
            xs.append(w.x); ys.append(w.y); zs.append(w.z)
    print(f"FLIEGE total_tris={total} "
          f"x={max(xs)-min(xs):.4f} y={max(ys)-min(ys):.4f} z={max(zs)-min(zs):.4f}")
    kb = out["Koerper"].bound_box
    ky = [(o.matrix_world @ mathutils.Vector(c)).y for c in kb]
    print(f"KOERPER y-extent={max(ky)-min(ky):.4f} (Ziel 0.036)")
    return out
