import math
import bpy, sys, mathutils

bpy.ops.wm.read_factory_settings(use_empty=True)
for path in sys.argv[sys.argv.index('--') + 1:]:
    bpy.ops.import_scene.fbx(filepath=path)
    for o in bpy.context.scene.objects:
        if o.type == 'MESH':
            cs = [o.matrix_world @ mathutils.Vector(c) for c in o.bound_box]
            mn = mathutils.Vector((min(c.x for c in cs), min(c.y for c in cs), min(c.z for c in cs)))
            mx = mathutils.Vector((max(c.x for c in cs), max(c.y for c in cs), max(c.z for c in cs)))
            print('REIMPORT', path.split('/')[-1], o.name,
                  'X %.3f..%.3f Y %.3f..%.3f Z %.3f..%.3f' % (mn.x, mx.x, mn.y, mx.y, mn.z, mx.z),
                  'rot', tuple(round(math.degrees(a), 1) for a in o.rotation_euler))
    bpy.ops.wm.read_factory_settings(use_empty=True)
