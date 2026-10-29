# Prueft Material/Texturen im offenen Blender und rendert Pruefbilder.
import bpy, math, mathutils
im = bpy.data.images.get('Unkraut_Albedo.png')
print('img', im.name, im.size[:], 'filepath', im.filepath)
px = list(im.pixels[0:8])
print('first px', [round(v, 3) for v in px])
ob = bpy.data.objects['Unkraut']
print('slots', [m.name if m is not None else 'NONE' for m in ob.data.materials])
sc = bpy.context.scene
cam = bpy.data.objects.get('TMP-cam')
cam.location = (0.30, 0.42, 0.20)
d = mathutils.Vector((0.05, 0.12, 0.14)) - cam.location
cam.rotation_euler = d.to_track_quat('-Z', 'Y').to_euler()
sc.render.filepath = 'C:/Users/vorau/dev/Spinnenspiel/Logs/modell/Unkraut/blender_nahaufnahme.png'
bpy.ops.render.render(write_still=True)
print('re-rendered closeup')
