import bpy
bpy.ops.wm.read_factory_settings(use_empty=True)
print("bpy.object exists:", hasattr(bpy, "object"))
if hasattr(bpy, "object"):
    print([m for m in dir(bpy.object) if not m.startswith("_")])
s = bpy.context.scene
d = bpy.data.lights.new("Sun", "SUN")
o = bpy.data.objects.new("Sun", d)
print("auto in scene:", o in s.objects)
print("auto in data:", o in bpy.data.objects)
