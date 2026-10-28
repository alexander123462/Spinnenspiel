# MODELL21: Muecke prozedural (Blender 5.1). Ersatz fuer die primitive S2-Kapsel-Muecke.
# Blender-Koordinaten: Vorwaerts = -Y (Unity +Z), hoch = Z, X seitlich.
# Ursprung = Mitte Thorax. Spielskalierung ~4x real (Koerper Thorax+Leib real 6 mm
# -> ~0.0236 m im Spiel; Fliegen-Koerper 0.039 m, Kriterium Muecke 50..70 %).
# Objekte und Slots (feste Reihenfolge, je Objekt Slot 0 = Chitin, Fluegel-Objekte
# tragen das Fluegel-Material; Augen bekommen dunkle Face-Farbe im Texturskript):
#   Koerper     -> Muecke_Koerper (Thorax + segmentierter Leib + Schuppenquaste)
#   Kopf        -> Muecke_Koerper (Kugel + 2 Facettenaugen)
#   Schnabel    -> Muecke_Koerper (Stechruessel + 2 Taster)
#   Fuehler_L/R -> Muecke_Koerper (gebogene Antennen mit Haaren)
#   Beine       -> Muecke_Koerper (6 lange, winklige Beine)
#   Wing_L/R    -> Muecke_Fluegel (schmal, lang, Schuppenrand + Adern)
import bpy, math
import numpy as np

rng = np.random.default_rng(21)
TAU = 2.0 * math.pi

# Groessen in Metern (Spielskalierung ~4x). Ziel: Koerper ~0.023 entlang Y,
# Fluegellange ~0.026 je Seite, Spannweite ~0.055.
BODY_LEN = 0.023


def new_obj(name, verts, faces):
    me = bpy.data.meshes.new(name)
    me.from_pydata(verts, [], faces)
    me.update()
    ob = bpy.data.objects.new(name, me)
    bpy.context.scene.collection.objects.link(ob)
    return ob


def ellipsoid(cx, cy, cz, rx, ry, rz, nseg=12, nrings=7):
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


def tube(pts, r0, r1, nseg=5):
    """Rohr entlang Polylinie pts, Radius linear r0->r1. (verts, faces)."""
    verts, faces = [], []
    ring_start = []
    n = len(pts)
    for i, p in enumerate(pts):
        t = i / max(1, n - 1)
        r = r0 + (r1 - r0) * t
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
    c0 = len(verts); verts.append(tuple(float(x) for x in pts[0]))
    for j in range(nseg):
        faces.append((c0, ring_start[0] + j, ring_start[0] + (j + 1) % nseg))
    c1 = len(verts); verts.append(tuple(float(x) for x in pts[-1]))
    last = ring_start[-1]
    for j in range(nseg):
        faces.append((c1, last + (j + 1) % nseg, last + j))
    return verts, faces


def ring_row(y, rx, rz, nseg, wob, ph_off=0.0):
    """Ein Ring um Y bei y, Radien rx/rz mit Rauhigkeit (Chitin-Unregelmassigkeit)."""
    ring = []
    for j in range(nseg):
        ph = TAU * j / nseg + ph_off
        k = 1.0 + wob * rng.uniform(-1.0, 1.0)
        ring.append((rx * k * math.cos(ph), y, rz * k * math.sin(ph)))
    return ring


def build_koerper():
    """Koerper = segmentierter Leib + gewoelbter Thorax + Schuppenquaste (nur Slot 0).
    Ziel: Thorax+Leib ~0.0236 m (davon 50..70 % Kriterium vs Fliegen-Koerper 0.039).
    Gibt ((verts, faces, mat_index)-Teile) zurueck."""
    parts = []

    # Leib: 10 Ringe von y=+0.003 (Hals) bis y=+0.0160 (Spitze), nach hinten leicht
    # abfallend, Segmentfurchen an 4 Stellen, Ende duenn (typischer Muecken-Leib).
    abd_y = [0.0030, 0.0047, 0.0063, 0.0080, 0.0096, 0.0112, 0.0127, 0.0140, 0.0151, 0.0160]
    abd_rx = [0.0016, 0.0020, 0.0023, 0.0025, 0.0025, 0.0023, 0.0021, 0.0017, 0.0013, 0.0007]
    for k in (2, 4, 6, 8):
        abd_rx[k] *= 0.84  # Segmentfurche (sichtbar tiefer als vorher)
    nseg = 14
    rings = []
    for i, (y, rx) in enumerate(zip(abd_y, abd_rx)):
        zc = -0.0012 * (i / 9.0) ** 1.5  # Leib fallt nach hinten leicht ab
        rings.append([(rx * (1 + 0.05 * rng.uniform(-1, 1)) * math.cos(TAU * j / nseg),
                       y,
                       zc + rx * 0.86 * (1 + 0.05 * rng.uniform(-1, 1)) * math.sin(TAU * j / nseg))
                      for j in range(nseg)])

    # Thorax: 8 Ringe von y=-0.0064 (Hals) bis y=+0.0034, Buckel nach vorn-oben
    # (Muecken-'Rucken'), Querschnitt leicht hochovale.
    tho_prof = [(-0.0064, 0.0017, 0.0012), (-0.0051, 0.0023, 0.0021),
                (-0.0038, 0.0028, 0.0027), (-0.0024, 0.0029, 0.0030),
                (-0.0011, 0.0028, 0.0030), (0.0005, 0.0026, 0.0026),
                (0.0020, 0.0022, 0.0021), (0.0034, 0.0018, 0.0016)]
    tho_rings = []
    for (y, rx, rz) in tho_prof:
        tho_rings.append([(rx * (1 + 0.05 * rng.uniform(-1, 1)) * math.cos(TAU * j / nseg),
                           y,
                           0.0012 + rz * (1 + 0.05 * rng.uniform(-1, 1)) * math.sin(TAU * j / nseg))
                          for j in range(nseg)])

    allr = tho_rings + rings
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
    # Polkappen: vorn (Hals wird vom Kopf ueberdeckt) + Leibspitze.
    c = len(verts); verts.append((0.0, -0.0071, 0.0009))
    for j in range(nseg):
        faces.append((c, starts[0] + (j + 1) % nseg, starts[0] + j))
    c = len(verts); verts.append((0.0, 0.0167, -0.0011))
    last = starts[-1]
    for j in range(nseg):
        faces.append((c, last + j, last + (j + 1) % nseg))
    parts.append((verts, faces, 0))

    # Schuppenquaste am Leibende (2 kleine Bueschel, lesbar im Nahfoto).
    for side in (-1.0, 1.0):
        pts = [(side * 0.0006, 0.0152, -0.0010), (side * 0.0016, 0.0174, 0.0004)]
        tv, tf = tube(pts, 0.00030, 0.00010, nseg=4)
        parts.append((tv, tf, 0))
    return parts


def build_kopf():
    """Kopf: kleine Kugel + 2 grosse Facettenaugen (ein Material; die Augen bekommen
    im Texturskript dunkle Face-Farbe). Eigenes Objekt wie bei der Fliege, damit
    die Koerper-Laenge (Thorax+Leib) an der Unity-Vorlage sauber messbar ist."""
    parts = []
    v, f = ellipsoid(0.0, -0.0103, 0.0002, 0.0020, 0.0018, 0.0018, nseg=12, nrings=7)
    parts.append((v, f))
    v, f = ellipsoid(-0.0022, -0.0110, 0.0009, 0.0018, 0.0019, 0.0016, nseg=10, nrings=6)
    parts.append((v, f))
    v, f = ellipsoid(0.0022, -0.0110, 0.0009, 0.0018, 0.0019, 0.0016, nseg=10, nrings=6)
    parts.append((v, f))
    return parts


def build_schnabel():
    """Stechruessel (langer duenner Rohr nach vorn) + 2 Taster seitlich an der Basis."""
    parts = []
    # Ruesssel: von der Kopf-Vorderseite nach vorn-unten, leicht gebogen, endduenn.
    pts = [(0.0, -0.0118, -0.0006), (0.0, -0.0152, -0.0020),
           (0.0, -0.0190, -0.0033), (0.0, -0.0228, -0.0044)]
    parts.append(tube(pts, 0.00055, 0.00012, nseg=5))
    # Taster: 2 kurze duenne Rohre seitlich an der Ruessselbasis.
    for side in (-1.0, 1.0):
        pts = [(side * 0.0006, -0.0116, -0.0010), (side * 0.0011, -0.0145, -0.0020)]
        parts.append(tube(pts, 0.00022, 0.00008, nseg=4))
    return join(*parts)


def ant_p(u, mirror):
    """Antennenbogen einer Seite (u 0..1): setzt VORN oben am Kopf an (nicht in den
    Facettenaugen!) und bogen nach vorn-oben-aussen."""
    x = mirror * (0.0013 + 0.0042 * u)
    y = -0.0130 - 0.0050 * math.sin(u * math.pi * 0.72)
    z = 0.0024 + 0.0056 * math.sin(u * math.pi * 0.9)
    return (x, y, z)


def build_fuehler(mirror):
    """Hauptast (14 Segmente) + 13 Haarpaare je Antenne (plumoes)."""
    parts = []
    pts = [ant_p(i / 14.0, mirror) for i in range(15)]
    parts.append(tube(pts, 0.00022, 0.00009, nseg=4))
    for i in range(1, 14):
        u = i / 14.0
        p = ant_p(u, mirror)
        q = ant_p(u + 0.05, mirror)
        d = np.subtract(q, p)
        d = d / (np.linalg.norm(d) + 1e-9)
        # senkrecht zum Ast, fast in der Antennenebene
        perp = np.cross(d, [0.0, 0.0, 1.0])
        perp = perp / (np.linalg.norm(perp) + 1e-9)
        L = 0.0009 + 0.0020 * math.sin(min(1.0, u * 1.1) * math.pi)
        for s in (1.0, -1.0):
            q0 = np.array(p)
            q1 = q0 + perp * (s * L) + np.array([0.0, -0.0003 * u, 0.0003])
            parts.append(tube([tuple(q0), tuple(q1)], 0.00013, 0.00004, nseg=3))
    return join(*parts)


def leg_pts(k, mirror):
    """7 Stuetzpunkte eines Beins (Huefte, Oberschenkel, Knie, Schiene, Fuss, Tarsus).
    k=0 vorn (nach vorn), k=1 mitte (seitwaerts), k=2 hinten (nach hinten).
    Beine sind lang und duenn: Fuss-Reichweite ~2x Koerperlaenge (typisch Muecke)."""
    m = mirror
    hips = [(-0.0036, 0.0023), (-0.0011, 0.0028), (0.0017, 0.0023)]
    hy, hz = hips[k]
    jx = rng.uniform(-0.0008, 0.0008)
    if k == 0:   # Vorderbein: seitwaerts-vorn; nach vorn zeigt nur der Ressel (Less Ambiguitaet)
        knee = (m * 0.0135 + jx, -0.0080, 0.0026)
        foot = (m * 0.0205 + jx, -0.0135, -0.0130)
    elif k == 1:  # Mittelbein: Knie seitwaerts, Fuss seitwaerts-unten
        knee = (m * 0.0170 + jx, 0.0005, 0.0034)
        foot = (m * 0.0265 + jx, -0.0045, -0.0140)
    else:         # Hinterbein: Knie hoch seitwaerts-hinten, Fuss weit nach hinten
        knee = (m * 0.0160 + jx, 0.0125, 0.0060)
        foot = (m * 0.0240 + jx, 0.0270, -0.0110)
    hip = (m * 0.0024, hy, hz)
    mid_f = tuple(0.5 * (hip[i] + knee[i]) for i in range(3))
    mid_t = tuple(0.52 * (knee[i] + foot[i]) for i in range(3))
    tip = tuple(foot[i] + 0.004 * (foot[i] - knee[i]) for i in range(3))
    return [hip, mid_f, knee, mid_t, foot, tip]


def build_beine():
    """6 lange, duenne, winklige Beine als Rohre (je 5 Segmente, 4-seitig)."""
    parts = []
    for mirror in (-1.0, 1.0):
        for k in range(3):
            pts = leg_pts(k, mirror)
            parts.append(tube(pts, 0.00026, 0.00006, nseg=4))
    return join(*parts)


def chaikin(pts, iters=2):
    """Open-Polylinie glaetten (Corner-Cutting), Endpunkte bleiben erhalten."""
    p = list(pts)
    for _ in range(iters):
        out = [p[0]]
        for i in range(len(p) - 1):
            a, b = p[i], p[i + 1]
            out.append(tuple(0.75 * a[kk] + 0.25 * b[kk] for kk in range(3)))
            out.append(tuple(0.25 * a[kk] + 0.75 * b[kk] for kk in range(3)))
        out.append(p[-1])
        p = out
    return p


def resample_xz(poly, n):
    """Polylinie nach Bogenlaenge (nur XY) auf n Punkte neu abtasten."""
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


def build_wing(mirror):
    """Ein schmaler langer Fluegel: Gitter mit Fall/Woelbung, Schuppenfransen am Saum,
    2 duenne Adern als Rohr auf der Flaeche."""
    # Costa (Vorderkante) und Saum (Hinterkante), rechte Seite; y hinten.
    COSTA_CTRL = [(0.0030, -0.0060, 0.0052), (0.0075, 0.0040, 0.0048),
                  (0.0100, 0.0140, 0.0034), (0.0112, 0.0225, 0.0018)]
    TRAIL_CTRL = [(0.0032, -0.0030, 0.0046), (0.0072, 0.0060, 0.0040),
                  (0.0094, 0.0160, 0.0026), (0.0106, 0.0238, 0.0010)]
    n_cols = 12
    n_rows = 3
    costa = resample_xz(chaikin(COSTA_CTRL, 2), n_cols)
    trail = resample_xz(chaikin(TRAIL_CTRL, 2), n_cols)
    verts, faces = [], []
    col_start = []
    for i in range(n_cols):
        u = i / (n_cols - 1)
        L, T = costa[i], trail[i]
        col_start.append(len(verts))
        droop = -0.0028 * (u ** 1.6)              # Spitze haengt durch
        for j in range(n_rows + 1):
            v = j / n_rows
            x = L[0] + (T[0] - L[0]) * v
            y = L[1] + (T[1] - L[1]) * v
            z = L[2] + (T[2] - L[2]) * v + droop
            z += 0.0011 * math.sin(math.pi * v)    # Woelbung (Costa hoch, Membran bauchig)
            z += 0.00025 * rng.uniform(-1.0, 1.0) * u
            verts.append((mirror * x, y, z))
    for i in range(n_cols - 1):
        a0 = col_start[i]
        a1 = col_start[i + 1]
        for j in range(n_rows):
            faces.append((a0 + j, a0 + j + 1, a1 + j + 1, a1 + j))
    parts = [(verts, faces)]

    # Schuppenfransen: schmales Zaehnchen-Band am Hinter-/Aussensaum.
    fv, ff = [], []
    prev = None
    for i in range(2, n_cols):
        T = trail[i]
        nxt = trail[min(i + 1, n_cols - 1)]
        ox, oy = nxt[0] - trail[max(i - 1, 0)][0], nxt[1] - trail[max(i - 1, 1)][1]
        ln = math.hypot(ox, oy) + 1e-12
        ox, oy = ox / ln * 0.0012, oy / ln * 0.0012
        base = (mirror * T[0], T[1], T[2] - 0.0002)
        tipv = (mirror * (T[0] + ox), T[1] + oy, T[2] - 0.0016)
        bi = len(fv); fv.append(base)
        ti = len(fv); fv.append(tipv)
        if prev is not None:
            ff.append((prev[0], prev[1], bi))
            ff.append((prev[1], ti, bi))
        prev = (bi, ti)
    parts.append((fv, ff))

    # Adern: 2 duenne Rohre auf der Flaeche laengs des Fluegels.
    for frac in (0.18, 0.55):
        pts = []
        for i in range(0, n_cols, 3):
            u = i / (n_cols - 1)
            L, T = costa[i], trail[i]
            x = L[0] + (T[0] - L[0]) * frac
            y = L[1] + (T[1] - L[1]) * frac
            z = L[2] + (T[2] - L[2]) * frac - 0.0028 * (u ** 1.6) + 0.0004
            pts.append((mirror * x, y, z))
        parts.append(tube(pts, 0.00016, 0.00006, nseg=3))

    v_all, f_all = join(*parts)
    if mirror < 0:
        f_all = [tuple(reversed(f)) for f in f_all]  # Mirroring dreht Normalen
    return v_all, f_all


def make_materials():
    """Zwei Principled-Materialien mit sprechenden Namen (Slot-Reihenfolge fest):
    Slot 0 = Koerper-Chitin, Slot 1 = Fluegel. Augen werden ueber Face-Farbe dunkel."""
    mats = {}
    for name, col in (("Muecke_Koerper", (0.28, 0.24, 0.20, 1)),
                      ("Muecke_Fluegel", (0.55, 0.55, 0.58, 1))):
        m = bpy.data.materials.get(name) or bpy.data.materials.new(name)
        m.use_nodes = True
        bsdf = m.node_tree.nodes.get("Principled BSDF")
        bsdf.inputs["Base Color"].default_value = col
        bsdf.inputs["Roughness"].default_value = 0.65
        mats[name] = m
    return mats


def make_obj_multi(name, parts, mat_names):
    """Objekt aus (verts, faces, mat_index)-Teilen mit festen Slots bauen."""
    v_all, f_all, mi_all = [], [], []
    for v, f, mi in parts:
        off = len(v_all)
        v_all += list(v)
        f_all += [tuple(i + off for i in face) for face in f]
        mi_all += [mi] * len(f)
    ob = new_obj(name, v_all, f_all)
    mats = make_materials()
    for mn in mat_names:
        ob.data.materials.append(mats[mn])
    for p, mi in zip(ob.data.polygons, mi_all):
        p.material_index = mi
        p.use_smooth = True
    return ob


def create_objects():
    """Alle Mueckenobjekte inkl. Slots bauen + Stats ausgeben (Slot = Submesh in Unity)."""
    import mathutils
    mats = make_materials()
    out = {}

    ob = make_obj_multi("Koerper", build_koerper(), ["Muecke_Koerper"])
    out["Koerper"] = ob

    kv, kf = join(*build_kopf())
    ob = new_obj("Kopf", kv, kf)
    ob.data.materials.append(mats["Muecke_Koerper"])
    for p in ob.data.polygons:
        p.use_smooth = True
    out["Kopf"] = ob

    for nm, builder in (("Schnabel", build_schnabel),
                        ("Beine", build_beine)):
        v, f = builder()
        ob = new_obj(nm, v, f)
        ob.data.materials.append(mats["Muecke_Koerper"])
        for p in ob.data.polygons:
            p.use_smooth = True
        out[nm] = ob

    for mirror, nm in ((-1.0, "Fuehler_L"), (1.0, "Fuehler_R")):
        v, f = build_fuehler(mirror)
        ob = new_obj(nm, v, f)
        ob.data.materials.append(mats["Muecke_Koerper"])
        for p in ob.data.polygons:
            p.use_smooth = True
        out[nm] = ob

    for mirror, nm in ((-1.0, "Wing_L"), (1.0, "Wing_R")):
        v, f = build_wing(mirror)
        ob = new_obj(nm, v, f)
        ob.data.materials.append(mats["Muecke_Fluegel"])
        out[nm] = ob

    total = 0
    xs, ys, zs = [], [], []
    for name, o in out.items():
        t = sum(len(p.vertices) - 2 for p in o.data.polygons)
        total += t
        print(f"OBJ {name} tris={t} slots={[m.name for m in o.data.materials]}")
        for c in o.bound_box:
            w = o.matrix_world @ mathutils.Vector(c)
            xs.append(w.x); ys.append(w.y); zs.append(w.z)
    print(f"MUECKE total_tris={total} "
          f"x={max(xs)-min(xs):.4f} y={max(ys)-min(ys):.4f} z={max(zs)-min(zs):.4f}")
    ko = out["Koerper"]
    ky = [(ko.matrix_world @ mathutils.Vector(c)).y for c in ko.bound_box]
    print(f"KOERPER y-extent={max(ky)-min(ky):.4f} (Ziel ~0.023)")
    return out


# Beim Ausfuehren (MCP-exec) direkt bauen. KEIN read_factory_settings: das
# entregistriert das BlenderMCP-Addon und killt die Verbindung. Stattdessen:
for _ob in list(bpy.data.objects):
    bpy.data.objects.remove(_ob, do_unlink=True)
import builtins
builtins.muecke_ns = globals()
create_objects()
