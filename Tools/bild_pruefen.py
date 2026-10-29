#!/usr/bin/env python
"""Zahlen-Pruefung fuer Modell-Bilder (Alex 2026-10-28): erkennt schwarze, blasse und leere Bilder,
die die Selbstkritik per Sprachmodell nicht zuverlaessig sieht.

Aufruf:  python Tools/bild_pruefen.py <bild.png> [weitere.png ...] [--spiel]
  Standard (Galerie-/Studio-Bild, Objekt vor einfarbigem Hintergrund): prueft nur die Objektpixel.
  --spiel  (Player-Foto, ganzes Bild ist Szene): prueft das ganze Bild.
Ausgabe je Bild: OK oder FEHLER mit Grund. Exit 0 = alle OK, 1 = mindestens ein Fehler, 2 = Datei fehlt.
Schwellen sind absichtlich grob: nur offensichtliche Fehler melden, nie Geschmack."""
import sys
import numpy as np
from PIL import Image


def pruefe(pfad, spiel):
    img = np.asarray(Image.open(pfad).convert("RGB")).astype(np.float32)
    h, w, _ = img.shape
    lum = 0.2126 * img[..., 0] + 0.7152 * img[..., 1] + 0.0722 * img[..., 2]
    mx, mn = img.max(axis=2), img.min(axis=2)
    sat = np.where(mx > 1, (mx - mn) / np.maximum(mx, 1), 0)

    if spiel:
        maske = np.ones((h, w), bool)
    else:
        rand = np.concatenate([img[:8].reshape(-1, 3), img[-8:].reshape(-1, 3),
                               img[:, :8].reshape(-1, 3), img[:, -8:].reshape(-1, 3)])
        hg = np.median(rand, axis=0)
        maske = np.abs(img - hg).max(axis=2) > 14
        if maske.mean() < 0.01:
            return ["Objekt nicht zu sehen (weniger als 1 % Bildflaeche weicht vom Hintergrund ab)"], {}

    l, s = lum[maske], sat[maske]
    werte = {
        "Objektanteil": float(maske.mean()),
        "Helligkeit_mittel": float(l.mean()),
        "Schwarzanteil": float((l < 25).mean()),
        "Weissanteil": float((l > 235).mean()),
        "Saettigung_mittel": float(s.mean()),
        "Kontrast_std": float(l.std()),
    }
    fehler = []
    if werte["Helligkeit_mittel"] < 45 or werte["Schwarzanteil"] > (0.25 if spiel else 0.55):
        fehler.append("zu dunkel/schwarz (Helligkeit %.0f, Schwarzanteil %.0f %%) - Material, Normalmap oder Licht pruefen"
                      % (werte["Helligkeit_mittel"], 100 * werte["Schwarzanteil"]))
    if werte["Weissanteil"] > (0.20 if spiel else 0.60):
        fehler.append("ueberbelichtet (Weissanteil %.0f %%)" % (100 * werte["Weissanteil"]))
    if not spiel and werte["Saettigung_mittel"] < 0.06 and werte["Kontrast_std"] < 20:
        fehler.append("blass/grau ohne Zeichnung (Saettigung %.2f, Kontrast %.0f) - Bild nicht beurteilbar, Beleuchtung pruefen"
                      % (werte["Saettigung_mittel"], werte["Kontrast_std"]))
    if spiel and werte["Kontrast_std"] < 15:
        fehler.append("fast kontrastlos (Kontrast %.0f)" % werte["Kontrast_std"])
    return fehler, werte


def main():
    args = [a for a in sys.argv[1:] if not a.startswith("--")]
    spiel = "--spiel" in sys.argv
    if not args:
        print(__doc__)
        return 2
    code = 0
    for p in args:
        try:
            fehler, werte = pruefe(p, spiel)
        except FileNotFoundError:
            print("FEHLT  %s" % p)
            code = max(code, 2)
            continue
        info = " ".join("%s=%.2f" % (k, v) for k, v in werte.items())
        if fehler:
            print("FEHLER %s: %s | %s" % (p, "; ".join(fehler), info))
            code = max(code, 1)
        else:
            print("OK     %s | %s" % (p, info))
    return code


if __name__ == "__main__":
    sys.exit(main())
