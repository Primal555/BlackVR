import argparse
import os
import sys
from pathlib import Path

import bpy


def clear_scene():
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete(use_global=False)

    for collection in (
        bpy.data.armatures,
        bpy.data.meshes,
        bpy.data.materials,
        bpy.data.images,
    ):
        for data_block in list(collection):
            if data_block.users == 0:
                collection.remove(data_block)


def relink_textures(texture_directory):
    if not texture_directory:
        return

    texture_path = Path(texture_directory)
    for image in bpy.data.images:
        if image.source != "FILE":
            continue
        candidate = texture_path / Path(bpy.path.abspath(image.filepath)).name
        if candidate.exists():
            image.filepath = str(candidate)


def export_model(source_path, output_path, texture_directory):
    clear_scene()
    result = bpy.ops.mmd_tools.import_model(filepath=str(source_path))
    if "FINISHED" not in result:
        raise RuntimeError(f"Unable to import PMX model: {source_path}")

    relink_textures(texture_directory)
    bpy.ops.object.select_all(action="DESELECT")
    exported_objects = [
        obj for obj in bpy.context.scene.objects if obj.type in {"ARMATURE", "MESH"}
    ]
    if not exported_objects:
        raise RuntimeError(f"No exportable objects imported from: {source_path}")

    for obj in exported_objects:
        obj.select_set(True)

    output_path.parent.mkdir(parents=True, exist_ok=True)
    result = bpy.ops.export_scene.fbx(
        filepath=str(output_path),
        use_selection=True,
        object_types={"ARMATURE", "MESH"},
        apply_unit_scale=True,
        apply_scale_options="FBX_SCALE_UNITS",
        axis_forward="-Z",
        axis_up="Y",
        add_leaf_bones=False,
        use_armature_deform_only=False,
        bake_anim=False,
        path_mode="AUTO",
    )
    if "FINISHED" not in result:
        raise RuntimeError(f"Unable to export FBX model: {output_path}")

    print(f"Exported {source_path} -> {output_path}")


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--source", default=os.environ.get("MMD_SOURCE"))
    parser.add_argument("--output", default=os.environ.get("MMD_OUTPUT"))
    parser.add_argument("--textures", default=os.environ.get("MMD_TEXTURES"))
    script_arguments = sys.argv[sys.argv.index("--") + 1 :] if "--" in sys.argv else []
    arguments = parser.parse_args(script_arguments)
    if not arguments.source or not arguments.output:
        parser.error("Provide --source and --output or set MMD_SOURCE and MMD_OUTPUT.")

    export_model(
        Path(arguments.source),
        Path(arguments.output),
        arguments.textures,
    )


if __name__ == "__main__":
    main()
