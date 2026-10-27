# MODELL10 Busch3: fedriger Strauch (Nadeln an boegigen Zweigen), prozedural, feste Seeds.
# Umbau nach D1-Selbstkritik: Sekundaerzweige frueher angesetzt, Tertiaerzweige ergaenzt,
# Nadeln dichter und bis an die Zweigspitzen (Endbuschel), Stamm unten teilbestueckt.
import bpy, bmesh, math, random
from mathutils import Vector

for o in list(bpy.data.objects):
    m = o.data
    bpy.data.objects.remove(o, do_unlink=True)
    if m and isinstance(m, bpy.types.Mesh) and m.users == 0:
        bpy.data.meshes.remove(m)

GOLD = 2.399963
# UV-Atlas wie Busch2: drei Blattzellen (U-Bereiche), Rindenband V 0.02..0.30.
CELLS = ((0.02, 0.31), (0.35, 0.64), (0.68, 0.97))
BARK0, BARK1 = 0.02, 0.30

def tube(bm_out, pts, radii, sides=6):
    rings = []
    for i, p in enumerate(pts):
        p = Vector(p)
        tang = (Vector(pts[min(i+1, len(pts)-1)]) - Vector(pts[max(i-1, 0)]))
        if tang.length < 1e-6:
            tang = Vector((0, 0, 1))
        nrm = tang.normalized()
        up = Vector((0, 0, 1)) if abs(nrm.z) < 0.9 else Vector((1, 0, 0))
        x1 = nrm.cross(up).normalized()
        y1 = nrm.cross(x1).normalized()
        r = radii[i]
        ring = []
        for s in range(sides):
            a = 2 * math.pi * s / sides
            ring.append(bm_out.verts.new(p + x1 * math.cos(a) * r + y1 * math.sin(a) * r))
        rings.append(ring)
    nseg = len(rings) - 1
    uvl = bm_out.loops.layers.uv.verify() or bm_out.loops.layers.uv.new('UVMap')
    for i in range(nseg):
        t0 = BARK0 + (BARK1 - BARK0) * i / nseg
        t1 = BARK0 + (BARK1 - BARK0) * (i + 1) / nseg
        for s in range(sides):
            f = bm_out.faces.new((rings[i][s], rings[i][(s+1) % sides],
                                  rings[i+1][(s+1) % sides], rings[i+1][s]))
            u0 = s / sides
            u1 = 1.0 if s + 1 == sides else (s + 1) / sides
            for k, uv in enumerate(((u0, t0), (u1, t0), (u1, t1), (u0, t1))):
                f.loops[k][uvl].uv = uv
    return rings

def link(name, bm):
    bm.normal_update()
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me)
    bm.free()
    ob = bpy.data.objects.new(name, me)
    bpy.context.collection.objects.link(ob)
    return ob

# --- Astgeruest -------------------------------------------------------------
rng = random.Random(731)
stems = []      # list of (stations, radii)
for k in range(9):   # 6 aussere + 3 kurze Innenstaelle fuellen die Mitte (D3)
    inner = k >= 6
    az = rng.uniform(0, 2 * math.pi)
    lean = rng.uniform(0.04, 0.12) if inner else rng.uniform(0.12, 0.30)
    h = rng.uniform(0.45, 0.65) if inner else rng.uniform(0.58, 0.88)
    r0 = (0.03 + rng.uniform(0, 0.03)) if inner else 0.09 * rng.uniform(0.6, 1.0)
    pts = []
    radi = []
    for st in range(7):
        t = st / 6.0
        x = math.cos(az) * (r0 + lean * h * t ** 1.6) * 0.9
        y = math.sin(az) * (r0 + lean * h * t ** 1.6) * 0.9
        z = h * t * (1.0 - 0.05 * lean)
        pts.append((x, y, z))
        base_r = 0.008 if inner else 0.014
        radi.append(base_r * (1 - t) + 0.003 * t if st > 0 else base_r * 1.2)
    stems.append((pts, radi))

rng2 = random.Random(913)
twigs = []      # (pts, radii, kind) kind: 1=Sekundaer, 2=Tertiaer, 3=Innenzweig
for k, (pts, radi) in enumerate(stems):
    if k >= 6:      # D3: Innenstaelle tragen keine Sekundaerzweige (Budget)
        continue
    nb = 2          # D5: fix 2 statt 2-3 (Budget fuer Kronenfueller)
    for b in range(nb):
        t = rng2.uniform(0.35, 0.8)
        si = int(t * 6)
        base = Vector(pts[si])
        az = rng2.uniform(0, 2 * math.pi)
        el = rng2.uniform(0.55, 1.10)
        L = rng2.uniform(0.16, 0.28)
        droop = rng2.uniform(0.0, 0.10)
        tp = []
        tr = []
        for st in range(6):
            u = st / 5.0
            phi = el + math.radians(-14) * u * (droop * 10)
            d = L * u
            x = base.x + math.cos(phi) * math.cos(az) * d
            y = base.y + math.cos(phi) * math.sin(az) * d
            z = base.z + math.sin(phi) * d - droop * L * u * u
            tp.append((x, y, z))
            tr.append(0.0065 * (1 - u) + 0.0022 * u)
        twigs.append((tp, tr, 1))

# D2/D3-Korrektur: Innenzweige — fuellen die kahle Kronenmitte (Draufsicht).
rng2b = random.Random(4111)
for k, (pts, radi) in enumerate(stems):
    for rep in range(1 if k < 6 else 2):
        si = rng2b.randint(3, 4)
        base = Vector(pts[si])
        az = math.atan2(-base.y, -base.x) + rng2b.uniform(-0.5, 0.5)  # zur Mitte
        el = rng2b.uniform(1.0, 1.35)
        L = rng2b.uniform(0.12, 0.18)
        tp = []
        tr = []
        for st in range(5):
            u = st / 4.0
            d = L * u
            tp.append((base.x + math.cos(el) * math.cos(az) * d,
                       base.y + math.cos(el) * math.sin(az) * d,
                       base.z + math.sin(el) * d))
            tr.append(0.0045 * (1 - u) + 0.0015 * u)
        twigs.append((tp, tr, 3))

# D5-Korrektur: Kronenfueller — steile Innenzweige fuellen die offenen Keile
# zwischen den oberen Haupttrieben (Selbstkritik D4: obere Haelfte zu sparrig).
rng2c = random.Random(607)
for k in range(6):
    pts, radi = stems[k]
    for rep in range(2):
        si = rng2c.randint(3, 5)
        base = Vector(pts[si])
        az = math.atan2(-base.y, -base.x) + rng2c.uniform(-0.7, 0.7)
        el = rng2c.uniform(1.15, 1.55)          # steil nach oben/in die Krone
        L = rng2c.uniform(0.20, 0.30)
        tp = []
        tr = []
        for st in range(6):
            u = st / 5.0
            d = L * u
            tp.append((base.x + math.cos(el) * math.cos(az) * d,
                       base.y + math.cos(el) * math.sin(az) * d,
                       base.z + math.sin(el) * d))
            tr.append(0.005 * (1 - u) + 0.0016 * u)
        twigs.append((tp, tr, 3))

rng3 = random.Random(2027)
for (tp, tr, kind) in list(twigs):
    if kind != 1:
        continue
    nb = 1
    if rng3.random() < 0.45:   # D3: nur gut die Haelfte der Sekundaerzweige verzweigt tertiaer
        continue
    for b in range(nb):
        u = rng3.uniform(0.4, 0.9)
        si = max(1, int(u * 5))
        base = Vector(tp[si])
        az = rng3.uniform(0, 2 * math.pi)
        el = rng3.uniform(0.4, 1.2)
        L = rng3.uniform(0.10, 0.18)
        tp2 = []
        tr2 = []
        for st in range(5):
            uu = st / 4.0
            d = L * uu
            x = base.x + math.cos(el) * math.cos(az) * d
            y = base.y + math.cos(el) * math.sin(az) * d
            z = base.z + math.sin(el) * d - 0.02 * L * uu * uu
            tp2.append((x, y, z))
            tr2.append(0.0035 * (1 - uu) + 0.0012 * uu)
        twigs.append((tp2, tr2, 2))

# --- Nadeln -----------------------------------------------------------------
rng4 = random.Random(1777)

def needles_on(bm, pts, skip=0, spacing=(0.012, 0.015), nlen=(0.028, 0.05)):
    phase = rng4.uniform(0, 6.28)
    uvl = bm.loops.layers.uv.verify() or bm.loops.layers.uv.new('UVMap')
    k = 0
    for i in range(skip, len(pts) - 1):
        a, b = Vector(pts[i]), Vector(pts[i+1])
        seg = b - a
        steps = max(1, int(seg.length / rng4.uniform(*spacing)))
        for s in range(steps):
            t = (s + 0.5) / steps
            p = a.lerp(b, t)
            tang = seg.normalized()
            ang = phase + k * GOLD
            k += 1
            up = Vector((0, 0, 1)) if abs(tang.z) < 0.9 else Vector((1, 0, 0))
            x1 = tang.cross(up).normalized()
            y1 = tang.cross(x1).normalized()
            rad = x1 * math.cos(ang) + y1 * math.sin(ang)
            pitch = rng4.uniform(0.7, 1.25)   # D4: Nadeln mehr seitlich abstehend
            d = (tang * math.cos(pitch) + rad * math.sin(pitch)).normalized()
            L = rng4.uniform(*nlen)
            w = L * rng4.uniform(0.30, 0.38)   # D6: grosse Laubdeckung pro Dreieck (Pixel-Check: Fuellung zu niedrig)
            keel = w * 0.9
            q = d.cross(tang).normalized()
            v0 = bm.verts.new(p)
            v1 = bm.verts.new(p + d*L*0.32 + q*w*0.5 + Vector((0, 0, -keel*0.35)))
            v2 = bm.verts.new(p + d*L*0.32 - q*w*0.5 + Vector((0, 0, -keel*0.35)))
            v3 = bm.verts.new(p + d*L*0.75 + q*w*0.28 + Vector((0, 0, keel*0.15)))
            v4 = bm.verts.new(p + d*L*0.75 - q*w*0.28 + Vector((0, 0, keel*0.15)))
            v5 = bm.verts.new(p + d*L + Vector((0, 0, -0.002)))
            cell = CELLS[rng4.randrange(3)]   # Nadel-UV: Blattzelle, V=Laengsachse
            cu = (cell[0] + cell[1]) * 0.5
            cw = cell[1] - cell[0]
            uvmap = {v0: (cu, 0.0), v1: (cu + 0.40 * cw, 0.32), v2: (cu - 0.40 * cw, 0.32),
                     v3: (cu + 0.22 * cw, 0.75), v4: (cu - 0.22 * cw, 0.75), v5: (cu, 1.0)}
            for f in (bm.faces.new((v0, v2, v1)), bm.faces.new((v2, v4, v3, v1)),
                      bm.faces.new((v3, v4, v5))):
                for lp in f.loops:
                    lp[uvl].uv = uvmap[lp.vert]

def end_tuft(bm, pts):
    tip = Vector(pts[-1])
    tang = (Vector(pts[-1]) - Vector(pts[-2])).normalized()
    uvl = bm.loops.layers.uv.verify() or bm.loops.layers.uv.new('UVMap')
    up = Vector((0, 0, 1)) if abs(tang.z) < 0.9 else Vector((1, 0, 0))
    x1 = tang.cross(up).normalized()
    y1 = tang.cross(x1).normalized()
    for j in range(5):   # D5: Buschel 5 Nadeln (Budget)
        ang = rng4.uniform(0, 6.28) + j * GOLD
        rad = x1 * math.cos(ang) + y1 * math.sin(ang)
        d = (tang * 0.75 + rad * 0.66).normalized()
        L = rng4.uniform(0.05, 0.08)
        w = L * 0.30
        q = d.cross(tang).normalized()
        v0 = bm.verts.new(tip)
        v1 = bm.verts.new(tip + d*L*0.4 + q*w*0.5)
        v2 = bm.verts.new(tip + d*L*0.4 - q*w*0.5)
        v5 = bm.verts.new(tip + d*L + Vector((0, 0, -0.003)))
        cell = CELLS[rng4.randrange(3)]   # Buschel-UV: gleiche Blattzellen
        cu = (cell[0] + cell[1]) * 0.5
        cw = cell[1] - cell[0]
        uvmap = {v0: (cu, 0.0), v1: (cu + 0.40 * cw, 0.40), v2: (cu - 0.40 * cw, 0.40),
                 v5: (cu, 1.0)}
        for f in (bm.faces.new((v0, v2, v1)), bm.faces.new((v1, v2, v5))):
            for lp in f.loops:
                lp[uvl].uv = uvmap[lp.vert]

bm_a = bmesh.new()
for pts, radi in stems:
    tube(bm_a, pts, radi)
for tp, tr, kind in twigs:
    tube(bm_a, tp, tr)
# Wurzel-Anlaufkegel
rings = []
rng5 = random.Random(555)
for (r, z) in [(0.055, -0.012), (0.045, 0.03), (0.030, 0.09), (0.016, 0.16)]:
    ring = []
    for s in range(8):
        a = 2 * math.pi * s / 8 + rng5.uniform(-0.15, 0.15)
        rr = r * rng5.uniform(0.85, 1.15)
        ring.append(bm_a.verts.new(Vector((math.cos(a)*rr, math.sin(a)*rr, z))))
    rings.append(ring)
uvl_f = bm_a.loops.layers.uv.verify() or bm_a.loops.layers.uv.new('UVMap')
for i in range(len(rings)-1):
    t0 = BARK0 + (BARK1 - BARK0) * i / (len(rings)-1)
    t1 = BARK0 + (BARK1 - BARK0) * (i+1) / (len(rings)-1)
    for s in range(8):
        f = bm_a.faces.new((rings[i][s], rings[i][(s+1)%8], rings[i+1][(s+1)%8], rings[i+1][s]))
        ua = s / 8.0
        ub = 1.0 if s + 1 == 8 else (s + 1) / 8.0
        for k, uv in enumerate(((ua, t0), (ub, t0), (ub, t1), (ua, t1))):
            f.loops[k][uvl_f].uv = uv

bm_l = bmesh.new()
for pts, radi in stems:
    if radi[0] < 0.012:   # Innenstaengel: grober bestueckt (Dreiecksbudget)
        needles_on(bm_l, pts, skip=2, spacing=(0.020, 0.024), nlen=(0.07, 0.11))
    else:
        needles_on(bm_l, pts, skip=1, spacing=(0.016, 0.019), nlen=(0.07, 0.11))
for tp, tr, kind in twigs:
    if kind == 1:
        needles_on(bm_l, tp, spacing=(0.013, 0.016), nlen=(0.06, 0.10))
    elif kind == 2:
        needles_on(bm_l, tp, spacing=(0.013, 0.016), nlen=(0.05, 0.08))
    elif len(tp) > 5:   # Kronenfueller (D5): grober bestueckt
        needles_on(bm_l, tp, spacing=(0.019, 0.023), nlen=(0.07, 0.11))
    else:
        needles_on(bm_l, tp, spacing=(0.013, 0.016), nlen=(0.05, 0.08))
# D5: Endbuschel nur an Sekundaer-/Tertiaerzweigen (Budget)
for tp, tr, kind in twigs:
    if kind != 3:
        end_tuft(bm_l, tp)
for pts, radi in stems:
    end_tuft(bm_l, pts)   # D2: auch Stamm-Spitzen tragen Nadelbuschel

aeste = link('Busch3_Aeste', bm_a)
batt = link('Busch3_Blaetter', bm_l)
m_b = bpy.data.materials.get('MAT_Busch3_Aeste') or bpy.data.materials.new('MAT_Busch3_Aeste')
m_l = bpy.data.materials.get('MAT_Busch3_Blaetter') or bpy.data.materials.new('MAT_Busch3_Blaetter')
for m, col in ((m_b, (0.09, 0.065, 0.04, 1)), (m_l, (0.07, 0.20, 0.06, 1))):
    m.use_nodes = True
    m.node_tree.nodes['Principled BSDF'].inputs['Base Color'].default_value = col
aeste.data.materials.append(m_b)
batt.data.materials.append(m_l)

for area in bpy.context.screen.areas:
    if area.type == 'VIEW_3D':
        sp = area.spaces.active
        try:
            sp.shading.type = 'MATERIAL'
        except TypeError:   # Blender 5.x: kein 'MATERIAL'-Enum mehr
            sp.shading.type = 'SOLID'
            sp.shading.color_type = 'MATERIAL'
        sp.overlay.show_overlays = False
        region = [r for r in area.regions if r.type == 'WINDOW'][0]
        with bpy.context.temp_override(area=area, region=region):
            bpy.ops.view3d.view_axis(type='FRONT')
            bpy.ops.view3d.view_all()

tri_a = sum(len(p.vertices) - 2 for p in aeste.data.polygons)
tri_l = sum(len(p.vertices) - 2 for p in batt.data.polygons)
allz = [ (aeste.matrix_world @ v.co).z for v in aeste.data.vertices ] + \
       [ (batt.matrix_world @ v.co).z for v in batt.data.vertices ]
allx = [ (aeste.matrix_world @ v.co).x for v in aeste.data.vertices ] + \
       [ (batt.matrix_world @ v.co).x for v in batt.data.vertices ]
ally = [ (aeste.matrix_world @ v.co).y for v in aeste.data.vertices ] + \
       [ (batt.matrix_world @ v.co).y for v in batt.data.vertices ]
print('Dreiecke Aeste:', tri_a, 'Blaetter:', tri_l, 'Gesamt:', tri_a + tri_l)
print('Hoehe:', round(max(allz)-min(allz), 3), 'min z:', round(min(allz), 3),
      'Breite x:', round(max(allx)-min(allx), 3), 'y:', round(max(ally)-min(ally), 3))
