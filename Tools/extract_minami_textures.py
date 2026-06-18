import os
import sys

import bpy


fbx_path = sys.argv[sys.argv.index("--") + 1]
output_dir = sys.argv[sys.argv.index("--") + 2]

os.makedirs(output_dir, exist_ok=True)

bpy.ops.object.select_all(action="SELECT")
bpy.ops.object.delete()
bpy.ops.import_scene.fbx(filepath=fbx_path)

seen = set()
for image in bpy.data.images:
    if not image.name.lower().endswith((".png", ".jpg", ".jpeg", ".tga", ".bmp")):
        continue
    if image.name in seen:
        continue

    seen.add(image.name)
    output_path = os.path.join(output_dir, image.name)
    image.filepath_raw = output_path
    image.file_format = "PNG"
    image.save()
    print(f"EXTRACTED_TEXTURE={output_path}")
