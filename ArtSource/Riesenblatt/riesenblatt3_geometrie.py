# -*- coding: utf-8 -*-
# MODELL14 Riesenblatt3: Geometrie-Bausteine (Master-Skript).
# EIN ovales Bananenblatt mit eingerissenem Rand (tiefe seitliche Risse
# entlang der Bogenadern, wie zerrissene Musa-Spreiten) an aufrechtem
# Faserstiel. Blender-Z-up, Ursprung am Fusspunkt. Wird per exec() in der
# MCP-Sitzung geladen und am Ende headless exportiert (gleicher Code).
import math
import numpy as np

SEED = 1407
RNG = np.random.default_rng(SEED)

# Zielmasse: Spreitenlaenge 1.90 m, max Halbbreite ~0.75 m -> ca. 1.5 m breit.
BLADE_L = 1.90
BLADE_HW = 0.70
COLS = 44            # Spalten entlang Blattachse v (0 Basis .. 1 Spitze)
ROWS = 16            # Reihen Rippe (t=0) -> Rand (t=1), je Blattseite
                     # (Selbstkritik D3: ROWS 14 -> stufige Risskanten)
NVEIN = 22           # Bogenadern je Seite (Bananenblatt: viele parallele Adern)


def width_profile(s):
    """Huell-Halbbreite: langleich-ovales Bananenblatt (Musa) — schmalere,
    keilformige Basis, breite konstante Mitte, ueber laengere Strecke
    zulaufende, spitze Apex. Bei s=1 minimal >0 (Selbstkritik D2: exakt 0
    erzeugte entartete Zero-Width-Quads -> Faltartefakt an der Spitze)."""
    f = 0.20 + 1.75 * s ** 0.9 - 1.95 * s ** 4.2
    return BLADE_HW * max(f, 0.015)


def margin_w(v):
    """Randhalbbreite: Grundkurve mit leichter Randwelligkeit (Banane:
    welliger, aber glatter Rand — keine Saegezaehne)."""
    return width_profile(v) * (1.0 + 0.015 * math.sin(v * 60.0 + 0.7))


# Risse je Blattseite (eingerissener Rand): (v, av, u_in, slant) — Ellipse in
# (v,u) mit Mittelpunkt u=1.45 (ausserhalb des Rands), die vom Rand (u=1) bis
# u=u_in reicht. u_in klein = tiefer Riss fast bis zur Rippe. slant: Schraege
# des Schnitts entlang der Bogenadern (Riss laeuft zur Rippe hin flacher aus).
# Banane: Risse am dichtesten in der Spreitenmitte, Spitze und Basis sauber;
# Haupt- und Sekundaerrisse unregelmaessig verzahnt (kein Kamm-Muster).
def _tears(n, v0, v1, u_lo, u_hi, seed_off, jitter=0.035):
    rng = np.random.default_rng(SEED + seed_off)
    vs = [v0 + (v1 - v0) * (k + 0.5) / n + float(rng.uniform(-jitter, jitter))
          for k in range(n)]
    out = []
    for k, vk in enumerate(vs):
        # Tiefe: mittige Risse tief (bis u~0.26), zu Basis/Spitze flacher.
        depth = 0.5 - 0.5 * math.cos(math.pi * min(1.0, 1.6 * (1.0 - abs(vk - 0.55) / 0.38)))
        u_in = (u_lo + (u_hi - u_lo) * (1.0 - depth)) + float(rng.uniform(-0.07, 0.07))
        av = round(0.020 + 0.012 * depth + float(rng.uniform(-0.004, 0.005)), 4)
        slant = round(float(rng.uniform(0.02, 0.05)), 3)
        out.append((round(vk, 4), av, round(min(max(u_in, 0.26), 0.95), 3), slant))
    return out


TEARS = [_tears(7, 0.17, 0.90, 0.26, 0.62, 11) + _tears(5, 0.14, 0.88, 0.76, 0.92, 13, 0.05),
         _tears(6, 0.20, 0.88, 0.26, 0.60, 23) + _tears(5, 0.17, 0.91, 0.74, 0.90, 29, 0.05)]
for _h in range(2):
    TEARS[_h].sort()


def in_cutout(v, u, half):
    """True = Zelle wird entfernt (Riss). Ellipse in (v,u), scharfe Grenze:
    Bananenriss ist ein glatter Schnitt, kein Ausfransen der Schnittflaeche.
    slant zieht den Schnitt entlang der Bogenadern schraeg (zur Spitze)."""
    for (vk, av, u_in, slant) in TEARS[half]:
        a_u = 1.45 - u_in
        vc = vk + slant * (u - 0.7)
        if abs(v - vc) > av * 1.25:
            continue
        dv = (v - vc) / av
        du = (u - 1.45) / a_u
        if dv * dv + du * du <= 1.0:
            return True
    return False


def vein_dv(v):
    """Abstand zu naechster Bogenader (normiert in v)."""
    x = v * NVEIN
    return abs(x - round(x)) / NVEIN


def blade_shape(v, t, half=0):
    """Punkt (x, y, z) einer Blattseite: v=Laenge 0..1, t=Rippe 0..Rand 1.
    half: 0=rechts, 1=links."""
    w = margin_w(v) * t
    y = BLADE_L * v
    # Bananenblatt: V-First an der Rippe, Fluegel fallen nach unten ab.
    fold = -0.20 * math.sin(t * math.pi * 0.34) ** 2 * (1.0 - 0.25 * v)
    rib = 0.070 * math.exp(-(t / 0.11) ** 2) * (1.0 - 0.40 * v)
    rib += 0.065 * math.exp(-((t + 0.03) / 0.20) ** 2) * math.exp(-v * 2.6)
    # Leichte Mulde quer zur Mittelrippe.
    dome = 0.045 * math.cos(t * math.pi * 0.55) * (0.35 + 0.65 * min(v, 1.0))
    # Bogenader-Relief: feine, unregelmaessig phasenverschobene parallele Adern
    # (Selbstkritik D1: zu staerkes Wellpappen-Relief -> deutlich flacher).
    vein = 0.006 * math.exp(-(vein_dv(v) / 0.010) ** 2) * t \
        * (1.0 + 0.35 * math.sin(v * 17.0 + half * 2.3))
    # Pose: steigt bis v~0.3 an, danach haengt die Spitze gleichmaessig durch
    # (Banane). Selbstkritik D3: Knick bei v=0.25 zu hart -> Faltartefakt;
    # Uebergang weicher (Exponent 1.55 -> 1.3, Beginn spaeter).
    arch = 0.42 * math.sin(min(v, 0.30) / 0.30 * math.pi / 2) \
        - 0.60 * max(v - 0.30, 0.0) ** 1.3
    droop = -0.24 * BLADE_L * v ** 1.9
    edge = -0.05 * BLADE_HW * max(0.0, t - 0.72) ** 2 * 4.0
    ph = 3.1 * half
    crinkle = 0.006 * math.sin(v * 41.0 + t * 9.0 + ph) * t * t
    return (w, y, fold + rib + dome + vein + arch + droop + edge + crinkle)


def build_blade():
    """Halbes Blatt als Gitter, Risse ausschneiden (je Seite eigener Riss-
    satz), Rippenpalte geteilt, dann gespiegelt. Gibt (verts, faces, uvs)
    zurueck; Z-up."""
    verts, uvs = [], []
    col_idx = []                      # [half][i][j] -> Vertexindex

    def add(p, uv):
        verts.append(p); uvs.append(uv)
        return len(verts) - 1

    for half in range(2):
        col = []
        for i in range(COLS + 1):
            v = i / COLS
            cell = [add(blade_shape(v, 0.0, half), (0.0, v))]
            for j in range(1, ROWS + 1):
                t = 0.5 * (1.0 - math.cos(math.pi * j / ROWS))
                p = blade_shape(v, t, half)
                if half:
                    p = (-p[0], p[1], p[2])
                cell.append(add(p, (t, v)))
            col.append(cell)
        col_idx.append(col)

    faces = []
    counts = []
    for half in range(2):
        col = col_idx[half]
        sub = []
        for i in range(COLS):
            vc = (i + 0.5) / COLS
            for j in range(ROWS):
                tc = 0.5 * (1.0 - math.cos(math.pi * (j + 0.5) / ROWS))
                if in_cutout(vc, tc, half):
                    continue
                a = col[i][j]; b = col[i + 1][j]
                c = col[i + 1][j + 1]; d = col[i][j + 1]
                sub.append((a, d, c, b))     # Gegen den Uhr: Normal +Z
        counts.append(len(sub))
        faces.extend(sub)
    # Linke Haelfte (hinten im Array, eigene Risse -> eigene Anzahl) umdrehen.
    n_right = counts[0]
    left = [tuple(reversed(f)) for f in faces[n_right:]]
    faces = faces[:n_right] + left
    return verts, faces, uvs


def build_tube(P0, C, P1, r0, r1, rings=14, seg=8, flare=0.0):
    """Leicht gebogene Rohren-Stiel (quadratische Bezier), UV (u Umfang,
    v Laeng). flare: Radius-Zuwachs am oberen Ende (Uebergang in die Blattspreite)."""
    P0, C, P1 = np.array(P0, float), np.array(C, float), np.array(P1, float)
    pts = [(1 - k) ** 2 * P0 + 2 * (1 - k) * k * C + k ** 2 * P1
           for k in np.linspace(0, 1, rings)]
    verts, uvs = [], []
    for i, p in enumerate(pts):
        tan = pts[min(i + 1, rings - 1)] - pts[max(i - 1, 0)]
        tan /= max(np.linalg.norm(tan), 1e-9)
        side = np.cross(tan, [0.0, 0.0, 1.0])
        if np.linalg.norm(side) < 1e-4:
            side = np.array([1.0, 0.0, 0.0])
        side /= np.linalg.norm(side)
        up = np.cross(side, tan)
        f = flare * (i / (rings - 1)) ** 3
        r = (r0 + (r1 - r0) * i / (rings - 1)) * (1.0 + f) \
            * (1.0 + 0.05 * math.sin(i * 2.4))
        for j in range(seg + 1):
            a = j / seg * 2.0 * math.pi
            verts.append(tuple(p + r * (math.cos(a) * side + math.sin(a) * up)))
            uvs.append((j / seg, i / (rings - 1)))
    faces = []
    for i in range(rings - 1):
        for j in range(seg):
            a = i * (seg + 1) + j
            faces.append((a, a + seg + 1, a + seg + 2, a + 1))
    return verts, faces, uvs


def build_all():
    """Gesetztes Modell: (verts, faces, uvs, mats). mat 0=Stiel, 1=Blatt.
    Welt: Z nach oben, Fuss bei (0,0,0), Blatt neigt nach +Y."""
    tilt = math.radians(20.0)
    # Selbstkritik D2: Stielende war offen und die Rippenleiste ragte als
    # Zacken ueber die Stielmuendung -> Stiel reicht jetzt ins Blatt hinein.
    p1z = 1.14
    P0 = (0.0, 0.0, 0.005)
    P1 = (0.0, 0.16, p1z)             # Blattachse bis zur Spreitenbasis
    C = (0.0, 0.03, 0.55)
    # Selbstkritik D1: duerrer Stiel -> dicker; D4: flare-Breite war breiter
    # als die schmale Blattbasis -> Stielstirnflaeche/Zipfel sichtbar -> 1.2.
    stem_v, stem_f, stem_uv = build_tube(P0, C, P1, 0.062, 0.056, flare=1.2)

    bv, bf, buv = build_blade()
    ct, st = math.cos(tilt), math.sin(tilt)

    def place(p):
        x, y, z = p
        return (x, y * ct - z * st, y * st + z * ct + p1z)

    n0 = len(stem_v)
    verts = list(stem_v) + [place(p) for p in bv]
    uvs = list(stem_uv) + buv
    faces = list(stem_f) + [tuple(k + n0 for k in f) for f in bf]
    mats = [0] * len(stem_f) + [1] * len(bf)
    return verts, faces, uvs, mats


def stats():
    v, f, uv, m = build_all()
    tris = sum(len(q) - 2 for q in f)
    import numpy as _np
    a = _np.array(v)
    ext = a.max(0) - a.min(0)
    print("verts=%d faces=%d tris=%d" % (len(v), len(f), tris))
    print("extent X %.3f Y %.3f Z %.3f m" % tuple(ext))
    print("z min %.3f max %.3f" % (a[:, 2].min(), a[:, 2].max()))
    return tris
