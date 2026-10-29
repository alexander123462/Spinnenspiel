import bpy, sys, mathutils
bpy.ops.wm.read_factory_settings(use_empty=True)
for path in sys.argv[sys.argv.index('--') + 1:]:
    bpy.ops.import_scene.fbx(filepath=path)
    for o in bpy.context.scene.objects:
        if o.type == 'MESH':
            xs = [v.co.x for v in o.data.vertices]
            ys = [v.co.y for v in o.data.vertices]
            zs = [v.co.z for v in o.data.vertices]
            print('LOKAL', path.split('/')[-1], o.name,
                  'X %.3f..%.3f Y %.3f..%.3f Z %.3f..%.3f' % (min(xs), max(xs), min(ys), max(ys), min(zs), max(zs)),
                  'rot', tuple(round(a, 2) for a in o.rotation_euler))
    bpy.ops.wm.read_factory_settings(use_empty=True)
