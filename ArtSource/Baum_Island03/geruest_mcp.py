# MODELL25 (Baum_Island03): prozedurales Geruest fuer den Insel-Baum 3.
# Laeuft in Blender (per exec aus dem MCP) oder headless (reine Stdlib). Seed 2503.
# Charakter (bewusst ANDERER Wuchs als Insel-Baeume 1 und 2): deutlich
# geneigter, bogig aufwaerts geschwungener Leittrieb (~28 Grad Fussneigung),
# daran 4 dicke fast waagerechte Hauptaste, darauf ein breiter flacher
# Schirm (Krone ~10.5-13.5 m, Radius bis ~6 m). Kein dichter Spiral-Kurzast-
# Bewuchs wie bei Insel02, statt dessen wenige krftige Etagen mit Sekundaerasten.
# Schreibt geruest.json (Zweig-Polylinien) + cluster.json.
import json, math, random

HERE = r'C:\Users\vorau\dev\Spinnenspiel\ArtSource\Baum_Island03'
random.seed(2503)

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
    freq = random.uniform(1.4, 2.2)      # wenige, weiche Biegungen (Schirmast-Charakter)
    phase = level + random.uniform(0.0, 1.5)
    pts = []
    p = start
    for i in range(npts):
        t = i / (npts - 1)
        r = r0 + (r1 - r0) * t
        wob = 0.03 + 0.03 * level
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

# --- Leittrieb: deutlich geneigt ( Fuss ~26 Grad), bogig aufwaerts geschwungen,
# Spitze fast aufrecht. Anker-Polylinie, dann weich interpoliert.
anchors = [
    (0.00,  0.00, -0.02, 1.05),   # Fusspunkt auf Bodenhoehe, flacher Wurzelsockel
    (0.35, -0.07,  1.60, 0.88),
    (0.85, -0.17,  3.40, 0.74),
    (1.45, -0.29,  5.40, 0.62),
    (2.15, -0.42,  7.40, 0.50),
    (2.75, -0.52,  9.20, 0.40),
    (3.25, -0.58, 10.80, 0.30),
    (3.60, -0.62, 12.20, 0.20),
    (3.85, -0.64, 13.30, 0.12),
]
def lerp(a, b, f):
    return tuple(a[k] + (b[k] - a[k]) * f for k in range(4))
trunk_pts = []
for i in range(len(anchors) - 1):
    seg = int(round((anchors[i+1][2] - anchors[i][2]) / 0.9)) + 2
    for j in range(seg):
        f = j / seg
        trunk_pts.append(lerp(anchors[i], anchors[i+1], f))
trunk_pts.append(anchors[-1])
# weiche Wulst-Asymmetrie schon in der Achse (Bogen nach Suedost, Naturform)
for i, p in enumerate(trunk_pts):
    w = 0.05 * math.sin(i * 0.9)
    trunk_pts[i] = (p[0] + w, p[1] - w * 0.6, p[2], p[3])
# Selbstkritik D2: die Anker-Knicks der Polylinie waren die "Perlenkette".
# Achse laplace-glaetten (Endpunkte fix), Radius als gleitende Funktion statt
# Anker-Stufen: unten Fuss-Ausweitung, dann monotones Taper bis zur Spitze.
for _ in range(4):
    old = trunk_pts[:]
    for i in range(1, len(old) - 1):
        mx = (old[i-1][0] + old[i+1][0]) / 2.0
        my = (old[i-1][1] + old[i+1][1]) / 2.0
        mz = (old[i-1][2] + old[i+1][2]) / 2.0
        trunk_pts[i] = lerp(trunk_pts[i], (mx, my, mz, trunk_pts[i][3]), 0.5)
zmax_t = trunk_pts[-1][2]
for i, p in enumerate(trunk_pts):
    z = max(0.0, p[2])
    r = 1.00 * (1.0 - min(z / (zmax_t + 0.5), 0.97)) ** 0.78
    fl = max(0.0, 1.0 - z / 3.0)
    r *= 1.0 + 0.24 * fl * fl                # Wurzelanlauf: breit, sichtbar, stetig
    trunk_pts[i] = p[:3] + (r,)
trunk = {'level': 0, 'pts': trunk_pts}
branches.append(trunk)

def trunk_at(z):
    """Punkt auf der Leittrieb-Achse bei Hoehe z (inkl. Radius)."""
    tp = trunk["pts"]
    for i in range(len(tp) - 1):
        if tp[i][2] <= z <= tp[i+1][2]:
            f = (z - tp[i][2]) / max(1e-6, tp[i+1][2] - tp[i][2])
            return tuple(tp[i][k] + (tp[i+1][k] - tp[i][k]) * f for k in range(4))
    return tp[-1]

# --- 4 dicke Schirm-Hauptaste: fast waagerecht, Spitzen leicht aufgebogen.
# Selbstkritik D4: 5 Aeste in hoeherer Etage (9.0-12.2), etwas ansteigend —
# darunter bleibt der Mast bis ~9 m frei, die Speichen werden lesbar; mehr
# Aeste fuellen die Kronenscheibe gleichmaessig (keine drei Lappen).
GA = 137.5 * math.pi / 180.0
zs = [9.4, 10.2, 10.9, 11.5, 12.0]
mains = []
for k, z in enumerate(zs):
    a = k * GA + random.uniform(-0.3, 0.3)
    upness = random.uniform(0.10, 0.22)           # Schirm: flach bis leicht steigend
    dirv = (math.cos(a) * (1 - upness), math.sin(a) * (1 - upness), upness)
    base = trunk_at(z)
    length = random.uniform(4.6, 5.8) if k < 3 else random.uniform(3.2, 4.2)
    b1 = add_branch(1, base[:3], dirv, length,
                    max(base[3] * 0.75, 0.20), 0.08, 8,
                    droop=0.015, curl=0.11)       # erst minimal hangend, dann aufgebogen
    mains.append((b1, a))

# --- Sekundaeraste: wenige, aber lange Aufwaerts-Zweige ueber dem Schirm ---
for b1, a in mains:
    for j in range(random.choice([3, 4])):
        t1 = b1["pts"][random.randrange(max(2, int(len(b1["pts"]) * 0.4)), len(b1["pts"]))]
        a2 = a + random.uniform(-1.1, 1.1)
        up2 = random.uniform(0.28, 0.45)          # flaches Schirmdach, kein Senkrecht
        d2 = (math.cos(a2) * (1 - up2), math.sin(a2) * (1 - up2), up2)
        add_branch(2, t1[:3], d2, random.uniform(1.6, 2.4),
                   max(t1[3] * 0.62, 0.05), 0.03, 6, curl=0.04)
# Selbstkritik D9: kein Kronenaufsatz — die Spitze endet IN der flachen
# Schirmoberkante (kein Zacken, der die flache Oberlinie bricht).

# --- Blattcluster entlang der Aeste (Schirmdach ab ~6 m, Unterraeume offen) ---
for b in branches:
    if b["level"] < 1:
        continue
    for i in range(1, len(b["pts"])):
        p = b["pts"][i]
        # D5: an Hauptaesten bleibt die innere Haelfte kahl (Sicht auf die
        # Speichen unter dem Dach), erst die aeussere Haelfte traegt Laub.
        inner_ok = b["level"] == 2 or i >= len(b["pts"]) * 0.45
        if p[2] > 8.8 and inner_ok and random.random() < 0.95:
            clusters.append([[round(p[0], 3), round(p[1], 3),
                              round(p[2] + p[3] * 0.7, 3)], b["level"]])
            if random.random() < 0.45:
                clusters.append([[round(p[0] + random.uniform(-0.55, 0.55), 3),
                                  round(p[1] + random.uniform(-0.55, 0.55), 3),
                                  round(p[2] - p[3] * 0.4, 3)], b["level"]])
    tp = b["pts"][-1]
    clusters.append([[round(tp[0], 3), round(tp[1], 3), round(tp[2] + tp[3], 3)], b["level"]])
    if b["level"] == 1:  # D4: auch den vorletzten Punkt polstern (keine kahlen Spitzen)
        q = b["pts"][-2]
        clusters.append([[round(q[0], 3), round(q[1], 3), round(q[2] + q[3] * 0.8, 3)], b["level"]])

# Endskalierung: Mittellinie auf 13.4 m -> Mesh-Grenzen im Fenster 12-15 m.
zmax = max(p[2] for b in branches for p in b["pts"])
f = 13.4 / zmax
for b in branches:
    b["pts"] = [(p[0] * f, p[1] * f, p[2] * f, p[3] * f) for p in b["pts"]]
clusters = [[[c[0] * f, c[1] * f, c[2] * f], lvl] for c, lvl in clusters]

# Fuell-Cluster: flaches Schirmdach als Ellipsoid-Schale (Zentrum ~10.8 m,
# rx ~5.4, ry ~5.0, rz ~2.2). Nur setzen, wenn ein Astpunkt nahe ist, damit
# der Schirm nicht ueber leeren Luftraum "schwebt" (Schwaechenlehre MODELL23).
pts_hi = [p[:3] for b in branches for p in b["pts"] if p[2] > 7.0]
CEN = (3.5, -0.65, 11.4)

def fill_band(cen, rx, ry, rz, target, near2, rmin=0.0):
    placed = tries = 0
    while placed < target and tries < 6000:
        tries += 1
        a = random.uniform(0, 2 * math.pi)
        rr = rmin + (1.0 - rmin) * random.random() ** 0.5
        x = cen[0] + math.cos(a) * rr * rx
        y = cen[1] + math.sin(a) * rr * ry
        z = cen[2] + math.sqrt(max(0.0, 1 - rr * rr)) * random.uniform(-0.5, 1.0) * rz
        if z < 10.9:                       # D10: nichts abhaengendes unter der Schale
            continue
        d2 = min((p[0] - x) ** 2 + (p[1] - y) ** 2 + (p[2] - z) ** 2 for p in pts_hi)
        if d2 < near2:
            clusters.append([[round(x, 3), round(y, 3), round(z, 3)], 2])
            placed += 1
    return placed

# Selbstkritik D8: die Blattschale (bau_lib) traegt die zusammenhaengende
# Scheibe; Fuell-Cluster nur noch als Randpolster darueber — weniger, hoeher,
# damit keine abgeloesten Wolken unter dem Dach haengen.
p1 = fill_band((3.5, -0.65, 11.9), 5.7, 5.3, 0.8, 190, 4.0)
p2 = fill_band((3.5, -0.65, 11.3), 5.1, 4.9, 0.6, 110, 3.5, rmin=0.45)
print("Fuell-Cluster oben", p1, "Platte", p2)

json.dump([pts_out(b) for b in branches], open(HERE + r"\geruest.json", "w"))
json.dump(clusters, open(HERE + r"\cluster.json", "w"))
zs = [p[2] for b in branches for p in b["pts"]]
xs = [math.hypot(p[0] - 3.1, p[1] + 0.6) for b in branches for p in b["pts"]]
print("Zweige", len(branches), "Cluster", len(clusters),
      "Hoehe %.2f m" % max(zs), "Radius max %.2f m" % max(xs))

