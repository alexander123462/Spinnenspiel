# MODELL6: Farn-Szatter-Patch-Instanzen = EINZELWEDDEL in 3 Varianten.
# Einzelwedel: ein Wedel (Stiel + Spindel in Slot 0, Fiederblaetter in Slot 1),
# Ursprung am Fusspunkt, Z nach oben, Groesse in Metern.
# UV-Atlas gemeinsam ueber alle Varianten (8 Spalten x 4 Zeilen = 32 Zellen),
# damit der Patch mit EINEM Atlas-Texturpaar auskommt (Draw Calls bleiben gleich).
#   Variante A: Zellen 0,1,2 = Fiedern (3 Groessenbande), 3 = Stiel/Spindel
#   Variante B: Zellen 4,5,6 = Fiedern, 7 = Stiel/Spindel
#   Variante C: Zellen 8,9,10 = Fiedern, 11 = Stiel/Spindel
import bpy, bmesh, math, random
from mathutils import Vector

SEED = 6134
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
    """Keule: Stiel erst aufrecht, dann bogt die Spindel nach vorn (+Y) um."""
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

def rachis_point(s, L, phi0_deg, phi1_deg, wig, rng=None):
    pts = _rachis_curve(L, phi0_deg, phi1_deg, wig)
    n = len(pts) - 1
    u = max(0.0, min(1.0, s)) * n
    i = min(n - 1, int(u))
    return pts[i].lerp(pts[i + 1], u - i)

def rachis_tangent(s, L, phi0_deg, phi1_deg, wig):
    a = rachis_point(max(0.0, s - 0.01), L, phi0_deg, phi1_deg, wig, None)
    b = rachis_point(min(1.0, s + 0.01), L, phi0_deg, phi1_deg, wig, None)
    return (b - a).normalized()

def rachis_w(s, w0, w1):
    """Dicke der Achse: unten am dicksten, nach oben schlank."""
    return w0 + (w1 - w0) * s

def build_axis(bm, rng, L, phi0, phi1, wig, w0, w1, nst, cell_i):
    """Stiel + Spindel als 5-seitiges Rohr mit Kappe unten. Slot 0."""
    lay = bm.loops.layers.uv.active
    rings = []
    for k in range(nst):
        s = k / (nst - 1)
        p = rachis_point(s, L, phi0, phi1, wig, rng)
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

# ---- Fiederblatt: flach zur Wedelachse, Mittelrippe hoch, Rand tief und gebuchtet ----
def build_pinna(bm, rng, p0, d, wdir, N, Lp, Wp, lob_amp, fold, droop, cell_i, npn=6):
    """Ein Fiederblatt: n Stations, je 3 Vertices (links/mitte/rechts), Spitze als Faecher.
    d = Achse der Fieder, wdir = Querrichtung der Spreite, N = Wedelebenen-Normale.
    Gibt die Dreiecksanzahl zurueck."""
    n = npn
    lay = bm.loops.layers.uv.active
    ts = [i / (n - 1.0) for i in range(n)]
    roll = rng.uniform(-0.12, 0.12)          # leichte Verdrillung
    def place(t, hw, side):
        arc = fold * Lp * 0.16 * math.sin(math.pi * t)      # Bogen aus der Ebene
        dro = -droop * Lp * (t ** 1.7)                      # Haengung zur Spitze
        base = p0 + d * (Lp * t)
        off = wdir * (side * hw) + N * (side * hw * 0.45 * fold)
        return bm.verts.new(base + off + Vector((0, 0, arc + dro)) + d * (roll * hw * side))
    rows = []
    for idx, t in enumerate(ts):
        # breit am Grund, zur Spitze dünn auslaufend
        prof = max(0.05, min(1.0, (t / 0.30) ** 0.5) * (1.0 - 0.94 * t))
        if idx in (0, n - 2, n - 1):
            lob_f = 1.0
        elif idx % 2 == 1:
            lob_f = 1.0 - lob_amp          # tiefer Einschnitt = eigener Lappen
        else:
            lob_f = 1.0 - 0.35 * lob_amp
        hw = max(0.0016, Wp * prof * lob_f * rng.uniform(0.93, 1.07))
        rows.append((place(t, hw, -1.0), place(t, hw * 0.42, 0.0), place(t, hw, 1.0)))
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

# ---- Varianten-Definition (Parameter je Einzelwedel) ----
# L = Wedellaenge, phi0/phi1 = Neigung unten/oben (Grad von der Senkrechten),
# pairs = Fiederpaare, s0/s1 = Bereich der Fiedern entlang der Spindel,
# lp(u) = Laenge der Fieder, wp = Breite als Anteil der Fiederlaenge,
# ang = Winkel Fieder zur Spindel (Grad), fold = Rippenfold, droop = Haengung.
VARIANTS = {
    # A: Damenfarn-Typ, bogig, Fiedern tief gelappt, langste in der Mitte
    "wedel_1": dict(cell0=0, L=0.56, phi0=6, phi1=55, wig=0.030, pairs=14,
                    s0=0.28, s1=0.97, ang=18.0, wp=0.24, lob=0.55, fold=0.55,
                    tilt=14, np=7, droop=0.14, nst=11, w0=0.0085, w1=0.0022,
                    lp=lambda u: 0.135 * math.sin(math.pi * min(1.0, u ** 0.8)) ** 0.85),
    # B: Buchenfarn-Typ, unterste Fiedern am langsten, steil anliegend
    "wedel_2": dict(cell0=4, L=0.58, phi0=8, phi1=46, wig=-0.035, pairs=15,
                    s0=0.24, s1=0.97, ang=34.0, wp=0.20, lob=0.45, fold=0.7,
                    tilt=10, np=7, droop=0.08, nst=11, w0=0.0080, w1=0.0020,
                    lp=lambda u: 0.040 + 0.126 * (1.0 - u) ** 1.2),
    # C: kurzer breiter Wedel, starke Lappen, steil aufrecht
    "wedel_3": dict(cell0=8, L=0.44, phi0=4, phi1=40, wig=0.022, pairs=12,
                    s0=0.32, s1=0.95, ang=24.0, wp=0.30, lob=0.60, fold=0.45,
                    tilt=18, np=7, droop=0.20, nst=10, w0=0.0095, w1=0.0026,
                    lp=lambda u: 0.118 * math.sin(math.pi * min(1.0, u ** 0.9)) ** 0.8),
}


def build_variant(name, seed_off=0):
    """Baut einen Einzelwedel als zwei Objekte: <name>_stem (Slot 0) und
    <name>_leaves (Slot 1). Gibt (stem_obj, leaf_obj, tris_stem, tris_leaf) zurueck."""
    p = VARIANTS[name]
    rng = random.Random(SEED + seed_off)
    global uvl
    bm_axis = bmesh.new()
    bm_leaf = bmesh.new()
    for bm in (bm_axis, bm_leaf):
        uvl = bm.loops.layers.uv.new("UVMap")
    build_axis(bm_axis, rng, p["L"], p["phi0"], p["phi1"], p["wig"],
               p["w0"], p["w1"], p["nst"], p["cell0"] + 3)
    N = Vector((1.0, 0.0, 0.0))
    tris_leaf = 0
    for i in range(p["pairs"]):
        u = i / (p["pairs"] - 1.0)
        s = p["s0"] + (p["s1"] - p["s0"]) * u
        p0 = rachis_point(s, p["L"], p["phi0"], p["phi1"], p["wig"])
        tg = rachis_tangent(s, p["L"], p["phi0"], p["phi1"], p["wig"])
        side_v = N.cross(tg).normalized()
        if side_v.x < 0:
            side_v = -side_v
        band = p["cell0"] + (0 if u < 0.34 else (1 if u < 0.67 else 2))
        Lp = p["lp"](u) * rng.uniform(0.88, 1.10)
        for sgn in (-1.0, 1.0):
            sd = side_v * sgn
            a = math.radians(p["ang"] + rng.uniform(-9.0, 9.0))
            d = (sd * math.cos(a) + tg * math.sin(a)).normalized()
            wdir = d.cross(N).normalized() * sgn
            perp = d.cross(wdir).normalized()
            tl = math.radians(p["tilt"] * rng.uniform(0.7, 1.3))
            wdir = (wdir * math.cos(tl) + perp * math.sin(tl)).normalized()
            tris_leaf += build_pinna(bm_leaf, rng, p0, d, wdir, N, Lp,
                                     Lp * p["wp"], p["lob"], p["fold"],
                                     p["droop"] * rng.uniform(0.85, 1.15), band,
                                     p.get("np", 6))
    meshes = {}
    for key, bm in (("stem", bm_axis), ("leaves", bm_leaf)):
        me = bpy.data.meshes.new(name + "_" + key)
        bm.to_mesh(me)
        bm.free()
        me.calc_loop_triangles()
        ob = bpy.data.objects.new(name + "_" + key, me)
        bpy.context.scene.collection.objects.link(ob)
        meshes[key] = (ob, len(me.loop_triangles))
    return meshes["stem"][0], meshes["leaves"][0], meshes["stem"][1], meshes["leaves"][1]
