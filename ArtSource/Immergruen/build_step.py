# MODELL4: Immergruen-Bodendecker (Vinca-Typ) mit Ranken, gegenstaendige
# ovale Blaetter, ~0.18 m hoch, UV-Atlas 8x4, ein Slot 'Immergruen_Blatt'.
import bpy, bmesh, math, random

SEED = 4417
COLS, ROWS = 8, 4      # UV-Atlas-Zellen (28 Blaetter + 3 Ranken + Puffer)
PAD = 0.03             # Zell-Padding, identisch zu textur_numpy.py
TS = [0.0, 0.08, 0.25, 0.45, 0.65, 0.85, 1.0]   # Stations je Blatt

def build_ts(n):
    """n gleichmaessige Stations-Werte 0..1 (fuer glattere Blattform)."""
    return [i / (n - 1) for i in range(n)]

def oval_w(t):
    """Ovale Blattform (Vinca): breit in der Mitte, kurze Spitze vorn."""
    return math.sin(math.pi * min(1.0, t ** 0.82)) ** 1.15

def add_leaf(bm, uvl, rng, L, W, tilt_deg, azim_deg, base_r, base_z, cell_i,
             base_xy=None, ts=None):
    """Ein Ovalblatt: lokal +Y Laengsachse, X Breite, Z Rippe/Woelbung.
    Danach aufrichten (X), drehen (Z), Wurzelversatz. UV beim Anlegen."""
    if ts is None:
        ts = TS
    col, row = cell_i % COLS, cell_i // COLS
    def cell(t, xn):
        return ((col + PAD + (1 - 2 * PAD) * xn) / COLS,
                (row + PAD + (1 - 2 * PAD) * t) / ROWS)
    wj = rng.uniform(0.85, 1.15)          # Breiten-Jitter
    arch = rng.uniform(0.010, 0.024)      # Bogen in m: steigt auf, Spitze neigt
    td = rng.uniform(0.004, 0.010)        # Spaetze neigt leicht nach unten
    roll = math.tan(math.radians(rng.uniform(-10.0, 10.0)))  # Rollen um Rippe
    wig = rng.uniform(-1.0, 1.0)          # Randwellung
    rows = []
    for idx, t in enumerate(ts):
        half = max(0.0012, oval_w(t) * W * wj)
        za = arch * math.sin(math.pi * t ** 0.7) - td * max(0.0, t - 0.75) * 3.0
        zc = za + W * 0.16 * math.sin(math.pi * min(1.0, t * 1.1))   # Rippe hoch
        ze = za - W * 0.10 * math.sin(math.pi * min(1.0, t * 1.05)) \
             + 0.004 * wig * math.sin(t * 17.0)                      # Rand tief
        rz = half * roll * t
        vl = bm.verts.new((-half, t * L, ze - rz))
        vc = bm.verts.new((0.0, t * L, zc))
        vr = bm.verts.new((half, t * L, ze + rz))
        rows.append((vl, vc, vr))
    pairs = list(zip(rows, rows[1:], range(len(rows) - 1)))
    for a, b, k in pairs:
        ta, tb = ts[k], ts[k + 1]
        f1 = bm.faces.new((a[0], a[1], b[1], b[0]))
        f2 = bm.faces.new((a[1], a[2], b[2], b[1]))
        for lp, (tt, xn) in zip(f1.loops, ((ta, 0.0), (ta, 0.5), (tb, 0.5), (tb, 0.0))):
            lp[uvl].uv = cell(tt, xn)
        for lp, (tt, xn) in zip(f2.loops, ((ta, 0.5), (ta, 1.0), (tb, 1.0), (tb, 0.5))):
            lp[uvl].uv = cell(tt, xn)
    tl = math.radians(tilt_deg); az = math.radians(azim_deg - 90.0)
    ct, st = math.cos(tl), math.sin(tl); ca, sa = math.cos(az), math.sin(az)
    if base_xy is not None:
        px, py = base_xy
    else:
        px = base_r * math.cos(math.radians(azim_deg))
        py = base_r * math.sin(math.radians(azim_deg))
    for rv in rows:
        for v in rv:
            y1 = v.co.y * ct - v.co.z * st
            z1 = v.co.y * st + v.co.z * ct
            x1 = v.co.x
            v.co.x = x1 * ca - y1 * sa + px
            v.co.y = x1 * sa + y1 * ca + py
            v.co.z = z1 + base_z

def add_runner(bm, uvl, rng, length, azim_deg, cell_i):
    """ Ranke: duennes Band am Boden entlang, leicht geschwungen. Rueckgrat
    der gegenstaendigen Blattpaare. UV in eigener Atlas-Zelle (Stielzone)."""
    col, row = cell_i % COLS, cell_i // COLS
    def cell(t, xn):
        return ((col + PAD + (1 - 2 * PAD) * xn) / COLS,
                (row + PAD + (1 - 2 * PAD) * t) / ROWS)
    nseg = 6
    amp = rng.uniform(0.02, 0.05)          # S-Kurve seitlich
    ph = rng.uniform(0.0, 6.28)
    az = math.radians(azim_deg)
    ca, sa = math.cos(az), math.sin(az)
    rows = []
    for k in range(nseg + 1):
        t = k / nseg
        x = t * length
        y = amp * math.sin(t * 3.2 + ph)   # sanfte Schwungkurve (lokal)
        hw = (0.0070 + 0.0020 * math.sin(t * 9.0 + ph)) * (1.0 - 0.55 * t)  # Spitze laeuft aus
        z = 0.004 + 0.002 * math.sin(t * 3.0 + ph)      # dicht am Boden
        vl = bm.verts.new((x * ca - (y - hw) * sa, x * sa + (y - hw) * ca, z - 0.003))
        vr = bm.verts.new((x * ca - (y + hw) * sa, x * sa + (y + hw) * ca, z + 0.003))
        rows.append((vl, vr))
    for k in range(nseg):
        a, b = rows[k], rows[k + 1]
        f = bm.faces.new((a[0], a[1], b[1], b[0]))
        ta, tb = k / nseg, (k + 1) / nseg
        for lp, (tt, xn) in zip(f.loops, ((ta, 0.0), (ta, 1.0), (tb, 1.0), (tb, 0.0))):
            lp[uvl].uv = cell(tt, xn)
    # Blattpaare an Knoten 1..3 und 5: gegenstaendig zur Rankenrichtung;
    # Knoten 5 nahe der Spitze -> kahles Ende nur noch ein kurzes Stueck.
    nodes = []
    for k in (1, 2, 3, 5):
        t = k / nseg
        x = t * length
        y = amp * math.sin(t * 3.2 + ph)
        nodes.append((x * ca - y * sa, x * sa + y * ca))   # Weltkoordinaten
    return azim_deg, nodes

def build():
    """Leert die Szene, baut den Bodendecker als ein Mesh 'Immergruen'."""
    for o in list(bpy.data.objects):
        bpy.data.objects.remove(o, do_unlink=True)
    rng = random.Random(SEED)
    me = bpy.data.meshes.new('Immergruen')
    bm = bmesh.new()
    uvl = bm.loops.layers.uv.new('UVMap')
    # 1) Ranken (Zellen 29..31) mit je vier gegenstaendigen Blattpaaren
    #    (Zellen 0..23). Rankenrichtung bestimmt die Blattachse.
    cell_leaf = 0
    for ri, azd in enumerate((35.0, 155.0, 268.0)):
        azj = azd + rng.uniform(-8.0, 8.0)
        length = rng.uniform(0.24, 0.30)
        azdeg, nodes = add_runner(bm, uvl, rng, length, azj, 29 + ri)
        for (nx, ny) in nodes:
            for sgn in (-1.0, 1.0):
                L = rng.uniform(0.075, 0.10)
                W = rng.uniform(0.038, 0.050)
                tilt = rng.uniform(15.0, 35.0)
                azm = azdeg + sgn * rng.uniform(75.0, 105.0)
                add_leaf(bm, uvl, rng, L, W, tilt, azm, 0.0,
                         rng.uniform(0.004, 0.008), cell_leaf, base_xy=(nx, ny),
                         ts=build_ts(8))
                cell_leaf += 1
    # 2) Mittlere Rosette in zwei Ebenen (Vinca: aussen alt ausgebreitet,
    #    innen junge aufrechte Triebe). Breite ovale Blaetter, 9 Stations.
    for i in range(3):
        L = rng.uniform(0.12, 0.15)
        W = rng.uniform(0.052, 0.062)
        tilt = rng.uniform(15.0, 32.0)
        az = i * 120.0 + rng.uniform(-9.0, 9.0)
        add_leaf(bm, uvl, rng, L, W, tilt, az, rng.uniform(0.020, 0.030),
                 rng.uniform(0.004, 0.009), 24 + i, ts=build_ts(9))
    for i in range(2):
        L = rng.uniform(0.15, 0.175)
        W = rng.uniform(0.052, 0.065)
        tilt = rng.uniform(55.0, 68.0)
        az = i * 180.0 + 30.0 + rng.uniform(-10.0, 10.0)
        add_leaf(bm, uvl, rng, L, W, tilt, az, rng.uniform(0.005, 0.012),
                 rng.uniform(0.006, 0.012), 27 + i, ts=build_ts(9))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    bm.to_mesh(me); bm.free()
    # Weiche Normalen: nimmt den facettierten Flat-Shading-Look von den
    # leicht gewoelbten Blattspreiten (Selbstkritik Durchlauf 2).
    for p in me.polygons:
        p.use_smooth = True
    ob = bpy.data.objects.new('Immergruen', me)
    bpy.context.scene.collection.objects.link(ob)
    # Rankenansatz minimal ins Erdreich absenken (kein Schwebe-Rand).
    for v in me.vertices:
        v.co.z -= 0.006
    tris = sum(len(p.vertices) - 2 for p in me.polygons)
    xs = [v.co.x for v in me.vertices]; ys = [v.co.y for v in me.vertices]
    zs = [v.co.z for v in me.vertices]
    print('BUILD tris', tris, 'verts', len(me.vertices), 'slots', len(me.materials))
    print('BUILD size X %.3f Y %.3f Z %.3f  zmin %.3f' %
          (max(xs)-min(xs), max(ys)-min(ys), max(zs)-min(zs), min(zs)))

def export_fbx():
    """Exportiert das aktive Immergruen-Mesh nach ArtSource/Immergruen."""
    import os
    ob = bpy.data.objects['Immergruen']
    out = 'C:/Users/vorau/dev/Spinnenspiel/ArtSource/Immergruen/immergruen_neu.fbx'
    os.makedirs(os.path.dirname(out), exist_ok=True)
    bpy.ops.object.select_all(action='DESELECT')
    ob.select_set(True); bpy.context.view_layer.objects.active = ob
    bpy.ops.export_scene.fbx(filepath=out, use_selection=True,
        apply_scale_options='FBX_SCALE_ALL', axis_forward='-Z', axis_up='Y',
        bake_space_transform=True, mesh_smooth_type='FACE',
        path_mode='STRIP', use_mesh_modifiers=True)
    print('EXPORT', out, os.path.getsize(out), 'bytes')

build()
