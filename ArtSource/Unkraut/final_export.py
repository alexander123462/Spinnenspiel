# Finaler FBX-Export + Blend-Speichern (MODELL3 Unkraut).
import bpy, os
ob = bpy.data.objects['Unkraut']
me = ob.data
tris = sum(len(p.vertices) - 2 for p in me.polygons)
xs = [v.co for v in me.vertices]
print('Z %.3f..%.3f X %.3f Y %.3f Tris %d Slots %s' % (
    min(c.z for c in xs), max(c.z for c in xs),
    max(c.x for c in xs) - min(c.x for c in xs),
    max(c.y for c in xs) - min(c.y for c in xs), tris,
    [m.name if m is not None else 'NONE' for m in me.materials]))
out = 'C:/Users/vorau/dev/Spinnenspiel/ArtSource/Unkraut/unkraut_neu.fbx'
bpy.ops.export_scene.fbx(filepath=out,
    apply_scale_options='FBX_SCALE_ALL', axis_forward='-Z', axis_up='Y',
    bake_space_transform=True, mesh_smooth_type='FACE',
    path_mode='STRIP', use_mesh_modifiers=True)
print('fbx bytes', os.path.getsize(out))
bpy.ops.wm.save_as_mainfile(filepath='C:/Users/vorau/dev/Spinnenspiel/ArtSource/Unkraut/unkraut.blend')
