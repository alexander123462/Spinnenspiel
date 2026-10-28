# MODELL19 Schritt A: UV-Atlas fuer Koerper/Kopf/Beine, Face-Farben (Fallenstaub,
# Segmentbaender), dann Backen in 512px-Albedo. Ausfuehren per Blender-MCP exec.
import builtins
import math
import numpy as np

ns = getattr(builtins, "fliege_ns", None)
if ns is None:
    raise RuntimeError("fliege_ns fehlt: erst _tmp_fliege_rebuild.py ausfuehren")

KOERPER_OBJS = ["Koerper", "Kopf", "Beine"]


def uv_atlas():
    """Koerper+Kopf+Beine gemeinsam smart-unwrappen -> gemeinsamer 0..1-Atlas."""
    import bpy
    bpy.ops.object.select_all(action='DESELECT')
    for n in KOERPER_OBJS:
        bpy.data.objects[n].select_set(True)
    bpy.context.view_layer.objects.active = bpy.data.objects["Koerper"]
    bpy.ops.object.mode_set(mode='EDIT')
    bpy.ops.mesh.select_all(action='SELECT')
    bpy.ops.uv.smart_project(angle_limit=math.radians(66), island_margin=0.02)
    bpy.ops.object.mode_set(mode='OBJECT')


def face_colors():
    """Face-Farb-Attribut 'col': dunkler Fallfliegen-Koerper, heller behaarter
    Thorax, dunkle Segmentfurchen, heller Fallenstaub-Sprengrand."""
    import bpy
    rng = np.random.default_rng(19)
    for n in KOERPER_OBJS:
        ob = bpy.data.objects[n]
        me = ob.data
        # Blender 5.1: color_attributes-Proxy zeigt neu angelegte Attribute nicht;
        # direkt ueber me.attributes arbeiten.
        names = [a.name for a in me.attributes]
        for junk in [a for a in me.attributes if a.name.startswith("col.")]:
            me.attributes.remove(junk)
        if "col" not in [a.name for a in me.attributes]:
            me.attributes.new("col", 'BYTE_COLOR', 'FACE')
        attr = me.attributes["col"]
        for p in me.polygons:
            y = p.center.y
            if n == "Kopf" and p.material_index == 1:
                rgb = (0.16, 0.05, 0.04)          # Auge (wird eh eigenes MAT)
            elif n == "Koerper" and y < -0.0010:
                rgb = (0.17, 0.16, 0.15)          # Thorax: heller, 'behaart'
            else:
                rgb = [0.085, 0.080, 0.075]       # Hinterleib dunkel
                for fy in (0.0052, 0.0132, 0.0212):
                    if abs(y - fy) < 0.0016:
                        rgb = [0.045, 0.043, 0.041]  # Segmentfurche
            if rng.random() < 0.14:               # Staub-Sprenkel
                rgb = [min(1.0, c + 0.07) for c in rgb]
            attr.data[p.index].color = (rgb[0], rgb[1], rgb[2], 1.0)


def body_paint_nodes():
    """Fliege_Koerper: Attribut 'col' als Basisfarbe (wird zurueckgebacken)."""
    import bpy
    m = bpy.data.materials["Fliege_Koerper"]
    nt = m.node_tree
    nt.nodes.clear()
    out = nt.nodes.new('ShaderNodeOutputMaterial')
    bsdf = nt.nodes.new('ShaderNodeBsdfPrincipled')
    attr = nt.nodes.new('ShaderNodeAttribute')
    attr.attribute_name = "col"
    nt.links.new(attr.outputs['Color'], bsdf.inputs['Base Color'])
    bsdf.inputs['Roughness'].default_value = 0.6
    nt.links.new(bsdf.outputs['BSDF'], out.inputs['Surface'])


def bake_albedo():
    """DIFFUSE/Farbe backen (Cycles, CPU) auf 512px; Bild 'FliegeBodyAlbedo'."""
    import bpy
    sc = bpy.context.scene
    sc.render.engine = 'CYCLES'
    sc.cycles.device = 'CPU'
    sc.cycles.samples = 16
    img = bpy.data.images.get("FliegeBodyAlbedo")
    if img is not None:
        bpy.data.images.remove(img)
    img = bpy.data.images.new("FliegeBodyAlbedo", 512, 512, alpha=False)
    m = bpy.data.materials["Fliege_Koerper"]
    node = m.node_tree.nodes.new('ShaderNodeTexImage')
    node.image = img
    m.node_tree.nodes.active = node
    bpy.ops.object.select_all(action='DESELECT')
    for n in KOERPER_OBJS:
        bpy.data.objects[n].select_set(True)
    bpy.context.view_layer.objects.active = bpy.data.objects["Koerper"]
    bpy.ops.object.bake(type='DIFFUSE', pass_filter={'COLOR'}, margin=6)
    return img


def save_and_export(img):
    """Albedo als PNG sichern, eingebrannte Textur an MAT, .blend + FBX exportieren."""
    import bpy
    png = r"C:\Users\vorau\dev\Spinnenspiel\Assets\_Project\Art\Insekten\Fliege_albedo.png"
    img.filepath_raw = png
    img.file_format = 'PNG'
    img.save()
    # Fuer den Export: Basisfarbe = eingebranntes Bild (kein Attribut mehr).
    m = bpy.data.materials["Fliege_Koerper"]
    nt = m.node_tree
    attr = next(n for n in nt.nodes if n.type == 'ATTRIBUTE')
    tex = nt.nodes.active
    nt.links.new(tex.outputs['Color'], nt.nodes['Principled BSDF'].inputs['Base Color'])
    nt.nodes.remove(attr)
    blend = r"C:\Users\vorau\dev\Spinnenspiel\ArtSource\Insekten\Fliege.blend"
    bpy.ops.wm.save_as_mainfile(filepath=blend)
    fbx = r"C:\Users\vorau\dev\Spinnenspiel\Assets\_Project\Art\Insekten\Fliege.fbx"
    bpy.ops.object.select_all(action='DESELECT')
    for n in KOERPER_OBJS + ["Wing_L", "Wing_R"]:
        bpy.data.objects[n].select_set(True)
    bpy.ops.export_scene.fbx(filepath=fbx, use_selection=True,
                             apply_scale_options="FBX_SCALE_ALL",
                             axis_forward="-Z", axis_up="Y", bake_space_transform=True,
                             mesh_smooth_type="FACE", path_mode="STRIP",
                             use_mesh_modifiers=True, object_types={"MESH"})
    print("EXPORT ok:", png, fbx)
