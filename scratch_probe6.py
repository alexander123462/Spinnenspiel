import bpy
bpy.ops.wm.read_factory_settings(use_empty=True)
print("data.objects methods:", [m for m in dir(bpy.data.objects) if not m.startswith("_")])
print("scene members:", [m for m in dir(bpy.context.scene) if not m.startswith("_") and "object" in m.lower()])
print("bpy.types members:", [m for m in dir(bpy.types) if "Object" in m][:20])
