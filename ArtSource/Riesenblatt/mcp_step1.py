import math
from mathutils import Euler
ob = make_object()
mat = bpy.data.materials.get('Preview') or bpy.data.materials.new('Preview')
mat.use_nodes = True
b = mat.node_tree.nodes['Principled BSDF']
b.inputs['Base Color'].default_value = (0.18, 0.32, 0.14, 1)
b.inputs['Roughness'].default_value = 0.6
ob.data.materials.clear()
ob.data.materials.append(mat)
for a in bpy.context.screen.areas:
    if a.type == 'VIEW_3D':
        sp = a.spaces[0]
        sp.shading.type = 'MATERIAL'
        r3d = sp.region_3d
        r3d.view_location = (0, 0.2, 0.9)
        r3d.view_distance = 4.2
        r3d.view_rotation = Euler((math.radians(75), 0, math.radians(200))).to_quaternion()
print('obj:', ob.name, 'polys:', len(ob.data.polygons))
