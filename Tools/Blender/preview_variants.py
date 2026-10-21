"""Renders combinations of variants over the body (rest pose and a stress pose) for a quick visual check.
    blender -b SoldierVariants.blend --python preview_variants.py -- <out_dir>
"""
import math
import os
import sys

import bpy
from mathutils import Euler, Vector

out = sys.argv[sys.argv.index("--") + 1]
os.makedirs(out, exist_ok=True)
sc = bpy.context.scene
sc.render.engine = "BLENDER_WORKBENCH"
sc.display.shading.light = "STUDIO"
sc.display.shading.color_type = "RANDOM"
sc.render.resolution_x, sc.render.resolution_y = 420, 620
cam = bpy.data.objects.new("PreviewCam", bpy.data.cameras.new("PreviewCam"))
sc.collection.objects.link(cam)
sc.camera = cam
cam.data.type = "ORTHO"
cam.data.ortho_scale = 1.15

arm = bpy.data.objects["Rig"]
variants = [o for o in bpy.data.objects if o.type == "MESH" and o.name != "Marine"]
COMBOS = {
    "default": ("Helmet_Default", "Glasses_Default", "Backpack_Default", "Vest_Default"),
    "hood_visor_small_belt": ("Helmet_Hood", "Glasses_Visor", "Backpack_Small", "Vest_BeltPouches"),
    "shades_large": ("Helmet_Default", "Glasses_Shades", "Backpack_Large", "Vest_Default"),
}
VIEWS = {"front": ((0, -4, 1.35), (90, 0, 0)), "side": ((4, 0, 1.35), (90, 0, 90)), "back": ((0, 4, 1.35), (90, 0, 180))}
POSE = {"head": (25, 0, 35), "neck": (10, 0, 10), "spine.003": (0, 0, 25), "spine.002": (15, 0, 0), "Hip": (0, 0, -15)}


def set_pose(posed):
    for pb in arm.pose.bones:
        pb.rotation_mode = "XYZ"
        pb.rotation_euler = Euler([math.radians(a) for a in POSE[pb.name]]) if posed and pb.name in POSE else Euler()


for posed in (False, True):
    set_pose(posed)
    for combo, names in COMBOS.items():
        for o in variants:
            o.hide_render = o.name not in names
        for view, (loc, rot) in VIEWS.items():
            cam.location = Vector(loc)
            cam.rotation_euler = Euler([math.radians(a) for a in rot])
            sc.render.filepath = os.path.join(out, "%s_%s_%s.png" % ("posed" if posed else "rest", combo, view))
            bpy.ops.render.render(write_still=True)
print("PREVIEW DONE")
