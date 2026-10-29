# MODELL24 (Baum_Island02): prozedurales Geruest fuer den Insel-Baum 2.
# Laeuft in Blender (per exec aus dem MCP) oder headless. Seed 2402, deterministisch.
# Charakter (bewusst ANDERER Wuchs als Insel-Baum 1): schlanker, fast gerader
# Leittrieb mit sauberem Taper, ~7 Grad Neigung (ausbalanciert, nicht umkippend),
# spiralige Kurzast-Haare ueber die ganze Staemmung (Goldener Winkel),
# schmal-ovale/eggfoermige Krone (Radius ~4.2 m), flacher Wurzelfuecher ohne
# Tritte (K5-Lehre). Schreibt geruest.json (Zweig-Polylinien) + cluster.json.
import json, math, random

HERE = r'C:\Users\vorau\dev\Spinnenspiel\ArtSource\Baum_Island02'
random.seed(2402)

def pts_out(branch):
    return {'level': branch['level'],
            'pts': [[round(p[0], 3), round(p[1], 3), round(p[2], 3), round(p[3], 3)]
                    for p in branch['pts']]}

branches = []
clusters = []

def add_branch(level, start, direction, length, r0, r1, npts, droop=0.0, curl=None):
    """Polylinie entlang direction; droop zieht die Spitze nach unten (Hastaenge)."""
    d = direction
    ll = math.sqrt(d[0]**2 + d[1]**2 + d[2]**2)
    d = (d[0]/ll, d[1]/ll, d[2]/ll)
    side = random.uniform(-1, 1)
    axis = (-d[1], d[0], 0.0)
    al = math.sqrt(axis[0]**2 + axis[1]**2) or 1.0
    axis = (axis[0]/al, axis[1]/al, 0.0)
    freq = random.uniform(1.6, 2.6)      # sanftere Biegung als Insel01 (glatter Wuchs)
    phase = level + random.uniform(0.0, 1.5)
    pts = []
    p = start
    for i in range(npts):
        t = i / (npts - 1)
        r = r0 + (r1 - r0) * t
        wob = 0.04 + 0.04 * level
        off = math.sin(t * freq + phase) * wob * length
        dz = -droop * t * t * length
        c = (curl or 0.0) * t * t * length
        step = (d[0] + axis[0] * off / max(length, 0.1) * 0.25,
                d[1] + axis[1] * off / max(length, 0.1) * 0.25,
                d[2] + c / max(length, 0.1) * 0.5)
        p = (p[0] + step[0] * length / (npts - 1),
             p[1] + step[1] * length / (npts - 1),
             p[2] + step[2] * length / (npts - 1) + dz / (npts - 1))
        pts.append((p[0], p[1], p[2], r))
    br = {'level': level, 'pts': pts}
    branches.append(br)
    return br


# --- Leittrieb: schlank, fast gerade, sauberer Taper, ~7 Grad Neigung ---
trunk = add_branch(0, (0.0, 0.0, 0.0), (0.08, -0.03, 1.0), 12.3, 0.62, 0.10, 14, curl=0.012)
# add_branch haengt den ERSTEN Punkt erst nach einem Schritt an -> Stamm wuerde
# ~0.9 m ueber Boden schweben. Echten Fusspunkt auf Bodenhoehe voranstellen.
trunk['pts'].insert(0, (0.0, 0.0, -0.02, 1.10))
# Flacher Wurzelfuecher: nur die untersten Ringe verbreitert (sichtbarer Stand).
# K5-Lehre: flach halten, sonst bauen Spinnen-Fuessen die Rippen Tritte.
trunk['pts'][1] = trunk['pts'][1][:3] + (0.92,)
trunk['pts'][2] = trunk['pts'][2][:3] + (0.76,)

# --- Spiralige Kurzast-Haare: Goldener Winkel, ueber die ganze Staemmung ---
def trunk_pt(z):
    """Punkt auf der Leittrieb-Achse bei Hoehe z (inkl. Radius)."""
    tp = trunk["pts"]
    for i in range(len(tp) - 1):
        if tp[i][2] <= z <= tp[i+1][2]:
            f = (z - tp[i][2]) / max(1e-6, tp[i+1][2] - tp[i][2])
            return tuple(tp[i][k] + (tp[i+1][k] - tp[i][k]) * f for k in range(4))
    return tp[-1]

GA = 137.5 * math.pi / 180.0
ks = list(range(9))
for k in ks:
    z = 3.4 + k * (8.6 / (len(ks) - 1))            # 3.4 m bis 12.0 m
    egg = math.sin(math.pi * (z - 3.0) / 9.6)      # Eiform: Mitte lange, oben/kurz unten kurz
    length = 1.5 + 1.6 * max(0.3, egg) + random.uniform(-0.2, 0.2)
    a = k * GA + random.uniform(-0.22, 0.22)
    upness = 0.30 + 0.15 * ((z - 3.4) / 8.6)       # flacher unten, leicht aufwaerts oben
    dirv = (math.cos(a) * (1 - upness), math.sin(a) * (1 - upness), upness)
    base = trunk_pt(z)
    b1 = add_branch(1, base[:3], dirv, length, max(base[3] * 0.62, 0.10), 0.05, 7,
                    droop=0.02 + 0.02 * random.random())
    # Stufe 2: 2-4 kurze Zweige ueber die Astlaenge verteilt (kein reiner Endbusch).
    for j in range(random.choice([2, 3, 4])):
        t1 = b1["pts"][random.randrange(2, len(b1["pts"]))]
        a2 = a + random.uniform(-1.4, 1.4)
        up2 = upness + random.uniform(-0.15, 0.25)
        d2 = (math.cos(a2) * (1 - up2), math.sin(a2) * (1 - up2), max(up2, 0.1))
        add_branch(2, t1[:3], d2, random.uniform(0.9, 1.8),
                   max(t1[3] * 0.7, 0.05), 0.028, 5)



# --- Blattcluster entlang der Aeste (Kronenhaare bis ~2.5 m herab) ---
for b in branches:
    if b["level"] < 1:
        continue
    for i in range(1, len(b["pts"])):
        p = b["pts"][i]
        if p[2] > 2.5 and random.random() < 0.95:
            clusters.append([[round(p[0], 3), round(p[1], 3),
                              round(p[2] + p[3] * 0.7, 3)], b["level"]])
            if random.random() < 0.85:
                clusters.append([[round(p[0] + random.uniform(-0.5, 0.5), 3),
                                  round(p[1] + random.uniform(-0.5, 0.5), 3),
                                  round(p[2] - p[3] * 0.4, 3)], b["level"]])
    # Astspitzen IMMER bedecken (Selbstkritik D2: kahle Stummel ruinierten die Silhouette).
    tp = b["pts"][-1]
    clusters.append([[round(tp[0], 3), round(tp[1], 3), round(tp[2] + tp[3], 3)], b["level"]])

# Endskalierung: Mittellinie auf 13.3 m -> Mesh-Grenzen im Fenster 12-15 m.
zmax = max(p[2] for b in branches for p in b["pts"])
f = 13.3 / zmax
for b in branches:
    b["pts"] = [(p[0] * f, p[1] * f, p[2] * f, p[3] * f) for p in b["pts"]]
clusters = [[[c[0] * f, c[1] * f, c[2] * f], lvl] for c, lvl in clusters]

# Fuell-Cluster: schliessen Lücken der Eikrone. Rasterpunkte im Ellipsoid
# (Zentrum ~8.6 m, rx ~4.0, rz ~4.2); nur setzen, wenn ein Astpunkt nahe ist.
pts_hi = [p[:3] for b in branches for p in b["pts"] if p[2] > 3.0]
placed = tries = 0
while placed < 130 and tries < 4000:
    tries += 1
    a = random.uniform(0, 2 * math.pi)
    rr = random.random() ** 0.55
    x = math.cos(a) * rr * 2.9
    y = math.sin(a) * rr * 2.7
    z = 8.4 + math.sqrt(max(0.0, 1 - (rr * rr))) * random.uniform(0.2, 1.0) * 3.6
    d2 = min((p[0] - x) ** 2 + (p[1] - y) ** 2 + (p[2] - z) ** 2 for p in pts_hi)
    if d2 < 1.6:
        clusters.append([[round(x, 3), round(y, 3), round(z, 3)], 2])
        placed += 1

json.dump([pts_out(b) for b in branches], open(HERE + r"\geruest.json", "w"))
json.dump(clusters, open(HERE + r"\cluster.json", "w"))
zs = [p[2] for b in branches for p in b["pts"]]
xs = [math.hypot(p[0], p[1]) for b in branches for p in b["pts"]]
print("Zweige", len(branches), "Cluster", len(clusters),
      "Hoehe %.2f m" % max(zs), "Radius max %.2f m" % max(xs))
