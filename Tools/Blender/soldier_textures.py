"""
Soldier textures generator (Blender 5.2, numpy).

1) Camo atlases: recolors MarineTexture1.png (256x256 atlas of the soldier) with a palette per camo.
   The UV layout is preserved pixel by pixel: only colors change, so every mesh that uses the atlas
   (body, helmet, vest, pack and the variants mapped into those rects) keeps working. The face/
   balaclava (UVs of the body's head faces), the goggles lens and the red patches are left untouched.
2) Gear atlas (Gear_Atlas.png): new 256x256 texture for spec-ops accessories (gas masks, NVG, radio...).
   Its rects are the GEAR_RECTS shared with soldier_variants.py, so the generated meshes always fit.

    blender -b SoldierVariants.blend --python soldier_textures.py
"""
import os
import sys

import bpy
import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.append(HERE)
from soldier_gear_layout import GEAR_RECTS  # noqa: E402

UNITY = os.path.normpath(os.path.join(HERE, "..", "..", "Game", "CrimsonDraft"))
SOURCE_ATLAS = os.path.join(UNITY, "Assets", "Art", "Models", "CharactersFBX", "Marine Character", "MarineTexture1.png")
OUT_DIR = os.path.join(UNITY, "Assets", "Art", "Models", "CharactersFBX", "MarineRigged", "Variants", "Textures")

# Palettes dark -> light (sRGB 0..1). Spec-ops inspired.
CAMOS = {
    "Woodland":    [(0.09, 0.08, 0.06), (0.27, 0.20, 0.12), (0.27, 0.33, 0.19), (0.55, 0.52, 0.38)],
    "Desert":      [(0.33, 0.26, 0.18), (0.52, 0.42, 0.29), (0.69, 0.59, 0.43), (0.83, 0.76, 0.59)],
    "Urban":       [(0.10, 0.10, 0.11), (0.27, 0.28, 0.30), (0.47, 0.48, 0.50), (0.70, 0.71, 0.72)],
    "Arctic":      [(0.40, 0.42, 0.45), (0.64, 0.66, 0.69), (0.82, 0.84, 0.86), (0.95, 0.96, 0.97)],
    "RangerGreen": [(0.10, 0.11, 0.08), (0.18, 0.20, 0.14), (0.25, 0.28, 0.19), (0.34, 0.37, 0.26)],
    "Coyote":      [(0.25, 0.19, 0.12), (0.40, 0.31, 0.20), (0.52, 0.42, 0.28), (0.62, 0.52, 0.36)],
    "NightOps":    [(0.03, 0.04, 0.06), (0.08, 0.10, 0.15), (0.14, 0.17, 0.24), (0.25, 0.28, 0.37)],
}


# ---------------------------------------------------------------------------------------------
# Image helpers (Blender stores byte PNGs as sRGB values in .pixels, bottom row first)
# ---------------------------------------------------------------------------------------------
def load(path):
    img = bpy.data.images.load(path, check_existing=False)
    w, h = img.size
    return np.array(img.pixels[:], dtype=np.float32).reshape(h, w, 4)


def save(arr, path):
    h, w = arr.shape[:2]
    img = bpy.data.images.new(os.path.basename(path), w, h, alpha=True)
    img.pixels[:] = np.clip(arr, 0, 1).astype(np.float32).ravel()
    img.filepath_raw = path
    img.file_format = "PNG"
    img.save()
    bpy.data.images.remove(img)


def box_blur(a, r):
    """Separable box blur with edge clamp."""
    k = 2 * r + 1
    p = np.pad(a, ((r, r), (0, 0)), mode="edge")
    c = np.cumsum(np.vstack([np.zeros((1, p.shape[1])), p]), 0)
    a = (c[k:] - c[:-k]) / k
    p = np.pad(a, ((0, 0), (r, r)), mode="edge")
    c = np.cumsum(np.hstack([np.zeros((p.shape[0], 1)), p]), 1)
    return (c[:, k:] - c[:, :-k]) / k


def smoothstep(e0, e1, x):
    t = np.clip((x - e0) / (e1 - e0), 0, 1)
    return t * t * (3 - 2 * t)


# ---------------------------------------------------------------------------------------------
# Protected regions from UVs
# ---------------------------------------------------------------------------------------------
def raster_uv(obj, h, w, face_filter=None):
    me = obj.data
    me.calc_loop_triangles()
    uv = me.uv_layers.active.data
    m = np.zeros((h, w), bool)
    ys, xs = np.mgrid[0:h, 0:w]
    pts = np.stack([(xs + 0.5) / w, (ys + 0.5) / h], -1)
    for t in me.loop_triangles:
        if face_filter and not face_filter(t):
            continue
        a, b, c = (np.array(uv[l].uv[:]) for l in t.loops)
        lo = np.floor(np.minimum(np.minimum(a, b), c) * [w, h]).astype(int) - 1
        hi = np.ceil(np.maximum(np.maximum(a, b), c) * [w, h]).astype(int) + 2
        x0, y0 = np.clip(lo, 0, [w, h])
        x1, y1 = np.clip(hi, 0, [w, h])
        if x1 <= x0 or y1 <= y0:
            continue
        p = pts[y0:y1, x0:x1]
        v0, v1, v2 = b - a, c - a, p - a
        d00, d01, d11 = v0 @ v0, v0 @ v1, v1 @ v1
        den = d00 * d11 - d01 * d01
        if abs(den) < 1e-12:
            continue
        v = (d11 * (v2 @ v0) - d01 * (v2 @ v1)) / den
        u = (d00 * (v2 @ v1) - d01 * (v2 @ v0)) / den
        m[y0:y1, x0:x1] |= (v >= -0.02) & (u >= -0.02) & (v + u <= 1.04)
    return m


def protected_mask(h, w):
    body = bpy.data.objects["Marine"]
    head = body.vertex_groups["head"].index
    verts = body.data.vertices

    def is_head(t):
        return all(any(g.group == head and g.weight > 0.5 for g in verts[v].groups) for v in t.vertices)

    face = raster_uv(body, h, w, is_head)
    lens = raster_uv(bpy.data.objects["Glasses_Default"], h, w)
    grow = box_blur((face | lens).astype(np.float32), 1) > 0.01  # 1px margin against bleeding
    return grow


# ---------------------------------------------------------------------------------------------
# Camo
# ---------------------------------------------------------------------------------------------
def make_camo(src, keep, palette):
    rgb = src[..., :3]
    lum = rgb @ np.array([0.299, 0.587, 0.114], np.float32)
    mean = box_blur(lum, 7)
    var = np.maximum(box_blur(lum * lum, 7) - mean * mean, 0)
    n = (lum - mean) / (np.sqrt(var) + 0.04)
    # Pattern value: local contrast (multicam blobs + fabric) + a bit of absolute value.
    t = np.clip(0.5 + 0.28 * n + 0.6 * (lum - 0.35), 0, 1)
    # Soft posterize into the 4 palette bands (PSX look, keeps the pattern readable).
    pal = np.array(palette, np.float32)
    x = t * 3.0
    i = np.clip(np.floor(x), 0, 2).astype(int)
    f = smoothstep(0.35, 0.65, x - i)[..., None]
    camo = pal[i] * (1 - f) + pal[i + 1] * f
    # Keep fabric/stitch detail from the original.
    detail = (lum - box_blur(lum, 1))[..., None]
    camo = camo * (1 + 0.9 * detail)
    # Gloves, boots and straps (dark, flat areas) stay dark: only tinted with the darkest color.
    dark = smoothstep(0.22, 0.10, box_blur(lum, 6))[..., None]
    camo = camo * (1 - dark) + (rgb * 0.6 + pal[0] * 0.6) * dark
    out = src.copy()
    out[..., :3] = camo
    red = (rgb[..., 0] > 0.40) & (rgb[..., 0] > 1.8 * rgb[..., 1]) & (rgb[..., 0] > 1.8 * rgb[..., 2])
    k = keep | red
    out[k] = src[k]
    out[..., 3] = 1.0
    return out


# ---------------------------------------------------------------------------------------------
# Pattern camos: a procedural pattern (new shapes, not only new colors) painted in UV space and
# multiplied by the original atlas shading, so folds, pouches, straps and stitching still read.
# ---------------------------------------------------------------------------------------------
def box_blur2(a, ry, rx):
    """Anisotropic box blur (ry rows, rx columns)."""
    if ry:
        k = 2 * ry + 1
        p = np.pad(a, ((ry, ry), (0, 0)), mode="wrap")
        c = np.cumsum(np.vstack([np.zeros((1, p.shape[1])), p]), 0)
        a = (c[k:] - c[:-k]) / k
    if rx:
        k = 2 * rx + 1
        p = np.pad(a, ((0, 0), (rx, rx)), mode="wrap")
        c = np.cumsum(np.hstack([np.zeros((p.shape[0], 1)), p]), 1)
        a = (c[:, k:] - c[:, :-k]) / k
    return a


def blob(rng, h, w, ry, rx=None, passes=2):
    """Smooth value noise (z-scored). Bigger radius = bigger blobs; rx != ry stretches them."""
    n = rng.random((h, w))
    for _ in range(passes):
        n = box_blur2(n, ry, ry if rx is None else rx)
    return (n - n.mean()) / (n.std() + 1e-6)


def layered(rng, h, w, layers):
    """M81-style: each (radius, rx, threshold) layer paints color index i+1 over the previous ones."""
    idx = np.zeros((h, w), int)
    for i, (r, rx, thr) in enumerate(layers):
        idx[blob(rng, h, w, r, rx) > thr] = i + 1
    return idx


def pat_woodland(rng, h, w):
    return layered(rng, h, w, [(9, 13, -0.15), (7, 10, 0.45), (5, 8, 1.0)])


def pat_tiger(rng, h, w):
    stripes = blob(rng, h, w, 1, 16, passes=2) + 0.7 * blob(rng, h, w, 10)
    body = blob(rng, h, w, 8, 12)
    idx = np.where(body > 0.1, 1, 0)
    idx[stripes > 0.55] = 2
    idx[stripes > 1.05] = 3
    return idx


def pat_digital(rng, h, w):
    """MARPAT/CADPAT-like: 4px squares with 2px breakup on the edges."""
    def low(div, r):
        n = blob(rng, h // div, w // div, r)
        return np.repeat(np.repeat(n, div, 0), div, 1)[:h, :w]
    n = low(4, 3) + 0.45 * low(2, 2)
    q = np.quantile(n, [0.35, 0.65, 0.88])
    return np.digitize(n, q)


def pat_flecktarn(rng, h, w):
    idx = np.zeros((h, w), int)
    density = blob(rng, h, w, 10)
    for color, p in ((1, 0.030), (2, 0.024), (3, 0.016), (4, 0.006)):
        cluster = blob(rng, h, w, 7)
        pts = (rng.random((h, w)) < p * np.clip(1 + 0.8 * density + 0.8 * cluster, 0, 3)).astype(np.float32)
        dots = box_blur2(pts, 1, 1) > 0.09
        idx[dots] = color
    return idx


def pat_splinter(rng, h, w):
    """Splittertarn: angular Voronoi patches + vertical 'rain' dashes."""
    n_seeds = 70
    sy, sx = rng.random(n_seeds) * h, rng.random(n_seeds) * w
    cols = rng.choice([0, 1, 2], n_seeds, p=[0.45, 0.35, 0.2])
    ys, xs = np.mgrid[0:h, 0:w]
    d = (ys[..., None] - sy) ** 2 + ((xs[..., None] - sx) * 0.6) ** 2  # slanted, elongated cells
    idx = cols[np.argmin(d, -1)]
    rain = (xs % 4 == 0) & (box_blur2((rng.random((h, w)) < 0.12).astype(np.float32), 3, 0) > 0.1)
    idx = np.where(rain, 3, idx)
    return idx


def pat_hex(rng, h, w):
    """Kryptek-like: hexagonal scales in two sizes over a soft base."""
    ys, xs = np.mgrid[0:h, 0:w].astype(np.float32)

    def hex_layer(size, r, thr):
        q = (np.sqrt(3) / 3 * xs - ys / 3) / size
        rr = (2 / 3 * ys) / size
        x, z = q, rr
        y = -x - z
        rx, ry, rz = np.round(x), np.round(y), np.round(z)
        dx, dy, dz = np.abs(rx - x), np.abs(ry - y), np.abs(rz - z)
        fix_x = (dx > dy) & (dx > dz)
        fix_z = ~fix_x & (dz >= dy)
        rx = np.where(fix_x, -ry - rz, rx)
        rz = np.where(fix_z, -rx - ry, rz)
        cx = np.clip(size * (np.sqrt(3) * rx + np.sqrt(3) / 2 * rz), 0, w - 1).astype(int)
        cy = np.clip(size * 1.5 * rz, 0, h - 1).astype(int)
        n = blob(rng, h, w, r)
        edge = np.maximum(np.maximum(dx, dy), dz) > 0.42  # thin outline inside each scale
        return n[cy, cx] > thr, edge
    idx = np.where(blob(rng, h, w, 9) > 0.0, 1, 0)
    big, big_edge = hex_layer(5.0, 8, 0.25)
    idx[big] = 2
    small, small_edge = hex_layer(2.6, 5, 0.75)
    idx[small] = 3
    idx[big & big_edge & ~small] = 3
    return idx


def pat_chocochip(rng, h, w):
    """Six-color desert: soft blobs + black 'rocks' with white highlights."""
    idx = layered(rng, h, w, [(10, 14, -0.1), (8, 11, 0.7)])
    rocks = (box_blur2((rng.random((h, w)) < 0.004).astype(np.float32), 1, 1) > 0.09)
    hi = np.roll(np.roll(rocks, -1, 0), 1, 1) & ~rocks
    idx[hi] = 3
    idx[rocks] = 4
    return idx


PATTERNS = {  # name: (generator, palette by index, seed)
    "M81":          (pat_woodland, [(0.60, 0.55, 0.40), (0.30, 0.36, 0.20), (0.38, 0.27, 0.17), (0.10, 0.10, 0.09)], 11),
    "TigerStripe":  (pat_tiger, [(0.44, 0.45, 0.28), (0.27, 0.32, 0.17), (0.30, 0.22, 0.13), (0.07, 0.07, 0.06)], 12),
    "TigerNight":   (pat_tiger, [(0.22, 0.24, 0.30), (0.14, 0.16, 0.22), (0.09, 0.10, 0.15), (0.03, 0.03, 0.05)], 13),
    "MARPAT":       (pat_digital, [(0.47, 0.44, 0.31), (0.33, 0.37, 0.22), (0.25, 0.21, 0.15), (0.10, 0.10, 0.08)], 14),
    "DesertDigital": (pat_digital, [(0.80, 0.72, 0.56), (0.67, 0.57, 0.41), (0.54, 0.44, 0.31), (0.40, 0.35, 0.28)], 15),
    "UrbanDigital": (pat_digital, [(0.70, 0.71, 0.72), (0.52, 0.53, 0.55), (0.33, 0.34, 0.36), (0.14, 0.14, 0.15)], 16),
    "Flecktarn":    (pat_flecktarn, [(0.43, 0.46, 0.33), (0.35, 0.42, 0.22), (0.20, 0.26, 0.14), (0.36, 0.25, 0.15), (0.08, 0.08, 0.07)], 17),
    "Splinter":     (pat_splinter, [(0.56, 0.53, 0.41), (0.30, 0.34, 0.22), (0.40, 0.30, 0.20), (0.18, 0.24, 0.14)], 18),
    "HexTyphon":    (pat_hex, [(0.30, 0.31, 0.29), (0.20, 0.21, 0.20), (0.11, 0.12, 0.11), (0.04, 0.04, 0.04)], 19),
    "ChocolateChip": (pat_chocochip, [(0.80, 0.72, 0.56), (0.66, 0.54, 0.38), (0.50, 0.38, 0.26), (0.93, 0.92, 0.88), (0.09, 0.08, 0.07)], 20),
}


def make_pattern_camo(src, keep, generator, palette, seed):
    h, w = src.shape[:2]
    rgb = src[..., :3]
    lum = rgb @ np.array([0.299, 0.587, 0.114], np.float32)
    rng = np.random.default_rng(seed)
    color = np.array(palette, np.float32)[generator(rng, h, w)]
    # Original shading: fine detail (stitches, pouch edges) + broad light/shadow painted in the atlas.
    local = box_blur(lum, 6)
    detail = np.clip((lum + 0.04) / (local + 0.04), 0.55, 1.5) ** 0.85
    broad = np.clip(0.8 + 0.9 * (local - local.mean()), 0.7, 1.2)
    camo = color * (detail * broad)[..., None]
    # Gloves, boots and straps (dark, flat areas) stay dark: only tinted with the darkest color.
    pal = np.array(palette, np.float32)
    darkest = pal[np.argmin(pal @ np.array([0.299, 0.587, 0.114]))]
    dark = smoothstep(0.22, 0.10, box_blur(lum, 6))[..., None]
    camo = camo * (1 - dark) + (rgb * 0.6 + darkest * 0.6) * dark
    out = src.copy()
    out[..., :3] = camo
    red = (rgb[..., 0] > 0.40) & (rgb[..., 0] > 1.8 * rgb[..., 1]) & (rgb[..., 0] > 1.8 * rgb[..., 2])
    k = keep | red
    out[k] = src[k]
    out[..., 3] = 1.0
    return out


# ---------------------------------------------------------------------------------------------
# Gear atlas
# ---------------------------------------------------------------------------------------------
def make_gear(size=256):
    rng = np.random.default_rng(7)
    img = np.zeros((size, size, 4), np.float32)
    img[..., 3] = 1
    img[..., :3] = 0.08

    def region(name):
        u0, v0, u1, v1 = GEAR_RECTS[name]
        x0, x1 = int(round(u0 * size)), int(round(u1 * size))
        y0, y1 = int(round(v0 * size)), int(round(v1 * size))
        h, w = y1 - y0, x1 - x0
        yy, xx = np.mgrid[0:h, 0:w]
        return (slice(y0, y1), slice(x0, x1)), (xx + 0.5) / w, (yy + 0.5) / h

    def noise(shape, scale, amp):
        n = rng.random(shape).astype(np.float32)
        return (box_blur(n, scale) - 0.5) * amp

    def fill(name, color, n_amp=0.06, n_scale=1):
        s, u, v = region(name)
        base = np.array(color, np.float32)
        img[s][..., :3] = base * (1 + noise(u.shape, n_scale, n_amp * 6)[..., None])
        return s, u, v

    # Rubber (gas mask face piece, straps).
    s, u, v = fill("RUBBER", (0.10, 0.10, 0.10), 0.05)
    img[s][..., :3] *= (1 + 0.25 * ((v * 18) % 1 < 0.08))[..., None]  # moulding lines
    # Lens: dark glass with a diagonal highlight.
    s, u, v = fill("LENS", (0.10, 0.16, 0.17), 0.02)
    hl = np.exp(-((u - v - 0.15) ** 2) / 0.004)
    img[s][..., :3] += (hl * 0.55)[..., None]
    # NVG tube: phosphor green with dark ring.
    s, u, v = fill("NVG_LENS", (0.25, 0.75, 0.30), 0.03)
    r = np.hypot(u - 0.5, v - 0.5)
    img[s][..., :3] *= np.where(r > 0.38, 0.15, 1.0)[..., None]
    # Gunmetal.
    s, u, v = fill("METAL", (0.24, 0.25, 0.26), 0.06, 2)
    img[s][..., :3] += (rng.random(u.shape) > 0.985)[..., None] * 0.25  # scratches
    # Filter canister: olive with ridges and a darker cap band.
    s, u, v = fill("FILTER", (0.28, 0.30, 0.18), 0.04)
    img[s][..., :3] *= (0.75 + 0.25 * (np.sin(v * np.pi * 16) > 0))[..., None]
    img[s][..., :3] *= np.where(v > 0.82, 0.55, 1.0)[..., None]
    # Shemagh: sand cloth with black woven pattern.
    s, u, v = fill("SHEMAGH", (0.78, 0.72, 0.58), 0.04)
    weave = ((np.floor(u * 12) + np.floor(v * 12)) % 2 == 0) & (((u * 12) % 1 < 0.25) | ((v * 12) % 1 < 0.25))
    img[s][..., :3] = np.where(weave[..., None], img[s][..., :3] * 0.25, img[s][..., :3])
    # Skull (lower face): bone with nose cavity and teeth (front projection, v=0 chin, v=1 eyes).
    s, u, v = fill("SKULL", (0.86, 0.84, 0.76), 0.03)
    nose = (np.abs(u - 0.5) < 0.12 * (1 - (v - 0.62) / 0.3)) & (v > 0.62) & (v < 0.9)
    teeth_band = (v > 0.28) & (v < 0.46) & (np.abs(u - 0.5) < 0.32)
    gaps = ((u * 14) % 1 < 0.14) | (np.abs(v - 0.37) < 0.012)
    cheek = (np.hypot((u - 0.5) * 1.3, v - 0.75) > 0.55)
    dark = nose | (teeth_band & gaps) | cheek
    img[s][..., :3] = np.where(dark[..., None], 0.06, img[s][..., :3])
    # Beret (maroon felt), polymer (tan NVG mount / headset), black cloth.
    fill("BERET", (0.36, 0.07, 0.09), 0.05, 2)
    fill("POLYMER", (0.52, 0.45, 0.33), 0.03)
    fill("CLOTH_BLACK", (0.07, 0.07, 0.08), 0.06, 2)
    # Radio: dark olive body, speaker grille and a small amber display.
    s, u, v = fill("RADIO", (0.17, 0.19, 0.13), 0.04)
    grille = (u < 0.45) & (v < 0.45) & ((u * 20) % 1 < 0.4)
    img[s][..., :3] = np.where(grille[..., None], 0.05, img[s][..., :3])
    disp = (u > 0.55) & (u < 0.9) & (v > 0.6) & (v < 0.8)
    img[s][..., :3] = np.where(disp[..., None], np.array([0.85, 0.55, 0.10]), img[s][..., :3])

    # --- Mask detail ---------------------------------------------------------------------------
    def radial(name):
        s, u, v = region(name)
        return s, u, v, np.hypot(u - 0.5, v - 0.5), np.arctan2(v - 0.5, u - 0.5)

    # Grille: metal disc with concentric slot rings and a dark outer lip.
    s, u, v, r, a = radial("GRILLE")
    fill("GRILLE", (0.30, 0.31, 0.32), 0.04)
    slots = ((r * 9) % 1 < 0.35) & (r < 0.42) & (np.cos(a * 10) > -0.6)
    img[s][..., :3] = np.where((slots | (r > 0.46))[..., None], 0.05, img[s][..., :3])
    # Rim: dark centre, bright metal ring with 6 screws.
    s, u, v, r, a = radial("RIM")
    fill("RIM", (0.36, 0.37, 0.38), 0.05)
    screws = (np.abs(r - 0.40) < 0.04) & (np.cos(a * 6) > 0.92)
    img[s][..., :3] = np.where(((r < 0.30) | (r > 0.48))[..., None], 0.07, img[s][..., :3])
    img[s][..., :3] = np.where(screws[..., None], 0.75, img[s][..., :3])
    # Filter cap: olive disc with perforation grid and a centre bolt.
    s, u, v, r, a = radial("FILTER_CAP")
    fill("FILTER_CAP", (0.30, 0.32, 0.19), 0.04)
    holes = (r < 0.38) & ((u * 10) % 1 < 0.35) & ((v * 10) % 1 < 0.35)
    img[s][..., :3] = np.where((holes | (r > 0.46))[..., None], 0.06, img[s][..., :3])
    img[s][..., :3] = np.where((r < 0.08)[..., None], np.array([0.45, 0.46, 0.47]), img[s][..., :3])
    # Strap: black webbing, weave lines and light stitching near the edges.
    s, u, v = fill("STRAP", (0.19, 0.19, 0.17), 0.03)  # dark grey webbing: reads against the black balaclava
    img[s][..., :3] *= (0.8 + 0.3 * ((u * 40) % 1 < 0.5))[..., None]
    stitch = ((np.abs(v - 0.15) < 0.03) | (np.abs(v - 0.85) < 0.03)) & ((u * 24) % 1 < 0.5)
    img[s][..., :3] = np.where(stitch[..., None], 0.35, img[s][..., :3])
    # Seal: lighter rubber with a grain.
    s, u, v = fill("SEAL", (0.17, 0.17, 0.17), 0.05)
    img[s][..., :3] *= (0.9 + 0.15 * (((u + v) * 20) % 1 < 0.5))[..., None]
    # Respirator body: grey polymer, moulded seams and a small embossed badge.
    s, u, v = fill("RESP_BODY", (0.27, 0.28, 0.30), 0.03)
    seams = (np.abs(u - 0.5) < 0.012) | (np.abs(v - 0.3) < 0.01) | (np.abs(v - 0.75) < 0.01)
    img[s][..., :3] = np.where(seams[..., None], 0.12, img[s][..., :3])
    badge = (np.abs(u - 0.5) < 0.1) & (np.abs(v - 0.52) < 0.06)
    img[s][..., :3] = np.where(badge[..., None], 0.40, img[s][..., :3])
    # Respirator pancake filter: olive with a yellow multi-gas band, dark mesh and white centre label.
    # (Real P100s are magenta, but in Unity that reads as the missing-shader pink, so it is avoided.)
    s, u, v, r, a = radial("P100")
    fill("P100", (0.30, 0.33, 0.18), 0.03)
    mesh = (r < 0.30) & ((u * 14) % 1 < 0.2) & ((v * 14) % 1 < 0.2)
    img[s][..., :3] = np.where(mesh[..., None], 0.12, img[s][..., :3])
    band = (r > 0.32) & (r < 0.40)
    img[s][..., :3] = np.where(band[..., None], np.array([0.78, 0.66, 0.12]), img[s][..., :3])
    img[s][..., :3] = np.where((r < 0.10)[..., None], 0.90, img[s][..., :3])
    img[s][..., :3] = np.where((r > 0.46)[..., None], img[s][..., :3] * 0.4, img[s][..., :3])
    # Medic patch: beret maroon with a bone-white cross and a dark stitched border (existing palette).
    s, u, v = fill("MEDIC", (0.36, 0.07, 0.09), 0.04)
    cross = ((np.abs(u - 0.5) < 0.12) & (np.abs(v - 0.5) < 0.34)) | ((np.abs(v - 0.5) < 0.12) & (np.abs(u - 0.5) < 0.34))
    img[s][..., :3] = np.where(cross[..., None], np.array([0.86, 0.84, 0.76]), img[s][..., :3])
    border = (u < 0.06) | (u > 0.94) | (v < 0.06) | (v > 0.94)
    img[s][..., :3] = np.where(border[..., None], 0.08, img[s][..., :3])
    return img


def main():
    os.makedirs(OUT_DIR, exist_ok=True)
    src = load(SOURCE_ATLAS)
    keep = protected_mask(*src.shape[:2])
    for name, palette in CAMOS.items():
        path = os.path.join(OUT_DIR, "Camo_%s.png" % name)
        save(make_camo(src, keep, palette), path)
        print("[soldier_textures] wrote", path)
    for name, (generator, palette, seed) in PATTERNS.items():
        path = os.path.join(OUT_DIR, "Camo_%s.png" % name)
        save(make_pattern_camo(src, keep, generator, palette, seed), path)
        print("[soldier_textures] wrote", path)
    path = os.path.join(OUT_DIR, "Gear_Atlas.png")
    save(make_gear(), path)
    print("[soldier_textures] wrote", path)


if __name__ == "__main__":
    main()
