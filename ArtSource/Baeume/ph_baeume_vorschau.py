# Poly Haven Tree Preview: renders an orthographic preview of an FBX and prints one stats line.
# Usage: blender -b -P ph_baeume_vorschau.py -- <fbx-pfad> <ausgabe-png>
import sys, math, os
import bpy

argv = sys.argv
idx = argv.index('--')
fbx_path = argv[idx + 1]
out_png = argv[idx + 2]

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=fbx_path)

meshes = [o for o in bpy.context.scene.objects if o.type == 'MESH']

# Dreieckcount: Summe (len(p.vertices)-2) per Polygon
tris = 0
for o in meshes:
    for p in o.data.polygons:
        tris += len(p.vertices) - 2

# World-Bounds ber matrix_world
xs, ys, zs = [], [], []
for o in meshes:
    mw = o.matrix_world
    for v in o.data.vertices:
        w = mw @ v.co
        xs.append(w.x); ys.append(w.y); zs.append(w.z)

if xs:
    x_min, x_max = min(xs), max(xs)
    y_min, y_max = min(ys), max(ys)
    z_min, z_max = min(zs), max(zs)
else:
    x_min = x_max = y_min = y_max = z_min = z_max = 0.0

hoehe = z_max - z_min
breite = max(x_max - x_min, y_max - y_min)
cx = (x_min + x_max) / 2.0
cy = (y_min + y_max) / 2.0
cz = (z_min + z_max) / 2.0

mats = sorted({m.name for o in meshes for m in o.data.materials if m})

print(f"{fbx_path.rpartition(chr(92))[2] if chr(92) in fbx_path else fbx_path.rpartition('/')[2]}; "
      f"Dreiecke={tris}; Hoehe={hoehe:.2f} m; Breite={breite:.2f} m; Materialien={','.join(mats)}")

# ---------- Scene setup ----------
scene = bpy.context.scene

world = bpy.data.worlds.new('World')
scene.world = world
bg_node = world.node_tree.nodes['Background']
bg_node.inputs['Color'].default_value = (0.75, 0.8, 0.85, 1.0)
bg_node.inputs['Strength'].default_value = 1.0

sun_data = bpy.data.lights.new('Sun', 'SUN')
sun_data.energy = 4.0
sun_obj = bpy.data.objects.new('Sun', sun_data)
scene.collection.objects.link(sun_obj)
sun_obj.rotation_euler = (math.radians(45), 0.0, math.radians(45))

cam_data = bpy.data.cameras.new('PreviewCam')
cam_data.type = 'ORTHO'
cam_data.ortho_scale = 1.1 * max(hoehe, breite, 1.0)
cam_data.clip_end = 10.0 * max(hoehe, 1.0)
cam_obj = bpy.data.objects.new('PreviewCam', cam_data)
scene.collection.objects.link(cam_obj)
cam_obj.location = (cx, cy - 3.0 * max(hoehe, 1.0), cz)
cam_obj.rotation_euler = (math.radians(90), 0.0, 0.0)
scene.camera = cam_obj

try:
    scene.render.engine = 'BLENDER_EEVEE_NEXT'
except Exception:
    scene.render.engine = 'CYCLES'
    scene.cycles.device = 'CPU'
    scene.cycles.samples = 16

bpy.context.scene.render.resolution_x = 640
bpy.context.scene.render.resolution_y = 640
bpy.context.scene.render.image_settings.file_format = 'PNG'
bpy.context.scene.render.filepath = out_png
bpy.ops.render.render(write_still=True)
