import bpy

blend_path = r"E:\unity projects\KamenRider\Assets\Characters\MinamiKotaro\Models\Untitled.blend"
fbx_path = r"E:\unity projects\KamenRider\Assets\Characters\MinamiKotaro\Models\kotaro_unity_direct.fbx"

bpy.ops.wm.open_mainfile(filepath=blend_path)
bpy.ops.object.mode_set(mode="OBJECT") if bpy.ops.object.mode_set.poll() else None
bpy.ops.object.select_all(action="DESELECT")

selected_names = ["rig", "MinamiKotaro_Rerig_Mesh"]
for name in selected_names:
    obj = bpy.data.objects.get(name)
    if obj:
        obj.select_set(True)
        bpy.context.view_layer.objects.active = obj

bpy.ops.export_scene.fbx(
    filepath=fbx_path,
    use_selection=True,
    object_types={"ARMATURE", "MESH"},
    apply_unit_scale=True,
    apply_scale_options="FBX_SCALE_UNITS",
    bake_space_transform=False,
    use_mesh_modifiers=True,
    mesh_smooth_type="OFF",
    use_subsurf=False,
    add_leaf_bones=False,
    primary_bone_axis="Y",
    secondary_bone_axis="X",
    use_armature_deform_only=True,
    armature_nodetype="NULL",
    bake_anim=False,
    path_mode="AUTO",
)

print(f"EXPORTED {fbx_path}")
