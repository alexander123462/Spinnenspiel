import os, glob, sys
from PIL import Image, ImageDraw
d = 'Logs/ci/galerie'
out = os.path.join(d, 'sheets')
os.makedirs(out, exist_ok=True)
names = sorted({os.path.basename(f).rsplit('_', 1)[0] for f in glob.glob(d + '/*_*.png')})
for nm in names:
    sh = Image.new('RGB', (1024, 768), (30, 30, 30))
    for i, v in enumerate(['vorne', 'seite', 'oben', 'spiel']):
        f = '%s/%s_%s.png' % (d, nm, v)
        if os.path.exists(f):
            im = Image.open(f).convert('RGB').resize((512, 384))
            ImageDraw.Draw(im).text((8, 6), '%s %s' % (nm, v), fill=(255, 255, 0))
            sh.paste(im, ((i % 2) * 512, (i // 2) * 384))
    sh.save('%s/%s.png' % (out, nm))
print(len(names), 'Sheets')
