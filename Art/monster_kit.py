# Shared helpers for the humanoid monster builds (standing skin-modifier body, head, rig, FBX export).
import bpy, bmesh, math, os, random
from mathutils import Vector

def mat(name, color, rough=0.5):
    m = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    m.use_nodes = True
    p = next(n for n in m.node_tree.nodes if n.type == "BSDF_PRINCIPLED")
    p.inputs["Base Color"].default_value = (*color, 1)
    p.inputs["Roughness"].default_value = rough
    return m

def collection(name):
    col = bpy.data.collections.get(name)
    if col:
        for o in list(col.objects): bpy.data.objects.remove(o, do_unlink=True)
    else:
        col = bpy.data.collections.new(name); bpy.context.scene.collection.children.link(col)
    return col

def humanoid(prefix, col, s, arm_len, thin, neck_len, finger=0.07, finger_r=0.011, subdiv=2):
    """A standing humanoid from a skin-modifier skeleton. s = overall scale (1 = 1.75 m person)."""
    def link(o):
        for c in o.users_collection: c.objects.unlink(o)
        col.objects.link(o); return o
    P = {
        "pelvis": ((0, 0, 0.98), 0.14), "spine": ((0, 0.01, 1.15), 0.12 * thin), "chest": ((0, 0, 1.33), 0.17 * thin),
        "upchest": ((0, -0.01, 1.47), 0.15 * thin), "neck0": ((0, 0, 1.52), 0.05), "neck1": ((0, -0.01, 1.52 + neck_len), 0.045),
    }
    for sd, x in (("L", 1), ("R", -1)):
        wz = 0.98 - (arm_len - 1) * 0.6
        P.update({
            f"shoulder{sd}": ((0.2 * x, 0, 1.45), 0.06), f"elbow{sd}": ((0.25 * x, 0.03, 1.45 - 0.28 * arm_len), 0.042),
            f"wrist{sd}": ((0.27 * x, -0.01, 1.45 - 0.53 * arm_len), 0.032), f"palm{sd}": ((0.275 * x, -0.02, 1.45 - 0.6 * arm_len), 0.035),
            f"hip{sd}": ((0.1 * x, 0, 0.93), 0.075), f"knee{sd}": ((0.11 * x, -0.02, 0.5), 0.055),
            f"ankle{sd}": ((0.11 * x, 0.02, 0.08), 0.04), f"ball{sd}": ((0.11 * x, -0.12, 0.03), 0.04),
        })
        for i, dx in enumerate((-0.03, -0.01, 0.01, 0.03)):
            fl = finger * arm_len
            P[f"f{i}{sd}"] = ((0.275 * x + dx * 1.6 * x, -0.03 - fl * 0.25, 1.45 - 0.6 * arm_len - fl), finger_r)
    E = [("pelvis", "spine"), ("spine", "chest"), ("chest", "upchest"), ("upchest", "neck0"), ("neck0", "neck1")]
    for sd in "LR":
        E += [("upchest", f"shoulder{sd}"), (f"shoulder{sd}", f"elbow{sd}"), (f"elbow{sd}", f"wrist{sd}"), (f"wrist{sd}", f"palm{sd}"),
              ("pelvis", f"hip{sd}"), (f"hip{sd}", f"knee{sd}"), (f"knee{sd}", f"ankle{sd}"), (f"ankle{sd}", f"ball{sd}")]
        E += [(f"palm{sd}", f"f{i}{sd}") for i in range(4)]
    names = list(P)
    me = bpy.data.meshes.new(prefix + "Body")
    me.from_pydata([tuple(c * s for c in P[n][0]) for n in names], [(names.index(a), names.index(b)) for a, b in E], [])
    body = link(bpy.data.objects.new(prefix + "Body", me))
    sk = body.modifiers.new("Skin", "SKIN"); sk.use_smooth_shade = True; sk.branch_smoothing = 0.6
    for i, n in enumerate(names):
        r = P[n][1] * s
        body.data.skin_vertices[0].data[i].radius = (r, r * 0.85)
    body.data.skin_vertices[0].data[names.index("pelvis")].use_root = True
    body.modifiers.new("Sub", "SUBSURF").levels = subdiv
    bpy.context.view_layer.objects.active = body
    for m in list(body.modifiers): bpy.ops.object.modifier_apply(modifier=m.name)
    _nb = bmesh.new(); _nb.from_mesh(body.data); bmesh.ops.recalc_face_normals(_nb, faces=_nb.faces); _nb.to_mesh(body.data); _nb.free()   # the skin modifier leaves some faces inside-out
    pos = {n: Vector(P[n][0]) * s for n in names}
    return body, pos, link

def head(prefix, link, center, r, scale):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=48, ring_count=32, radius=r, location=center)
    h = link(bpy.context.object); h.name = prefix + "Head"
    h.scale = scale
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    bpy.ops.object.shade_smooth()
    return h

def rig(prefix, col, link, pos, top, parts_on_head, meshes):
    arm = bpy.data.armatures.new(prefix + "Rig")
    rg = link(bpy.data.objects.new(prefix + "Rig", arm))
    bpy.context.view_layer.objects.active = rg
    bpy.ops.object.mode_set(mode="EDIT")
    def bone(name, a, b, parent=None):
        eb = arm.edit_bones.new(name)
        eb.head = pos[a] if isinstance(a, str) else a
        eb.tail = pos[b] if isinstance(b, str) else b
        if parent: eb.parent = arm.edit_bones[parent]
    bone("Hips", "pelvis", "spine"); bone("Spine", "spine", "chest", "Hips"); bone("Chest", "chest", "upchest", "Spine")
    bone("Neck", "neck0", "neck1", "Chest"); bone("Head", "neck1", top, "Neck")
    for sd in "LR":
        bone(f"UpperArm.{sd}", f"shoulder{sd}", f"elbow{sd}", "Chest")
        bone(f"Forearm.{sd}", f"elbow{sd}", f"wrist{sd}", f"UpperArm.{sd}")
        bone(f"Hand.{sd}", f"wrist{sd}", f"f1{sd}", f"Forearm.{sd}")
        bone(f"Thigh.{sd}", f"hip{sd}", f"knee{sd}", "Hips")
        bone(f"Shin.{sd}", f"knee{sd}", f"ankle{sd}", f"Thigh.{sd}")
        bone(f"Foot.{sd}", f"ankle{sd}", f"ball{sd}", f"Shin.{sd}")
    bpy.ops.object.mode_set(mode="OBJECT")
    bpy.ops.object.select_all(action="DESELECT")
    for m in meshes: m.select_set(True)
    rg.select_set(True); bpy.context.view_layer.objects.active = rg
    bpy.ops.object.parent_set(type="ARMATURE_AUTO")
    for o, b in parts_on_head:
        bpy.context.view_layer.update()
        mw = o.matrix_world.copy()
        o.parent = rg; o.parent_type = "BONE"; o.parent_bone = b
        o.matrix_world = mw
    return rg

def export(col, path):
    bpy.ops.object.select_all(action="DESELECT")
    for o in col.objects: o.select_set(True)
    bpy.ops.export_scene.fbx(filepath=path, use_selection=True, object_types={"ARMATURE", "MESH"},
                             apply_unit_scale=True, apply_scale_options="FBX_SCALE_ALL", axis_forward="-Z", axis_up="Y",
                             add_leaf_bones=False, bake_anim=False, mesh_smooth_type="FACE", use_armature_deform_only=True)

