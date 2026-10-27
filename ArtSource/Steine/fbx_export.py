import bpy, os
bpy.ops.wm.open_mainfile(filepath='C:/Users/vorau/dev/Spinnenspiel/ArtSource/Steine/steine.blend')
dirs = ['C:/Users/vorau/dev/Spinnenspiel/ArtSource/Steine',
        'C:/Users/vorau/dev/Spinnenspiel/Assets/_Project/Art/Plants/Steine']
for nm in ['Stein_1','Stein_2','Stein_3','Stein_4','Stein_5']:
    ob = bpy.data.objects[nm]
    ob.location=(0,0,0); ob.rotation_euler=(0,0,0); ob.scale=(1,1,1)
    bpy.ops.object.select_all(action='DESELECT')
    ob.select_set(True); bpy.context.view_layer.objects.active = ob
    for d in dirs:
        bpy.ops.export_scene.fbx(filepath=os.path.join(d, nm + '.fbx'),
            use_selection=True, apply_scale_options='FBX_SCALE_ALL',
            axis_forward='-Z', axis_up='Y', bake_space_transform=True,
            mesh_smooth_type='FACE', path_mode='STRIP', use_mesh_modifiers=True)
    print('reexported', nm)
