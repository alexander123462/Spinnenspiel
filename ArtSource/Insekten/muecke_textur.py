# MODELL21 Schritt C: UV-Atlas + Face-Farben + Backen fuer die neue Muecke.
# Koerper/Kopf/Schnabel/Beine/Fuehler -> 'MueckeBodyAlbedo', Fluegel -> 'MueckeWingAlbedo'.
# Ausfuehren per Blender-MCP exec, nachdem muecke_geometrie.py lief (builtins.muecke_ns).
import builtins
import math
import numpy as np

ns = getattr(builtins, "muecke_ns", None)
if ns is None:
    raise RuntimeError("muecke_ns fehlt: erst muecke_geometrie.py ausfuehren")

KOERPER_OBJS = ["Koerper", "Kopf", "Schnabel", "Fuehler_L", "Fuehler_R", "Beine"]
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


def _attr(me):
    if "col" not in [a.name for a in me.attributes]:
        me.attributes.new("col", 'BYTE_COLOR', 'FACE')
    return me.attributes["col"]


def body_face_colors():
    """Face-Farbe 'col': dunkles Chitin, helle Abdomen-Baender, rotbraune Augen."""
    import bpy
    rng = np.random.default_rng(31)
    band_y = (0.0075, 0.0108, 0.0138, 0.0158)   # helle Hinterrand-Baender je Segment
    for n in KOERPER_OBJS:
        me = bpy.data.objects[n].data
        attr = _attr(me)
        for p in me.polygons:
            cx, cy, cz = p.center
            if n == "Kopf":
                if abs(cx) > 0.0016 and cy < -0.0092:
                    rgb = (0.150, 0.055, 0.040)           # Facettenaugen: dunkelrotbraun
                else:
                    rgb = (0.105, 0.092, 0.080)           # Kopfkapsel: schwarzbraun
            elif n == "Koerper":
                if cy < 0.0034:
                    rgb = (0.160, 0.135, 0.105)           # Thorax: braun, helle Schuppen
                    if cz > 0.0030:
                        rgb = (0.235, 0.205, 0.155)       # Rueckenrueckenschild heller
                else:
                    rgb = (0.095, 0.085, 0.072)           # Leib: fast schwarz
                    for fy in band_y:                    # helle Segmenteinsaetze
                        if abs(cy - fy) < 0.0013:
                            rgb = (0.330, 0.305, 0.250)
            elif n == "Schnabel":
                rgb = (0.100, 0.085, 0.070)               # Stechruesel dunkel
            else:                                        # Beine + Fuehler
                rgb = (0.130, 0.112, 0.090)
                if cz > 0.0045:
                    rgb = (0.180, 0.155, 0.120)           # Knie/Buerstchen heller
            j = rng.uniform(-0.05, 0.05, 3)              # Chitin-Sprenkel
            rgb = tuple(min(1.0, max(0.0, c + jk)) for c, jk in zip(rgb, j))
            attr.data[p.index].color = (rgb[0], rgb[1], rgb[2], 1.0)


def wing_face_colors():
    """Face-Farbe Fluegel: hellgraue Membran, dunklere Adern, Schuppenflecken."""
    import bpy
    rng = np.random.default_rng(32)
    for n in WING_OBJS:
        me = bpy.data.objects[n].data
        attr = _attr(me)
        for p in me.polygons:
            cx, cy, cz = p.center
            base = np.array([0.50, 0.50, 0.53])          # durchscheinend hellgrau
            if abs(cy) < 0.020 and (cz > 0.0048 or cz < 0.0016):
                base *= 0.55                             # Adern/Zellreihen dunkler
            fleck = math.sin(cy * 900.0) * math.sin(cx * 700.0)
            if fleck > 0.6:
                base *= 0.8                              # Schuppenflecken
            j = rng.uniform(-0.05, 0.05, 3)
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
        nt.links.new(tc.outputs["UV"], mp.inputs["Vector"])
        noise = nt.nodes.new("ShaderNodeTexNoise")
        noise.inputs["Scale"].default_value = 140.0 if not wing else 220.0
        noise.inputs["Detail"].default_value = 3.0
        nt.links.new(mp.outputs["Vector"], noise.inputs["Vector"])
        mix1 = nt.nodes.new("ShaderNodeMixRGB")
        mix1.blend_type = 'MULTIPLY'
        mix1.inputs["Fac"].default_value = 0.25
        nt.links.new(attr.outputs["Color"], mix1.inputs["Color1"])
        nt.links.new(noise.outputs["Fac"], mix1.inputs["Color2"])
        last = mix1.outputs["Color"]
        if wing:
            wave = nt.nodes.new("ShaderNodeTexWave")
            wave.inputs["Scale"].default_value = 4.0
            wave.inputs["Distortion"].default_value = 5.0
            nt.links.new(mp.outputs["Vector"], wave.inputs["Vector"])
            soft = nt.nodes.new("ShaderNodeMixRGB")
            soft.blend_type = 'MULTIPLY'
            soft.inputs["Fac"].default_value = 0.25
            nt.links.new(last, soft.inputs["Color1"])
            nt.links.new(wave.outputs["Color"], soft.inputs["Color2"])
            last = soft.outputs["Color"]
        nt.links.new(last, bsdf.inputs["Base Color"])
        bsdf.inputs["Roughness"].default_value = 0.55
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
    """DIFFUSE/Farbe backen (Cycles CPU, 1024px) fuer eine Objektgruppe."""
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
    for img, fn in ((img_body, "Muecke_body_albedo.png"),
                    (img_wing, "Muecke_wing_albedo.png")):
        img.filepath_raw = art + "\\" + fn
        img.file_format = "PNG"
        img.save()
    paint_nodes("Muecke_Koerper", img_body)
    paint_nodes("Muecke_Fluegel", img_wing, wing=True)
    blend = r"C:\Users\vorau\dev\Spinnenspiel\ArtSource\Insekten\Muecke.blend"
    bpy.ops.wm.save_as_mainfile(filepath=blend)
    fbx = art + r"\Muecke.fbx"
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
paint_nodes("Muecke_Koerper")
paint_nodes("Muecke_Fluegel", wing=True)
ib = bake_albedo(KOERPER_OBJS, "Muecke_Koerper", "MueckeBodyAlbedo")
iw = bake_albedo(WING_OBJS, "Muecke_Fluegel", "MueckeWingAlbedo")
save_and_export(ib, iw)
