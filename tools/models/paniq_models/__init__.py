"""
Paniq's model kit: what a model script has to work with.

A model is a short Python file under ``tools/models/models/`` with one
function, ``build()``, that makes a :class:`Model`, adds pieces and parts to
it and returns it. ``tools/models/build.py`` runs that file inside Blender,
checks the result against the rules in ``docs/model-pipeline.md``, exports
it for Unity and renders a preview picture. Nobody runs Blender by hand.

The authoring frame is Blender's own: +Z is up, -Y is the front (what
Blender's Front view shows), +X is the model's right when you look at its
front. Distances are metres. A piece's ``at`` is its bottom centre, so a
box ``at=(0, 0, 0)`` stands on the floor with its middle over the origin.
The export step (``export.py``) turns all of that into Unity's frame; a
model script never thinks about Unity's axes.
"""

from math import radians

import bmesh
import bpy
from mathutils import Matrix, Vector

DIRECTIONS = {
    "up": Vector((0.0, 0.0, 1.0)),
    "down": Vector((0.0, 0.0, -1.0)),
    "front": Vector((0.0, -1.0, 0.0)),
    "back": Vector((0.0, 1.0, 0.0)),
    "right": Vector((1.0, 0.0, 0.0)),
    "left": Vector((-1.0, 0.0, 0.0)),
}

MODEL_COLLECTION = "Model"


class ModelError(Exception):
    """A model broke one of the rules in docs/model-pipeline.md. The message says which, in plain words."""


class Piece:
    """
    One block of geometry on its way into an object. Every op changes the
    block in place and returns it, so calls can be chained:
    ``m.box("Cabinet", (0.9, 0.8, 1.8)).inset("front", 0.05, -0.02)``.
    """

    def __init__(self, name, bm):
        self.name = name
        self.bm = bm

    def bevel(self, width, segments=1):
        """Rounds every edge off by ``width`` metres; more segments make a rounder edge."""
        bmesh.ops.bevel(
            self.bm,
            geom=self.bm.verts[:] + self.bm.edges[:],
            offset=width,
            offset_type="OFFSET",
            segments=segments,
            profile=0.5,
            affect="EDGES",
            clamp_overlap=True,
        )
        return self

    def inset(self, face, thickness, depth=0.0):
        """
        Draws a border ``thickness`` metres wide just inside the face that
        looks ``face``-wards ("front", "up", ...) and pushes the middle in
        (negative ``depth``) or out (positive): a screen, a drawer front, a
        recessed panel.
        """
        target = self._face(face)
        bmesh.ops.inset_individual(self.bm, faces=[target], thickness=thickness, depth=depth, use_even_offset=True)
        return self

    def extrude(self, face, distance, scale=1.0):
        """
        Pulls the face that looks ``face``-wards out by ``distance`` metres.
        A ``scale`` under 1 shrinks the pulled face, which tapers the block
        like a lampshade or a flower pot.
        """
        target = self._face(face)
        normal = target.normal.copy()
        result = bmesh.ops.extrude_discrete_faces(self.bm, faces=[target])
        new_face = result["faces"][0]
        verts = list(new_face.verts)
        bmesh.ops.translate(self.bm, vec=normal * distance, verts=verts)
        if scale != 1.0:
            centre = sum((v.co for v in verts), Vector()) / len(verts)
            for vert in verts:
                vert.co = centre + (vert.co - centre) * scale
        return self

    def _face(self, direction):
        if direction not in DIRECTIONS:
            raise ModelError(f"'{direction}' is not a face name; use one of {', '.join(DIRECTIONS)}.")
        self.bm.normal_update()
        wanted = DIRECTIONS[direction]
        return max(self.bm.faces, key=lambda f: f.normal.dot(wanted))


class Part:
    """
    A named piece of the model the game may move on its own: a lid, a flap,
    the door of a cabinet. Its origin is the hinge, so turning it in Unity
    swings it the way the real thing would. Pieces are added to it with
    ``parent=``, in the same frame as everything else.
    """

    def __init__(self, name, hinge_at):
        self.name = name
        self.hinge_at = Vector(hinge_at)
        self.pieces = []


class Model:
    """
    One model: its name, the footprint and height it must fit, the number
    of triangles it may spend, and the pieces, parts and shape keys added
    to it. ``realise`` turns all of that into Blender objects.
    """

    def __init__(self, name, footprint_mm, height_mm, budget_tris):
        self.name = name
        self.footprint_mm = (int(footprint_mm[0]), int(footprint_mm[1]))
        self.height_mm = int(height_mm)
        self.budget_tris = int(budget_tris)
        self.pieces = []
        self.parts = []
        self.shape_keys = []
        self.body = None
        self.objects = []

    def box(self, name, size, at=(0.0, 0.0, 0.0), bevel=0.0, bevel_segments=1, parent=None):
        """A block ``size`` = (width, depth, height) metres, its bottom centre ``at``."""
        width, depth, height = size
        bm = bmesh.new()
        bmesh.ops.create_cube(bm, size=1.0)
        bmesh.ops.scale(bm, vec=(width, depth, height), verts=bm.verts[:])
        bmesh.ops.translate(bm, vec=(at[0], at[1], at[2] + height / 2.0), verts=bm.verts[:])
        return self._add(Piece(name, bm), bevel, bevel_segments, parent)

    def cylinder(self, name, radius, height, at=(0.0, 0.0, 0.0), segments=12, axis="Z", bevel=0.0,
                 bevel_segments=1, parent=None):
        """
        A drum ``height`` long and ``radius`` wide, standing up (``axis="Z"``)
        or lying along X or Y, its bottom centre ``at``. Twelve segments is
        the crude look; use more only for something large and round.
        """
        bm = bmesh.new()
        bmesh.ops.create_cone(bm, cap_ends=True, cap_tris=False, segments=segments, radius1=radius,
                              radius2=radius, depth=height)
        if axis == "Z":
            lift = height / 2.0
        elif axis == "X":
            bmesh.ops.rotate(bm, cent=(0.0, 0.0, 0.0), matrix=Matrix.Rotation(radians(90.0), 3, "Y"), verts=bm.verts[:])
            lift = radius
        elif axis == "Y":
            bmesh.ops.rotate(bm, cent=(0.0, 0.0, 0.0), matrix=Matrix.Rotation(radians(90.0), 3, "X"), verts=bm.verts[:])
            lift = radius
        else:
            raise ModelError(f"A cylinder's axis is 'X', 'Y' or 'Z', not '{axis}'.")
        bmesh.ops.translate(bm, vec=(at[0], at[1], at[2] + lift), verts=bm.verts[:])
        return self._add(Piece(name, bm), bevel, bevel_segments, parent)

    def part(self, name, hinge_at):
        """A named moving part hinged at ``hinge_at``; add its pieces with ``parent=``."""
        part = Part(name, hinge_at)
        self.parts.append(part)
        return part

    def shape_key(self, name, move):
        """
        A stored deformation of the body, which Unity calls a blend shape.
        ``move`` takes a vertex position (a Vector in the authoring frame)
        and returns where that vertex goes at full strength; the game dials
        the strength between 0 and 1 in code.
        """
        self.shape_keys.append((name, move))

    def _add(self, piece, bevel, segments, parent):
        if bevel > 0.0:
            piece.bevel(bevel, segments)
        (parent.pieces if parent is not None else self.pieces).append(piece)
        return piece

    def realise(self):
        """
        Turns the pieces into Blender objects: one body named after the
        model, plus one child object per part with its origin at the hinge,
        all in the ``Model`` collection so the export can pick them out.
        """
        if not self.pieces:
            raise ModelError(f"{self.name} has no pieces on its body. Add at least one box or cylinder without a parent.")
        collection = bpy.data.collections.new(MODEL_COLLECTION)
        bpy.context.scene.collection.children.link(collection)
        self.body = _object_from(self.name, self.pieces, Vector((0.0, 0.0, 0.0)), collection)
        self.objects = [self.body]
        for part in self.parts:
            if not part.pieces:
                raise ModelError(f"The part '{part.name}' of {self.name} has no pieces. Add them with parent={part.name}.")
            obj = _object_from(part.name, part.pieces, part.hinge_at, collection)
            obj.parent = self.body
            obj.matrix_parent_inverse = Matrix.Identity(4)
            self.objects.append(obj)
        if self.shape_keys:
            self.body.shape_key_add(name="Basis", from_mix=False)
            mesh = self.body.data
            for name, move in self.shape_keys:
                key = self.body.shape_key_add(name=name, from_mix=False)
                for vertex in mesh.vertices:
                    key.data[vertex.index].co = Vector(move(vertex.co.copy()))
        return self.objects


def _object_from(name, pieces, origin, collection):
    bm = bmesh.new()
    for piece in pieces:
        scratch = bpy.data.meshes.new(f"{piece.name} (piece)")
        piece.bm.to_mesh(scratch)
        piece.bm.free()
        bm.from_mesh(scratch)
        bpy.data.meshes.remove(scratch)
    if origin.length > 0.0:
        bmesh.ops.translate(bm, vec=-origin, verts=bm.verts[:])
    mesh = bpy.data.meshes.new(name)
    bm.to_mesh(mesh)
    bm.free()
    for polygon in mesh.polygons:
        polygon.use_smooth = False
    obj = bpy.data.objects.new(name, mesh)
    obj.location = origin
    collection.objects.link(obj)
    return obj


def triangle_count(objects):
    """What the FBX will hold: every face counted as the triangles it splits into."""
    return sum(max(len(polygon.vertices) - 2, 0) for obj in objects for polygon in obj.data.polygons)
