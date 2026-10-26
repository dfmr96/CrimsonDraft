# Parser for Resident Evil 2 (PC) PLD/PLW files: EDD (anim table), EMR (skeleton + keyframes), MD1 (mesh), TIM.
import struct


def u16(d, o): return struct.unpack_from('<H', d, o)[0]
def s16(d, o): return struct.unpack_from('<h', d, o)[0]
def u32(d, o): return struct.unpack_from('<I', d, o)[0]


def sections(d):
    dirofs, n = struct.unpack_from('<II', d, 0)
    return list(struct.unpack_from('<%dI' % n, d, dirofs))


def parse_edd(d, base):
    first_ofs = u16(d, base + 2)
    anims = []
    for i in range(first_ofs // 4):
        count, ofs = struct.unpack_from('<HH', d, base + i * 4)
        frames = [u32(d, base + ofs + j * 4) for j in range(count)]
        anims.append([(f & 0xFFF, f >> 12) for f in frames])
    return anims


def parse_emr(d, base):
    arm_ofs, anim_ofs, count, size = struct.unpack_from('<4H', d, base)
    relpos = [struct.unpack_from('<3h', d, base + 8 + i * 6) for i in range(count)] if arm_ofs else []
    parent = [-1] * count
    if arm_ofs:
        a = base + arm_ofs
        for i in range(count):
            n, o = struct.unpack_from('<HH', d, a + i * 4)
            for k in range(n):
                parent[d[a + o + k]] = i
    return dict(count=count, size=size, relpos=relpos, parent=parent, anim_base=base + anim_ofs)


def read_keyframe(d, emr, idx):
    o = emr['anim_base'] + idx * emr['size']
    pos = struct.unpack_from('<3h', d, o)
    speed = struct.unpack_from('<3h', d, o + 6)
    raw = d[o + 12:o + emr['size']]
    angles = []
    for k in range(emr['count'] * 3):
        b = (k * 3) // 2
        if k % 2 == 0:
            a = raw[b] | ((raw[b + 1] & 0x0F) << 8)
        else:
            a = (raw[b] >> 4) | (raw[b + 1] << 4)
        angles.append(a)
    rots = [tuple(angles[i * 3:i * 3 + 3]) for i in range(emr['count'])]
    return pos, speed, rots


def parse_md1(d, base):
    nobj = u32(d, base + 8)
    t = base + 12
    objs = []
    for i in range(nobj):
        vo, vc, no, nc, fo, fc, to = struct.unpack_from('<7I', d, t + i * 28)
        quad = i % 2 == 1
        verts = [struct.unpack_from('<3h', d, t + vo + k * 8) for k in range(vc)]
        faces, uvs = [], []
        for k in range(fc):
            if quad:
                v = struct.unpack_from('<8H', d, t + fo + k * 16)
                faces.append((v[1], v[3], v[5], v[7]))
                tx = d[t + to + k * 16:t + to + k * 16 + 16]
                clut, page = struct.unpack_from('<H', tx, 2)[0], struct.unpack_from('<H', tx, 6)[0]
                uvs.append(([(tx[0], tx[1]), (tx[4], tx[5]), (tx[8], tx[9]), (tx[12], tx[13])], clut, page))
            else:
                v = struct.unpack_from('<6H', d, t + fo + k * 12)
                faces.append((v[1], v[3], v[5]))
                tx = d[t + to + k * 12:t + to + k * 12 + 12]
                clut, page = struct.unpack_from('<H', tx, 2)[0], struct.unpack_from('<H', tx, 6)[0]
                uvs.append(([(tx[0], tx[1]), (tx[4], tx[5]), (tx[8], tx[9])], clut, page))
        objs.append(dict(verts=verts, faces=faces, uvs=uvs, quad=quad))
    # Pair (tris, quads) per bone; both share the same vertex list in practice but keep them separate.
    return [(objs[i * 2], objs[i * 2 + 1]) for i in range(nobj // 2)]


MESH_SECTION, TIM_SECTION = 2, 3


def parse_mesh(d, base, bone_count):
    # Common shape shared with re3pld: one {verts, faces, uvs} dict per bone.
    out = []
    for tri, quad in parse_md1(d, base)[:bone_count]:
        out.append(dict(verts=tri['verts'], faces=tri['faces'] + quad['faces'], uvs=tri['uvs'] + quad['uvs']))
    return out


def parse_tim(d, base):
    flags = u32(d, base + 4)
    o = base + 8
    cluts = []
    if flags & 8:
        clen, cx, cy, cw, ch = struct.unpack_from('<I4H', d, o)
        for r in range(ch):
            cluts.append([u16(d, o + 12 + (r * cw + c) * 2) for c in range(cw)])
        o += clen
    ilen, ix, iy, iw, ih = struct.unpack_from('<I4H', d, o)
    bpp = 8 if (flags & 3) == 1 else 4
    width = iw * 2 if bpp == 8 else iw * 4
    pix = d[o + 12:o + 12 + iw * 2 * ih]
    return dict(width=width, height=ih, bpp=bpp, cluts=cluts, pixels=pix)


def rgb15(c):
    r, g, b = c & 31, (c >> 5) & 31, (c >> 10) & 31
    a = 0.0 if c == 0 else 1.0
    return r / 31.0, g / 31.0, b / 31.0, a
