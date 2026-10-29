# Render-Nahaufnahmen der Muecke (Workbench, 900x900) aus drei Blickwinkeln.
# Aufruf: render_views('C:/pfad/ordner') -> dateien vorne.png seite.png oben.png
import bpy, math, mathutils, os

def _cam(name, loc, target):
    cd = bpy.data.cameras.new(name)
    cd.lens = 80
    cd.clip_start = 1e-5   # Millimeter-Objekte: Default-Clip 0.1 m würde alles verschlucken
    cd.clip_end = 100.0
    cam = bpy.data.objects.new(name, cd)
    bpy.context.collection.objects.link(cam)
    d = mathutils.Vector(loc) - mathutils.Vector(target)
    cam.rotation_euler = d.to_track_quat('Z', 'Y').to_euler()
    cam.location = loc
    return cam

def render_views(outdir):
    os.makedirs(outdir, exist_ok=True)
    scene = bpy.context.scene
    scene.render.engine = 'BLENDER_WORKBENCH'
    scene.display.shading.light = 'STUDIO'
    scene.display.shading.color_type = 'MATERIAL'
    scene.display.shading.show_backface_culling = False
    scene.render.resolution_x = 900
    scene.render.resolution_y = 900
    old_cam = scene.camera
    views = {
        "vorne": ((-0.028, -0.075, 0.012), (0.0, -0.006, 0.0)),
        "seite": ((-0.085, -0.002, 0.010), (0.0, 0.003, 0.0)),
        "oben": ((0.0, -0.004, 0.095), (0.0, 0.003, 0.0)),
    }
    for key, (loc, tgt) in views.items():
        cam = _cam("Cam_" + key, loc, tgt)
        scene.camera = cam
        scene.render.filepath = os.path.join(outdir, key + ".png")
        bpy.ops.render.render(write_still=True)
        data = cam.data
        bpy.data.objects.remove(cam, do_unlink=True)
        bpy.data.cameras.remove(data)
    scene.camera = old_cam
    print("rendered:", sorted(os.listdir(outdir)))
