import sys
import mathutils
import bpy

fbx_path = sys.argv[-1] if sys.argv[-2:-1] == ["--"] else r"E:\unity projects\KamenRider\Assets\Characters\MinamiKotaro\Models\kotaro_game.fbx"

try:
    import io_scene_fbx.import_fbx as import_fbx

    def skip_light_import(_fbx_tmpl, _fbx_obj, _settings):
        return bpy.data.lights.new("Skipped_FBX_Light", "POINT")

    import_fbx.blen_read_light = skip_light_import
except Exception as exc:
    print("LIGHT_PATCH_FAILED", exc)

if fbx_path.lower().endswith(".blend"):
    bpy.ops.wm.open_mainfile(filepath=fbx_path)
else:
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete()
    bpy.ops.import_scene.fbx(
        filepath=fbx_path,
        use_custom_normals=True,
        ignore_leaf_bones=False,
        automatic_bone_orientation=False,
    )

print("=== OBJECTS ===")
for obj in bpy.context.scene.objects:
    print(
        obj.name,
        obj.type,
        "hide=", obj.hide_get(),
        "visible=", obj.visible_get(),
        "scale=", tuple(round(value, 4) for value in obj.scale),
        "loc=", tuple(round(value, 4) for value in obj.location),
    )

print("=== MESH_DETAIL ===")
for obj in bpy.context.scene.objects:
    if obj.type != "MESH":
        continue

    depsgraph = bpy.context.evaluated_depsgraph_get()
    eval_obj = obj.evaluated_get(depsgraph)
    mesh = eval_obj.to_mesh()
    bbox = [eval_obj.matrix_world @ mathutils.Vector(corner) for corner in eval_obj.bound_box]
    bbox_min = mathutils.Vector((min(v.x for v in bbox), min(v.y for v in bbox), min(v.z for v in bbox)))
    bbox_max = mathutils.Vector((max(v.x for v in bbox), max(v.y for v in bbox), max(v.z for v in bbox)))
    modifiers = [
        (mod.name, mod.type, mod.object.name if getattr(mod, "object", None) else None)
        for mod in obj.modifiers
    ]
    nonzero_groups = 0
    for vertex_group in obj.vertex_groups:
        found = False
        for vertex in obj.data.vertices[:5000]:
            if any(group.group == vertex_group.index and group.weight > 0 for group in vertex.groups):
                found = True
                break
        if found:
            nonzero_groups += 1

    print(
        obj.name,
        "verts=", len(mesh.vertices),
        "polys=", len(mesh.polygons),
        "bbox_min=", tuple(round(value, 4) for value in bbox_min),
        "bbox_max=", tuple(round(value, 4) for value in bbox_max),
        "mods=", modifiers,
        "groups=", len(obj.vertex_groups),
        "nonzero_sample_groups=", nonzero_groups,
    )
    eval_obj.to_mesh_clear()

print("=== ARMATURE_DETAIL ===")
for obj in bpy.context.scene.objects:
    if obj.type != "ARMATURE":
        continue

    print(obj.name, "bones=", len(obj.data.bones), "display=", obj.data.display_type)
    for bone_name in [
        "root.x",
        "spine_03.x",
        "neck.x",
        "head.x",
        "shoulder.l",
        "arm_stretch.l",
        "forearm_stretch.l",
        "hand.l",
        "shoulder.r",
        "arm_stretch.r",
        "forearm_stretch.r",
        "hand.r",
    ]:
        bone = obj.data.bones.get(bone_name)
        if bone:
            print(
                "bone",
                bone_name,
                "parent=", bone.parent.name if bone.parent else None,
                "head=", tuple(round(value, 4) for value in bone.head_local),
                "tail=", tuple(round(value, 4) for value in bone.tail_local),
                "len=", round(bone.length, 4),
            )
