# -*- coding: utf-8 -*-
# MODELL12 Riesenblatt1: Geometrie-Bausteine (Master-Skript).
# EIN grosses gelapptes Blatt (Alocasia/Philodendron-Typ) an aufrechtem
# Faserstiel. Blender-Z-up, Ursprung am Fusspunkt. Wird per exec() in der
# MCP-Sitzung geladen und am Ende headless exportiert (gleicher Code).
# Anlauf 2 (Selbstkritik rb1_vorne.png): Kerben waren kastig, einseitig,
# ungleich, Rippe unsichtbar. Jetzt: Randkurve w(v) oszilliert exakt an den
# Textur-Adern (v = normierte Bogenlaenge), Zacken als Ecken der Randkurve,
# Rippe als First, Stiel mit Auschwung, Grundkappe am Blattansatz.
import math
import numpy as np

SEED = 1207
RNG = np.random.default_rng(SEED)

# Zielmasse (Blatt-Achse): Laenge 1.75 m, max Halbbreite 0.92 m.
BLADE_L = 1.75
BLADE_HW = 0.92
COLS = 40            # Spalten entlang Bogenlaenge v (0 Basis .. 1 Spitze)
ROWS = 12            # Reihen Rippe (t=0) -> Rand (t=1), je Blattseite

# Lappen-/Kerbenrhythmus: Kerben bei v=k/6, Lappenmitten bei (2k+1)/12.
# Passiert die Adern der Textur (textur_numpy.py VEINS bei denselben v).
# Breiten und Kerbenpositionen mit festem Seed variiert -> weniger uniform.
NLOBE = 5
LOBE_W = [0.80, 0.95, 0.95, 0.82, 0.46]   # wenige breite Lappen, schmale Spitze
LOBE_W = [round(w * float(1 + RNG.uniform(-0.12, 0.12)), 3) for w in LOBE_W]
# Kerben bei NOTCH_V[k] (statt starr k/6), Lappenmitte = Mitte zweier Kerben.
NOTCH_V = [round(k / NLOBE + float(RNG.uniform(-0.03, 0.03)), 4)
           for k in range(NLOBE + 1)]
NOTCH_V[0] = 0.0
NOTCH_V[NLOBE] = 1.0
NOTCH_F = 0.58       # U-Buchten: Kerben flacher, Lappenkoerpen bleiben breit


def width_profile(s):
    """Huell-Halbbreite: Herzform mit breiter Basis, schmale Spitze."""
    return BLADE_HW * (0.42 + 0.92 * s ** 0.7 - 0.45 * s ** 2.6)


def margin_w(v):
    """Randhalbbreite an Bogenlaenge v: oszilliert zwischen Lappenmaxima und
    Kerbenminima; Zacken (Ecken der Kurve) genau bei den Kerben NOTCH_V."""
    k = 0
    while k < NLOBE - 1 and v >= NOTCH_V[k + 1]:
        k += 1
    a = (v - NOTCH_V[k]) / max(NOTCH_V[k + 1] - NOTCH_V[k], 1e-6)
    a = min(max(a, 0.0), 1.0)
    wl = LOBE_W[k]
    wr = LOBE_W[min(k + 1, NLOBE - 1)]
    wmax = 0.5 * (wl + wr) * width_profile(v) / max(width_profile(0.5), 1e-6)
    wmin = NOTCH_F * wmax
    # Erhobener Cosinus: abgerundete Lappen, scharfe Kerben (natuerlicher als
    # Saegezahn-Dreieck). Leichte Asymmetrie: Lappen zur Spitze gezogen.
    a2 = 0.5 + 0.62 * (a - 0.5)
    a2 = min(max(a2, 0.0), 1.0)
    # Mischung Cosinus x Dreieck: runde Lappenschultern, aber sichtbare Kerbe.
    tri = 0.45 * (1.0 - abs(2.0 * a2 - 1.0)) + 0.55 * 0.5 * (
        1.0 - math.cos(math.pi * a2))
    return wmin + (wmax - wmin) * tri


def blade_shape(v, t, half=0):
    """Punkt (x, y, z) einer Blattseite: v=Bogenlaenge 0..1, t=Rippe 0..Rand 1.
    half: 0=rechts, 1=links ( leichte Asymmetrie wie echte Blaetter)."""
    w = margin_w(v) * t
    # Seitwaerts-Biegung der Mittelachse (Schwerpunkt verlagert, natuerlicher).
    y = BLADE_L * v + 0.05 * math.sin(v * math.pi * 1.2)
    # First an der Rippe: Fluegel starten fast flach und biegen nach unten.
    fold = -0.14 * math.sin(t * math.pi * 0.30) ** 2 * (1.0 - 0.35 * v)
    rib = 0.055 * math.exp(-(t / 0.13) ** 2) * (1.0 - 0.45 * v)
    # Verdickung am Blattansatz (Uebergang aus der Blattscheide).
    rib += 0.05 * math.exp(-((t + 0.02) / 0.18) ** 2) * math.exp(-v * 4.0)
    # Pose: Bogen — steigt nahe der Basis leicht an, Spitze haengt tiefer.
    # Bogen: steigt bis v~0.3 an, danach faellt die Spitze sanft ab.
    arch = 0.42 * math.sin(min(v, 0.3) / 0.3 * math.pi / 2)         - 0.52 * max(v - 0.3, 0.0) ** 1.5
    droop = -0.30 * BLADE_L * v ** 1.7
    edge = -0.06 * BLADE_HW * max(0.0, t - 0.75) ** 2 * 4.0
    # Randwellen: klein, feinkoernig, je Seite andere Phase (Normal-Map Details).
    ph = 3.1 * half
    crinkle = 0.006 * math.sin(v * 40.0 + t * 9.0 + ph) * t * t
    return (w, y, fold + rib + arch + droop + edge + crinkle)


def build_blade():
    """Halbes Blatt als Gitter (Spalten x Reihen), Rippen-Spalte geteilt,
    dann gespiegelt. Gibt (verts, faces, uvs) zurueck; lokal Z-up."""
    verts, uvs = [], []
    col_idx = []                      # [half][i] -> Liste von Reihenindizes

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
    for col in col_idx:
        for i in range(COLS):
            for j in range(ROWS):
                a = col[i][j]; b = col[i + 1][j]
                c = col[i + 1][j + 1]; d = col[i][j + 1]
                faces.append((a, d, c, b))   # Gegen den Uhr: Normal +Z
    # Linke Seite umdrehen (Normalen bleiben nach +Z zeigen).
    nq = COLS * ROWS
    faces = [tuple(reversed(f)) for f in faces[nq:]] + faces[:nq]
    # Grundkappe am Blattansatz (Normal -Z, zeigt nach unten zum Stieltrichter).
    faces.append((col_idx[0][0][0], col_idx[0][0][ROWS], col_idx[1][0][ROWS]))
    return verts, faces, uvs


def build_tube(P0, C, P1, r0, r1, rings=16, seg=8, flare=0.0):
    """Leicht gebogene Rohren-Stiel (quadratische Bezier), UV (u Umfang, v Laeng).
    flare: Radius-Zuwachs am oberen Ende (Uebergang in die Blattspreite)."""
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
        # Faserbaender: je RING konstanter Radius (keine Spiralrillen).
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
    """Gesetztes Modell: (verts, faces, uvs, mats). mat 0=Stiel, 1=Blatt
    (Slot-Reihenfolge wie in main.py und im Test). Welt: Z nach oben, Fuss
    bei (0,0,0), Blatt neigt nach +Y."""
    tilt = math.radians(20.0)       # Blattachse steigt, Spreite bogt zur Spitze
    P0 = (0.0, 0.0, 0.005)
    P1 = (0.0, 0.16, 1.05)          # lange schlanke Blattachse bis zur Spreitenbasis
    C = (0.0, 0.03, 0.55)           # sanfter Schwung des Stiels
    stem_v, stem_f, stem_uv = build_tube(P0, C, P1, 0.052, 0.040, flare=2.2)

    bv, bf, buv = build_blade()
    ct, st = math.cos(tilt), math.sin(tilt)

    def place(p):
        x, y, z = p
        # Neigung um X: y-Achse kippt nach oben (z' = y*sin + z*cos)
        return (x, y * ct - z * st, y * st + z * ct + 1.05)

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
