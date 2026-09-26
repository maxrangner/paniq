"""
The one way a model leaves Blender: an FBX file with settings Unity reads
at true size and the right way up. The settings are fixed here so no model
script and no human ever chooses them.
"""

import bpy

from . import MODEL_COLLECTION, UNWRAP_ANGLE_DEGREES, UNWRAP_ISLAND_MARGIN
from .axes import EXPORT_YAW_DEGREES, export_matrix

# Named in the report and folded into the geometry hash, so a change to the
# recipe rebuilds every model.
RECIPE = (f"yaw={EXPORT_YAW_DEGREES:.0f};forward=-Z;up=Y;apply_transform;scale=FBX_SCALE_ALL;triangles;"
          f"uv=smart{UNWRAP_ANGLE_DEGREES:.0f}/{UNWRAP_ISLAND_MARGIN};surfaces")


def export_fbx(objects, path):
    """Bakes the Unity-facing yaw into the mesh data, then writes the FBX."""
    rotation = export_matrix()
    for obj in objects:
        obj.data.transform(rotation, shape_keys=True)
        obj.location = rotation @ obj.location
    bpy.ops.export_scene.fbx(
        filepath=path,
        collection=MODEL_COLLECTION,
        global_scale=1.0,
        apply_unit_scale=True,
        apply_scale_options="FBX_SCALE_ALL",
        axis_forward="-Z",
        axis_up="Y",
        use_space_transform=True,
        bake_space_transform=True,
        object_types={"MESH", "EMPTY"},
        use_mesh_modifiers=False,
        mesh_smooth_type="FACE",
        colors_type="NONE",
        use_triangles=True,
        add_leaf_bones=False,
        bake_anim=False,
        path_mode="STRIP",
        embed_textures=False,
        use_custom_props=False,
        use_metadata=False,
    )
