"""
Body types for the soldier as shape keys (Female, Heavy, Muscular, Thin) on the body mesh, plus the
same shape keys transferred to every accessory so gear follows the body and does not clip.

Shape keys only move vertices: the skeleton, skin weights and every animation stay untouched (all
clips key position/rotation/scale of every bone, so bone proportions can't be changed). Height is
a uniform scale done in Unity (SoldierAppearance), not a shape key.

Displacements are built procedurally from the body's bone weights (regions) and a few smooth
falloffs around anatomical landmarks taken from the rig, so they stay smooth on the low-poly mesh.
Accessories get, for each vertex, the displacement of the nearest point of the body surface
(barycentric on the nearest triangle), which keeps their original clearance in every shape and in
any linear combination of shapes.
"""
import math

import bpy
from mathutils import Vector
from mathutils.bvhtree import BVHTree
from mathutils.interpolate import poly_3d_calc

BODY_SHAPES = ("Female", "Heavy", "Muscular", "Thin")
# Fit adjustments for face gear (Unity sliders Depth / Height / Size): pairs of shape keys so the
# weights stay in 0..100. Deltas at 100 %: depth 2 cm, height 1.5 cm, size 12 %.
FIT_SLOTS = ("Glasses", "Mask")
FIT_SHAPES = ("Fit_In", "Fit_Out", "Fit_Up", "Fit_Down", "Fit_Grow", "Fit_Shrink")
FIT_DEPTH, FIT_HEIGHT, FIT_SIZE = 0.02, 0.015, 0.12
# Global intensity per shape: the low-poly PSX silhouette needs readable differences.
STRENGTH = {"Female": 1.4, "Heavy": 1.3, "Muscular": 1.6, "Thin": 1.6}


def _gauss(x, s):
    return math.exp(-(x * x) / (2 * s * s))


def _weights(obj):
    names = {g.index: g.name for g in obj.vertex_groups}
    out = []
    for v in obj.data.vertices:
        out.append({names[g.group]: g.weight for g in v.groups if g.weight > 1e-4})
    return out


def _w(ws, *prefixes):
    """Sum of the weights of every group whose name starts with one of the prefixes (both sides)."""
    return sum(w for name, w in ws.items() if name.startswith(prefixes))


def body_deltas(body, arm):
    """{shape: [Vector delta per body vertex]} in armature space."""
    bones = {b.name: b.head_local for b in arm.data.bones}
    z_hip, z_s1, z_s2, z_s3, z_neck = (bones[n].z for n in ("Hip", "spine.001", "spine.002", "spine.003", "neck"))
    z_chest = z_s3 - 0.01
    z_belly = (z_hip + z_s2) / 2
    mw = body.matrix_world
    co = [mw @ v.co for v in body.data.vertices]
    nrm = [(mw.to_3x3() @ v.normal).normalized() for v in body.data.vertices]
    torso = [c for c in co if z_hip < c.z < z_neck and abs(c.x) < 0.2]
    cy = sum(c.y for c in torso) / len(torso)
    ws = _weights(body)
    out = {k: [] for k in BODY_SHAPES}

    for c, n, w in zip(co, nrm, ws):
        front = max(0.0, -n.y)  # character faces -Y
        side = 1.0 if c.x >= 0 else -1.0
        torso_w = _w(w, "spine.001", "spine.002", "spine.003", "Hip")
        shoulder_w = _w(w, "shoulder.")
        upper_arm_w = _w(w, "upper_arm.")
        forearm_w = _w(w, "forearm.")
        thigh_w = _w(w, "thigh.")
        shin_w = _w(w, "shin.")
        neck_w = _w(w, "neck")
        head_w = _w(w, "head")
        pelvis_w = _w(w, "pelvis.")

        # Heavy: belly, waist, hips and thighs grow; arms, neck and cheeks a little.
        amount = (0.040 * _w(w, "spine.001") + 0.035 * _w(w, "Hip") + 0.030 * _w(w, "spine.002")
                  + 0.016 * _w(w, "spine.003") + 0.022 * thigh_w + 0.012 * upper_arm_w + 0.007 * forearm_w
                  + 0.012 * neck_w + 0.006 * head_w * (1.0 if c.z < bones["head"].z + 0.06 else 0.0)
                  + 0.020 * pelvis_w)
        heavy = n * amount
        heavy.y -= 0.045 * front * _gauss(c.z - z_belly, 0.09) * torso_w  # belly
        out["Heavy"].append(heavy)

        # Muscular: shoulders, chest, arms, neck and thighs; slightly wider shoulders.
        amount = (0.028 * shoulder_w + 0.024 * upper_arm_w + 0.018 * forearm_w + 0.022 * _w(w, "spine.003")
                  + 0.012 * _w(w, "spine.002") + 0.018 * neck_w + 0.014 * thigh_w + 0.009 * shin_w)
        muscular = n * amount
        muscular.y -= 0.016 * front * _gauss(c.z - z_chest, 0.07) * _w(w, "spine.003")  # pecs
        muscular.x += side * 0.012 * (shoulder_w + upper_arm_w + forearm_w)
        out["Muscular"].append(muscular)

        # Thin: everything slimmer, mostly torso and limbs.
        amount = (0.018 * _w(w, "spine.001") + 0.015 * _w(w, "Hip") + 0.015 * _w(w, "spine.002")
                  + 0.012 * _w(w, "spine.003") + 0.012 * thigh_w + 0.008 * shin_w + 0.009 * upper_arm_w
                  + 0.006 * forearm_w + 0.006 * neck_w + 0.008 * pelvis_w)
        out["Thin"].append(-n * amount)

        # Female: narrower shoulders and waist, wider hips, chest, slimmer neck.
        female = Vector((0, 0, 0))
        female.x -= side * 0.016 * (shoulder_w + upper_arm_w + forearm_w + _w(w, "hand.", "Finger", "Thumb"))
        waist = _gauss(c.z - z_s1, 0.07) * torso_w
        radial = Vector((c.x, c.y - cy, 0))
        if radial.length > 1e-5:
            female -= radial.normalized() * 0.022 * waist
        hips = _gauss(c.z - z_hip, 0.08) * (torso_w + pelvis_w + thigh_w)
        female.x += side * 0.022 * hips
        female.y += 0.012 * hips * max(0.0, n.y)  # a bit of volume at the back too
        for bx in (-0.085, 0.085):
            female.y -= 0.034 * front * _gauss(c.x - bx, 0.05) * _gauss(c.z - (z_chest - 0.015), 0.05)
        female -= n * 0.006 * neck_w
        out["Female"].append(female)
    return {k: [d * STRENGTH[k] for d in v] for k, v in out.items()}


def set_shape_keys(obj, deltas):
    """(Re)creates Basis + one key per shape with the given per-vertex deltas."""
    if obj.data.shape_keys:
        obj.shape_key_clear()
    obj.shape_key_add(name="Basis", from_mix=False)
    basis = [v.co.copy() for v in obj.data.vertices]
    to_local = obj.matrix_world.inverted().to_3x3()
    for name in BODY_SHAPES:
        key = obj.shape_key_add(name=name, from_mix=False)
        for i, d in enumerate(deltas[name]):
            key.data[i].co = basis[i] + to_local @ d
        key.value = 0.0


def add_fit_keys(obj):
    """Fit shape keys for glasses/masks: move toward/away from the face (+Y is toward the face, the
    character looks at -Y), up/down, and grow/shrink around the piece's own center."""
    mw = obj.matrix_world
    to_local = mw.inverted().to_3x3()
    basis = obj.data.shape_keys.key_blocks["Basis"].data
    pts = [mw @ basis[i].co for i in range(len(obj.data.vertices))]
    lo = Vector((min(p.x for p in pts), min(p.y for p in pts), min(p.z for p in pts)))
    hi = Vector((max(p.x for p in pts), max(p.y for p in pts), max(p.z for p in pts)))
    center = (lo + hi) / 2
    moves = {
        "Fit_In": lambda p: Vector((0, FIT_DEPTH, 0)),
        "Fit_Out": lambda p: Vector((0, -FIT_DEPTH, 0)),
        "Fit_Up": lambda p: Vector((0, 0, FIT_HEIGHT)),
        "Fit_Down": lambda p: Vector((0, 0, -FIT_HEIGHT)),
        "Fit_Grow": lambda p: (p - center) * FIT_SIZE,
        "Fit_Shrink": lambda p: (p - center) * -FIT_SIZE,
    }
    for name in FIT_SHAPES:
        key = obj.shape_key_add(name=name, from_mix=False)
        for i, p in enumerate(pts):
            key.data[i].co = basis[i].co + to_local @ moves[name](p)
        key.value = 0.0


def transfer_to(obj, body, body_delta, tree, tris, body_co):
    """Each accessory vertex follows the nearest point of the body surface, for every shape."""
    mw = obj.matrix_world
    deltas = {k: [] for k in BODY_SHAPES}
    for v in obj.data.vertices:
        p = mw @ v.co
        loc, _n, ti, _d = tree.find_nearest(p)
        tri = tris[ti]
        bary = poly_3d_calc([body_co[i] for i in tri], loc)
        for k in BODY_SHAPES:
            d = Vector((0, 0, 0))
            for i, b in zip(tri, bary):
                d += body_delta[k][i] * b
            deltas[k].append(d)
    set_shape_keys(obj, deltas)


def push_out(obj, body_co, deltas, tris, margin=0.002):
    """Clearance fix: in every shape, an accessory vertex may not be deeper in the body than it was in
    the basis (or closer than margin when it was outside). Pushes it out along the body normal."""
    base_tree = BVHTree.FromPolygons(body_co, tris)
    mw = obj.matrix_world
    to_local = mw.inverted().to_3x3()
    keys = obj.data.shape_keys.key_blocks
    base = [mw @ keys["Basis"].data[i].co for i in range(len(obj.data.vertices))]
    base_dist = []
    for p in base:
        loc, n, _ti, _d = base_tree.find_nearest(p)
        base_dist.append((p - loc).dot(n))
    for name in BODY_SHAPES:
        shaped_body = [c + d for c, d in zip(body_co, deltas[name])]
        tree = BVHTree.FromPolygons(shaped_body, tris)
        kd = keys[name].data
        for i, d0 in enumerate(base_dist):
            p = mw @ kd[i].co
            loc, n, _ti, _d = tree.find_nearest(p)
            want = min(d0, margin)
            dist = (p - loc).dot(n)
            if dist < want:
                kd[i].co += to_local @ (n * (want - dist))


def push_base(obj, tree, margin=0.003):
    """Base-pose clearance for script-made gear: vertices inside (or touching) the body are pushed out
    along the body normal to margin. Original pieces (*_Default) are hand-made art and left as they are."""
    mw = obj.matrix_world
    to_local = mw.inverted().to_3x3()
    moved = 0
    for v in obj.data.vertices:
        p = mw @ v.co
        loc, n, _ti, _d = tree.find_nearest(p)
        if loc is None:
            continue
        dist = (p - loc).dot(n)
        if dist < margin and (p - loc).length < 0.10:
            v.co += to_local @ (n * (margin - dist))
            moved += 1
    return moved


def make(sv):
    arm, body = sv.rig(), sv.body()
    if body.data.shape_keys:
        body.shape_key_clear()  # measure on the clean basis
    deltas = body_deltas(body, arm)
    body_co = [body.matrix_world @ v.co for v in body.data.vertices]
    body.data.calc_loop_triangles()
    tris = [tuple(t.vertices) for t in body.data.loop_triangles]
    tree = BVHTree.FromPolygons(body_co, tris)
    fixed = {}
    for obj in sv.variant_objects():
        if obj.data.shape_keys:
            obj.shape_key_clear()
        if not obj.name.endswith("_Default"):
            for _ in range(4):  # a few passes: concave areas (neck, armpits) may need more than one
                n = push_base(obj, tree)
                if not n:
                    break
                fixed[obj.name] = fixed.get(obj.name, 0) + n
        transfer_to(obj, body, deltas, tree, tris, body_co)
        push_out(obj, body_co, deltas, tris)
        if obj.name.split("_", 1)[0] in FIT_SLOTS:
            add_fit_keys(obj)
    if fixed:
        sv.log("Base clearance (vertices pushed out of the body): %s" % fixed)
    set_shape_keys(body, deltas)
    sv.log("Body shapes %s on the body and %d accessories." % (", ".join(BODY_SHAPES), len(sv.variant_objects())))
    return report_clipping(sv)


def report_clipping(sv, tolerance=0.002):
    """Counts accessory vertices that end up inside the body for each shape at 100 %, compared with the
    basis (some straps already touch the skin by design). A growing count would mean clipping."""
    body = sv.body()
    keys = body.data.shape_keys.key_blocks
    body.data.calc_loop_triangles()
    tris = [tuple(t.vertices) for t in body.data.loop_triangles]
    mw = body.matrix_world
    report = {}
    for shape in ("Basis",) + BODY_SHAPES:
        co = [mw @ keys[shape].data[i].co for i in range(len(body.data.vertices))]
        tree = BVHTree.FromPolygons(co, tris)
        for obj in sv.variant_objects():
            ok = obj.data.shape_keys and shape in obj.data.shape_keys.key_blocks
            pts = obj.data.shape_keys.key_blocks[shape].data if ok else None
            inside = 0
            for i in range(len(obj.data.vertices)):
                p = obj.matrix_world @ (pts[i].co if pts else obj.data.vertices[i].co)
                loc, n, _ti, _d = tree.find_nearest(p)
                if loc is not None and (p - loc).dot(n) < -tolerance:
                    inside += 1
            report.setdefault(obj.name, {})[shape] = inside
    worse = {o: {s: c - r["Basis"] for s, c in r.items() if s != "Basis" and c - r["Basis"] > 0}
             for o, r in report.items()}
    worse = {o: r for o, r in worse.items() if r}
    sv.log("Clipping check (accessory vertices inside the body vs. basis): %s" %
           ("no new clipping" if not worse else worse))
    return report
