import bpy
bpy.ops.wm.read_factory_settings(use_empty=True)
s = bpy.context.scene
print("ops.light:", [m for m in dir(bpy.ops.light) if not m.startswith("_")] if hasattr(bpy.ops, "light") else "none")
print("ops.object add-ish:", [m for m in dir(bpy.ops.object) if "add" in m][:15])
d = bpy.data.lights.new("Sun", "SUN")
try:
    o = s.objects.new("Sun", d)
    print("scene.objects.new ok:", o.name)
except Exception as e:
    print("scene.objects.new fail:", e)
try:
    s.update()
    print("after s.update keys:", list(s.objects.keys()))
except Exception as e:
    print("update fail:", e)
