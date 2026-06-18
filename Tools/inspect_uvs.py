import json
import sys

import bpy


fbx_path = sys.argv[sys.argv.index("--") + 1]

bpy.ops.object.select_all(action="SELECT")
bpy.ops.object.delete()
bpy.ops.import_scene.fbx(filepath=fbx_path)

result = []
for obj in bpy.context.scene.objects:
    if obj.type != "MESH":
        continue

    uv_layers = []
    for layer in obj.data.uv_layers:
        values = [loop.uv.copy() for loop in layer.data]
        if values:
            min_u = min(value.x for value in values)
            max_u = max(value.x for value in values)
            min_v = min(value.y for value in values)
            max_v = max(value.y for value in values)
        else:
            min_u = max_u = min_v = max_v = 0

        uv_layers.append(
            {
                "name": layer.name,
                "loop_count": len(layer.data),
                "u_range": [round(min_u, 6), round(max_u, 6)],
                "v_range": [round(min_v, 6), round(max_v, 6)],
            }
        )

    result.append(
        {
            "mesh": obj.name,
            "vertices": len(obj.data.vertices),
            "polygons": len(obj.data.polygons),
            "uv_layers": uv_layers,
        }
    )

print("MINAMI_UV_JSON=" + json.dumps(result, indent=2))
