"""Blender headless: import raw GLB, replace the baseColor image texture with
a corrected PNG (deterministic HSV recolor applied locally), re-export GLB.

Usage:
  blender --background --python swap_basecolor.py -- <in.glb> <corrected_basecolor.png> <out.glb>
"""
import bpy
import sys

argv = sys.argv
argv = argv[argv.index("--") + 1:]
in_glb, corrected_png, out_glb = argv[0], argv[1], argv[2]

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=in_glb)

new_img = bpy.data.images.load(corrected_png)

replaced = 0
for mat in bpy.data.materials:
    if not mat.use_nodes:
        continue
    for node in mat.node_tree.nodes:
        if node.type == "TEX_IMAGE" and node.image is not None:
            # baseColor feeds a Principled BSDF's Base Color input (not Normal/MR/Emission)
            is_base_color = False
            for link in mat.node_tree.links:
                if link.from_node == node and link.to_socket.name == "Base Color":
                    is_base_color = True
            if is_base_color:
                node.image = new_img
                replaced += 1

print("REPLACED_BASECOLOR_NODES:", replaced)
assert replaced > 0, "no baseColor image texture node found to replace"

bpy.ops.export_scene.gltf(filepath=out_glb, export_format="GLB")
print("EXPORTED:", out_glb)
