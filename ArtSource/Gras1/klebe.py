# Nebenaeinander-Bild fuer den bild_fragen-Vergleich (Vorher | Nachher).
from PIL import Image
import sys
base = r"C:/Users/vorau/dev/Spinnenspiel/Logs/modell/Gras1/"
a = Image.open(base + "vorher/Gras1_vorne.png")
b = Image.open(base + "nachher/Gras1_vorne.png")
h = 512
def rs(im):
    return im.resize((int(im.width * h / im.height), h))
a, b = rs(a), rs(b)
out = Image.new("RGB", (a.width + b.width + 8, h), "white")
out.paste(a, (0, 0)); out.paste(b, (a.width + 8, 0))
out.save(base + "vorher_nachher.png")
print("ok", out.size)
