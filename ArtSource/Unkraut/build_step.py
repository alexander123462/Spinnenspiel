# MODELL3: Unkraut-Rosette (Loewenzahn-/Ampfer-Typ), 12 Blaetter mit Mittelrippe.
# Blattform als echte Geometrie: ueber Stationen alternierend Zahnspitze/Bucht.
import bpy, bmesh, math, random

SEED = 3311
N_LEAVES = 12
# (t, halbe Breite als Anteil der Blattlaenge); Zacken: Spitze-Bucht-Spitze...
STATIONS = [(0.00,0.006),(0.06,0.010),(0.10,0.045),(0.16,0.020),(0.24,0.085),
            (0.32,0.035),(0.42,0.115),(0.50,0.050),(0.60,0.125),(0.68,0.060),
            (0.78,0.120),(0.86,0.070),(0.93,0.135),(1.00,0.004)]

def add_leaf(bm, uvl, rng, length, tilt_deg, azim_deg, base_r, base_z, leaf_i):
    """Ein Blatt: lokal +Y Laengsachse, X Breite, Z oben (Rippe/Woelbung).
    Danach aufrichten (um X), nach Azimut drehen (um Z), Wurzelversatz.
    UV je Flaeche direkt beim Anlegen (4x3-Atlas, v entlang Mittelrippe)."""
    wj = rng.uniform(0.85, 1.15)          # Breiten-Jitter je Blatt
    wig = rng.uniform(-1.0, 1.0)          # Randwellung je Blatt
    arch = rng.uniform(0.05, 0.08)        # Bogen: Blatt steigt auf, Spitze neigt
    roll = math.tan(math.radians(rng.uniform(-12.0, 12.0)))  # Rollen um Mittelrippe
    td = rng.uniform(0.02, 0.045)         # Spaetze neigt leicht nach unten
    rows = []
    for idx, (t, w) in enumerate(STATIONS):
        # Zahnschaerfe: Kontrast Spitze/Bucht erhoehen, dann verbreitern
        wn = (w / 0.125) ** 1.3 * 0.125 * 1.5
        half = wn * wj * length
        # Loewenzahn: seitliche Zacken nach hinten (Endlappen zeigt nach vorn)
        if 2 <= idx < 12 and idx % 2 == 0:
            yj = -0.030 * length          # Zackenspitze zeigt zur Wurzel
        elif 3 <= idx <= 11:
            yj = 0.012 * length           # Bucht liegt weiter vorn
        else:
            yj = 0.0
        za = arch * math.sin(math.pi * t ** 0.75) - td * max(0.0, t - 0.8) * 2.5
        zc = za + length * 0.055 * (1.0 - t) ** 1.3        # Mittelrippe hoch
        ze = za - length * 0.040 * math.sin(math.pi * min(1.0, t * 1.05)) \
             + 0.006 * wig * math.sin(t * 19.0)            # Rand tiefer, gewellt
        y = t * length + yj
        # Mittelrippe bleibt gerade: Rueckwaerts-Versatz nur an den Randzacken
        rz = half * roll * t              # ein Rand hoch, der andere tief
        vl = bm.verts.new((-half, y, ze - rz))
        vc = bm.verts.new((0.0, t * length, zc))
        vr = bm.verts.new((half, y, ze + rz))
        rows.append((vl, vc, vr))
    col, row = leaf_i % 4, leaf_i // 4

    def cell(t, xn):
        return ((col + 0.03 + 0.94 * xn) / 4.0, (row + 0.03 + 0.94 * t) / 3.0)

    pairs = list(zip(rows, rows[1:], range(len(rows) - 1)))
    for a, b, k in pairs:
        ta = STATIONS[k][0]; tb = STATIONS[k + 1][0]
        f1 = bm.faces.new((a[0], a[1], b[1], b[0]))
        f2 = bm.faces.new((a[1], a[2], b[2], b[1]))
        # Loop-Reihenfolge folgt demface-Zyklus: direkt zuordenbar
        for lp, (tt, xn) in zip(f1.loops, ((ta, 0.0), (ta, 0.5), (tb, 0.5), (tb, 0.0))):
            lp[uvl].uv = cell(tt, xn)
        for lp, (tt, xn) in zip(f2.loops, ((ta, 0.5), (ta, 1.0), (tb, 1.0), (tb, 0.5))):
            lp[uvl].uv = cell(tt, xn)
    tl = math.radians(tilt_deg); az = math.radians(azim_deg - 90.0)
    ct, st = math.cos(tl), math.sin(tl)
    ca, sa = math.cos(az), math.sin(az)
    for row_v in rows:
        for v in row_v:
            y1 = v.co.y * ct - v.co.z * st
            z1 = v.co.y * st + v.co.z * ct
            x1 = v.co.x
            v.co.x = x1 * ca - y1 * sa + base_r * math.cos(math.radians(azim_deg))
            v.co.y = x1 * sa + y1 * ca + base_r * math.sin(math.radians(azim_deg))
            v.co.z = z1 + base_z

def build():
    """Leert die Szene, baut die Rosette als ein Mesh 'Unkraut', druckt Masse."""
    for o in list(bpy.data.objects):
        bpy.data.objects.remove(o, do_unlink=True)
    rng = random.Random(SEED)
    me = bpy.data.meshes.new('Unkraut')
    bm = bmesh.new()
    uvl = bm.loops.layers.uv.new('UVMap')
    for i in range(N_LEAVES):
        outer = (i % 2 == 0)
        # Altersgradient: innen kleine junge, aussen grosse alte Blaetter.
        # Neigungen sind vor der Z-Streckung gemeint (Streckung richtet auf).
        L = rng.uniform(0.36, 0.42) if outer else rng.uniform(0.22, 0.28)
        tilt = rng.uniform(8.0, 15.0) if outer else rng.uniform(30.0, 40.0)
        az = i * 360.0 / N_LEAVES + rng.uniform(-5.0, 5.0)
        br = 0.035 if outer else 0.020
        bz = rng.uniform(0.003, 0.010) if outer else rng.uniform(0.008, 0.016)
        add_leaf(bm, uvl, rng, L, tilt, az, br, bz, i)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    bm.to_mesh(me); bm.free()
    ob = bpy.data.objects.new('Unkraut', me)
    bpy.context.scene.collection.objects.link(ob)
    # Endskala: Rosette 0.49 m Duchmesser, ~0.41 m hoch (Vorgabe MODELL3;
    # der Bodendecker-Spawner skaliert nach Hoehe, darum aufrecht gebaut).
    ob.scale = (0.6, 0.6, 2.6)
    bpy.ops.object.select_all(action='DESELECT')
    ob.select_set(True); bpy.context.view_layer.objects.active = ob
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    # Kronenansatz 2 cm ins Erdreich absenken (wie Gras2): keine Schwebe-Luecke.
    for v in me.vertices:
        v.co.z -= 0.02
    tris = sum(len(p.vertices) - 2 for p in me.polygons)
    xs = [v.co.x for v in me.vertices]; ys = [v.co.y for v in me.vertices]
    zs = [v.co.z for v in me.vertices]
    print('BUILD tris', tris, 'verts', len(me.vertices), 'slots', len(me.materials))
    print('BUILD size X %.3f Y %.3f Z %.3f  zmin %.3f' %
          (max(xs)-min(xs), max(ys)-min(ys), max(zs)-min(zs), min(zs)))

def export_fbx():
    """Exportiert das aktive Unkraut-Mesh nach ArtSource/Unkraut/unkraut_neu.fbx."""
    import os
    ob = bpy.data.objects['Unkraut']
    out = 'C:/Users/vorau/dev/Spinnenspiel/ArtSource/Unkraut/unkraut_neu.fbx'
    os.makedirs(os.path.dirname(out), exist_ok=True)
    bpy.ops.object.select_all(action='DESELECT')
    ob.select_set(True); bpy.context.view_layer.objects.active = ob
    bpy.ops.export_scene.fbx(filepath=out, use_selection=True,
        apply_scale_options='FBX_SCALE_ALL', axis_forward='-Z', axis_up='Y',
        bake_space_transform=True, mesh_smooth_type='FACE',
        path_mode='STRIP', use_mesh_modifiers=True)
    print('EXPORT', out, os.path.getsize(out), 'bytes')

build()

