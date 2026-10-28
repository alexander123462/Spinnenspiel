# Kritk-Rendern (Workbench, overlay-frei) fuer die Selbstkritik-Schleife MODELL19.
import bpy
import math
import os

OUT = r"C:\Users\vorau\dev\Spinnenspiel\Logs\modell\Fliege\kritik"


def render_views():
    os.makedirs(OUT, exist_ok=True)
    sc = bpy.context.scene
    sc.render.engine = "BLENDER_WORKBENCH"
    sc.display.shading.light = "STUDIO"
    sc.display.shading.color_type = "MATERIAL"
    sc.display.shading.show_cavity = True
    cam_data = bpy.data.cameras.new("CritCam")
    cam_data.clip_start = 0.001
    cam = bpy.data.objects.get("CritCam") or bpy.data.objects.new("CritCam", cam_data)
    if cam.name not in [o.name for o in sc.collection.objects]:
        sc.collection.objects.link(cam)
    sc.camera = cam
    views = [
        ("front", (0.0, -0.16, 0.004), (90, 0, 0)),
        ("seite", (0.17, 0.004, 0.002), (90, 0, 90)),
        ("oben", (0.0, 0.006, 0.16), (0, 0, 0)),
    ]
    sc.render.resolution_x = 900
    sc.render.resolution_y = 700
    for name, loc, rot in views:
        cam.location = loc
        cam.rotation_euler = [math.radians(a) for a in rot]
        sc.render.filepath = os.path.join(OUT, "k_" + name + ".png")
        bpy.ops.render.render(write_still=True)
    print("kritik gerendert:", os.listdir(OUT))
