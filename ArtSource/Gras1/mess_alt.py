# Misst das alte Gras-Mesh: Dreiecke, Slots, Bounds.
import bpy, sys

path = r"C:/Users/vorau/dev/Spinnenspiel/Assets/_Project/Art/Scans/Models/grass_medium_01/grass_medium_01_1k.fbx"
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=path)
for ob in bpy.data.objects:
    if ob.type != 'MESH':
        continue
    me = ob.data
    tris = sum(len(p.vertices) - 2 for p in me.polygons)
    xs = [ (ob.matrix_world @ v.co) for v in me.vertices ]
    mn = [min(c[i] for c in xs) for i in range(3)]
    mx = [max(c[i] for c in xs) for i in range(3)]
    print("OBJ", ob.name, "verts", len(me.vertices), "tris", tris,
          "slots", [m.name if m else None for m in me.materials])
    print("BOUNDS min", [round(v,3) for v in mn], "max", [round(v,3) for v in mx],
          "size", [round(mx[i]-mn[i],3) for i in range(3)])
