# MODELL0: Bodenfarn neu (Blender-MCP-Arbeit, 2026-10-25).
# Bau in kleinen Schritten; dieser Kern wird per exec(open(...)) in Blender geladen.
import bpy, math, random
from mathutils import Vector, Matrix

def tube(pts, r0, r1, seg=4):
    # duennes Rohr entlang Punkteliste; seg=4 -> 8 Dreiecke je Abschnitt
    pts = [Vector(p) for p in pts]
    n = len(pts); V=[]; F=[]
    for i,p in enumerate(pts):
        tan = (pts[min(i+1,n-1)] - pts[max(i-1,0)]).normalized()
        side = tan.cross(Vector((0,0,1)))
        if side.length < 1e-6: side = Vector((1,0,0))
        side.normalize(); up = side.cross(tan)
        r = r0 + (r1-r0)*i/(n-1)
        for j in range(seg+1):
            a = j/seg*2*math.pi
            V.append(p + (math.cos(a)*side + math.sin(a)*up)*r)
    for i in range(n-1):
        for j in range(seg):
            a = i*(seg+1)+j
            F.append((a, a+seg+1, a+seg+2, a+1))
    return V,F

def mat_make(name, rgb, rough=0.75):
    m = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    m.use_nodes = True
    b = m.node_tree.nodes.get("Principled BSDF")
    b.inputs["Base Color"].default_value = (*rgb, 1)
    b.inputs["Roughness"].default_value = rough
    return m

def get_mats():
    # Slot-Reihenfolge: 0 Stiel, 1 Blatt. Gruen nach WALD-ZIEL (95..130 Grad, Sat 0.3..0.5)
    leaf = mat_make("M_FernNeu_Leaf", (0.165, 0.280, 0.135))
    stem = mat_make("M_FernNeu_Stem", (0.100, 0.145, 0.075))
    return stem, leaf

def pinna(base, axis, side, up, L, wmax, rng, mat=1):
    # Ein Fiederblatt: Mittelrippe als Grat, gesaegter Rand (alternierende Breite),
    # Abwaertswoelbung zum Tip. 3 Segmente bei laengeren Fiedern.
    n = 2                              # Budget: 2 Segmente (= Saegezahn + Spitze)
    a = axis.normalized(); p = side.normalized(); q = up.normalized()
    V=[]; F=[]
    prev=None
    for i in range(n+1):
        u = i/n
        w = wmax * math.sin(math.pi * (0.15 + 0.85*u)) * (1.0 - 0.25*u)
        if i % 2 == 1: w *= 0.58       # Saegezahn: deutliche Einschnitte
        curl = -q * (0.18*w + 0.25*w*u)   # massige Abwaertswoelbung (bleibt ueber Grund)
        ridge = q * 0.28 * wmax
        c = base + a*(L*u) + ridge
        V.append(c); V.append(c - p*w + curl); V.append(c + p*w + curl)
        i0 = len(V)-3
        if prev is not None:
            F.append((prev[0], prev[1], i0+1, i0)); F.append((prev[0], i0, i0+2, prev[2]))
        prev = (i0, i0+1, i0+2)
    tip = base + a*L*1.12 - q*0.5*wmax*0.4
    V.append(tip)
    F.append((prev[1], prev[2], len(V)-1))
    return V, [(f, mat) for f in F]

def rachis_strips(pts, w):
    # Rachis als 2 gekreuzte Quad-Streifen (billig, DoubleSided-Material).
    n = len(pts); V=[]; F=[]
    for i,p in enumerate(pts):
        tan = (pts[min(i+1,n-1)] - pts[max(i-1,0)]).normalized()
        s = tan.cross(Vector((0,0,1)))
        if s.length < 1e-6: s = Vector((1,0,0))
        s.normalize(); u_ = s.cross(tan)
        V.append(p - s*w); V.append(p + s*w); V.append(p - u_*w*0.7); V.append(p + u_*w*0.7)
    for i in range(n-1):
        b0 = i*4; b1 = (i+1)*4
        F.append((b0, b1, b1+1, b0+1))      # horizontales Streifen
        F.append((b0+2, b0+3, b1+3, b1+2))  # vertikales Streifen
    return V,F

def frond(L, pairs, rng):
    # Ein Wedel: Rachis als Streifen + ueberlappende, schmale Fiederpaare.
    n = 8
    lean = rng.uniform(0.55, 0.8)      # ueberhaengender Bogen (Schwerkraft)
    pts=[]
    for i in range(n+1):
        u = i/n
        y = L*u*0.92
        z = 0.30*L*math.sin(u*2.1)*(1.0-lean*0.55) + 0.06*L*(1-u)*(u<0.2)
        floorz = 0.02*L*(1.0-u) + 0.004   # Spitze bleibt ueber Grund (Bodenfarn!)
        pts.append(Vector((rng.uniform(-0.02,0.02)*u, y, max(z, floorz))))
    V,F = rachis_strips(pts, L*0.011)
    Fm = [(f, 1) for f in F]            # gruenen Blattrachis -> Blatt-Slot
    for k in range(pairs):
        t01 = 0.06 + 0.90*(k+0.5)/pairs
        idx = min(int(t01*n), n-1)
        c = pts[idx]
        tan = (pts[min(idx+1,n)] - pts[max(idx-1,0)]).normalized()
        s = tan.cross(Vector((0,0,1))).normalized()
        u_ = s.cross(tan)
        rel = math.sin(math.pi*(0.10 + 0.88*(k+0.5)/pairs))  # Mitte am groessten
        Lp = L*0.30*rel*rng.uniform(0.85,1.15) + 0.006       # lang und ueberlappend
        if Lp < 0.008: continue
        ang = math.radians(rng.uniform(55, 72))
        # Winkelgradient: basisnahe Fiedern zeigen mehr seitwaerts/abwaerts,
        # spitzennahe mehr vorwaerts (typischer Farnverlauf)
        fwd = 0.35 + 0.55*(k/max(1,pairs-1))
        for sgn in (-1, 1):
            # Paare leicht versetzt (nicht streng gegenstaendig) -> dichtere Feder
            c2 = c + tan*L*0.02*sgn*(k % 2)
            ax = (tan*math.cos(ang*fwd) + s*sgn*math.sin(ang)).normalized()
            ax += u_*rng.uniform(0.08, 0.34)   # Fiedern leicht nach oben neigen
            ax.normalize()
            sv = ax.cross(u_).normalized()
            if sv.dot(s)*sgn < 0: sv = -sv
            vv, ff = pinna(c2, ax, sv, u_, Lp, Lp*0.16*rng.uniform(0.8,1.1), rng)
            o = len(V); V.extend(vv)
            Fm.extend((tuple(x+o for x in f), m) for f,m in ff)
    return V, Fm

def plant(name, seed, frond_len, n_fronds, pairs, spread_tilt):
    rng = random.Random(seed)
    stem_mat, leaf_mat = get_mats()
    V=[]; Fm=[]
    # Kronenbasis: kurzer dicker Spross am Boden, aus dem alle Stiele wachsen
    cv, cf = tube([Vector((0,0,-0.02)), Vector((0,0,0.035))], 0.016, 0.011, seg=5)
    V.extend(cv); Fm.extend((f, 0) for f in cf)
    # Ein eingerollter Jungwedel (Fiddlehead) — das typischste Farn-Merkmal
    fh_pts = []
    turns = 1.6; r0 = 0.05
    for i in range(9):
        t = i/8
        a = t*turns*2*math.pi
        r = r0*(1.0-t*0.75)
        fh_pts.append(Vector((r*math.cos(a), r*math.sin(a), 0.035 + 0.10*t)))
    fv0, ff0 = tube(fh_pts, 0.005, 0.002, seg=4)
    V.extend(fv0); Fm.extend((f, 0) for f in ff0)
    for fi in range(n_fronds):
        # Goldener Winkel: natuerliche, nicht-gleichmaessige Rosette
        az = fi*2.39996 + rng.uniform(-0.25,0.25) + rng.uniform(0, 6.283)*0.0
        tilt = math.radians(spread_tilt + rng.uniform(-12, 12))
        L = frond_len*rng.uniform(0.85,1.08)   # Laengenvarianz; stays in 0.4..0.8 m
        stipe_h = L*rng.uniform(0.16,0.24)     # echter Blattstiel am Ansatz
        dirh = Vector((math.cos(az), math.sin(az), 0))
        pts=[dirh*0.010 + Vector((0,0,0.010)),
             dirh*(stipe_h*0.5) + Vector((0,0,stipe_h*0.75 + 0.02)),
             dirh*stipe_h + Vector((0,0,stipe_h + 0.03))]
        sv,sf = tube(pts, 0.0065, 0.0042, seg=4)
        o=len(V); V.extend(sv); Fm.extend((tuple(x+o for x in f), 0) for f in sf)
        fv, ff = frond(L, pairs, rng)
        M = (Matrix.Translation(dirh*stipe_h + Vector((0,0,stipe_h + 0.03)))
             @ Matrix.Rotation(az - math.pi/2, 4, 'Z')
             @ Matrix.Rotation(tilt, 4, 'X'))   # Anhebung: Wedel zeigt erst nach oben
        o=len(V); V.extend(M @ v for v in fv)
        Fm.extend((tuple(x+o for x in f), m) for f,m in ff)
    me = bpy.data.meshes.new(name)
    me.from_pydata([tuple(v) for v in V], [], [tuple(f) for f,_ in Fm])
    me.update()
    for p in me.polygons: p.use_smooth = True
    me.materials.append(stem_mat); me.materials.append(leaf_mat)
    for p,(_,m) in zip(me.polygons, Fm): p.material_index = m
    ob = bpy.data.objects.new(name, me)
    bpy.context.scene.collection.objects.link(ob)
    tris = sum(len(p.vertices)-2 for p in me.polygons)
    return ob, tris

def bounds(ob):
    bb=[ob.matrix_world @ Vector(c) for c in ob.bound_box]
    xs=[b.x for b in bb]; ys=[b.y for b in bb]; zs=[b.z for b in bb]
    return (max(xs)-min(xs), max(ys)-min(ys), max(zs)-min(zs)), min(zs)
