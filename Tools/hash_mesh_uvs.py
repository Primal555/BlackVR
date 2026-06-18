import hashlib
import sys

import bpy


fbx_path = sys.argv[sys.argv.index("--") + 1]

bpy.ops.object.select_all(action="SELECT")
bpy.ops.object.delete()
bpy.ops.import_scene.fbx(filepath=fbx_path)

for obj in bpy.context.scene.objects:
    if obj.type != "MESH":
        continue

    digest = hashlib.sha256()
    for poly in obj.data.polygons:
        digest.update(str(tuple(poly.vertices)).encode("utf-8"))
        for loop_index in poly.loop_indices:
            uv = obj.data.uv_layers.active.data[loop_index].uv if obj.data.uv_layers.active else None
            if uv:
                digest.update(f"{uv.x:.8f},{uv.y:.8f};".encode("utf-8"))

    print(f"MINAMI_MESH_UV_HASH={obj.name}:{digest.hexdigest()}")
