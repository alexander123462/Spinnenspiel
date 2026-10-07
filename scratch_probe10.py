import bpy
bpy.ops.wm.read_factory_settings(use_empty=True)
s = bpy.context.scene
d = bpy.data.lights.new("Sun", "SUN")
o = bpy.data.objects.new("Sun", d)
coll = s.collection
print("collection members:", [m for m in dir(coll) if not m.startswith("_")])
try:
    coll.objects.link(o)
    print("linked:", "Sun" in s.objects)
except Exception as e:
    print("coll.objects.link fail:", e)
print("scene members full:", [m for m in dir(s) if not m.startswith("_")])
