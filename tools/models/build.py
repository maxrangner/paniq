"""
Runs one model script inside Blender: build, check, export, preview, report.

Called by tools/BuildModel.ps1, never by hand:

    blender --background --factory-startup --python-exit-code 1
            --python tools/models/build.py --
            --model <Name> --repo <repository> --scratch <folder> [--fixture] [--keep-blend]

Every line meant for a person starts with "PANIQ "; BuildModel.ps1 shows
those and hides the rest of Blender's chatter unless something fails.
"""

import argparse
import importlib.util
import os
import shutil
import sys


def parse_args():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    parser = argparse.ArgumentParser(prog="build.py")
    parser.add_argument("--model", required=True, help="the script's name without .py")
    parser.add_argument("--repo", required=True, help="the repository root")
    parser.add_argument("--scratch", required=True, help="where raw exports, view renders and .blend files go")
    parser.add_argument("--fixture", action="store_true", help="a test fixture from examples/, not a game model")
    parser.add_argument("--keep-blend", action="store_true", help="also save a .blend file to look at")
    parser.add_argument("--force", action="store_true", help="render the picture again even if nothing changed")
    return parser.parse_args(argv)


def load_script(path):
    spec = importlib.util.spec_from_file_location("paniq_model_script", path)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


def main():
    args = parse_args()
    repo = os.path.abspath(args.repo)
    tools = os.path.join(repo, "tools", "models")
    sys.path.insert(0, tools)

    import bpy
    from paniq_models import Model, ModelError, export, preview, report, validate

    folder = "examples" if args.fixture else "models"
    script = os.path.join(tools, folder, f"{args.model}.py")
    if not os.path.isfile(script):
        raise ModelError(f"There is no tools/models/{folder}/{args.model}.py to build.")

    bpy.ops.wm.read_factory_settings(use_empty=True)
    scene = bpy.context.scene
    scene.unit_settings.system = "METRIC"
    scene.unit_settings.scale_length = 1.0

    module = load_script(script)
    if not hasattr(module, "build"):
        raise ModelError(f"{args.model}.py has no build() function.")
    model = module.build()
    if not isinstance(model, Model):
        raise ModelError(f"{args.model}.py's build() must return the Model it made.")
    if model.name != args.model:
        raise ModelError(
            f"The script is {args.model}.py but its model is named '{model.name}'; the two must match, "
            "because the file Unity sees takes the model's name.")

    objects = model.realise()
    low, high, triangles = validate.check(model, objects)
    digest = report.geometry_hash(objects, export.RECIPE)

    if args.fixture:
        destination = os.path.join(repo, "Assets", "Paniq", "Tests", "Fixtures", "Models", f"{model.name}.fbx")
    else:
        destination = os.path.join(repo, "Assets", "Paniq", "Content", "Models", f"{model.name}.fbx")
    previews = os.path.join(repo, "docs", "models", "previews")
    preview_png = os.path.join(previews, f"{model.name}.png")
    report_json = os.path.join(previews, f"{model.name}.json")

    previous = report.read(report_json)
    unchanged = previous is not None and previous.get("hash") == digest and os.path.isfile(destination)
    if unchanged and os.path.isfile(preview_png) and not args.force:
        print(f"PANIQ unchanged {model.name}: nothing copied (hash {digest})")
        return

    scratch = os.path.join(os.path.abspath(args.scratch), model.name)
    os.makedirs(scratch, exist_ok=True)

    rendered = True
    try:
        preview.render(objects, low, high, scratch, preview_png)
    except Exception as error:  # a machine without usable graphics still gets its FBX
        rendered = False
        print(f"PANIQ warning {model.name}: no preview picture ({error})")

    if unchanged:
        print(f"PANIQ unchanged {model.name}: the picture was drawn again, the FBX was left alone (hash {digest})")
        return

    exported = os.path.join(scratch, f"{model.name}.fbx")
    export.export_fbx(objects, exported)
    if args.keep_blend:
        bpy.ops.wm.save_as_mainfile(filepath=os.path.join(scratch, f"{model.name}.blend"))
    os.makedirs(os.path.dirname(destination), exist_ok=True)
    shutil.copyfile(exported, destination)

    data = {
        "name": model.name,
        "hash": digest,
        "recipe": export.RECIPE,
        "blender": bpy.app.version_string,
        "triangles": triangles,
        "vertices": sum(len(obj.data.vertices) for obj in objects),
        "parts": [obj.name for obj in objects if obj.parent is not None],
        "shape_keys": [name for name, _ in model.shape_keys],
        "footprint_mm": list(model.footprint_mm),
        "height_mm": model.height_mm,
        "budget_tris": model.budget_tris,
        "bounds_min_m": [round(value, 4) for value in low],
        "bounds_max_m": [round(value, 4) for value in high],
        "fbx": os.path.relpath(destination, repo).replace(os.sep, "/"),
        "preview": rendered,
    }
    report.write(report_json, data)

    if not bpy.app.version_string.startswith("5.2"):
        print(f"PANIQ warning {model.name}: built with Blender {bpy.app.version_string}, not the 5.2 LTS the project pins")
    print(
        f"PANIQ built {model.name}: {triangles} triangles, {len(data['parts'])} moving part(s), "
        f"{len(data['shape_keys'])} shape key(s), {high.x - low.x:.2f} x {high.y - low.y:.2f} x {high.z - low.z:.2f} m, "
        f"hash {digest} -> {data['fbx']}")


if __name__ == "__main__":
    main()
