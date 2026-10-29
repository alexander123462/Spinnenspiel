# MODELL23 Selbstkritik D1: Rohr-Gabeln schliessen (Neuaufbau-Hilfsskript).
# Fehler im alten Bau: Kinder-Zweige starten auf der Eltern-OBERFLAEHE, ihre
# Rohre beruehren sich nur punktuell -> sichtbare Schlitzluecke an jeder Gabel
# (Vision-Kritik D0: 'Loch/Fuge mitten im Stamm', 'Stativ-Beine'). Fix: jeden
# Kinderzweig mit einem Einsatzpunkt INS Elternrohr verlangern; Wurzelaste bis
# in den Stamm hochziehen (gemeinsamer Wurzeltrichter statt Stativ-Beinen).
# Aufruf in Blender: exec(open(PFAD).read()); neu_bauen()
import bpy, json, math, random
from mathutils import Vector

HERE = r'C:\Users\vorau\dev\Spinnenspiel\ArtSource\Baum_Island01'
exec(open(HERE + r'\bau_lib_mcp.py').read())

def load_geruest():
    branches = json.load(open(HERE + r'\geruest.json'))
    for b in branches:
        b['pts'] = [(Vector(p[:3]), p[3]) for p in b['pts']]
    return branches

def attach_into_parent(b, parent_pts):
    """Basis von b an die Elternpolylinie haengen: Einsatzpunkt INS Rohr."""
    p0, r0 = b['pts'][0]
    best_i, best_d = 0, 1e9
    for i, (q, _) in enumerate(parent_pts):
        d = (p0 - q).length
        if d < best_d:
            best_d, best_i = d, i
    q, rq = parent_pts[best_i]
    dvec = (b['pts'][1][0] - p0).normalized()
    depth = rq * 1.6 + 0.15
    inner = p0 - dvec * depth
    b['pts'] = ([(inner, rq * 0.9), (q + (p0 - q) * 0.4, (rq + r0) * 0.5)]
                + list(b['pts']))

def fix_roots(branches):
    """Wurzelaste (Level 0, nicht Stamm Nr. 0) bis z=3.2 im Stamm fortsetzen:
    gemeinsamer Wurzeltrichter statt Stativ-Beinen."""
    for b in branches:
        if b['level'] != 0 or b is branches[0]:
            continue
        p0, r0 = b['pts'][0]
        top = Vector((p0.x * 0.15, p0.y * 0.15, 3.2))
        b['pts'] = [(top, 0.62)] + list(b['pts'])

def add_leader(branches):
    """D2: Der Hauptstamm endet bei z=5.1, darueber nur duenne Aeste ->
    sichtbarer Hohlraum wie ein Besenstiel-Loch. Leittrieb bis in die Krone
    (z=9.2) und von dort zwei starke Geruestaeste zur Kronenmitte."""
    trunk = branches[0]
    p, r = trunk['pts'][-1]
    import random
    random.seed(2312)
    lead = [(p, r)]
    zc = p.z
    rr = r
    while zc < 9.2:
        zc += 0.55
        rr = max(0.14, rr * 0.86)
        lead.append((Vector((p.x + random.uniform(-0.18, 0.18) * (zc - p.z),
                             p.y + random.uniform(-0.18, 0.18) * (zc - p.z),
                             zc)), rr))
    trunk['pts'].extend(lead[1:])
    # zwei mittlere Geruestaeste vom Leittrieb (Level 1, dicker als Rest)
    for k, ang in enumerate((0.9, 2.6)):
        base = trunk['pts'][-3][0]
        d = Vector((math.cos(ang), math.sin(ang), 0.55)).normalized()
        pts = []
        q = base
        rq = 0.30
        for i in range(7):
            pts.append((q, rq))
            q = q + d * 0.85 + Vector((0, 0, -0.10 * i))
            rq = max(0.10, rq * 0.85)
        branches.append({'level': 1, 'pts': pts})

def neu_bauen():
    branches = load_geruest()
    add_leader(branches)
    for b in branches[1:]:
        if b['level'] == 0:
            continue
        best_p, best_d = None, 1e9
        for p in branches:
            if p is b:
                continue
            d = (b['pts'][0][0] - p['pts'][-1][0]).length
            if d < best_d:
                best_d, best_p = d, p
        if best_p is not None and best_d < 1.2:
            attach_into_parent(b, best_p['pts'])
    fix_roots(branches)
    json.dump([{'level': b['level'],
                'pts': [[round(p[0].x, 3), round(p[0].y, 3), round(p[0].z, 3), p[1]]
                        for p in b['pts']]} for b in branches],
              open(HERE + r'\geruest_v2.json', 'w'))
    _, clusters = load_data()
    # D2: Fuell-Cluster gegen kahle Kronenluecken (sparsam: Budget 30k Dreiecke)
    random.seed(2313)
    fill = []
    for b in branches:
        if len(b['pts']) < 4:
            continue
        for i in range(2, len(b['pts'])):
            p, r = b['pts'][i]
            if p.z < 6.5 or random.random() < 0.72:
                continue
            off = Vector((random.uniform(-0.7, 0.7), random.uniform(-0.7, 0.7),
                          random.uniform(-0.4, 0.4)))
            fill.append((p + off, b['level']))
    clusters_all = clusters + fill
    # Budget: alle Blattstreifen 2 Segmente, Holzquerschnitte feiner abgestuft
    _orig_add_cluster = globals()['add_cluster']
    def add_cluster2(bm, uvl, c, lvl, dnorm, segs_leaf):
        return _orig_add_cluster(bm, uvl, c, lvl, dnorm, 2)
    globals()['add_cluster'] = add_cluster2
    def sides2(b, coarse):
        if coarse:
            return {0: 8, 1: 5, 2: 4, 3: 3}[b['level']]
        return {0: 10, 1: 7, 2: 5, 3: 4}[b['level']]
    globals()['sides_for'] = sides2
    for nm in ('island_tree_01_LOD0', 'island_tree_01_LOD1'):
        old = bpy.data.objects.get(nm)
        if old:
            me_old = old.data
            bpy.context.scene.collection.objects.unlink(old)
            bpy.data.objects.remove(old)
            bpy.data.meshes.remove(me_old)
    o0 = build('island_tree_01_LOD0', False, branches, clusters_all)
    o1 = build('island_tree_01_LOD1', True, branches, clusters_all,
               only_levels=[0, 1, 2])
    return o0, o1
