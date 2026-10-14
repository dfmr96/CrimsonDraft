"""UV rects (u0, v0, u1, v1) of Gear_Atlas.png, shared by soldier_textures.py (painting) and
soldier_variants.py (mesh UVs) so accessories always land on the right paint."""

GEAR_RECTS = {
    "RUBBER":      (0.000, 0.750, 0.250, 1.000),
    "LENS":        (0.250, 0.875, 0.375, 1.000),
    "NVG_LENS":    (0.375, 0.875, 0.500, 1.000),
    "CLOTH_BLACK": (0.250, 0.750, 0.500, 0.875),
    "METAL":       (0.500, 0.750, 0.750, 1.000),
    "FILTER":      (0.750, 0.750, 1.000, 1.000),
    "SHEMAGH":     (0.000, 0.500, 0.250, 0.750),
    "SKULL":       (0.250, 0.500, 0.500, 0.750),
    "BERET":       (0.500, 0.500, 0.625, 0.625),
    "POLYMER":     (0.500, 0.625, 0.625, 0.750),
    "RADIO":       (0.625, 0.500, 0.875, 0.750),
    # Mask detail (gas masks / respirator)
    "GRILLE":      (0.000, 0.375, 0.125, 0.500),  # voicemitter / valve grille (circular slots)
    "RIM":         (0.125, 0.375, 0.250, 0.500),  # lens / filter mount ring with screws
    "FILTER_CAP":  (0.250, 0.375, 0.375, 0.500),  # filter end face, perforated
    "STRAP":       (0.375, 0.375, 0.625, 0.500),  # black webbing with stitching
    "SEAL":        (0.625, 0.375, 0.750, 0.500),  # lighter rubber (frames, seals, chin cup)
    "RESP_BODY":   (0.750, 0.375, 1.000, 0.500),  # moulded grey polymer of the half-face respirator
    "P100":        (0.000, 0.250, 0.125, 0.375),  # magenta pancake filter
}

GEAR_MATERIAL = "Mat_Gear"
GEAR_TEXTURE = "Gear_Atlas.png"
