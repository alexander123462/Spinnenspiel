# Misst das neue Gras2-FBX: Dreiecke, Slots, Bounds, Objektname.
import bpy, sys

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=r"C:\Users\vorau\dev\Spinnenspiel\Assets\_Project\Art\Plants\Gras2\Gras2.fbx")
for ob in bpy.context.selected_objects:
    if ob.type != "MESH":
        continue
    me = ob.data
    tris = 0
    for p in me.polygons:
        tris += len(p.loop_totals) - 2 if hasattr(p, "loop_totals") else max(0, len(p.vertices) - 2)
    dims = ob.dimensions
    slots = [m.name if m else "?" for m in ob.material_slots]
    print("OBJEKT:", ob.name)
    print("DREIECKE:", tris)
    print("SLOTS:", slots)
    print("DIM X/Y/Z: %.3f %.3f %.3f" % (dims.x, dims.y, dims.z))
    print("ORIGIN WORLD Z: %.3f" % ob.location.z)
