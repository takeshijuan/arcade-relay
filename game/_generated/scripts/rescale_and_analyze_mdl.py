"""Blender headless: import GLB, uniformly rescale to hit the art-bible authoring
height target (Meshy image-to-3d normalizes output size and ignores prompt-stated
real-world scale), re-export GLB, recompute mesh/material/bone stats + authoring
bbox, check non-manifold geometry, and render a style-check preview PNG.

Usage:
  blender --background --python rescale_and_analyze_mdl.py -- \
    <in.glb> <target_height_m> <out.glb> <out_metrics.json> <out_preview.png>

target_height_m is measured along the post-import Blender Z axis (up).
"""
import bpy
import bmesh
import json
import math
import sys
import os
import mathutils

argv = sys.argv
argv = argv[argv.index("--") + 1:]
in_glb, target_height_m, out_glb, out_json, out_png = argv[0], float(argv[1]), argv[2], argv[3], argv[4]

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=in_glb)

mesh_objs = [o for o in bpy.context.scene.objects if o.type == "MESH"]
armature_objs = [o for o in bpy.context.scene.objects if o.type == "ARMATURE"]


def world_bbox():
    min_co = [math.inf, math.inf, math.inf]
    max_co = [-math.inf, -math.inf, -math.inf]
    for obj in mesh_objs:
        for v in obj.bound_box:
            wv = obj.matrix_world @ mathutils.Vector(v)
            for i in range(3):
                min_co[i] = min(min_co[i], wv[i])
                max_co[i] = max(max_co[i], wv[i])
    return min_co, max_co


min_co, max_co = world_bbox()
current_height = max_co[2] - min_co[2]
scale_factor = target_height_m / current_height if current_height > 0 else 1.0

# Apply uniform scale about the world origin's XY center, floor (min Z) grounded at Z=0
center_xy = ((min_co[0] + max_co[0]) / 2, (min_co[1] + max_co[1]) / 2)
for obj in mesh_objs + armature_objs:
    obj.select_set(True)
bpy.context.view_layer.objects.active = mesh_objs[0] if mesh_objs else None

for obj in mesh_objs + armature_objs:
    # scale about world origin, then translate so min corner sits on ground plane, centered at XY origin
    obj.scale = (obj.scale[0] * scale_factor, obj.scale[1] * scale_factor, obj.scale[2] * scale_factor)
    obj.location = (
        (obj.location[0] - center_xy[0]) * scale_factor + center_xy[0] * scale_factor,
        (obj.location[1] - center_xy[1]) * scale_factor + center_xy[1] * scale_factor,
        obj.location[2] * scale_factor,
    )

bpy.ops.object.select_all(action="DESELECT")
for obj in mesh_objs + armature_objs:
    obj.select_set(True)
bpy.context.view_layer.objects.active = mesh_objs[0] if mesh_objs else armature_objs[0]
bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)

# Recompute bbox after scale, then ground it (min Z -> 0) and center XY at origin
min_co, max_co = world_bbox()
shift = (-((min_co[0] + max_co[0]) / 2), -((min_co[1] + max_co[1]) / 2), -min_co[2])
for obj in mesh_objs + armature_objs:
    obj.location = (obj.location[0] + shift[0], obj.location[1] + shift[1], obj.location[2] + shift[2])
bpy.context.view_layer.update()

bpy.ops.object.select_all(action="DESELECT")
for obj in mesh_objs + armature_objs:
    obj.select_set(True)
bpy.context.view_layer.objects.active = mesh_objs[0] if mesh_objs else armature_objs[0]
bpy.ops.object.transform_apply(location=True, rotation=False, scale=False)

# Export rescaled GLB
bpy.ops.export_scene.gltf(filepath=out_glb, export_format="GLB")

# --- Recompute metrics on rescaled mesh ---
total_tris = 0
total_verts = 0
non_manifold_verts = 0
materials = set()
min_co = [math.inf, math.inf, math.inf]
max_co = [-math.inf, -math.inf, -math.inf]

for obj in mesh_objs:
    for mat in obj.data.materials:
        if mat is not None:
            materials.add(mat.name)

    depsgraph = bpy.context.evaluated_depsgraph_get()
    eval_obj = obj.evaluated_get(depsgraph)
    eval_mesh = eval_obj.to_mesh()

    bm = bmesh.new()
    bm.from_mesh(eval_mesh)
    bmesh.ops.triangulate(bm, faces=bm.faces[:])
    total_tris += len(bm.faces)
    total_verts += len(bm.verts)

    bm.verts.ensure_lookup_table()
    for v in bm.verts:
        if not v.is_manifold:
            non_manifold_verts += 1
    bm.free()
    eval_obj.to_mesh_clear()

    for v in obj.bound_box:
        wv = obj.matrix_world @ mathutils.Vector(v)
        for i in range(3):
            min_co[i] = min(min_co[i], wv[i])
            max_co[i] = max(max_co[i], wv[i])

bone_count = sum(len(a.data.bones) for a in armature_objs)
bbox_size = [max_co[i] - min_co[i] for i in range(3)] if mesh_objs else [0, 0, 0]

metrics = {
    "file": os.path.basename(out_glb),
    "source_file": os.path.basename(in_glb),
    "scale_factor_applied": round(scale_factor, 6),
    "triangle_count": total_tris,
    "vertex_count": total_verts,
    "material_count": len(materials),
    "material_names": sorted(materials),
    "bone_count": bone_count,
    "non_manifold_verts": non_manifold_verts,
    "bbox_authoring_m": [round(bbox_size[0], 4), round(bbox_size[1], 4), round(bbox_size[2], 4)],
    "bbox_min": [round(x, 4) for x in min_co],
    "bbox_max": [round(x, 4) for x in max_co],
    "mesh_object_count": len(mesh_objs),
}
with open(out_json, "w") as f:
    json.dump(metrics, f, indent=2)
print("METRICS:", json.dumps(metrics))

# --- Preview render (elevated 3/4 view, soft two-light studio setup) ---
scene = bpy.context.scene
try:
    scene.render.engine = "BLENDER_EEVEE_NEXT"
except TypeError:
    scene.render.engine = "BLENDER_EEVEE"

scene.render.resolution_x = 768
scene.render.resolution_y = 768
world = bpy.data.worlds.new("World")
world.use_nodes = True
world.node_tree.nodes["Background"].inputs[0].default_value = (1.0, 1.0, 1.0, 1.0)
world.node_tree.nodes["Background"].inputs[1].default_value = 1.0
scene.world = world

center = [(min_co[i] + max_co[i]) / 2 for i in range(3)]
radius = max(bbox_size) if max(bbox_size) > 0 else 1.0

cam_data = bpy.data.cameras.new("PreviewCam")
cam_obj = bpy.data.objects.new("PreviewCam", cam_data)
scene.collection.objects.link(cam_obj)
scene.camera = cam_obj

dist = radius * 2.4
angle = math.radians(40.0)
cam_obj.location = (
    center[0] + dist * math.cos(angle),
    center[1] - dist * math.cos(angle),
    center[2] + dist * math.sin(angle),
)
direction = mathutils.Vector(center) - mathutils.Vector(cam_obj.location)
cam_obj.rotation_euler = direction.to_track_quat("-Z", "Y").to_euler()
cam_data.lens = 50

key = bpy.data.lights.new("KeyLight", type="SUN")
key.energy = 3.0
key_obj = bpy.data.objects.new("KeyLight", key)
scene.collection.objects.link(key_obj)
key_obj.rotation_euler = (math.radians(55), 0, math.radians(-35))

fill = bpy.data.lights.new("FillLight", type="SUN")
fill.energy = 1.0
fill_obj = bpy.data.objects.new("FillLight", fill)
scene.collection.objects.link(fill_obj)
fill_obj.rotation_euler = (math.radians(70), 0, math.radians(145))

scene.render.filepath = out_png
bpy.ops.render.render(write_still=True)
print("RENDERED:", out_png)
