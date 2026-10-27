# MODELL11 Schilf Teil A: Halme mit Knoten (Sechskant-Rohr, Doppelringe an den
# Nodien), 5 Halme 1.15-1.55 m, Wurzel-Anlauf am Fuss. UV: U=Umfang, V=Laenge
# im Stamm-Band 0.30..0.52 des Atlas. Szene wird vorher geleert.
import bpy, bmesh, math, random
from mathutils import Vector

for o in list(bpy.data.objects):
    m = o.data
    bpy.data.objects.remove(o, do_unlink=True)
    if m and isinstance(m, bpy.types.Mesh) and m.users == 0:
        bpy.data.meshes.remove(m)

STEM0, STEM1 = 0.30, 0.52   # Stamm-Band im UV-Atlas (V)

def tube_uv(bm, pts, radii, sides=6, v0=0.0, v1=1.0):
    """Rohr aus Profilkreisen; UV: U=Umfang 0..1, V=v0..v1 entlang der Laenge."""
    rings = []
    for i, p in enumerate(pts):
        p = Vector(p)
        tang = (Vector(pts[min(i + 1, len(pts) - 1)]) - Vector(pts[max(i - 1, 0)]))
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
            ring.append(bm.verts.new(p + x1 * math.cos(a) * r + y1 * math.sin(a) * r))
        rings.append(ring)
    uvl = bm.loops.layers.uv.verify() or bm.loops.layers.uv.new('UVMap')
    n = len(rings) - 1
    for i in range(n):
        t0 = v0 + (v1 - v0) * i / n
        t1 = v0 + (v1 - v0) * (i + 1) / n
        for s in range(sides):
            f = bm.faces.new((rings[i][s], rings[i][(s + 1) % sides],
                              rings[i + 1][(s + 1) % sides], rings[i + 1][s]))
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

# --- Halme ------------------------------------------------------------------
rng = random.Random(1711)
GOLD = 2.399963
stem_data = []   # (pts, radii) fuer Blatt-/Kolben-Ansaetze in Teil B/C
for k in range(6):   # D3: sechster Halm gegen duenne Silhouette
    az = rng.uniform(0, 2 * math.pi) + k * GOLD * 0.37
    lean = rng.uniform(0.06, 0.18)      # D2: mehr Neigung/Spreizung (Gewicht oben)
    h = rng.uniform(1.05, 1.65)         # D2: groesserer Hohenmix
    r0 = rng.uniform(0.0038, 0.0052)
    base = rng.uniform(0.04, 0.13)      # D2: breiterer Horst-Fuss
    ts = [0.0, 0.22, 0.26, 0.48, 0.52, 0.74, 0.78, 1.0]
    node_idx = {1, 3, 5}          # Doppelring: Knoten-Unterhang vor dem Sprung
    pts, radi = [], []
    for i, t in enumerate(ts):
        z = h * t - (0.008 if i == 0 else 0.0)
        rad_off = (base + r0 + lean * h * t ** 1.7) * 0.9
        x = math.cos(az) * rad_off
        y = math.sin(az) * rad_off
        pts.append((x, y, z))
        r = r0 * (1.0 - 0.55 * t) + 0.0012
        if i == 0:
            r *= 2.3               # Wurzel-Anlauf
        elif i in node_idx:
            r *= 1.32              # Knoten-Verdickung
        radi.append(r)
    stem_data.append((pts, radi))

bm_h = bmesh.new()
for pts, radi in stem_data:
    tube_uv(bm_h, pts, radi, sides=6, v0=STEM0, v1=STEM1)
halme = link('Schilf_Halme', bm_h)

tris = sum(len(p.vertices) - 2 for p in halme.data.polygons)
zs = [v.co.z for v in halme.data.vertices]
print('Halme: Dreiecke', tris, 'Hoehe', round(max(zs), 3), 'min z', round(min(zs), 4))
import builtins
builtins.SCHILF_STEMS = stem_data   # Teil B/C lesen die Profile aus dem laufenden Blender
