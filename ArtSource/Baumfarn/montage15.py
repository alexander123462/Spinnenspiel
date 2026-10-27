# -*- coding: utf-8 -*-
"""Vorher/Nachher-Montage fuer Farn_Baum: zwei Zeilen (vorne, seite), je links alt rechts neu."""
import sys
from PIL import Image, ImageDraw

VOR = r"C:/Users/vorau/dev/Spinnenspiel/Logs/modell/Farn_Baum/vorher"
NACH = r"C:/Users/vorau/dev/Spinnenspiel/Logs/ci/galerie"
OUT = r"C:/Users/vorau/dev/Spinnenspiel/Logs/modell/Farn_Baum/vorher_nachher.png"

def laden(p):
    im = Image.open(p).convert("RGB")
    im.thumbnail((1024, 768))
    return im

def zeile(vornamen, nachname):
    a = laden(vornamen)
    b = laden(nachname)
    h = max(a.height, b.height) + 34
    w = a.width + b.width + 20
    z = Image.new("RGB", (w, h), (16, 16, 16))
    z.paste(a, (0, 34))
    z.paste(b, (a.width + 20, 34))
    d = ImageDraw.Draw(z)
    d.text((6, 8), "VORHER (alt, Platzhalter)", fill=(255, 255, 0))
    d.text((a.width + 26, 8), "NACHHER (neu, Baumfarn)", fill=(0, 255, 128))
    return z

r1 = zeile(VOR + r"\Farn_Baum_vorne.png", NACH + r"\Farn_Baum_vorne.png")
r2 = zeile(VOR + r"\Farn_Baum_seite.png", NACH + r"\Farn_Baum_seite.png")
W = max(r1.width, r2.width)
H = r1.height + r2.height + 10
m = Image.new("RGB", (W, H), (16, 16, 16))
m.paste(r1, (0, 0))
m.paste(r2, (0, r1.height + 10))
m.save(OUT)
print("Montage:", OUT, m.size)
