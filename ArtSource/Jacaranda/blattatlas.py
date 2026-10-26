"""Erzeugt den Blatt-Atlas fuer den neuen Jacaranda (2x2 Kacheln a 512 px, RGBA 1024x1024).
Kachel (u,v) im Bild: 0 = Wedelbuendel gruen A, 1 = gruen B, 2 = Bluetenrispe lila, 3 = gruen mit einzelnen Bluetenbuescheln.
Alpha: Fiederblaettchen (doppelt gefiedert) und Blueten; RGB unter Transparenz = mittleres Gruen (keine dunklen Raender).
Aufruf: python blattatlas.py <Ausgabeordner>   (schreibt jacaranda_tree_leaves_Albedo/Normal/Mask.png)
"""
import sys, math, os
import numpy as np
from PIL import Image

rng = np.random.default_rng(11)
T = 512  # Kachelgroesse
SS = 2   # Supersampling
N = T * SS


def hsv(h, s, v):
    import colorsys
    return np.array(colorsys.hsv_to_rgb(h / 360.0, s, v))


def ellipse(rgb, a, cx, cy, ax, ay, ang, col, soft=1.0):
    """Gefuellte Ellipse (Halbachsen ax, ay in Pixeln, Drehwinkel ang) mit weichem Rand in das Feld rgb/a zeichnen."""
    r = int(max(ax, ay)) + 3
    x0, x1 = max(0, int(cx) - r), min(N, int(cx) + r + 1)
    y0, y1 = max(0, int(cy) - r), min(N, int(cy) + r + 1)
    if x0 >= x1 or y0 >= y1:
        return
    ys, xs = np.mgrid[y0:y1, x0:x1]
    dx, dy = xs - cx, ys - cy
    c, s = math.cos(ang), math.sin(ang)
    u = (dx * c + dy * s) / ax
    v = (-dx * s + dy * c) / ay
    d = np.sqrt(u * u + v * v)
    m = np.clip((1.0 - d) * soft * 3.0, 0, 1)
    # Schattierung: dunkler zum Rand, Mittelrippe heller
    shade = 0.75 + 0.35 * (1.0 - np.clip(d, 0, 1)) + 0.12 * np.exp(-(v * 6.0) ** 2)
    sel = m > a[y0:y1, x0:x1]
    col_img = np.clip(col[None, None, :] * shade[..., None], 0, 1)
    sub = rgb[y0:y1, x0:x1]
    sub[sel] = col_img[sel]
    a[y0:y1, x0:x1] = np.maximum(a[y0:y1, x0:x1], m)


def line(rgb, a, p0, p1, w, col):
    n = int(max(abs(p1[0] - p0[0]), abs(p1[1] - p0[1]))) + 1
    for i in range(n + 1):
        t = i / max(1, n)
        x, y = p0[0] + (p1[0] - p0[0]) * t, p0[1] + (p1[1] - p0[1]) * t
        ellipse(rgb, a, x, y, w, w, 0, col)


def frond(rgb, a, base, ang, length, pinnae, leaflets, leaf_len, tint):
    """Doppelt gefiederter Wedel: Hauptspindel, Fiedern paarweise, je Fieder viele Fiederblaettchen paarweise."""
    bx, by = base
    ex, ey = bx + math.cos(ang) * length, by + math.sin(ang) * length
    stem_col = hsv(75, 0.45, 0.30)
    line(rgb, a, (bx, by), (ex, ey), 1.6 * SS, stem_col)
    for i in range(pinnae):
        t = 0.12 + 0.88 * i / max(1, pinnae - 1)
        px, py = bx + (ex - bx) * t, by + (ey - by) * t
        plen = length * 0.30 * math.sin(math.pi * min(1.0, t * 0.9 + 0.1)) ** 0.7 + length * 0.04
        for side in (-1, 1):
            pang = ang + side * (1.15 - 0.35 * t)
            qx, qy = px + math.cos(pang) * plen, py + math.sin(pang) * plen
            line(rgb, a, (px, py), (qx, qy), 0.9 * SS, stem_col)
            for j in range(leaflets):
                u = 0.1 + 0.9 * j / max(1, leaflets - 1)
                lx, ly = px + (qx - px) * u, py + (qy - py) * u
                ll = leaf_len * (1.0 - 0.45 * u) * rng.uniform(0.85, 1.15)
                for s2 in (-1, 1):
                    la = pang + s2 * (1.2 - 0.25 * u) + rng.normal(0, 0.08)
                    cx, cy = lx + math.cos(la) * ll * 0.5, ly + math.sin(la) * ll * 0.5
                    hue = tint + rng.normal(0, 4)
                    col = hsv(hue, rng.uniform(0.45, 0.62), rng.uniform(0.30, 0.46))
                    ellipse(rgb, a, cx, cy, ll * 0.52, ll * 0.20, la, col)


def blossom(rgb, a, cx, cy, size):
    """Trichterbluete von oben: 5 Zipfel, hellerer Schlund."""
    col = hsv(rng.uniform(268, 288), rng.uniform(0.38, 0.55), rng.uniform(0.62, 0.80))
    for k in range(5):
        ang = k * 2 * math.pi / 5 + rng.uniform(-0.2, 0.2)
        ellipse(rgb, a, cx + math.cos(ang) * size * 0.45, cy + math.sin(ang) * size * 0.45, size * 0.55, size * 0.38, ang, col)
    ellipse(rgb, a, cx, cy, size * 0.32, size * 0.32, 0, hsv(285, 0.12, 0.92))


def panicle(rgb, a, base, ang, length, count, size):
    bx, by = base
    ex, ey = bx + math.cos(ang) * length, by + math.sin(ang) * length
    line(rgb, a, (bx, by), (ex, ey), 1.4 * SS, hsv(80, 0.4, 0.30))
    for i in range(count):
        t = rng.uniform(0.05, 1.0)
        spread = (1.0 - t) * length * 0.32 + size * 0.5
        off = rng.normal(0, spread * 0.5)
        x = bx + (ex - bx) * t + math.cos(ang + math.pi / 2) * off
        y = by + (ey - by) * t + math.sin(ang + math.pi / 2) * off
        blossom(rgb, a, x, y, size * rng.uniform(0.75, 1.25))


def tile(kind):
    rgb = np.ones((N, N, 3)) * hsv(105, 0.5, 0.32)
    a = np.zeros((N, N))
    base = (N * 0.5, N * 0.94)
    if kind in (0, 1):
        for k, da in enumerate((-0.55, 0.0, 0.5)):
            frond(rgb, a, (base[0] + (k - 1) * N * 0.06, base[1]), -math.pi / 2 + da + rng.normal(0, 0.05),
                  N * rng.uniform(0.62, 0.78), 9, 11, N * 0.052, 108 if kind == 0 else 118)
    elif kind == 2:
        for k, da in enumerate((-0.5, 0.0, 0.45)):
            panicle(rgb, a, (base[0] + (k - 1) * N * 0.05, base[1]), -math.pi / 2 + da, N * rng.uniform(0.55, 0.72), 42, N * 0.050)
        frond(rgb, a, (base[0], base[1]), -math.pi / 2 + 0.9, N * 0.5, 7, 9, N * 0.045, 112)
    else:
        frond(rgb, a, (base[0] - N * 0.05, base[1]), -math.pi / 2 - 0.35, N * 0.7, 9, 11, N * 0.05, 112)
        frond(rgb, a, (base[0] + N * 0.05, base[1]), -math.pi / 2 + 0.35, N * 0.7, 9, 11, N * 0.05, 104)
        panicle(rgb, a, base, -math.pi / 2 + 0.05, N * 0.5, 22, N * 0.045)
    # Downsample
    rgb = rgb.reshape(T, SS, T, SS, 3).mean(axis=(1, 3))
    a = a.reshape(T, SS, T, SS).mean(axis=(1, 3))
    return rgb, a


def main(out):
    os.makedirs(out, exist_ok=True)
    atlas = np.zeros((2 * T, 2 * T, 4))
    for k in range(4):
        rgb, a = tile(k)
        r, c = divmod(k, 2)
        # Bild-Zeile 0 = oben. Kachel 0/1 oben, 2/3 unten.
        atlas[r * T:(r + 1) * T, c * T:(c + 1) * T, :3] = rgb
        atlas[r * T:(r + 1) * T, c * T:(c + 1) * T, 3] = (a > 0.35).astype(float) * np.clip(a * 1.4, 0, 1)
    # Alpha schaerfen fuer Cutout 0.5 und Rand-Farbe fuellen
    alpha = (atlas[..., 3] > 0.5).astype(float) * 255
    albedo = np.dstack([np.clip(atlas[..., :3], 0, 1) * 255, alpha]).astype(np.uint8)
    Image.fromarray(albedo, 'RGBA').resize((1024, 1024), Image.LANCZOS).save(os.path.join(out, 'jacaranda_tree_leaves_Albedo.png'))
    normal = np.zeros((1024, 1024, 4), np.uint8); normal[..., 0] = 128; normal[..., 1] = 128; normal[..., 2] = 255; normal[..., 3] = 255
    Image.fromarray(normal, 'RGBA').save(os.path.join(out, 'jacaranda_tree_leaves_Normal.png'))
    mask = np.zeros((1024, 1024, 4), np.uint8); mask[..., 0] = 0; mask[..., 1] = 255; mask[..., 2] = 0; mask[..., 3] = 90
    Image.fromarray(mask, 'RGBA').save(os.path.join(out, 'jacaranda_tree_leaves_Mask.png'))
    print('Atlas geschrieben:', out)


if __name__ == '__main__':
    main(sys.argv[1] if len(sys.argv) > 1 else 'out')
