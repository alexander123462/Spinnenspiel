# TEXTUR1: UVs fuer Gras1 je Halm (Insel), V entlang des Halms, U ueber die Breite.
# Packung: Raster 6x5 Zellen, keine ueberlappenden Inseln. Zellenplan -> JSON.
import bpy, bmesh, heapq, json, math

COLS, ROWS, PAD = 6, 5, 0.006  # PAD in UV-Einheiten (ca. 6 px bei 1024)

ob = bpy.data.objects['Gras1']
me = ob.data
bm = bmesh.new()
bm.from_mesh(me)
bm.faces.ensure_lookup_table()
bm.verts.ensure_lookup_table()
bm.edges.ensure_lookup_table()

# Inseln = zusammenhaengende Flaeche
seen, islands = set(), []
for f in bm.faces:
    if f.index in seen:
        continue
    stack, comp = [f], []
    seen.add(f.index)
    while stack:
        c = stack.pop()
        comp.append(c)
        for e in c.edges:
            for lf in e.link_faces:
                if lf.index not in seen:
                    seen.add(lf.index)
                    stack.append(lf)
    islands.append(comp)


def dijkstra(verts):
    """Kantenlaengen-Laenge s(v) ab Basisvertex (tiefster Punkt)."""
    start = min(verts, key=lambda v: v.co.z)
    s = {v.index: math.inf for v in verts}
    s[start.index] = 0.0
    pq = [(0.0, start.index)]
    while pq:
        d, vi = heapq.heappop(pq)
        if d > s[vi]:
            continue
        for e in bm.verts[vi].link_edges:
            ov = e.other_vert(bm.verts[vi])
            if ov.index in s:
                nd = d + e.calc_length()
                if nd < s[ov.index]:
                    s[ov.index] = nd
                    heapq.heappush(pq, (nd, ov.index))
    return s


def spine_of(comp, verts, s):
    """Knickrueckgrat: Vertex mit >=4 Insel-Flaechen (vm-Spalte), nach s sortiert."""
    compset = {f.index for f in comp}
    vs = [v for v in verts
          if sum(1 for f in v.link_faces if f.index in compset) >= 4]
    return sorted(vs, key=lambda v: s[v.index])


def closest_on_poly(p, poly):
    best, bd = None, math.inf
    for a, b in zip(poly, poly[1:]):
        ab = b - a
        L2 = ab.length_squared or 1e-12
        t = max(0.0, min(1.0, (p - a).dot(ab) / L2))
        q = a + ab * t
        d = (p - q).length_squared
        if d < bd:
            best, bd = (q, ab.normalized() if ab.length else None), d
    return best


def island_uv(comp):
    verts = {v.index: v for f in comp for v in f.verts}
    s = dijkstra(list(verts.values()))
    smax = max(s.values()) or 1.0
    spine = spine_of(comp, list(verts.values()), s)
    poly = [v.co for v in spine]
    # Vorzeichen-Seite ueber Hauptbreite-Richtung (PCA der Offsets)
    offs = []
    for v in verts.values():
        if v.index in {sv.index for sv in spine}:
            continue
        q, _ = closest_on_poly(v.co, poly)
        offs.append((v.co - q))
    w = max(offs, key=lambda o: o.length).normalized() if offs else None
    signed = []
    for v in verts.values():
        q, _ = closest_on_poly(v.co, poly)
        d = (v.co - q)
        signed.append((v.index, math.copysign(d.length, d.dot(w)) if w else 0.0,
                       s[v.index] / smax))
    mx = max(abs(x[1]) for x in signed) or 1e-6
    return {vi: (0.5 + 0.5 * sd / mx, 0.0 if smax == 0 else vv)
            for vi, sd, vv in signed}


uvl = bm.loops.layers.uv.new('UVMap') if 'UVMap' not in bm.loops.layers.uv else bm.loops.layers.uv['UVMap']
cells = []
for k, comp in enumerate(islands):
    cx, cy = k % COLS, k // COLS
    cells.append((cx, cy))
    u0, v0 = cx / COLS + PAD, cy / ROWS + PAD
    cw, ch = 1.0 / COLS - 2 * PAD, 1.0 / ROWS - 2 * PAD
    uvs = island_uv(comp)
    for f in comp:
        for lp in f.loops:
            u, vv = uvs[lp.vert.index]
            lp[uvl].uv = (u0 + u * cw, v0 + vv * ch)

bm.to_mesh(me)
bm.free()
me.uv_layers.update()
print('Inseln', len(islands), 'UVs', len(me.uv_layers[0].data))
mins = [min(d.uv[i] for d in me.uv_layers[0].data) for i in (0, 1)]
maxs = [max(d.uv[i] for d in me.uv_layers[0].data) for i in (0, 1)]
print('UV-Bereich', [round(x, 4) for x in mins], [round(x, 4) for x in maxs])
with open(r'C:/Users/vorau/dev/Spinnenspiel/ArtSource/Gras1/zellen.json', 'w') as fh:
    json.dump({'cols': COLS, 'rows': ROWS, 'pad': PAD,
               'cells': cells}, fh)
print('Zellenplan gespeichert')
