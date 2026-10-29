# Vorher/Nachher-Montage fuer MODELL14 (links alt, rechts neu; identische
# CI-Galerie-Kamera: vorher/ aus r112-f1 mit altem FBX, nachher/ aus r112-f3
# mit dem neuen Bananenblatt-FBX).
from PIL import Image
import os
D = 'Logs/modell/Riesenblatt3'
H = 420


def tile(p):
    im = Image.open(p).convert('RGB')
    return im.resize((int(im.width * H / im.height), H))


rows = []
for view in ('vorne', 'oben'):
    a = tile(os.path.join(D, 'vorher', 'Riesenblatt3_%s.png' % view))
    b = tile(os.path.join(D, 'nachher', 'Riesenblatt3_%s.png' % view))
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
