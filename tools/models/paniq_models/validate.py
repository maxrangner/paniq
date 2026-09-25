"""The rules every model follows, checked before anything is exported."""

from math import inf

from mathutils import Vector

from . import ModelError, triangle_count

OVERSIZE_ALLOWED = 1.05
FLOOR_SLACK = 0.001
HINGE_SLACK = 0.01
DEFAULT_NAMES = ("Cube", "Cylinder", "Cone", "Mesh", "Object")


def world_bounds(objects):
    """The box round every vertex of every object, in the authoring frame."""
    low = Vector((inf, inf, inf))
    high = Vector((-inf, -inf, -inf))
    for obj in objects:
        for vertex in obj.data.vertices:
            co = obj.location + vertex.co
            low.x, low.y, low.z = min(low.x, co.x), min(low.y, co.y), min(low.z, co.z)
            high.x, high.y, high.z = max(high.x, co.x), max(high.y, co.y), max(high.z, co.z)
    return low, high


def check(model, objects):
    """Raises ModelError with a plain-language reason, or returns (low, high, triangles)."""
    low, high = world_bounds(objects)
    if abs(low.z) > FLOOR_SLACK:
        raise ModelError(
            f"{model.name} does not stand on the floor: its lowest point is at {low.z:.3f} m, "
            "and every model's lowest point must be at exactly 0.")
    width = model.footprint_mm[0] / 1000.0
    depth = model.footprint_mm[1] / 1000.0
    height = model.height_mm / 1000.0
    reach_x = max(abs(low.x), abs(high.x))
    reach_y = max(abs(low.y), abs(high.y))
    if reach_x > width / 2.0 * OVERSIZE_ALLOWED or reach_y > depth / 2.0 * OVERSIZE_ALLOWED:
        raise ModelError(
            f"{model.name} spills over its footprint: it reaches {reach_x * 2:.3f} m across and "
            f"{reach_y * 2:.3f} m deep, but is declared {width:.3f} x {depth:.3f} m (5% over is allowed).")
    if high.z > height * OVERSIZE_ALLOWED:
        raise ModelError(
            f"{model.name} is too tall: {high.z:.3f} m against a declared {height:.3f} m (5% over is allowed).")
    triangles = triangle_count(objects)
    if triangles > model.budget_tris:
        raise ModelError(
            f"{model.name} spends {triangles} triangles against a budget of {model.budget_tris}. "
            "Crude is the style: fewer pieces, fewer bevel segments, fewer cylinder segments.")
    for obj in objects:
        if not obj.name.strip() or obj.name.startswith(DEFAULT_NAMES):
            raise ModelError(f"An object in {model.name} is called '{obj.name}'; every piece and part needs a real name.")
        if obj.parent is not None:
            hinge = obj.location
            if not (low.x - HINGE_SLACK <= hinge.x <= high.x + HINGE_SLACK
                    and low.y - HINGE_SLACK <= hinge.y <= high.y + HINGE_SLACK
                    and low.z - HINGE_SLACK <= hinge.z <= high.z + HINGE_SLACK):
                raise ModelError(
                    f"The hinge of '{obj.name}' at ({hinge.x:.3f}, {hinge.y:.3f}, {hinge.z:.3f}) lies outside "
                    f"{model.name}; a hinge sits on the thing it swings from.")
    return low, high, triangles
