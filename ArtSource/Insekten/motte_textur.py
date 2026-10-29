# MODELL20 Schritt C: UV-Atlas + Face-Farben + Backen fuer die neue Motte.
# Koerper/Fuehler/Beine -> 'MotteBodyAlbedo' (512), Fluegel -> 'MotteWingAlbedo'.
# Ausfuehren per Blender-MCP exec, nachdem motte_geometrie.py lief (builtins.motte_ns).
import builtins
import math
import numpy as np

ns = getattr(builtins, "motte_ns", None)
if ns is None:
    raise RuntimeError("motte_ns fehlt: erst motte_geometrie.py ausfuehren")

KOERPER_OBJS = ["Koerper", "Fuehler_L", "Fuehler_R", "Beine"]
WING_OBJS = ["Wing_L", "Wing_R"]


def uv_atlas(objs, margin):
    """Smart-Project fuer eine Objektgruppe -> gemeinsamer 0..1-Atlas."""
    import bpy
    bpy.ops.object.select_all(action='DESELECT')
    for n in objs:
        bpy.data.objects[n].select_set(True)
    bpy.context.view_layer.objects.active = bpy.data.objects[objs[0]]
    bpy.ops.object.mode_set(mode='EDIT')
    bpy.ops.mesh.select_all(action='SELECT')
    bpy.ops.uv.smart_project(angle_limit=math.radians(66), island_margin=margin)
    bpy.ops.object.mode_set(mode='OBJECT')


def _set_col(me, rng, jitter=0.10):
    if "col" not in [a.name for a in me.attributes]:
        me.attributes.new("col", 'BYTE_COLOR', 'FACE')
    attr = me.attributes["col"]
    return attr


def body_face_colors():
    """Face-Farbe 'col': pelziger Thorax, gebaender Leib, dunkle Augen/Fuehler."""
    import bpy
    rng = np.random.default_rng(20)
    for n in KOERPER_OBJS:
        me = bpy.data.objects[n].data
        attr = _set_col(me, rng)
        for p in me.polygons:
            cx, cy, cz = p.center
            if n == "Koerper":
                if abs(cx) > 0.0058 and cy < -0.0300:
                    rgb = (0.045, 0.042, 0.040)              # Augen: fast schwarz
                elif cy < -0.0270:
                    rgb = (0.24, 0.20, 0.15)                 # Kopf: dunkler Pelz
                elif cy < 0.0045:
                    rgb = (0.46, 0.37, 0.24)                 # Thorax: ocker Pelz
                else:
                    rgb = (0.34, 0.27, 0.18)                 # Leib Basis
                    for fy in (0.010, 0.022, 0.031):         # dunkle Segmentbaender
                        if abs(cy - fy) < 0.0024:
                            rgb = (0.16, 0.13, 0.09)
            else:
                rgb = (0.15, 0.125, 0.095)                   # Fuehler/Beine dunkel
            j = rng.uniform(-0.10, 0.10, 3)                  # Pelz-Sprenkel
            rgb = tuple(min(1.0, max(0.0, c + jk)) for c, jk in zip(rgb, j))
            attr.data[p.index].color = (rgb[0], rgb[1], rgb[2], 1.0)

def wing_face_colors():
    """Face-Farbe Fluegel: Schuppenbander langs des Fluegels, dunkler Saum.
    Fransen (tiefes z) und Ansatz hellbraun."""
    import bpy
    rng = np.random.default_rng(21)
    for n in WING_OBJS:
        me = bpy.data.objects[n].data
        attr = _set_col(me, rng)
        for p in me.polygons:
            cx, cy, cz = p.center
            ax = abs(cx)
            base = np.array([0.52, 0.43, 0.30])       # heller Schuppen-Grundton
            band = math.sin(ax * 760.0 + cy * 180.0)   # Schuppenreihen
            if band > 0.55:
                base *= 0.72                           # dunklere Reihe
            if cz < -0.0012:                           # Franzen-Saum dunkel
                base = np.array([0.20, 0.16, 0.11])
            if ax < 0.008:                             
                base = base * 0.8 + np.array([0.10, 0.08, 0.05])  # Ansatz brauner
            j = rng.uniform(-0.06, 0.06, 3)
            rgb = tuple(min(1.0, max(0.0, c + jk)) for c, jk in zip(base, j))
            attr.data[p.index].color = (rgb[0], rgb[1], rgb[2], 1.0)

def paint_nodes(mat_name, img=None, wing=False):
    """MAT auf Attribut-Farbe + feines Muster (vor dem Backen) bzw. aufs Bild."""
    import bpy
    m = bpy.data.materials[mat_name]
    nt = m.node_tree
    if img is None:
        nt.nodes.clear()
        out = nt.nodes.new("ShaderNodeOutputMaterial")
        bsdf = nt.nodes.new("ShaderNodeBsdfPrincipled")
        bsdf.name = "Principled BSDF"
        attr = nt.nodes.new("ShaderNodeAttribute")
        attr.attribute_name = "col"
        tc = nt.nodes.new("ShaderNodeTexCoord")
        mp = nt.nodes.new("ShaderNodeMapping")
        mp.inputs["Scale"].default_value = (1.0, 1.0, 1.0)
        nt.links.new(tc.outputs["UV"], mp.inputs["Vector"])
        noise = nt.nodes.new("ShaderNodeTexNoise")
        noise.inputs["Scale"].default_value = 90.0 if not wing else 160.0
        noise.inputs["Detail"].default_value = 3.0
        nt.links.new(mp.outputs["Vector"], noise.inputs["Vector"])
        mix1 = nt.nodes.new("ShaderNodeMixRGB")
        mix1.blend_type = 'MULTIPLY'
        mix1.inputs["Fac"].default_value = 0.28
        nt.links.new(attr.outputs["Color"], mix1.inputs["Color1"])
        nt.links.new(noise.outputs["Fac"], mix1.inputs["Color2"])
        last = mix1.outputs["Color"]
        if wing:
            # Wellen-Schuppenreihen laengs der Fluegelspan-Richtung (U).
            wave = nt.nodes.new("ShaderNodeTexWave")
            wave.inputs["Scale"].default_value = 3.0
            wave.inputs["Distortion"].default_value = 6.0
            wave.inputs["Detail"].default_value = 2.0
            nt.links.new(mp.outputs["Vector"], wave.inputs["Vector"])
            soft = nt.nodes.new("ShaderNodeMixRGB")
            soft.blend_type = 'MULTIPLY'
            soft.inputs["Fac"].default_value = 0.30
            nt.links.new(last, soft.inputs["Color1"])
            nt.links.new(wave.outputs["Color"], soft.inputs["Color2"])
            last = soft.outputs["Color"]
        nt.links.new(last, bsdf.inputs["Base Color"])
        bsdf.inputs["Roughness"].default_value = 0.75
        nt.links.new(bsdf.outputs["BSDF"], out.inputs["Surface"])
    else:
        bsdf = nt.nodes["Principled BSDF"]
        tex = m.node_tree.nodes.active
        nt.links.new(tex.outputs["Color"], bsdf.inputs["Base Color"])
        for n in [x for x in nt.nodes if x.type in ("ATTRIBUTE", "TEX_NOISE",
                                                    "TEX_WAVE", "TEX_COORD",
                                                    "MAPPING", "MIX_RGB")]:
            nt.nodes.remove(n)

def bake_albedo(objs, mat_name, img_name):
    """DIFFUSE/Farbe backen (Cycles CPU, 512px) fuer eine Objektgruppe."""
    import bpy
    sc = bpy.context.scene
    sc.render.engine = "CYCLES"
    sc.cycles.device = "CPU"
    sc.cycles.samples = 16
    img = bpy.data.images.get(img_name)
    if img is not None:
        bpy.data.images.remove(img)
    img = bpy.data.images.new(img_name, 1024, 1024, alpha=False)
    m = bpy.data.materials[mat_name]
    node = m.node_tree.nodes.new("ShaderNodeTexImage")
    node.image = img
    m.node_tree.nodes.active = node
    bpy.ops.object.select_all(action="DESELECT")
    for n in objs:
        bpy.data.objects[n].select_set(True)
    bpy.context.view_layer.objects.active = bpy.data.objects[objs[0]]
    bpy.ops.object.bake(type="DIFFUSE", pass_filter={"COLOR"}, margin=6)
    return img

def save_and_export(img_body, img_wing):
    """PNGs sichern, MATs aufs Bild hangen, .blend + FBX exportieren."""
    import bpy
    art = r"C:\Users\vorau\dev\Spinnenspiel\Assets\_Project\Art\Insekten"
    for img, fn in ((img_body, "Motte_body_albedo.png"), (img_wing, "Motte_wing_albedo.png")):
        img.filepath_raw = art + "\\" + fn
        img.file_format = "PNG"
        img.save()
    paint_nodes("Motte_Koerper", img_body)
    paint_nodes("Motte_Fluegel", img_wing)
    blend = r"C:\Users\vorau\dev\Spinnenspiel\ArtSource\Insekten\Motte.blend"
    bpy.ops.wm.save_as_mainfile(filepath=blend)
    fbx = art + r"\Motte.fbx"
    bpy.ops.object.select_all(action="DESELECT")
    for n in KOERPER_OBJS + WING_OBJS:
        bpy.data.objects[n].select_set(True)
    bpy.ops.export_scene.fbx(filepath=fbx, use_selection=True,
                             apply_scale_options="FBX_SCALE_ALL",
                             axis_forward="-Z", axis_up="Y", bake_space_transform=True,
                             mesh_smooth_type="FACE", path_mode="STRIP",
                             use_mesh_modifiers=True, object_types={"MESH"})
    print("EXPORT ok:", fbx)

uv_atlas(KOERPER_OBJS, 0.02)
uv_atlas(WING_OBJS, 0.05)
body_face_colors()
wing_face_colors()
paint_nodes("Motte_Koerper")
paint_nodes("Motte_Fluegel", wing=True)
ib = bake_albedo(KOERPER_OBJS, "Motte_Koerper", "MotteBodyAlbedo")
iw = bake_albedo(WING_OBJS, "Motte_Fluegel", "MotteWingAlbedo")
save_and_export(ib, iw)
