import bpy
bpy.ops.wm.read_factory_settings(use_empty=True)
print("bpy members:", [m for m in dir(bpy) if not m.startswith("_")][:40])
s = bpy.context.scene
d = bpy.data.lights.new("Sun", "SUN")
o = bpy.data.objects.new("Sun", d)
print("names in scene:", list(s.objects.keys()))
print("name check:", "Sun" in s.objects)
print("get:", s.objects.get("Sun"))
