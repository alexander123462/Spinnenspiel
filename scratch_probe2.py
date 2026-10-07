import bpy
bpy.ops.wm.read_factory_settings(use_empty=True)
s = bpy.context.scene
d = bpy.data.lights.new("Sun", "SUN")
o = bpy.data.objects.new("Sun", d)
print(hasattr(o, "link"), hasattr(s, "objects"))
try:
    s.objects.link(o)
    print("linked via scene.objects.link")
except Exception as e:
    print("scene link fail:", e)
print("in scene:", o in s.objects)
print("in data.objects:", o in bpy.data.objects)
