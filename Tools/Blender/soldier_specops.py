"""
Spec-ops accessory blockouts for soldier_variants.py (gas masks, NVGs, boonie, radio pack, chest rig...).

Every part is built with bmesh in armature space, UV-mapped (box projection) inside a rect of either
Gear_Atlas.png (GEAR_RECTS) or the soldier atlas (rect of an existing piece, so camo textures apply),
then weighted with soldier_variants.transfer_weights from the body. Masks follow the operators'
inventory portraits (Art/Sprites/UI/New/operators.png): twin round lenses, voicemitter, cheek filter.
"""
import math
import os

import bmesh
import bpy
from mathutils import Matrix, Vector
from mathutils.bvhtree import BVHTree

from soldier_gear_layout import GEAR_MATERIAL, GEAR_RECTS, GEAR_TEXTURE

PX = 1.0 / 256.0  # one atlas pixel; rects are inset to avoid bleeding with point sampling


# ---------------------------------------------------------------------------------------------
# Mesh builder
# ---------------------------------------------------------------------------------------------
class Builder:
    def __init__(self, base_mesh=None):
        self.bm = bmesh.new()
        if base_mesh is not None:
            self.bm.from_mesh(base_mesh)
        self.uv = self.bm.loops.layers.uv.active or self.bm.loops.layers.uv.new("UVMap")

    def _finish(self, verts, rect, mat, rect_fn=None):
        """Material + box-projected UVs into rect. rect_fn(face_center) -> rect assigns a rect per face
        (e.g. visor band of a mask shell); each group of faces is projected on its own bounds."""
        faces = list({f for v in verts for f in v.link_faces})
        groups = {}
        for f in faces:
            groups.setdefault(rect_fn(f.calc_center_median()) if rect_fn else rect, []).append(f)
        for r, group in groups.items():
            gv = {v for f in group for v in f.verts}
            lo = Vector((min(v.co.x for v in gv), min(v.co.y for v in gv), min(v.co.z for v in gv)))
            hi = Vector((max(v.co.x for v in gv), max(v.co.y for v in gv), max(v.co.z for v in gv)))
            size = Vector((max(hi.x - lo.x, 1e-5), max(hi.y - lo.y, 1e-5), max(hi.z - lo.z, 1e-5)))
            u0, v0, u1, v1 = r[0] + PX, r[1] + PX, r[2] - PX, r[3] - PX
            for f in group:
                f.material_index = mat
                n = f.normal
                axis = max(range(3), key=lambda i: abs(n[i]))
                a, b = {0: (1, 2), 1: (0, 2), 2: (0, 1)}[axis]
                for loop in f.loops:
                    co = loop.vert.co
                    fu = (co[a] - lo[a]) / size[a]
                    fv = (co[b] - lo[b]) / size[b]
                    loop[self.uv].uv = (u0 + fu * (u1 - u0), v0 + fv * (v1 - v0))
        return verts

    def cube(self, center, size, rect, mat=0, rot=Matrix.Identity(4)):
        m = Matrix.Translation(center) @ rot @ Matrix.Diagonal((*size, 1.0))
        return self._finish(bmesh.ops.create_cube(self.bm, size=1.0, matrix=m)["verts"], rect, mat)

    def cylinder(self, center, radius, depth, axis, rect, mat=0, segments=8, radius2=None, back_cap=True, caps=True):
        """Cylinder along axis. back_cap=False drops the cap facing -axis (hidden against the mask:
        saves vertices); caps=False makes an open tube."""
        axis = Vector(axis).normalized()
        rot = Vector((0, 0, 1)).rotation_difference(axis).to_matrix().to_4x4()
        m = Matrix.Translation(center) @ rot
        res = bmesh.ops.create_cone(self.bm, cap_ends=caps, segments=segments, radius1=radius,
                                    radius2=radius if radius2 is None else radius2, depth=depth, matrix=m)
        verts = res["verts"]
        if caps and not back_cap:
            faces = {f for v in verts for f in v.link_faces}
            back = [f for f in faces if len(f.verts) == segments and f.normal.dot(axis) < -0.9]
            bmesh.ops.delete(self.bm, geom=back, context="FACES_ONLY")
        return self._finish(verts, rect, mat)

    def disc(self, center, radius, axis, rect, mat=0, segments=8):
        """Single n-gon facing axis (grilles, filter caps, buckles, lens glass): 1 face only."""
        axis = Vector(axis).normalized()
        rot = Vector((0, 0, 1)).rotation_difference(axis).to_matrix().to_4x4()
        res = bmesh.ops.create_circle(self.bm, cap_ends=True, segments=segments, radius=radius,
                                      matrix=Matrix.Translation(center) @ rot)
        for f in {f for v in res["verts"] for f in v.link_faces}:
            if f.normal.dot(axis) < 0:
                f.normal_flip()
        return self._finish(res["verts"], rect, mat)

    def ribbon(self, points, sides, rect, mat=0, outward_from=None):
        """Flat single-sided strip (straps): points along the strap and the half-width side vector at each."""
        left = [self.bm.verts.new(p - s) for p, s in zip(points, sides)]
        right = [self.bm.verts.new(p + s) for p, s in zip(points, sides)]
        for i in range(len(points) - 1):
            f = self.bm.faces.new((left[i], right[i], right[i + 1], left[i + 1]))
            f.normal_update()
            if outward_from is not None and f.normal.dot(f.calc_center_median() - outward_from) < 0:
                f.normal_flip()
        return self._finish(left + right, rect, mat)

    def shell(self, center, radii, keep, rect, mat=0, thickness=0.006, u=12, v=8, rect_fn=None):
        """Ellipsoid surface, keeping only faces whose center passes keep(center); solidified when
        thickness > 0, otherwise single-sided with normals pointing outwards."""
        res = bmesh.ops.create_uvsphere(self.bm, u_segments=u, v_segments=v, radius=1.0)
        verts = res["verts"]
        for vt in verts:
            vt.co = Vector((vt.co.x * radii[0], vt.co.y * radii[1], vt.co.z * radii[2])) + Vector(center)
        faces = list({f for vt in verts for f in vt.link_faces})
        drop = [f for f in faces if not keep(f.calc_center_median())]
        bmesh.ops.delete(self.bm, geom=drop, context="FACES")
        verts = [vt for vt in verts if vt.is_valid]
        faces = list({f for vt in verts for f in vt.link_faces})
        for f in faces:
            f.normal_update()
            if f.normal.dot(f.calc_center_median() - Vector(center)) < 0:
                f.normal_flip()
        if thickness:
            solid = bmesh.ops.solidify(self.bm, geom=faces, thickness=thickness)
            verts += [g for g in solid["geom"] if isinstance(g, bmesh.types.BMVert)]
        return self._finish(verts, rect, mat, rect_fn)

    def grid(self, rows, keep, rect, mat=0, outward_from=None, rect_fn=None, closed=True, pole=None):
        """Quad grid from rows of points (each row a ring when closed). keep(face_center) filters faces;
        an optional pole closes the first row with triangles. Single-sided, normals pointing outwards."""
        vrows = [[self.bm.verts.new(p) for p in row] for row in rows]
        faces = []
        n = len(rows[0])
        for r0, r1 in zip(vrows, vrows[1:]):
            for i in range(n if closed else n - 1):
                j = (i + 1) % n
                quad = (r0[i], r0[j], r1[j], r1[i])
                center = sum((v.co for v in quad), Vector()) / 4
                if keep(center):
                    faces.append(self.bm.faces.new(quad))
        if pole is not None:
            pv = self.bm.verts.new(pole)
            for i in range(n if closed else n - 1):
                j = (i + 1) % n
                center = (pv.co + vrows[0][i].co + vrows[0][j].co) / 3
                if keep(center):
                    faces.append(self.bm.faces.new((pv, vrows[0][j], vrows[0][i])))
        for f in faces:
            f.normal_update()
            if outward_from is not None and f.normal.dot(f.calc_center_median() - Vector(outward_from)) < 0:
                f.normal_flip()
        loose = [v for row in vrows for v in row if not v.link_faces]
        if loose:
            bmesh.ops.delete(self.bm, geom=loose, context="VERTS")
        used = list(dict.fromkeys(v for f in faces for v in f.verts))
        return self._finish(used, rect, mat, rect_fn)

    def ring(self, zs, radii_per_z, cy, rect, mat=0, arc=None, sides=12, thickness=0.006):
        """Stacked rings (list of radius-per-angle lists), optionally only an arc (a0, a1)."""
        rows = []
        for z, radii in zip(zs, radii_per_z):
            row = []
            for i in range(sides + (1 if arc else 0)):
                a = (arc[0] + (arc[1] - arc[0]) * i / sides) if arc else 2 * math.pi * i / sides
                r = _sample_radius(radii, a)
                row.append(self.bm.verts.new((math.cos(a) * r, cy + math.sin(a) * r, z)))
            rows.append(row)
        faces = []
        n = len(rows[0])
        for r0, r1 in zip(rows, rows[1:]):
            for i in range(n - 1 if arc else n):
                j = (i + 1) % n
                faces.append(self.bm.faces.new((r0[i], r0[j], r1[j], r1[i])))
        bmesh.ops.recalc_face_normals(self.bm, faces=faces)
        verts = [v for row in rows for v in row]
        if thickness:
            solid = bmesh.ops.solidify(self.bm, geom=faces, thickness=thickness)
            verts += [g for g in solid["geom"] if isinstance(g, bmesh.types.BMVert)]
        return self._finish(verts, rect, mat)

    def to_object(self, sv, name, materials):
        me = bpy.data.meshes.new(name)
        self.bm.normal_update()
        self.bm.to_mesh(me)
        self.bm.free()
        for m in materials:
            me.materials.append(m)
        obj = bpy.data.objects.new(name, me)
        sv.slot_collection(sv.slot_of(name)).objects.link(obj)
        arm = sv.rig()
        obj.parent = arm
        obj.matrix_parent_inverse = Matrix.Identity(4)
        sv.ensure_armature_modifier(obj, arm)
        return obj


def _sample_radius(radii, a):
    n = len(radii)
    x = (a % (2 * math.pi)) / (2 * math.pi) * n
    i = int(math.floor(x)) % n
    f = x - math.floor(x)
    return radii[i] * (1 - f) + radii[(i + 1) % n] * f


# ---------------------------------------------------------------------------------------------
# Body measurements (armature space; character faces -Y, its left is +X)
# ---------------------------------------------------------------------------------------------
class Body:
    def __init__(self, sv):
        self.obj = sv.body()
        mw = self.obj.matrix_world
        self.co = [mw @ v.co for v in self.obj.data.vertices]
        head = self.obj.vertex_groups["head"].index
        head_co = [mw @ v.co for v in self.obj.data.vertices
                   if any(g.group == head and g.weight > 0.5 for g in v.groups)]
        self.hlo = Vector((min(c.x for c in head_co), min(c.y for c in head_co), min(c.z for c in head_co)))
        self.hhi = Vector((max(c.x for c in head_co), max(c.y for c in head_co), max(c.z for c in head_co)))
        self.hc, self.hr = (self.hlo + self.hhi) / 2, (self.hhi - self.hlo) / 2
        glo, ghi = sv.mesh_bounds(bpy.data.objects["Glasses_Default"])
        self.eye_z = (glo.z + ghi.z) / 2

    def face_y(self, z, half_width=0.04):
        """Most forward (min y) point of the body around height z near the center line."""
        ys = [c.y for c in self.co if abs(c.z - z) < 0.02 and abs(c.x) < half_width]
        return min(ys) if ys else self.hc.y - self.hr.y

    def ring_fit(self, z0, z1, sides=12, pad=0.01, max_x=0.24):
        pts = []
        for grow in range(8):  # low-poly body: widen the band until it has vertices
            pts = [c for c in self.co if z0 - 0.02 * grow < c.z < z1 + 0.02 * grow and abs(c.x) < max_x]
            if len(pts) >= 6:
                break
        cy = sum(c.y for c in pts) / len(pts)
        radii = []
        for i in range(sides):
            a = 2 * math.pi * i / sides
            best = 0.0
            for c in pts:
                d = Vector((c.x, c.y - cy))
                if d.length > 1e-6 and math.cos(math.atan2(d.y, d.x) - a) > math.cos(math.pi / sides):
                    best = max(best, d.length)
            radii.append(best)
        mean = sum(r for r in radii if r > 0) / max(1, sum(1 for r in radii if r > 0))
        return cy, [(r if r > 0 else mean) + pad for r in radii]


# ---------------------------------------------------------------------------------------------
# Materials
# ---------------------------------------------------------------------------------------------
def gear_material(textures_dir):
    mat = bpy.data.materials.get(GEAR_MATERIAL)
    if mat:
        return mat
    mat = bpy.data.materials.new(GEAR_MATERIAL)
    mat.use_nodes = True
    path = os.path.join(textures_dir, GEAR_TEXTURE)
    if os.path.exists(path):
        tex = mat.node_tree.nodes.new("ShaderNodeTexImage")
        tex.image = bpy.data.images.load(path, check_existing=True)
        tex.interpolation = "Closest"
        bsdf = mat.node_tree.nodes.get("Principled BSDF")
        if bsdf:
            mat.node_tree.links.new(tex.outputs["Color"], bsdf.inputs["Base Color"])
    return mat


# ---------------------------------------------------------------------------------------------
# Accessories
# ---------------------------------------------------------------------------------------------
def make(sv, textures_dir):
    existing = {o.name for o in bpy.data.objects}
    body = Body(sv)
    gear = gear_material(textures_dir)
    soldier = bpy.data.objects["Helmet_Default"].data.materials[0]  # Soldier2: camo-able atlas
    helmet_rect = sv.uv_rect(bpy.data.objects["Helmet_Default"])
    vest_rect = sv.uv_rect(bpy.data.objects["Vest_Default"])
    G = GEAR_RECTS
    hc, hr, ez = body.hc, body.hr, body.eye_z
    shell_r = (hr.x * 1.14, hr.y * 1.14, hr.z * 1.0)

    def front_y(x, z, radii=shell_r):
        """Front surface of the mask shell ellipsoid at (x, z)."""
        k = 1 - (x / radii[0]) ** 2 - ((z - hc.z) / radii[2]) ** 2
        return hc.y - radii[1] * math.sqrt(max(k, 0.05))

    def build(name, slot, fn, materials, base=None):
        if name in existing:
            return
        b = Builder(base_mesh=base)
        fn(b)
        obj = b.to_object(sv, name, materials)
        sv.transfer_weights(obj, body.obj, slot)
        sv.log("Created %s" % name)

    def mask_shell(b, z_lo, z_hi, rect, scale=1.0, x_max=None, segs=(12, 8), thickness=0.006):
        radii = tuple(r * scale for r in shell_r)
        b.shell(hc, radii, lambda c: c.y < hc.y - 0.2 * hr.y and z_lo < c.z < z_hi and (x_max is None or abs(c.x) < x_max),
                rect, thickness=thickness, u=segs[0], v=segs[1])

    def mask_strap(b):
        cy, radii = body.ring_fit(ez - 0.02, ez + 0.04, pad=0.008, max_x=0.15)
        b.ring([ez, ez + 0.02], [radii, radii], cy, G["RUBBER"], arc=(math.radians(20), math.radians(160)), sides=8, thickness=0.004)

    strap_r = (hr.x * 1.13, hr.y * 1.13, hr.z * 1.08)

    body.obj.data.calc_loop_triangles()
    body_tree = BVHTree.FromPolygons(body.co, [tuple(t.vertices) for t in body.obj.data.loop_triangles])

    def on_head(d, offset=0.007):
        """Point just above the real head surface in direction d (head frame: -Y front, +X left):
        raycast from outside towards the head centre; ellipsoid fallback if nothing is hit."""
        d = Vector(d).normalized()
        k = 1.0 / math.sqrt((d.x / strap_r[0]) ** 2 + (d.y / strap_r[1]) ** 2 + (d.z / strap_r[2]) ** 2)
        hit, _normal, _i, _dist = body_tree.ray_cast(hc + d * 0.5, -d)
        # Ignore hits on the shoulders/trapezius (low directions): only the head/neck surface counts.
        if hit is not None and (hit - hc).length < k * 1.1:
            return hit + d * offset
        return hc + d * k

    def strap(b, dirs, width=0.016, rect=None, steps=3, mat=0):
        """Webbing strap: a flat single-sided ribbon following the head surface (4 verts per segment)."""
        pts = []
        for d0, d1 in zip(dirs, dirs[1:]):
            for i in range(steps):
                pts.append(on_head(Vector(d0).lerp(Vector(d1), i / steps)))
        pts.append(on_head(dirs[-1]))
        sides = []
        for i, p in enumerate(pts):
            t = pts[min(i + 1, len(pts) - 1)] - pts[max(i - 1, 0)]
            sides.append((p - hc).normalized().cross(t).normalized() * (width / 2))
        b.ribbon(pts, sides, rect or G["STRAP"], mat, outward_from=hc)

    def plate(b, d, r, rect, segments=4):
        """Flat plate on the head surface (buckles, harness pad): one face."""
        p = on_head(d, offset=0.006)
        b.disc(p, r, p - hc, rect, segments=segments)

    def harness(b, sides=((0.75, -0.55, 0.15), (0.7, -0.6, -0.45))):
        """Gas mask 5-point head harness: top strap + temple and cheek straps to a back pad."""
        strap(b, [(0, -0.5, 0.85), (0, -0.1, 1.0), (0, 0.55, 0.8), (0, 0.95, 0.15)])  # starts above lenses/visor
        for sx in (-1, 1):
            for (x, y, z) in sides:
                strap(b, [(sx * x, y, z), (sx * 0.9, 0.2, z * 0.8), (sx * 0.3, 0.95, z * 0.4 + 0.05)])
                plate(b, (sx * x, y + 0.12, z), 0.011, G["METAL"])  # buckle
        plate(b, (0, 1, 0.12), 0.045, G["SEAL"])  # back pad

    def lens(b, x, z, r):
        """Lens glass disc on a screwed metal rim (rim: open-back cylinder)."""
        y = front_y(x, z)
        b.cylinder((x, y - 0.004, z), r + 0.007, 0.014, (0, 1, 0), G["RIM"], segments=8, back_cap=False)
        b.disc((x, y - 0.0115, z), r, (0, -1, 0), G["LENS"], segments=8)

    def voicemitter(b, center, axis, r=0.017):
        axis = Vector(axis).normalized()
        c = Vector(center)
        b.cylinder(c, r + 0.004, 0.02, axis, G["RIM"], segments=8, back_cap=False)
        b.disc(c + axis * 0.0105, r, axis, G["GRILLE"], segments=8)

    def canister(b, x, z, r=0.03, depth=0.04, out=None):
        """Filter: bayonet ring, canister and perforated end cap."""
        out = Vector(out or (math.copysign(0.55, x), -0.8, -0.25)).normalized()
        base = Vector((x, front_y(x, z), z))
        b.cylinder(base + out * 0.007, r * 0.8, 0.014, out, G["RIM"], segments=6, back_cap=False)
        b.cylinder(base + out * (0.014 + depth / 2), r, depth, out, G["FILTER"], segments=8, back_cap=False)
        b.disc(base + out * (0.0145 + depth), r * 0.95, out, G["FILTER_CAP"], segments=8)

    # --- Masks (new slot) ----------------------------------------------------------------------
    # Budget: below the whole body mesh (1050 vertices in Unity); single-sided shells, 6-8 sided
    # cylinders without hidden back caps, discs for grilles/caps and flat ribbons for straps.
    def gas_mask(b):  # GP-5 / M40 look of the inventory portraits: twin rimmed lenses, voicemitter, cheek filter
        mask_shell(b, ez - 0.115, ez + 0.035, G["RUBBER"], segs=(12, 10), thickness=0.0)
        for x in (-0.034, 0.034):
            lens(b, x, ez, 0.02)
        vz = ez - 0.058
        voicemitter(b, (0, front_y(0, vz) - 0.008, vz), (0, -1, -0.15))
        ez2 = ez - 0.095
        b.cube((0, front_y(0, ez2) - 0.006, ez2), (0.03, 0.016, 0.018), G["SEAL"])  # exhale valve cover
        b.disc((0, front_y(0, ez2) - 0.0145, ez2), 0.008, (0, -1, 0), G["GRILLE"], segments=6)
        canister(b, 0.05, ez - 0.07)
        harness(b)

    def gas_mask_dual(b):  # M50 look: panoramic visor with frame, twin bayonet filters, side voicemitter, drink tube
        visor_z = (ez - 0.03, ez + 0.03)

        def zones(c):  # visor and its rubber frame are painted on the shell faces (no extra geometry)
            if visor_z[0] < c.z < visor_z[1] and abs(c.x) < hr.x * 0.9:
                return G["LENS"]
            if visor_z[0] - 0.02 < c.z < visor_z[1] + 0.02 and abs(c.x) < hr.x * 1.0:
                return G["SEAL"]
            return G["RUBBER"]
        radii = shell_r
        b.shell(hc, radii, lambda c: c.y < hc.y - 0.2 * hr.y and ez - 0.115 < c.z < ez + 0.06,
                G["RUBBER"], thickness=0.0, u=14, v=14, rect_fn=zones)
        vz = ez - 0.068
        voicemitter(b, (0, front_y(0, vz) - 0.008, vz), (0, -1, -0.1), r=0.015)
        voicemitter(b, (-0.062, front_y(-0.062, vz) + 0.004, vz), (-0.8, -0.6, 0), r=0.011)  # side voicemitter (right)
        canister(b, 0.05, ez - 0.078, r=0.027, depth=0.034, out=(0.7, -0.65, -0.25))
        canister(b, -0.05, ez - 0.078, r=0.027, depth=0.034, out=(-0.7, -0.65, -0.25))
        nozzle = Vector((0.022, front_y(0.022, ez - 0.1) - 0.006, ez - 0.1))
        b.cylinder(nozzle, 0.006, 0.02, (0.3, -1, -0.4), G["METAL"], segments=6, back_cap=False)  # drink coupling
        tube_end = Vector((0.07, front_y(0.07, ez - 0.13) + 0.01, ez - 0.135))
        d = tube_end - nozzle
        b.cylinder((nozzle + tube_end) / 2, 0.005, d.length, d, G["RUBBER"], segments=4, caps=False)
        harness(b, sides=((0.78, -0.5, 0.2), (0.72, -0.55, -0.5)))

    def respirator(b):  # 3M-style half face: moulded body, central valve, pancake filters, head + neck straps
        mask_shell(b, ez - 0.118, ez - 0.03, G["RESP_BODY"], scale=0.99, segs=(12, 10), thickness=0.0)
        b.cube((0, front_y(0, ez - 0.034) - 0.004, ez - 0.034), (0.03, 0.012, 0.008), G["SEAL"])  # nose bridge
        vz = ez - 0.082
        b.cube((0, front_y(0, vz) - 0.011, vz), (0.034, 0.02, 0.032), G["RESP_BODY"])  # exhalation valve housing
        b.disc((0, front_y(0, vz) - 0.0215, vz), 0.011, (0, -1, 0), G["GRILLE"], segments=6)
        for sx in (-1, 1):
            x, z = sx * 0.046, ez - 0.072
            out = Vector((sx * 0.65, -0.75, -0.1)).normalized()
            base = Vector((x, front_y(x, z), z))
            b.cylinder(base + out * 0.006, 0.017, 0.012, out, G["RESP_BODY"], segments=6, back_cap=False)  # bayonet
            b.cylinder(base + out * 0.018, 0.034, 0.012, out, G["P100"], segments=8, back_cap=False)  # pancake filter
        for sx in (-1, 1):
            strap(b, [(sx * 0.82, -0.45, -0.25), (sx * 0.7, 0.25, 0.55), (0, 0.55, 0.85)], width=0.014)  # head cradle
            strap(b, [(sx * 0.78, -0.45, -0.55), (sx * 0.85, 0.35, -0.75), (0, 0.95, -0.7)], width=0.012)  # neck strap
        plate(b, (0, 0.95, -0.7), 0.012, G["METAL"])  # neck buckle
        plate(b, (0, 0.55, 0.85), 0.03, G["STRAP"])  # crown cradle
    def skull(b):  # lower-face skull mask (painted in the SKULL rect)
        b.shell(hc, (shell_r[0] * 0.98, shell_r[1] * 0.98, shell_r[2]),
                lambda c: c.y < hc.y - 0.25 * hr.y and ez - 0.115 < c.z < ez - 0.01, G["SKULL"], thickness=0.004)
        mask_strap(b)

    def shemagh(b):  # scarf over the lower face and neck
        zs = [ez - 0.035, ez - 0.075, ez - 0.12, ez - 0.17, ez - 0.215]
        rings = []
        cy = None
        for z in zs:
            c, radii = body.ring_fit(z - 0.025, z + 0.025, pad=0.016, max_x=0.14)
            cy = c if cy is None else cy
            rings.append(radii)
        b.ring(zs, rings, cy, G["SHEMAGH"], sides=12, thickness=0.006)
        b.cube((0.0, cy + max(rings[-1]) * 0.85, zs[-1] - 0.03), (0.07, 0.015, 0.09), G["SHEMAGH"])  # knot

    build("Mask_GasMask", "Mask", gas_mask, [gear])
    build("Mask_GasMaskDual", "Mask", gas_mask_dual, [gear])
    build("Mask_Respirator", "Mask", respirator, [gear])
    build("Mask_Skull", "Mask", skull, [gear])
    build("Mask_Shemagh", "Mask", shemagh, [gear])

    # --- Helmets -------------------------------------------------------------------------------
    crown_r = max(hr.x, hr.y) * 1.1
    top = body.hhi.z + 0.015

    def boonie(b):
        z_brim = hc.z + 0.25 * hr.z
        b.cylinder((0, hc.y, (z_brim + top) / 2), crown_r, top - z_brim, (0, 0, 1), helmet_rect, segments=10, radius2=crown_r * 0.86)
        inner = [crown_r] * 10
        outer = [crown_r * 1.7] * 10
        b.ring([z_brim, z_brim - 0.025], [inner, outer], hc.y, helmet_rect, sides=10, thickness=0.004)

    def head_band(b, z0, z1, rect, pad):
        """Band hugging the head between z0 and z1 (radius measured on the body at each height)."""
        cy0, r0 = body.ring_fit(z0 - 0.015, z0 + 0.015, sides=10, pad=pad, max_x=0.14)
        cy1, r1 = body.ring_fit(z1 - 0.015, z1 + 0.015, sides=10, pad=pad, max_x=0.14)
        b.ring([z0, z1], [r0, r1], (cy0 + cy1) / 2, rect, sides=10, thickness=0.006)

    def beanie(b):
        b.shell(hc + Vector((0, 0.004, 0.01)), (hr.x * 1.09, hr.y * 1.09, hr.z * 1.06),
                lambda c: c.z > hc.z + 0.12 * hr.z, G["CLOTH_BLACK"], thickness=0.006)
        head_band(b, hc.z + 0.12 * hr.z, hc.z + 0.32 * hr.z, G["CLOTH_BLACK"], 0.014)  # folded cuff

    def beret(b):
        tilt = Matrix.Rotation(math.radians(-14), 4, "Y")
        verts = b.shell(Vector((0, 0, 0)), (hr.x * 1.25, hr.y * 1.12, 0.04), lambda c: True, G["BERET"], thickness=0.0)
        for v in verts:
            v.co = (tilt @ v.co.to_4d()).to_3d() + Vector((-0.02, hc.y + 0.005, body.hhi.z - 0.02))
        head_band(b, body.hhi.z - 0.06, body.hhi.z - 0.035, G["CLOTH_BLACK"], 0.006)

    def headset(b):
        for side in (-1, 1):
            x = side * (hr.x * 1.08)
            b.cylinder((x, hc.y + 0.005, hc.z - 0.15 * hr.z), 0.035, 0.03, (1, 0, 0), G["POLYMER"], segments=10)
        steps = 8
        for i in range(steps):
            a0, a1 = math.pi * i / steps, math.pi * (i + 1) / steps
            p0 = Vector((math.cos(a0) * hr.x * 1.1, hc.y + 0.005, hc.z - 0.1 * hr.z + math.sin(a0) * (top - hc.z + 0.1 * hr.z)))
            p1 = Vector((math.cos(a1) * hr.x * 1.1, hc.y + 0.005, hc.z - 0.1 * hr.z + math.sin(a1) * (top - hc.z + 0.1 * hr.z)))
            d = p1 - p0
            rot = Vector((1, 0, 0)).rotation_difference(d.normalized()).to_matrix().to_4x4()
            b.cube((p0 + p1) / 2, (d.length + 0.004, 0.02, 0.008), G["CLOTH_BLACK"], rot=rot)
        mic = Vector((-hr.x * 1.08, hc.y - 0.05, ez - 0.07))
        b.cube(mic, (0.008, 0.09, 0.008), G["METAL"], rot=Matrix.Rotation(math.radians(-20), 4, "Z"))

    helmet = bpy.data.objects["Helmet_Default"]
    helmet.data.calc_loop_triangles()
    helmet_tree = BVHTree.FromPolygons([helmet.matrix_world @ v.co for v in helmet.data.vertices],
                                       [tuple(t.vertices) for t in helmet.data.loop_triangles])

    def helmet_extent_y(z, front=True):
        """Front (min y) or back (max y) surface of the helmet at height z on the center line (raycast:
        the helmet is too low-poly to measure with vertices)."""
        origin = Vector((0, -1.0 if front else 1.0, z))
        hit, _n, _i, _d = helmet_tree.ray_cast(origin, Vector((0, 1.0 if front else -1.0, 0)))
        return hit.y if hit else (hc.y - hr.y if front else hc.y + hr.y)

    def fast_nvg(b):
        hlo, hhi = sv.mesh_bounds(bpy.data.objects["Helmet_Default"])
        zm = hhi.z - 0.075
        fy = helmet_extent_y(zm)
        b.cube((0, fy - 0.006, zm), (0.05, 0.016, 0.035), G["POLYMER"], mat=1)  # shroud
        b.cube((0, fy - 0.03, zm + 0.01), (0.03, 0.04, 0.02), G["METAL"], mat=1)  # arm
        axis = Vector((0, -0.35, 1)).normalized()  # flipped-up binocular tubes, resting on the helmet front
        for x in (-0.024, 0.024):
            base = Vector((x, fy - 0.045, zm + 0.005))
            b.cylinder(base + axis * 0.025, 0.014, 0.05, axis, G["METAL"], mat=1)
            b.cylinder(base + axis * 0.052, 0.013, 0.006, axis, G["NVG_LENS"], mat=1)
        zb = zm - 0.02
        b.cube((0, helmet_extent_y(zb, front=False) + 0.008, zb), (0.06, 0.02, 0.04), G["RADIO"], mat=1)  # counterweight

    build("Helmet_Boonie", "Helmet", boonie, [soldier])
    build("Helmet_Beanie", "Helmet", beanie, [gear])
    build("Helmet_Beret", "Helmet", beret, [gear])
    build("Helmet_Headset", "Helmet", headset, [gear])
    build("Helmet_FastNVG", "Helmet", fast_nvg, [soldier, gear], base=bpy.data.objects["Helmet_Default"].data)  # keeps the helmet UVs

    # --- Glasses -------------------------------------------------------------------------------
    fy_eye = body.face_y(ez, 0.06)

    def nvg_quad(b):  # GPNVG-style, flipped down over the eyes
        b.cube((0, fy_eye - 0.03, ez + 0.03), (0.11, 0.03, 0.03), G["METAL"])
        for x, yaw in ((-0.05, -12), (-0.018, 0), (0.018, 0), (0.05, 12)):
            d = Matrix.Rotation(math.radians(yaw), 3, "Z") @ Vector((0, -1, 0))
            c = Vector((x, fy_eye - 0.05, ez + 0.005))
            b.cylinder(c, 0.013, 0.05, d, G["METAL"])
            b.cylinder(c + d * 0.027, 0.012, 0.005, d, G["NVG_LENS"])
        b.cube((0, fy_eye - 0.02, ez + 0.065), (0.02, 0.02, 0.05), G["POLYMER"])  # mount arm

    def nvg_mono(b):  # PVS-14 over the right eye (-X)
        c = Vector((-0.034, fy_eye - 0.045, ez))
        b.cylinder(c, 0.017, 0.06, (0, -1, 0), G["METAL"])
        b.cylinder(c + Vector((0, -0.032, 0)), 0.016, 0.005, (0, -1, 0), G["NVG_LENS"])
        b.cube((-0.02, fy_eye - 0.03, ez + 0.05), (0.02, 0.02, 0.07), G["POLYMER"])

    build("Glasses_NVGQuad", "Glasses", nvg_quad, [gear])
    build("Glasses_NVGMono", "Glasses", nvg_mono, [gear])

    # --- Backpacks -----------------------------------------------------------------------------
    plo, phi = sv.mesh_bounds(bpy.data.objects["Backpack_Default"])

    def radio(b):
        cx, zc = (plo.x + phi.x) / 2, (plo.z + phi.z) / 2 + 0.02
        b.cube((cx, plo.y + 0.045, zc), (0.17, 0.08, 0.24), G["RADIO"])
        b.cube((cx, plo.y + 0.03, zc - 0.17), (0.15, 0.06, 0.09), G["CLOTH_BLACK"])
        b.cylinder((cx + 0.065, plo.y + 0.06, zc + 0.12 + 0.22), 0.004, 0.44, (0, 0.15, 1), G["METAL"], segments=6)

    build("Backpack_Radio", "Backpack", radio, [gear])
    if "Backpack_Hydration" not in existing:
        sv._scaled_copy("Backpack_Default", "Hydration", (0.8, 0.45, 0.95), Vector(((plo.x + phi.x) / 2, plo.y, phi.z)))

    # --- Vests ---------------------------------------------------------------------------------
    vlo, vhi = sv.mesh_bounds(bpy.data.objects["Vest_Default"])

    def plate_carrier(b):
        z = vlo.z + 0.12
        cy, radii = body.ring_fit(z - 0.03, z + 0.03, pad=0.0)
        fy = min(vlo.y, cy - _sample_radius(radii, -math.pi / 2)) - 0.015
        for x in (-0.07, 0.0, 0.07):
            b.cube((x, fy - 0.02, z), (0.06, 0.035, 0.1), vest_rect)

    def chest_rig(b):
        z = (vlo.z + vhi.z) / 2 + 0.02
        cy, radii = body.ring_fit(z - 0.04, z + 0.04, pad=0.0)
        fy = cy - _sample_radius(radii, -math.pi / 2)
        b.cube((0, fy - 0.012, z), (0.26, 0.024, 0.13), vest_rect)  # panel
        for x in (-0.09, -0.03, 0.03, 0.09):
            b.cube((x, fy - 0.04, z + 0.01), (0.05, 0.035, 0.1), vest_rect)
        zs = vhi.z - 0.04
        for x in (-0.09, 0.09):  # shoulder straps over to the back, measured at the strap height
            ys = [c.y for c in body.co if abs(c.z - zs) < 0.03 and abs(c.x - x) < 0.04]
            y0, y1 = (min(ys), max(ys)) if ys else (cy - radii[9], cy + radii[3])
            b.cube((x, (y0 + y1) / 2, zs), (0.04, (y1 - y0) + 0.012, 0.015), vest_rect)
        b.ring([z - 0.075, z - 0.055], [radii, radii], cy, vest_rect, sides=12, thickness=0.004)  # back strap

    def bandolier(b):  # shotgun shells across the chest, left shoulder -> right hip
        p0 = Vector((0.13, 0, vhi.z - 0.03))
        p1 = Vector((-0.15, 0, vlo.z - 0.02))
        steps = 9
        for i in range(steps):
            t0, t1 = i / steps, (i + 1) / steps
            q0, q1 = p0.lerp(p1, t0), p0.lerp(p1, t1)
            for q in (q0, q1):
                cy, radii = body.ring_fit(q.z - 0.03, q.z + 0.03, pad=0.012)
                a = math.atan2(-1.0, q.x / max(radii[9], 0.05))
                q.y = cy + math.sin(a) * _sample_radius(radii, a)
            d = q1 - q0
            rot = Vector((0, 0, 1)).rotation_difference(d.normalized()).to_matrix().to_4x4()
            mid = (q0 + q1) / 2
            b.cube(mid, (0.05, 0.008, d.length + 0.004), G["CLOTH_BLACK"], rot=rot)
            b.cube(mid + Vector((0, -0.01, 0)), (0.04, 0.014, 0.018), G["BERET"], rot=rot)  # red shells

    build("Vest_PlateCarrier", "Vest", plate_carrier, [soldier], base=bpy.data.objects["Vest_Default"].data)
    build("Vest_ChestRig", "Vest", chest_rig, [soldier])
    build("Vest_Bandolier", "Vest", bandolier, [gear])

    make_more(sv, locals())


def make_more(sv, ctx):
    """Second wave: head-hugging hoods (no clipping, open at the bottom) and more gear per slot.
    Low poly (single-sided surfaces, 6-8 sided cylinders, discs, ribbons) and only existing palettes:
    Gear_Atlas rects or camo-able soldier atlas rects."""
    from soldier_gear_layout import SOLDIER_RECTS
    existing, body, gear, soldier = ctx["existing"], ctx["body"], ctx["gear"], ctx["soldier"]
    build, on_head, strap, plate = ctx["build"], ctx["on_head"], ctx["strap"], ctx["plate"]
    body_tree, front_y, G = ctx["body_tree"], ctx["front_y"], ctx["G"]
    helmet_rect, vest_rect = ctx["helmet_rect"], ctx["vest_rect"]
    hc, hr, ez = body.hc, body.hr, body.eye_z
    S = SOLDIER_RECTS
    bones = {b.name: b.head_local for b in sv.rig().data.bones}

    def sph(theta, phi):
        """Direction from polar angle theta (0 = up) and azimuth phi (0 = front, -Y)."""
        return Vector((math.sin(theta) * math.sin(phi), -math.sin(theta) * math.cos(phi), math.cos(theta)))

    def surf(x, z, front=True, offset=0.006):
        """Body surface point at (x, z), seen from the front or the back (raycast along Y)."""
        o = Vector((x, -1.0 if front else 1.0, z))
        hit, n, _i, _d = body_tree.ray_cast(o, Vector((0, 1.0 if front else -1.0, 0)))
        if hit is None:
            return Vector((x, hc.y, z))
        return hit + Vector((0, -offset if front else offset, 0))

    def torso_strap(b, pts, width, rect, mat=0):
        """Ribbon through explicit surface points (front/back of the torso)."""
        sides = []
        for i, p in enumerate(pts):
            t = pts[min(i + 1, len(pts) - 1)] - pts[max(i - 1, 0)]
            out = Vector((p.x * 0.3, p.y - hc.y, 0)).normalized() if abs(p.y - hc.y) > 1e-4 else Vector((1, 0, 0))
            sides.append(out.cross(t).normalized() * (width / 2))
        b.ribbon(pts, sides, rect, mat, outward_from=Vector((0, hc.y, sum(p.z for p in pts) / len(pts))))

    # --- Hoods -------------------------------------------------------------------------------
    def hood_rows(offset, theta_max, lon=12, lat=6):
        """Rings around the head following its real surface (raycast), from the crown down to theta_max."""
        rows = []
        for i in range(1, lat + 1):
            theta = math.radians(18) + (theta_max - math.radians(18)) * (i - 1) / (lat - 1)
            rows.append([on_head(sph(theta, 2 * math.pi * j / lon), offset) for j in range(lon)])
        return rows

    def face_open(c, half_angle, z_lo, z_hi):
        d = Vector((c.x, c.y - hc.y, 0))
        ang = math.degrees(math.acos(max(-1.0, min(1.0, -d.y / max(d.length, 1e-6)))))
        return ang < half_angle and z_lo < c.z < z_hi

    def hood(b):  # snug hood: follows the head surface, face opening, open below the jaw
        rows = hood_rows(0.016, math.radians(128), lat=7)  # down the sides of the face to the neck
        top = on_head((0, 0, 1), 0.016)
        b.grid(rows, lambda c: not face_open(c, 40, ez - 0.12, ez + 0.05), S["CAMO_CLOTH"],
               outward_from=hc, pole=top,
               rect_fn=lambda c: S["DARK_CLOTH"] if face_open(c, 58, ez - 0.13, ez + 0.07) else S["CAMO_CLOTH"])

    def parka(b):  # like the reference: pointed crown, thick rim around the face, drape open on the shoulders
        rows = hood_rows(0.022, math.radians(118), lon=12, lat=6)
        # Drape: two flared rings hanging from the last head ring down onto the shoulders.
        last = rows[-1]
        for k, (down, flare) in enumerate(((0.07, 0.035), (0.13, 0.075))):
            ring = []
            for p in last:
                out = Vector((p.x, p.y - hc.y, 0))
                out = out.normalized() if out.length > 1e-5 else Vector((1, 0, 0))
                ring.append(p + out * flare - Vector((0, 0, down)))
            rows.append(ring)
        top = on_head((0, 0.15, 1), 0.022) + Vector((0, 0.01, 0.022))  # small peak, slightly back like the drawing
        chin_z = ez - 0.13

        def keep(c):
            if face_open(c, 50, chin_z, ez + 0.07):
                return False
            if c.z < chin_z and face_open(c, 32, -10, chin_z):  # the front stays open all the way down
                return False
            return True
        rim = lambda c: S["DARK_CLOTH"] if (face_open(c, 68, chin_z - 0.01, ez + 0.09) or
                                            (c.z < chin_z and face_open(c, 46, -10, chin_z))) else S["CAMO_CLOTH"]
        b.grid(rows, keep, S["CAMO_CLOTH"], outward_from=hc, pole=top, rect_fn=rim)

    if "Helmet_Hood" not in existing:
        build("Helmet_Hood", "Helmet", hood, [soldier])
    build("Helmet_Parka", "Helmet", parka, [soldier])

    def hoodie(b):  # sweatshirt hood: loose and rounded (roomier at the back/top), joined to the collar
        lon, lat = 12, 6
        theta_max = math.radians(120)
        rows = []
        for i in range(1, lat + 1):
            theta = math.radians(16) + (theta_max - math.radians(16)) * (i - 1) / (lat - 1)
            row = []
            for j in range(lon):
                d = sph(theta, 2 * math.pi * j / lon)
                off = 0.022 + 0.022 * max(0.0, d.y) + 0.01 * max(0.0, d.z)  # baggy at the back and crown
                row.append(on_head(d, off))
            rows.append(row)
        # Collar: the hood's lower edge meets a ring around the neck base (sweatshirt neckline).
        z_col = bones["neck"].z - 0.01
        cy, radii = body.ring_fit(z_col - 0.02, z_col + 0.02, sides=lon, pad=0.02, max_x=0.16)
        collar = []
        for j in range(lon):
            a = 2 * math.pi * j / lon - math.pi / 2  # ring angles start at +X; our phi starts at the front (-Y)
            r = _sample_radius(radii, a)
            collar.append(Vector((math.cos(a) * r, cy + math.sin(a) * r, z_col)))
        rows.append(collar)
        top = on_head((0, 0.1, 1), 0.03)
        chin_z = ez - 0.11

        def keep(c):
            if face_open(c, 46, chin_z, ez + 0.07):
                return False
            return not (c.z < chin_z + 0.01 and face_open(c, 22, -10, chin_z + 0.01))  # small V at the throat
        rim = lambda c: S["DARK_CLOTH"] if face_open(c, 62, chin_z - 0.02, ez + 0.09) else S["CAMO_CLOTH"]
        b.grid(rows, keep, S["CAMO_CLOTH"], outward_from=hc, pole=top, rect_fn=rim)
        for sx in (-1, 1):  # drawstrings hanging from the opening
            p0 = on_head(sph(math.radians(112), sx * math.radians(42)), 0.026)
            p1 = p0 + Vector((sx * 0.004, -0.004, -0.075))
            d = p1 - p0
            b.cylinder((p0 + p1) / 2, 0.003, d.length, d, S["DARK_CLOTH"], segments=4, caps=False)
            b.cylinder(p1, 0.005, 0.012, d, G["METAL"], mat=1, segments=4)  # aglet

    build("Helmet_Hoodie", "Helmet", hoodie, [soldier, gear])

    # --- Helmets -------------------------------------------------------------------------------
    brow_z = ez + 0.045

    def cap(b):  # patrol cap: crown on the head surface + curved bill
        rows = hood_rows(0.012, math.radians(70), lon=12, lat=4)
        b.grid(rows, lambda c: True, S["CAMO_CLOTH"], outward_from=hc, pole=on_head((0, 0, 1), 0.012))
        base = rows[-1]
        front = [p for p in base if p.y < hc.y - hr.y * 0.3]
        front.sort(key=lambda p: p.x)
        outer = [p + Vector((p.x * 0.25, -0.065, -0.012)) for p in front]
        b.grid([front, outer], lambda c: True, S["DARK_CLOTH"], outward_from=hc + Vector((0, 0, -1)), closed=False)
        b.disc(on_head((0, 0, 1), 0.016), 0.008, (0, 0, 1), S["DARK_CLOTH"], segments=6)

    def pasgt(b):  # PASGT "fritz": rounded shell with flared rim, cover band and chin strap
        rows = hood_rows(0.03, math.radians(98), lon=12, lat=5)
        last = rows[-1]
        flare = []
        for p in last:
            out = Vector((p.x, p.y - hc.y, 0)).normalized()
            dz = -0.012 if out.y > 0.3 else 0.004  # longer at the back, like the real one
            flare.append(p + out * 0.02 + Vector((0, 0, dz)))
        rows.append(flare)
        b.grid(rows, lambda c: True, helmet_rect, outward_from=hc, pole=on_head((0, 0, 1), 0.03))
        band = [Vector(p) + (Vector(p) - hc).normalized() * 0.003 for p in rows[2]]
        band2 = [Vector(p) + (Vector(p) - hc).normalized() * 0.003 for p in rows[3]]
        b.grid([band, band2], lambda c: True, G["STRAP"], mat=1, outward_from=hc)  # cover band
        for sx in (-1, 1):
            strap(b, [(sx * 0.95, 0.05, -0.05), (sx * 0.8, -0.2, -0.6), (0, -0.55, -0.85)], width=0.012, mat=1)  # chin strap

    build("Helmet_Cap", "Helmet", cap, [soldier])
    build("Helmet_PASGT", "Helmet", pasgt, [soldier, gear])

    # --- Glasses -------------------------------------------------------------------------------
    def aviator(b):
        for sx in (-1, 1):
            x = sx * 0.034
            c = surf(x, ez - 0.004, True, 0.009)  # resting on the real face, not on the mask shell
            b.cylinder(c, 0.022, 0.004, (0, -1, 0), G["METAL"], segments=8, caps=False)  # frame ring
            verts = b.disc(c + Vector((0, -0.001, 0)), 0.021, (0, -1, 0), G["LENS"], segments=8)
            for v in verts:  # teardrop: lower outer corner drops
                if v.co.z < c.z:
                    v.co.z -= 0.006 * (1 + sx * (v.co.x - c.x) / 0.021)
            strap(b, [(sx * 0.55, -0.85, 0.02), (sx * 0.98, -0.1, 0.05), (sx * 0.9, 0.35, 0.0)], width=0.004, rect=G["METAL"])
        b.cube(surf(0, ez + 0.008, True, 0.01), (0.028, 0.004, 0.004), G["METAL"])  # bridge over the nose

    def tactical(b):  # wraparound ballistic goggles with frame and strap
        r = (ctx["shell_r"][0] * 1.06, ctx["shell_r"][1] * 1.06, ctx["shell_r"][2])
        b.shell(hc, r, lambda c: c.y < hc.y - 0.35 * hr.y and ez - 0.024 < c.z < ez + 0.022,
                G["LENS"], thickness=0.0, u=16, v=24,
                rect_fn=lambda c: G["POLYMER"] if abs(c.z - ez) > 0.015 or abs(c.x) > hr.x * 0.85 else G["LENS"])
        strap(b, [(0.9, -0.3, 0.05), (0.7, 0.7, 0.05), (0, 1, 0.05), (-0.7, 0.7, 0.05), (-0.9, -0.3, 0.05)], width=0.014)

    build("Glasses_Aviator", "Glasses", aviator, [gear])
    build("Glasses_Tactical", "Glasses", tactical, [gear])

    # --- Backpacks -----------------------------------------------------------------------------
    plo, phi = sv.mesh_bounds(bpy.data.objects["Backpack_Default"])
    cx, zc = (plo.x + phi.x) / 2, (plo.z + phi.z) / 2

    def back_y(x, z):
        return surf(x, z, front=False, offset=0.004).y

    def assault(b):  # MOLLE assault pack: body, lid, side pouches, compression straps, bedroll
        y0 = back_y(cx, zc)
        b.cube((cx, y0 + 0.06, zc), (0.26, 0.12, 0.34), vest_rect)
        b.cube((cx, y0 + 0.065, zc + 0.185), (0.27, 0.13, 0.05), vest_rect)
        for sx in (-1, 1):
            b.cube((cx + sx * 0.15, y0 + 0.07, zc - 0.03), (0.05, 0.09, 0.16), vest_rect)
            b.cube((cx + sx * 0.07, y0 + 0.122, zc), (0.025, 0.006, 0.34), G["STRAP"], mat=1)
        b.cylinder((cx, y0 + 0.07, zc - 0.21), 0.045, 0.3, (1, 0, 0), S["CAMO_CLOTH"], segments=8)

    def medic(b):  # medic pack with maroon cross patch and side pouch
        y0 = back_y(cx, zc)
        b.cube((cx, y0 + 0.055, zc), (0.25, 0.11, 0.3), vest_rect)
        b.disc((cx, y0 + 0.112, zc + 0.03), 0.06, (0, 1, 0), G["MEDIC"], mat=1, segments=4)
        b.cube((cx + 0.15, y0 + 0.06, zc - 0.05), (0.05, 0.08, 0.13), vest_rect)
        b.cube((cx, y0 + 0.06, zc + 0.17), (0.2, 0.09, 0.04), G["CLOTH_BLACK"], mat=1)

    def tanks(b):  # twin air tanks on a back plate, valves and a hose over the right shoulder
        y0 = back_y(cx, zc)
        b.cube((cx, y0 + 0.008, zc), (0.2, 0.016, 0.34), G["CLOTH_BLACK"])
        for sx in (-1, 1):
            c = Vector((cx + sx * 0.055, y0 + 0.06, zc - 0.01))
            b.cylinder(c, 0.045, 0.36, (0, 0, 1), G["FILTER"], segments=8)
            b.cylinder(c + Vector((0, 0, 0.2)), 0.014, 0.04, (0, 0, 1), G["METAL"], segments=6, back_cap=False)
        hose0 = Vector((cx - 0.055, y0 + 0.06, zc + 0.22))
        hose1 = surf(-0.1, bones["neck"].z - 0.02, front=False, offset=0.02)
        hose2 = surf(-0.06, ez - 0.1, front=True, offset=0.03)
        for p0, p1 in ((hose0, hose1), (hose1, hose2)):
            d = p1 - p0
            b.cylinder((p0 + p1) / 2, 0.008, d.length, d, G["RUBBER"], segments=4, caps=False)

    build("Backpack_Assault", "Backpack", assault, [soldier, gear])
    build("Backpack_Medic", "Backpack", medic, [soldier, gear])
    build("Backpack_Tanks", "Backpack", tanks, [gear])

    # --- Vests ---------------------------------------------------------------------------------
    z_chest, z_belt = bones["spine.003"].z - 0.02, bones["spine.001"].z - 0.05
    z_shoulder = bones["neck"].z - 0.03

    def suspenders(b, x, back_cross, rect, mat):
        top = Vector((x, hc.y, z_shoulder + 0.005))
        hit, _n, _i, _d = body_tree.ray_cast(Vector((x, hc.y, 2.5)), Vector((0, 0, -1)))
        if hit is not None:
            top = hit + Vector((0, 0, 0.006))
        front = [surf(x * 1.0, z, True) for z in (z_shoulder - 0.04, z_chest, z_belt + 0.06)]
        bx = -x if back_cross else x
        back = [surf((x + bx) / 2 if z < z_chest else x, z, False) for z in (z_shoulder - 0.04, z_chest, z_belt + 0.06)]
        torso_strap(b, front[::-1] + [top] + back, 0.035, rect, mat)

    def belt(b, rect, mat):
        cy, radii = body.ring_fit(z_belt - 0.02, z_belt + 0.02, pad=0.012)
        b.ring([z_belt - 0.022, z_belt + 0.022], [radii, radii], cy, rect, mat, sides=12, thickness=0.0)
        return cy, radii

    def webbing(b):  # LBE: suspenders (X at the back), belt, ammo pouches, canteen, first-aid pouch
        for sx in (-1, 1):
            suspenders(b, sx * 0.075, True, G["STRAP"], 1)
        cy, radii = belt(b, G["STRAP"], 1)
        for sx in (-1, 1):
            p = surf(sx * 0.075, z_belt + 0.01, True, 0.03)
            b.cube(p, (0.06, 0.04, 0.07), vest_rect)
            p = surf(sx * 0.13, z_belt - 0.005, False, 0.035)
            if sx > 0:
                b.cylinder(p, 0.035, 0.1, (0, 0, 1), G["FILTER"], mat=1, segments=8)  # canteen
            else:
                b.cube(p, (0.07, 0.05, 0.06), vest_rect)  # first aid

    def vest_tactical(b):  # slim vest panel with MOLLE rows, radio on the shoulder and a knife on the chest
        rows = []
        zs = [z_belt + 0.03, z_chest - 0.06, z_chest + 0.04]
        for z in zs:
            row = [surf(x, z, True, 0.012) for x in (-0.14, -0.07, 0.0, 0.07, 0.14)]
            rows.append(row)
        b.grid(rows, lambda c: True, vest_rect, closed=False, outward_from=Vector((0, hc.y + 0.2, z_chest)))
        for z in (z_chest - 0.09, z_chest - 0.04):
            pts = [surf(x, z, True, 0.016) for x in (-0.13, -0.065, 0.0, 0.065, 0.13)]
            sides = [Vector((0, 0, 0.006))] * len(pts)
            b.ribbon(pts, sides, G["STRAP"], 1, outward_from=Vector((0, hc.y + 0.2, z)))
        r = surf(0.1, z_chest + 0.03, True, 0.03)
        b.cube(r, (0.045, 0.03, 0.08), G["RADIO"], mat=1)
        b.cylinder(r + Vector((0.01, 0, 0.12)), 0.003, 0.16, (0, 0, 1), G["METAL"], mat=1, segments=4, caps=False)
        k = surf(-0.07, z_chest - 0.02, True, 0.026)
        b.cube(k, (0.025, 0.012, 0.12), G["CLOTH_BLACK"], mat=1, rot=Matrix.Rotation(math.radians(25), 4, "Y"))
        b.cube(k + Vector((0.02, 0, 0.07)), (0.012, 0.012, 0.035), G["METAL"], mat=1, rot=Matrix.Rotation(math.radians(25), 4, "Y"))
        for sx in (-1, 1):
            suspenders(b, sx * 0.09, False, G["STRAP"], 1)

    build("Vest_Webbing", "Vest", webbing, [soldier, gear])
    build("Vest_Tactical", "Vest", vest_tactical, [soldier, gear])

    # --- Masks ---------------------------------------------------------------------------------
    shell_r = ctx["shell_r"]

    def bandana(b):  # camo bandana over the nose, tip hanging below the chin, knot behind
        r = (shell_r[0] * 1.02, shell_r[1] * 1.04, shell_r[2] * 1.02)
        verts = b.shell(hc, r, lambda c: c.y < hc.y + 0.1 * hr.y and ez - 0.12 < c.z < ez - 0.025,
                        S["CAMO_CLOTH"], thickness=0.0, u=12, v=10)
        for v in verts:  # the front hangs lower in a point
            if v.co.y < hc.y - 0.5 * hr.y and v.co.z < ez - 0.09:
                v.co.z -= 0.035 * (1 - abs(v.co.x) / (hr.x * 0.8))
        knot = on_head((0, 1, -0.45), 0.012)
        b.cube(knot, (0.03, 0.02, 0.025), S["CAMO_CLOTH"])

    def ballistic(b):  # ballistic face mask: tan plate, eye holes, mouth vents, straps
        def keep(c):
            for sx in (-1, 1):
                if abs(c.x - sx * 0.036) < 0.03 and abs(c.z - (ez + 0.002)) < 0.022:
                    return False  # eye holes
            return c.y < hc.y - 0.3 * hr.y and abs(c.x) < hr.x * 0.9 and ez - 0.11 < c.z < ez + 0.055
        r = (shell_r[0] * 0.97, shell_r[1] * 1.03, shell_r[2])  # hugs the face, no side wings
        b.shell(hc, r, keep, G["POLYMER"], thickness=0.0, u=18, v=16)
        for x in (-0.012, 0.012):
            b.disc((x, front_y(x, ez - 0.075, r) - 0.002, ez - 0.075), 0.007, (0, -1, 0), G["GRILLE"], segments=6)
        for sx in (-1, 1):
            strap(b, [(sx * 0.85, -0.45, 0.25), (sx * 0.8, 0.5, 0.25), (0, 1, 0.25)], width=0.014)
            strap(b, [(sx * 0.85, -0.45, -0.45), (sx * 0.8, 0.5, -0.35), (0, 1, -0.3)], width=0.014)

    build("Mask_Bandana", "Mask", bandana, [soldier])
    build("Mask_Ballistic", "Mask", ballistic, [gear])
