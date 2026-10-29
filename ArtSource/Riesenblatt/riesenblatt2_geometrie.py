# -*- coding: utf-8 -*-
# MODELL13 Riesenblatt2: Geometrie-Bausteine (Master-Skript).
# EIN grosses Monstera-artiges Blatt (herzfoermige Spreite mit tiefen seitlichen
# Schnittschlitzen und inneren Fensterloechern) an aufrechtem Faserstiel.
# Blender-Z-up, Ursprung am Fusspunkt. Wird per exec() in der MCP-Sitzung
# geladen und am Ende headless exportiert (gleicher Code).
import math
import numpy as np

SEED = 1307
RNG = np.random.default_rng(SEED)

# Zielmasse: Spreitenlaenge 1.60 m, max Halbbreite 0.78 m -> Blattbreite ~1.5 m.
BLADE_L = 1.60
BLADE_HW = 0.78
COLS = 42            # Spalten entlang Blattachse v (0 Basis .. 1 Spitze)
ROWS = 20            # Reihen Rippe (t=0) -> Rand (t=1), je Blattseite

# Monstera: 6 Seitenschlitze je Seite (bei SPLIT_V), durch Stege getrennt.
# Der letzte Schlitz liegt bei ~0.79, damit die Spitze als cleaner Apex
# ueber den letzten Schlitz hinauslaeuft (kein Querbalken oben).
NLOBE = 6
SPLIT_V = [round(k / (NLOBE + 0.5) + float(RNG.uniform(-0.015, 0.015)), 4)
           for k in range(NLOBE)]
SPLIT_V[0] = 0.105   # unterster Schlitz: Ellipsenunterkante bei ~0.06 (frei von der Spreitenbasis)
# Schlitzgeometrie in (v,u)-Einheit: schmale, radiale Schnitte (lang entlang u,
# schmal entlang v), die vom Rand (u~1.26) bis u~0.58 reichen — breite Lappen,
# schmale Schlitze (Monstera: Schnitte folgen den Bogenadern, enden vor der Rippe).
SPLIT_CU = 0.92
SPLIT_AU = 0.34
SPLIT_AV = [round(0.035 * float(RNG.uniform(0.85, 1.15)), 4) for _ in range(NLOBE)]
# Spitze: letzter Schlitz naeher an die Spitze -> Apexband wird schmaler
SPLIT_V[-1] = 0.72
# Fensterloecher (innere Ellipsen, hv,hu,av,au): laenglich ENTLANG der Adern
# (u-Halbachse gross), nahe der Rippe, erst ab Spreitenmitte (wie echte
# Monstera). hu klein halten -> getrennt von den Schlitzen (eigene Stege).
HOLES = []
for _k in range(2, NLOBE - 1):
    _vm = 0.5 * (SPLIT_V[_k] + SPLIT_V[_k + 1])
    HOLES.append((round(_vm + float(RNG.uniform(-0.006, 0.006)), 4),
                  round(0.30 + float(RNG.uniform(-0.012, 0.012)), 3),
                  round(0.048 + float(RNG.uniform(-0.006, 0.008)), 3),
                  round(0.12 + float(RNG.uniform(-0.008, 0.012)), 3)))


def width_profile(s):
    """Huell-Halbbreite: herzfoermig, Maximum bei ~0.45 der Laenge, laeuft bei
    s=1 auf 0 aus (akuminate Spitze) und bei s=0 schmal zu (Spreitenbasis)."""
    return BLADE_HW * max(0.18 + 1.50 * s ** 0.62 - 1.68 * s ** 3.4, 0.0)


def lobe_amp(v):
    """Randmodulation: flache Einschnitte AN den Schlitzen (Gauelse um die
    Schlitzmitte), breite Lappen dazwischen (Monstera-Rand wellig)."""
    amp = 0.0
    for sv in SPLIT_V:
        amp += math.exp(-((v - sv) / 0.055) ** 2)
    return min(amp, 1.0) * 0.12


def w_base(v):
    return width_profile(v) * (1.0 - lobe_amp(v))


def in_cutout(v, u):
    """True = Pixel wird entfernt (Schlitz oder Fenster). Ellipsen in (v,u),
    scharfe Grenze (Monstera-Rand ist glatt und ganz)."""
    for k in range(NLOBE):
        a_v = SPLIT_AV[k]
        if abs(v - SPLIT_V[k]) > a_v * 1.25:
            continue
        dv = (v - SPLIT_V[k]) / a_v
        du = (u - SPLIT_CU) / SPLIT_AU
        if dv * dv + du * du <= 1.0:
            return True
    for (hv, hu, av, au) in HOLES:
        dv = (v - hv) / av
        du = (u - hu) / au
        if dv * dv + du * du <= 1.0:
            return True
    return False


# Bogenadern: je eine Ader in der Mitte zwischen zwei Schlitzen plus eine
# zur Spitze (7 Adern je Seite). VEIN_V = v-Werte, wo die Ader den Rand trifft.
VEIN_V = [round(0.5 * (SPLIT_V[k] + SPLIT_V[k + 1]), 4) for k in range(NLOBE - 1)]
VEIN_V.append(0.93)   # Spitze


def lobe_vein_v(v):
    """Normierte Spaltennummer der Bogenader zum Lappenmittelpunkt bei v."""
    n = len(VEIN_V)
    k = 0
    while k < n and v >= VEIN_V[k]:
        k += 1
    k = min(max(k - 1, 0), n - 2)
    a = (v - VEIN_V[k]) / max(VEIN_V[k + 1] - VEIN_V[k], 1e-6)
    a2 = min(max(0.5 - 0.48 * (a - 0.5), 0.0), 1.0)
    return COLS * (VEIN_V[k] + a2 * (VEIN_V[k + 1] - VEIN_V[k]))


def blade_shape(v, t, half=0):
    """Punkt (x, y, z) einer Blattseite: v=Laenge 0..1, t=Rippe 0..Rand 1.
    half: 0=rechts, 1=links."""
    w = w_base(v) * t
    y = BLADE_L * v
    # First an der Rippe: Fluegel starten fast flach, biegen nach unten.
    fold = -0.16 * math.sin(t * math.pi * 0.32) ** 2 * (1.0 - 0.30 * v)
    rib = 0.060 * math.exp(-(t / 0.12) ** 2) * (1.0 - 0.45 * v)
    rib += 0.05 * math.exp(-((t + 0.02) / 0.18) ** 2) * math.exp(-v * 4.0)
    # Woelbung: Querwlbung nach oben (Monstera-Spreite muldig).
    dome = 0.075 * math.cos(t * math.pi * 0.55) * (0.35 + 0.65 * min(v, 1.0))
    # Bogenader-Relief: sanfter Wall entlang der Bogenseitenadern.
    i_ref = lobe_vein_v(v)
    dv_vein = (COLS * v - i_ref) / COLS
    vein = 0.012 * math.exp(-(dv_vein / 0.020) ** 2) * t
    # Pose: steigt bis v~0.3 an, Spitze haengt sanft durch.
    arch = 0.40 * math.sin(min(v, 0.3) / 0.3 * math.pi / 2) \
        - 0.50 * max(v - 0.3, 0.0) ** 1.5
    droop = -0.28 * BLADE_L * v ** 1.7
    edge = -0.06 * BLADE_HW * max(0.0, t - 0.75) ** 2 * 4.0
    ph = 3.1 * half
    crinkle = 0.006 * math.sin(v * 38.0 + t * 9.0 + ph) * t * t
    return (w, y, fold + rib + dome + vein + arch + droop + edge + crinkle)


def build_blade():
    """Halbes Blatt als Gitter, Masken auschneiden (Schlitze+Fenster), Rippen-
    spalte geteilt, dann gespiegelt. Gibt (verts, faces, uvs) zurueck; Z-up."""
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
    for col in col_idx:
        for i in range(COLS):
            vc = (i + 0.5) / COLS
            for j in range(ROWS):
                tc = 0.5 * (1.0 - math.cos(math.pi * (j + 0.5) / ROWS))
                if in_cutout(vc, tc):
                    continue
                a = col[i][j]; b = col[i + 1][j]
                c = col[i + 1][j + 1]; d = col[i][j + 1]
                faces.append((a, d, c, b))   # Gegen den Uhr: Normal +Z
    # Linke Seite umdrehen (Normalen bleiben nach +Z).
    nq = len(faces) // 2
    faces = [tuple(reversed(f)) for f in faces[nq:]] + faces[:nq]
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
    tilt = math.radians(24.0)
    P0 = (0.0, 0.0, 0.005)
    P1 = (0.0, 0.14, 0.95)           # Blattachse bis zur Spreitenbasis
    C = (0.0, 0.03, 0.50)
    stem_v, stem_f, stem_uv = build_tube(P0, C, P1, 0.058, 0.046, flare=2.0)

    bv, bf, buv = build_blade()
    ct, st = math.cos(tilt), math.sin(tilt)

    def place(p):
        x, y, z = p
        return (x, y * ct - z * st, y * st + z * ct + 0.95)

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
