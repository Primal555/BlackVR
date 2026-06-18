import bpy

source_fbx_path = r"E:\unity projects\KamenRider\Assets\Characters\MinamiKotaro\Models\kotarotest.fbx"
clean_fbx_path = r"E:\unity projects\KamenRider\Assets\Characters\MinamiKotaro\Models\kotarotest_clean.fbx"

try:
    import io_scene_fbx.import_fbx as import_fbx

    def skip_light_import(_fbx_tmpl, _fbx_obj, _settings):
        return bpy.data.lights.new("Skipped_FBX_Light", "POINT")

    import_fbx.blen_read_light = skip_light_import
except Exception as exc:
    print("LIGHT_PATCH_FAILED", exc)

bpy.ops.object.select_all(action="SELECT")
bpy.ops.object.delete()
bpy.ops.import_scene.fbx(
    filepath=source_fbx_path,
    use_custom_normals=True,
    ignore_leaf_bones=False,
    automatic_bone_orientation=False,
)

target_armature = bpy.data.objects.get("rig_humanoid")
target_mesh = bpy.data.objects.get("MinamiKotaro_Rerig_Mesh")

if target_armature is None:
    raise RuntimeError("rig_humanoid not found")

if target_mesh is None:
    raise RuntimeError("MinamiKotaro_Rerig_Mesh not found")

for obj in list(bpy.context.scene.objects):
    if obj not in {target_armature, target_mesh}:
        bpy.data.objects.remove(obj, do_unlink=True)

for modifier in list(target_mesh.modifiers):
    if modifier.type != "ARMATURE":
        target_mesh.modifiers.remove(modifier)

armature_modifier = next((modifier for modifier in target_mesh.modifiers if modifier.type == "ARMATURE"), None)
if armature_modifier is None:
    armature_modifier = target_mesh.modifiers.new("rig_humanoid", "ARMATURE")

armature_modifier.object = target_armature
target_mesh.parent = target_armature

bpy.ops.object.select_all(action="DESELECT")
target_armature.select_set(True)
target_mesh.select_set(True)
bpy.context.view_layer.objects.active = target_armature

bpy.ops.export_scene.fbx(
    filepath=clean_fbx_path,
    use_selection=True,
    object_types={"ARMATURE", "MESH"},
    apply_unit_scale=True,
    apply_scale_options="FBX_SCALE_UNITS",
    bake_space_transform=False,
    use_mesh_modifiers=False,
    mesh_smooth_type="OFF",
    use_subsurf=False,
    add_leaf_bones=False,
    primary_bone_axis="Y",
    secondary_bone_axis="X",
    use_armature_deform_only=False,
    armature_nodetype="NULL",
    bake_anim=False,
    path_mode="AUTO",
)

print(f"EXPORTED {clean_fbx_path}")
