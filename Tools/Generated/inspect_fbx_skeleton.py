import bpy
import sys
from pathlib import Path

fbx_path = Path(sys.argv[-1])

try:
    import io_scene_fbx.import_fbx as import_fbx

    def skip_light_import(_fbx_tmpl, _fbx_obj, _settings):
        return bpy.data.lights.new("Skipped_FBX_Light", "POINT")

    import_fbx.blen_read_light = skip_light_import
except Exception as exc:
    print("LIGHT_PATCH_FAILED", exc)

bpy.ops.object.select_all(action="SELECT")
bpy.ops.object.delete()
bpy.ops.import_scene.fbx(filepath=str(fbx_path))

armatures = [obj for obj in bpy.context.scene.objects if obj.type == "ARMATURE"]
meshes = [obj for obj in bpy.context.scene.objects if obj.type == "MESH"]
lights = [obj for obj in bpy.context.scene.objects if obj.type == "LIGHT"]
cameras = [obj for obj in bpy.context.scene.objects if obj.type == "CAMERA"]

print(f"FBX {fbx_path.name}")
print(f"OBJECTS armatures={len(armatures)} meshes={len(meshes)} lights={len(lights)} cameras={len(cameras)}")

for armature in armatures:
    names = [bone.name for bone in armature.data.bones]
    control_like = [name for name in names if name.startswith("c_") or "_ik" in name or "_fk" in name or "stretch" in name or "twist" in name or name.startswith("cs_")]
    humanoid_like = [name for name in names if name.lower() in {
        "hips", "spine", "chest", "neck", "head",
        "leftupperarm", "leftlowerarm", "lefthand",
        "rightupperarm", "rightlowerarm", "righthand",
        "upper_arm.l", "forearm.l", "hand.l",
        "upper_arm.r", "forearm.r", "hand.r",
        "arm.l", "forearm.l", "hand.l",
        "arm.r", "forearm.r", "hand.r",
    }]
    print(f"ARMATURE {armature.name} bones={len(names)} control_like={len(control_like)} humanoid_like={len(humanoid_like)}")
    print("FIRST_BONES " + ", ".join(names[:60]))
    if control_like:
        print("CONTROL_SAMPLE " + ", ".join(control_like[:80]))
    for key_name in [
        "root.x", "spine_01.x", "spine_02.x", "spine_03.x", "neck.x", "head.x",
        "shoulder.l", "arm_stretch.l", "forearm_stretch.l", "hand.l",
        "shoulder.r", "arm_stretch.r", "forearm_stretch.r", "hand.r",
    ]:
        bone = armature.data.bones.get(key_name)
        if bone:
            parent = bone.parent.name if bone.parent else ""
            print(f"KEY_PARENT {key_name} <- {parent}")

interesting = [
    "shoulder.l", "arm.l", "arm_stretch.l", "arm_twist.l", "forearm.l", "forearm_stretch.l", "forearm_twist.l", "hand.l",
    "shoulder.r", "arm.r", "arm_stretch.r", "arm_twist.r", "forearm.r", "forearm_stretch.r", "forearm_twist.r", "hand.r",
    "upper_arm.l", "upper_arm.r", "forearm.l", "forearm.r", "hand.l", "hand.r",
    "LeftUpperArm", "LeftLowerArm", "LeftHand", "RightUpperArm", "RightLowerArm", "RightHand",
]

print("WEIGHT_GROUPS")
for mesh in meshes:
    group_names = {group.name for group in mesh.vertex_groups}
    hits = [name for name in interesting if name in group_names]
    if hits:
        print(f"MESH {mesh.name}: " + ", ".join(hits))
