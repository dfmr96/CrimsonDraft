"""
Sheep Key case generator (Blender 4.2+, numpy).

Builds a small lined case (jewelry-box style: curved lid, padded interior) sized to hold KI_01, the
Sheep Key (SheepKey mesh in DoorKeys.fbx, used at 0.3 scale by KI_01_Pickeable), with a lid opening
animation, and exports it for Unity:

    Assets/Art/Models/ItemsFBX/0_NewItems/SheepKeyCase.fbx
    Assets/Art/Models/ItemsFBX/0_NewItems/SheepKeyCase.png   (128x128 PSX atlas)

Hierarchy (meters, Blender Z up; the case is long along X, front faces -Y):
    SheepKeyCase            empty, origin at the bottom centre
      Case_Base             shell + velvet lining + cushion + hinges/feet/catch (static)
      Case_Lid              curved lid, origin on the hinge axis; action "SheepKeyCase_Open" rotates it on X
      KeySocket             empty holding KI_01's pose: put the SheepKey mesh under it with identity
                            position/rotation and scale 0.3 and it lies in its recess on the cushion

The real key is imported as a hidden reference (collection "Reference", not exported) to check the fit
and to paint its silhouette on the cushion.

    blender -b --python sheep_key_case.py              # build + save SheepKeyCase.blend
    blender -b --python sheep_key_case.py -- export    # build + save + export FBX/PNG
"""
import math
import os
import sys

import bmesh
import bpy
import numpy as np
from mathutils import Matrix, Vector

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.normpath(os.path.join(HERE, "..", ".."))
ASSETS = os.path.join(REPO, "Game", "CrimsonDraft", "Assets")
KEY_FBX = os.path.join(ASSETS, "Art", "Models", "ItemsFBX", "Keys", "DoorKeys.fbx")
OUT_DIR = os.path.join(ASSETS, "Art", "Models", "ItemsFBX", "0_NewItems")
BLEND = os.path.join(HERE, "SheepKeyCase.blend")

KEY_MESH = "SheepKey"
KEY_SCALE = 0.3            # KI_01_Pickeable scale: the key is ~0.252 x 0.114 x 0.009 m

# Case dimensions (m)
L, W = 0.316, 0.176        # outer footprint (X long, Y deep)
WALL = 0.011
BASE_H = 0.058             # base height
CUSHION_TOP = 0.036        # cushion top above the floor of the case
CUSHION_PUFF = 0.006       # bevel of the cushion's top edges
LID_EDGE_H, LID_TOP_H = 0.014, 0.036   # curved lid: height at front/back edges and at the centre
LID_HOLLOW = 0.008         # velvet recess under the lid
LID_ARC_SEGS = 6
KEY_SINK = 0.003           # how far the key presses into the cushion

FPS = 30
OPEN_KEYS = [(1, 0.0), (5, -4.0), (8, -6.0), (22, -110.0), (26, -101.0), (30, -105.0)]  # (frame, deg)

# 128x128 atlas regions (px, origin bottom-left like UVs)
TEX = 128
REGIONS = {
    "leather": (0, 0, 64, 64),
    "velvet":  (64, 0, 64, 64),
    "cushion": (0, 64, 96, 52),
    "gold":    (96, 64, 32, 32),
}
REGION_ORDER = ["leather", "velvet", "cushion", "gold"]   # material slot index -> region


# ---------------------------------------------------------------------------------------------
# Scene helpers
# ---------------------------------------------------------------------------------------------
def reset_scene():
    for o in list(bpy.data.objects):
        bpy.data.objects.remove(o)
    for coll in (bpy.data.meshes, bpy.data.materials, bpy.data.images, bpy.data.actions, bpy.data.collections):
        for x in list(coll):
            coll.remove(x)
    sc = bpy.context.scene
    sc.render.fps = FPS
    sc.frame_start, sc.frame_end = OPEN_KEYS[0][0], OPEN_KEYS[-1][0]


def link(obj, coll=None):
    (coll or bpy.context.scene.collection).objects.link(obj)
    return obj


def mesh_object(name, bm, parent=None):
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me)
    bm.free()
    obj = link(bpy.data.objects.new(name, me))
    obj.parent = parent
    for _ in REGION_ORDER:
        me.materials.append(None)   # slots only carry the region index until the atlas is applied
    return obj


def set_region(faces, region):
    idx = REGION_ORDER.index(region)
    for f in faces:
        f.material_index = idx


def add_box(bm, center, size, region):
    """Axis-aligned box appended to bm; returns its faces."""
    c, s = Vector(center), Vector(size) / 2
    vs = [bm.verts.new(c + Vector((sx * s.x, sy * s.y, sz * s.z)))
          for sx in (-1, 1) for sy in (-1, 1) for sz in (-1, 1)]
    quads = [(0, 1, 3, 2), (4, 6, 7, 5), (0, 4, 5, 1), (2, 3, 7, 6), (0, 2, 6, 4), (1, 5, 7, 3)]
    faces = [bm.faces.new([vs[i] for i in q]) for q in quads]
    set_region(faces, region)
    return faces


def add_cylinder_x(bm, center, radius, length, region, segs=6):
    c = Vector(center)
    rings = []
    for sx in (-0.5, 0.5):
        rings.append([bm.verts.new(c + Vector((sx * length, radius * math.cos(a), radius * math.sin(a))))
                      for a in (2 * math.pi * i / segs for i in range(segs))])
    faces = [bm.faces.new((rings[0][i], rings[0][(i + 1) % segs], rings[1][(i + 1) % segs], rings[1][i]))
             for i in range(segs)]
    faces.append(bm.faces.new(list(reversed(rings[0]))))
    faces.append(bm.faces.new(rings[1]))
    set_region(faces, region)
    return faces


# ---------------------------------------------------------------------------------------------
# Geometry
# ---------------------------------------------------------------------------------------------
def build_base(parent):
    bm = bmesh.new()
    shell = add_box(bm, (0, 0, BASE_H / 2), (L, W, BASE_H), "leather")
    top = max(shell, key=lambda f: f.calc_center_median().z)
    # Hollow it: inset the top by the wall thickness and push the inner face down to the floor.
    res = bmesh.ops.inset_region(bm, faces=[top], thickness=WALL, depth=0)
    set_region(res["faces"], "gold")                 # thin rim along the opening
    ext = bmesh.ops.extrude_face_region(bm, geom=[top])
    new_faces = [g for g in ext["geom"] if isinstance(g, bmesh.types.BMFace)]
    bmesh.ops.translate(bm, vec=(0, 0, -(BASE_H - WALL)), verts=[g for g in ext["geom"] if isinstance(g, bmesh.types.BMVert)])
    if top.is_valid:
        bmesh.ops.delete(bm, geom=[top], context="FACES_ONLY")
    set_region(new_faces, "velvet")
    # The shell is closed, so consistent outward normals make the cavity's walls/floor face into it.
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])

    # Cushion: fills the floor up to CUSHION_TOP, top edges bevelled so it reads as padded.
    inner_l, inner_w = L - 2 * WALL - 0.002, W - 2 * WALL - 0.002
    cush = add_box(bm, (0, 0, WALL + CUSHION_TOP / 2), (inner_l, inner_w, CUSHION_TOP), "velvet")
    ctop = max(cush, key=lambda f: f.calc_center_median().z)
    set_region([ctop], "cushion")
    bmesh.ops.bevel(bm, geom=list(ctop.edges), offset=CUSHION_PUFF, segments=2, profile=0.5, affect="EDGES")
    bm.normal_update()
    for f in bm.faces:
        if f.material_index == REGION_ORDER.index("velvet") and f.normal.z > 0.3 and f.calc_center_median().z > WALL + CUSHION_TOP - CUSHION_PUFF - 1e-4:
            f.material_index = REGION_ORDER.index("cushion")

    # Brass hardware: hinge barrels on the back edge, catch on the front, four feet.
    for x in (-L * 0.3, L * 0.3):
        add_cylinder_x(bm, (x, W / 2 + 0.002, BASE_H), 0.0045, 0.034, "gold")
    add_box(bm, (0, -W / 2 - 0.0015, BASE_H - 0.010), (0.026, 0.003, 0.016), "gold")
    for sx in (-1, 1):
        for sy in (-1, 1):
            add_box(bm, (sx * (L / 2 - 0.012), sy * (W / 2 - 0.012), -0.003), (0.016, 0.016, 0.006), "gold")
    obj = mesh_object("Case_Base", bm, parent)
    return obj


def lid_profile():
    """(y, z) points of the lid's outer arc, front (-Y) to back (+Y), relative to the base top."""
    pts = []
    for i in range(LID_ARC_SEGS + 1):
        t = i / LID_ARC_SEGS
        y = -W / 2 + W * t
        z = LID_EDGE_H + (LID_TOP_H - LID_EDGE_H) * math.sin(math.pi * t)
        pts.append((y, z))
    return pts


def build_lid(parent):
    bm = bmesh.new()
    prof = lid_profile()
    ends = []
    for sx in (-L / 2, L / 2):
        ring = [bm.verts.new((sx, W / 2, 0.0)), bm.verts.new((sx, -W / 2, 0.0))]
        ring += [bm.verts.new((sx, y, z)) for y, z in prof]
        ends.append(ring)
    n = len(ends[0])
    side = [bm.faces.new((ends[0][i], ends[0][(i + 1) % n], ends[1][(i + 1) % n], ends[1][i])) for i in range(n)]
    caps = [bm.faces.new(list(reversed(ends[0]))), bm.faces.new(ends[1])]
    set_region(side + caps, "leather")
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])

    bottom = min(bm.faces, key=lambda f: f.calc_center_median().z)
    res = bmesh.ops.inset_region(bm, faces=[bottom], thickness=WALL, depth=0)
    set_region(res["faces"], "gold")
    ext = bmesh.ops.extrude_face_region(bm, geom=[bottom])
    bmesh.ops.translate(bm, vec=(0, 0, LID_HOLLOW), verts=[g for g in ext["geom"] if isinstance(g, bmesh.types.BMVert)])
    hollow = [g for g in ext["geom"] if isinstance(g, bmesh.types.BMFace)]
    set_region(hollow, "velvet")
    if bottom.is_valid:
        bmesh.ops.delete(bm, geom=[bottom], context="FACES_ONLY")
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])

    # Clasp plate hanging from the front edge, over the base's catch.
    add_box(bm, (0, -W / 2 - 0.0025, -0.004), (0.022, 0.003, 0.020), "gold")

    # Origin on the hinge axis (back edge, base top): the lid geometry is built relative to it.
    hinge = Vector((0, W / 2 + 0.002, 0))
    bmesh.ops.translate(bm, vec=-hinge, verts=bm.verts[:])
    obj = mesh_object("Case_Lid", bm, parent)
    obj.location = Vector((0, 0, BASE_H)) + hinge
    return obj


# ---------------------------------------------------------------------------------------------
# Key reference (real SheepKey mesh, 0.3 scale) and socket
# ---------------------------------------------------------------------------------------------
def import_key():
    before = set(bpy.data.objects)
    bpy.ops.import_scene.fbx(filepath=KEY_FBX)
    new = [o for o in bpy.data.objects if o not in before]
    key = next(o for o in new if o.type == "MESH" and (o.name.startswith(KEY_MESH) or o.data.name.startswith(KEY_MESH)))
    # Keep only the key's mesh in local space: bake the object scale away and drop the rest of the FBX.
    me = key.data.copy()
    me.name = "Ref_SheepKey"
    for o in new:
        bpy.data.objects.remove(o)
    ref_coll = bpy.data.collections.new("Reference")
    bpy.context.scene.collection.children.link(ref_coll)
    ref = bpy.data.objects.new("Ref_SheepKey", me)
    ref_coll.objects.link(ref)

    # The mesh stays as authored (like Unity imports it from DoorKeys.fbx). The rotation that lays it
    # long axis -> X, thin axis -> Z goes on the socket instead, so in Unity the key mesh goes under
    # KeySocket with identity position/rotation and scale KEY_SCALE.
    co = np.array([v.co[:] for v in me.vertices])
    dims = co.max(0) - co.min(0)
    order = np.argsort(dims)                      # thin, mid, long
    rot = np.zeros((3, 3))
    rot[0, order[2]] = 1.0                        # long -> X
    rot[1, order[1]] = 1.0                        # mid  -> Y
    rot[2, order[0]] = 1.0                        # thin -> Z
    if np.linalg.det(rot) < 0:
        rot[1] *= -1
    centre = Vector((co.max(0) + co.min(0)) / 2)
    key_rot = Matrix([list(r) for r in rot]).to_4x4()
    key_dims = np.abs(rot @ dims) * KEY_SCALE
    return ref, key_dims, key_rot, centre


def place_key(ref, key_dims, key_rot, centre, parent):
    socket = link(bpy.data.objects.new("KeySocket", None))
    socket.parent = parent
    socket.empty_display_type = "ARROWS"
    socket.empty_display_size = 0.03
    rest = Vector((0, 0, WALL + CUSHION_TOP + key_dims[2] / 2 - KEY_SINK))   # where the key's centre sits
    # Socket = key pose: rotated like the key and shifted so the mesh's bbox centre lands on `rest`.
    socket.matrix_local = Matrix.Translation(rest - key_rot @ (centre * KEY_SCALE)) @ key_rot
    ref.parent = socket
    ref.matrix_parent_inverse = Matrix.Identity(4)
    ref.matrix_local = Matrix.Scale(KEY_SCALE, 4)
    ref.hide_render = True
    return socket


def check_fit(key_dims):
    inner = (L - 2 * WALL - 2 * CUSHION_PUFF, W - 2 * WALL - 2 * CUSHION_PUFF)
    key_top = WALL + CUSHION_TOP + key_dims[2] - KEY_SINK
    clearance_lid = BASE_H + LID_HOLLOW - key_top
    report = {
        "key_dims_m": [round(float(x), 4) for x in key_dims],
        "cushion_flat_top_m": [round(x, 4) for x in inner],
        "margin_x_m": round(inner[0] - float(key_dims[0]), 4),
        "margin_y_m": round(inner[1] - float(key_dims[1]), 4),
        "key_top_below_rim_m": round(BASE_H - key_top, 4),
        "clearance_to_lid_m": round(clearance_lid, 4),
    }
    assert report["margin_x_m"] > 0 and report["margin_y_m"] > 0, report
    assert report["key_top_below_rim_m"] > 0, report
    return report


# ---------------------------------------------------------------------------------------------
# UVs + texture atlas
# ---------------------------------------------------------------------------------------------
def planar_uvs(obj):
    """Each face is projected on its dominant axis and squeezed into its region's rect,
    normalised over the bounding box of all faces of that region on the object."""
    me = obj.data
    bm = bmesh.new()
    bm.from_mesh(me)
    uv = bm.loops.layers.uv.new("UV")
    axes_for = {0: (1, 2), 1: (0, 2), 2: (0, 1)}
    groups = {}
    cushion = REGION_ORDER.index("cushion")
    for f in bm.faces:
        # The whole cushion top (flat + puffed edges) shares one XY projection, the one the key
        # silhouette is painted with (see cushion_rect_bounds).
        a = 2 if f.material_index == cushion else int(np.argmax([abs(c) for c in f.normal]))
        groups.setdefault((f.material_index, a), []).append(f)
    for (mi, a), faces in groups.items():
        rx, ry, rw, rh = REGIONS[REGION_ORDER[mi]]
        u_ax, v_ax = axes_for[a]
        pts = np.array([[l.vert.co[u_ax], l.vert.co[v_ax]] for f in faces for l in f.loops])
        lo, hi = pts.min(0), np.maximum(pts.max(0), pts.min(0) + 1e-6)
        for f in faces:
            for l in f.loops:
                t = (np.array([l.vert.co[u_ax], l.vert.co[v_ax]]) - lo) / (hi - lo)
                l[uv].uv = ((rx + 1 + t[0] * (rw - 2)) / TEX, (ry + 1 + t[1] * (rh - 2)) / TEX)
    bm.to_mesh(me)
    bm.free()


def cushion_rect_bounds(base):
    co = [v.co for p in base.data.polygons if p.material_index == REGION_ORDER.index("cushion")
          for v in (base.data.vertices[i] for i in p.vertices)]
    return (min(c.x for c in co), min(c.y for c in co)), (max(c.x for c in co), max(c.y for c in co))


def noise(rng, shape, scale, amp):
    small = rng.random((max(1, shape[0] // scale), max(1, shape[1] // scale)))
    big = np.kron(small, np.ones((scale, scale)))[:shape[0], :shape[1]]
    big = np.pad(big, ((0, shape[0] - big.shape[0]), (0, shape[1] - big.shape[1])), mode="edge")
    return (big - 0.5) * 2 * amp


def make_atlas(base, ref):
    rng = np.random.default_rng(7)
    img = np.zeros((TEX, TEX, 4), np.float32)
    img[..., 3] = 1.0

    def fill(name, color, grain):
        x, y, w, h = REGIONS[name]
        n = noise(rng, (h, w), 1, grain) + noise(rng, (h, w), 4, grain * 1.4)
        img[y:y + h, x:x + w, :3] = np.clip(np.array(color) * (1 + n[..., None]), 0, 1)

    fill("leather", (0.30, 0.075, 0.07), 0.10)     # oxblood leather
    fill("velvet", (0.07, 0.09, 0.20), 0.08)       # navy velvet
    fill("cushion", (0.08, 0.10, 0.23), 0.07)
    fill("gold", (0.72, 0.56, 0.24), 0.12)          # tarnished brass

    # Worn, lighter leather along the region's border (edges of each face get a little scuffed).
    x, y, w, h = REGIONS["leather"]
    yy, xx = np.mgrid[0:h, 0:w]
    edge = np.minimum.reduce([xx, yy, w - 1 - xx, h - 1 - yy])
    img[y:y + h, x:x + w, :3] *= (1 + 0.35 * (edge < 3))[..., None]

    # Key silhouette pressed into the cushion: rasterise the key's top view into the cushion rect.
    (cx0, cy0), (cx1, cy1) = cushion_rect_bounds(base)
    rx, ry, rw, rh = REGIONS["cushion"]
    mask = np.zeros((rh, rw), bool)
    to_px = lambda p: (1 + (p[0] - cx0) / (cx1 - cx0) * (rw - 2), 1 + (p[1] - cy0) / (cy1 - cy0) * (rh - 2))
    me = ref.data
    me.calc_loop_triangles()
    bpy.context.view_layer.update()                     # matrix_world is stale right after matrix_local edits
    m = ref.matrix_world                                # key mesh -> case space (the root sits at the origin)
    vs = [to_px(m @ v.co) for v in me.vertices]
    for tri in me.loop_triangles:
        (ax, ay), (bx, by), (cx, cy) = (vs[i] for i in tri.vertices)
        x0, x1 = int(max(0, min(ax, bx, cx))), int(min(rw - 1, max(ax, bx, cx) + 1))
        y0, y1 = int(max(0, min(ay, by, cy))), int(min(rh - 1, max(ay, by, cy) + 1))
        if x1 < x0 or y1 < y0:
            continue
        py, px = np.mgrid[y0:y1 + 1, x0:x1 + 1] + 0.5
        d = (by - cy) * (ax - cx) + (cx - bx) * (ay - cy)
        if abs(d) < 1e-9:
            continue
        l1 = ((by - cy) * (px - cx) + (cx - bx) * (py - cy)) / d
        l2 = ((cy - ay) * (px - cx) + (ax - cx) * (py - cy)) / d
        inside = (l1 >= 0) & (l2 >= 0) & (l1 + l2 <= 1)
        mask[y0:y1 + 1, x0:x1 + 1] |= inside
    grown = mask.copy()
    for dy, dx in ((1, 0), (-1, 0), (0, 1), (0, -1)):
        grown |= np.roll(np.roll(mask, dy, 0), dx, 1)
    rim = grown & ~mask
    region = img[ry:ry + rh, rx:rx + rw, :3]
    region[mask] *= 0.45            # the recess, in shadow
    region[rim] *= 1.35             # pressed edge catching light
    return img


def apply_atlas(objs, pixels):
    path = os.path.join(OUT_DIR, "SheepKeyCase.png")
    image = bpy.data.images.new("SheepKeyCase", TEX, TEX, alpha=True)
    image.pixels.foreach_set(np.ascontiguousarray(pixels).ravel())
    image.filepath_raw = path
    image.file_format = "PNG"
    mat = bpy.data.materials.new("SheepKeyCase")
    mat.use_nodes = True
    nt = mat.node_tree
    bsdf = next(nd for nd in nt.nodes if nd.type == "BSDF_PRINCIPLED")
    tex = nt.nodes.new("ShaderNodeTexImage")
    tex.image = image
    tex.interpolation = "Closest"
    nt.links.new(tex.outputs["Color"], bsdf.inputs["Base Color"])
    bsdf.inputs["Roughness"].default_value = 0.9
    for obj in objs:
        planar_uvs(obj)
        me = obj.data
        me.materials.clear()
        me.materials.append(mat)
        for p in me.polygons:
            p.material_index = 0
    return image


# ---------------------------------------------------------------------------------------------
# Animation + export
# ---------------------------------------------------------------------------------------------
def animate_lid(lid):
    # Set here, after import_key: the FBX importer overwrites the scene fps with the file's (24).
    sc = bpy.context.scene
    sc.render.fps, sc.render.fps_base = FPS, 1.0
    sc.frame_start, sc.frame_end = OPEN_KEYS[0][0], OPEN_KEYS[-1][0]
    lid.rotation_mode = "XYZ"
    lid.animation_data_create()
    act = bpy.data.actions.new("SheepKeyCase_Open")
    lid.animation_data.action = act
    for frame, deg in OPEN_KEYS:
        lid.rotation_euler = (math.radians(deg), 0, 0)
        lid.keyframe_insert("rotation_euler", index=0, frame=frame)
    for fc in act.fcurves:
        for k in fc.keyframe_points:
            k.interpolation = "BEZIER"
            k.handle_left_type = k.handle_right_type = "AUTO_CLAMPED"
    lid.rotation_euler = (0, 0, 0)
    bpy.context.scene.frame_set(1)
    return act


def build():
    reset_scene()
    root = link(bpy.data.objects.new("SheepKeyCase", None))
    root.empty_display_type = "PLAIN_AXES"
    root.empty_display_size = 0.05
    ref, key_dims, key_rot, centre = import_key()
    base = build_base(root)
    lid = build_lid(root)
    place_key(ref, key_dims, key_rot, centre, root)
    report = check_fit(key_dims)
    image = apply_atlas([base, lid], make_atlas(base, ref))
    animate_lid(lid)
    return root, image, report


def export(root, image):
    os.makedirs(OUT_DIR, exist_ok=True)
    image.save()
    for o in bpy.data.objects:
        o.select_set(False)
    for o in [root] + list(root.children):
        o.select_set(True)
    bpy.context.view_layer.objects.active = root
    bpy.ops.export_scene.fbx(
        filepath=os.path.join(OUT_DIR, "SheepKeyCase.fbx"),
        use_selection=True,
        object_types={"EMPTY", "MESH"},
        apply_scale_options="FBX_SCALE_ALL",
        axis_forward="-Z", axis_up="Y",
        use_mesh_modifiers=True,
        add_leaf_bones=False,
        bake_anim=True,
        bake_anim_use_all_actions=False,
        bake_anim_use_nla_strips=False,
        bake_anim_force_startend_keying=False,   # otherwise every static object gets constant curves
        bake_anim_simplify_factor=1.0,           # also lets the exporter drop the static objects' curves
        path_mode="STRIP",
    )


def main(do_export):
    root, image, report = build()
    print("fit:", report)
    bpy.ops.wm.save_as_mainfile(filepath=BLEND)
    if do_export:
        export(root, image)
        print("exported to", OUT_DIR)


if __name__ == "__main__":
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    main("export" in argv)
