# Parser for Resident Evil 3 (PC) PLD/PLW files (already extracted from Rofs with rofs.py).
# Same family as RE2 (see re2pld.py) with a different anim table, keyframe header and mesh (MD2) format.
import struct
from re2pld import sections, parse_tim, rgb15  # noqa: F401  (re-exported for the Blender importer)

# PLD: 0 EDD, 1 EMR, 2 MD2, 3 unknown, 4 TIM. PLW: 0-1 full-body EDD/EMR, 2-5 partial (upper body)
# EDD/EMR layers, 6 weapon EMR, 7 weapon MD2, 8 weapon TIM.
MESH_SECTION, TIM_SECTION = 2, 4


def parse_edd(d, base):
    # { u16 frame_count, u16 offset, u32 first_keyframe }[n]; frames are u16: low byte = keyframe
    # relative to first_keyframe, high bits = flags.
    n = struct.unpack_from('<H', d, base + 2)[0] // 8
    anims = []
    for i in range(n):
        count, ofs, start = struct.unpack_from('<HHI', d, base + i * 8)
        frames = struct.unpack_from('<%dH' % count, d, base + ofs)
        anims.append([(start + (f & 0xFF), f >> 8) for f in frames])
    return anims


def parse_emr(d, base):
    arm_ofs, anim_ofs, count, size = struct.unpack_from('<4H', d, base)
    # PLW full-body EMRs carry keyframes only (anim_ofs == 8): the skeleton comes from the PLD.
    has_skel = anim_ofs > 8
    relpos = [struct.unpack_from('<3h', d, base + 8 + i * 6) for i in range(count)] if has_skel else []
    parent = [-1] * count
    if has_skel:
        a = base + arm_ofs
        for i in range(count):
            n, o = struct.unpack_from('<HH', d, a + i * 4)
            for k in range(n):
                parent[d[a + o + k]] = i
    return dict(count=count, size=size, relpos=relpos, parent=parent, anim_base=base + anim_ofs)


def read_keyframe(d, emr, idx):
    # s16 offset[3] (cumulative root motion), s16 root_y, then count*3 packed 12-bit angles.
    o = emr['anim_base'] + idx * emr['size']
    sx, sy, sz, y = struct.unpack_from('<4h', d, o)
    raw = d[o + 8:o + emr['size']]
    angles = []
    for k in range(emr['count'] * 3):
        b = (k * 3) // 2
        if k % 2 == 0:
            a = raw[b] | ((raw[b + 1] & 0x0F) << 8)
        else:
            a = (raw[b] >> 4) | (raw[b + 1] << 4)
        angles.append(a)
    rots = [tuple(angles[i * 3:i * 3 + 3]) for i in range(emr['count'])]
    return (0, y, 0), (sx, sy, sz), rots


def parse_md2(d, base, bone_count):
    # u32 length, u32 object_count; objects: u32 vtx_ofs, nrm_ofs, vtx_count, tri_ofs, quad_ofs; u16 tri_count, quad_count.
    n = struct.unpack_from('<I', d, base + 4)[0]
    b = base + 8
    meshes = []
    for i in range(min(n, bone_count)):
        vo, no, vc, to, qo, tc, qc = struct.unpack_from('<5I2H', d, b + i * 24)
        verts = [struct.unpack_from('<3h', d, b + vo + k * 8) for k in range(vc)]
        faces, uvs = [], []
        for k in range(tc):
            t = d[b + to + k * 12:b + to + k * 12 + 12]
            faces.append((t[7], t[10], t[11]))
            uvs.append(([(t[0], t[1]), (t[4], t[5]), (t[8], t[9])], t[2] | t[3] << 8, t[6]))
        for k in range(qc):
            q = d[b + qo + k * 16:b + qo + k * 16 + 16]
            faces.append((q[10], q[11], q[14], q[15]))
            uvs.append(([(q[0], q[1]), (q[4], q[5]), (q[8], q[9]), (q[12], q[13])], q[2] | q[3] << 8, q[6]))
        meshes.append(dict(verts=verts, faces=faces, uvs=uvs))
    return meshes


parse_mesh = parse_md2


def _sub(a, b): return (a[0] - b[0], a[1] - b[1], a[2] - b[2])
def _add(a, b): return (a[0] + b[0], a[1] + b[1], a[2] + b[2])
def _dot(a, b): return a[0] * b[0] + a[1] * b[1] + a[2] * b[2]


def _seg_dist(p, a, b):
    ab = _sub(b, a)
    t = max(0.0, min(1.0, _dot(_sub(p, a), ab) / max(_dot(ab, ab), 1e-9)))
    q = (a[0] + ab[0] * t, a[1] + ab[1] * t, a[2] + ab[2] * t)
    return _dot(_sub(p, q), _sub(p, q)) ** 0.5


def skin(meshes, heads, parent):
    """RE3 meshes are not all local to their bone: most are stored in root space in the rest pose,
    and bones whose mesh is a 3-vertex dummy (forearm, thigh, foot...) get their geometry from the
    parent's mesh. The real per-vertex assignment is probably in PLD section 3 (not decoded); this
    approximates it: pick local vs root space per mesh, then give each vertex the nearest bone among
    the mesh's bone and its dummy descendants. Returns [(positions, bones)] per mesh."""
    n = len(heads)
    children = [[c for c in range(n) if parent[c] == b] for b in range(n)]
    dummy = [len(m['verts']) <= 3 for m in meshes]

    def segment(b):
        if children[b]:
            k = children[b]
            tip = tuple(sum(heads[c][a] for c in k) / len(k) for a in range(3))
        else:
            # Leaf: extend a little past the joint, along the parent bone.
            p = parent[b]
            d = _sub(heads[b], heads[p]) if p >= 0 else (0, 100, 0)
            tip = _add(heads[b], tuple(x * 0.3 for x in d))
        return heads[b], tip

    segs = [segment(b) for b in range(n)]
    out = []
    for i, m in enumerate(meshes):
        if dummy[i]:
            out.append(([], []))
            continue
        cand, stack = [i], [i]
        while stack:
            for c in children[stack.pop()]:
                if dummy[c]:
                    cand.append(c)
                    stack.append(c)

        def cost(pts):
            return sum(min(_seg_dist(p, *segs[b]) for b in cand) for p in pts) / len(pts)

        local = [_add(heads[i], v) for v in m['verts']]
        root = [_add(heads[0], v) for v in m['verts']]
        pts = local if cost(local) <= cost(root) else root
        bones = [min(cand, key=lambda b: _seg_dist(p, *segs[b])) for p in pts]
        out.append((pts, bones))
    return out
