import json
import sys

import bpy


fbx_path = sys.argv[sys.argv.index("--") + 1]

bpy.ops.object.select_all(action="SELECT")
bpy.ops.object.delete()
bpy.ops.import_scene.fbx(filepath=fbx_path)

materials = []
for material in bpy.data.materials:
    entry = {
        "name": material.name,
        "use_nodes": material.use_nodes,
        "base_color": list(material.diffuse_color),
        "textures": [],
    }

    if material.use_nodes and material.node_tree:
        for node in material.node_tree.nodes:
            if node.type == "TEX_IMAGE" and node.image:
                entry["textures"].append(
                    {
                        "node": node.name,
                        "image": node.image.name,
                        "filepath": bpy.path.abspath(node.image.filepath),
                        "packed": node.image.packed_file is not None,
                        "size": list(node.image.size),
                    }
                )

    materials.append(entry)

meshes = []
for obj in bpy.context.scene.objects:
    if obj.type == "MESH":
        meshes.append(
            {
                "name": obj.name,
                "vertices": len(obj.data.vertices),
                "polygons": len(obj.data.polygons),
                "material_slots": [slot.material.name if slot.material else None for slot in obj.material_slots],
                "vertex_color_layers": [layer.name for layer in obj.data.color_attributes],
                "armature_modifiers": [mod.object.name if mod.object else None for mod in obj.modifiers if mod.type == "ARMATURE"],
            }
        )

armatures = []
for obj in bpy.context.scene.objects:
    if obj.type != "ARMATURE":
        continue

    bones = []
    for bone in obj.data.bones:
        bones.append(
            {
                "name": bone.name,
                "parent": bone.parent.name if bone.parent else None,
                "head": [round(value, 6) for value in bone.head_local],
                "tail": [round(value, 6) for value in bone.tail_local],
            }
        )

    armatures.append(
        {
            "name": obj.name,
            "location": [round(value, 6) for value in obj.location],
            "rotation_euler": [round(value, 6) for value in obj.rotation_euler],
            "scale": [round(value, 6) for value in obj.scale],
            "bone_count": len(obj.data.bones),
            "bones": bones[:80],
        }
    )

print(
    "MINAMI_INSPECT_JSON="
    + json.dumps(
        {
            "materials": materials,
            "meshes": meshes,
            "armatures": armatures,
        },
        ensure_ascii=False,
        indent=2,
    )
)
