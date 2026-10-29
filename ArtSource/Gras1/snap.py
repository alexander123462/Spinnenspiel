# Sichert Viewport-Screenshot aus Blender (per MCP ausgefuehrt).
import bpy, math, mathutils, os, sys

out = globals().get('SNAP_OUT', r'C:/Users/vorau/dev/Spinnenspiel/Logs/modell/Gras1/nachher/schritt.png')
view = globals().get('SNAP_VIEW', 'front')   # front|side|top

euler = {'front': (83, 0, 15), 'side': (83, 0, 90), 'top': (0, 0, 0)}[view]
for area in bpy.context.screen.areas:
    if area.type == 'VIEW_3D':
        sp = area.spaces[0]
        sp.shading.type = 'SOLID'
        r3d = sp.region_3d
        r3d.view_rotation = mathutils.Euler(
            tuple(math.radians(a) for a in euler), 'XYZ').to_quaternion()
        r3d.view_location = (0, 0, 0.25)
        r3d.view_distance = 1.0
        region = [r for r in area.regions if r.type == 'WINDOW'][0]
        bpy.context.scene.render.filepath = out
        with bpy.context.temp_override(area=area, region=region):
            bpy.ops.render.opengl(write_still=True, view_context=True)
print('Screenshot:', out, os.path.exists(out))
