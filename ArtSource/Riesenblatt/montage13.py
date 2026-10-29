# Vorher/Nachher-Montage fuer MODELL13 (links alt, rechts neu; identische
# Blender-Beleuchtung: alt_*.png aus Riesenblatt_2_alt.fbx.bak, rb2_*.png Neubau)
from PIL import Image
import os
D = 'Logs/modell/Riesenblatt2'
H = 360


def tile(p):
    im = Image.open(p).convert('RGB')
    return im.resize((int(im.width * H / im.height), H))


rows = []
for view in ('vorne', 'oben'):
    a = tile(os.path.join(D, 'alt_%s.png' % view))
    b = tile(os.path.join(D, 'rb2_%s.png' % view))
    row = Image.new('RGB', (a.width + b.width + 8, H), (16, 16, 16))
    row.paste(a, (0, 0))
    row.paste(b, (a.width + 8, 0))
    rows.append(row)
w = max(r.width for r in rows)
out = Image.new('RGB', (w, sum(r.height for r in rows) + 8), (16, 16, 16))
y = 0
for r in rows:
    out.paste(r, (0, y))
    y += r.height + 8
out.save(os.path.join(D, 'vorher_nachher.png'))
print('montage ok', out.size)
