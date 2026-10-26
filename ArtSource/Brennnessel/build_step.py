# MODELL5: Brennnessel (Urtica-Typ) fuer Unity: 0.5-1.0 m aufrecht, eckiger
# (4-kantiger) Stengel mit Knoten, kreuzgegenstaendige Blattpaare an
# Blattstielen, gezaeckte herzfoermige Blaetter mit Mittelrippe und Woelbung.
# Ein Mesh 'Brennnessel', ein Slot, UV-Atlas 8x4 (Zellen: 0..11 Blaetter,
# 12..23 Blattstiele, 24 Stengel). Ursprung am Fuss, Z nach oben.
import bpy, bmesh, math, random
from mathutils import Vector

SEED = 5521
COLS, ROWS = 8, 4
PAD = 0.03

def cell(bm_cell_i, t, xn):
    col, row = bm_cell_i % COLS, bm_cell_i // COLS
    return ((col + PAD + (1 - 2 * PAD) * xn) / COLS,
            (row + PAD + (1 - 2 * PAD) * t) / ROWS)

def set_uv(face, cell_i, pts):
    for lp, (tt, xn) in zip(face.loops, pts):
        lp[uvl].uv = cell(cell_i, tt, xn)

# Stengel-Mittellinie: leicht gebogen, Knoten bei t-Werten.
NODE_T = [0.24, 0.40, 0.56, 0.71, 0.86]
STEM_H = 0.72

def stem_xy(t):
    return (0.018 * math.sin(t * 2.1 + 0.4) * t,
            0.014 * math.sin(t * 2.7 + 1.9) * t)

def stem_w(t):
    # unten am dicksten, nach oben schlanker; Knoten leicht verdickt
    base = 0.0115 - 0.0060 * t
    bulge = sum(0.0022 * math.exp(-((t - nt) / 0.035) ** 2) for nt in NODE_T)
    return base + bulge

def build_stem(bm, rng):
    """4-kantiger Stengel: je Station ein Ring aus 4 Ecken, Zellen-24-UV."""
    nst = 11
    rings = []
    for k in range(nst):
        t = k / (nst - 1)
        cx, cy = stem_xy(t)
        w = stem_w(t) * rng.uniform(0.96, 1.04)
        rot = math.radians(12.0 * t + 8.0)          # Kanten leicht verdrehen
        pts = []
        for c in range(4):
            a = math.radians(45.0 + 90.0 * c) + rot
            pts.append(bm.verts.new((cx + w * math.sqrt(2) * math.cos(a),
                                     cy + w * math.sqrt(2) * math.sin(a),
                                     t * STEM_H)))
        rings.append(pts)
    for k in range(nst - 1):
        a, b = rings[k], rings[k + 1]
        ta, tb = k / (nst - 1), (k + 1) / (nst - 1)
        for c in range(4):
            f = bm.faces.new((a[c], a[(c + 1) % 4], b[(c + 1) % 4], b[c]))
            set_uv(f, 24, ((ta, c / 4), (ta, (c + 1) / 4),
                           (tb, (c + 1) / 4), (tb, c / 4)))
    cap = bm.faces.new(tuple(reversed(rings[0])))
    for lp in cap.loops:
        lp[uvl].uv = cell(24, 0.0, 0.5)
    return rings

def add_petiole(bm, rng, p0, direction, length, w0, cell_i):
    """Blattstiel: duennes Band vom Knoten zur Blattbasis, leicht gebogen."""
    nseg = 3
    dx, dy, dz = direction
    tilt_up = rng.uniform(18.0, 30.0)
    ct, st = math.cos(math.radians(tilt_up)), math.sin(math.radians(tilt_up))
    horiz = math.hypot(dx, dy)
    ux = dx / max(horiz, 1e-6); uy = dy / max(horiz, 1e-6)
    px_, py_ = -uy, ux                      # Querrichtung (horizontal)
    rows = []
    for k in range(nseg + 1):
        t = k / nseg
        h = length * t
        x = p0[0] + dx * h
        y = p0[1] + dy * h
        z = p0[2] + dz * h + st * h * 0.35
        hw = w0 * (1.0 - 0.45 * t)
        rows.append((bm.verts.new((x + px_ * hw, y + py_ * hw, z - 0.0015)),
                     bm.verts.new((x - px_ * hw, y - py_ * hw, z + 0.0015))))
    for k in range(nseg):
        a, b = rows[k], rows[k + 1]
        f = bm.faces.new((a[0], a[1], b[1], b[0]))
        ta, tb = k / nseg, (k + 1) / nseg
        set_uv(f, cell_i, ((ta, 0.0), (ta, 1.0), (tb, 1.0), (tb, 0.0)))
    tip = rows[-1]
    mid = ((tip[0].co + tip[1].co) / 2)
    axis = Vector((dx * ct, dy * ct, dz * ct + st * 0.35)).normalized()
    return (mid.x, mid.y, mid.z), axis

def cordate_w(t):
    """Herzfoermige Blattspreite: breite Herz-Ohrchen kurz hinter der Basis,
    Maximum unter der Mitte, kurze drawn-out Spitze."""
    base = math.sin(math.pi * min(1.0, t ** 0.72)) ** 1.08
    lobes = 1.0 + 0.35 * math.exp(-((t - 0.10) / 0.075) ** 2)  # Herz-Ohren
    notch = min(1.0, t / 0.045)                                # Bucht an der Anheftung
    return base * lobes * notch

def serr_amp(t):
    """Saegezaehne nur am Mittelrand (nicht an Basis/Spitze)."""
    return 0.20 * max(0.0, min(1.0, t / 0.15, (1.0 - t) / 0.12))

def add_leaf(bm, rng, L, W, tilt_deg, azim_deg, origin, cell_i):
    """Brennnesselblatt: 14 Stations, gezaeckter Rand (Stationen alternieren
    breit/schmal, zaehne zur Spitze geneigt), Herz-Basis nach hinten versetzt,
    Rippe hoch, Rand tief."""
    ts = [i / 13.0 for i in range(14)]
    wj = rng.uniform(0.88, 1.12)
    arch = rng.uniform(-0.010, 0.020)     # Bogen: steigt, Spitze neigt
    roll = math.tan(math.radians(rng.uniform(-14.0, 14.0)))
    wig = rng.uniform(-1.0, 1.0)
    rows = []
    for idx, t in enumerate(ts):
        out = (idx % 2 == 1)              # Zacke zeigt zur Spitze
        serr = 1.0 + serr_amp(t) * (1.0 if out else -1.0)
        half = max(0.0015, cordate_w(t) * W * wj * serr)
        tooth_f = L * 0.045 * serr_amp(t) if out else 0.0   # Zahn neigt nach vorn
        za = arch * math.sin(math.pi * t ** 0.8) - 0.045 * L * max(0.0, t - 0.72) * 2.8
        cup = 0.05 * W * math.sin(math.pi * min(1.0, t))  # Blattflaechig, Rand steigt
        zc = za + W * 0.10 * math.sin(math.pi * min(1.0, t * 1.05))
        ze = za - W * 0.05 * math.sin(math.pi * min(1.0, t)) + cup \
             + 0.006 * wig * math.sin(t * 19.0)
        rz = half * roll * t
        back = -0.11 * L * max(0.0, 1.0 - t / 0.16) ** 2   # Herz-Ohrchen rueckwaerts
        vl = bm.verts.new((-half, t * L + back + tooth_f, ze - rz))
        vc = bm.verts.new((0.0, t * L, zc))
        vr = bm.verts.new((half, t * L + back + tooth_f, ze + rz))
        rows.append((vl, vc, vr))
    for k in range(13):
        a, b = rows[k], rows[k + 1]
        ta, tb = ts[k], ts[k + 1]
        f1 = bm.faces.new((a[0], a[1], b[1], b[0]))
        f2 = bm.faces.new((a[1], a[2], b[2], b[1]))
        set_uv(f1, cell_i, ((ta, 0.0), (ta, 0.5), (tb, 0.5), (tb, 0.0)))
        set_uv(f2, cell_i, ((ta, 0.5), (ta, 1.0), (tb, 1.0), (tb, 0.5)))
    # Ausrichten: Blatt hebt von der Horizontalen ab, dann azimutal drehen,
    # schliesslich an die Blattstiel-Spitze setzen.
    tl = math.radians(tilt_deg); az = math.radians(azim_deg - 90.0)
    ct, st = math.cos(tl), math.sin(tl); ca, sa = math.cos(az), math.sin(az)
    ox, oy, oz = origin
    for rv in rows:
        for v in rv:
            y1 = v.co.y * ct - v.co.z * st
            z1 = v.co.y * st + v.co.z * ct
            x1 = v.co.x
            v.co.x = x1 * ca - y1 * sa + ox
            v.co.y = x1 * sa + y1 * ca + oy
            v.co.z = z1 + oz

def build():
    """Leert die Szene, baut die Brennnessel als ein Mesh 'Brennnessel'."""
    global uvl
    for o in list(bpy.data.objects):
        bpy.data.objects.remove(o, do_unlink=True)
    rng = random.Random(SEED)
    me = bpy.data.meshes.new('Brennnessel')
    bm = bmesh.new()
    uvl = bm.loops.layers.uv.new('UVMap')
    rings = build_stem(bm, rng)
    cell_leaf, cell_pet = 0, 12
    for k, nt in enumerate(NODE_T):
        t = nt
        cx, cy = stem_xy(t)
        node = Vector((cx, cy, t * STEM_H))
        az_base = 40.0 + k * 90.0 + rng.uniform(-7.0, 7.0)   # kreuzgegenstaendig
        for sgn in (-1.0, 1.0):
            azm = az_base + (180.0 if sgn > 0 else 0.0)
            d = Vector((math.cos(math.radians(azm)),
                        math.sin(math.radians(azm)), 0.0))
            plen = rng.uniform(0.065, 0.105) * (1.15 - 0.4 * t)
            tip, _ = add_petiole(bm, rng, (node.x, node.y, node.z),
                                 (d.x, d.y, 0.0), plen, 0.006, cell_pet)
            cell_pet += 1
            L = rng.uniform(0.105, 0.155) * (1.2 - 0.45 * t)
            W = L * rng.uniform(0.64, 0.78)
            tilt = rng.uniform(22.0, 48.0) + 18.0 * t   # obere Blaetter aufrechter
            add_leaf(bm, rng, L, W, tilt, azm, tip, cell_leaf)
            cell_leaf += 1
    # Endpaar: kleine aufrechte Blaettchen an der wachsenden Spitze
    tcx, tcy = stem_xy(0.93)
    for azm in (35.0, 215.0):
        L = rng.uniform(0.060, 0.075)
        W = L * rng.uniform(0.64, 0.75)
        add_leaf(bm, rng, L, W, rng.uniform(58.0, 72.0),
                 azm + rng.uniform(-6.0, 6.0), (tcx, tcy, STEM_H * 0.93), cell_leaf)
        cell_leaf += 1
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    bm.to_mesh(me); bm.free()
    for p in me.polygons:
        p.use_smooth = True
    ob = bpy.data.objects.new('Brennnessel', me)
    bpy.context.scene.collection.objects.link(ob)
    tris = sum(len(p.vertices) - 2 for p in me.polygons)
    xs = [v.co.x for v in me.vertices]; ys = [v.co.y for v in me.vertices]
    zs = [v.co.z for v in me.vertices]
    print('BUILD tris', tris, 'verts', len(me.vertices), 'slots', len(me.materials))
    print('BUILD size X %.3f Y %.3f Z %.3f  zmin %.3f' %
          (max(xs)-min(xs), max(ys)-min(ys), max(zs)-min(zs), min(zs)))

def export_fbx():
    """Exportiert das Brennnessel-Mesh nach ArtSource/Brennnessel."""
    import os
    ob = bpy.data.objects['Brennnessel']
    out = 'C:/Users/vorau/dev/Spinnenspiel/ArtSource/Brennnessel/brennnessel_neu.fbx'
    os.makedirs(os.path.dirname(out), exist_ok=True)
    bpy.ops.object.select_all(action='DESELECT')
    ob.select_set(True); bpy.context.view_layer.objects.active = ob
    bpy.ops.export_scene.fbx(filepath=out, use_selection=True,
        apply_scale_options='FBX_SCALE_ALL', axis_forward='-Z', axis_up='Y',
        bake_space_transform=True, mesh_smooth_type='FACE',
        path_mode='STRIP', use_mesh_modifiers=True)
    print('EXPORT', out, os.path.getsize(out), 'bytes')

build()
