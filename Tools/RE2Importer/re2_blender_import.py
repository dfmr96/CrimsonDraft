# Builds an RE2/RE3 (PC) player character in Blender: textured mesh, armature and one Action per animation.
import math, os, sys, importlib
import bpy
from mathutils import Euler, Vector

# Run via exec() with SCRIPT_DIR defined (see README.md); falls back to __file__ when run as a file.
SCRIPT_DIR = globals().get('SCRIPT_DIR') or os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, SCRIPT_DIR)
import re2pld, re3pld
importlib.reload(re2pld)
importlib.reload(re3pld)

GAME = r'C:\Program Files (x86)\GOG Galaxy\Games\Resident Evil 2\Pl0\pld'
SCALE = 0.00064
EULER_ORDER = globals().get('EULER_ORDER', 'ZYX')

# Leon's PLW files with animations, labelled by the weapon id in their name. The missing ids
# (03, 09-0C, 0E) are empty placeholders for Claire-only weapons.
LEON_WEAPONS = [
    ('PL00W00.PLW', 'W00_Desarmado'), ('PL00W01.PLW', 'W01_Cuchillo'), ('PL00W02.PLW', 'W02_VP70'),
    ('PL00W04.PLW', 'W04_CustomHandgun'), ('PL00W05.PLW', 'W05_Magnum'), ('PL00W06.PLW', 'W06_CustomMagnum'),
    ('PL00W07.PLW', 'W07_Escopeta'), ('PL00W08.PLW', 'W08_CustomShotgun'), ('PL00W0D.PLW', 'W0D_ColtSAA'),
    ('PL00W0F.PLW', 'W0F_SMG'), ('PL00W10.PLW', 'W10_Lanzallamas'), ('PL00W11.PLW', 'W11_Lanzacohetes'),
    ('PL00W12.PLW', 'W12_Gatling'),
]


def build(pld='PL00.PLD', plws=(), name='Leon', game_dir=GAME, fmt=re2pld, root_motion=False):
    """fmt: re2pld for RE2, re3pld for RE3 (game_dir = files extracted from Rofs5.dat with rofs.py).
    root_motion: also key the per-frame displacement, so the clip travels instead of playing in place."""
    d = open(os.path.join(game_dir, pld), 'rb').read()
    s = fmt.sections(d)
    anims = fmt.parse_edd(d, s[0])
    emr = fmt.parse_emr(d, s[1])
    n = emr['count']
    meshes = fmt.parse_mesh(d, s[fmt.MESH_SECTION], n)
    tim = fmt.parse_tim(d, s[fmt.TIM_SECTION])

    # Rest heads in PSX space (Y down).
    heads = [Vector()] * n
    for i in range(n):
        p = emr['parent'][i]
        rp = Vector(emr['relpos'][i])
        heads[i] = rp if p < 0 else heads[p] + rp

    # Armature: every bone points along +Y with no roll, so its rest orientation is identity and
    # the file's per-bone euler angles can be keyed straight into the pose.
    arm_data = bpy.data.armatures.new(name + '_Rig')
    arm = bpy.data.objects.new(name + '_Rig', arm_data)
    bpy.context.collection.objects.link(arm)
    arm.rotation_euler = (-math.pi / 2, 0, -math.pi / 2)   # PSX Y-down -> Blender Z-up, facing -Y
    arm.scale = (SCALE,) * 3
    bpy.context.view_layer.objects.active = arm
    bpy.ops.object.mode_set(mode='EDIT')
    ebs = []
    for i in range(n):
        eb = arm_data.edit_bones.new('bone_%02d' % i)
        eb.head = heads[i]
        eb.tail = heads[i] + Vector((0, 80, 0))
        eb.roll = 0
        ebs.append(eb)
    for i in range(n):
        p = emr['parent'][i]
        if p >= 0:
            ebs[i].parent = ebs[p]
    bpy.ops.object.mode_set(mode='OBJECT')
    arm_data.display_type = 'STICK'

    # Texture: 128px-wide 8bpp pages, page k uses CLUT row k.
    W, H = tim['width'], tim['height']
    img = bpy.data.images.new(name + '_TIM', W, H, alpha=True)
    px = [0.0] * (W * H * 4)
    pal = [[fmt.rgb15(c) for c in row] for row in tim['cluts']]
    for y in range(H):
        for x in range(W):
            row = pal[min(x // 128, len(pal) - 1)]
            c = row[tim['pixels'][y * W + x]]
            o = ((H - 1 - y) * W + x) * 4
            px[o:o + 4] = c
    img.pixels = px
    img.pack()

    mat = bpy.data.materials.new(name + '_Mat')
    mat.use_nodes = True
    nt = mat.node_tree
    bsdf = next(nd for nd in nt.nodes if nd.type == 'BSDF_PRINCIPLED')
    tex = nt.nodes.new('ShaderNodeTexImage')
    tex.image = img
    tex.interpolation = 'Closest'
    nt.links.new(tex.outputs['Color'], bsdf.inputs['Base Color'])
    nt.links.new(tex.outputs['Alpha'], bsdf.inputs['Alpha'])
    bsdf.inputs['Roughness'].default_value = 1.0
    mat.blend_method = 'CLIP'

    # Mesh: one object, each vertex fully weighted to one bone (rigid skinning, like the original).
    # RE2 meshes are local to their bone; RE3 needs fmt.skin to place and assign its vertices.
    if hasattr(fmt, 'skin'):
        placed = fmt.skin(meshes, [tuple(h) for h in heads], emr['parent'])
    else:
        placed = [([tuple(heads[b] + Vector(v)) for v in m['verts']], [b] * len(m['verts']))
                  for b, m in enumerate(meshes)]
    verts, faces, uvs, groups = [], [], [], [[] for _ in range(n)]
    for m, (pts, bones) in zip(meshes, placed):
        if not pts:
            continue
        base = len(verts)
        verts += pts
        for k, bone in enumerate(bones):
            groups[bone].append(base + k)
        for f, (uv, clut, page) in zip(m['faces'], m['uvs']):
            pu = (page & 0x0F) * 128
            if len(f) == 4:
                f = (f[0], f[1], f[3], f[2])
                uv = [uv[0], uv[1], uv[3], uv[2]]
            faces.append(tuple(base + i for i in f))
            uvs.append([((pu + u + 0.5) / W, 1.0 - (v + 0.5) / H) for u, v in uv])
    me = bpy.data.meshes.new(name)
    me.from_pydata(verts, [], faces)
    uvl = me.uv_layers.new(name='UV')
    for poly, fuv in zip(me.polygons, uvs):
        for li, uv in zip(poly.loop_indices, fuv):
            uvl.data[li].uv = uv
    me.materials.append(mat)
    me.update()
    mo = bpy.data.objects.new(name, me)
    bpy.context.collection.objects.link(mo)
    mo.parent = arm
    for b, idx in enumerate(groups):
        vg = mo.vertex_groups.new(name='bone_%02d' % b)
        vg.add(idx, 1.0, 'REPLACE')
    mo.modifiers.new('Armature', 'ARMATURE').object = arm

    # Animations.
    arm.animation_data_create()
    for pb in arm.pose.bones:
        pb.rotation_mode = EULER_ORDER
    actions = []

    def add_actions(data, emr_, anims_, prefix):
        for ai, frames in enumerate(anims_):
            act = bpy.data.actions.new('%s_%02d' % (prefix, ai))
            act.use_fake_user = True
            arm.animation_data.action = act
            prev = None
            for fi, (kf, _flags) in enumerate(frames):
                pos, speed, rots = fmt.read_keyframe(data, emr_, kf)
                # Unwrap 12-bit angles so linear interpolation never spins the long way round.
                cur = [[a - 4096 if a >= 2048 else a for a in r] for r in rots]
                if prev is not None:
                    for bi in range(len(cur)):
                        for ax in range(3):
                            while cur[bi][ax] - prev[bi][ax] > 2048: cur[bi][ax] -= 4096
                            while cur[bi][ax] - prev[bi][ax] < -2048: cur[bi][ax] += 4096
                prev = cur
                root = arm.pose.bones['bone_00']
                root.location = Vector(pos) - heads[0]
                if root_motion:
                    root.location += Vector(speed)
                root.keyframe_insert('location', frame=fi + 1)
                for bi, r in enumerate(cur):
                    pb = arm.pose.bones['bone_%02d' % bi]
                    pb.rotation_euler = Euler([a * 2 * math.pi / 4096.0 for a in r], EULER_ORDER)
                    pb.keyframe_insert('rotation_euler', frame=fi + 1)
            for fc in act.fcurves:
                for k in fc.keyframe_points:
                    k.interpolation = 'LINEAR'
            actions.append((act.name, len(frames)))

    add_actions(d, emr, anims, 'PLD')
    for plw in plws:
        plw, label = plw if isinstance(plw, tuple) else (plw, os.path.splitext(plw)[0])
        d2 = open(os.path.join(game_dir, plw), 'rb').read()
        s2 = fmt.sections(d2)
        add_actions(d2, fmt.parse_emr(d2, s2[1]), fmt.parse_edd(d2, s2[0]), label)

    # Each action carries its own frame range, so picking it in the Action Editor shows the whole clip.
    for act_name, _ in actions:
        act = bpy.data.actions[act_name]
        act.use_frame_range = True
        act.frame_start, act.frame_end = act.frame_range

    arm.animation_data.action = bpy.data.actions[actions[0][0]]
    sc = bpy.context.scene
    sc.render.fps = 30
    sc.frame_start, sc.frame_end = 1, actions[0][1]
    return arm, mo, actions
