# MODELL1 Gras1: Aufbau in Blender (wird per MCP ausgefuehrt).
# Vorgaben: 0.3..0.6 m, >=30 gebogene Halme (Dreiecksstreifen mit Mittelrinne),
# zufaellige Neigung und Hoehe, 1 Slot, 300..800 Dreiecke. Fester Seed.
# Selbstkritik D1: glattere Boegen (7 Stationen), mischte Neigung
# (aufrecht innen, ueberhaengend aussen), kurze Innenhalme, Sockelfaeche weg.
import bpy, bmesh, math, random

SEED = 17
N_BLADES = 30
STATIONS = 7   # 4*(S-1)+2 = 26 Dreiecke/Halm -> 780 gesamt

rng = random.Random(SEED)


def make_blade(bm, az, height, tilt, bend, width, phi, twist):
    """Halm als integrierter Bogen: Tangente neigt sich von tilt (unten)
    auf tilt+bend (Spitze). Querschnitt: V-Rinne (Mittelrippe)."""
    r0 = rng.uniform(0.010, 0.075)          # Basis auf flacher Scheibe (D2)
    bx, by = math.cos(az) * r0, math.sin(az) * r0
    rows = []
    x, y, z = bx, by, 0.0
    ds = height / (STATIONS - 1.5)          # Bogensegmentlaenge
    for i in range(STATIONS):
        t = i / (STATIONS - 1)
        th = tilt + bend * t ** 1.6         # Tangentenneigung (weicher Bogen)
        psi = az + twist * t                # seitliches Abdriften
        if i > 0:
            x += ds * math.sin(th) * math.cos(psi)
            y += ds * math.sin(th) * math.sin(psi)
            z += ds * math.cos(th)
        # Breite: unten schmal, Maximum bei ~25%, dann spitz auslaufend
        w = width * min(1.0, t / 0.25) * (1.0 - t) ** 0.7
        w = max(w, 0.0008)
        d = 0.34 * w                        # Rinne nach innen geknickt
        nx, ny = math.cos(psi + phi), -math.sin(psi + phi)  # Blattflaeche
        v1 = bm.verts.new((x - w * nx, y - w * ny, z))
        v2 = bm.verts.new((x + w * nx, y + w * ny, z))
        vm = bm.verts.new((x, y, z - d))
        rows.append((v1, vm, v2))
    for i in range(STATIONS - 1):
        a1, am, a2 = rows[i]
        n1, nm, n2 = rows[i + 1]
        bm.faces.new((a1, am, nm)); bm.faces.new((a1, nm, n1))
        bm.faces.new((am, a2, n2)); bm.faces.new((am, n2, nm))
    t1, tm, t2 = rows[-1]
    th = tilt + bend
    tip = bm.verts.new((t1.co.x + 0.03 * height * math.sin(th),
                        t1.co.y + 0.03 * height * math.sin(th),
                        t1.co.z + 0.03 * height * math.cos(th)))
    bm.faces.new((t1, tm, tip)); bm.faces.new((tm, t2, tip))


def build(name):
    bm = bmesh.new()
    golden = 2.39996  # Goldener Winkel: gleichmaessige Verteilung ohne Klumpen
    for k in range(N_BLADES):
        az = k * golden + rng.uniform(-0.1, 0.1)
        if k < 8:                           # D3: innere Fuellhalme (kurz)
            tilt = rng.uniform(0.02, 0.25)
            height = rng.uniform(0.18, 0.32)
        elif rng.random() < 0.4:            # ueberhaengende Aussenspitzen
            tilt = rng.uniform(0.35, 0.75)
            height = rng.uniform(0.32, 0.55)
        else:                               # aufrechte Innenhalme
            tilt = rng.uniform(0.04, 0.35)
            height = rng.uniform(0.22, 0.58)
        # D3: Gesamtauslenkung klemmen -> keine fast horizontalen Ausreisser
        bend = rng.uniform(0.4, 1.3) if height < 0.5 else rng.uniform(0.3, 0.8)
        # D4: Klemme schaerfer -> kein fast horizontaler Halm
        bend = min(bend, 1.25 - tilt)
        width = rng.uniform(0.016, 0.028)   # D2: breiter -> fuellt Luecken
        phi = rng.uniform(-1.1, 1.1)        # Blattflaechen-Orientierung
        twist = rng.uniform(-0.6, 0.6)      # seitliches Abdriften
        make_blade(bm, az, height, tilt, bend, width, phi, twist)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)  # D3: saubere Normale
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me)
    bm.free()
    ob = bpy.data.objects.new(name, me)
    bpy.context.collection.objects.link(ob)
    xs = [v.co.x for v in me.vertices]
    ys = [v.co.y for v in me.vertices]
    zs = [v.co.z for v in me.vertices]
    tris = sum(len(p.vertices) - 2 for p in me.polygons)
    print(name, ': Halme', N_BLADES, 'Tris', tris,
          'H', round(max(zs), 3), 'Bx', round(max(xs) - min(xs), 3),
          'By', round(max(ys) - min(ys), 3))
    return ob


if __name__ == '__main__' or True:
    old = bpy.data.objects.get('Gras1')
    if old:
        m = old.data
        bpy.data.objects.remove(old, do_unlink=True)
        bpy.data.meshes.remove(m)
    build('Gras1')
