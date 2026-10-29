# MODELL7: Farn_Tropisch (Schwertfarn) - aufrechte Rosette, 8 bogige Wedel
# mit je 25 Fiederpaaren, 2 junge Krumme, kurzer Rhizom-Kegel.
# EIN Objekt "Farn_Tropisch", EIN Material-Slot, Ursprung am Fuss, Z oben.
# Budget 1500..3000 Dreiecke, Zielhoehe ca. 1.0 m.
# UV-Atlas 8x4 (wie MODELL6): Zellen 0,1,2 = Fiedern (3 Groessenbande),
# Zelle 3 = Spindel/Stiel/Rhizom/Krumme.
import bpy, bmesh, math, random
from mathutils import Vector

SEED = 7742
COLS, ROWS = 8, 4
PAD = 0.04


def uv_cell(cell_i, t, xn):
    """UV einer Atlaszelle: t entlang (0..1), xn quer (0..1)."""
    col, row = cell_i % COLS, cell_i // COLS
    return ((col + PAD + (1 - 2 * PAD) * xn) / COLS,
            (row + PAD + (1 - 2 * PAD) * t) / ROWS)


uvl = None  # UV-Layer-Name, wird vor dem Bauen gesetzt


def set_face_uv(face, cell_i, pts, lay):
    for lp, (tt, xn) in zip(face.loops, pts):
        lp[lay].uv = uv_cell(cell_i, tt, xn)


# ---- Wedelachse: echter Bogen durch Integration der Tangente ----
_RACHIS = {}


def _rachis_curve(L, phi0_deg, phi1_deg, wig, n=48):
    """Stiel erst aufrecht, dann bogt die Spindel nach vorn (+Y) um."""
    key = (round(L, 4), phi0_deg, phi1_deg, round(wig, 4), n)
    pts = _RACHIS.get(key)
    if pts is not None:
        return pts
    pts = [Vector((0.0, 0.0, 0.0))]
    ds = 1.0 / n
    for k in range(n):
        t = (k + 0.5) * ds
        phi = math.radians(phi0_deg + (phi1_deg - phi0_deg) * (t ** 0.9))
        p = pts[-1] + Vector((wig * L * math.cos(t * 5.2) * ds,
                              L * ds * math.sin(phi), L * ds * math.cos(phi)))
        pts.append(p)
    _RACHIS[key] = pts
    return pts


def rachis_point(s, L, phi0_deg, phi1_deg, wig):
    pts = _rachis_curve(L, phi0_deg, phi1_deg, wig)
    n = len(pts) - 1
    u = max(0.0, min(1.0, s)) * n
    i = min(n - 1, int(u))
    return pts[i].lerp(pts[i + 1], u - i)


def rachis_tangent(s, L, phi0_deg, phi1_deg, wig):
    a = rachis_point(max(0.0, s - 0.01), L, phi0_deg, phi1_deg, wig)
    b = rachis_point(min(1.0, s + 0.01), L, phi0_deg, phi1_deg, wig)
    return (b - a).normalized()


def rachis_w(s, w0, w1):
    return w0 + (w1 - w0) * s


def build_axis(bm, rng, L, phi0, phi1, wig, w0, w1, nst, cell_i):
    """Stiel + Spindel als 5-seitiges Rohr mit Kappe unten. Slot 0."""
    lay = bm.loops.layers.uv.active
    rings = []
    for k in range(nst):
        s = k / (nst - 1)
        p = rachis_point(s, L, phi0, phi1, wig)
        tg = rachis_tangent(s, L, phi0, phi1, wig)
        side = Vector((tg.z, 0.0, -tg.x)).normalized()
        up = tg.cross(side)
        w = rachis_w(s, w0, w1) * rng.uniform(0.95, 1.05)
        ring = []
        for c in range(5):
            a = math.radians(72.0 * c) + 0.35 * s
            ring.append(bm.verts.new(p + (side * math.cos(a) + up * math.sin(a)) * w))
        rings.append(ring)
    for k in range(nst - 1):
        a, b = rings[k], rings[k + 1]
        ta, tb = k / (nst - 1), (k + 1) / (nst - 1)
        for c in range(5):
            f = bm.faces.new((a[c], a[(c + 1) % 5], b[(c + 1) % 5], b[c]))
            set_face_uv(f, cell_i, ((ta, c / 5), (ta, (c + 1) / 5),
                                    (tb, (c + 1) / 5), (tb, c / 5)), lay)
    tip = bm.faces.new(tuple(reversed(rings[-1])))
    for lp in tip.loops:
        lp[lay].uv = uv_cell(cell_i, 1.0, 0.5)
    cap = bm.faces.new(rings[0])
    for lp in cap.loops:
        lp[lay].uv = uv_cell(cell_i, 0.0, 0.5)


# ---- Fiederblatt: flach zur Wedelachse, Mittelrippe hoch, Rand gezackt ----
def build_pinna(bm, rng, p0, d, wdir, N, Lp, Wp, lob_amp, fold, droop,
                cell_i, npn=3):
    """Ein Fiederblatt: npn Stationen, je 3 Vertices (links/mitte/rechts),
    Spitze als Faecher. Gibt die Dreiecksanzahl zurueck."""
    n = npn
    lay = bm.loops.layers.uv.active
    ts = [i / (n - 1.0) for i in range(n)]
    roll = rng.uniform(-0.12, 0.12)          # leichte Verdrillung

    def place(t, hw, side):
        arc = fold * Lp * 0.16 * math.sin(math.pi * t)   # Bogen aus der Ebene
        dro = -droop * Lp * (t ** 1.7)                   # Haengung zur Spitze
        base = p0 + d * (Lp * t)
        off = wdir * (side * hw) + N * (side * hw * 0.45 * fold)
        return bm.verts.new(base + off + Vector((0, 0, arc + dro))
                            + d * (roll * hw * side))

    rows = []
    for idx, t in enumerate(ts):
        # lanzettlich: am Ansatz breit, zur Spitze gleichmaessig verjuengt
        prof = max(0.06, 1.0 - 0.62 * t)
        if idx == 0 or idx == n - 1:
            lob_f = 1.0
        else:
            lob_f = 1.0 - lob_amp          # tiefer Einschnitt = Zacke
        hw = max(0.0016, Wp * prof * lob_f * rng.uniform(0.93, 1.07))
        rows.append((place(t, hw, -1.0), place(t, hw * 0.42, 0.0),
                     place(t, hw, 1.0)))
    tris = 0
    for k in range(n - 1):
        a, b = rows[k], rows[k + 1]
        ta, tb = k / (n - 1.0), (k + 1) / (n - 1.0)
        f1 = bm.faces.new((a[0], a[1], b[1], b[0]))
        f2 = bm.faces.new((a[1], a[2], b[2], b[1]))
        set_face_uv(f1, cell_i, ((ta, 0.0), (ta, 0.5), (tb, 0.5), (tb, 0.0)), lay)
        set_face_uv(f2, cell_i, ((ta, 0.5), (ta, 1.0), (tb, 1.0), (tb, 0.5)), lay)
        tris += 4
    tipv = bm.verts.new(p0 + d * (Lp * 1.14) + Vector((0, 0, -droop * Lp * 1.1)))
    last = rows[-1]
    for a, b in ((last[0], last[1]), (last[1], last[2])):
        f = bm.faces.new((a, b, tipv))
        for lp in f.loops:
            lp[lay].uv = uv_cell(cell_i, 1.0, 0.5)
        tris += 1
    return tris


# ---- generic tube along a polyline (Krumme, Rhizom) ----
def build_tube(bm, pts, widths, cell_i, sides=4):
    """Rohr entlang Punktliste pts, Dicke je Punkt widths. Gibt Tris zurueck."""
    lay = bm.loops.layers.uv.active
    n = len(pts)
    rings = []
    for k in range(n):
        p = Vector(pts[k])
        if k == 0:
            tg = (Vector(pts[1]) - p).normalized()
        elif k == n - 1:
            tg = (p - Vector(pts[k - 1])).normalized()
        else:
            tg = (Vector(pts[k + 1]) - Vector(pts[k - 1])).normalized()
        side = Vector((tg.z, 0.0, -tg.x))
        if side.length < 1e-4:
            side = Vector((1.0, 0.0, 0.0))
        side.normalize()
        up = tg.cross(side)
        ring = []
        for c in range(sides):
            a = math.radians(360.0 / sides * c) + 0.5 * k
            ring.append(bm.verts.new(p + (side * math.cos(a) + up * math.sin(a))
                                     * widths[k]))
        rings.append(ring)
    tris = 0
    for k in range(n - 1):
        a, b = rings[k], rings[k + 1]
        ta, tb = k / (n - 1.0), (k + 1) / (n - 1.0)
        for c in range(sides):
            f = bm.faces.new((a[c], a[(c + 1) % sides], b[(c + 1) % sides], b[c]))
            set_face_uv(f, cell_i, ((ta, c / sides), (ta, (c + 1) / sides),
                                    (tb, (c + 1) / sides), (tb, c / sides)), lay)
            tris += 2
    tip = bm.faces.new(tuple(reversed(rings[-1])))
    for lp in tip.loops:
        lp[lay].uv = uv_cell(cell_i, 1.0, 0.5)
    cap = bm.faces.new(rings[0])
    for lp in cap.loops:
        lp[lay].uv = uv_cell(cell_i, 0.0, 0.5)
    return tris + sides * 2 - 2


def fiddlehead_pts(h, rc, arc_deg, x0, y0, azim):
    """Stiel gerade hoch, oben Rolle (Krumme) in geneigter Ebene."""
    pts = []
    n1 = 5
    for k in range(n1):
        t = k / (n1 - 1.0)
        pts.append((x0, y0, h * 0.62 * t))
    top = Vector((x0, y0, h * 0.62))
    n2 = 5
    a0 = math.radians(azim)
    for k in range(1, n2):
        t = k / (n2 - 1.0)
        a = a0 + math.radians(arc_deg) * t
        r = rc * (1.0 - 0.45 * t)
        c = top + Vector((math.cos(a) * r, math.sin(a) * r,
                          -rc * 0.55 * math.sin(math.radians(arc_deg) * t)))
        pts.append(tuple(c))
    return pts


# ---- Wedel-Parameter: 5 bogige aussen, 3 jüngere aufrecht innen ----
# L = Wedellaenge, phi0/phi1 = Neigung unten/oben (Grad von der Senkrechten),
# azim = Himmelsrichtung, pairs = Fiederpaare, np = Stationszahl je Fieder.
FRONDS = [
    dict(L=1.16, phi0=18, phi1=62, wig=0.030, az=2,   s0=0.12, s1=0.95),
    dict(L=1.11, phi0=20, phi1=58, wig=-0.034, az=74,  s0=0.12, s1=0.95),
    dict(L=1.18, phi0=15, phi1=66, wig=0.026, az=146, s0=0.12, s1=0.95),
    dict(L=1.09, phi0=19, phi1=56, wig=-0.028, az=218, s0=0.12, s1=0.95),
    dict(L=1.14, phi0=17, phi1=60, wig=0.032, az=290, s0=0.12, s1=0.95),
    dict(L=0.90, phi0=8,  phi1=34, wig=0.020, az=40,  s0=0.15, s1=0.95),
    dict(L=0.82, phi0=5,  phi1=30, wig=-0.018, az=160, s0=0.15, s1=0.95),
    dict(L=0.95, phi0=7,  phi1=36, wig=0.022, az=262, s0=0.15, s1=0.95),
]
PAIRS = 25          # Fiederpaare je Wedel (Vorgabe)
NPIN = 2            # Stationszahl je Fieder (6 Dreiecke/Fieder -> Budget)


def pinna_len(u):
    """Laengste Fiedern im unteren Wedeldrittel, zur Spitze und zum Grund kurz."""
    return 0.150 * math.sin(math.pi * min(1.0, u ** 0.7)) ** 0.85 + 0.020


def build_frond(rng, p):
    """Baut einen Wedel (Spindel + 50 Fiedern) in einem eigenen bmesh."""
    bm = bmesh.new()
    bm.loops.layers.uv.new("UVMap")
    lay = bm.loops.layers.uv.active
    build_axis(bm, rng, p["L"], p["phi0"], p["phi1"], p["wig"],
               0.0085, 0.0022, 5, 3)
    N = Vector((1.0, 0.0, 0.0))
    for i in range(PAIRS):
        u = i / (PAIRS - 1.0)
        s = p["s0"] + (p["s1"] - p["s0"]) * u
        p0 = rachis_point(s, p["L"], p["phi0"], p["phi1"], p["wig"])
        tg = rachis_tangent(s, p["L"], p["phi0"], p["phi1"], p["wig"])
        side_v = N.cross(tg).normalized()
        if side_v.x < 0:
            side_v = -side_v
        band = 0 if u < 0.34 else (1 if u < 0.67 else 2)
        alt = 1.0 if i % 2 == 0 else 0.85     # versetzte Groessen = Saegezahn
        Lp = pinna_len(u) * alt * rng.uniform(0.90, 1.10)
        for sgn in (-1.0, 1.0):
            sd = side_v * sgn
            a = math.radians(32.0 + rng.uniform(-6.0, 12.0))
            d = (sd * math.cos(a) + tg * math.sin(a)).normalized()
            wdir = d.cross(N).normalized() * sgn
            perp = d.cross(wdir).normalized()
            tl = math.radians(rng.uniform(14.0, 38.0))
            wdir = (wdir * math.cos(tl) + perp * math.sin(tl)).normalized()
            build_pinna(bm, rng, p0, d, wdir, N, Lp, Lp * 0.14, 0.0,
                        0.90, rng.uniform(0.14, 0.22), band, npn=NPIN)
    return bm


def build_plant():
    """Baut die ganze Rosette als EIN Objekt 'Farn_Tropisch' (1 Slot)."""
    rng = random.Random(SEED)
    bm = bmesh.new()
    bm.loops.layers.uv.new("UVMap")
    tris = 0
    # Rhizom-Kegel am Fuss (Zell-Atlas 3), rundlich statt blockig
    tris += build_tube(bm, [(0.0, 0.0, -0.015), (0.0, 0.0, 0.045),
                            (0.0, 0.0, 0.10), (0.0, 0.0, 0.15)],
                       [0.046, 0.036, 0.026, 0.014], 3, sides=8)
    # zwei junge Krumme in der Mitte
    for (fx, fy, h, arc) in ((0.03, 0.02, 0.32, 300), (-0.035, -0.01, 0.26, 330)):
        pts = fiddlehead_pts(h, 0.062, arc, fx, fy, rng.uniform(0, 360))
        w = [0.0110 * (1.0 - 0.55 * k / (len(pts) - 1.0)) for k in range(len(pts))]
        tris += build_tube(bm, pts, w, 3, sides=4)
    # Wedel bauen, auf kleinen Richten setzen, um Z-Achse drehen, fuehren
    for f in FRONDS:
        fbm = build_frond(rng, f)
        me = bpy.data.meshes.new("tmp")
        fbm.to_mesh(me)
        fbm.free()
        az = f["az"] + rng.uniform(-9, 9)
        rot = __import__("mathutils").Matrix.Rotation(
            math.radians(az), 4, "Z")
        ring = __import__("mathutils").Vector(
            (0.030 * math.cos(math.radians(az)),
             0.030 * math.sin(math.radians(az)), -0.012))
        for v in me.vertices:
            v.co = rot @ v.co + ring
        bm.from_mesh(me)
        bpy.data.meshes.remove(me)
    me = bpy.data.meshes.new("Farn_Tropisch")
    bm.to_mesh(me)
    bm.free()
    me.calc_loop_triangles()
    ob = bpy.data.objects.new("Farn_Tropisch", me)
    bpy.context.scene.collection.objects.link(ob)
    mat = bpy.data.materials.get("MAT_FernTropisch") or \
        bpy.data.materials.new("MAT_FernTropisch")
    ob.data.materials.append(mat)
    bb = [ob.matrix_world @ __import__("mathutils").Vector(c) for c in ob.bound_box]
    xs = [v.x for v in bb]; ys = [v.y for v in bb]; zs = [v.z for v in bb]
    print(f"FIGUREN: Farn_Tropisch tris={len(me.loop_triangles)} "
          f"verts={len(me.vertices)} slots={len(ob.material_slots)} "
          f"hoehe={max(zs)-min(zs):.3f} spread_x={max(xs)-min(xs):.3f} "
          f"spread_y={max(ys)-min(ys):.3f} minz={min(zs):.3f}")
    return ob


# Beim Ausfuehren (exec in Blender) immer sofort bauen:
for _ob in list(bpy.data.objects):
    bpy.data.objects.remove(_ob, do_unlink=True)
build_plant()
