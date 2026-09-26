"""
The pipeline's ruler, not a game model.

A one-metre box with a bump on top, a bump on the front and a bump on its
right side, a flap hinged along its back top edge, and one stored
deformation that squashes it. The front bump is a second surface
("Accent"). ModelsEditModeTests loads the exported file in Unity and checks
that the box is a metre across, that the bumps sit on +Y (up), +X (the way
people face) and +Z (the model's right), that the flap's pivot is on the
hinge, that the deformation arrived as a blend shape, that the two surfaces
arrived as two sub-meshes, that every mesh has a texture map and the
tangents lighting needs, and that nothing carries a stray rotation or
scale. If a Blender or
Unity upgrade ever changes any of that, this is where it shows.
"""

from paniq_models import Model


def build():
    m = Model("CalibrationBox", footprint_mm=(1200, 1200), height_mm=1200, budget_tris=200)
    m.box("Block", size=(1.0, 1.0, 1.0), bevel=0.02)
    m.box("TopBump", size=(0.2, 0.2, 0.2), at=(0.0, 0.0, 1.0))
    m.box("FrontBump", size=(0.2, 0.1, 0.2), at=(0.0, -0.55, 0.4), surface="Accent")
    m.box("RightBump", size=(0.1, 0.2, 0.2), at=(0.55, 0.0, 0.4))
    lid = m.part("Lid", hinge_at=(0.0, 0.5, 1.0))
    m.box("Flap", size=(0.6, 0.3, 0.02), at=(0.0, 0.35, 1.0), parent=lid)
    m.shape_key("Squash", lambda p: (p.x * 1.15, p.y * 1.15, p.z * 0.8))
    return m
