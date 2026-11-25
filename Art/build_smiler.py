# Builds the Smiler (wiki Entity 3) from scratch: a starved, bent-limbed body from a skin-modifier skeleton,
# a separately shaped skull whose face is mostly a too-wide grin, a rig with named bones for code animation,
# needle teeth and two glowing eyes sunk in sockets.
# Run: blender -b Art/Monsters.blend --python Art/build_smiler.py   (rebuilds collection "Smiler", exports FBX)
import bpy, bmesh, math, os, random
from mathutils import Vector
from mathutils.bvhtree import BVHTree

HERE = os.path.dirname(bpy.data.filepath) or os.path.dirname(os.path.abspath(__file__))
OUT = os.path.abspath(os.path.join(HERE, "..", "Assets", "_Game", "Resources", "Monsters", "Smiler.fbx"))
random.seed(3)

def mat(name, color, rough=0.5, emit=None, strength=0.0):
    m = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    m.use_nodes = True
    p = next(n for n in m.node_tree.nodes if n.type == "BSDF_PRINCIPLED")
    p.inputs["Base Color"].default_value = (*color, 1)
    p.inputs["Roughness"].default_value = rough
    if emit:
        p.inputs["Emission Color"].default_value = (*emit, 1)
        p.inputs["Emission Strength"].default_value = strength
    return m

col = bpy.data.collections.get("Smiler")
if col:
    for o in list(col.objects): bpy.data.objects.remove(o, do_unlink=True)
else:
    col = bpy.data.collections.new("Smiler"); bpy.context.scene.collection.children.link(col)
def link(o):
    for c in o.users_collection: c.objects.unlink(o)
    col.objects.link(o)
    return o

skin_m = mat("SmilerSkin", (0.075, 0.068, 0.062), 0.22)
teeth_m = mat("SmilerTeeth", (1, 1, 1), 0.3, (1, 0.97, 0.9), 6.0)
eye_m = mat("SmilerEyes", (1, 1, 1), 0.2, (1, 1, 1), 10.0)
mouth_m = mat("SmilerMouth", (0, 0, 0), 1.0)

# ---------------- body: skeleton (name -> position, radius), Blender Z-up, facing -Y ----------------
P = {
    "pelvis": ((0, 0.00, 1.22), 0.12), "spine": ((0, 0.03, 1.48), 0.085),
    "chest": ((0, 0.00, 1.78), 0.16), "upchest": ((0, -0.06, 1.98), 0.15),
    "neck0": ((0, -0.14, 2.10), 0.05), "neck1": ((0.01, -0.25, 2.21), 0.042),
}
for s, x in (("L", 1), ("R", -1)):
    P.update({
        f"shoulder{s}": ((0.27 * x, -0.06, 1.97), 0.06), f"elbow{s}": ((0.44 * x, -0.02, 1.42), 0.04),
        f"wrist{s}": ((0.52 * x, -0.16, 0.92), 0.03), f"palm{s}": ((0.54 * x, -0.20, 0.82), 0.035),
        f"hip{s}": ((0.13 * x, 0.01, 1.17), 0.07), f"knee{s}": ((0.19 * x, -0.24, 0.72), 0.052),
        f"ankle{s}": ((0.19 * x, 0.10, 0.20), 0.035), f"ball{s}": ((0.19 * x, -0.08, 0.04), 0.032),
    })
    for i, dx in enumerate((-0.035, -0.012, 0.012, 0.035)):   # four long fingers
        L = 0.24 + 0.03 * (i in (1, 2))
        P[f"f{i}a{s}"] = ((0.54 * x + dx * x, -0.24, 0.70), 0.012)
        P[f"f{i}b{s}"] = ((0.54 * x + dx * 1.3 * x, -0.25, 0.77 - L), 0.008)
    for i, dx in enumerate((-0.03, 0.0, 0.03)):               # three long toes
        P[f"t{i}{s}"] = ((0.19 * x + dx * x, -0.26, 0.015), 0.011)
E = [("pelvis", "spine"), ("spine", "chest"), ("chest", "upchest"), ("upchest", "neck0"), ("neck0", "neck1")]
for s in "LR":
    E += [("upchest", f"shoulder{s}"), (f"shoulder{s}", f"elbow{s}"), (f"elbow{s}", f"wrist{s}"), (f"wrist{s}", f"palm{s}"),
          ("pelvis", f"hip{s}"), (f"hip{s}", f"knee{s}"), (f"knee{s}", f"ankle{s}"), (f"ankle{s}", f"ball{s}")]
    E += [(f"palm{s}", f"f{i}a{s}") for i in range(4)] + [(f"f{i}a{s}", f"f{i}b{s}") for i in range(4)]
    E += [(f"ball{s}", f"t{i}{s}") for i in range(3)]
# Thin mid-points on the long bones, so elbows, knees and wrists read as knuckles of bone.
for a, b in list(E):
    if a.rstrip("LR") in ("shoulder", "elbow", "hip", "knee", "wrist") or a == "spine":
        m = f"mid_{a}_{b}"
        P[m] = (tuple((Vector(P[a][0]) + Vector(P[b][0])) / 2), min(P[a][1], P[b][1]) * 0.62)
        E.remove((a, b)); E += [(a, m), (m, b)]

names = list(P)
me = bpy.data.meshes.new("SmilerBody")
me.from_pydata([P[n][0] for n in names], [(names.index(a), names.index(b)) for a, b in E], [])
body = link(bpy.data.objects.new("Body", me))
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
    if 1.52 < z < 1.95 and 0.03 < abs(x) < 0.19:              # ribs on the sides and front (not the arms)
        push += 0.024 * max(0.0, math.sin(z * 125.0)) ** 0.5 * min(1.0, abs(x) * 8) - 0.01
    if 1.28 < z < 1.52 and y < 0.0:                           # sunken belly under the ribcage
        push -= 0.025 * math.sin((z - 1.28) / 0.24 * math.pi)
    if 1.2 < z < 2.05 and y > 0.03 and abs(x) < 0.035:        # vertebrae down the back
        push += 0.018 * max(0.0, math.sin(z * 70.0))
    v.co += v.normal * (push + random.uniform(-0.002, 0.002))
bm.to_mesh(body.data); bm.free()
body.data.materials.append(skin_m)

# ---------------- head: a stretched skull, the face mostly grin ----------------
C = Vector((0.02, -0.40, 2.40))       # skull centre (cocked a little to one side)
HS = 1.45                             # the head is built human-sized, then blown up: a face that fills the dark
R = 0.17
def grin_phi(u): return -0.16 + 0.55 * u * u      # height angle of the grin: the corners curl up
TH = 1.35                                          # the grin wraps ~77 degrees round each side: wider than a face
def ray_dir(theta, phi): return Vector((math.sin(theta) * math.cos(phi), -math.cos(theta) * math.cos(phi), math.sin(phi)))

bpy.ops.mesh.primitive_uv_sphere_add(segments=64, ring_count=40, radius=R, location=(0, 0, 0))
head = link(bpy.context.object); head.name = "Head"
bm = bmesh.new(); bm.from_mesh(head.data)
for v in bm.verts:
    p = v.co.copy()
    p.x *= 1.0; p.z *= 1.12
    if p.z < 0 and p.y < 0: p.y *= 1.0 + 0.5 * (-p.z / R)       # heavy lower face pushed forward
    if -0.02 < p.z < 0.07 and abs(p.x) > 0.09: p.x *= 0.9                # hollow cheeks
    if p.z > 0.06 and p.y > 0: p.y *= 1.15                         # long back of the skull
    p.z += 0.012 * math.sin(p.x * 40) ** 2 * (p.z > 0.05)          # brow ridges
    v.co = p
bm.to_mesh(head.data); bm.free()
smooth_tree = BVHTree.FromObject(head, bpy.context.evaluated_depsgraph_get())   # the face before carving
def on_face(theta, phi, inset=0.0):
    hit, n, _, _ = smooth_tree.ray_cast(Vector((0, 0, 0)), ray_dir(theta, phi))
    return hit - n * inset, n
# Carve the grin groove and the eye sockets.
EYE_TH, EYE_PHI = 0.40, 0.30
bm = bmesh.new(); bm.from_mesh(head.data)
for v in bm.verts:
    d = v.co.normalized()
    theta = math.atan2(d.x, -d.y); phi = math.asin(max(-1, min(1, d.z)))
    u = theta / TH
    if abs(u) < 1.0:
        gap = 0.15 * (1 - 0.7 * u * u)                 # mouth opening (angle), thinner toward the corners
        dz = abs(phi - grin_phi(u))
        if dz < gap: v.co -= d * 0.055 * (1 - (dz / gap) ** 4)
    for et in (EYE_TH, -EYE_TH):
        de = math.hypot(theta - et, phi - EYE_PHI)
        if de < 0.2: v.co -= d * 0.03 * (1 - (de / 0.2) ** 2)
bm.to_mesh(head.data); bm.free()
head.data.materials.append(skin_m)
for poly in head.data.polygons: poly.use_smooth = True

def tooth(theta, phi, lower, h, parts):
    root, n = on_face(theta, phi, 0.012)
    down = Vector((0, 0, 1 if lower else -1))
    axis = (down * 0.9 - n * 0.35).normalized()                  # points across the mouth, a little inward
    bpy.ops.mesh.primitive_cone_add(vertices=6, radius1=0.013 + 0.005 * random.random(), radius2=0.0, depth=h, location=root + axis * h / 2)
    t = bpy.context.object
    t.rotation_euler = axis.to_track_quat("Z", "Y").to_euler()
    t.rotation_euler.x += random.uniform(-0.15, 0.15); t.rotation_euler.y += random.uniform(-0.15, 0.15)
    parts.append(t)
def join(objs, name, m):
    bpy.ops.object.select_all(action="DESELECT")
    for o in objs: o.select_set(True)
    bpy.context.view_layer.objects.active = objs[0]
    bpy.ops.object.join()
    o = link(bpy.context.object); o.name = name
    o.data.materials.clear(); o.data.materials.append(m)
    return o
up, lo = [], []
for i in range(19):
    u = i / 9.0 - 1
    gap = 0.15 * (1 - 0.7 * u * u)
    tooth(u * TH, grin_phi(u) + gap * 0.95, False, (0.045 + 0.035 * random.random()) * (1.1 - 0.45 * abs(u)), up)
for i in range(17):
    u = i / 8.0 - 1 + 0.05
    gap = 0.15 * (1 - 0.7 * u * u)
    tooth(u * TH, grin_phi(u) - gap * 0.95, True, (0.035 + 0.025 * random.random()) * (1.1 - 0.45 * abs(u)), lo)
grin_up = join(up, "GrinUpper", teeth_m)
grin_lo = join(lo, "GrinLower", teeth_m)
# Black mouth inside the groove
mb = bmesh.new(); rows = []
for i in range(41):
    u = i / 20 - 1
    gap = 0.15 * (1 - 0.7 * u * u)
    rows.append([mb.verts.new(on_face(u * TH, grin_phi(u) + k * gap * 0.9, 0.05)[0]) for k in (1, -1)])
for i in range(40):
    mb.faces.new((rows[i][0], rows[i + 1][0], rows[i + 1][1], rows[i][1]))
mm = bpy.data.meshes.new("Mouth"); mb.to_mesh(mm); mb.free()
mouth = link(bpy.data.objects.new("Mouth", mm)); mouth.data.materials.append(mouth_m)
eyes = []
for s, et in (("L", EYE_TH), ("R", -EYE_TH)):
    at, n = on_face(et, EYE_PHI, 0.024)
    bpy.ops.mesh.primitive_uv_sphere_add(segments=16, ring_count=10, radius=0.036, location=at)
    e = link(bpy.context.object); e.name = f"Eye{s}"
    e.data.materials.append(eye_m); bpy.ops.object.shade_smooth()
    eyes.append(e)
for o in (head, grin_up, grin_lo, mouth, *eyes):   # everything was built round the origin: enlarge it, move it onto the neck
    o.location = o.location * HS + C
    o.scale = (HS, HS, HS)
    bpy.context.view_layer.objects.active = o
    bpy.ops.object.select_all(action="DESELECT"); o.select_set(True)
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)

# ---------------- rig ----------------
arm = bpy.data.armatures.new("SmilerRig")
rig = link(bpy.data.objects.new("SmilerRig", arm))
bpy.context.view_layer.objects.active = rig
bpy.ops.object.mode_set(mode="EDIT")
def bone(name, a, b, parent=None):
    eb = arm.edit_bones.new(name)
    eb.head = Vector(P[a][0]) if isinstance(a, str) else Vector(a)
    eb.tail = Vector(P[b][0]) if isinstance(b, str) else Vector(b)
    if parent: eb.parent = arm.edit_bones[parent]
    return eb
bone("Hips", "pelvis", "spine"); bone("Spine", "spine", "chest", "Hips"); bone("Chest", "chest", "upchest", "Spine")
bone("Neck", "neck0", "neck1", "Chest"); bone("Head", "neck1", C + Vector((0, 0, 0.17 * HS)), "Neck")
bone("Jaw", C + Vector((0, 0.05, -0.02)) * HS, C + Vector((0, -0.17, -0.09)) * HS, "Head")
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

# Skull, upper teeth, mouth and eyes ride the Head bone; the lower teeth ride the Jaw (code opens it to lunge).
def bone_parent(o, b):
    bpy.context.view_layer.update()
    mw = o.matrix_world.copy()
    o.parent = rig; o.parent_type = "BONE"; o.parent_bone = b
    o.matrix_world = mw
for o in (head, grin_up, mouth, *eyes): bone_parent(o, "Head")
bone_parent(grin_lo, "Jaw")

# ---------------- export ----------------
os.makedirs(os.path.dirname(OUT), exist_ok=True)
bpy.ops.object.select_all(action="DESELECT")
for o in col.objects: o.select_set(True)
bpy.ops.export_scene.fbx(filepath=OUT, use_selection=True, object_types={"ARMATURE", "MESH"},
                         apply_unit_scale=True, apply_scale_options="FBX_SCALE_ALL", axis_forward="-Z", axis_up="Y",
                         add_leaf_bones=False, bake_anim=False, mesh_smooth_type="FACE", use_armature_deform_only=True)
print("SMILER_OK", sum(len(o.data.vertices) for o in col.objects if o.type == "MESH"), "verts", OUT)
