# MODELL23 (Baum_Island01): baut LOD0/LOD1 aus geruest.json + cluster.json.
# Nutzung in Blender: exec(open(PFAD).read()), dann export_fbx(pfad) oder build().
# Adapter-Rohre fuer Holz (Slot 0 = island_tree_01_trunk), Lanzenblatt-Streifen
# fuer Laub (Slot 1 = island_tree_01_leaves). Ursprung am Stammfuss, Z oben.
import bpy, bmesh, json, math, random
from mathutils import Vector

HERE = r'C:\Users\vorau\dev\Spinnenspiel\ArtSource\Baum_Island01'

def load_data():
    branches = json.load(open(HERE + r'\geruest.json'))
    for b in branches:
        b['pts'] = [(Vector(p[:3]), p[3]) for p in b['pts']]
    clusters = [(Vector(p), lvl) for p, lvl in json.load(open(HERE + r'\cluster.json'))]
    return branches, clusters

def perp_basis(t):
    t = t.normalized()
    up = Vector((0, 0, 1)) if abs(t.z) < 0.95 else Vector((1, 0, 0))
    u = t.cross(up).normalized(); v = u.cross(t).normalized()
    return u, v

def add_tube(bm, b, sides, mat_idx, uvl):
    pts = b['pts']; rings = []
    for i, (p, r) in enumerate(pts):
        t_dir = pts[min(i + 1, len(pts) - 1)][0] - pts[max(i - 1, 0)][0]
        u, v = perp_basis(t_dir); ring = []
        for k in range(sides):
            a = 2 * math.pi * k / sides + (0.08 if i % 2 else 0.0)
            ring.append(bm.verts.new(p + (u * math.cos(a) + v * math.sin(a)) * r))
        rings.append(ring)
    nseg = len(rings) - 1
    for i in range(nseg):
        for k in range(sides):
            k2 = (k + 1) % sides
            f = bm.faces.new([rings[i][k], rings[i][k2], rings[i + 1][k2], rings[i + 1][k]])
            s = i / nseg; e = (i + 1) / nseg
            f.material_index = mat_idx; f.smooth = True
            us = (k / sides, (k + 1) / sides, (k + 1) / sides, k / sides); vs = (s, s, e, e)
            for loop, cc in zip(f.loops, zip(us, vs)):
                loop[uvl].uv = cc
    try:
        bm.faces.new(list(reversed(rings[0]))).material_index = mat_idx
        bm.faces.new(rings[-1]).material_index = mat_idx
    except ValueError:
        pass

def add_cluster(bm, uvl, c, lvl, dnorm, segs_leaf):
    # Lanzenblaetter (Insel-Baum-Vorbild): schmal und lang, stark groessenvariiert,
    # Blattspreizung meist waagerecht bis haengend (Schirmcharakter).
    edge = 1.0 + 0.7 * dnorm
    scale = {0: 1.0, 1: 1.0, 2: 0.9, 3: 0.65}[lvl] * edge * random.uniform(0.7, 1.35)
    n = (random.randint(12, 15) if lvl <= 1 else
         (random.randint(9, 12) if lvl == 2 else random.randint(7, 9)))
    L = random.uniform(0.62, 0.8) * scale
    W = L * random.uniform(0.30, 0.42)
    for i in range(n):
        ph = random.uniform(0, 2 * math.pi)
        # Schirm-Charakter: Blattspreizung waagerecht bis haengend (1.2-2.1 rad
        # aus der Vertikalen), nicht nach oben abstrahlend.
        tilt = random.uniform(1.2, 2.1) if lvl > 0 else random.uniform(1.0, 1.9)
        up = Vector((0, 0, 1)); side = Vector((math.cos(ph), math.sin(ph), 0))
        dvec = (up * math.cos(tilt) + side * math.sin(tilt)).normalized()
        u = dvec.cross(up).normalized()
        if u.length < 1e-6:
            u = Vector((1, 0, 0))
        u = u.normalized() * W * 0.5
        curl = random.uniform(0.35, 0.6) * L
        for s in range(segs_leaf):
            t0 = s / segs_leaf; t1 = (s + 1) / segs_leaf
            def pt(t):
                p = c + dvec * (L * t)
                bend = (Vector((0, 0, -1)) * (curl * 0.6 * t * t)
                        + dvec.cross(Vector((0, 0, 1))) * curl * 0.18 * t * t)
                return p + bend
            p0 = pt(t0); p1 = pt(t1)
            w0 = math.sin(math.pi * min(1.0, 0.16 + t0 * 0.84)) ** 0.55
            w1 = math.sin(math.pi * min(1.0, 0.16 + t1 * 0.84)) ** 0.55
            v0a = bm.verts.new(p0 - u * w0); v0b = bm.verts.new(p0 + u * w0)
            v1a = bm.verts.new(p1 - u * w1); v1b = bm.verts.new(p1 + u * w1)
            try:
                f = bm.faces.new([v0a, v0b, v1b, v1a])
            except ValueError:
                continue
            f.material_index = 1; f.smooth = True
            us = (0, 1, 1, 0); vs = (t0, t0, t1, t1)
            for loop, cc in zip(f.loops, zip(us, vs)):
                loop[uvl].uv = cc

def sides_for(b, coarse):
    return ({0: 12, 1: 8, 2: 6, 3: 4} if not coarse else {0: 8, 1: 5, 2: 4, 3: 3})[b['level']]

def mat(name):
    return bpy.data.materials.get(name) or bpy.data.materials.new(name)

def build(name, coarse, branches, clusters, only_levels=None):
    bm = bmesh.new(); uvl = bm.loops.layers.uv.new('UVMap')
    for b in branches:
        if only_levels is not None and b['level'] not in only_levels:
            continue
        add_tube(bm, b, sides_for(b, coarse), 0, uvl)
    random.seed(2303); ci = 0
    cen = Vector((0, 0, 10.6))
    for c, lvl in clusters:
        if coarse and ci % 2:
            ci += 1; continue
        dnorm = (c - cen).length / 5.6
        add_cluster(bm, uvl, c, lvl, min(1.0, dnorm), 2 if coarse else 3)
        ci += 1
    me = bpy.data.meshes.new(name)
    me.materials.clear()
    me.materials.append(mat('island_tree_01_trunk'))
    me.materials.append(mat('island_tree_01_leaves'))
    bm.to_mesh(me); bm.free()
    ob = bpy.data.objects.new(name, me)
    bpy.context.scene.collection.objects.link(ob)
    tris = sum(len(p.vertices) - 2 for p in me.polygons)
    print(name, 'Tris', tris, 'Masse %.2f x %.2f x %.2f' % tuple(ob.dimensions))
    return ob

def export_fbx(path, objs):
    bpy.ops.object.select_all(action='DESELECT')
    for o in objs:
        o.select_set(True)
    bpy.context.view_layer.objects.active = objs[0]
    bpy.ops.export_scene.fbx(filepath=path, use_selection=True, object_types={'MESH'},
                             add_leaf_bones=False, mesh_smooth_type='FACE', axis_forward='-Z',
                             axis_up='Y', bake_anim=False, path_mode='AUTO',
                             apply_scale_options='FBX_SCALE_UNITS', bake_space_transform=True)
    print('FBX geschrieben:', path)


