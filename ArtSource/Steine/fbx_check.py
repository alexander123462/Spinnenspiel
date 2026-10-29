import bpy, sys
bpy.ops.wm.read_factory_settings(use_empty=True)
for path in sys.argv[sys.argv.index('--') + 1:]:
    bpy.ops.import_scene.fbx(filepath=path)
    for o in bpy.context.scene.objects:
        if o.type == 'MESH':
            me = o.data
            xs=[v.co.x for v in me.vertices]; ys=[v.co.y for v in me.vertices]; zs=[v.co.z for v in me.vertices]
            cols = [c for c in me.color_attributes]
            ncol = len(cols[0].data) if cols else 0
            uv = [u.name for u in me.uv_layers]
            print('CHK', path.split('/')[-1], o.name, 'tris', len(me.polygons),
                  'slots', len(me.materials),
                  'X %.3f..%.3f Y %.3f..%.3f Z %.3f..%.3f' % (min(xs),max(xs),min(ys),max(ys),min(zs),max(zs)),
                  'colattrs', [c.name for c in cols], 'colcount', ncol, 'uv', uv)
    bpy.ops.wm.read_factory_settings(use_empty=True)
