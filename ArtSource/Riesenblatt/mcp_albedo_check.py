# Beweisrender: Albedo direkt als Emission, ohne Licht/Normal.
import bpy, os
LOG = r'C:\Users\vorau\dev\Spinnenspiel\Logs\modell\Riesenblatt1'
scn = bpy.context.scene
for m in bpy.data.materials:
    if 'Riesenblatt' not in m.name:
        continue
    nt = m.node_tree
    out = next(n for n in nt.nodes if n.type == 'OUTPUT_MATERIAL')
    em = nt.nodes.new('ShaderNodeEmission'); em.location = (150, -300)
    ta = next(n for n in nt.nodes if n.type == 'TEX_IMAGE')
    nt.links.new(ta.outputs['Color'], em.inputs['Color'])
    nt.links.new(em.outputs[0], out.inputs['Surface'])
scn.render.engine = 'BLENDER_EEVEE'
scn.render.resolution_x = 900; scn.render.resolution_y = 900
scn.camera = bpy.data.objects.get('Cam') or scn.camera
old = bpy.context.scene.eevee.taa_render_samples
bpy.context.scene.eevee.taa_render_samples = 4
scn.render.filepath = os.path.join(LOG, 'rb1_albedo_proof.png')
bpy.ops.render.render(write_still=True)
print('render:', scn.render.filepath)
