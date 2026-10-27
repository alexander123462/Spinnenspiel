# -*- coding: utf-8 -*-
# MODELL8: weist den beiden Materialien Albedo + Normalmap zu (Viewport-Kontrolle).
import bpy

ALB = r'C:\Users\vorau\dev\Spinnenspiel\ArtSource\Busch1\Busch1_Albedo.png'
NRM = r'C:\Users\vorau\dev\Spinnenspiel\ArtSource\Busch1\Busch1_Normal.png'


def setup(mat_name, rough):
    mat = bpy.data.materials[mat_name]
    mat.use_nodes = True
    nt = mat.node_tree
    nt.nodes.clear()
    out = nt.nodes.new('ShaderNodeOutputMaterial')
    bsdf = nt.nodes.new('ShaderNodeBsdfPrincipled')
    bsdf.inputs['Roughness'].default_value = rough
    ta = nt.nodes.new('ShaderNodeTexImage')
    ta.image = bpy.data.images.load(ALB, check_existing=True)
    tn = nt.nodes.new('ShaderNodeTexImage')
    tn.image = bpy.data.images.load(NRM, check_existing=True)
    nm = nt.nodes.new('ShaderNodeNormalMap')
    nt.links.new(ta.outputs['Color'], bsdf.inputs['Base Color'])
    nt.links.new(tn.outputs['Color'], nm.inputs['Color'])
    nt.links.new(nm.outputs['Normal'], bsdf.inputs['Normal'])
    nt.links.new(bsdf.outputs['BSDF'], out.inputs['Surface'])
    ta.image.colorspace_settings.name = 'sRGB'
    tn.image.colorspace_settings.name = 'Non-Color'


if __name__ == '__main__' or True:
    setup('MAT_Busch1_Aeste', 0.85)
    setup('MAT_Busch1_Blaetter', 0.60)
    print('Materialien gesetzt')
