"""
A picture of the model so the owner and the assistant can see it without
opening Blender or Unity: the game's own view on the left, a straight
front view on the right, each surface in its own pale shade. Rendering may fail on a machine with no usable
graphics; the FBX never waits for it.
"""

import os

import bpy
import numpy as np
from mathutils import Vector

PREVIEW_COLLECTION = "Preview"
VIEW_PIXELS = 512
SURFACE_SHADES = [
    (0.82, 0.80, 0.76, 1.0),
    (0.52, 0.66, 0.80, 1.0),
    (0.86, 0.68, 0.42, 1.0),
    (0.56, 0.74, 0.52, 1.0),
    (0.80, 0.56, 0.58, 1.0),
    (0.64, 0.58, 0.78, 1.0),
]
FLOOR_COLOUR = (0.42, 0.43, 0.45, 1.0)
SKY_COLOUR = (0.78, 0.80, 0.83)


def render(objects, low, high, scratch_dir, out_path):
    """Renders the two views into ``scratch_dir`` and composes them side by side at ``out_path``."""
    scene = bpy.context.scene
    collection = bpy.data.collections.new(PREVIEW_COLLECTION)
    scene.collection.children.link(collection)
    centre = (low + high) / 2.0
    extent = max(high.x - low.x, high.y - low.y, high.z - low.z)

    _tint_surfaces(objects)
    _floor(collection, centre, extent)
    _sun(collection)
    _world(scene)
    _render_settings(scene)

    views = [
        # The game's corner-on isometric view: 35.264 degrees down, from the front-right.
        ("game", Vector((1.0, -1.0, 1.0)).normalized(), extent * 1.9),
        # Straight at the front, for proportions.
        ("front", Vector((0.0, -1.0, 0.0)), max(high.x - low.x, high.z - low.z) * 1.3),
    ]
    paths = []
    for name, direction, ortho_scale in views:
        camera = _camera(collection, name, centre, direction, extent, ortho_scale)
        scene.camera = camera
        path = os.path.join(scratch_dir, f"view-{name}.png")
        scene.render.filepath = path
        bpy.ops.render.render(write_still=True)
        paths.append(path)
    _compose(paths, out_path)


def _tint_surfaces(objects):
    """
    Each surface a different pale shade, the first (usually Body) the
    palest, so the picture shows where one surface ends and the next
    begins. Unity imports none of these colours; they are for the picture.
    """
    names = []
    for obj in objects:
        for material in obj.data.materials:
            if material.name not in names:
                names.append(material.name)
    for index, name in enumerate(names):
        _colour(bpy.data.materials[name], SURFACE_SHADES[index % len(SURFACE_SHADES)], 0.7)


def _colour(material, colour, roughness):
    """Sets a material's colour both where the viewport reads it and where the renderer's shader node does."""
    material.diffuse_color = colour
    material.roughness = roughness
    tree = getattr(material, "node_tree", None)
    if tree is None:
        return
    for node in tree.nodes:
        if node.type == "BSDF_PRINCIPLED":
            node.inputs["Base Color"].default_value = colour
            node.inputs["Roughness"].default_value = roughness


def _floor(collection, centre, extent):
    size = extent * 6.0
    mesh = bpy.data.meshes.new("PreviewFloor")
    mesh.from_pydata(
        [(-size, -size, 0.0), (size, -size, 0.0), (size, size, 0.0), (-size, size, 0.0)], [], [(0, 1, 2, 3)])
    obj = bpy.data.objects.new("PreviewFloor", mesh)
    obj.location = (centre.x, centre.y, -0.0005)
    material = bpy.data.materials.new("PreviewFloor")
    _colour(material, FLOOR_COLOUR, 0.9)
    mesh.materials.append(material)
    collection.objects.link(obj)


def _sun(collection):
    light = bpy.data.lights.new("PreviewSun", "SUN")
    light.energy = 4.5
    light.angle = 0.2
    obj = bpy.data.objects.new("PreviewSun", light)
    obj.rotation_euler = (0.95, 0.15, -0.7)
    collection.objects.link(obj)


def _world(scene):
    world = bpy.data.worlds.new("PreviewWorld")
    world.use_nodes = True
    background = world.node_tree.nodes.get("Background")
    if background is not None:
        background.inputs[0].default_value = (*SKY_COLOUR, 1.0)
        background.inputs[1].default_value = 0.45
    scene.world = world


def _render_settings(scene):
    scene.render.engine = "BLENDER_EEVEE"
    try:
        scene.eevee.taa_render_samples = 16
    except AttributeError:
        pass
    try:
        scene.view_settings.view_transform = "Standard"
    except TypeError:
        pass
    scene.render.resolution_x = VIEW_PIXELS
    scene.render.resolution_y = VIEW_PIXELS
    scene.render.resolution_percentage = 100
    scene.render.film_transparent = False
    scene.render.image_settings.file_format = "PNG"
    scene.render.image_settings.color_mode = "RGBA"


def _camera(collection, name, centre, direction, extent, ortho_scale):
    data = bpy.data.cameras.new(f"PreviewCamera-{name}")
    data.type = "ORTHO"
    data.ortho_scale = ortho_scale
    data.clip_start = 0.01
    data.clip_end = extent * 50.0
    obj = bpy.data.objects.new(f"PreviewCamera-{name}", data)
    obj.location = centre + direction * extent * 8.0
    obj.rotation_euler = direction.to_track_quat("Z", "Y").to_euler()
    collection.objects.link(obj)
    return obj


def _compose(paths, out_path):
    strips = []
    for path in paths:
        image = bpy.data.images.load(path)
        image.colorspace_settings.name = "Non-Color"
        width, height = image.size
        pixels = np.empty(width * height * 4, dtype=np.float32)
        image.pixels.foreach_get(pixels)
        strips.append(pixels.reshape(height, width, 4))
        bpy.data.images.remove(image)
    sheet = np.concatenate(strips, axis=1)
    height, width = sheet.shape[:2]
    out = bpy.data.images.new("PaniqPreview", width=width, height=height, alpha=True)
    out.colorspace_settings.name = "Non-Color"
    out.pixels.foreach_set(sheet.ravel())
    os.makedirs(os.path.dirname(out_path), exist_ok=True)
    out.filepath_raw = out_path
    out.file_format = "PNG"
    out.save()
    bpy.data.images.remove(out)
