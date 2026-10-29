import bpy, os
OUT = r'C:\Users\vorau\dev\Spinnenspiel\Logs\modell\Riesenblatt1'
os.makedirs(OUT, exist_ok=True)
sc = bpy.context.scene
sc.render.engine = 'BLENDER_EEVEE'
sc.display.shading.light = 'STUDIO'
sc.display.shading.color_type = 'MATERIAL'
sc.render.resolution_x = 900
sc.render.resolution_y = 900
cam_data = bpy.data.cameras.get('PCam') or bpy.data.cameras.new('PCam')
cam = bpy.data.objects.get('PCam') or bpy.data.objects.new('PCam', cam_data)
if cam.name not in [o.name for o in sc.collection.objects]:
    sc.collection.objects.link(cam)
sc.camera = cam
from mathutils import Vector
shots = {
    'vorne': ((3.1, -3.4, 1.5), (0, 0.2, 0.9)),
    'seite': ((4.4, 0.2, 1.1), (0, 0.2, 0.9)),
    'oben': ((0.5, -0.7, 5.0), (0, 0.2, 0.9)),
}
for name, (loc, tgt) in shots.items():
    cam.location = loc
    cam_data.lens = 42
    d = Vector(tgt) - Vector(loc)
    cam.rotation_euler = d.to_track_quat('-Z', 'Y').to_euler()
    p = os.path.join(OUT, 'rb1_%s.png' % name)
    sc.render.filepath = p
    bpy.ops.render.render(write_still=True)
    print('render:', p)

# --- Beleuchtung (einmalig) fuer EEVEE-Aufnahmen ---
def setup_light():
    so = bpy.data.objects.get('Sun')
    if so is None:
        ld = bpy.data.lights.new('Sun', 'SUN')
        ld.energy = 9.0
        ld.angle = 0.3
        so = bpy.data.objects.new('Sun', ld)
        bpy.context.scene.collection.objects.link(so)
    so.rotation_euler = (0.9, 0.0, 0.6)
    w = bpy.data.worlds.get('World') or bpy.data.worlds.new('World')
    bpy.context.scene.world = w
    w.use_nodes = True
    bg = w.node_tree.nodes.get('Background')
    if bg:
        bg.inputs[0].default_value = (0.5, 0.55, 0.6, 1)
        bg.inputs[1].default_value = 2.5


setup_light()
