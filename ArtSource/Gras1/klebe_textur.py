# TEXTUR1: Vorher/Nachher-Seitenbild fuer bild_fragen.py (analog klebe.py).
from PIL import Image
import sys
base = r"C:/Users/vorau/dev/Spinnenspiel/Logs/modell/Gras1/"
a = Image.open(sys.argv[1] if len(sys.argv) > 1 else base + "spiel.png")
b = Image.open(sys.argv[2] if len(sys.argv) > 2 else base + "spiel_textur.png")
h = 720
def rs(im):
    return im.resize((int(im.width * h / im.height), h))
a, b = rs(a), rs(b)
out = Image.new("RGB", (a.width + b.width + 8, h), "white")
out.paste(a, (0, 0)); out.paste(b, (a.width + 8, 0))
out.save(base + "spiel_textur_vorher_nachher.png")
print("ok", out.size)
