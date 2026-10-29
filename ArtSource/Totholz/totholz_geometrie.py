# MODELL16: Totholz-Staemme (3 Varianten) fuer Unity. Laeuft IN Blender
# (per MCP execute_blender_code ausgefuehrt): baut Totholz_1..3 als je ein Mesh
# mit 2 Material-Slots (0 = Borke, 1 = Holz/Schnittflaeche), UVs inklusive.
# Vorgabe: 1.5-3 m lang, Borke mit Rissen (laengsseitige Furchen + Quetschbeulen),
# abgebrochene Aeste (Stummel mit Splitterende), Bruchenden mit Holz-Sicht,
# 2000-5000 Dreiecke je Variante. Ursprung: Mitte x, Fuss bei z=0, Achse entlang X.
import bpy, bmesh, math, random
from mathutils import Vector, Matrix

SLOT_BARK = 0
SLOT_WOOD = 1

def _hash2(a, b, seed):
    """Deterministisches Pseudo-Rauschen 0..1 (周期 in a ueber Ganzzahligkeit)."""
    x = math.sin((a * 127.1 + b * 311.7 + seed * 74.7)) * 43758.5453
    return x - math.floor(x)

def _vnoise(t, u, seed):
    """Weich interpoliertes Wert-Rauschen auf (t,u), u periodisch in 0..1."""
    import math as _m
    i0 = int(_m.floor(t)); f0 = t - i0; j0 = int(u); f1 = u - j0
    def g(i, j):
        return _hash2(i, j % 64, seed)
    s0 = f0 * f0 * (3 - 2 * f0); s1 = f1 * f1 * (3 - 2 * f1)
    a = g(i0, j0) * (1 - s0) + g(i0 + 1, j0) * s0
    b = g(i0, j0 + 1) * (1 - s0) + g(i0 + 1, j0 + 1) * s0
    return a * (1 - s1) + b * s1

def pnoise1(t, cells, seed):
    """Wert-Rauschen 0..1, t-stetig; cells Ganzzahlen -> ueber 0..1 periodisch."""
    i0 = int(math.floor(t)); f = t - i0
    a = _hash2(i0 % cells, seed, 0); b = _hash2((i0 + 1) % cells, seed, 0)
    s = f * f * (3 - 2 * f)
    return a + (b - a) * s

def pnoise2(u, v, nu, nv, seed):
    """2D-Wert-Rauschen; u-achse periodisch ueber nu Zellen ( fuer Zylinder)."""
    iu = u * nu; iv = v * nv
    i0 = int(math.floor(iu)); j0 = int(math.floor(iv))
    fu = iu - i0; fv = iv - j0
    su = fu * fu * (3 - 2 * fu); sv = fv * fv * (3 - 2 * fv)
    def g(i, j):
        return _hash2(i % nu, j * 13 + seed, seed)
    a = g(i0, j0) * (1 - su) + g(i0 + 1, j0) * su
    b = g(i0, j0 + 1) * (1 - su) + g(i0 + 1, j0 + 1) * su
    return a + (b - a) * sv

def fbm2(u, v, nu0, nv0, seed, octaves=3):
    tot = 0.0; amp = 1.0; ntot = 0.0
    for o in range(octaves):
        tot += amp * pnoise2(u, v, nu0 << o, nv0 << o, seed + 7 * o)
        ntot += amp; amp *= 0.5
    return tot / ntot

def trunk(bm, uv, L, r_base, r_tip, sides, rings, seed):
    """Stamm entlang X (x=-L/2..L/2), Fuss bei z=0. Borke: laengsseitige
    Furchen (Risse) + grosse Beulen. Randringe an den Enden fuer Bruch-
    Splitter. Rueckgabe (boundary, r_at, uv_scale)."""
    v_scale = L / max(0.5 * (r_base + r_tip), 0.01)
    ring_idx = []
    for r in range(rings + 1):
        t = r / rings
        rc = r_base + (r_tip - r_base) * t
        cx = -L / 2 + L * t
        bend_z = L * 0.055 * math.sin(math.pi * (t + 0.15 * pnoise1(t, 3, seed + 1)))
        bend_y = L * 0.03 * (fbm2(t, 0.3, 4, 1, seed + 2) - 0.5)
        cy = bend_y
        cz = rc + bend_z
        # Wurzelanlauf: Basis dicker (nur erstes Zehntel).
        if t < 0.12:
            rc *= 1 + 0.25 * (1 - t / 0.12) ** 2
        vs = []
        for s in range(sides):
            a = s / sides
            phi = a * 2 * math.pi
            nbeul = fbm2(a, t, 8, 10, seed + 3) - 0.5
            fmod = 0.6 + 0.8 * pnoise1(t, 7, seed + 4)
            fur = abs(math.sin(a * math.pi * fmod * 2))
            ridge = 0.09 * (1.0 - fur) ** 2          # First der Rindenbretter
            crack = -0.16 * (1.0 - fur) ** 5         # tiefer Riss dazwischen
            quers = 0.05 * math.sin(phi * 3 + 1.7 * pnoise1(t, 5, seed + 9))                   + 0.03 * math.sin(phi * 5 - 0.8) * pnoise1(t, 3, seed + 10)
            rr = rc * (1 + 0.13 * nbeul + quers + ridge + crack)
            # Feines Schuppen-Relief: Radius springt pro Umfangs-/Laengenzelle.
            rr *= 1 + 0.09 * (_vnoise(s / sides * 6, t * rings / 3.0, seed + 70) - 0.5)
            y = cy + rr * math.sin(phi)
            z = cz + rr * math.cos(phi)
            vs.append(bm.verts.new((cx, y, z)))
        ring_idx.append(vs)
    for r in range(rings):
        t0 = r / rings; t1 = (r + 1) / rings
        for s in range(sides):
            s2 = (s + 1) % sides
            f = bm.faces.new((ring_idx[r][s], ring_idx[r][s2],
                              ring_idx[r + 1][s2], ring_idx[r + 1][s]))
            f.material_index = SLOT_BARK
            # UV: u = Umfang (0..1, Naht bei s=0), v = Laenge in r-Einheiten.
            for loop, ss, tt in ((f.loops[0], s, t0), (f.loops[1], s2, t0),
                                 (f.loops[2], s2, t1), (f.loops[3], s, t1)):
                loop[uv].uv = (ss / sides, tt * v_scale)
    return ring_idx

def jagged_end(bm, ring, center, r_end, seed, axis):
    """Endring entlang axis zu Splittern versetzen (Bruchkante)."""
    ax = Vector(axis); n = len(ring)
    cy = sum(v.co.y for v in ring) / n
    cz = sum(v.co.z for v in ring) / n
    for s, v in enumerate(ring):
        a = s / n
        spl = pnoise1(a * 2, 16, seed + 20)          # 16 Splitter ueber Umfang
        v.co = v.co + ax * r_end * (0.05 + 0.85 * spl ** 1.2)  # tiefe Splitter
        # Radiale Zacken: Rand springt quer zur Faser -> gezackte Silhouette.
        d = Vector((0, v.co.y - cy, v.co.z - cz))
        tiefe = 0.55 * pnoise1(a * 2 + 0.5, 8, seed + 24)               + 0.30 * pnoise1(a * 2 + 1.3, 24, seed + 25)   # 2 Frequenzen
        if d.length > 1e-6:
            v.co += d.normalized() * (r_end * tiefe)
    return ring

def cap_face(bm, uv, ring, cx, r_end, seed, sign):
    """Bruchflaeche mit zwei Zacken-Ringen (aussen + innen, gegenphaesig) und
    Faecher zum Zentrum: liest als faseriger Bruch, nicht als Saegenschnitt.
    Slot HOLZ, UV = Scheibe um Achse."""
    n = len(ring)
    cy = sum(v.co.y for v in ring) / n
    cz = sum(v.co.z for v in ring) / n
    axv = Vector((sign, 0, 0))
    # Innerer Rand: 0.78*r nach innen, Gegen-Phasen-Zacken entlang Achse.
    inner = []
    for s, v in enumerate(ring):
        a = s / n
        d = Vector((0, v.co.y - cy, v.co.z - cz))
        f = 0.78 - 0.10 * pnoise1(a * 2 + 3.1, 6, seed + 26)
        p = Vector((v.co.x, cy + d.y * f, cz + d.z * f))
        p += axv * r_end * (0.05 + 0.45 * pnoise1(a * 2 + 7.7, 20, seed + 27)
                            - 0.25)
        inner.append(bm.verts.new(p))
    c = bm.verts.new((cx + sign * 0.02 * r_end, cy, cz))

    def _uv(loop, p):
        loop[uv].uv = (min(max(0.5 + (p.y - cy) / (2.6 * r_end), 0.0), 1.0),
                       min(max(0.5 + (p.z - cz) / (2.6 * r_end), 0.0), 1.0))

    rng = list(range(n)) if sign > 0 else list(range(n - 1, -1, -1))
    for s in rng:
        s2 = (s + 1) % n
        # Aussenring -> Innenring (Faserstufe, Holz).
        f0 = bm.faces.new((ring[s], ring[s2], inner[s2], inner[s]))
        f0.material_index = SLOT_WOOD
        for loop in f0.loops:
            _uv(loop, loop.vert.co)
        # Innenring -> Zentrum (Scheibe mit Jahresringen).
        f = bm.faces.new((c, inner[s], inner[s2])) if sign > 0 else \
            bm.faces.new((c, inner[s2], inner[s]))
        f.material_index = SLOT_WOOD
        for loop in f.loops:
            _uv(loop, loop.vert.co)
    return c

def stub(bm, uv, base, axis, L, r0, r1, seed, rings=5, sides=9):
    """Abgebrochener Aststummel: Kette verjuengter Ringe entlang axis (basis-
    nahez Ende in der Stammoberflaeche vergraben), Splitterende + Holz-Cap.
    axis muss normiert sein. UV: u Umfang, v Laenge/r0."""
    ax = Vector(axis); ax.normalize()
    up = Vector((1, 0, 0)) if abs(ax.x) < 0.7 else Vector((0, 1, 0))
    e1 = ax.cross(up); e1.normalize()
    e2 = ax.cross(e1); e2.normalize()
    v_scale = L / max(r0, 0.01)
    start = Vector(base) - ax * 0.5 * r0            # Sockel in Rinde versenken
    ring = []
    bend = 0.35 * (pnoise1(0.13, 4, seed + 32) - 0.5)      # Kruemmung ueber Stummel
    bdir = e1 * math.cos(seed) + e2 * math.sin(seed)
    for r in range(rings + 1):
        t = r / rings
        rr = r0 + (r1 - r0) * t
        c_off = bdir * (bend * L * t * t)                  # sanfter Bogen
        rr *= 1 + 0.10 * (pnoise1(t, 4, seed + 30) - 0.5)
        if t < 0.25:                                  # Kollar-Wulst am Ansatz
            rr *= 1 + 0.55 * (1 - t / 0.25) ** 2
        c = start + ax * (L * t) + c_off
        vs = []
        for s in range(sides):
            phi = s / sides * 2 * math.pi
            wob = 1 + 0.06 * math.sin(phi * 3 + seed) * t
            vs.append(bm.verts.new(c + (e1 * math.cos(phi) + e2 * math.sin(phi)) * rr * wob))
        ring.append(vs)
    for r in range(rings):
        t0 = r / rings; t1 = (r + 1) / rings
        for s in range(sides):
            s2 = (s + 1) % sides
            f = bm.faces.new((ring[r][s], ring[r + 1][s], ring[r + 1][s2], ring[r][s2]))
            f.material_index = SLOT_BARK
            for loop, ss, tt in ((f.loops[0], s, t0), (f.loops[1], s, t1),
                                 (f.loops[2], s2, t1), (f.loops[3], s2, t0)):
                loop[uv].uv = (ss / sides, tt * v_scale)
    jagged_end(bm, ring[-1], 0.0, r1, seed + 31, ax)
    # Sockeloeffnung (im Stamm versenkt) mit Rinden-Disc schliessen -> wasserdicht.
    n = len(ring[0])
    c0 = bm.verts.new(Vector(start))
    for s in range(n):
        f = bm.faces.new((c0, ring[0][(s + 1) % n], ring[0][s]))
        f.material_index = SLOT_BARK
        for loop in f.loops:
            d = loop.vert.co - Vector(start)
            loop[uv].uv = (0.5 + d.dot(e1) / (2 * r0), 0.5 + d.dot(e2) / (2 * r0))
    return ring, e1, e2, ax

def cap_stub(bm, uv, ring, center, r_end, e1, e2):
    """Splitterende-Flaeche des Stummels; UV = Scheibe um Achse."""
    n = len(ring)
    c = bm.verts.new(Vector(center))
    for s in range(n):
        # Ring-Kante Ruewaerts: gleiche Richtung wie die Stummel-Quads, sonst
        # entstehen 9 Doppelkanten (Randkanten) pro Cap.
        f = bm.faces.new((c, ring[(s + 1) % n], ring[s]))
        f.material_index = SLOT_WOOD
        for loop in f.loops:
            d = loop.vert.co - Vector(center)
            loop[uv].uv = (0.5 + d.dot(e1) / (2.4 * r_end), 0.5 + d.dot(e2) / (2.4 * r_end))
    return c

VARIANTS = [
    # name, L, r_base, r_tip, sides, rings, seed, stubs[(t, phi_deg, L, r0)]
    ("Totholz_1", 1.50, 0.150, 0.120, 26, 40, 11,
     [(0.25, 70, 0.44, 0.062), (0.68, 110, 0.38, 0.052)]),
    ("Totholz_2", 2.05, 0.190, 0.150, 30, 54, 22,
     [(0.20, 55, 0.48, 0.070), (0.55, 100, 0.38, 0.055), (0.80, 35, 0.30, 0.042)]),
    ("Totholz_3", 2.60, 0.230, 0.175, 32, 60, 33,
     [(0.15, 65, 0.55, 0.080), (0.48, 115, 0.44, 0.062), (0.66, 25, 0.34, 0.048)]),
]

def build_variant(name, L, r_base, r_tip, sides, rings, seed, stubs):
    me = bpy.data.meshes.new(name)
    ob = bpy.data.objects.new(name, me)
    bpy.context.collection.objects.link(ob)
    bm = bmesh.new()
    uv = bm.loops.layers.uv.new("UVMap")
    ring_idx = trunk(bm, uv, L, r_base, r_tip, sides, rings, seed)
    rc = [r_base + (r_tip - r_base) * (r / rings) for r in range(rings + 1)]
    # Bruchende mit Splittern an BEIDEN Enden, dann Holz-Caps.
    jagged_end(bm, ring_idx[0], 0.0, rc[0], seed + 40, (-1, 0, 0))
    jagged_end(bm, ring_idx[-1], 0.0, rc[-1], seed + 41, (1, 0, 0))
    cap_face(bm, uv, ring_idx[0], -L / 2, rc[0], seed + 42, -1)
    cap_face(bm, uv, ring_idx[-1], L / 2, rc[-1], seed + 43, +1)
    for k, (t, phi_deg, sl, sr0) in enumerate(stubs):
        ri = min(int(t * rings), rings - 1)
        cx = -L / 2 + L * (ri / rings)
        rcx = rc[ri]
        phi = math.radians(phi_deg)
        cy = rcx * math.sin(phi); cz = rcx * math.cos(phi)
        base = Vector((cx, cy, cz))
        radial = Vector((0, math.sin(phi), math.cos(phi)))
        tilt = 0.45 + 0.45 * pnoise1(k * 0.37, 8, seed + 50)   # meist nach oben/saewerts
        axis = (radial * math.cos(tilt) + Vector((0, 0, math.sin(tilt)))).normalized()
        ring, e1, e2, ax = stub(bm, uv, base, axis, sl, sr0, sr0 * 0.72, seed + 60 + k)
        endc = sum((v.co for v in ring[-1]), Vector()) / len(ring[-1])
        cap_stub(bm, uv, ring[-1], endc, sr0 * 0.72, e1, e2)
    bmesh.ops.remove_doubles(bm, verts=bm.verts[:], dist=0.0015)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    # Fuss auf z=0 setzen (Tiefster Punkt incl. Rindenrisse und Stummel).
    zmin = min(v.co.z for v in bm.verts)
    for v in bm.verts:
        v.co.z -= zmin
    # Orientierung: Stammachse entlang Blender-X, Fuss auf Blender-Z=0.
    # Blender-Z wird in Unity Y (senkrecht): der Stamm liegt wie die alten
    # M_Log0..2 quer; der Generator dreht per rotY und versenkt 0.05 m.
    bm.to_mesh(me); bm.free()
    # Glattes Shading mit Aut-smooth: Kanten unter 35 Grad weich (weniger Facetten).
    for p in me.polygons:
        p.use_smooth = True
    # benachbarte Polygone pro Kante sammeln (MeshEdge.link_faces gibt es in 5.1 nicht)
    nachbar = {}
    for poly in me.polygons:
        for ek in poly.edge_keys:
            nachbar.setdefault(tuple(sorted(ek)), []).append(poly.index)
    cos35 = math.cos(math.radians(35))
    for idxs in nachbar.values():
        if len(idxs) == 2:
            a, b = me.polygons[idxs[0]], me.polygons[idxs[1]]
            if a.normal.dot(b.normal) < cos35:
                a.use_smooth = False
                b.use_smooth = False
    return ob

def _img(path):
    import os
    if os.path.exists(path):
        try:
            return bpy.data.images.load(path, check_existing=True)
        except Exception as e:
            print('Bildfehler', path, e)
            return None
    return None

def make_mats(texdir):
    for im in list(bpy.data.images):
        if "Totholz" in (im.filepath or ""):
            bpy.data.images.remove(im)
    """Zwei Slots fester Reihenfolge: 0 Bark, 1 Wood (Slot-Index = Submesh)."""
    def mat(nm, albedo, normal, rough):
        m = bpy.data.materials.new(nm); m.use_nodes = True
        nt = m.node_tree; b = nt.nodes["Principled BSDF"]
        b.inputs["Roughness"].default_value = rough
        im = _img(texdir + "/" + albedo)
        if im:
            tx = nt.nodes.new("ShaderNodeTexImage"); tx.image = im
            try:
                tx.extension = 'EXTEND'      # Blender 5.1: Extension am Knoten
            except Exception:
                pass
            nt.links.new(tx.outputs[0], b.inputs["Base Color"])
        else:
            print('FEHLEND:', albedo)
        nrm = _img(texdir + "/" + normal)
        if nrm:
            tn = nt.nodes.new("ShaderNodeTexImage"); tn.image = nrm
            try:
                tn.extension = 'EXTEND'
            except Exception:
                pass
            nmnode = nt.nodes.new("ShaderNodeNormalMap")
            nmnode.inputs["Strength"].default_value = 1.6
            nt.links.new(tn.outputs[0], nmnode.inputs[1])
            nt.links.new(nmnode.outputs[0], b.inputs["Normal"])
        return m
    return [mat("M_Totholz_Bark", "Totholz_Bark_Albedo.png", "Totholz_Bark_Normal.png", 0.9),
            mat("M_Totholz_Wood", "Totholz_Wood_Albedo.png", "Totholz_Wood_Normal.png", 0.7)]

def build_all(texdir):
    for ob in list(bpy.data.objects):
        bpy.data.objects.remove(ob, do_unlink=True)
    for m in list(bpy.data.materials):
        bpy.data.materials.remove(m)
    mats = make_mats(texdir)
    stats = []
    for args in VARIANTS:
        ob = build_variant(*args)
        for m in mats:
            ob.data.materials.append(m)
        tris = sum(len(p.vertices) - 2 for p in ob.data.polygons)
        bb = [ob.matrix_world @ Vector(c) for c in ob.bound_box]
        xs = [v.x for v in bb]; ys = [v.y for v in bb]; zs = [v.z for v in bb]
        stats.append((ob.name, tris, len(ob.data.polygons), max(xs) - min(xs),
                      max(zs) - min(zs), min(zs)))
    for s in stats:
        print("%s: Dreiecke=%d Polys=%d Laenge=%.2f m Dicke=%.2f m min.z=%.3f" % s)
    return stats

def build_hulls():
    """Konvexe Hull-Prismen (9-Eck) je Variante fuer Unity-MeshCollider(convex):
    Stummel ragen ueber den Mantel hinaus -> Hull umhuellt Stamm + Stummel."""
    for args in VARIANTS:
        nm = args[0]
        src = bpy.data.objects[nm]
        me = bpy.data.meshes.new(nm + "_Hull")
        ob = bpy.data.objects.new(nm + "_Hull", me)
        bpy.context.collection.objects.link(ob)
        bm = bmesh.new()
        xs = [v.co.x for v in src.data.vertices]
        x0, x1 = min(xs), max(xs)
        ys = [v.co.y for v in src.data.vertices]
        zs = [v.co.z for v in src.data.vertices]
        cy = (max(ys) + min(ys)) / 2
        cz = (max(zs) + min(zs)) / 2
        ry = (max(ys) - min(ys)) / 2 * 1.02
        rz = (max(zs) - min(zs)) / 2 * 1.02
        ringA, ringB = [], []
        n = 9
        for i in range(n):
            phi = i / n * 2 * math.pi
            y = cy + math.cos(phi) * ry
            z = cz + math.sin(phi) * rz
            ringA.append(bm.verts.new((x0, y, z)))
            ringB.append(bm.verts.new((x1, y, z)))
        for i in range(n):
            a2 = (i + 1) % n
            bm.faces.new((ringA[i], ringA[a2], ringB[a2], ringB[i]))
        ca = bm.verts.new((x0, cy, cz)); cb = bm.verts.new((x1, cy, cz))
        for i in range(n):
            a2 = (i + 1) % n
            bm.faces.new((ca, ringA[a2], ringA[i]))
            bm.faces.new((cb, ringB[i], ringB[a2]))
        bmesh.ops.triangulate(bm, faces=bm.faces[:])
        bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
        # Hull wie beim Stamm: Achse auf Blender-X (liegt in Unity quer auf X).
        bm.to_mesh(me); bm.free()
        ob.hide_render = True
        print('Hull', nm, 'Verts:', len(me.vertices), 'Tris:', len(me.polygons))


EXPORT_DIR = "C:/Users/vorau/dev/Spinnenspiel/Assets/_Project/Art/Plants/Totholz/"
SRC_DIR = "C:/Users/vorau/dev/Spinnenspiel/ArtSource/Totholz/"


def export_fbx():
    """FBX je Variante, Export-Konvention wie Baumfarn (Baumfarn-Zeile)."""
    import os
    for d in (EXPORT_DIR, SRC_DIR):
        os.makedirs(d, exist_ok=True)
    names = [v[0] + "_Hull" for v in VARIANTS] + [v[0] for v in VARIANTS]
    for nm in names:
        ob = bpy.data.objects[nm]
        bpy.ops.object.select_all(action="DESELECT")
        ob.select_set(True)
        bpy.context.view_layer.objects.active = ob
        for d in (SRC_DIR, EXPORT_DIR):
            bpy.ops.export_scene.fbx(filepath=os.path.join(d, nm + ".fbx"),
                use_selection=True, apply_scale_options="FBX_SCALE_ALL",
                axis_forward="-Z", axis_up="Y", bake_space_transform=True,
                mesh_smooth_type="FACE", path_mode="STRIP", use_mesh_modifiers=True)
        slots = [m.name for m in ob.data.materials]
        print("Export", nm, "Slots:", slots)


if __name__ == "__main__" or True:
    build_all("C:/Users/vorau/dev/Spinnenspiel/ArtSource/Totholz/Textures")
    build_hulls()
    export_fbx()
