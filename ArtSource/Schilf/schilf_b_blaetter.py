# MODELL11 Schilf Teil B: schmale lange Blaetter (6 Dreiecke je Blatt, Mittel-
# kiel/V-Querschnitt, Uebhaenger), besetzt an Knoten und Internodien der Halme.
# UV: 4 Blattzellen im Band V 0.62..0.95, V=Blattlaengsachse. Liest Profile aus
# builtins.SCHILF_STEMS (Teil A). Haengt sich an den bestehenden Schilf-Halm-Objekt an.
import bpy, bmesh, math, random
from mathutils import Vector

CELLS = ((0.03, 0.26), (0.27, 0.50), (0.51, 0.74), (0.75, 0.98))
LV0, LV1 = 0.62, 0.95   # Blatt-Band im UV-Atlas

def leaf_on(bm, base, radial, up, el, az_off, L, wmax, droop, seedf):
    """Ein Schilfblatt: steigt mit elevation el aus der Blattscheide, bogt nach
    aussen und ueberhaengt. 9 Vertices, 4 Faces (6 Dreiecke)."""
    uvl = bm.loops.layers.uv.verify() or bm.loops.layers.uv.new('UVMap')
    q = radial.cross(up).normalized()          # tangential
    dirv = (radial * math.sin(el) + up * math.cos(el)
            + q * math.sin(az_off) * 0.35).normalized()
    def pt(u):
        d = L * u
        return base + dirv * d - Vector((0, 0, droop * L * u * u))
    def wq(u):
        if u < 0.15:
            return wmax * (0.35 + 4.0 * u)     # Blattscheide laeuft zu
        return wmax * (1.0 - 0.75 * ((u - 0.15) / 0.85) ** 1.25)
    us = (0.30, 0.65, 0.90)
    v0 = bm.verts.new(pt(0))
    pairs = []
    for u in us:
        p = pt(u)
        w = wq(u)
        keel = -wmax * (0.42 if u == 0.30 else (0.18 if u == 0.65 else 0.04))
        pairs.append((bm.verts.new(p + q * w * 0.5 + Vector((0, 0, keel))),
                      bm.verts.new(p - q * w * 0.5 + Vector((0, 0, keel)))))
    v7 = bm.verts.new(pt(1.0) + Vector((0, 0, -droop * L * 0.02)))
    cell = CELLS[seedf % 4]
    cu = (cell[0] + cell[1]) * 0.5
    cw = cell[1] - cell[0]
    uvmap = {v0: (cu, LV0)}
    half = (0.45, 0.38, 0.16)
    vs = [v0]
    for i, (a, b) in enumerate(pairs):
        uvmap[a] = (cu + half[i] * cw, LV0 + (LV1 - LV0) * us[i])
        uvmap[b] = (cu - half[i] * cw, LV0 + (LV1 - LV0) * us[i])
        vs += [a, b]
    uvmap[v7] = (cu, LV1)
    a1, b1, a2, b2, a3, b3 = pairs[0][0], pairs[0][1], pairs[1][0], pairs[1][1], pairs[2][0], pairs[2][1]
    for f in (bm.faces.new((v0, b1, a1)),
              bm.faces.new((b1, b2, a2, a1)),
              bm.faces.new((b2, b3, a3, a2)),
              bm.faces.new((a3, b3, v7))):
        for lp in f.loops:
            lp[uvl].uv = uvmap[lp.vert]

# --- Besetzung: je Halm 6-8 Blaetter an Knoten/Internodien -------------------
import builtins
stems = builtins.SCHILF_STEMS
rng = random.Random(2903)
GOLD = 2.399963
bm_l = bmesh.new()
n_leaves = 0
for si, (pts, radii) in enumerate(stems):
    h = pts[-1][2]
    top = Vector(pts[-1])
    stem_up = (top - Vector(pts[0])).normalized()
    for li in range(8):   # D1: dichter (8 je Halm)
        t = (0.16 + 0.11 * li + rng.uniform(-0.03, 0.03))
        t = min(t, 0.92)
        fi = max(1, int(round(t * (len(pts) - 1))))
        base = Vector(pts[fi]) + Vector((0, 0, rng.uniform(-0.02, 0.02)))
        # D3: radial um den Halm verteilen (Goldwinkel) statt Auswuchs-Richtung
        az = li * GOLD + si * 1.7 + rng.uniform(-0.35, 0.35)
        radv = Vector((math.cos(az), math.sin(az), 0))
        az_off = rng.uniform(-0.8, 0.8)
        el = math.radians(rng.uniform(38, 62)) if t < 0.55 else math.radians(rng.uniform(18, 40))
        # D1: Mischung kurzen Innen- und langen Aussenblaettern statt Einheitslaenge
        L = (rng.uniform(0.16, 0.30) if rng.random() < 0.25 else rng.uniform(0.42, 0.82))
        L *= (1.12 - 0.40 * t)
        wmax = rng.uniform(0.016, 0.028) * (1.2 - 0.3 * t)  # D2: breitere Klingen (real 8-25 mm)
        droop = rng.uniform(0.45, 0.95)   # D1: weicher Bogen durch Eigengewicht
        leaf_on(bm_l, base, radv, stem_up, el, az_off, L, wmax, droop, si * 8 + li)
        n_leaves += 1

def link(name, bm):
    bm.normal_update()
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me)
    bm.free()
    ob = bpy.data.objects.new(name, me)
    bpy.context.collection.objects.link(ob)
    return ob

blaetter = link('Schilf_Blaetter', bm_l)
tris = sum(len(p.vertices) - 2 for p in blaetter.data.polygons)
print('Blaetter:', n_leaves, 'Dreiecke', tris)
