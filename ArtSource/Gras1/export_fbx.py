# Export des finalen Gras1 nach FBX (per MCP ausgefuehrt).
import bpy, os

ob = bpy.data.objects.get('Gras1')
mat = bpy.data.materials.get('MAT_Gras1') or bpy.data.materials.new('MAT_Gras1')
ob.data.materials.clear()
ob.data.materials.append(mat)

# Ursprung am Fuss, Z nach oben ist schon so. Keine Duplikate.
for o in bpy.data.objects:
    if o.name.startswith('Gras1.00'):
        raise RuntimeError('Duplikat im Weg: ' + o.name)

bpy.ops.object.select_all(action='DESELECT')
ob.select_set(True)
bpy.context.view_layer.objects.active = ob

out = r'C:/Users/vorau/dev/Spinnenspiel/ArtSource/Gras1/gras1_neu.fbx'
bpy.ops.export_scene.fbx(filepath=out, use_selection=True,
                         apply_scale_options='FBX_SCALE_ALL',
                         axis_forward='-Z', axis_up='Y',
                         bake_space_transform=True,
                         object_types={'MESH'},
                         mesh_smooth_type='FACE', add_leaf_bones=False)
print('Export:', out, os.path.exists(out), os.path.getsize(out))
print('Slots:', [m.name for m in ob.data.materials])
