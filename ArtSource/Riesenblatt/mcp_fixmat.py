import bpy, numpy as np
ob = bpy.data.objects['Riesenblatt1']
me = ob.data
before = np.fromiter((p.material_index for p in me.polygons), np.int32)
print('vorher:', dict(zip(*np.unique(before, return_counts=True))))
idx = list(ob.get('rb_mats'))
arr = np.asarray(idx, dtype=np.int32)
me.polygons.foreach_set('material_index', arr)
me.update()
after = np.fromiter((p.material_index for p in me.polygons), np.int32)
print('nachher:', dict(zip(*np.unique(after, return_counts=True))))
