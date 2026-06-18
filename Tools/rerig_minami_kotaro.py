import argparse
import sys
from pathlib import Path

import bpy
from mathutils import Vector


def clear_scene():
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete()


def import_fbx(path):
    bpy.ops.import_scene.fbx(filepath=str(path))


def largest_mesh():
    meshes = [obj for obj in bpy.context.scene.objects if obj.type == "MESH"]
    if not meshes:
        raise RuntimeError("No mesh found after importing FBX")
    return max(meshes, key=lambda obj: len(obj.data.vertices))


def rebind_material_textures(texture_dir):
    if not texture_dir:
        return

    texture_dir = Path(texture_dir).resolve()
    texture_by_name = {
        "texture_pbr_20250901.png": texture_dir / "texture_pbr_20250901.png",
        "texture_pbr_20250901_normal.png": texture_dir / "texture_pbr_20250901_normal.png",
        "texture_pbr_20250901_roughness.png": texture_dir / "texture_pbr_20250901_roughness.png",
        "texture_pbr_20250901_metallic.png": texture_dir / "texture_pbr_20250901_metallic.png",
    }

    for material in bpy.data.materials:
        if not material.use_nodes or not material.node_tree:
            continue

        for node in material.node_tree.nodes:
            if node.type != "TEX_IMAGE" or not node.image:
                continue

            image_name = Path(node.image.name).name
            target_path = texture_by_name.get(image_name)
            if not target_path or not target_path.exists():
                continue

            node.image.filepath = str(target_path)
            node.image.filepath_raw = str(target_path)
            node.image.pack()


def world_bounds(obj):
    corners = [obj.matrix_world @ Vector(corner) for corner in obj.bound_box]
    mins = Vector((min(c.x for c in corners), min(c.y for c in corners), min(c.z for c in corners)))
    maxs = Vector((max(c.x for c in corners), max(c.y for c in corners), max(c.z for c in corners)))
    return mins, maxs


def normalize_mesh(mesh):
    bpy.context.view_layer.objects.active = mesh
    mesh.select_set(True)
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)

    for mod in list(mesh.modifiers):
        if mod.type == "ARMATURE":
            mesh.modifiers.remove(mod)

    mesh.vertex_groups.clear()

    mins, maxs = world_bounds(mesh)
    mesh.location.z -= mins.z
    bpy.context.view_layer.update()

    mins, maxs = world_bounds(mesh)
    center = (mins + maxs) * 0.5
    height = maxs.z - mins.z
    width = maxs.x - mins.x
    depth = maxs.y - mins.y
    return center, height, width, depth


def delete_armatures():
    for obj in list(bpy.context.scene.objects):
        if obj.type == "ARMATURE":
            bpy.data.objects.remove(obj, do_unlink=True)


def add_bone(armature, name, head, tail, parent=None, deform=True):
    bone = armature.edit_bones.new(name)
    bone.head = head
    bone.tail = tail
    bone.roll = 0.0
    bone.use_deform = deform
    if parent:
        bone.parent = armature.edit_bones[parent]
        bone.use_connect = False
    return bone


def create_finger_chain(armature, side, hand, side_sign, palm_forward, z_offsets, x_offset, base_parent):
    prefix = f"{side}Hand"
    base = hand + Vector((side_sign * x_offset, palm_forward, z_offsets[0]))
    second = hand + Vector((side_sign * (x_offset + 0.018), palm_forward + 0.025, z_offsets[1]))
    third = hand + Vector((side_sign * (x_offset + 0.032), palm_forward + 0.047, z_offsets[2]))
    tip = hand + Vector((side_sign * (x_offset + 0.044), palm_forward + 0.066, z_offsets[3]))
    add_bone(armature, f"{prefix}{base_parent}1", base, second, f"{side}Hand")
    add_bone(armature, f"{prefix}{base_parent}2", second, third, f"{prefix}{base_parent}1")
    add_bone(armature, f"{prefix}{base_parent}3", third, tip, f"{prefix}{base_parent}2")


def create_humanoid_armature(center, height, width):
    bpy.ops.object.armature_add(enter_editmode=True, location=(0, 0, 0))
    arm_obj = bpy.context.object
    arm_obj.name = "MinamiKotaro_Rerig_Armature"
    arm_data = arm_obj.data
    arm_data.name = "MinamiKotaro_Rerig_Armature"

    default_bone = arm_data.edit_bones[0]
    arm_data.edit_bones.remove(default_bone)

    x = center.x
    y = center.y
    half_shoulder = max(width * 0.18, height * 0.145)
    half_hip = max(width * 0.08, height * 0.055)
    side_span = max(width * 0.36, height * 0.34)

    hips = Vector((x, y, height * 0.53))
    spine = Vector((x, y, height * 0.62))
    chest = Vector((x, y, height * 0.73))
    upper_chest = Vector((x, y, height * 0.82))
    neck = Vector((x, y, height * 0.88))
    head = Vector((x, y, height * 0.965))

    add_bone(arm_data, "Hips", hips, spine)
    add_bone(arm_data, "Spine", spine, chest, "Hips")
    add_bone(arm_data, "Spine1", chest, upper_chest, "Spine")
    add_bone(arm_data, "Spine2", upper_chest, neck, "Spine1")
    add_bone(arm_data, "Neck", neck, head, "Spine2")
    add_bone(arm_data, "Head", head, Vector((x, y, height * 1.04)), "Neck")

    for side, sign in (("Left", 1), ("Right", -1)):
        shoulder = Vector((x + sign * half_shoulder * 0.55, y, height * 0.815))
        upper_arm = Vector((x + sign * half_shoulder, y, height * 0.79))
        forearm = Vector((x + sign * side_span * 0.72, y, height * 0.71))
        hand = Vector((x + sign * side_span, y, height * 0.63))
        hand_tip = Vector((x + sign * (side_span + height * 0.07), y, height * 0.60))

        add_bone(arm_data, f"{side}Shoulder", shoulder, upper_arm, "Spine2")
        add_bone(arm_data, f"{side}Arm", upper_arm, forearm, f"{side}Shoulder")
        add_bone(arm_data, f"{side}ForeArm", forearm, hand, f"{side}Arm")
        add_bone(arm_data, f"{side}Hand", hand, hand_tip, f"{side}ForeArm")

        create_finger_chain(arm_data, side, hand, sign, height * 0.012, (-0.004, -0.002, 0.0, 0.002), height * 0.018, "Thumb")
        create_finger_chain(arm_data, side, hand, sign, height * 0.02, (0.010, 0.010, 0.009, 0.008), height * 0.025, "Index")
        create_finger_chain(arm_data, side, hand, sign, height * 0.024, (0.000, 0.000, -0.001, -0.002), height * 0.008, "Middle")
        create_finger_chain(arm_data, side, hand, sign, height * 0.021, (-0.010, -0.011, -0.012, -0.013), -height * 0.008, "Ring")
        create_finger_chain(arm_data, side, hand, sign, height * 0.017, (-0.018, -0.020, -0.022, -0.024), -height * 0.022, "Pinky")

        upper_leg = Vector((x + sign * half_hip, y, height * 0.50))
        knee = Vector((x + sign * half_hip * 0.72, y, height * 0.285))
        ankle = Vector((x + sign * half_hip * 0.55, y, height * 0.055))
        toe = Vector((x + sign * half_hip * 0.55, y - height * 0.065, height * 0.025))
        toe_tip = Vector((x + sign * half_hip * 0.55, y - height * 0.125, height * 0.025))

        add_bone(arm_data, f"{side}UpLeg", upper_leg, knee, "Hips")
        add_bone(arm_data, f"{side}Leg", knee, ankle, f"{side}UpLeg")
        add_bone(arm_data, f"{side}Foot", ankle, toe, f"{side}Leg")
        add_bone(arm_data, f"{side}ToeBase", toe, toe_tip, f"{side}Foot")

    bpy.ops.object.mode_set(mode="OBJECT")
    arm_obj.show_in_front = True
    return arm_obj


def create_vertex_groups(mesh, armature):
    for bone in armature.data.bones:
        if bone.use_deform and bone.name not in mesh.vertex_groups:
            mesh.vertex_groups.new(name=bone.name)


def add_armature_modifier(mesh, armature):
    modifier = mesh.modifiers.new("MinamiKotaro_Rerig_Armature", "ARMATURE")
    modifier.object = armature


def try_auto_weights(mesh, armature):
    bpy.ops.object.select_all(action="DESELECT")
    mesh.select_set(True)
    armature.select_set(True)
    bpy.context.view_layer.objects.active = armature
    bpy.ops.object.parent_set(type="ARMATURE_AUTO")


def export_fbx(output_path, mesh, armature):
    bpy.ops.object.select_all(action="DESELECT")
    mesh.select_set(True)
    armature.select_set(True)
    bpy.context.view_layer.objects.active = armature
    bpy.ops.export_scene.fbx(
        filepath=str(output_path),
        use_selection=True,
        object_types={"ARMATURE", "MESH"},
        add_leaf_bones=False,
        bake_anim=False,
        axis_forward="-Z",
        axis_up="Y",
        use_armature_deform_only=True,
        apply_unit_scale=True,
        apply_scale_options="FBX_SCALE_UNITS",
    )


def save_blend(output_path):
    bpy.ops.wm.save_as_mainfile(filepath=str(output_path))


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--input", required=True)
    parser.add_argument("--output", required=True)
    parser.add_argument("--blend", required=True)
    parser.add_argument("--texture-dir")
    parser.add_argument("--auto-weights", action="store_true")
    argv = sys.argv[sys.argv.index("--") + 1 :] if "--" in sys.argv else []
    args = parser.parse_args(argv)

    input_path = Path(args.input).resolve()
    output_path = Path(args.output).resolve()
    blend_path = Path(args.blend).resolve()
    output_path.parent.mkdir(parents=True, exist_ok=True)
    blend_path.parent.mkdir(parents=True, exist_ok=True)

    clear_scene()
    import_fbx(input_path)
    rebind_material_textures(args.texture_dir)
    mesh = largest_mesh()
    mesh.name = "MinamiKotaro_Rerig_Mesh"
    mesh.data.name = "MinamiKotaro_Rerig_Mesh"
    center, height, width, _depth = normalize_mesh(mesh)
    delete_armatures()
    armature = create_humanoid_armature(center, height, width)
    create_vertex_groups(mesh, armature)
    add_armature_modifier(mesh, armature)

    if args.auto_weights:
        try_auto_weights(mesh, armature)

    save_blend(blend_path)
    export_fbx(output_path, mesh, armature)
    print(f"MINAMI_RERIG_OUTPUT={output_path}")
    print(f"MINAMI_RERIG_BLEND={blend_path}")
    print(f"MINAMI_RERIG_HEIGHT={height:.6f}")
    print(f"MINAMI_RERIG_AUTO_WEIGHTS={args.auto_weights}")


if __name__ == "__main__":
    main()
