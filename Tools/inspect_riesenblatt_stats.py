import bpy, os, math

folder = r"C:\Users\vorau\dev\Spinnenspiel\Assets\_Project\Art\Plants\Riesenblatt"
path = os.path.join(folder, "Riesenblatt_1.fbx")
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=path)

for obj in bpy.data.objects:
    if obj.type != "MESH":
        continue
    me = obj.data
    tris = sum(len(p.vertices) - 2 for p in me.polygons)
    # world-space bounds
    from mathutils import Vector
    corners = [obj.matrix_world @ Vector(v.co) for v in me.vertices]
    xs = [c.x for c in corners]; ys = [c.y for c in corners]; zs = [c.z for c in corners]
    print("OBJ %s" % obj.name)
    print("  verts=%d faces=%d tris=%d" % (len(me.vertices), len(me.polygons), tris))
    print("  slots=%s" % [s.material.name if s.material else None for s in obj.material_slots])
    print("  X %.3f..%.3f Y %.3f..%.3f Z %.3f..%.3f" % (min(xs), max(xs), min(ys), max(ys), min(zs), max(zs)))
    print("  extent %.3f x %.3f x %.3f m" % (max(xs)-min(xs), max(ys)-min(ys), max(zs)-min(zs)))
