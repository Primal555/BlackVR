import bpy
from pathlib import Path

root = Path(r"E:\unity projects\KamenRider")
blend_path = root / "Assets/Characters/MinamiKotaro/Models/MinamiKotaro_ARP_Player.blend"

bpy.ops.wm.open_mainfile(filepath=str(blend_path))

interesting = {
    "shoulder.l", "arm.l", "arm_stretch.l", "arm_twist.l", "arm_twist_twk.l",
    "forearm.l", "forearm_stretch.l", "forearm_twist.l",
    "c_arm_fk.l", "c_forearm_fk.l", "forearm_fk.l", "c_hand_fk.l",
    "shoulder.r", "arm.r", "arm_stretch.r", "arm_twist.r", "arm_twist_twk.r",
    "forearm.r", "forearm_stretch.r", "forearm_twist.r",
    "c_arm_fk.r", "c_forearm_fk.r", "forearm_fk.r", "c_hand_fk.r",
}

print("ARMATURES")
for armature in [obj for obj in bpy.context.scene.objects if obj.type == "ARMATURE"]:
    print(armature.name)
    for bone_name in sorted(interesting):
        bone = armature.data.bones.get(bone_name)
        if bone:
            parent = bone.parent.name if bone.parent else ""
            print(f"  bone {bone_name} parent={parent}")

print("MESH WEIGHTS")
for mesh in [obj for obj in bpy.context.scene.objects if obj.type == "MESH"]:
    stats = {group.name: [0, 0.0] for group in mesh.vertex_groups if group.name in interesting}
    if not stats:
        continue
    for vertex in mesh.data.vertices:
        for group_ref in vertex.groups:
            group = mesh.vertex_groups[group_ref.group]
            if group.name in stats and group_ref.weight > 0:
                stats[group.name][0] += 1
                stats[group.name][1] += group_ref.weight
    print(mesh.name)
    for name, (count, total) in sorted(stats.items()):
        if count:
            print(f"  {name}: vertices={count} total={total:.3f}")
