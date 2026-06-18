import os
import bpy

blend_path = r"E:\unity projects\KamenRider\Assets\Characters\MinamiKotaro\Models\Untitled.blend"
game_fbx_path = r"E:\unity projects\KamenRider\Assets\Characters\MinamiKotaro\Models\kotaro_game.fbx"
fixed_fbx_path = r"E:\unity projects\KamenRider\Assets\Characters\MinamiKotaro\Models\kotaro_game_fixed.fbx"

bpy.ops.object.select_all(action="SELECT")
bpy.ops.object.delete()

bpy.ops.import_scene.fbx(
    filepath=game_fbx_path,
    use_custom_normals=True,
    ignore_leaf_bones=False,
    automatic_bone_orientation=False,
)

game_armature = next(obj for obj in bpy.context.scene.objects if obj.type == "ARMATURE")
game_armature.name = "rig"
game_armature.data.name = "rig"

for obj in list(bpy.context.scene.objects):
    if obj.type == "MESH":
        bpy.data.objects.remove(obj, do_unlink=True)

with bpy.data.libraries.load(blend_path, link=False) as (data_from, data_to):
    data_to.objects = ["MinamiKotaro_Rerig_Mesh"]

mesh_obj = bpy.data.objects["MinamiKotaro_Rerig_Mesh"]
bpy.context.collection.objects.link(mesh_obj)

for modifier in list(mesh_obj.modifiers):
    mesh_obj.modifiers.remove(modifier)

armature_modifier = mesh_obj.modifiers.new("rig", "ARMATURE")
armature_modifier.object = game_armature
mesh_obj.parent = game_armature

bpy.ops.object.select_all(action="DESELECT")
game_armature.select_set(True)
mesh_obj.select_set(True)
bpy.context.view_layer.objects.active = game_armature

bpy.ops.export_scene.fbx(
    filepath=fixed_fbx_path,
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

print(f"EXPORTED {fixed_fbx_path}")
