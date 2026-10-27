import bpy, numpy as np
exec(open(r'C:\Users\vorau\dev\Spinnenspiel\ArtSource\Riesenblatt\mcp_load.py', encoding='utf-8').read(), globals())
ob = make_object()
for im in list(bpy.data.images):
    if 'Riesenblatt' in im.name:
        im.reload()
make_materials(ob)
me = ob.data
print('polys', len(me.polygons),
      'slots', [m.name for m in me.materials],
      'idx', sorted({p.material_index for p in me.polygons}))
