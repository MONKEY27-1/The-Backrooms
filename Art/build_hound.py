# Builds the Hound (wiki Entity 8) from scratch: a starved human body built to run on all fours, long clawed hands,
# a skull with a separate, far-too-big jaw, and long black hair hanging over the face. Rig with named bones for
# code animation (quadruped gait, lunge, jaw).
# Run: blender -b Art/Monsters.blend --python Art/build_hound.py   (rebuilds collection "Hound", exports FBX)
import bpy, bmesh, math, os, random
from mathutils import Vector

HERE = os.path.dirname(bpy.data.filepath) or os.path.dirname(os.path.abspath(__file__))
OUT = os.path.abspath(os.path.join(HERE, "..", "Assets", "_Game", "Resources", "Monsters", "Hound.fbx"))
random.seed(8)

def mat(name, color, rough=0.5, emit=None, strength=0.0):
    m = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    m.use_nodes = True
    p = next(n for n in m.node_tree.nodes if n.type == "BSDF_PRINCIPLED")
    p.inputs["Base Color"].default_value = (*color, 1)
    p.inputs["Roughness"].default_value = rough
    return m

col = bpy.data.collections.get("Hound")
if col:
    for o in list(col.objects): bpy.data.objects.remove(o, do_unlink=True)
else:
    col = bpy.data.collections.new("Hound"); bpy.context.scene.collection.children.link(col)
def link(o):
    for c in o.users_collection: c.objects.unlink(o)
    col.objects.link(o)
    return o

skin_m = mat("HoundSkin", (0.42, 0.38, 0.34), 0.45)
hair_m = mat("HoundHair", (0.01, 0.01, 0.012), 0.6)
teeth_m = mat("HoundTeeth", (0.75, 0.7, 0.58), 0.3)
claw_m = mat("HoundClaw", (0.08, 0.07, 0.06), 0.35)
mouth_m = mat("HoundMouth", (0.05, 0.0, 0.0), 0.8)

# ---------------- body skeleton: on all fours, facing -Y ----------------
P = {
    "pelvis": ((0, 0.42, 0.80), 0.11), "spine": ((0, 0.12, 0.84), 0.075),
    "chest": ((0, -0.20, 0.80), 0.14), "upchest": ((0, -0.40, 0.76), 0.13),
    "neck0": ((0, -0.52, 0.76), 0.05), "neck1": ((0, -0.64, 0.72), 0.045),
}
for s, x in (("L", 1), ("R", -1)):
    P.update({
        f"shoulder{s}": ((0.20 * x, -0.42, 0.74), 0.055), f"elbow{s}": ((0.30 * x, -0.48, 0.42), 0.04),
        f"wrist{s}": ((0.27 * x, -0.66, 0.07), 0.028), f"palm{s}": ((0.27 * x, -0.74, 0.03), 0.035),
        f"hip{s}": ((0.13 * x, 0.45, 0.76), 0.07), f"knee{s}": ((0.20 * x, 0.18, 0.42), 0.05),
        f"ankle{s}": ((0.18 * x, 0.62, 0.16), 0.033), f"ball{s}": ((0.18 * x, 0.52, 0.02), 0.03),
    })
    for i, dx in enumerate((-0.04, -0.013, 0.013, 0.04)):   # long fingers splayed on the floor
        P[f"f{i}a{s}"] = ((0.27 * x + dx * x, -0.82, 0.025), 0.011)
        P[f"f{i}b{s}"] = ((0.27 * x + dx * 1.6 * x, -0.93, 0.012), 0.008)
    for i, dx in enumerate((-0.025, 0.0, 0.025)):
        P[f"t{i}{s}"] = ((0.18 * x + dx * x, 0.44, 0.012), 0.01)
E = [("pelvis", "spine"), ("spine", "chest"), ("chest", "upchest"), ("upchest", "neck0"), ("neck0", "neck1")]
for s in "LR":
    E += [("upchest", f"shoulder{s}"), (f"shoulder{s}", f"elbow{s}"), (f"elbow{s}", f"wrist{s}"), (f"wrist{s}", f"palm{s}"),
          ("pelvis", f"hip{s}"), (f"hip{s}", f"knee{s}"), (f"knee{s}", f"ankle{s}"), (f"ankle{s}", f"ball{s}")]
    E += [(f"palm{s}", f"f{i}a{s}") for i in range(4)] + [(f"f{i}a{s}", f"f{i}b{s}") for i in range(4)]
    E += [(f"ball{s}", f"t{i}{s}") for i in range(3)]
for a, b in list(E):   # bony joints
    if a.rstrip("LR") in ("shoulder", "elbow", "hip", "knee") or a == "spine":
        m = f"mid_{a}_{b}"
        P[m] = (tuple((Vector(P[a][0]) + Vector(P[b][0])) / 2), min(P[a][1], P[b][1]) * 0.65)
        E.remove((a, b)); E += [(a, m), (m, b)]

names = list(P)
me = bpy.data.meshes.new("HoundBody")
me.from_pydata([P[n][0] for n in names], [(names.index(a), names.index(b)) for a, b in E], [])
body = link(bpy.data.objects.new("HoundBody", me))
sk = body.modifiers.new("Skin", "SKIN"); sk.use_smooth_shade = True; sk.branch_smoothing = 0.6
for i, n in enumerate(names):
    body.data.skin_vertices[0].data[i].radius = (P[n][1], P[n][1] * 0.85)
body.data.skin_vertices[0].data[names.index("pelvis")].use_root = True
body.modifiers.new("Sub", "SUBSURF").levels = 2
bpy.context.view_layer.objects.active = body
for m in list(body.modifiers): bpy.ops.object.modifier_apply(modifier=m.name)
_nb = bmesh.new(); _nb.from_mesh(body.data); bmesh.ops.recalc_face_normals(_nb, faces=_nb.faces); _nb.to_mesh(body.data); _nb.free()   # the skin modifier leaves some faces inside-out
bm = bmesh.new(); bm.from_mesh(body.data); bm.normal_update()
for v in bm.verts:
    x, y, z = v.co
    push = 0.0
    if -0.42 < y < 0.0 and z < 0.8 and 0.04 < abs(x) < 0.16:   # ribs hanging under the chest
        push += 0.012 * max(0.0, math.sin(y * 110.0)) - 0.004
    if -0.45 < y < 0.45 and z > 0.84 and abs(x) < 0.035:       # spine knuckles along the back
        push += 0.02 * max(0.0, math.sin(y * 60.0))
    v.co += v.normal * (push + random.uniform(-0.002, 0.002))
bm.to_mesh(body.data); bm.free()
body.data.materials.append(skin_m)

# ---------------- head: skull + a jaw far too big for it ----------------
C = Vector((0, -0.76, 0.70))
def sphere(name, r, scale, loc, m, seg=40, rings=24):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=seg, ring_count=rings, radius=r, location=loc)
    o = link(bpy.context.object); o.name = name
    o.scale = scale
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    o.data.materials.append(m); bpy.ops.object.shade_smooth()
    return o
skull = sphere("Skull", 0.11, (0.78, 1.25, 0.85), C + Vector((0, 0.02, 0.05)), skin_m)
jaw = sphere("Jaw", 0.1, (1.05, 1.7, 0.75), C + Vector((0, 0.0, -0.08)), skin_m)
# hollow the top of the jaw: a dark mouth inside, rows of teeth on the rim
bm = bmesh.new(); bm.from_mesh(jaw.data)
for v in bm.verts:
    if v.co.z > 0.005: v.co.z = 0.005 - (v.co.z - 0.005) * 0.6   # cup shape
bm.to_mesh(jaw.data); bm.free()
mouth = sphere("HoundMouth", 0.085, (1.0, 1.55, 0.3), C + Vector((0, -0.01, -0.06)), mouth_m, 24, 12)
def ring_teeth(name, center, rx, ry, z, up, n, length):
    parts = []
    for i in range(n):
        a = math.pi * (0.08 + 0.84 * i / (n - 1))           # front half of an ellipse
        p = center + Vector((math.cos(a) * rx, -math.sin(a) * ry, z))
        h = length * random.uniform(0.7, 1.2)
        bpy.ops.mesh.primitive_cone_add(vertices=6, radius1=0.008, radius2=0.0, depth=h, location=p + Vector((0, 0, h / 2 if up else -h / 2)))
        t = bpy.context.object
        t.rotation_euler = (0 if up else math.pi, random.uniform(-0.15, 0.15), 0)
        parts.append(t)
    bpy.ops.object.select_all(action="DESELECT")
    for o in parts: o.select_set(True)
    bpy.context.view_layer.objects.active = parts[0]
    bpy.ops.object.join()
    o = link(bpy.context.object); o.name = name
    o.data.materials.clear(); o.data.materials.append(teeth_m)
    return o
lower_teeth = ring_teeth("TeethLower", C + Vector((0, -0.01, 0)), 0.095, 0.14, -0.065, True, 22, 0.04)
upper_teeth = ring_teeth("TeethUpper", C + Vector((0, 0.0, 0)), 0.08, 0.12, -0.02, False, 18, 0.035)

# claws on every finger tip
claws = []
for s, x in (("L", 1), ("R", -1)):
    for i in range(4):
        tip = Vector(P[f"f{i}b{s}"][0])
        bpy.ops.mesh.primitive_cone_add(vertices=6, radius1=0.009, radius2=0.0, depth=0.06, location=tip + Vector((0, -0.03, 0)))
        c = bpy.context.object; c.rotation_euler = (math.radians(90), 0, 0); claws.append(c)
bpy.ops.object.select_all(action="DESELECT")
for o in claws: o.select_set(True)
bpy.context.view_layer.objects.active = claws[0]
bpy.ops.object.join()
claw_o = link(bpy.context.object); claw_o.name = "Claws"
claw_o.data.materials.clear(); claw_o.data.materials.append(claw_m)

# hair: long black strands from the scalp, falling over the face to the floor
hb = bmesh.new()
for i in range(190):
    th = random.uniform(-math.pi * 0.85, math.pi * 0.85)   # around the scalp, front included
    ph = random.uniform(0.25, 1.2)
    root = skull.location + Vector((math.sin(th) * math.cos(ph) * 0.095, -math.cos(th) * math.cos(ph) * 0.11, math.sin(ph) * 0.1))
    length = random.uniform(0.45, 0.75)
    w = random.uniform(0.012, 0.022)
    side = Vector((math.cos(th), math.sin(th), 0)).normalized() * w
    out = Vector((math.sin(th), -math.cos(th), 0)) * (0.05 + 0.13 * max(0.0, math.cos(th)))   # front strands drape out over the jaw
    prev = None
    for k in range(7):
        t = k / 6
        p = root + out * math.sin(t * math.pi * 0.5) + Vector((random.uniform(-0.01, 0.01), random.uniform(-0.01, 0.01), -length * t * t * 0.95 - length * t * 0.05))
        p.z = max(p.z, 0.01)
        a, b = hb.verts.new(p - side * (1 - t * 0.7)), hb.verts.new(p + side * (1 - t * 0.7))
        if prev: hb.faces.new((prev[0], prev[1], b, a))
        prev = (a, b)
hm = bpy.data.meshes.new("Hair"); hb.to_mesh(hm); hb.free()
hair = link(bpy.data.objects.new("Hair", hm)); hair.data.materials.append(hair_m)

# ---------------- rig ----------------
arm = bpy.data.armatures.new("HoundRig")
rig = link(bpy.data.objects.new("HoundRig", arm))
bpy.context.view_layer.objects.active = rig
bpy.ops.object.mode_set(mode="EDIT")
def bone(name, a, b, parent=None):
    eb = arm.edit_bones.new(name)
    eb.head = Vector(P[a][0]) if isinstance(a, str) else Vector(a)
    eb.tail = Vector(P[b][0]) if isinstance(b, str) else Vector(b)
    if parent: eb.parent = arm.edit_bones[parent]
bone("Hips", "pelvis", "spine"); bone("Spine", "spine", "chest", "Hips"); bone("Chest", "chest", "upchest", "Spine")
bone("Neck", "neck0", "neck1", "Chest"); bone("Head", "neck1", C + Vector((0, -0.12, 0.06)), "Neck")
bone("Jaw", C + Vector((0, 0.06, 0.0)), C + Vector((0, -0.2, -0.06)), "Head")
for s in "LR":
    bone(f"Shoulder.{s}", "upchest", f"shoulder{s}", "Chest")
    bone(f"UpperArm.{s}", f"shoulder{s}", f"elbow{s}", f"Shoulder.{s}")
    bone(f"Forearm.{s}", f"elbow{s}", f"wrist{s}", f"UpperArm.{s}")
    bone(f"Hand.{s}", f"wrist{s}", f"f1b{s}", f"Forearm.{s}")
    bone(f"Thigh.{s}", f"hip{s}", f"knee{s}", "Hips")
    bone(f"Shin.{s}", f"knee{s}", f"ankle{s}", f"Thigh.{s}")
    bone(f"Foot.{s}", f"ankle{s}", f"t1{s}", f"Shin.{s}")
bpy.ops.object.mode_set(mode="OBJECT")
bpy.ops.object.select_all(action="DESELECT")
body.select_set(True); rig.select_set(True); bpy.context.view_layer.objects.active = rig
bpy.ops.object.parent_set(type="ARMATURE_AUTO")
def bone_parent(o, b):
    bpy.context.view_layer.update()
    mw = o.matrix_world.copy()
    o.parent = rig; o.parent_type = "BONE"; o.parent_bone = b
    o.matrix_world = mw
for o in (skull, hair, upper_teeth): bone_parent(o, "Head")
for o in (jaw, mouth, lower_teeth): bone_parent(o, "Jaw")
# claws ride the hands
bpy.context.view_layer.objects.active = claw_o
bpy.ops.object.select_all(action="DESELECT"); claw_o.select_set(True)
bpy.ops.object.mode_set(mode="EDIT"); bpy.ops.mesh.separate(type="LOOSE"); bpy.ops.object.mode_set(mode="OBJECT")
for o in [o for o in col.objects if o.name.startswith("Claws")]:
    o.data.materials.clear(); o.data.materials.append(claw_m)
    bpy.context.view_layer.objects.active = o
    bpy.ops.object.select_all(action="DESELECT"); o.select_set(True)
    bpy.ops.object.origin_set(type="ORIGIN_GEOMETRY")
    bone_parent(o, "Hand.L" if o.location.x > 0 else "Hand.R")
L = [o for o in col.objects if o.name.startswith("Claws") and o.parent_bone == "Hand.L"]
R = [o for o in col.objects if o.name.startswith("Claws") and o.parent_bone == "Hand.R"]
for group, name, b in ((L, "ClawsL", "Hand.L"), (R, "ClawsR", "Hand.R")):
    bpy.ops.object.select_all(action="DESELECT")
    for o in group: o.select_set(True)
    bpy.context.view_layer.objects.active = group[0]
    bpy.ops.object.join(); bpy.context.object.name = name

# ---------------- export ----------------
os.makedirs(os.path.dirname(OUT), exist_ok=True)
bpy.ops.object.select_all(action="DESELECT")
for o in col.objects: o.select_set(True)
bpy.ops.export_scene.fbx(filepath=OUT, use_selection=True, object_types={"ARMATURE", "MESH"},
                         apply_unit_scale=True, apply_scale_options="FBX_SCALE_ALL", axis_forward="-Z", axis_up="Y",
                         add_leaf_bones=False, bake_anim=False, mesh_smooth_type="FACE", use_armature_deform_only=True)
print("HOUND_OK", sum(len(o.data.vertices) for o in col.objects if o.type == "MESH"), "verts", [o.name for o in col.objects], OUT)
