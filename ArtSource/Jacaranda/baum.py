"""Jacaranda neu bauen (Blender 5.1, headless):  blender -b -P baum.py -- <fbx-ausgabe> <vorschau-ordner>
Zwei Meshes (LOD0/LOD1) mit 3 Material-Slots in fester Reihenfolge [jacaranda_tree_branches, jacaranda_tree_trunk, jacaranda_tree_leaves].
Ursprung am Stammfuss, Z oben. Gleiches Skelett fuer beide LODs. Fester Seed.
"""
import bpy, bmesh, math, random, sys
from mathutils import Vector, Quaternion

argv = sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else []
FBX_OUT = argv[0] if argv else r'C:\temp\jacaranda_tree.fbx'
PREV = argv[1] if len(argv) > 1 else r'C:\temp'
SEED = int(argv[2]) if len(argv) > 2 else 7
random.seed(SEED)

TARGET_H, TARGET_W = 19.0, 20.0   # wie das Original (Scan): 18.9 m hoch, ~24.5 m breit
GOLD = 2.399963

bpy.ops.wm.read_factory_settings(use_empty=True)

# ---------------------------------------------------------------- Skelett
branches = []   # dict(points, radii, level)


def perp_basis(t):
    a = Vector((0, 0, 1)) if abs(t.z) < 0.9 else Vector((1, 0, 0))
    p1 = t.cross(a).normalized()
    p2 = t.cross(p1).normalized()
    return p1, p2


def sample(b, t):
    """Punkt, Tangente, Radius auf Branch b bei t in 0..1."""
    pts, rad = b['points'], b['radii']
    f = t * (len(pts) - 1)
    i = min(int(f), len(pts) - 2)
    u = f - i
    p = pts[i].lerp(pts[i + 1], u)
    tg = (pts[i + 1] - pts[i]).normalized()
    r = rad[i] * (1 - u) + rad[i + 1] * u
    return p, tg, r


def grow(origin, d, length, r0, r1, level, segs, tropism=0.0, droop=0.0, jitter=0.1):
    pts, rad = [origin.copy()], [r0]
    cur, dv = origin.copy(), d.normalized()
    step = length / segs
    for i in range(segs):
        dv = (dv + Vector((0, 0, tropism - droop)) * 0.5
              + Vector((random.uniform(-jitter, jitter), random.uniform(-jitter, jitter), random.uniform(-jitter, jitter) * 0.5))).normalized()
        cur = cur + dv * step
        pts.append(cur.copy())
        rad.append(r0 + (r1 - r0) * (i + 1) / segs)
    b = {'points': pts, 'radii': rad, 'level': level}
    branches.append(b)
    return b


def child_dir(tg, k, lo, hi, up_bias):
    p1, p2 = perp_basis(tg)
    psi = k * GOLD + random.uniform(-0.5, 0.5)
    axis = p1 * math.cos(psi) + p2 * math.sin(psi)
    ang = math.radians(random.uniform(lo, hi))
    d = tg * math.cos(ang) + axis * math.sin(ang)
    return (d + Vector((0, 0, up_bias))).normalized()


def populate(parent, n, t0, t1, lo, hi, len_f, r_f, r_tip, level, segs, trop, droop, up_bias, jit):
    for k in range(n):
        t = t0 + (t1 - t0) * (k + random.uniform(0.0, 0.6)) / max(1, n)
        p, tg, r = sample(parent, min(0.98, t))
        d = child_dir(tg, k + random.randint(0, 3), lo, hi, up_bias)
        plen = sum((parent['points'][i + 1] - parent['points'][i]).length for i in range(len(parent['points']) - 1))
        L = plen * len_f * random.uniform(0.75, 1.15) * (1.0 - 0.45 * t)
        yield grow(p, d, L, max(r * r_f, r_tip * 1.5), r_tip, level, segs, trop, droop, jit)


# Stamm: leicht S-foermig, mit Wurzelanlauf
trunk_pts = [Vector((0, 0, -0.5)), Vector((0.05, 0.02, 0.0)), Vector((0.12, 0.05, 0.7)), Vector((0.05, 0.12, 1.5)),
             Vector((-0.12, 0.10, 2.3)), Vector((-0.10, -0.02, 3.0)), Vector((0.0, -0.05, 3.6))]
trunk_rad = [1.05, 0.80, 0.63, 0.56, 0.52, 0.49, 0.46]
trunk = {'points': trunk_pts, 'radii': trunk_rad, 'level': 0}
branches.append(trunk)

fork = trunk_pts[-1]
limbs = []
nl = 5
for k in range(nl):
    psi = k * (2 * math.pi / nl) + random.uniform(-0.35, 0.35)
    beta = math.radians(random.uniform(32, 55))
    d = Vector((math.sin(beta) * math.cos(psi), math.sin(beta) * math.sin(psi), math.cos(beta)))
    limbs.append(grow(fork, d, random.uniform(7.5, 9.5), 0.34, 0.12, 1, 9, tropism=0.18, droop=0.02, jitter=0.07))
# zwei kleinere Seitenaeste am Stamm
for k in range(2):
    p, tg, r = sample(trunk, 0.7 + 0.15 * k)
    d = child_dir(tg, k * 3, 45, 65, 0.15)
    limbs.append(grow(p, d, random.uniform(5.0, 6.5), 0.20, 0.09, 1, 7, tropism=0.10, droop=0.02, jitter=0.07))

l2 = []
for limb in limbs:
    l2 += list(populate(limb, 5, 0.22, 0.95, 32, 58, 0.62, 0.50, 0.045, 2, 7, 0.06, 0.03, 0.18, 0.08))
l3 = []
for b in l2:
    l3 += list(populate(b, 4, 0.25, 0.95, 35, 65, 0.55, 0.50, 0.020, 3, 5, 0.02, 0.07, 0.10, 0.10))
l4 = []
for b in l3:
    l4 += list(populate(b, 3, 0.30, 0.95, 35, 70, 0.55, 0.50, 0.007, 4, 4, 0.0, 0.10, 0.05, 0.12))

# Kronengroesse auf Zielmasse bringen (nur Positionen skalieren, Radien bleiben)
allp = [p for b in branches for p in b['points']]
minx, maxx = min(p.x for p in allp), max(p.x for p in allp)
miny, maxy = min(p.y for p in allp), max(p.y for p in allp)
maxz = max(p.z for p in allp)
w = max(maxx - minx, maxy - miny)
sxy = (TARGET_W - 2.0) / w   # Blattkarten stehen ueber die Zweigspitzen hinaus
sz = (TARGET_H - 2.4) / maxz
cx, cy = (minx + maxx) / 2, (miny + maxy) / 2
for b in branches:
    if b is trunk:
        continue
    for p in b['points']:
        p.x = (p.x - cx) * sxy
        p.y = (p.y - cy) * sxy
        if p.z > 0.5:
            p.z = 3.6 + (p.z - 3.6) * sz
# Stamm unter die Krone mittig halten
for p in trunk['points']:
    p.x -= cx * 0.0
    p.y -= cy * 0.0
print('Skelett: %d Aeste, Breite %.1f, Hoehe %.1f' % (len(branches), w * sxy, maxz * sz))

# ---------------------------------------------------------------- Mesh


def add_tube(bm, b, sides, mat_idx, vscale):
    pts, rad = b['points'], b['radii']
    n = len(pts)
    tans = []
    for i in range(n):
        a = pts[max(0, i - 1)]
        c = pts[min(n - 1, i + 1)]
        tans.append((c - a).normalized())
    p1, p2 = perp_basis(tans[0])
    rings = []
    vs = []
    acc = 0.0
    for i in range(n):
        if i > 0:
            acc += (pts[i] - pts[i - 1]).length
            q = tans[i - 1].rotation_difference(tans[i])
            p1 = q @ p1
            p2 = q @ p2
        ring = []
        for j in range(sides):
            a = 2 * math.pi * j / sides
            ring.append(bm.verts.new(pts[i] + (p1 * math.cos(a) + p2 * math.sin(a)) * rad[i]))
        rings.append(ring)
        vs.append(acc / vscale)
    uv = bm.loops.layers.uv.verify()
    for i in range(n - 1):
        for j in range(sides):
            j2 = (j + 1) % sides
            f = bm.faces.new((rings[i][j], rings[i][j2], rings[i + 1][j2], rings[i + 1][j]))
            f.material_index = mat_idx
            f.smooth = True
            u0, u1 = j / sides, (j + 1) / sides
            f.loops[0][uv].uv = (u0, vs[i])
            f.loops[1][uv].uv = (u1, vs[i])
            f.loops[2][uv].uv = (u1, vs[i + 1])
            f.loops[3][uv].uv = (u0, vs[i + 1])


# Blattbuescheldaten (fest, LOD1 nimmt jeden zweiten)
clusters = []
crown_c = Vector((0, 0, 3.6 + (TARGET_H - 3.6) * 0.55))
for b in branches:
    if b['level'] < 3:
        continue
    pts = b['points']
    ts = [1.0] if b['level'] == 4 else [1.0, 0.55]
    for t in ts:
        p, tg, r = sample(b, t)
        clusters.append(p)
random.shuffle(clusters)
clusters = clusters[:780]
print('Blattbuescheldaten:', len(clusters))
tile_w = [0.36, 0.36, 0.12, 0.16]


def add_cluster(bm, leaf_idx, c, S, cards, uv_layer, lod):
    # Wedelrichtung: nach aussen und leicht hoch, mit Streuung
    out = Vector((c.x - 0.0, c.y - 0.0, 0.0))
    out = out.normalized() if out.length > 0.1 else Vector((1, 0, 0))
    for k in range(cards):
        f = (out * random.uniform(0.3, 1.0) + Vector((0, 0, random.uniform(0.15, 0.65)))
             + Vector((random.uniform(-0.8, 0.8), random.uniform(-0.8, 0.8), 0))).normalized()
        w = f.cross(Vector((random.uniform(-1, 1), random.uniform(-1, 1), random.uniform(-0.3, 0.3)))).normalized()
        if w.length < 0.1:
            w = Vector((1, 0, 0))
        tile = random.choices((0, 1, 2, 3), tile_w)[0]
        u0 = 0.5 * (tile % 2)
        v0 = 0.5 if tile < 2 else 0.0
        size = S * random.uniform(0.8, 1.15)
        sags = (0.0, 0.07, 0.28)
        row = []
        for s in range(3):
            t = s / 2.0
            base = c + f * (size * t) - Vector((0, 0, size * sags[s]))
            half = size * 0.5 * (1.0 if s == 0 else 1.0)
            vl = bm.verts.new(base - w * half)
            vr = bm.verts.new(base + w * half)
            leaf_idx.append(vl.index if False else None)
            row.append((vl, vr))
        for s in range(2):
            a, b2 = row[s], row[s + 1]
            face = bm.faces.new((a[0], a[1], b2[1], b2[0]))
            face.material_index = 2
            face.smooth = True
            vv0 = v0 + 0.5 * (s / 2.0)
            vv1 = v0 + 0.5 * ((s + 1) / 2.0)
            face.loops[0][uv_layer].uv = (u0, vv0)
            face.loops[1][uv_layer].uv = (u0 + 0.5, vv0)
            face.loops[2][uv_layer].uv = (u0 + 0.5, vv1)
            face.loops[3][uv_layer].uv = (u0, vv1)
        leaf_idx[-1] = None


def build(name, sides_main, sides_fine, cluster_step, S, cards):
    bm = bmesh.new()
    uvl = bm.loops.layers.uv.verify()
    for b in branches:
        lvl = b['level']
        if lvl == 0:
            # nur der Stamm im Slot 'trunk': Unity leitet daraus Stamm-Kapsel und Rinden-Tiling ab (Astwerk gehoert in 'branches')
            add_tube(bm, b, sides_main, 1, 4.0)
        elif lvl <= 2:
            add_tube(bm, b, sides_main if lvl <= 1 else max(6, sides_main - 4), 0, 4.0)
        else:
            if lvl == 4 and cluster_step > 1:
                add_tube(bm, b, 3, 0, 4.0)
            else:
                add_tube(bm, b, sides_fine, 0, 4.0)
    first_leaf = len(bm.verts)
    dummy = []
    random.seed(SEED + 100)
    for ci, c in enumerate(clusters):
        if ci % cluster_step:
            continue
        add_cluster(bm, dummy, c, S, cards, uvl, name)
    bm.verts.ensure_lookup_table()
    bm.verts.index_update()
    leaf_ids = set(range(first_leaf, len(bm.verts)))
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me)
    bm.free()
    for mname in ('jacaranda_tree_branches', 'jacaranda_tree_trunk', 'jacaranda_tree_leaves'):
        me.materials.append(bpy.data.materials[mname])
    # eigene Normalen fuer Blattkarten: weich nach aussen/oben (Kugelverlauf der Krone)
    normals = []
    for v in me.vertices:
        n = v.normal.copy()
        if v.index in leaf_ids:
            n = ((v.co - crown_c).normalized() * 0.7 + Vector((0, 0, 0.5))).normalized()
        normals.append(n)
    try:
        me.normals_split_custom_set_from_vertices(normals)
    except Exception as e:
        print('Custom-Normalen nicht moeglich:', e)
    ob = bpy.data.objects.new(name, me)
    bpy.context.scene.collection.objects.link(ob)
    return ob, len(me.polygons)


# Materialien (Namen = Slot-Namen laut Unity-Builder)
for mname, col in (('jacaranda_tree_branches', (0.28, 0.2, 0.14, 1)), ('jacaranda_tree_trunk', (0.3, 0.22, 0.16, 1)), ('jacaranda_tree_leaves', (0.2, 0.4, 0.15, 1))):
    m = bpy.data.materials.new(mname)
    m.diffuse_color = col

lod0, p0 = build('jacaranda_tree_LOD0', 14, 6, 1, 3.4, 3)
lod1, p1 = build('jacaranda_tree_LOD1', 8, 4, 2, 4.6, 2)
print('LOD0 Polygone %d, LOD1 Polygone %d' % (p0, p1))
for o in (lod0, lod1):
    d = o.dimensions
    print(o.name, 'Masse %.1f x %.1f x %.1f' % (d.x, d.y, d.z), 'Slots', [s.material.name for s in o.material_slots])

bpy.ops.object.select_all(action='DESELECT')
lod0.select_set(True)
lod1.select_set(True)
bpy.context.view_layer.objects.active = lod0
bpy.ops.export_scene.fbx(filepath=FBX_OUT, use_selection=True, object_types={'MESH'}, add_leaf_bones=False,
                         mesh_smooth_type='FACE', axis_forward='-Z', axis_up='Y', bake_anim=False, path_mode='AUTO', apply_scale_options='FBX_SCALE_UNITS', bake_space_transform=True)
print('FBX geschrieben:', FBX_OUT)

# ---------------------------------------------------------------- Vorschau (Workbench, Texturfarbe)
tex = bpy.data.images.load(argv[3]) if len(argv) > 3 else None
if tex:
    for mname in ('jacaranda_tree_leaves',):
        m = bpy.data.materials[mname]
        m.use_nodes = True
        nt = m.node_tree
        for n in list(nt.nodes):
            nt.nodes.remove(n)
        out = nt.nodes.new('ShaderNodeOutputMaterial')
        bsdf = nt.nodes.new('ShaderNodeBsdfPrincipled')
        img = nt.nodes.new('ShaderNodeTexImage')
        img.image = tex
        nt.links.new(img.outputs['Color'], bsdf.inputs['Base Color'])
        nt.links.new(img.outputs['Alpha'], bsdf.inputs['Alpha'])
        nt.links.new(bsdf.outputs['BSDF'], out.inputs['Surface'])
        m.blend_method = 'CLIP' if hasattr(m, 'blend_method') else None
lod1.hide_render = True
sc = bpy.context.scene
sc.render.engine = 'BLENDER_WORKBENCH'
sc.display.shading.light = 'STUDIO'
sc.display.shading.color_type = 'TEXTURE'
sc.render.resolution_x, sc.render.resolution_y = 1280, 960
sc.world = bpy.data.worlds.new('w')
sc.world.color = (0.62, 0.72, 0.85)
cam = bpy.data.objects.new('cam', bpy.data.cameras.new('cam'))
bpy.context.scene.collection.objects.link(cam)
sc.camera = cam
cam.data.lens = 35
for nm, loc, tgt in (('seite', (0, -52, 9), (0, 0, 9)), ('schraeg', (34, -34, 4), (0, 0, 9)), ('stamm', (6, -9, 2.2), (0, 0, 2.5)), ('oben', (0, -8, 40), (0, 0, 10))):
    cam.location = Vector(loc)
    cam.rotation_euler = (Vector(tgt) - cam.location).to_track_quat('-Z', 'Y').to_euler()
    sc.render.filepath = PREV + '\\vorschau_' + nm + '.png'
    bpy.ops.render.render(write_still=True)
print('FERTIG')
