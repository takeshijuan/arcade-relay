"""Blender headless: decimate a GLB's mesh(es) to a target max triangle count
(gltfpack -si substitute — gltfpack is not installed in this environment).

Usage:
  blender --background --python decimate_mdl.py -- <in.glb> <target_tris> <out.glb>
"""
import bpy
import bmesh
import sys

argv = sys.argv
argv = argv[argv.index("--") + 1:]
in_glb, target_tris, out_glb = argv[0], int(argv[1]), argv[2]

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=in_glb)

mesh_objs = [o for o in bpy.context.scene.objects if o.type == "MESH"]


def count_tris():
    total = 0
    for obj in mesh_objs:
        depsgraph = bpy.context.evaluated_depsgraph_get()
        eval_obj = obj.evaluated_get(depsgraph)
        eval_mesh = eval_obj.to_mesh()
        bm = bmesh.new()
        bm.from_mesh(eval_mesh)
        bmesh.ops.triangulate(bm, faces=bm.faces[:])
        total += len(bm.faces)
        bm.free()
        eval_obj.to_mesh_clear()
    return total


current = count_tris()
print("BEFORE_TRIS:", current)
if current > target_tris:
    ratio = target_tris / current
    for obj in mesh_objs:
        bpy.context.view_layer.objects.active = obj
        mod = obj.modifiers.new(name="Decimate", type="DECIMATE")
        mod.ratio = ratio
        bpy.ops.object.modifier_apply(modifier=mod.name)

after = count_tris()
print("AFTER_TRIS:", after)

bpy.ops.export_scene.gltf(filepath=out_glb, export_format="GLB")
print("EXPORTED:", out_glb)
