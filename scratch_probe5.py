import bpy
bpy.ops.wm.read_factory_settings(use_empty=True)
s = bpy.context.scene
d = bpy.data.lights.new("Sun", "SUN")
o = bpy.data.objects.new("Sun", d)
print("in data.objects:", "Sun" in bpy.data.objects)
print("in scene.objects:", "Sun" in s.objects)
print("data keys:", list(bpy.data.objects.keys()))
print("scene keys:", list(s.objects.keys()))
print("update:", bpy.context.update_data_all("scene") if hasattr(bpy.context, "update_data_all") else "n/a")
print("link attr:", hasattr(o, "link"))
print("obj methods:", [m for m in dir(o) if not m.startswith("_") and not m in ("bl_rna","rna_type","data","name","type","parent","matrix_world","location","rotation_euler","scale","dimensions")][:30])
