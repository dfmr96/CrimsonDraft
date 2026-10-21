"""Close-up textured preview of the masks (front, 3/4, side, back) as one sheet.
    blender -b SoldierVariants.blend --python preview_masks.py -- <out_dir> [Mask_A Mask_B ...]
"""
import math
import os
import sys

import bpy
import numpy as np
from mathutils import Euler, Vector

args = sys.argv[sys.argv.index("--") + 1:]
out = args[0]
masks = args[1:] or ["Mask_GasMask", "Mask_GasMaskDual", "Mask_Respirator"]
os.makedirs(out, exist_ok=True)
HERE = os.path.dirname(os.path.abspath(__file__))
UNITY = os.path.normpath(os.path.join(HERE, "..", "..", "Game", "CrimsonDraft"))
TEX = os.path.join(UNITY, "Assets", "Art", "Models", "CharactersFBX", "MarineRigged", "Variants", "Textures")
MARINE = os.path.join(UNITY, "Assets", "Art", "Models", "CharactersFBX", "Marine Character", "MarineTexture.png")


def image_material(mat, path):
    mat.use_nodes = True
    nt = mat.node_tree
    tex = next((n for n in nt.nodes if n.type == "TEX_IMAGE"), None) or nt.nodes.new("ShaderNodeTexImage")
    tex.image = bpy.data.images.load(path, check_existing=True)
    tex.interpolation = "Closest"
    nt.nodes.active = tex


image_material(bpy.data.materials["Mat_Gear"], os.path.join(TEX, "Gear_Atlas.png"))
for m in ("Soldier1", "Soldier2"):
    image_material(bpy.data.materials[m], MARINE)
sc = bpy.context.scene
sc.render.engine = "BLENDER_WORKBENCH"
sc.display.shading.light = "STUDIO"
sc.display.shading.color_type = "TEXTURE"
sc.display.shading.show_cavity = True
sc.render.resolution_x, sc.render.resolution_y = 360, 360
cam = bpy.data.objects.new("PreviewCam", bpy.data.cameras.new("PreviewCam"))
sc.collection.objects.link(cam)
sc.camera = cam
cam.data.type = "ORTHO"
cam.data.ortho_scale = 0.42

zc = 1.78
views = [(0, -1), (-0.75, -0.66), (-1, 0), (0, 1)]  # front, 3/4 (character's right), side, back
rows = []
for mask in masks:
    for o in bpy.data.objects:
        if o.type == "MESH":
            o.hide_render = o.name not in ("Marine", mask, "Vest_Default")
    row = []
    for i, (dx, dy) in enumerate(views):
        cam.location = Vector((dx * 3, dy * 3, zc))
        cam.rotation_euler = Vector((0, 0, zc)).to_track_quat("-Z", "Y").to_euler() if False else \
            (Vector((0, 0, zc)) - cam.location).to_track_quat("-Z", "Y").to_euler()
        path = os.path.join(out, "%s_%d.png" % (mask, i))
        sc.render.filepath = path
        bpy.ops.render.render(write_still=True)
        img = bpy.data.images.load(path)
        row.append(np.array(img.pixels[:], np.float32).reshape(img.size[1], img.size[0], 4))
        bpy.data.images.remove(img)
    rows.append(np.hstack(row))
sheet = np.vstack(rows[::-1])
res = bpy.data.images.new("masks", sheet.shape[1], sheet.shape[0])
res.pixels[:] = sheet.ravel()
res.filepath_raw = os.path.join(out, "masks_sheet.png")
res.file_format = "PNG"
res.save()
print("MASKS", res.filepath_raw)
