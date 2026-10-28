# MODELL18: Blume (Bluete mit 5 Kronblaettern + Staubblaettern, Stengel mit
# lanzenfoermigen Blaettern, 3 Stengel je Busch, UV-Atlas 8x5, ein Slot 'Blume').
# Rosa-Variante (Immergruen/Vinca-Typ) und gelb/orange Variante nutzen dasselbe
# Mesh; die Farbe kommt aus zwei Albedo-Karten (textur_numpy.py).
import bpy, bmesh, math, random

SEED = 7310
COLS, ROWS = 8, 5      # UV-Atlas-Zellen: 0..2 Stengel, 3..14 Blaetter,
PAD = 0.03             # 15..29 Kronblaetter, 30..32 Bluetenmitte, Rest Puffer
TS_L = [i / 6.0 for i in range(7)]    # 7 Stations je Blatt
TS_P = [i / 4.0 for i in range(5)]    # 5 Stations je Kronblatt


def cell_of(cell_i):
    return cell_i % COLS, cell_i // COLS


def uv_cell(cell_i, t, xn):
    """UV in Atlas-Zelle: t laengsseits 0..1, xn quer 0..1, mit PAD."""
    col, row = cell_of(cell_i)
    return ((col + PAD + (1 - 2 * PAD) * xn) / COLS,
            (row + PAD + (1 - 2 * PAD) * t) / ROWS)


def place(v, tilt, azim, px, py, pz):
    """Lokales +Y-Laengsblatt: neigen um X, drehen um Z, Verschieben."""
    ct, st = math.cos(tilt), math.sin(tilt)
    ca, sa = math.cos(azim), math.sin(azim)
    y1 = v.co.y * ct - v.co.z * st
    z1 = v.co.y * st + v.co.z * ct
    x1 = v.co.x
    v.co.x = x1 * ca - y1 * sa + px
    v.co.y = x1 * sa + y1 * ca + py
    v.co.z = z1 + pz


def add_leaf(bm, uvl, rng, L, W, tilt, azim, px, py, pz, cell_i):
    """Lanzenfoermiges Blatt (Vinca-schlank): Rippe hoch, Rand leicht gewellt."""
    wj = rng.uniform(0.85, 1.15)
    arch = rng.uniform(0.006, 0.014)          # Bogen: Spitze hebt leicht an
    roll = math.tan(math.radians(rng.uniform(-14.0, 14.0)))
    wig = rng.uniform(-1.0, 1.0)
    rows = []
    for t in TS_L:
        half = max(0.0010, W * wj * math.sin(math.pi * min(1.0, t ** 0.75)) ** 1.2)
        za = arch * math.sin(math.pi * t ** 0.8)
        zc = za + W * 0.35 * math.sin(math.pi * min(1.0, t * 1.05))   # Rippe
        ze = za - W * 0.22 * math.sin(math.pi * t) \
             + 0.004 * wig * math.sin(t * 15.0)                       # Rand
        rz = half * roll * t
        rows.append((bm.verts.new((-half, t * L, ze - rz)),
                     bm.verts.new((0.0, t * L, zc)),
                     bm.verts.new((half, t * L, ze + rz))))
    for k in range(len(TS_L) - 1):
        a, b = rows[k], rows[k + 1]
        ta, tb = TS_L[k], TS_L[k + 1]
        f1 = bm.faces.new((a[0], a[1], b[1], b[0]))
        f2 = bm.faces.new((a[1], a[2], b[2], b[1]))
        for lp, (tt, xn) in zip(f1.loops, ((ta, 0.0), (ta, 0.5), (tb, 0.5), (tb, 0.0))):
            lp[uvl].uv = uv_cell(cell_i, tt, xn)
        for lp, (tt, xn) in zip(f2.loops, ((ta, 0.5), (ta, 1.0), (tb, 1.0), (tb, 0.5))):
            lp[uvl].uv = uv_cell(cell_i, tt, xn)


def add_stem(bm, uvl, rng, length, lean, azim, cell_i):
    """Vierkantiger, leicht gebogener Stengel (Rohr) von der Basis aufrecht.
    Rueckgabe: Spitze (x,y,z) und Knoten (t=0.35,0.6) als Weltkoordinaten."""
    ca, sa = math.cos(azim), math.sin(azim)
    cl, sl = math.cos(lean), math.sin(lean)
    bend = rng.uniform(0.10, 0.22)          # Zusatzneigung zur Spitze hin
    ph = rng.uniform(0.0, 6.28)
    nseg = 6
    tip = None
    nodes = []
    prev = None
    for k in range(nseg + 1):
        t = k / nseg
        ln = lean + bend * t * t            # wachsende Neigung
        h = length * t
        y = math.sin(ln) * h
        z = math.cos(ln) * h
        x = 0.006 * math.sin(t * 4.5 + ph)  # seitliche S-Kurve (lokal)
        w = 0.0040 - 0.0018 * t             # halb nach oben duenner
        rows = []
        for sx, sy in ((-1, 0), (1, 0), (0, -1), (0, 1)):
            lx = x + sx * w
            ly = y + sy * w
            lz = z
            v = bm.verts.new((lx * ca - ly * sa, lx * sa + ly * ca, lz))
            rows.append(v)
        if prev is not None:
            for j in range(4):
                a, b = prev[j], prev[(j + 1) % 4]
                c, d = rows[(j + 1) % 4], rows[j]
                f = bm.faces.new((a, b, c, d))
                ta, tb = k / nseg, (k + 1) / nseg
                xn0, xn1 = j / 4.0, (j + 1) / 4.0
                for lp, (tt, xn) in zip(f.loops, ((ta, xn0), (ta, xn1), (tb, xn1), (tb, xn0))):
                    lp[uvl].uv = uv_cell(cell_i, tt, xn)
        prev = rows
        wx = x * ca - y * sa
        wy = x * sa + y * ca
        if k in (1, 2, 4):                  # Knoten t~1/6,1/3,2/3 fuer Blaetter
            nodes.append((wx, wy, z))
        tip = (wx, wy, z)
    return tip, nodes


def add_flower(bm, uvl, rng, tip, spin0, cell_p, cell_m):
    """Bluete: 5 Kronblaetter (Vinca-Windrad), Fuelltrichter mit Augenscheibe,
    5 Staubblaetter. tip = Welt-Position am Stengelende."""
    px, py, pz = tip
    up = math.radians(rng.uniform(8.0, 20.0))     # Kronblaetter heben an
    spin = spin0 + rng.uniform(-10.0, 10.0)
    for k in range(5):
        az = math.radians(spin + k * 72.0 + rng.uniform(-6.0, 6.0))
        L = rng.uniform(0.052, 0.062)
        Wp = rng.uniform(0.037, 0.045)                       # fast geschlossener Teller
        shear = rng.uniform(0.25, 0.45) * Wp      # Windrad: seitlich verschoben
        twist = math.tan(math.radians(rng.uniform(6.0, 12.0)))
        rows = []
        for t in TS_P:
            half = max(0.0040, Wp * math.sin(math.pi * min(1.0, t ** 0.8)) ** 1.1)
            xc = shear * t
            zc = 0.009 * math.sin(math.pi * min(1.0, t * 0.92))
            droop = -0.016 * max(0.0, t - 0.72)   # Spitze haengt leicht
            ze = zc - Wp * 0.10 * math.sin(math.pi * t) + droop   # flacher, weicher
            rz = twist * t * half                 # Drillung
            rows.append((bm.verts.new((xc - half, t * L, ze - rz)),
                         bm.verts.new((xc, t * L, zc + droop)),
                         bm.verts.new((xc + half, t * L, ze + rz))))
        for i in range(len(TS_P) - 1):
            a, b = rows[i], rows[i + 1]
            ta, tb = TS_P[i], TS_P[i + 1]
            f1 = bm.faces.new((a[0], a[1], b[1], b[0]))
            f2 = bm.faces.new((a[1], a[2], b[2], b[1]))
            for lp, (tt, xn) in zip(f1.loops, ((ta, 0.0), (ta, 0.5), (tb, 0.5), (tb, 0.0))):
                lp[uvl].uv = uv_cell(cell_p + k, tt, xn)
            for lp, (tt, xn) in zip(f2.loops, ((ta, 0.5), (ta, 1.0), (tb, 1.0), (tb, 0.5))):
                lp[uvl].uv = uv_cell(cell_p + k, tt, xn)
        for rv in rows:
            for v in rv:
                place(v, up, az, px, py, pz)
    # Bluetenmitte: Fuelltrichter (5 Ecken) nach unten + Augenscheibe.
    frame = math.radians(spin)
    top, bot = [], []
    for j in range(5):
        a = frame + math.radians(j * 72.0)
        vt = bm.verts.new((0.016 * math.cos(a), 0.016 * math.sin(a), 0.004))
        vb = bm.verts.new((0.006 * math.cos(a), 0.006 * math.sin(a), -0.013))
        top.append(vt); bot.append(vb)
    for j in range(5):
        f = bm.faces.new((top[j], top[(j + 1) % 5], bot[(j + 1) % 5], bot[j]))
        xn0, xn1 = j / 5.0, (j + 1) / 5.0
        for lp, (tt, xn) in zip(f.loops, ((0.0, xn0), (0.0, xn1), (0.45, xn1), (0.45, xn0))):
            lp[uvl].uv = uv_cell(cell_m, tt, xn)
    vc = bm.verts.new((0.0, 0.0, -0.012))
    for j in range(5):
        f = bm.faces.new((vc, bot[j], bot[(j + 1) % 5]))
        for lp, (tt, xn) in zip(f.loops, ((0.46, j / 5.0), (0.54, j / 5.0),
                                          (0.54, (j + 1) / 5.0))):
            lp[uvl].uv = uv_cell(cell_m, tt, xn)
    vk = bm.verts.new((0.0, 0.0, 0.004))   # Kap-Scheibe: Auge von oben sichtbar
    for j in range(5):
        f = bm.faces.new((vk, top[(j + 1) % 5], top[j]))
        a0 = frame + math.radians((j + 1) * 72)
        a1 = frame + math.radians(j * 72)
        pts = ((0.5, 0.68),
               (0.62 + 0.12 * math.cos(a0), 0.60 + 0.12 * math.sin(a0)),
               (0.62 + 0.12 * math.cos(a1), 0.60 + 0.12 * math.sin(a1)))
        for lp, (tt, xn) in zip(f.loops, ((pts[0][1], pts[0][0]), (pts[1][1], pts[1][0]),
                                          (pts[2][1], pts[2][0]))):
            lp[uvl].uv = uv_cell(cell_m, tt, xn)
    for v in top + bot + [vc, vk]:
        place(v, up, frame, px, py, pz)
    # Staubblaetter: 5 duenne Bander, dichtes Buendel ueber der Mitte.
    for j in range(5):
        a = frame + math.radians(j * 72.0 + 36.0)
        ca, sa = math.cos(a), math.sin(a)
        p1 = bm.verts.new((0.010 * ca - 0.0010 * sa, 0.010 * sa + 0.0010 * ca, 0.002))
        p2 = bm.verts.new((0.010 * ca + 0.0010 * sa, 0.010 * sa - 0.0010 * ca, 0.002))
        p3 = bm.verts.new((0.0035 * ca + 0.0008 * sa, 0.0035 * sa - 0.0008 * ca, 0.013))
        p4 = bm.verts.new((0.0035 * ca - 0.0008 * sa, 0.0035 * sa + 0.0008 * ca, 0.013))
        f = bm.faces.new((p1, p2, p3, p4))
        xn0, xn1 = j / 5.0, (j + 0.7) / 5.0
        for lp, (tt, xn) in zip(f.loops, ((0.82, xn0), (0.82, xn1), (1.0, xn1), (1.0, xn0))):
            lp[uvl].uv = uv_cell(cell_m, tt, xn)
        for v in (p1, p2, p3, p4):
            place(v, up, frame, px, py, pz)


def build():
    """Leert die Szene, baut die Blume als ein Mesh 'Blume' (ein Slot)."""
    for o in list(bpy.data.objects):
        bpy.data.objects.remove(o, do_unlink=True)
    rng = random.Random(SEED)
    me = bpy.data.meshes.new('Blume')
    bm = bmesh.new()
    uvl = bm.loops.layers.uv.new('UVMap')
    cell_leaf = 3
    for si, azd in enumerate((25.0, 148.0, 266.0)):
        azim = math.radians(azd + rng.uniform(-7.0, 7.0))
        length = rng.uniform(0.205, 0.265)
        lean = math.radians(rng.uniform(6.0, 13.0))
        tip, nodes = add_stem(bm, uvl, rng, length, lean, azim, si)
        for ni, (nx, ny, nz) in enumerate(nodes):
            if ni == 0:                     # unten: ein kleines hanges Blatt
                sgn_list = (1.0 if si % 2 == 0 else -1.0,)
            else:                           # oben: gegenstaendige Blattpaare
                sgn_list = (-1.0, 1.0)
            for sgn in sgn_list:
                if ni == 0:
                    L = rng.uniform(0.045, 0.062)
                    W = rng.uniform(0.014, 0.019)
                    tilt = math.radians(rng.uniform(-38.0, -25.0))
                else:
                    L = rng.uniform(0.060, 0.085)
                    W = rng.uniform(0.016, 0.023)
                    tilt = math.radians(rng.uniform(-24.0, -6.0))
                azm = azim + sgn * math.radians(rng.uniform(78.0, 100.0))
                add_leaf(bm, uvl, rng, L, W, tilt, azm, nx, ny,
                         nz + rng.uniform(-0.002, 0.004), cell_leaf)
                cell_leaf += 1
        add_flower(bm, uvl, rng, tip, azd + 90.0, 18 + si * 5, 33 + si)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    bm.to_mesh(me); bm.free()
    for p in me.polygons:
        p.use_smooth = True
    ob = bpy.data.objects.new('Blume', me)
    bpy.context.scene.collection.objects.link(ob)
    for v in me.vertices:                           # Fuss minimal ins Erdreich
        v.co.z -= 0.004
    tris = sum(len(p.vertices) - 2 for p in me.polygons)
    xs = [v.co.x for v in me.vertices]; ys = [v.co.y for v in me.vertices]
    zs = [v.co.z for v in me.vertices]
    print('BUILD tris', tris, 'verts', len(me.vertices), 'slots', len(me.materials))
    print('BUILD size X %.3f Y %.3f Z %.3f  zmin %.3f' %
          (max(xs) - min(xs), max(ys) - min(ys), max(zs) - min(zs), min(zs)))


def export_fbx():
    """Exportiert das Blume-Mesh nach ArtSource/Blume."""
    import os
    ob = bpy.data.objects['Blume']
    out = 'C:/Users/vorau/dev/Spinnenspiel/ArtSource/Blume/blume_neu.fbx'
    os.makedirs(os.path.dirname(out), exist_ok=True)
    bpy.ops.object.select_all(action='DESELECT')
    ob.select_set(True); bpy.context.view_layer.objects.active = ob
    bpy.ops.export_scene.fbx(filepath=out, use_selection=True,
        apply_scale_options='FBX_SCALE_ALL', axis_forward='-Z', axis_up='Y',
        bake_space_transform=True, mesh_smooth_type='FACE',
        path_mode='STRIP', use_mesh_modifiers=True)
    print('EXPORT', out, os.path.getsize(out), 'bytes')


build()
