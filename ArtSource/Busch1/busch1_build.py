# MODELL8: Busch1 - Strauch-Neubau (shrub_02-Ersatz), 2 Slots (Aeste, Blaetter).
# Deterministisch (Seed 811). Ausfuehrbar in Blender 5.x und im Hintergrund (-b -P).
import bpy, bmesh, math, random
from mathutils import Vector, Matrix

SEED = 811

# --- Altes Asset restlos entfernen (keine .001-Namen) -----------------------
for ob in list(bpy.data.objects):
    if ob.name.startswith('Busch1'):
        bpy.data.objects.remove(ob, do_unlink=True)
for block_list in (bpy.data.meshes, bpy.data.materials):
    for x in list(block_list):
        if x.name.startswith('Busch1') or x.name.startswith('MAT_Busch1'):
            if x.users == 0:
                block_list.remove(x)

# --- Astgeruest als Polylinien: (punkte, radien pro punkt) -------------------
random.seed(SEED)

def make_branch(origin, direction, length, r0, r1, bend_axis, bend_amt, steps):
    pts, rads = [], []
    cur = Vector(direction).normalized()
    step = length / steps
    p = Vector(origin)
    for i in range(steps + 1):
        pts.append(p.copy())
        rads.append(r0 + (r1 - r0) * (i / steps))
        cur = (cur + Vector(bend_axis) * bend_amt * (0.3 + 0.7 * i / steps)).normalized()
        p = p + cur * step
    return (pts, rads)

def polyline_point(pts, t):
    """Punkt + Tangente auf der Polylinie bei t in 0..1 (Bogenlaenge)."""
    segs = [pts[i + 1] - pts[i] for i in range(len(pts) - 1)]
    total = sum(s.length for s in segs)
    target = t * total
    acc = 0.0
    for i, s in enumerate(segs):
        if acc + s.length >= target:
            f = (target - acc) / s.length
            return pts[i] + s * f, s.normalized()
        acc += s.length
    return pts[-1], segs[-1].normalized()

branches = []
n_stems = 5
for s in range(n_stems):
    az = math.radians(s * 360 / n_stems + random.uniform(-18, 18))
    tilt = math.radians(random.uniform(6, 16))
    d = Vector((math.sin(tilt) * math.cos(az), math.sin(tilt) * math.sin(az), math.cos(tilt)))
    outward = Vector((math.cos(az), math.sin(az), 0.0))
    L = random.uniform(0.80, 1.02)
    stem = make_branch((random.uniform(-0.03, 0.03), random.uniform(-0.03, 0.03), 0.0), d, L,
                       random.uniform(0.018, 0.023), 0.009, outward, random.uniform(0.12, 0.20), 7)
    branches.append(stem)
    for k in range(2):
        t = random.uniform(0.45, 0.8)
        idx = min(int(t * len(stem[0])) - 1, len(stem[0]) - 2)
        if idx < 0:
            idx = 0
        base = stem[0][idx]
        seg = (stem[0][idx + 1] - stem[0][idx]).normalized()
        side = seg.cross(Vector((0, 0, 1))).normalized() if abs(seg.z) < 0.99 else Vector((1, 0, 0))
        az2 = random.uniform(0, 2 * math.pi)
        side2 = Matrix.Rotation(az2, 3, seg) @ side
        d2 = (seg * 0.60 + side2.normalized() * 0.45 + Vector((0, 0, 0.35))).normalized()
        br = make_branch(base, d2, random.uniform(0.30, 0.45), 0.010, 0.005,
                         (d2.cross(seg)).normalized(), 0.20, 5)
        branches.append(br)

# --- Mesh fuer das Astgeruest (6 Profilecken, UV: U umlaufend, V laengs) ----
stem_me = bpy.data.meshes.new('Busch1_Aeste')
bm = bmesh.new()
uv_l = bm.loops.layers.uv.new('UVMap')
for pts, rads in branches:
    ring = []
    for i, p in enumerate(pts):
        seg = (pts[i + 1] - p) if i < len(pts) - 1 else (p - pts[i - 1])
        seg.normalize()
        up = Vector((0, 0, 1)) if abs(seg.z) < 0.9 else Vector((1, 0, 0))
        u_ax = seg.cross(up).normalized(); v_ax = seg.cross(u_ax).normalized()
        ring.append([bm.verts.new(p + (u_ax * math.cos(a) + v_ax * math.sin(a)) * rads[i])
                     for a in [j * math.tau / 6 for j in range(6)]])
    vlen = sum((pts[i + 1] - pts[i]).length for i in range(len(pts) - 1))
    acc = 0.0
    for i in range(len(ring) - 1):
        segl = (pts[i + 1] - pts[i]).length
        # V ins Band 0.02..0.30 (Rindenbereich der Textur; Blattzellen ab 0.35)
        v0 = 0.02 + 0.28 * acc / vlen; v1 = 0.02 + 0.28 * (acc + segl) / vlen
        acc += segl
        for j in range(6):
            f = bm.faces.new((ring[i][j], ring[i][(j + 1) % 6],
                              ring[i + 1][(j + 1) % 6], ring[i + 1][j]))
            us = [j / 6.0, (j + 1) / 6.0, (j + 1) / 6.0, j / 6.0]
            vs = [v0, v0, v1, v1]
            for k, lp in enumerate(f.loops):
                lp[uv_l].uv = (us[k], vs[k])
bm.normal_update()
bm.to_mesh(stem_me); bm.free()

# Wurzelanlauf: flacher Kegel am Ansatz (gehoert zu Slot 0 Aeste)
bm = bmesh.new()
fuv = bm.loops.layers.uv.new('UVMap')
seg_res = 8
base_r = 0.05
ringv = [bm.verts.new((base_r * math.cos(j * math.tau / seg_res),
                       base_r * math.sin(j * math.tau / seg_res), 0.0))
         for j in range(seg_res)]
tip = bm.verts.new((0.0, 0.0, 0.07))
for j in range(seg_res):
    f = bm.faces.new((ringv[j], ringv[(j + 1) % seg_res], tip))
    us = [j / seg_res, (j + 1) / seg_res, (j + 0.5) / seg_res]
    for k, lp in enumerate(f.loops):
        lp[fuv].uv = (us[k], 0.04 if k < 2 else 0.16)
bm.normal_update()
flare_me = bpy.data.meshes.new('Busch1_Anlauf')
bm.to_mesh(flare_me); bm.free()

# --- Blattgeometrie: 3 Atlaszellen (0 lanzettlich, 1 elliptisch, 2 eifoermig)
CELLS = [(0.02, 0.31), (0.35, 0.64), (0.68, 0.97)]  # u-Bereich je Atlaszelle
RATIO = [3.8, 3.0, 2.4]        # Laenge/Breite je Variante
WMAX = [0.42, 0.50, 0.55]      # max. halbe Breite / Laenge je Variante

def width_at(cell, v):
    """Halbe Blattbreite (Anteil der Laenge) bei v 0..1, max an 0.45."""
    w = math.sin(math.pi * min(max(v, 0.02), 0.98) ** 0.85)
    skew = [1.0, 1.0, 1.25][cell]  # eifoermig: breit zum Ansatz hin
    return w * WMAX[cell] * (1.0 - (1.0 - skew) * v)

def add_leaf(bm, uv_l, origin, direction, length, cell, roll):
    """Setzt ein Blatt: Mittelrippe als Knick (V), Voelbung laengs, gesaegter
    Rand bei Variante 0. UV: u ueber die Breite, v entlang der Mittelrippe."""
    d = Vector(direction).normalized()
    ref = Vector((0, 0, 1)) if abs(d.z) < 0.9 else Vector((1, 0, 0))
    w_ax = d.cross(ref).normalized()
    w_ax = Matrix.Rotation(roll, 3, d) @ w_ax
    up_ax = d.cross(w_ax).normalized()
    n_st, n_cr = 5, 4
    width = length / RATIO[cell]
    us0, us1 = CELLS[cell]
    verts = []
    for si in range(n_st):
        v = si / (n_st - 1)
        # Boegung: erst leicht hoch, dann abhaengend (durchgaengig abwaerts)
        arc = 0.12 * math.sin(math.pi * min(v * 0.7, 1.0)) - 0.34 * v * v
        hw = width_at(cell, v) * length
        serr = 1.0
        if cell == 0 and si > 0:
            serr = 0.90 if si % 2 else 1.06  # gesaegter Rand
        row = []
        for ci in range(n_cr):
            t = (ci - (n_cr - 1) / 2.0) / ((n_cr - 1) / 2.0)  # -1..1
            edge_lift = 0.24 * abs(t) * hw
            pos = (origin + d * (v * length) + w_ax * (t * hw * serr)
                   + up_ax * arc * length + up_ax * edge_lift)
            row.append(bm.verts.new(pos))
        verts.append(row)
    for si in range(n_st - 1):
        for ci in range(n_cr - 1):
            f = bm.faces.new((verts[si][ci], verts[si][ci + 1],
                              verts[si + 1][ci + 1], verts[si + 1][ci]))
            uu = [us0 + (ci / (n_cr - 1.0)) * (us1 - us0),
                  us0 + ((ci + 1) / (n_cr - 1.0)) * (us1 - us0),
                  us0 + ((ci + 1) / (n_cr - 1.0)) * (us1 - us0),
                  us0 + (ci / (n_cr - 1.0)) * (us1 - us0)]
            vv = [si / (n_st - 1.0), si / (n_st - 1.0),
                  (si + 1) / (n_st - 1.0), (si + 1) / (n_st - 1.0)]
            for k, lp in enumerate(f.loops):
                lp[uv_l].uv = (uu[k], vv[k])

# --- Blaetter entlang der Zweige verteilen ----------------------------------
leaf_me = bpy.data.meshes.new('Busch1_Blaetter')
bm = bmesh.new()
luv = bm.loops.layers.uv.new('UVMap')
n_leaves = 0
for pts, rads in branches:
    vlen = sum((pts[i + 1] - pts[i]).length for i in range(len(pts) - 1))
    n = max(7, int(vlen * 21))            # ~21 Blaetter pro Meter Zweig
    for li in range(n):
        t = 0.12 + 0.86 * (li + random.uniform(-0.35, 0.35)) / max(n - 1, 1)
        if t > 1.0:
            t = 1.0
        if t < 0.12:
            t = 0.12
        p, seg = polyline_point(pts, t)
        ref = Vector((0, 0, 1)) if abs(seg.z) < 0.9 else Vector((1, 0, 0))
        u_ax = seg.cross(ref).normalized(); v_ax = seg.cross(u_ax).normalized()
        az = li * 2.39996 + random.uniform(-0.25, 0.25)   # Goldwinkelspirale
        outward = (u_ax * math.cos(az) + v_ax * math.sin(az))
        tilt_up = (0.18 if t > 0.75 else -0.35) + random.uniform(-0.15, 0.15)
        ld = (outward * 1.0 + seg * 0.25 + Vector((0, 0, tilt_up))).normalized()
        origin = p + outward * rads[min(int(t * (len(pts) - 1)), len(rads) - 1)]
        length = random.uniform(0.07, 0.12) * (0.8 + 0.2 * t)
        cell = random.choice((0, 0, 1, 1, 2))
        add_leaf(bm, luv, origin, ld, length, cell, random.uniform(-0.9, 0.9))
        n_leaves += 1
bm.normal_update()
bm.to_mesh(leaf_me); bm.free()

# --- Objekte anlegen, zu 'Busch1' verbinden, Slots pruefen -------------------
mat_a = bpy.data.materials.new('MAT_Busch1_Aeste')
mat_b = bpy.data.materials.new('MAT_Busch1_Blaetter')
stem_me.materials.append(mat_a); flare_me.materials.append(mat_a)
leaf_me.materials.append(mat_b)
ob_stem = bpy.data.objects.new('Busch1_Aeste', stem_me)
ob_flare = bpy.data.objects.new('Busch1_Anlauf', flare_me)
ob_leaf = bpy.data.objects.new('Busch1_Blaetter', leaf_me)
for o in (ob_stem, ob_flare, ob_leaf):
    bpy.context.scene.collection.objects.link(o)
bpy.ops.object.select_all(action='DESELECT')
for o in (ob_stem, ob_flare, ob_leaf):
    o.select_set(True)
bpy.context.view_layer.objects.active = ob_stem
bpy.ops.object.join()
busch = bpy.context.view_layer.objects.active
busch.name = 'Busch1'; busch.data.name = 'Busch1'
# Ursprung: Fusspunkt (z-min auf 0), x/y in Mitte
bb = [busch.matrix_world @ Vector(c) for c in busch.bound_box]
zmin = min(v.z for v in bb)
xm = (min(v.x for v in bb) + max(v.x for v in bb)) / 2
ym = (min(v.y for v in bb) + max(v.y for v in bb)) / 2
bpy.context.scene.cursor.location = (xm, ym, zmin)
bpy.ops.object.origin_set(type='ORIGIN_CURSOR')
bb = [busch.matrix_world @ Vector(c) for c in busch.bound_box]
print('Busch1: Blaetter', n_leaves, 'tris', sum(len(p.vertices) - 2 for p in busch.data.polygons),
      'slots', [s.material.name if s.material else None for s in busch.material_slots])
print('Bounds x %.2f..%.2f y %.2f..%.2f z %.2f..%.2f' % (
    min(v.x for v in bb), max(v.x for v in bb), min(v.y for v in bb),
    max(v.y for v in bb), min(v.z for v in bb), max(v.z for v in bb)))

# Export wie Brennnessel/Immergruen: bake_space_transform=backt die Z-up->Y-up-
# Umrechnung in die Vertices. Ohne sie (nur FBX_SCALE_ALL) steckt die Drehung als
# Node-Rotation im FBX und Unity verschiebt den Pivot in die Mitte (Fuss dann bei
# y=-0.47 statt 0). Fusshoehe bleibt in lokalen Koordinaten am Ursprung.
_bus = bpy.data.objects.get('Busch1')
if _bus is not None:
    bpy.ops.object.select_all(action='DESELECT')
    _bus.select_set(True)
    bpy.context.view_layer.objects.active = _bus
    bpy.ops.export_scene.fbx(
        filepath=r'C:\Users\vorau\dev\Spinnenspiel\ArtSource\Busch1\busch1_neu.fbx',
        use_selection=True, apply_scale_options='FBX_SCALE_ALL',
        axis_forward='-Z', axis_up='Y', bake_space_transform=True,
        mesh_smooth_type='FACE', path_mode='STRIP', use_mesh_modifiers=True,
        add_leaf_bones=False, bake_anim=False)
    print('EXPORT busch1_neu.fbx fertig')
