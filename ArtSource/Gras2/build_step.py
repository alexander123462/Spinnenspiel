# MODELL2 Gras2: Seggen-Bueschel (Carex-Typ), wird per Blender-MCP ausgefuehrt.
# Vorgaben: zweiter Grasbueschel, breite ueberhaengende Blaetter, 0.4..0.8 m,
# 20 Blaetter, 300..800 Dreiecke, 1 Slot (MAT_Gras2), fester Seed.
# Selbstkritik: D1 flach/eckig -> D2 10 Stationen, dichtere Rosette ->
# D3/D3b keine Senkrecht-Stachel, kuerezere Aussenspitzen, Spur ~0.9 m ->
# D4 phi nie radial (keine Haarlinien), Rinne 0.38w -> final: Spitze folgt
# der Tangente (Richtungs-Fix) und ohne z-Klemme (kein Haken).
# D5 (neue Runde): Hoehenklemmung auf Vorgabe (0.78 m Ziel, Spur max 0.80 m),
# Innenblaetter geneigt (kein Stachel), Rinne 0.50w (mehr Volumen).
# D6: Blattspitzen am Boden abgeflacht (z min -0.02, vorher -0.36 = im Erdreich)
# und Hoehe auf 0.76 m geklemmt (Unity-Test-Fenster 0.4..0.8 mit Reserve).
import bpy, bmesh, math, random

SEED = 103
N = 20       # Blaetter (Vorgabe)
S = 10       # Stationen/Blatt -> 4*(S-1)+2 = 38 Dreiecke, plus 8 Sockel = 763
rng = random.Random(SEED)
golden = 2.39996


def make_leaf(bm, az, L, tilt, bend, width, phi, twist):
    """Seggenblatt: integrierter Bogen, V-Rinne, weiche Spitze entlang Tangente."""
    r0 = rng.uniform(0.04, 0.12)                 # dichter Basalschopf
    x, y, z = math.cos(az) * r0, math.sin(az) * r0, 0.0
    rows = []
    ds = L / (S - 2.0)
    psi = az
    for i in range(S):
        t = i / (S - 1)
        th = tilt + bend * (t ** 1.15)           # Bogen setzt frueh ein
        psi = az + twist * math.sin(t * 1.4)     # sanftes Abdriften
        if i > 0:
            x += ds * math.sin(th) * math.cos(psi)
            y += ds * math.sin(th) * math.sin(psi)
            z += ds * math.cos(th)
        # Breite: schmale Basis, Maximum ~35%, langer weicher Auslauf
        w = width * (min(1.0, t / 0.35) ** 0.8) * ((1.0 - t) ** 0.42) * (1.0 + 0.2 * t)
        if t >= 0.9:
            w = max(w, 0.002)                    # letzte Reihe nicht kollabiert
        w = max(w, 0.0012)
        d = 0.50 * w                             # Seggen-V-Rinne (D5: tiefer, mehr Volumen)
        nx, ny = math.cos(psi + phi), -math.sin(psi + phi)
        v1 = bm.verts.new((x - w * nx, y - w * ny, z))
        v2 = bm.verts.new((x + w * nx, y + w * ny, z))
        vm = bm.verts.new((x, y, z - d))
        rows.append((v1, vm, v2))
    for i in range(S - 1):
        a1, am, a2 = rows[i]
        n1, nm, n2 = rows[i + 1]
        bm.faces.new((a1, am, nm)); bm.faces.new((a1, nm, n1))
        bm.faces.new((am, a2, n2)); bm.faces.new((am, n2, nm))
    t1, tm, t2 = rows[-1]
    th = tilt + bend
    ext = 0.6 * ds                               # tangentiale Spitze, kein Haken
    tip = bm.verts.new((tm.co.x + ext * math.sin(th) * math.cos(psi),
                        tm.co.y + ext * math.sin(th) * math.sin(psi),
                        tm.co.z + ext * math.cos(th)))
    bm.faces.new((t1, tm, tip)); bm.faces.new((tm, t2, tip))


def build(name):
    bm = bmesh.new()
    for k in range(N):
        az = k * golden + rng.uniform(-0.08, 0.08)
        if k < 5:                                # innen: boegig, bringt Hoehe
            tilt = rng.uniform(0.26, 0.42); bend = rng.uniform(0.9, 1.2); L = rng.uniform(0.6, 0.72)
        elif k < 13:                             # Mittel: Springbrunnen-Bogen
            tilt = rng.uniform(0.35, 0.55); bend = rng.uniform(0.9, 1.2); L = rng.uniform(0.45, 0.6)
        else:                                    # aussen ueberhaengend, kurze Bogen
            tilt = rng.uniform(0.55, 0.78); bend = rng.uniform(0.95, 1.3); L = rng.uniform(0.4, 0.5)
        width = rng.uniform(0.028, 0.048)        # breiter als Gras1 (0.016..0.028)
        phi = rng.choice([-1, 1]) * rng.uniform(0.3, 1.1)  # nie kantig-radial
        twist = rng.uniform(-0.5, 0.5)
        make_leaf(bm, az, L, tilt, bend, width, phi, twist)
    zc = bm.verts.new((0, 0, 0.004))             # Sockelscheibe als Wurzelansatz
    ring = []
    for i in range(8):
        a = i * math.pi / 4 + rng.uniform(-0.06, 0.06)
        r = rng.uniform(0.07, 0.11)
        ring.append(bm.verts.new((math.cos(a) * r, math.sin(a) * r, 0.0)))
    for i in range(8):
        bm.faces.new((zc, ring[i], ring[(i + 1) % 8]))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    bmesh.ops.dissolve_degenerate(bm, dist=0.0008, edges=bm.edges)
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me); bm.free()
    ob = bpy.data.objects.new(name, me)
    bpy.context.collection.objects.link(ob)
    mat = bpy.data.materials.get('MAT_Gras2') or bpy.data.materials.new('MAT_Gras2')
    ob.data.materials.append(mat)
    # Ziel-Proportion D5: Hoehe gleichmaessig in die Vorgabe 0.4..0.8 m klemmen
    # (Alter Stand: 0.92 m hoch -> Unity-Test 0.4..0.8 waere rot), dann Spur max ~0.8 m.
    zs = [v.co.z for v in me.vertices]
    s2 = min(1.0, 0.78 / max(zs))
    if s2 < 1.0:
        for v in me.vertices:
            v.co.x *= s2; v.co.y *= s2; v.co.z *= s2
    xs = [v.co.x for v in me.vertices]; ys = [v.co.y for v in me.vertices]
    zs = [v.co.z for v in me.vertices]
    s = min(1.0, 0.80 / max(max(xs) - min(xs), max(ys) - min(ys)))
    if s < 1.0:
        for v in me.vertices:
            v.co.x *= s; v.co.y *= s
    # D6: ueberhaengende Blattspitzen ragen bis -0.36 m unter den Fusspunkt und
    # waeren im Spiel im Erdreich vergraben (und Gesamthoehe 0.80 = Testgrenze).
    # Spitze am Boden abflachen (z-untergrenze -0.02) und Hoehe auf 0.76 klemmen.
    FLOOR = -0.02
    for v in me.vertices:
        if v.co.z < FLOOR:
            v.co.z = FLOOR
    zs = [v.co.z for v in me.vertices]
    s3 = min(1.0, 0.76 / (max(zs) - min(zs)))
    if s3 < 1.0:
        for v in me.vertices:
            v.co.x *= s3; v.co.y *= s3; v.co.z *= s3
    me.update()
    tris = sum(len(p.vertices) - 2 for p in me.polygons)
    print(name, ': Blaetter', N, 'Tris', tris,
          'H', round(max(zs), 3),
          'Bx', round(max(xs) - min(xs), 3), 'By', round(max(ys) - min(ys), 3))
    return ob


if __name__ == '__main__' or True:
    old = bpy.data.objects.get('Gras2')
    if old:
        m = old.data
        bpy.data.objects.remove(old, do_unlink=True)
        bpy.data.meshes.remove(m)
    build('Gras2')
