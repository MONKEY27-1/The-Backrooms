# Builds the Clump (wiki Entity 5) from scratch: a knot of human arms and legs of every length grown out of one
# fleshy core, a few eyes sunk in the mass, a toothed mouth at the centre underneath, and one limb far longer
# than the rest (it reaches out and drags prey in). Every limb has its own two bones; the long limb has three.
# Run: blender -b Art/Monsters.blend --python Art/build_clump.py
import bpy, bmesh, math, os, random
from mathutils import Vector

HERE = os.path.dirname(bpy.data.filepath) or os.path.dirname(os.path.abspath(__file__))
OUT = os.path.abspath(os.path.join(HERE, "..", "Assets", "_Game", "Resources", "Monsters", "Clump.fbx"))
random.seed(5)

def mat(name, color, rough=0.5):
    m = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    m.use_nodes = True
    p = next(n for n in m.node_tree.nodes if n.type == "BSDF_PRINCIPLED")
    p.inputs["Base Color"].default_value = (*color, 1)
    p.inputs["Roughness"].default_value = rough
    return m

col = bpy.data.collections.get("Clump")
if col:
    for o in list(col.objects): bpy.data.objects.remove(o, do_unlink=True)
else:
    col = bpy.data.collections.new("Clump"); bpy.context.scene.collection.children.link(col)
def link(o):
    for c in o.users_collection: c.objects.unlink(o)
    col.objects.link(o); return o

flesh = mat("ClumpFlesh", (0.62, 0.45, 0.4), 0.55)
eye_m = mat("ClumpEye", (0.86, 0.72, 0.68), 0.15)   # bloodshot
iris_m = mat("ClumpIris", (0.06, 0.05, 0.05), 0.2)
mouth_m = mat("ClumpMouth", (0.12, 0.02, 0.02), 0.6)
teeth_m = mat("ClumpTeeth", (0.78, 0.72, 0.6), 0.35)

CORE = Vector((0, 0, 0.75))
# ---------------- skeleton: core + limbs ----------------
verts, radii, edges = [CORE], [0.44], []
limbs = []   # (root, elbow, tip) per limb, for the bones
def add(p, r):
    verts.append(p); radii.append(r); return len(verts) - 1
N = 16
for i in range(N):
    az = i / N * math.tau + random.uniform(-0.2, 0.2)
    up = random.random() < 0.25                        # a few wave in the air; the rest walk
    d = Vector((math.cos(az), math.sin(az), 0))
    L = random.uniform(0.6, 1.0)
    thick = random.uniform(0.05, 0.075)
    root = CORE + d * 0.32 + Vector((0, 0, random.uniform(-0.15, 0.2)))
    az2 = az + random.uniform(-0.6, 0.6)                 # limbs cross over each other: tangled, not radial
    d = Vector((math.cos(az2), math.sin(az2), 0))
    if up:
        elbow = root + d * L * 0.45 + Vector((0, 0, L * 0.35))
        tip = elbow + d * L * 0.25 + Vector((0, 0, L * 0.3))
    else:
        elbow = root + d * L * 0.5 + Vector((0, 0, 0.1))     # knuckle up like a spider's knee
        tip = Vector((elbow.x + d.x * L * 0.25, elbow.y + d.y * L * 0.25, 0.03))
    a = add(root, thick); m1 = add((root + elbow) / 2, thick * 0.6); b = add(elbow, thick * 0.85); m2 = add((elbow + tip) / 2, thick * 0.45); c = add(tip, thick * 0.6)
    edges += [(0, a), (a, m1), (m1, b), (b, m2), (m2, c)]   # thin mid-bones, knobbly joints
    for f in range(3):   # stubby fingers or toes
        side = Vector((-d.y, d.x, 0)) * (f - 1) * 0.03
        edges.append((c, add(tip + d * 0.08 + side, 0.012)))
    limbs.append((f"Limb{i}", root, elbow, tip))
# the long one: forward (-Y), three segments, laid out straight so code can fold and unfold it
lr = CORE + Vector((0, -0.3, 0.05))
lp = [lr, lr + Vector((0, -0.9, 0.05)), lr + Vector((0, -1.8, 0.0)), lr + Vector((0, -2.6, -0.05))]
ids = [add(lp[0], 0.075), add(lp[1], 0.06), add(lp[2], 0.05), add(lp[3], 0.04)]
edges += [(0, ids[0]), (ids[0], ids[1]), (ids[1], ids[2]), (ids[2], ids[3])]
for f in range(4):
    edges.append((ids[3], add(lp[3] + Vector(((f - 1.5) * 0.03, -0.14, -0.02)), 0.012)))

me = bpy.data.meshes.new("ClumpBody")
me.from_pydata(verts, edges, [])
body = link(bpy.data.objects.new("ClumpBody", me))
sk = body.modifiers.new("Skin", "SKIN"); sk.use_smooth_shade = True; sk.branch_smoothing = 0.8
for i, r in enumerate(radii):
    body.data.skin_vertices[0].data[i].radius = (r, r * 0.9)
body.data.skin_vertices[0].data[0].use_root = True
body.modifiers.new("Sub", "SUBSURF").levels = 2
bpy.context.view_layer.objects.active = body
for m in list(body.modifiers): bpy.ops.object.modifier_apply(modifier=m.name)
_nb = bmesh.new(); _nb.from_mesh(body.data); bmesh.ops.recalc_face_normals(_nb, faces=_nb.faces); _nb.to_mesh(body.data); _nb.free()
bm = bmesh.new(); bm.from_mesh(body.data); bm.normal_update()
for v in bm.verts:   # lumpy, knotted flesh where the limbs grow together
    if (v.co - CORE).length < 0.55:
        v.co += v.normal * (0.025 * math.sin(v.co.x * 23) * math.sin(v.co.y * 19) * math.sin(v.co.z * 17) + random.uniform(-0.004, 0.004))
    off = v.co - (CORE + Vector((0, -0.05, -0.42)))   # the mouth: a pit in the underside
    if off.length < 0.22 and v.co.z < CORE.z - 0.2: v.co.z += 0.14 * (1 - off.length / 0.22)
bm.to_mesh(body.data); bm.free()
body.data.materials.append(flesh)

# eyes sunk in the mass
eyes = []
for i in range(6):
    th, ph = random.uniform(0, math.tau), random.uniform(0.1, 1.1)
    n = Vector((math.cos(th) * math.cos(ph), math.sin(th) * math.cos(ph), math.sin(ph)))
    p = CORE + n * 0.4
    bpy.ops.mesh.primitive_uv_sphere_add(segments=16, ring_count=10, radius=0.028, location=p)
    e = link(bpy.context.object); e.name = f"ClumpEye{i}"; e.data.materials.append(eye_m); bpy.ops.object.shade_smooth()
    bpy.ops.mesh.primitive_cylinder_add(vertices=12, radius=0.01, depth=0.006, location=p + n * 0.027)
    ir = link(bpy.context.object); ir.name = f"ClumpIris{i}"; ir.data.materials.append(iris_m)
    ir.rotation_euler = n.to_track_quat("Z", "Y").to_euler()
    eyes += [e, ir]
# the mouth underneath: a dark pit ringed with teeth
bpy.ops.mesh.primitive_uv_sphere_add(segments=20, ring_count=12, radius=0.16, location=CORE + Vector((0, -0.05, -0.24)))
mouth = link(bpy.context.object); mouth.name = "ClumpMouth"; mouth.data.materials.append(mouth_m)
teeth = []
for i in range(20):
    a = i / 20 * math.tau
    p = CORE + Vector((math.cos(a) * 0.2, -0.05 + math.sin(a) * 0.2, -0.33))
    bpy.ops.mesh.primitive_cone_add(vertices=5, radius1=0.016, radius2=0, depth=0.07, location=p + Vector((0, 0, -0.03)))
    t = bpy.context.object; t.rotation_euler = (math.pi + math.cos(a) * 0.4, math.sin(a) * 0.4, 0); teeth.append(t)
bpy.ops.object.select_all(action="DESELECT")
for t in teeth: t.select_set(True)
bpy.context.view_layer.objects.active = teeth[0]; bpy.ops.object.join()
tt = link(bpy.context.object); tt.name = "ClumpTeeth"; tt.data.materials.clear(); tt.data.materials.append(teeth_m)

# ---------------- rig ----------------
arm = bpy.data.armatures.new("ClumpRig")
rig = link(bpy.data.objects.new("ClumpRig", arm))
bpy.context.view_layer.objects.active = rig
bpy.ops.object.mode_set(mode="EDIT")
def bone(name, h, t, parent=None):
    eb = arm.edit_bones.new(name); eb.head = h; eb.tail = t
    if parent: eb.parent = arm.edit_bones[parent]
bone("Core", CORE + Vector((0, 0, -0.2)), CORE + Vector((0, 0, 0.25)))
for name, root, elbow, tip in limbs:
    bone(name + "a", root, elbow, "Core"); bone(name + "b", elbow, tip, name + "a")
bone("LongA", lp[0], lp[1], "Core"); bone("LongB", lp[1], lp[2], "LongA"); bone("LongC", lp[2], lp[3], "LongB")
bpy.ops.object.mode_set(mode="OBJECT")
bpy.ops.object.select_all(action="DESELECT")
body.select_set(True); rig.select_set(True); bpy.context.view_layer.objects.active = rig
bpy.ops.object.parent_set(type="ARMATURE_AUTO")
for o in eyes + [mouth, tt]:
    bpy.context.view_layer.update()
    mw = o.matrix_world.copy()
    o.parent = rig; o.parent_type = "BONE"; o.parent_bone = "Core"
    o.matrix_world = mw

os.makedirs(os.path.dirname(OUT), exist_ok=True)
bpy.ops.object.select_all(action="DESELECT")
for o in col.objects: o.select_set(True)
bpy.ops.export_scene.fbx(filepath=OUT, use_selection=True, object_types={"ARMATURE", "MESH"},
                         apply_unit_scale=True, apply_scale_options="FBX_SCALE_ALL", axis_forward="-Z", axis_up="Y",
                         add_leaf_bones=False, bake_anim=False, mesh_smooth_type="FACE", use_armature_deform_only=True)
print("CLUMP_OK", len(body.data.vertices), "verts", len(limbs), "limbs", OUT)
