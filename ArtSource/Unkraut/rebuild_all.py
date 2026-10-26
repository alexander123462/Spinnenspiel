# Rebuild + Material + Export in einem Schritt (nach Z-Absenkung).
import bpy, os
src = 'C:/Users/vorau/dev/Spinnenspiel/ArtSource/Unkraut/build_step.py'
ns = {}
exec(open(src, encoding='utf-8').read(), ns)
ob = bpy.data.objects['Unkraut']
mat = bpy.data.materials.get('Unkraut_Blatt')
if mat is None:
    mat = bpy.data.materials.new('Unkraut_Blatt')
    mat.use_nodes = True
    nt = mat.node_tree
    nt.nodes.clear()
    out = nt.nodes.new('ShaderNodeOutputMaterial'); out.location = (300, 0)
    b = nt.nodes.new('ShaderNodeBsdfPrincipled'); b.location = (0, 0)
    nt.links.new(b.outputs['BSDF'], out.inputs['Surface'])
    ta = nt.nodes.new('ShaderNodeTexImage'); ta.location = (-500, 200)
    ta.image = bpy.data.images.load('C:/Users/vorau/dev/Spinnenspiel/Assets/_Project/Art/Plants/Unkraut/Textures/Unkraut_Albedo.png')
    nt.links.new(ta.outputs['Color'], b.inputs['Base Color'])
    tn = nt.nodes.new('ShaderNodeTexImage'); tn.location = (-500, -250)
    tn.image = bpy.data.images.load('C:/Users/vorau/dev/Spinnenspiel/Assets/_Project/Art/Plants/Unkraut/Textures/Unkraut_Normal.png')
    nm = nt.nodes.new('ShaderNodeNormalMap'); nm.location = (-200, -250)
    nt.links.new(tn.outputs['Color'], nm.inputs['Color'])
    nt.links.new(nm.outputs['Normal'], b.inputs['Normal'])
    b.inputs['Roughness'].default_value = 0.6
ob.data.materials.clear()
ob.data.materials.append(bpy.data.materials.get('Unkraut_Blatt'))
ns['export_fbx']()
bpy.ops.wm.save_as_mainfile(filepath='C:/Users/vorau/dev/Spinnenspiel/ArtSource/Unkraut/unkraut.blend')
print('bytes', os.path.getsize('C:/Users/vorau/dev/Spinnenspiel/ArtSource/Unkraut/unkraut_neu.fbx'))
