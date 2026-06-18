import json
import sys

import bpy


argv = sys.argv[sys.argv.index("--") + 1 :] if "--" in sys.argv else []
fbx_path = argv[0]

bpy.ops.object.select_all(action="SELECT")
bpy.ops.object.delete()
bpy.ops.import_scene.fbx(filepath=fbx_path)

result = []
for obj in bpy.context.scene.objects:
    if obj.type != "MESH":
        continue

    non_empty = set()
    for vertex in obj.data.vertices:
        for group in vertex.groups:
            if group.weight > 0:
                non_empty.add(group.group)

    weighted_names = [obj.vertex_groups[index].name for index in sorted(non_empty)]
    result.append(
        {
            "mesh": obj.name,
            "vertex_groups": len(obj.vertex_groups),
            "weighted_vertex_groups": len(non_empty),
            "weighted_groups": weighted_names,
            "armatures": [mod.object.name if mod.object else None for mod in obj.modifiers if mod.type == "ARMATURE"],
        }
    )

print("MINAMI_SKIN_JSON=" + json.dumps(result, indent=2))
