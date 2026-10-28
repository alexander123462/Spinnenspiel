"""MODELL22 (Baum_Klein02): LOD0/LOD1 aus Geruest- und Cluster-JSON bauen und als FBX exportieren.
Headless:  "C:/Program Files/Blender Foundation/Blender 5.1/blender.exe" -b -P ArtSource/Baum_Klein02/bau_mcp.py -- <fbx-ausgabe>
Das Geruest selbst entstand per Blender-MCP (Seed 2202, Stamm 6.8 m mit Jitter); die fertigen
Zweig-Polylinien liegen in geruest.json, die Blattcluster-Punkte in cluster.json. Slots:
0 = tree_small_02_trunk (Holz), 1 = tree_small_02_leaves (Blatt). Ursprung am Stammfuss, Z oben.
Endskalierung 0.756 -> ca. 6.35 m Asset-Hoehe (Builder-Scale 3 -> ~19 m im Spiel)."""
import bpy, bmesh, json, math, random, sys
from mathutils import Vector

HERE = r'C:\Users\vorau\dev\Spinnenspiel\ArtSource\Baum_Klein02'
FBX_OUT = sys.argv[-1] if len(sys.argv) > 1 and not sys.argv[-1].startswith('-') else HERE + r'\tree_small_02.fbx'
SKALA = 0.756

branches = json.load(open(HERE + r'\geruest.json'))
for b in branches:
    b['pts'] = [(Vector(p), r) for p, r in b['pts']]
    # geruest.json enthält bereits die Verbreiterung x1.45 (Stand nach Selbstkritik-Durchlauf 3).
    for i in range(len(b['pts'])):
        p, r = b['pts'][i]
        b['pts'][i] = (Vector((p.x * SKALA, p.y * SKALA, p.z * SKALA)), r * SKALA)
cluster_raw = json.load(open(HERE + r'\cluster.json'))
clusters = [(Vector((p[0] * SKALA, p[1] * SKALA, p[2] * SKALA)), lvl)
            for p, lvl in cluster_raw]
CEN = Vector((0, 0, 5.6)) * SKALA

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
            a = 2 * math.pi * k / sides + (0.25 if i % 2 else 0.0)
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
    edge = 1.0 + 0.75 * dnorm
    scale = {0: 1.0, 1: 1.0, 2: 0.85, 3: 0.6}[lvl] * edge * random.uniform(0.8, 1.2)
    n = (random.randint(8, 10) if lvl <= 1 else
         (random.randint(6, 8) if lvl == 2 else random.randint(5, 7)))
    L = random.uniform(0.24, 0.30) * scale * SKALA
    W = L * random.uniform(0.60, 0.75)
    for i in range(n):
        ph = random.uniform(0, 2 * math.pi)
        tilt = random.uniform(0.8, 1.7) if lvl > 0 else random.uniform(0.4, 1.4)
        up = Vector((0, 0, 1)); side = Vector((math.cos(ph), math.sin(ph), 0))
        dvec = (up * math.cos(tilt) + side * math.sin(tilt)).normalized()
        u = dvec.cross(up).normalized()
        if u.length < 1e-6:
            u = Vector((1, 0, 0))
        u = u.normalized() * W * 0.5
        curl = random.uniform(0.25, 0.5) * L
        for s in range(segs_leaf):
            t0 = s / segs_leaf; t1 = (s + 1) / segs_leaf
            def pt(t):
                p = c + dvec * (L * t)
                bend = (Vector((0, 0, -1)) * (curl * 0.55 * t * t)
                        + Vector((0, 0, 1)) * (curl * 0.25 * t * t)
                        + dvec.cross(Vector((0, 0, 1))) * curl * 0.2 * t * t)
                return p + bend
            p0 = pt(t0); p1 = pt(t1)
            w0 = (0.4 + 0.6 * math.sin(math.pi * (t0 + 0.12)))
            w1 = (0.4 + 0.6 * math.sin(math.pi * (t1 + 0.12)))
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

def build(name, coarse):
    bm = bmesh.new(); uvl = bm.loops.layers.uv.new('UVMap')
    for b in branches:
        add_tube(bm, b, sides_for(b, coarse), 0, uvl)
    random.seed(2203); ci = 0
    for c, lvl in clusters:
        if coarse and ci % 2:
            ci += 1; continue
        dnorm = (c - CEN).length / (5.2 * SKALA)
        add_cluster(bm, uvl, c, lvl, min(1.0, dnorm), 2 if coarse else 3)
        ci += 1
    me = bpy.data.meshes.new(name)
    me.materials.clear()
    me.materials.append(mat('tree_small_02_trunk'))
    me.materials.append(mat('tree_small_02_leaves'))
    bm.to_mesh(me); bm.free()
    ob = bpy.data.objects.new(name, me)
    bpy.context.scene.collection.objects.link(ob)
    tris = sum(len(p.vertices) - 2 for p in me.polygons)
    print(name, 'Tris', tris, 'Masse %.2f x %.2f x %.2f' % tuple(ob.dimensions))
    return ob

for o in list(bpy.data.objects):
    if o.type == 'MESH':
        bpy.data.objects.remove(o, do_unlink=True)
l0 = build('tree_small_02_LOD0', False)
l1 = build('tree_small_02_LOD1', True)
bpy.ops.object.select_all(action='DESELECT')
l0.select_set(True); l1.select_set(True)
bpy.context.view_layer.objects.active = l0
bpy.ops.export_scene.fbx(filepath=FBX_OUT, use_selection=True, object_types={'MESH'},
                         add_leaf_bones=False, mesh_smooth_type='FACE', axis_forward='-Z',
                         axis_up='Y', bake_anim=False, path_mode='AUTO',
                         apply_scale_options='FBX_SCALE_UNITS', bake_space_transform=True)
print('FBX geschrieben:', FBX_OUT)
