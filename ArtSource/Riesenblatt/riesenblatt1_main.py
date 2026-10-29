# -*- coding: utf-8 -*-
# MODELL12 Riesenblatt1: Hauptskript — baut das Asset aus riesenblatt1_geometrie,
# legt Materialien (numpy-Atlas) an, exportiert FBX. Aufruf:
#   blender -b -P ArtSource/Riesenblatt/riesenblatt1_main.py
# In der MCP-Sitzung wird dasselbe Stueck Code exec() (Pfade relativ zum Projekt).
import bpy, math, os, sys
import numpy as np

ROOT = r"C:\Users\vorau\dev\Spinnenspiel"
GEO = os.path.join(ROOT, "ArtSource", "Riesenblatt", "riesenblatt1_geometrie.py")
TEX = os.path.join(ROOT, "Assets", "_Project", "Art", "Plants", "Riesenblatt", "Textures")
OUT_UNITY = os.path.join(ROOT, "Assets", "_Project", "Art", "Plants", "Riesenblatt", "Riesenblatt_1.fbx")
OUT_SRC = os.path.join(ROOT, "ArtSource", "Riesenblatt", "Riesenblatt_1.fbx")

exec(open(GEO, encoding="utf-8").read())


def make_object(name=None):
    """Loescht alte Riesenblatt1-Objekte und baut das Mesh neu (idempotent)."""
    for ob in list(bpy.data.objects):
        if ob.name.startswith("Riesenblatt1") or ob.name.startswith("Riesenblatt_1"):
            bpy.data.objects.remove(ob, do_unlink=True)
    verts, faces, uvs, mats = build_all()
    me = bpy.data.meshes.new("Riesenblatt1")
    me.from_pydata(verts, [], faces)
    me.update()
    uvl = me.uv_layers.new(name="UVMap")
    smooth = [True] * len(faces)
    me.polygons.foreach_set("use_smooth", smooth)
    for poly in me.polygons:
        for li in poly.loop_indices:
            vi = me.loops[li].vertex_index
            uvl.data[li].uv = uvs[vi]
    # Rippennaht: beide Halften legen eigene Rippen-Spalte an -> verschweissen.
    # (UVs vorher setzen: remove_doubles verschiebt Vertex-Indizes.)
    import bmesh
    bm = bmesh.new(); bm.from_mesh(me)
    n_before = len(bm.verts)
    bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=1e-4)
    bm.to_mesh(me); bm.free(); me.update()
    print('verschweisst:', n_before - len(me.vertices))
    ob = bpy.data.objects.new("Riesenblatt1", me)
    ob["rb_mats"] = list(mats)   # Merken: Indizes kommen nach materials.clear()
    bpy.context.scene.collection.objects.link(ob)
    return ob


def make_materials(ob):
    """Slot 0 = Stiel (MAT_Riesenblatt_Stem, opakes Faser-Albedo),
    Slot 1 = Blatt (MAT_Riesenblatt_Leaf, Albedo+Normal). Reihenfolge fest."""
    def img(name, noncolour=False):
        p = os.path.join(TEX, name)
        im = bpy.data.images.load(p, check_existing=True)
        if noncolour:
            im.colorspace_settings.name = "Non-Color"
        return im

    def new_mat(name):
        m = bpy.data.materials.get(name) or bpy.data.materials.new(name)
        m.use_nodes = True
        m.node_tree.nodes.clear()
        return m

    stem = new_mat("MAT_Riesenblatt_Stem")
    nt = stem.node_tree
    out = nt.nodes.new("ShaderNodeOutputMaterial"); out.location = (300, 0)
    b = nt.nodes.new("ShaderNodeBsdfPrincipled"); b.location = (0, 0)
    b.inputs["Roughness"].default_value = 0.6
    t = nt.nodes.new("ShaderNodeTexImage"); t.location = (-350, 100)
    t.image = img("Riesenblatt_Stem_Albedo.png")
    nt.links.new(t.outputs["Color"], b.inputs["Base Color"])
    nt.links.new(b.outputs[0], out.inputs["Surface"])

    leaf = new_mat("MAT_Riesenblatt_Leaf")
    nt = leaf.node_tree
    out = nt.nodes.new("ShaderNodeOutputMaterial"); out.location = (400, 0)
    b = nt.nodes.new("ShaderNodeBsdfPrincipled"); b.location = (0, 0)
    b.inputs["Roughness"].default_value = 0.5
    ta = nt.nodes.new("ShaderNodeTexImage"); ta.location = (-400, 200)
    ta.image = img("Riesenblatt_Leaf_Albedo.png")
    tn = nt.nodes.new("ShaderNodeTexImage"); tn.location = (-400, -150)
    tn.image = img("Riesenblatt_Leaf_Normal.png", noncolour=True)
    nm = nt.nodes.new("ShaderNodeNormalMap"); nm.location = (-100, -200)
    nt.links.new(ta.outputs["Color"], b.inputs["Base Color"])
    nt.links.new(tn.outputs["Color"], nm.inputs["Color"])
    nt.links.new(nm.outputs["Normal"], b.inputs["Normal"])
    nt.links.new(b.outputs[0], out.inputs["Surface"])

    ob.data.materials.clear()
    ob.data.materials.append(stem)   # Slot 0 = Stiel
    ob.data.materials.append(leaf)   # Slot 1 = Blatt
    # Indizes erst JETZT setzen: materials.clear() setzt alle zurueck auf 0.
    # foreach_set verlangt ein Array (Puffer), Python-Liste geht still fehl.
    idx = ob.get("rb_mats")   # ID-Python-Eigenschaft: get, nicht getattr
    if idx is not None:
        ob.data.polygons.foreach_set("material_index",
                                     np.asarray(list(idx), dtype=np.int32))
        ob.data.update()


def export_fbx(path):
    ob = bpy.data.objects["Riesenblatt1"]
    bpy.ops.object.select_all(action="DESELECT")
    ob.select_set(True)
    bpy.context.view_layer.objects.active = ob
    bpy.ops.export_scene.fbx(filepath=path, use_selection=True,
        apply_scale_options="FBX_SCALE_ALL", axis_forward="-Z", axis_up="Y",
        bake_space_transform=True, mesh_smooth_type="FACE",
        path_mode="STRIP", use_mesh_modifiers=True)
    print("exportiert:", path)


def preview(out_png):
    """Workbench-Render aus drei Blickwinkeln (vor/seite/oben) in Ein Bild."""
    import os
    sc = bpy.context.scene
    sc.render.engine = "BLENDER_WORKBENCH"
    sc.display.shading.light = "STUDIO"
    sc.display.shading.color_type = "MATERIAL"
    cam_data = bpy.data.cameras.new("PCam"); cam = bpy.data.objects.new("PCam", cam_data)
    bpy.context.scene.collection.objects.link(cam)
    from mathutils import Vector
    shots = [((3.1, -3.4, 1.5), (0, 0.3, 0.9)),   # vorn
             ((4.2, 0.2, 1.2), (0, 0.3, 0.9)),    # Seite
             ((0.4, -0.6, 5.2), (0, 0.3, 0.9))]   # oben
    outs = []
    for i, (loc, tgt) in enumerate(shots):
        cam.location = loc; cam_data.lens = 42
        d = Vector(tgt) - Vector(loc)
        cam.rotation_euler = d.to_track_quat("-Z", "Y").to_euler()
        sc.camera = cam
        p = out_png.replace(".png", "_%d.png" % i)
        sc.render.filepath = p
        bpy.ops.render.render(write_still=True)
        outs.append(p)
    print("previews:", outs)


if __name__ == "__main__":
    ob = make_object()
    make_materials(ob)
    stats()
    export_fbx(OUT_UNITY)
    export_fbx(OUT_SRC)
