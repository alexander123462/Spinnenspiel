# MODELL6: Patch-Zusammenstellung fuer die Einzelwedel-Varianten (fern_wedel.fbx).
# Dieselbe Poisson-Disc-Zusammensetzung wie U4 (scatter.py): PATCH=8 m, MIN_D=0.42 m,
# Seed 17 -> identische Grundflaeche/Abstaende, nur dass jetzt je Punkt EINE Variante
# (wedel_1..3) gesetzt wird. Ausgabe: fern_patch.txt mit "x z variante" pro Zeile.
# Aufruf: python wedel_patch.py [seed]
import math
import os
import random
import sys

PATCH = 8.0          # Patch-Seitenlaenge in m (wie U4)
MIN_D = 0.42         # Poisson-Disc-Mindestabstand in m (wie U4)
VARIANTS = 3         # Anzahl Wedel-Varianten aus fern_wedel.fbx


def poisson(seed):
    """Deterministisches Poisson-Disc-Sampling (grid-basiert), wie in scatter.py."""
    rng = random.Random(seed)
    cell = MIN_D / math.sqrt(2.0)
    grid = {}
    pts = []

    def too_close(x, z):
        gx, gz = int(x // cell), int(z // cell)
        for dx in range(-2, 3):
            for dy in range(-2, 3):
                for (px, pz) in grid.get((gx + dx, gz + dy), ()):
                    if (px - x) ** 2 + (pz - z) ** 2 < MIN_D * MIN_D:
                        return True
        return False

    first = (rng.random() * PATCH, rng.random() * PATCH)
    pts.append(first)
    grid.setdefault((int(first[0] // cell), int(first[1] // cell)), []).append(first)
    active = [first]
    while active:
        i = rng.randrange(len(active))
        bx, bz = active[i]
        placed = False
        for _ in range(30):
            ang = rng.random() * 2 * math.pi
            r = MIN_D * (1.0 + rng.random())
            x, z = bx + r * math.cos(ang), bz + r * math.sin(ang)
            if not (0 <= x < PATCH and 0 <= z < PATCH):
                continue
            if too_close(x, z):
                continue
            pts.append((x, z))
            grid.setdefault((int(x // cell), int(z // cell)), []).append((x, z))
            active.append((x, z))
            placed = True
            break
        if not placed:
            active.pop(i)
    return pts


def main():
    seed = int(sys.argv[1]) if len(sys.argv) > 1 else 17
    pts = poisson(seed)
    rng = random.Random(seed * 7919 + 13)   # Variantenwahl, unabhaengiger Seed-Zweig
    out = os.path.join(os.path.dirname(os.path.abspath(__file__)), "fern_patch.txt")
    with open(out, "w", encoding="utf-8") as fh:
        fh.write("# MODELL6 Farn-Patch: x z variante (0..%d), Patch %.1f m, MIN_D %.2f m, seed %d\n"
                 % (VARIANTS - 1, PATCH, MIN_D, seed))
        for (x, z) in pts:
            fh.write("%.3f %.3f %d\n" % (x, z, rng.randrange(VARIANTS)))
    print("Punkte: %d  Patch: %.1f m  Datei: %s" % (len(pts), PATCH, out))


if __name__ == "__main__":
    import sys
    main()
