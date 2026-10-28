# MODELL20: Motte prozedural (Blender 5.1). Ersatz fuer die flache 240-Trieder-Platte
# aus motte.py. Blender-Koordinaten: Vorwaerts = -Y (Unity +Z), hoch = Z, X seitlich.
# Ursprung = Mitte Thorax. Spielskalierung wie Fliege (real 15-mm-Koerper, 30 mm
# Spannweite auf Faktor ~4.5 gestreckt): Koerper ~68 mm, Spannweite ~135 mm.
# Objekte und Slots (feste Reihenfolge):
#   Koerper    -> Slot 0 Motte_Koerper  (Kopf + pelziger Thorax + segmentierter Leib + Augen)
#   Fuehler_L/R-> Slot 0 Motte_Koerper  (gefiederte Kammfuehler, beidseitige Daehnchen)
#   Beine      -> Slot 0 Motte_Koerper  (6 Beingespinste)
#   Wing_L/R   -> Slot 0 Motte_Fluegel  (grosses Feld mit welligem Rand, Fransen, Fall)
import bpy, math
import numpy as np

rng = np.random.default_rng(20)
TAU = 2.0 * math.pi

# Groessen in Metern (Spielskalierung). Ziel: Koerper-Laenge 0.068, Spannweite 0.135.
BODY_LEN = 0.068      # Kopf- bis Leibspitze entlang Y
SPAN_HALF = 0.0675    # Fluegel je Seite (ohne Rumpf)


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


def join(*parts):
    """Mehrere (verts, faces)-Teile zu einem Mesh fuehren (Indices versetzt)."""
    v_all, f_all = [], []
    for v, f in parts:
        off = len(v_all)
        v_all += list(v)
        f_all += [tuple(i + off for i in face) for face in f]
    return v_all, f_all


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
        d = d / (np.linalg.norm(d) + 1e-9)
        up = np.array([0.0, 0.0, 1.0]) if abs(d[2]) < 0.9 else np.array([0.0, 1.0, 0.0])
        a = np.cross(d, up); a = a / (np.linalg.norm(a) + 1e-9)
        b = np.cross(d, a)
        ring_start.append(len(verts))
        for j in range(nseg):
            ph = TAU * j / nseg
            c = np.array(p) + r * (math.cos(ph) * a + math.sin(ph) * b)
            verts.append(tuple(float(x) for x in c))
    for i in range(len(ring_start) - 1):
        a0, a1 = ring_start[i], ring_start[i + 1]
        for j in range(nseg):
            faces.append((a0 + j, a0 + (j + 1) % nseg,
                          a1 + (j + 1) % nseg, a1 + j))
    # Polkappen
    c0 = len(verts); verts.append(tuple(float(x) for x in pts[0]))
    for j in range(nseg):
        faces.append((c0, ring_start[0] + j, ring_start[0] + (j + 1) % nseg))
    c1 = len(verts); verts.append(tuple(float(x) for x in pts[-1]))
    last = ring_start[-1]
    for j in range(nseg):
        faces.append((c1, last + (j + 1) % nseg, last + j))
    return verts, faces


def furry_ring(y, rx, rz, nseg, wob, ph_off=0.0):
    """Ein Koenring um Y bei y, Radien rx/rz mit Rauhigkeit (Pelz-Silhouette)."""
    ring = []
    for j in range(nseg):
        ph = TAU * j / nseg + ph_off
        k = 1.0 + wob * rng.uniform(-1.0, 1.0)
        ring.append((rx * k * math.cos(ph), y, rz * k * math.sin(ph)))
    return ring


def build_koerper():
    """Koerper = Kopf + pelziger Thorax + segmentierter Leib + 2 Augen (eine Mesh)."""
    parts = []

    # Thorax: 7 Ringe von y=-0.026 (vorn) bis y=+0.004, wuchtigster Abschnitt.
    # Erster Ring schmaler = Hals zum Kopf, damit Thorax als Abschnitt lesbar ist.
    prof = [(-0.026, 0.0055), (-0.021, 0.0105), (-0.016, 0.0122), (-0.011, 0.0118),
            (-0.006, 0.0102), (-0.001, 0.0082), (0.004, 0.0062)]
    nseg = 14
    rings = [furry_ring(y, rx, rx * 0.95, nseg, 0.11) for (y, rx) in prof]

    # Leib: 7 Ringe, kraeftig spindelfoermig (nicht nadeldünn), Ende bei y=0.036.
    abd_y = [0.004, 0.010, 0.016, 0.022, 0.027, 0.031, 0.0355]
    abd_r = [0.0060, 0.0072, 0.0074, 0.0068, 0.0056, 0.0040, 0.0018]
    for k in (1, 3, 5):
        abd_r[k] *= 0.92  # Segmentfurche
    abd_rings = [furry_ring(y, rx, rx * 0.92, nseg, 0.10)
                 for (y, rx) in zip(abd_y, abd_r)]

    allr = rings + abd_rings
    verts, faces = [], []
    starts = []
    for ring in allr:
        starts.append(len(verts))
        verts += ring
    for i in range(len(allr) - 1):
        a0, a1 = starts[i], starts[i + 1]
        for j in range(nseg):
            faces.append((a0 + j, a0 + (j + 1) % nseg,
                          a1 + (j + 1) % nseg, a1 + j))
    # Polkappen vorne (y=-0.026) und hinten (Leibspitze y=0.0355).
    c = len(verts); verts.append((0.0, -0.0285, 0.0005))
    for j in range(nseg):
        faces.append((c, starts[0] + (j + 1) % nseg, starts[0] + j))
    c = len(verts); verts.append((0.0, 0.0372, 0.0004))
    last = starts[-1]
    for j in range(nseg):
        faces.append((c, last + j, last + (j + 1) % nseg))
    parts.append((verts, faces))

    # Kopf: kleine Kugel vorn bei y=-0.0325, ueergingelgt mit Thorax (Naht versteckt).
    v, f = ellipsoid(0.0, -0.0330, -0.0004, 0.0068, 0.0060, 0.0062, nseg=12, nrings=7)
    parts.append((v, f))

    # Augen: zwei dunkle Kugeln seitlich am Kopf, deutlich vorstehend.
    v, f = ellipsoid(-0.0072, -0.0350, 0.0006, 0.0034, 0.0030, 0.0034, nseg=10, nrings=6)
    parts.append((v, f))
    v, f = ellipsoid(0.0072, -0.0350, 0.0006, 0.0034, 0.0030, 0.0034, nseg=10, nrings=6)
    parts.append((v, f))

    return join(*parts)


def ant_arc(u):
    """Rechter Kammfuehler: Bogen vom Kopf nach vorn-aussen (u 0..1). Punkt + Tangente."""
    x = 0.0045 + 0.0230 * u
    y = -0.0340 - 0.0200 * math.sin(u * math.pi * 0.62)
    z = 0.0038 + 0.0045 * math.sin(u * math.pi)
    e = 1e-4
    p1 = ant_arc_p(u + e)
    p0 = ant_arc_p(u - e)
    d = np.subtract(p1, p0)
    d = d / (np.linalg.norm(d) + 1e-9)
    return (x, y, z), d


def ant_arc_p(u):
    u = min(max(u, 0.0), 1.0)
    x = 0.0050 + 0.0180 * u
    y = -0.0360 - 0.0150 * math.sin(u * math.pi * 0.62)
    z = 0.0048 + 0.0060 * math.sin(u * math.pi)
    return (x, y, z)


def build_fuehler(mirror):
    """Gefiederter Kammfuehler einer Seite: Hauptast + Daehnchen beidseits (Kamm)."""
    parts = []
    # Hauptast: 16 Segmente, Radius 0.0009 -> 0.00035.
    pts = [ant_arc_p(i / 16.0) for i in range(17)]
    pts = [(mirror * x, y, z) for (x, y, z) in pts]
    parts.append(tube(pts, 0.0009, 0.00035, nseg=5))

    # Daehnchen: 15 je Seite (dicht = gefiedert), senkrecht zum Ast in fast
    # horizontaler Ebene, Laenge zur Mitte hin groesser (Kammform).
    for i in range(1, 16):
        u = i / 16.0
        p, d = ant_arc(u)
        perp = np.array([-d[1], d[0], 0.08])
        perp = perp / (np.linalg.norm(perp) + 1e-9)
        L = 0.0018 + 0.0052 * math.sin(min(1.0, u * 1.15) * math.pi)
        for side in (1.0, -1.0):
            q0 = np.array(p)
            q1 = q0 + perp * side * L * 0.55 + np.array([0.0, -0.0010, 0.0005])
            q2 = q0 + perp * side * L
            w = 0.00078 * (1.0 - 0.45 * u)
            b = np.array([-perp[1], perp[0], 0.0]) * w
            vs = []
            for q in (q0, q1, q2):
                vs.append((mirror * (q[0] + b[0]), q[1] + b[1], q[2] + b[2]))
                vs.append((mirror * (q[0] - b[0]), q[1] - b[1], q[2] - b[2]))
            fs = [(0, 1, 3), (0, 3, 2), (2, 3, 5), (2, 5, 4)]
            parts.append((vs, fs))
    return join(*parts)


def build_beine():
    """6 Beine (3 je Seite): Schenkel + Schiene als feines Rohr, nach UNTER den Koerper."""
    parts = []
    ys = (-0.0215, -0.0115, -0.0015)
    for mirror in (-1.0, 1.0):
        for k, y in enumerate(ys):
            xout = 0.0056 + 0.0022 * (k % 2)
            pts = [(mirror * 0.0035, y, -0.0045),
                   (mirror * xout, y - 0.0010, -0.0088),
                   (mirror * (xout + 0.0018), y + 0.0020, -0.0116)]
            parts.append(tube(pts, 0.00070, 0.00035, nseg=4))
    return join(*parts)


def chaikin(pts, iters=2):
    """Open-Polylinie glaetten (Corner-Cutting), Endpunkte bleiben erhalten."""
    p = list(pts)
    for _ in range(iters):
        out = [p[0]]
        for i in range(len(p) - 1):
            a, b = p[i], p[i + 1]
            out.append(tuple(0.75 * a[k] + 0.25 * b[k] for k in range(3)))
            out.append(tuple(0.25 * a[k] + 0.75 * b[k] for k in range(3)))
        out.append(p[-1])
        p = out
    return p


def resample_xz(poly, n):
    """Polylinie (x,y,z) nach Bogenlaenge (nur XY) auf n Punkte neu abtasten."""
    acc = [0.0]
    for i in range(1, len(poly)):
        dx = poly[i][0] - poly[i - 1][0]
        dy = poly[i][1] - poly[i - 1][1]
        acc.append(acc[-1] + math.hypot(dx, dy))
    total = acc[-1] + 1e-12
    out = []
    j = 0
    for k in range(n):
        t = total * k / (n - 1)
        while j < len(poly) - 2 and acc[j + 1] < t:
            j += 1
        seg = acc[j + 1] - acc[j] + 1e-12
        w = (t - acc[j]) / seg
        out.append(tuple((1 - w) * poly[j][m] + w * poly[j + 1][m] for m in range(3)))
    return out


# Fluegel-Kontrollpunkte (rechte Seite; x = aussen, y = hinten entlang Koerper):
# Ansatz oben am Thorax Ruecken; flacher welliger Saum (kein Kerb-Artefakt).
COSTA_CTRL = [(0.0025, -0.0230, 0.0085), (0.0280, -0.0270, 0.0070),
              (0.0500, -0.0210, 0.0040), (0.0645, -0.0090, 0.0000)]
TRAIL_CTRL = [(0.0025, 0.0300, 0.0060), (0.0130, 0.0430, 0.0040),
              (0.0310, 0.0440, 0.0015), (0.0450, 0.0360, -0.0010),
              (0.0430, 0.0300, -0.0015), (0.0580, 0.0040, -0.0035),
              (0.0645, -0.0090, 0.0000)]


def build_wing(mirror):
    """Ein Fluegel: Costa- und Saumkurve als Gitter (Fall + Woelbung), welliger
    Saum durch Kontrollpunkte, Fransen am Aussen-/Hintersaum."""
    n_cols = 13
    n_rows = 4  # Sehnenrichtung, Quads pro Spalte
    costa = resample_xz(chaikin(COSTA_CTRL, 2), n_cols)
    trail = resample_xz(chaikin(TRAIL_CTRL, 2), n_cols)
    verts, faces = [], []
    col_start = []
    for i in range(n_cols):
        u = i / (n_cols - 1)
        L, T = costa[i], trail[i]
        col_start.append(len(verts))
        droop = -0.0065 * (u ** 1.6)          # Fluegelspitze haengt durch
        for j in range(n_rows + 1):
            v = j / n_rows
            x = L[0] + (T[0] - L[0]) * v
            y = L[1] + (T[1] - L[1]) * v
            z = L[2] + (T[2] - L[2]) * v + droop
            z += -0.0016 * math.sin(math.pi * v)   # leichte Woelbung
            z += 0.0007 * rng.uniform(-1.0, 1.0) * u  # Unregelmassigkeit nach aussen
            verts.append((mirror * x, y, z))
    for i in range(n_cols - 1):
        a0 = col_start[i]
        a1 = col_start[i + 1]
        for j in range(n_rows):
            faces.append((a0 + j, a0 + j + 1, a1 + j + 1, a1 + j))
    parts = [(verts, faces)]

    # Fransen: schmales Band am Hinter-/Aussensaum (u>0.25), nach unten-auessen.
    fv, ff = [], []
    first = None
    prev = None
    for i in range(3, n_cols):
        u = i / (n_cols - 1)
        T = trail[i]
        nxt = trail[min(i + 1, n_cols - 1)]
        ox, oy = nxt[0] - trail[max(i - 1, 0)][0], nxt[1] - trail[max(i - 1, 1)][1]
        ln = math.hypot(ox, oy) + 1e-12
        ox, oy = ox / ln * 0.0020, oy / ln * 0.0020
        base = (mirror * T[0], T[1], T[2] - 0.0004)
        tip = (mirror * (T[0] + ox), T[1] + oy, T[2] - 0.0040)
        bi = len(fv); fv.append(base)
        ti = len(fv); fv.append(tip)
        if prev is not None:
            ff.append((prev[0], prev[1], bi))
            ff.append((prev[1], ti, bi))
        prev = (bi, ti)
    parts.append((fv, ff))

    v_all, f_all = join(*parts)
    if mirror < 0:
        f_all = [tuple(reversed(f)) for f in f_all]  # Mirroring dreht Normalen
    return v_all, f_all


def make_materials():
    """Zwei Principled-Materialien mit sprechenden Namen (Slot 0 je Objekt)."""
    mats = {}
    for name, col in (("Motte_Koerper", (0.30, 0.24, 0.17, 1)),
                      ("Motte_Fluegel", (0.42, 0.35, 0.26, 1))):
        m = bpy.data.materials.get(name) or bpy.data.materials.new(name)
        m.use_nodes = True
        bsdf = m.node_tree.nodes.get("Principled BSDF")
        bsdf.inputs["Base Color"].default_value = col
        bsdf.inputs["Roughness"].default_value = 0.75
        mats[name] = m
    return mats


def create_objects():
    """Alle 6 Mottenobjekte inkl. Slots bauen. Gibt Dict name->object zurueck."""
    mats = make_materials()
    out = {}

    v, f = build_koerper()
    ob = new_obj("Koerper", v, f)
    ob.data.materials.append(mats["Motte_Koerper"])
    for p in ob.data.polygons:
        p.use_smooth = True
    out["Koerper"] = ob

    for mirror, nm in ((-1.0, "Fuehler_L"), (1.0, "Fuehler_R")):
        v, f = build_fuehler(mirror)
        ob = new_obj(nm, v, f)
        ob.data.materials.append(mats["Motte_Koerper"])
        for p in ob.data.polygons:
            p.use_smooth = True
        out[nm] = ob

    v, f = build_beine()
    ob = new_obj("Beine", v, f)
    ob.data.materials.append(mats["Motte_Koerper"])
    for p in ob.data.polygons:
        p.use_smooth = True
    out["Beine"] = ob

    for mirror, nm in ((-1.0, "Wing_L"), (1.0, "Wing_R")):
        v, f = build_wing(mirror)
        ob = new_obj(nm, v, f)
        ob.data.materials.append(mats["Motte_Fluegel"])
        out[nm] = ob

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
    print(f"MOTTE total_tris={total} "
          f"x={max(xs)-min(xs):.4f} y={max(ys)-min(ys):.4f} z={max(zs)-min(zs):.4f}")
    ko = out["Koerper"]
    ky = [(ko.matrix_world @ mathutils.Vector(c)).y for c in ko.bound_box]
    print(f"KOERPER y-extent={max(ky)-min(ky):.4f} (Ziel ~0.068)")
    return out


# Beim Ausfuehren (MCP-exec) direkt bauen. KEIN read_factory_settings: das
# entregistriert das BlenderMCP-Addon und killt die Verbindung. Stattdessen:
for _ob in list(bpy.data.objects):
    bpy.data.objects.remove(_ob, do_unlink=True)
create_objects()
