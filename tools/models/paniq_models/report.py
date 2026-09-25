"""The report beside each preview: what was built, and a hash that says whether anything changed."""

import hashlib
import json
import os


def geometry_hash(objects, recipe):
    """
    A short fingerprint of the geometry and the export recipe. An FBX file
    carries a timestamp, so two exports of the same model never match byte
    for byte; this is how the build knows nothing changed.
    """
    digest = hashlib.sha256()
    for obj in sorted(objects, key=lambda o: o.name):
        digest.update(obj.name.encode())
        digest.update((obj.parent.name if obj.parent is not None else "").encode())
        digest.update(_vector(obj.location))
        mesh = obj.data
        for vertex in mesh.vertices:
            digest.update(_vector(vertex.co))
        for polygon in mesh.polygons:
            digest.update(",".join(str(index) for index in polygon.vertices).encode())
            digest.update(b";")
        if mesh.shape_keys is not None:
            for block in mesh.shape_keys.key_blocks:
                digest.update(block.name.encode())
                for point in block.data:
                    digest.update(_vector(point.co))
    digest.update(recipe.encode())
    return digest.hexdigest()[:16]


def _vector(v):
    return f"{v.x:.5f},{v.y:.5f},{v.z:.5f};".encode()


def read(path):
    if not os.path.isfile(path):
        return None
    with open(path, "r", encoding="utf-8") as handle:
        return json.load(handle)


def write(path, data):
    os.makedirs(os.path.dirname(path), exist_ok=True)
    with open(path, "w", encoding="utf-8", newline="\n") as handle:
        json.dump(data, handle, indent=2, sort_keys=True)
        handle.write("\n")
