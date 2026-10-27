# MODELL17: UV-Atlas (4 Quadranten) + Bake (mossy_rock_diff x Vertexfarbe Col)
import bpy, os, math
ROOT = 'C:/Users/vorau/dev/Spinnenspiel'
DIFF = ROOT + '/Assets/_Project/Art/Scans/Textures/mossy_rock/mossy_rock_diff_2k.jpg'
OUT1 = ROOT + '/ArtSource/Steine/Textures/Steine_base.png'
OUT2 = ROOT + '/Assets/_Project/Art/Plants/Steine/Textures/Steine_base.png'
os.makedirs(os.path.dirname(OUT1), exist_ok=True)
os.makedirs(os.path.dirname(OUT2), exist_ok=True)

#---TEIL1: UVs in 4 Quadranten packen---
names = ['Stein_1','Stein_2','Stein_3','Stein_4','Stein_5']
for i, nm in enumerate(names):
    ob = bpy.data.objects[nm]
    me = ob.data
    uv = me.uv_layers.active.data
    u = (i % 3) * (1.0 / 3.0)
    v = (i // 3) * 0.5
    for d in uv:
        x, y = d.uv
        d.uv = (u + 0.015 + x * 0.30, v + 0.02 + y * 0.46)
    print('uv_packed', nm, 'quad', i)
#---TEIL2: Bake mossy_diff x Col in geteiltes Atlas---
img = bpy.data.images.new('Steine_base', 1024, 1024, alpha=False)
mat = bpy.data.materials['M_Stein']
nt = mat.node_tree
diff_ntex = nt.nodes.new('ShaderNodeTexImage'); diff_ntex.image = bpy.data.images.load(DIFF)
attr = nt.nodes.new('ShaderNodeAttribute'); attr.attribute_name = 'Col'
mix = nt.nodes.new('ShaderNodeMixRGB'); mix.blend_type = 'MULTIPLY'; mix.inputs['Fac'].default_value = 0.55
em = nt.nodes.new('ShaderNodeEmission')
out = next(n for n in nt.nodes if n.type == 'OUTPUT_MATERIAL')
nt.links.new(diff_ntex.outputs['Color'], mix.inputs['Color1'])
nt.links.new(attr.outputs['Color'], mix.inputs['Color2'])
nt.links.new(mix.outputs['Color'], em.inputs['Color'])
nt.links.new(em.outputs['Emission'], out.inputs['Surface'])
bake_node = nt.nodes.new('ShaderNodeTexImage'); bake_node.image = img
sc = bpy.context.scene
sc.render.engine = 'CYCLES'; sc.cycles.device = 'CPU'; sc.cycles.samples = 8
for i, nm in enumerate(names):
    ob = bpy.data.objects[nm]
    bpy.ops.object.select_all(action='DESELECT')
    ob.select_set(True); bpy.context.view_layer.objects.active = ob
    nt.nodes.active = bake_node; nt.update_tag()
    img.colorspace_settings.name = 'sRGB'
    bpy.ops.object.bake(type='EMIT', margin=6, use_clear=(i == 0))
    px = list(img.pixels[:])
    print('baked', nm, 'maxR', round(max(px[0::4]), 3))
img.colorspace_settings.name = 'sRGB'
img.filepath_raw = OUT1; img.file_format = 'PNG'; img.save()
img.filepath_raw = OUT2; img.save()
print('saved', OUT2)
#---TEIL3: FBX-Export mit Atlas-UVs---
dirs = [ROOT + '/ArtSource/Steine', ROOT + '/Assets/_Project/Art/Plants/Steine']
for nm in names:
    ob = bpy.data.objects[nm]
    ob.location = (0, 0, 0); ob.rotation_euler = (0, 0, 0); ob.scale = (1, 1, 1)
    bpy.ops.object.select_all(action='DESELECT')
    ob.select_set(True); bpy.context.view_layer.objects.active = ob
    for d in dirs:
        bpy.ops.export_scene.fbx(filepath=os.path.join(d, nm + '.fbx'),
            use_selection=True, apply_scale_options='FBX_SCALE_ALL',
            axis_forward='-Z', axis_up='Y', bake_space_transform=True,
            mesh_smooth_type='FACE', path_mode='STRIP', use_mesh_modifiers=True)
    print('reexported', nm)
