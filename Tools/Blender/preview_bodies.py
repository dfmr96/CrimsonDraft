"""Body types preview: each body shape with a loadout, front and side, as one textured sheet.
    blender -b SoldierVariants.blend --python preview_bodies.py -- <out_dir>
"""
import math
import os
import sys

import bpy
import numpy as np
from mathutils import Euler, Vector

out = sys.argv[sys.argv.index("--") + 1]
os.makedirs(out, exist_ok=True)
HERE = os.path.dirname(os.path.abspath(__file__))
UNITY = os.path.normpath(os.path.join(HERE, "..", "..", "Game", "CrimsonDraft"))
TEX = os.path.join(UNITY, "Assets", "Art", "Models", "CharactersFBX", "MarineRigged", "Variants", "Textures")
MARINE = os.path.join(UNITY, "Assets", "Art", "Models", "CharactersFBX", "Marine Character", "MarineTexture.png")

BODIES = [("Base", {}), ("Female", {"Female": 1}), ("Heavy", {"Heavy": 1}), ("Muscular", {"Muscular": 1}),
          ("Thin", {"Thin": 1}), ("Heavy+Muscular", {"Heavy": 0.6, "Muscular": 0.6})]
LOADOUT = ["Helmet_FastNVG", "Mask_GasMask", "Vest_ChestRig", "Backpack_Radio", "Vest_Default"]
LOADOUT_B = ["Helmet_Default", "Glasses_Default", "Vest_PlateCarrier", "Backpack_Default"]


def image_material(mat, path):
    mat.use_nodes = True
    nt = mat.node_tree
    tex = next((n for n in nt.nodes if n.type == "TEX_IMAGE"), None) or nt.nodes.new("ShaderNodeTexImage")
    tex.image = bpy.data.images.load(path, check_existing=True)
    tex.interpolation = "Closest"
    nt.nodes.active = tex


image_material(bpy.data.materials["Mat_Gear"], os.path.join(TEX, "Gear_Atlas.png"))
for m in ("Soldier1", "Soldier2"):
    image_material(bpy.data.materials[m], os.path.join(TEX, "Camo_Woodland.png"))
sc = bpy.context.scene
sc.render.engine = "BLENDER_WORKBENCH"
sc.display.shading.light = "STUDIO"
sc.display.shading.color_type = "TEXTURE"
sc.render.resolution_x, sc.render.resolution_y = 260, 440
cam = bpy.data.objects.new("PreviewCam", bpy.data.cameras.new("PreviewCam"))
sc.collection.objects.link(cam)
sc.camera = cam
cam.data.type = "ORTHO"
cam.data.ortho_scale = 2.05

meshes = [o for o in bpy.data.objects if o.type == "MESH"]
rows = []
for loadout in (LOADOUT_B, ["Helmet_Boonie", "Mask_Shemagh", "Vest_Bandolier", "Backpack_Large", "Vest_BeltPouches"]):
    for view, loc, rot in (("front", (0, -4, 0.98), (90, 0, 0)), ("side", (4, 0, 0.98), (90, 0, 90))):
        row = []
        for name, weights in BODIES:
            for o in meshes:
                o.hide_render = not (o.name == "Marine" or o.name in loadout)
                if o.data.shape_keys:
                    for kb in o.data.shape_keys.key_blocks[1:]:
                        kb.value = weights.get(kb.name, 0.0)
            cam.location = Vector(loc)
            cam.rotation_euler = Euler([math.radians(a) for a in rot])
            path = os.path.join(out, "body_%s_%s.png" % (name.replace("+", "_"), view))
            sc.render.filepath = path
            bpy.ops.render.render(write_still=True)
            img = bpy.data.images.load(path)
            row.append(np.array(img.pixels[:], np.float32).reshape(img.size[1], img.size[0], 4))
            bpy.data.images.remove(img)
        rows.append(np.hstack(row))
sheet = np.vstack(rows[::-1])
res = bpy.data.images.new("bodies", sheet.shape[1], sheet.shape[0])
res.pixels[:] = sheet.ravel()
res.filepath_raw = os.path.join(out, "bodies_sheet.png")
res.file_format = "PNG"
res.save()
print("BODIES", res.filepath_raw)
