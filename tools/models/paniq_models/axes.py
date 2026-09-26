"""
Where the authoring frame lands in Unity.

In the authoring frame a model's front faces -Y (Blender's Front view).
Between Blender's FBX exporter (forward -Z, up Y, "apply transform") and
Unity's importer, a Blender point (x, y, z) arrives in Unity as (x, z, y):
up stays up, Blender's +Y becomes Unity's +Z, and X is kept. So a front
left at -Y would face Unity -Z. Paniq's people face Unity +X
(``PresentationUtility.YawOf``), so the export step first turns the whole
model by ``EXPORT_YAW_DEGREES`` about the up axis.

The calibration box (``tools/models/examples/CalibrationBox.py``) and
``ModelsEditModeTests`` prove where the front lands. If that test fails
after a Blender or Unity upgrade, this number is the one to revisit.
"""

from math import radians

from mathutils import Matrix

# Turns the front from -Y to +X, which Unity keeps as +X. The first try was
# -90, on the belief that Unity mirrors X; the ruler showed the front on -X
# and the hinge on +X, so the sign was flipped (2026-09-25, Blender 5.2.2,
# Unity 6000.3.24f1).
EXPORT_YAW_DEGREES = 90.0


def export_matrix():
    return Matrix.Rotation(radians(EXPORT_YAW_DEGREES), 4, "Z")
