# MODELL11 Schilf Teil C: zwei braune Rohrkolben (Zylinder 8-seitig, Kap-Kappen)
# an den zwei hoechsten Halmen + Join zu EINEM Objekt 'Schilf' mit EINEM Slot
# MAT_Reed. UV: Kolben-Streifen V 0.03..0.26, Kappen-Insel U 0.93..0.98.
import bpy, bmesh, math, random
from mathutils import Vector

SPK0, SPK1 = 0.03, 0.26          # Kolben-Streifen (V)
CAP_U, CAP_V = (0.93, 0.98), (0.03, 0.07)

import builtins
stems = builtins.SCHILF_STEMS
tall = sorted(range(len(stems)), key=lambda i: -stems[i][0][-1][2])[:3]

def spike(bm, pts, t0=0.78, t1=0.95, r=0.0115, sides=8, segs=4):
    n = len(pts) - 1
    def along(t):
        fi = min(n - 1, int(t * n))
        u = t * n - fi
        return (Vector(pts[fi]) * (1 - u) + Vector(pts[fi + 1]) * u)
    axis = (along(t1) - along(t0)).normalized()
    up = Vector((0, 0, 1)) if abs(axis.z) < 0.9 else Vector((1, 0, 0))
    x1 = axis.cross(up).normalized()
    y1 = axis.cross(x1).normalized()
    uvl = bm.loops.layers.uv.verify() or bm.loops.layers.uv.new('UVMap')
    rings = []
    for i in range(segs + 1):
        t = t0 + (t1 - t0) * i / segs
        p = along(t)
        rr = r * (1.0 - 0.18 * abs(i / segs - 0.45))
        ring = [bm.verts.new(p + x1 * math.cos(2 * math.pi * s / sides) * rr
                             + y1 * math.sin(2 * math.pi * s / sides) * rr)
                for s in range(sides)]
        rings.append(ring)
    for i in range(segs):
        v0 = SPK0 + (SPK1 - SPK0) * i / segs
        v1 = SPK0 + (SPK1 - SPK0) * (i + 1) / segs
        for s in range(sides):
            f = bm.faces.new((rings[i][s], rings[i][(s + 1) % sides],
                              rings[i + 1][(s + 1) % sides], rings[i + 1][s]))
            u0 = s / sides
            u1 = 1.0 if s + 1 == sides else (s + 1) / sides
            for k, uv in enumerate(((u0, v0), (u1, v0), (u1, v1), (u0, v1))):
                f.loops[k][uvl].uv = uv
    for ring, top in ((rings[-1], True), (rings[0], False)):
        c = bm.verts.new(along(t1 if top else t0) + axis * (0.012 if top else -0.004))
        for s in range(sides):
            f = bm.faces.new((c, ring[s], ring[(s + 1) % sides]) if top
                             else (c, ring[(s + 1) % sides], ring[s]))
            for lp in f.loops:
                lp[uvl].uv = (CAP_U[0] + CAP_U[1]) * 0.5, (CAP_V[0] + CAP_V[1]) * 0.5

def link(name, bm):   # eigenstaendig (exec-Namespace pro MCP-Aufruf frisch)
    bm.normal_update()
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me)
    bm.free()
    ob = bpy.data.objects.new(name, me)
    bpy.context.collection.objects.link(ob)
    return ob

bm_k = bmesh.new()
# D3: Kolben dicker (real 2-3 cm Durchmesser) und laenger, damit sie lesbar sind
spk_cfg = [(0.76, 0.96, 0.016), (0.70, 0.90, 0.014), (0.80, 0.98, 0.012)]
for rank, i in enumerate(tall):
    t0, t1, r = spk_cfg[rank]
    spike(bm_k, stems[i][0], t0=t0, t1=t1, r=r)
kolben = link('Schilf_Kolben', bm_k)

# --- Join zu EINEM Objekt mit EINEM Material-Slot ----------------------------
bpy.ops.object.select_all(action='DESELECT')
for nm in ('Schilf_Halme', 'Schilf_Blaetter', 'Schilf_Kolben'):
    bpy.data.objects[nm].select_set(True)
bpy.context.view_layer.objects.active = bpy.data.objects['Schilf_Halme']
bpy.ops.object.join()
joined = bpy.context.view_layer.objects.active
joined.name = 'Schilf'
joined.data.name = 'Schilf'
mat = bpy.data.materials.get('MAT_Reed') or bpy.data.materials.new('MAT_Reed')
mat.use_nodes = True
joined.data.materials.clear()
joined.data.materials.append(mat)

tris = sum(len(p.vertices) - 2 for p in joined.data.polygons)
vs = joined.data.vertices
xs = [v.co.x for v in vs]; ys = [v.co.y for v in vs]; zs = [v.co.z for v in vs]
print('SCHILF gesamt: Dreiecke', tris, 'Slots', len(joined.data.materials),
      'Hoehe', round(max(zs), 3), 'min z', round(min(zs), 4),
      'Breite x', round(max(xs) - min(xs), 3), 'y', round(max(ys) - min(ys), 3))
