import bpy
bpy.ops.wm.read_factory_settings(use_empty=True)
s = bpy.context.scene
d = bpy.data.lights.new("Sun", "SUN")
# Versucht 1: new mit scene-Argument
try:
    o = bpy.data.objects.new("Sun", d, s)
    print("new(name,data,scene) ok:", "Sun" in s.objects)
except Exception as e:
    print("try1 fail:", e)
# Versucht 2: Scene-Members
print("Scene members:", [m for m in dir(s) if not m.startswith("_") and ("add" in m or "link" in m or "collect" in m.lower())])
# Versucht 3: bpy.types.Object
print("bpy.types.Object members:", [m for m in dir(bpy.types.Object) if not m.startswith("_") and ("link" in m or "add" in m or "scene" in m.lower())])
