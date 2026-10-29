# MODELL23 (Baum_Island01): prozedurales Geruest fuer den Insel-Baum 1.
# Laeuft in Blender (per exec aus dem MCP) oder headless. Seed 2301, deterministisch.
# Charakter (aus Vorher-Analyse des PH-Scans): knorriger, gedrehter Stamm mit
# Wurzelanlauf (Buttress-Rippen), tiefe Gabelung ~4.8 m, ein waagerechter
# Schirmast, schirmfoermige ausladende Krone bis ~13.2 m.
# Schreibt geruest.json (Zweig-Polylinien) und cluster.json (Blattcluster-Punkte).
import json, math, random

HERE = r'C:\Users\vorau\dev\Spinnenspiel\ArtSource\Baum_Island01'
random.seed(2301)

def pts_out(branch):
    return {'level': branch['level'],
            'pts': [[round(p[0], 3), round(p[1], 3), round(p[2], 3), round(p[3], 3)]
                    for p in branch['pts']]}

branches = []
clusters = []

def add_branch(level, start, direction, length, r0, r1, npts, droop=0.0, curl=None):
    """Polylinie entlang direction; droop zieht die Spitze nach unten (Schirmast)."""
    d = direction
    ll = math.sqrt(d[0]**2 + d[1]**2 + d[2]**2)
    d = (d[0]/ll, d[1]/ll, d[2]/ll)
    side = random.uniform(-1, 1)
    axis = (-d[1], d[0], 0.0)
    al = math.sqrt(axis[0]**2 + axis[1]**2) or 1.0
    axis = (axis[0]/al, axis[1]/al, 0.0)
    freq = random.uniform(2.0, 3.4)      # pro Ast konstant: gleichmaessige Biegung
    phase = level + random.uniform(0.0, 1.5)
    pts = []
    p = start
    for i in range(npts):
        t = i / (npts - 1)
        r = r0 + (r1 - r0) * t
        wob = 0.06 + 0.05 * level
        off = math.sin(t * freq + phase) * wob * length
        dz = -droop * t * t * length
        c = (curl or 0.0) * t * t * length
        step = d[0] + axis[0] * off / max(length, 0.1) * 0.25, \
               d[1] + axis[1] * off / max(length, 0.1) * 0.25, \
               d[2] + c / max(length, 0.1) * 0.5
        p = (p[0] + step[0] * length / (npts - 1),
             p[1] + step[1] * length / (npts - 1),
             p[2] + step[2] * length / (npts - 1) + dz / (npts - 1))
        pts.append((p[0], p[1], p[2], r))
    br = {'level': level, 'pts': pts}
    branches.append(br)
    return br

# --- Stamm: gedreht, Basis dick mit Wurzelanlauf-Verbreiterung, Gabelung ~4.8 m ---
trunk = add_branch(0, (0.0, 0.0, 0.0), (0.06, -0.03, 1.0), 4.8, 0.95, 0.52, 11, curl=0.05)
trunk['pts'][0] = trunk['pts'][0][:3] + (1.10,)   # tellerartiger Fuss (flacher: kein Tritt fuer Spinnenbeine)
trunk['pts'][1] = trunk['pts'][1][:3] + (0.95,)
trunk['pts'][2] = trunk['pts'][2][:3] + (0.85,)
# Wurzelanlauf: 5 Buttress-Rippen, steil und duenn zum Boden auslaufend —
# K5-Lehre: zu dicke Rippen nahe dem Boden bauen Spinnen-Fuessen Tritte.
for k in range(5):
    a = 2 * math.pi * k / 5 + random.uniform(-0.25, 0.25)
    top = (math.cos(a) * 0.45, math.sin(a) * 0.45, 2.4)
    out = add_branch(0, top, (math.cos(a) * 0.68, math.sin(a) * 0.68, -0.95),
                     3.0, 0.45, 0.25, 7, droop=0.10)
    out['root'] = True
# Leitaeste: versetzt startend und deutlich unterschiedlich dick (keine Y-Form).
tips0 = trunk['pts'][-1][:3]
mid0 = trunk['pts'][-3][:3]
umb0 = trunk['pts'][-2][:3]
lead = add_branch(1, tips0, (0.15, 0.10, 1.0), 4.6, 0.56, 0.20, 9, curl=0.04)
umb = add_branch(1, umb0, (0.97, 0.22, 0.12), 6.4, 0.45, 0.17, 11, droop=-0.06, curl=0.20)
mid = add_branch(1, mid0, (-0.72, 0.66, 0.40), 4.8, 0.30, 0.14, 10, curl=0.12)
for main in (lead, umb, mid):
    # Stufe 2: 4-5 Zweige pro Leitas. Der Schirmast bekommt flachere Zweige
    # (Schirmkrone), der Senkrechtast steilere.
    n2 = random.choice([4, 5])
    start_choices = list(range(4, len(main['pts'])))
    random.shuffle(start_choices)
    flat = (0.25, 0.5) if main is umb else ((0.45, 0.85) if main is lead else (0.35, 0.7))
    for j in range(n2):
        t0 = main['pts'][start_choices[j % len(start_choices)]]
        a = random.uniform(0, 2 * math.pi)
        upness = random.uniform(*flat)
        dirv = (math.cos(a) * (1 - upness), math.sin(a) * (1 - upness), upness)
        b2 = add_branch(2, t0[:3], dirv, random.uniform(2.2, 3.6),
                        max(t0[3] * 0.75, 0.09), 0.06, 8, droop=0.10)
        # Stufe 3: Zweigspitzen ragen ueber die Krone hinaus (Vorbild-Detail).
        twigs = []
        for _ in range(random.choice([1, 2])):
            t2 = b2['pts'][random.randrange(5, len(b2['pts']))]
            d3 = dirv
            twigs.append(add_branch(3, t2[:3],
                                    (d3[0] * 1.1, d3[1] * 1.1, d3[2] + random.uniform(0.1, 0.4)),
                                    random.uniform(1.0, 1.6), max(t2[3] * 0.7, 0.055), 0.03, 5))
        # Blattcluster: sehr dicht an Stufe 2/3 (bis ~6 m herab), drei Ebenen
        # (oben, versetzt, haengende Unter-Ebene) fuer geschlossene Schirmkrone.
        for b in [b2] + twigs:
            for i in range(1, len(b['pts'])):
                p = b['pts'][i]
                if p[2] > 6.0 and random.random() < 0.97:
                    clusters.append([[round(p[0], 3), round(p[1], 3),
                                      round(p[2] + p[3] * 0.6, 3)], b['level']])
                    if random.random() < 0.8:
                        clusters.append([[round(p[0] + random.uniform(-0.65, 0.65), 3),
                                          round(p[1] + random.uniform(-0.65, 0.65), 3),
                                          round(p[2] - p[3] * 0.4, 3)], b['level']])
                    if random.random() < 0.4:
                        clusters.append([[round(p[0] + random.uniform(-0.9, 0.9), 3),
                                          round(p[1] + random.uniform(-0.9, 0.9), 3),
                                          round(p[2] - 0.8, 3)], b['level']])
    # Cluster auch an den Leitas-Spitzen (dichte Krone oben).
    for i in range(2, len(main['pts'])):
        p = main['pts'][i]
        if random.random() < 0.95:
            clusters.append([[round(p[0], 3), round(p[1], 3),
                              round(p[2] + p[3] * 0.8, 3)], 1])

# Endskalierung: Mittellinie auf 13.6 m -> Mesh-Grenzen (inkl. Rohr-Radien und
# Wurzelspitzen unter 0) landen sicher im Vorgabenfenster 12-15 m.
zmax = max(p[2] for b in branches for p in b['pts'])
f = 13.2 / zmax
for b in branches:
    b['pts'] = [(p[0] * f, p[1] * f, p[2] * f, p[3] * f) for p in b['pts']]
clusters = [[[c[0] * f, c[1] * f, c[2] * f], lvl] for c, lvl in clusters]

# Fuell-Cluster: schliessen Lücken der Schirmkrone. Rasterpunkte in einer
# flachen Ellipse (z 9-12.4 m); ein Cluster wird nur gesetzt, wenn ein
# Astpunkt nahe ist — nichts schwebt frei.
pts_hi = [p[:3] for b in branches for p in b['pts'] if p[2] > 7.0]
placed = tries = 0
while placed < 75 and tries < 900:
    tries += 1
    a = random.uniform(0, 2 * math.pi)
    rr = random.uniform(1.5, 6.3)
    x = math.cos(a) * rr * 1.3
    y = math.sin(a) * rr * 0.95
    z = random.uniform(9.0, 12.4) - (rr / 6.3) * 1.2
    d2 = min((p[0] - x) ** 2 + (p[1] - y) ** 2 + (p[2] - z) ** 2 for p in pts_hi)
    if d2 < 0.9:
        clusters.append([[round(x, 3), round(y, 3), round(z, 3)], 2])
        placed += 1

json.dump([pts_out(b) for b in branches], open(HERE + r'\geruest.json', 'w'))
json.dump(clusters, open(HERE + r'\cluster.json', 'w'))
zs = [p[2] for b in branches for p in b['pts']]
xs = [math.hypot(p[0], p[1]) for b in branches for p in b['pts']]
print('Zweige', len(branches), 'Cluster', len(clusters),
      'Hoehe %.2f m' % max(zs), 'Radius max %.2f m' % max(xs))
