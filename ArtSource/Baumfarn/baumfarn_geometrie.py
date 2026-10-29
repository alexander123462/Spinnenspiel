# MODELL15: Baumfarn-Geometrie (Blender/bmesh). Ein Fertigungsmodul fuer drei
# Varianten: Stamm mit Narbenring-Relief und Wurzelfuss, bogige Wedel mit echten
# Fiederblaettern (V-Falz), UV-Atlas 4x4 (ein Keilblatt pro Wedel).
# Aufbau in Stuecken; Aufruf: build_variant(idx) baut Baumfarn_<idx+1>.
import bpy
import bmesh
import math
import random

from mathutils import Vector, Matrix

ATLAS_N = 4          # 4x4 Keilblaetter im Blatt-Atlas
TRUNK_SIDES = 14     # U-Segmente des Stamms (Naehe unter Grenze fuer Narbenwellen)

# Varianten-Festwerte (feste Werte statt Zufall: reproduzierbar, Tests messbar).
VARIANTS = [
    dict(h=1.50, fronds=11, rad=0.085, lean=0.14, seed=11),
    dict(h=2.00, fronds=12, rad=0.100, lean=0.11, seed=22),
    dict(h=2.40, fronds=13, rad=0.115, lean=0.16, seed=33),
]


def clear_scene():
    """Szene komplett leeren (Neubau startet immer bei null)."""
    for obj in list(bpy.data.objects):
        bpy.data.objects.remove(obj, do_unlink=True)
    for block in (bpy.data.meshes, bpy.data.materials, bpy.data.armatures):
        for item in list(block):
            if item.users == 0:
                block.remove(item)


def _link_tex(mat, albedo, normal):
    """Bildtextur an Principled BSDF haengen (UV -> Albedo, optional Normal)."""
    nt = mat.node_tree
    b = nt.nodes.get("Principled BSDF")
    if not b:
        return
    for inp in ("Base Color", "Normal"):   # alte Verdrahtung entfernen (2. Build)
        for l in list(b.inputs[inp].links):
            nt.links.remove(l)
    uvn = nt.nodes.new('ShaderNodeUVMap')
    ti = nt.nodes.new('ShaderNodeTexImage')
    ti.image = bpy.data.images.load(albedo, check_existing=True)
    ti.image.reload()   # Datei kann sich seit letztem Build geaendert haben
    nt.links.new(uvn.outputs['UV'], ti.inputs['Vector'])
    nt.links.new(ti.outputs['Color'], b.inputs['Base Color'])
    if normal:
        tn = nt.nodes.new('ShaderNodeTexImage')
        tn.image = bpy.data.images.load(normal, check_existing=True)
        tn.image.reload()
        try:
            tn.image.colorspace_settings.name = 'Non-Color'
        except TypeError:
            tn.image.color_space = 'Non-Color'   #aeltere API
        nm = nt.nodes.new('ShaderNodeNormalMap')
        nt.links.new(uvn.outputs['UV'], tn.inputs['Vector'])
        nt.links.new(tn.outputs['Color'], nm.inputs['Color'])
        nt.links.new(nm.outputs['Normal'], b.inputs['Normal'])


def ensure_materials():
    """Zwei Slots mit sprechenden Namen: 0 Stamm, 1 Blatt (Reihenfolge fest!).
    Textur aus dem 15a-Atlas (Blatt) bzw. Stamm-Albedo, damit die Vorschau
    im Blender dem spaeteren Unity-Look entspricht."""
    tex = "C:/Users/vorau/dev/Spinnenspiel/ArtSource/Baumfarn/Textures/"
    mats = []
    for name, col, alb, nrm in (("Baumfarn_Stem", (0.28, 0.20, 0.12, 1),
                                 tex + "Baumfarn_Stem_Albedo.png", None),
                                ("Baumfarn_Leaf", (0.10, 0.32, 0.10, 1),
                                 tex + "Baumfarn_Leaf_Albedo.png",
                                 tex + "Baumfarn_Leaf_Normal.png")):
        mat = bpy.data.materials.get(name)
        if mat is None:
            mat = bpy.data.materials.new(name)
        mat.use_nodes = True
        bsdf = mat.node_tree.nodes.get("Principled BSDF")
        if bsdf:
            bsdf.inputs["Base Color"].default_value = col
        _link_tex(mat, alb, nrm)
        mats.append(mat)
    return mats


def leaf_face(bm, vs):
    # Blattflaeche: material_index=1 direkt beim Anlegen (Blender 5.1 hat
    # keinen zuverlaessigen bmesh-Index mehr fuer spaetes Umfaerben).
    f = bm.faces.new(vs)
    f.material_index = 1
    return f


def make_trunk(bm, prm):
    """Stamm als leicht gebogener Kegel mit Wurzel-Flare und Kappe.
    UV spaeter: u = Ringwinkel, v = Hoehenanteil. Gibt Stamm-Spitze zurueck."""
    h = prm["h"]
    rad = prm["rad"]
    lean = prm["lean"]
    rnd = random.Random(prm["seed"])
    rings = 18  # mehr Hohenringe: Narbenbnder bleiben unter der Dreiecks-Grenze sichtbar
    azim = prm["azim"]
    dirx, diry = math.cos(azim), math.sin(azim)
    prev = None
    cx = cy = z = 0.0
    for i in range(rings + 1):
        t = i / float(rings)
        z = t * h
        off = lean * h * (t ** 2) + math.sin(t * 6.3) * 0.012
        cx, cy = dirx * off, diry * off
        r = rad * (1.35 - 0.45 * t)
        if t < 0.08:
            r += (0.08 - t) * 1.4
        ring = []
        for k in range(TRUNK_SIDES):
            a = 2.0 * math.pi * k / TRUNK_SIDES
            # Blattnarben: deutliche Ringe bei konstanter Hoehe (t), ca. 6 pro
            # Meter, dazu leichte Unregelmessigkeit um den Umfang.
            rr = (r * (1.0 + 0.115 * math.sin(t * 37.0 * h))
                  + r * 0.045 * math.sin(a * 7.0)
                  + r * rnd.uniform(-0.012, 0.012))
            ring.append(bm.verts.new((cx + math.cos(a) * rr,
                                      cy + math.sin(a) * rr, z)))
        if prev is not None:
            for k in range(TRUNK_SIDES):
                k2 = (k + 1) % TRUNK_SIDES
                bm.faces.new((prev[k], prev[k2], ring[k2], ring[k]))
        if i == 0:
            bm.faces.new(list(reversed(ring)))  # Boden-Kappe: hohler Stamm von unten
        prev = ring
    bm.faces.new(list(reversed(prev)))  # Kappe oben
    return (cx, cy, z)


def make_frond(bm, prm, j, cell, uv_list, l_scale=1.0,
                 arc=(150, 175), tilt_base=66, st_lo=5, st_hi=18):
    """Ein bogiger Wedel in lokalem Rahmen (+X aussen, +Y seitlich, +Z hoch).
    Blattachse: Bogelform ueber Bogenlaenge s; Fiedern links/rechts als V-Falz.
    UV je Keilblatt-Zelle: Blattband (q entlang Fieder) + Blattachsenband.
    Rueckgabe: Dreiecke. uv_list sammelt (vert, u, v) fuer spaeteren Export."""
    rnd = random.Random(prm["seed"] * 100 + j)
    n = prm["fronds"]
    azim = (j * 2.4 + prm["azim"] + rnd.uniform(-0.12, 0.12))
    tilt = math.radians(tilt_base - 9 * (j % 3) + rnd.uniform(-6, 6))
    L = (1.55 + 0.10 * prm["h"] / 2.4 + rnd.uniform(-0.06, 0.09)) * l_scale
    segs = 18
    # Bogenintegration: Richtungswinkel theta(s) von steil (Knick) bis haengend.
    pts = []
    dth = math.radians(rnd.uniform(arc[0], arc[1])) / segs
    x = y = z = 0.0
    th = tilt
    for i in range(segs + 1):
        s = i / float(segs)
        pts.append((x, y, z, s))
        step = L / segs
        # Bogen gewichtet nach hinten: Basis bleibt aufrecht, Spitze haengt
        # staerker ueber (Selbstkritik Runde 1: Wedel zu frueh ueberbogen).
        th2 = th - dth * (0.35 + 1.45 * s)
        x += step * math.cos(0.5 * (th + th2))
        z += step * math.sin(0.5 * (th + th2))
        th = th2
    def rach_w(s):
        if s < 0.22:
            return 0.004 + 0.006 * s
        return max(0.0015, 0.012 * math.sin(math.pi * (1.0 - s) ** 0.7))
    u0 = (cell % 4) * 0.25 + 0.015
    v0 = (cell // 4) * 0.25 + 0.015
    uw = vh = 0.22
    tris = 0
    # Blattachse als Band (2 Randvert pro Station), UV im unteren Band der Zelle.
    rL = []
    rR = []
    for (x, y, z, s) in pts:
        w = rach_w(s)
        vl = bm.verts.new((x, y + w, z))
        vr = bm.verts.new((x, y - w, z))
        uv_list.append((vl, u0 + s * uw, v0 + 0.020))
        uv_list.append((vr, u0 + s * uw, v0 + 0.052))
        rL.append(vl)
        rR.append(vr)
    tip = bm.verts.new((x, y, z - 0.01))
    uv_list.append((tip, u0 + uw, v0 + 0.036))
    for i in range(segs):
        f = leaf_face(bm, (rL[i], rL[i + 1], rR[i + 1], rR[i]))
        tris += 2
    leaf_face(bm, (rL[segs], tip, rR[segs]))
    tris += 1
    # Fiedern: an Stationen s=0.24..0.95 links/rechts, V-Falz (38 Grad),
    # UV in zwei Bjaendern der Zelle (q entlang Fieder = u).
    cf = math.cos(math.radians(52))
    sf = math.sin(math.radians(52))

    def half_w(q, lp):
        # Sockel-Breite 0.10: sonst sind die Fiedern an der Spindel punktuell
        # und wirken von weitem als isolierte Dreieckskette (Runde-3-Kritik).
        return lp * 0.5 * (0.42 * math.sin(math.pi * min(1.0, q) ** 0.75) ** 0.5 + 0.10)

    def add_pinna(base, tx, tz, side, s_a):
        nonlocal tris
        t = max(0.0, (s_a - 0.24)) / 0.71
        lp = (0.12 + 0.30 * math.sin(math.pi * min(1.0, t) ** 0.8)) \
             * rnd.uniform(0.85, 1.12)
        tl = math.sqrt(tx * tx + tz * tz) or 1.0
        # Fiederrichtung: hauptsjaechlich seitlich (Y), mitwaerts der Wedelachse
        # nach vorn/skraeg geneigt, anfangs leicht angehoben.
        dx = 0.30 * tx + side * rnd.uniform(0.10, 0.22) * tl
        dy = side * 0.90
        dz = 0.22 * tl + 0.30 * tz
        dl = math.sqrt(dx * dx + dy * dy + dz * dz)
        dx, dy, dz = dx / dl, dy / dl, dz / dl
        nl = math.sqrt(dx * dx + dy * dy) or 1.0
        nx, ny = dy / nl, -dx / nl   # Querrichtung der Fieder (Grundriss)
        vc = v0 + (0.100 if side > 0 else 0.195)
        cs = []
        es = []
        for m in range(3):
            q = m / 2.0
            cxp = base[0] + dx * q * lp
            cyp = base[1] + dy * q * lp
            # Durchhang ueber die Fiedermooslaenge, aber Blattspitze curlt leicht
            # nach oben (Selbstkritik 1: Fiedern wirken papierhaft flach).
            droop = (-0.08 * q * q + 0.06 * q ** 3) * lp
            czp = base[2] + dz * q * lp + droop
            cs.append(bm.verts.new((cxp, cyp, czp)))
            uv_list.append((cs[m], u0 + q * uw, vc))
            hw = half_w(q, lp)
            for sgn in (1, -1):
                ev = bm.verts.new((cxp + sgn * hw * nx * cf,
                                   cyp + sgn * hw * ny * cf,
                                   czp + hw * sf))
                uv_list.append((ev, u0 + q * uw,
                                vc + sgn * min(0.045, hw / 0.06 * 0.045)))
                es.append(ev)
        tp = bm.verts.new((base[0] + dx * lp, base[1] + dy * lp,
                           base[2] + dz * lp - 0.02 * lp))
        uv_list.append((tp, u0 + uw, vc))
        for m in range(2):
            leaf_face(bm, (cs[m], cs[m + 1], es[2 * m + 3], es[2 * m + 1]))
            leaf_face(bm, (cs[m], cs[m + 1], es[2 * m + 2], es[2 * m]))
            tris += 4
        leaf_face(bm, (cs[2], tp, es[4]))
        leaf_face(bm, (cs[2], es[5], tp))
        tris += 2
    for i in range(st_lo, st_hi):
        s_a = pts[i][3]
        nx_ = pts[i + 1][0] - pts[i][0]
        nz_ = pts[i + 1][2] - pts[i][2]
        add_pinna(tuple(rL[i].co), nx_, nz_, +1, s_a)
        add_pinna(tuple(rR[i].co), nx_, nz_, -1, s_a)
    return tris, azim


def make_buttresses(bm, prm):
    """Wurzelstreben: flache Flossen am Stammfuss (steht nicht 'auf einer Stange')."""
    rnd = random.Random(prm["seed"] + 7)
    rad = prm["rad"]
    n = 7
    for k in range(n):
        for half in (0.0, 0.5):   # versetzte Zwillingsflossen: Streben wirken
            a = 2.0 * math.pi * (k + half + rnd.uniform(-0.2, 0.2)) / n  # dickwandig
            a2 = a + math.radians(30)
            am = 0.5 * (a + a2)
            g1 = bm.verts.new((math.cos(a) * (rad + 0.34), math.sin(a) * (rad + 0.34), 0.0))
            g2 = bm.verts.new((math.cos(a2) * (rad + 0.34), math.sin(a2) * (rad + 0.34), 0.0))
            t1 = bm.verts.new((math.cos(am - 0.06) * rad * 1.05, math.sin(am - 0.06) * rad * 1.05, 0.28))
            t2 = bm.verts.new((math.cos(am + 0.06) * rad * 1.05, math.sin(am + 0.06) * rad * 1.05, 0.22))
            bm.faces.new((g1, g2, t2, t1))
    # Wurzelkragen: niedriger Ring am Fuss schliesst die Schlitze zwischen den
    # Flossen (Runde-4-Kritik 'Sternrock mit Lochzacken'). 14 Seiten x 2 Quad = 28 Tria.
    collar_lo, collar_hi = [], []
    for s in range(TRUNK_SIDES):
        a = 2.0 * math.pi * s / TRUNK_SIDES
        rr0 = rad + 0.16 + 0.03 * math.sin(a * 5.0)
        collar_lo.append(bm.verts.new((math.cos(a) * rr0, math.sin(a) * rr0, 0.0)))
        collar_hi.append(bm.verts.new((math.cos(a) * rad * 1.12, math.sin(a) * rad * 1.12, 0.16)))
    for s in range(TRUNK_SIDES):
        s2 = (s + 1) % TRUNK_SIDES
        bm.faces.new((collar_lo[s], collar_lo[s2], collar_hi[s2], collar_hi[s]))


def stem_uv(bm, prm):
    """Stamm-UV: u = Winkel um die (geneigte) Achse, v = Hoehenanteil."""
    tuv = bm.loops.layers.uv.active
    h = prm["h"]
    lean = prm["lean"]
    dirx, diry = math.cos(prm["azim"]), math.sin(prm["azim"])
    for f in bm.faces:
        for l in f.loops:
            v = l.vert.co
            t = min(1.0, max(0.0, v.z / h))
            off = lean * h * (t ** 2) + math.sin(t * 6.3) * 0.012
            ang = math.atan2(v.y - diry * off, v.x - dirx * off)
            l[tuv].uv = (ang / (2.0 * math.pi) % 1.0, t)


def make_fiddleheads(bm, prm, uv_l):
    """4 echte Croziers in der Kronenmitte: aufrechter Stiel, oben eng
    gerollter Haken (Einwaerts um haengende Achse). Slot 1 (Blatt).
    UV: eigene Zelle (Spalte 3, Zeile 0) — numpy malt dort Kraeuse+Flaum."""
    rnd = random.Random(prm["seed"] + 17)
    # Zelle 15 (Spalte 3, Zeile 3): numpy malt dort braunen Flaum mit Kraeuse.
    u0 = 3 * 0.25 + 0.015
    v0 = 3 * 0.25 + 0.015
    up = Vector((0, 0, 1))
    for k in range(4):
        az = prm["azim"] + k * 1.5708 + k * 0.4 + rnd.uniform(-0.3, 0.3)  # asymmetrisch
        out = Vector((math.cos(az), math.sin(az), 0))
        side = out.cross(up)
        r_base = 0.22 + 0.07 * (k % 2)   # Buendel nah an der Mitte
        h_stipe = 0.50 + rnd.uniform(-0.03, 0.05)  # ragen ueber die dichte Krone
        curl_r = 0.14 + rnd.uniform(-0.01, 0.02)   # deutlicher Haken: Fragezeichen
        turns = 1.05 + rnd.uniform(0.0, 0.25)      # ~1 Wicklung: echte Fadenkopf-Form
        segs = 12
        # Pfadpunkte: erst Stiel aufwaerts, dann Haken abwaerts einrollen.
        pts = []
        for i in range(segs + 1):
            f = i / float(segs)
            if f <= 0.55:                       # Stiel
                t = f / 0.55
                pts.append(out * (r_base + 0.12 * t * t) + up * (t * h_stipe))
            else:                               # Haken um Mittelpunkt c
                t = (f - 0.55) / 0.45
                phi = turns * 2.0 * math.pi * t
                rc = curl_r * (1.0 - 0.30 * t)   # Haken schrumpft nicht zum Klumpen
                c = out * r_base + up * h_stipe - up * curl_r
                pts.append(c + up * (rc * math.cos(phi)) + out * (rc * math.sin(phi)))
        prev_ring = None
        for i in range(segs + 1):
            f = i / float(segs)
            a0 = max(0, i - 1)
            a1 = min(segs, i + 1)
            tang = (pts[a1] - pts[a0])
            if tang.length < 1e-6:
                tang = up
            tang.normalize()
            e1 = tang.cross(side)
            if e1.length < 1e-6:
                e1 = tang.cross(up)
            e1.normalize()
            e2 = e1.cross(tang).normalized()
            rr = 0.038 * (1.0 - 0.25 * f) + 0.006  # schlank: Haken bleibt lesbar
            ring = []
            for a4 in range(6):
                aa = math.radians(60 * a4 + 30)
                vv = bm.verts.new(pts[i] + (e1 * math.cos(aa) + e2 * math.sin(aa)) * rr)
                uv_l.append((vv, u0 + f * 0.22, v0 + (a4 / 5.0) * 0.22))
                ring.append(vv)
            if prev_ring is not None:
                for a4 in range(6):
                    a5 = (a4 + 1) % 6
                    leaf_face(bm, (prev_ring[a4], prev_ring[a5], ring[a5], ring[a4]))
            prev_ring = ring


def build_variant(idx):
    """Baut Baumfarn_(idx+1) als EIN Mesh mit 2 Slots (0 Stamm, 1 Blatt)."""
    prm = dict(VARIANTS[idx])
    prm["azim"] = random.Random(prm["seed"]).uniform(0.0, 6.2832)
    mats = ensure_materials()
    bm = bmesh.new()
    bm.loops.layers.uv.new("UVMap")
    top = make_trunk(bm, prm)
    make_buttresses(bm, prm)
    stem_uv(bm, prm)
    uv_l = []
    leaf_tris = 0
    for j in range(prm["fronds"]):
        pre_v = len(bm.verts)
        t_cnt, azim = make_frond(bm, prm, j, j % 16, uv_l)
        leaf_tris += t_cnt
        bm.verts.ensure_lookup_table()
        zoff = top[2] - 0.04 - (j % 3) * 0.035   # engere Kronenzone (Trichter)
        m = (Matrix.Translation((top[0], top[1], zoff))
             @ Matrix.Rotation(azim, 4, 'Z'))
        for k in range(pre_v, len(bm.verts)):
            bm.verts[k].co = m @ bm.verts[k].co
    # MODELL15: innere, steil aufwaerts gerichtete Kurzwedel fuellen die
    # Kronenmitte (geteilter UV-Zell 14).
    for j in range(4):
        pre_v = len(bm.verts)
        make_frond(bm, prm, 900 + j, 14, uv_l, l_scale=0.45,
                   arc=(30, 50), tilt_base=72, st_lo=6, st_hi=16)
        bm.verts.ensure_lookup_table()
        m = (Matrix.Translation((top[0], top[1], top[2] + 0.03))
             @ Matrix.Rotation(prm["azim"] + j * 2.1, 4, 'Z'))
        for k in range(pre_v, len(bm.verts)):
            bm.verts[k].co = m @ bm.verts[k].co
    pre_v = len(bm.verts)
    make_fiddleheads(bm, prm, uv_l)
    bm.verts.ensure_lookup_table()
    for k in range(pre_v, len(bm.verts)):
        bm.verts[k].co += Vector((top[0], top[1], top[2] + 0.01))
    tuv = bm.loops.layers.uv.active
    for (vert, u, v) in uv_l:
        for l in vert.link_loops:
            l[tuv].uv = (u, v)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])  # einheitliche Aussenflaechen
    me = bpy.data.meshes.new("Baumfarn_%d" % (idx + 1))
    # Material-Slots VOR to_mesh anlegen: sonst klemmt Blender material_index=1 auf 0 zurueck.
    me.materials.append(mats[0])
    me.materials.append(mats[1])
    bm.to_mesh(me)
    bm.free()
    ob = bpy.data.objects.new("Baumfarn_%d" % (idx + 1), me)
    bpy.context.collection.objects.link(ob)
    t0 = sum(len(p.loop_indices) - 2 for p in me.polygons if p.material_index == 0)
    t1 = sum(len(p.loop_indices) - 2 for p in me.polygons if p.material_index == 1)
    xs = [v.co for v in me.vertices]
    minz = min(c.z for c in xs); maxz = max(c.z for c in xs)
    sx = max(c.x for c in xs) - min(c.x for c in xs)
    sy = max(c.y for c in xs) - min(c.y for c in xs)
    print("Baumfarn_%d: Stamm-Tria=%d Blatt-Tria=%d gesamt=%d "
          "y %.3f..%.3f spreadXZ=%.2f uv=%d" %
          (idx + 1, t0, t1, t0 + t1, minz, maxz, max(sx, sy), len(me.uv_layers[0].data)))


def build_all():
    clear_scene()
    for i in range(3):
        build_variant(i)

