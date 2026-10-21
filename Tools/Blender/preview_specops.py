"""Textured preview of operator loadouts (variants + camo atlas) as one contact sheet.
    blender -b SoldierVariants.blend --python preview_specops.py -- <out_dir> [posed]
"""
import math
import os
import sys

import bpy
import numpy as np
from mathutils import Euler, Vector

args = sys.argv[sys.argv.index("--") + 1:]
out = args[0]
posed = len(args) > 1 and args[1] == "posed"
os.makedirs(out, exist_ok=True)
HERE = os.path.dirname(os.path.abspath(__file__))
UNITY = os.path.normpath(os.path.join(HERE, "..", "..", "Game", "CrimsonDraft"))
TEX = os.path.join(UNITY, "Assets", "Art", "Models", "CharactersFBX", "MarineRigged", "Variants", "Textures")
MARINE = os.path.join(UNITY, "Assets", "Art", "Models", "CharactersFBX", "Marine Character")

LOADOUTS = [  # (name, camo texture, variants)
    ("Sprite_GasMask", os.path.join(MARINE, "MarineTexture.png"), ["Helmet_Default", "Mask_GasMask", "Backpack_Default", "Vest_Default"]),
    ("Hood_Dual_Radio", os.path.join(TEX, "Camo_Woodland.png"), ["Helmet_Hood", "Mask_GasMaskDual", "Vest_ChestRig", "Backpack_Radio"]),
    ("Night_NVG_Skull", os.path.join(TEX, "Camo_NightOps.png"), ["Helmet_Beanie", "Glasses_NVGQuad", "Mask_Skull", "Vest_PlateCarrier", "Backpack_Hydration"]),
    ("Boonie_Shemagh", os.path.join(TEX, "Camo_Desert.png"), ["Helmet_Boonie", "Glasses_Shades", "Mask_Shemagh", "Vest_Bandolier", "Backpack_Large"]),
    ("Headset_Mono", os.path.join(TEX, "Camo_Urban.png"), ["Helmet_Headset", "Glasses_NVGMono", "Mask_Respirator", "Vest_PlateCarrier", "Backpack_Small"]),
    ("FastNVG", os.path.join(TEX, "Camo_Coyote.png"), ["Helmet_FastNVG", "Glasses_Default", "Vest_ChestRig", "Backpack_Default"]),
    ("Beret", os.path.join(TEX, "Camo_RangerGreen.png"), ["Helmet_Beret", "Vest_Default"]),
    ("Arctic_Resp", os.path.join(TEX, "Camo_Arctic.png"), ["Helmet_Default", "Glasses_Visor", "Mask_Respirator", "Vest_PlateCarrier", "Backpack_Large"]),
]


def image_material(mat, path):
    mat.use_nodes = True
    nt = mat.node_tree
    tex = next((n for n in nt.nodes if n.type == "TEX_IMAGE"), None) or nt.nodes.new("ShaderNodeTexImage")
    tex.image = bpy.data.images.load(path, check_existing=True)
    tex.interpolation = "Closest"
    nt.nodes.active = tex


sc = bpy.context.scene
sc.render.engine = "BLENDER_WORKBENCH"
sc.display.shading.light = "STUDIO"
sc.display.shading.color_type = "TEXTURE"
sc.render.resolution_x, sc.render.resolution_y = 300, 420
cam = bpy.data.objects.new("PreviewCam", bpy.data.cameras.new("PreviewCam"))
sc.collection.objects.link(cam)
sc.camera = cam
cam.data.type = "ORTHO"

image_material(bpy.data.materials["Mat_Gear"], os.path.join(TEX, "Gear_Atlas.png"))
arm = bpy.data.objects["Rig"]
if posed:
    pose = {"head": (20, 0, 30), "spine.003": (0, 0, 20), "spine.002": (12, 0, 0)}
    for pb in arm.pose.bones:
        pb.rotation_mode = "XYZ"
        if pb.name in pose:
            pb.rotation_euler = Euler([math.radians(a) for a in pose[pb.name]])

meshes = [o for o in bpy.data.objects if o.type == "MESH"]
VIEWS = [("full_front", (0, -4, 1.0), (90, 0, 0), 2.1), ("head_front", (0, -4, 1.72), (90, 0, 0), 0.62),
         ("head_side", (4, 0, 1.72), (90, 0, 90), 0.62), ("full_back", (0, 4, 1.0), (90, 0, 180), 2.1)]
tiles = []
for name, camo, variants in LOADOUTS:
    for m in ("Soldier1", "Soldier2"):
        image_material(bpy.data.materials[m], camo)
    for o in meshes:
        o.hide_render = not (o.name == "Marine" or o.name in variants)
    row = []
    for view, loc, rot, scale in VIEWS:
        cam.location = Vector(loc)
        cam.rotation_euler = Euler([math.radians(a) for a in rot])
        cam.data.ortho_scale = scale
        path = os.path.join(out, "%s_%s.png" % (name, view))
        sc.render.filepath = path
        bpy.ops.render.render(write_still=True)
        img = bpy.data.images.load(path)
        row.append(np.array(img.pixels[:], np.float32).reshape(img.size[1], img.size[0], 4))
        bpy.data.images.remove(img)
    tiles.append(np.hstack(row))

sheet = np.vstack(tiles[::-1])  # first loadout on top (Blender rows are bottom-up)
h, w = sheet.shape[:2]
res = bpy.data.images.new("sheet", w, h)
res.pixels[:] = sheet.ravel()
res.filepath_raw = os.path.join(out, "sheet%s.png" % ("_posed" if posed else ""))
res.file_format = "PNG"
res.save()
print("SHEET", res.filepath_raw, w, h)
