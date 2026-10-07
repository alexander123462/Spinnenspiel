import bpy
bpy.ops.wm.read_factory_settings(use_empty=True)
s = bpy.context.scene
try:
    bpy.ops.light.add(name="Sun", light_type="SUN")
    print("ops.light.add ok:", list(s.objects.keys()))
except Exception as e:
    print("ops.light.add fail:", e)
try:
    bpy.ops.object.camera_add(name="Cam")
    print("ops.camera_add ok:", list(s.objects.keys()))
except Exception as e:
    print("ops.camera_add fail:", e)
print("context members:", [m for m in dir(bpy.context) if not m.startswith("_") and "object" in m.lower()])
