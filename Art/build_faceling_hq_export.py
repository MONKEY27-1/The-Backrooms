# Faceling, second version, step 3 (run right after build_faceling_hq.py, in the same Blender session):
# variant shaping (child / evolved), game-resolution meshes, baked detail and normal maps, rig, FBX export.
#   VARIANT = "adult" | "child" | "evolved"
#   exec(open(".../build_faceling_hq.py").read()); exec(open(".../build_faceling_hq_export.py").read())
import bpy, bmesh, math, os, random
from mathutils import Vector

VARIANT = globals().get("VARIANT", "adult")
HERE = r"C:\Users\drago\OneDrive\Documents\Backrooms\The Backrooms\Art"
OUTDIR = os.path.abspath(os.path.join(HERE, "..", "Assets", "_Game", "Resources", "Monsters"))
TEXDIR = os.path.join(OUTDIR, "FacelingTex")
os.makedirs(TEXDIR, exist_ok=True)
NAME = {"adult": "Faceling", "child": "FacelingChild", "evolved": "FacelingEvolved"}[VARIANT]
col = bpy.data.collections["FacelingHQ"]
def link(o):
    for c in o.users_collection: c.objects.unlink(o)
    col.objects.link(o); return o
def apply_all(o):
    bpy.context.view_layer.objects.active = o
    for m in list(o.modifiers): bpy.ops.object.modifier_apply(modifier=m.name)
O = bpy.data.objects
high = {"Body": O["FacelingHigh"], "Shirt": O["FacelingShirtMesh"], "Pants": O["FacelingPantsMesh"], "Shoes": O["FacelingShoesMesh"], "Hair": O["FacelingHairMesh"]}
random.seed(10)

if VARIANT == "evolved":
    # a malformed face: pin-prick eyes under a heavy brow, a lipless mouth hanging open, a fused nostril lump
    body = high["Body"]
    bm = bmesh.new(); bm.from_mesh(body.data)
    for v in bm.verts:
        x, y, z = v.co
        if z > 1.55 and y < -0.05:
            d = math.hypot(x * 0.8, z - 1.605)
            if d < 0.034: v.co.y += 0.045 * (1 - (d / 0.034) ** 2)       # the mouth, deep
            for ex in (0.032, -0.032):
                de = math.hypot(x - ex, z - 1.675)
                if de < 0.016: v.co.y += 0.012 * (1 - de / 0.016)        # sockets for the pin eyes
            if 1.69 < z < 1.71: v.co.y -= 0.009
            if abs(x) < 0.012 and 1.63 < z < 1.65: v.co.y -= 0.006
    bm.to_mesh(body.data); bm.free()
    for k in ("Shirt", "Pants"):   # torn: bites out of the cloth
        g = high[k]
        bm = bmesh.new(); bm.from_mesh(g.data)
        holes = [Vector((random.uniform(-0.3, 0.3), random.uniform(-0.15, 0.15), random.uniform(0.1, 1.4))) for _ in range(26)]
        bmesh.ops.delete(bm, geom=[f for f in bm.faces if any((f.calc_center_median() - c).length < 0.035 for c in holes)], context="FACES")
        bm.to_mesh(g.data); bm.free()
if VARIANT == "child":
    pivot = Vector((0, 0, 1.47))
    for o in high.values():
        for v in o.data.vertices:
            if v.co.z > 1.46:   # the head (and top of the neck) grows a third, blending into the neck
                k = 1.0 + 0.32 * min(1.0, (v.co.z - 1.46) / 0.08)
                v.co = pivot + (v.co - pivot) * k
            v.co *= 0.62

def game_mesh(kind, src, ratio):
    lo = src.copy(); lo.data = src.data.copy(); lo.name = NAME + kind
    col.objects.link(lo); lo.hide_set(False); lo.hide_render = False
    d = lo.modifiers.new("Decimate", "DECIMATE"); d.ratio = ratio
    apply_all(lo)
    bpy.ops.object.select_all(action="DESELECT"); lo.select_set(True); bpy.context.view_layer.objects.active = lo
    bpy.ops.object.mode_set(mode="EDIT"); bpy.ops.mesh.select_all(action="SELECT")
    bpy.ops.uv.smart_project(angle_limit=math.radians(66), island_margin=0.004)
    bpy.ops.object.mode_set(mode="OBJECT")
    for p in lo.data.polygons: p.use_smooth = True
    return lo

RATIOS = {"Body": 0.22, "Shirt": 0.05, "Pants": 0.05, "Shoes": 0.1, "Hair": 0.15}
SIZES = {"Body": 1024, "Shirt": 1024, "Pants": 1024, "Shoes": 512, "Hair": 512}
for o in list(col.objects):   # a rerun replaces the last game version of this variant
    if o.name.startswith(NAME) and o not in high.values() and (o.name[len(NAME):] in RATIOS or o.name[len(NAME):] in ("Rig", "Pipe")):
        bpy.data.objects.remove(o, do_unlink=True)
low = {k: game_mesh(k, v, RATIOS[k]) for k, v in high.items()}

scene = bpy.context.scene
prev_engine = scene.render.engine
try: scene.render.engine = "CYCLES"
except TypeError as e: print(e)
scene.cycles.samples = 4

def detail_material(kind):
    """Near-white detail the game multiplies by each Faceling's own colours: pores, weave, denim twill."""
    m = bpy.data.materials.new("Detail" + kind); m.use_nodes = True
    nt = m.node_tree; bsdf = next(n for n in nt.nodes if n.type == "BSDF_PRINCIPLED")
    tc = nt.nodes.new("ShaderNodeTexCoord")
    noise = nt.nodes.new("ShaderNodeTexNoise"); noise.inputs["Scale"].default_value = {"Body": 90.0, "Hair": 300.0}.get(kind, 60.0)
    noise.inputs["Detail"].default_value = 8.0
    nt.links.new(tc.outputs["Object"], noise.inputs["Vector"])
    ramp = nt.nodes.new("ShaderNodeValToRGB")
    a, b = {"Body": (0.82, 1.0), "Shirt": (0.86, 1.0), "Pants": (0.72, 1.0), "Shoes": (0.6, 0.9), "Hair": (0.55, 1.0)}[kind]
    ramp.color_ramp.elements[0].color = (a, a, a, 1); ramp.color_ramp.elements[1].color = (b, b, b, 1)
    nt.links.new(noise.outputs["Fac"], ramp.inputs["Fac"])
    out = ramp.outputs["Color"]
    if kind in ("Shirt", "Pants"):
        wave = nt.nodes.new("ShaderNodeTexWave"); wave.inputs["Scale"].default_value = 380.0 if kind == "Shirt" else 260.0
        wave.bands_direction = "DIAGONAL" if kind == "Pants" else "X"
        nt.links.new(tc.outputs["Object"], wave.inputs["Vector"])
        mix = nt.nodes.new("ShaderNodeMix"); mix.data_type = "RGBA"; mix.blend_type = "MULTIPLY"; mix.inputs["Factor"].default_value = 0.18
        nt.links.new(out, mix.inputs[6]); nt.links.new(wave.outputs["Color"], mix.inputs[7])
        out = mix.outputs[2]
    nt.links.new(out, bsdf.inputs["Base Color"])
    return m

def bake(kind, lo, hi):
    size = SIZES[kind]
    imgs = {}
    for pas in ("color", "normal"):
        img = bpy.data.images.get(f"{NAME}_{kind}_{pas}")
        if img: bpy.data.images.remove(img)
        img = bpy.data.images.new(f"{NAME}_{kind}_{pas}", size, size, alpha=False)
        if pas == "normal": img.colorspace_settings.name = "Non-Color"
        imgs[pas] = img
    lo.data.materials.clear(); lo.data.materials.append(detail_material(kind))
    nt = lo.data.materials[0].node_tree
    tex = nt.nodes.new("ShaderNodeTexImage"); nt.nodes.active = tex
    hi.hide_set(False)
    tex.image = imgs["color"]
    bpy.ops.object.select_all(action="DESELECT"); lo.select_set(True); bpy.context.view_layer.objects.active = lo
    scene.render.bake.use_selected_to_active = False
    bpy.ops.object.bake(type="DIFFUSE", pass_filter={"COLOR"}, margin=4)
    tex.image = imgs["normal"]
    hi.select_set(True); lo.select_set(True); bpy.context.view_layer.objects.active = lo
    scene.render.bake.use_selected_to_active = True
    scene.render.bake.cage_extrusion = 0.006
    scene.render.bake.max_ray_distance = 0.015   # tight: don't catch other parts (hair over the face)
    bpy.ops.object.bake(type="NORMAL", margin=4)
    for pas, img in imgs.items():
        img.filepath_raw = os.path.join(TEXDIR, f"{NAME}_{kind}_{pas}.png"); img.file_format = "PNG"; img.save()
    lo.data.materials.clear()
    lo.data.materials.append(bpy.data.materials.get("Faceling" + kind) or bpy.data.materials.new("Faceling" + kind))   # just names the part
    hi.hide_set(True)

for k in low: bake(k, low[k], high[k])
scene.render.engine = prev_engine

# ---------------- rig ----------------
S = 0.62 if VARIANT == "child" else 1.0
def P(x, y, z): return Vector((x, y, z)) * S
arm = bpy.data.armatures.new(NAME + "Rig")
rig = link(bpy.data.objects.new(NAME + "Rig", arm))
bpy.context.view_layer.objects.active = rig
bpy.ops.object.mode_set(mode="EDIT")
def bone(n, a, b, parent=None):
    eb = arm.edit_bones.new(n); eb.head = a; eb.tail = b
    if parent: eb.parent = arm.edit_bones[parent]
bone("Hips", P(0, 0, 0.92), P(0, 0, 1.05)); bone("Spine", P(0, 0, 1.05), P(0, 0, 1.25), "Hips"); bone("Chest", P(0, 0, 1.25), P(0, 0, 1.44), "Spine")
bone("Neck", P(0, 0, 1.47), P(0, -0.01, 1.55), "Chest")
headk = 1.32 if VARIANT == "child" else 1.0
bone("Head", P(0, -0.01, 1.55), (Vector((0, 0, 1.47)) + Vector((0, -0.01, 0.32)) * headk) * S, "Neck")
for sx in (1, -1):
    s = "L" if sx > 0 else "R"
    bone(f"UpperArm.{s}", P(0.19 * sx, 0.01, 1.41), P(0.26 * sx, 0.025, 1.1), "Chest")
    bone(f"Forearm.{s}", P(0.26 * sx, 0.025, 1.1), P(0.272 * sx, -0.005, 0.86), f"UpperArm.{s}")
    bone(f"Hand.{s}", P(0.272 * sx, -0.005, 0.86), P(0.275 * sx, -0.015, 0.7), f"Forearm.{s}")
    bone(f"Thigh.{s}", P(0.09 * sx, 0, 0.92), P(0.09 * sx, -0.005, 0.48), "Hips")
    bone(f"Shin.{s}", P(0.09 * sx, -0.005, 0.48), P(0.087 * sx, 0.0, 0.08), f"Thigh.{s}")
    bone(f"Foot.{s}", P(0.087 * sx, 0.0, 0.08), P(0.09 * sx, -0.13, 0.02), f"Shin.{s}")
bpy.ops.object.mode_set(mode="OBJECT")
bpy.ops.object.select_all(action="DESELECT")
for lo in low.values(): lo.select_set(True)
rig.select_set(True); bpy.context.view_layer.objects.active = rig
bpy.ops.object.parent_set(type="ARMATURE_AUTO")

# The hands hang at hip height, so automatic weights tie the waistband and shirt hem to the arms.
# Trousers never follow the arms; the shirt only does on its sleeves; nothing but skin follows the hands.
ARM = ("UpperArm.", "Forearm.", "Hand.")
for kind, lo in low.items():
    if kind not in ("Shirt", "Pants"): continue
    groups = {g.index: g.name for g in lo.vertex_groups}
    for v in lo.data.vertices:
        sleeve = kind == "Shirt" and abs(v.co.x) >= 0.17 * S
        for g in list(v.groups):
            n = groups[g.group]
            if n.startswith(ARM) and (not sleeve or n.startswith("Hand.")):
                lo.vertex_groups[n].remove([v.index])
    bpy.ops.object.select_all(action="DESELECT"); lo.select_set(True); bpy.context.view_layer.objects.active = lo
    bpy.ops.object.vertex_group_normalize_all(lock_active=False)

pipe_o = None
extra = []
if VARIANT == "evolved":
    # dark mouth interior and pin-prick eyes, so the face reads at a distance
    head_bone_parts = []
    for loc, r, nm in (((0, -0.075, 1.605), 0.03, "FacelingEvolvedMouth"), ((0.032, -0.09, 1.675), 0.007, "FacelingEvolvedEyeL"), ((-0.032, -0.09, 1.675), 0.007, "FacelingEvolvedEyeR")):
        bpy.ops.mesh.primitive_uv_sphere_add(segments=12, ring_count=8, radius=r, location=loc)
        e = link(bpy.context.object); e.name = nm
        e.data.materials.append(bpy.data.materials.get("FacelingPit") or bpy.data.materials.new("FacelingPit"))
        bpy.context.view_layer.update(); mw = e.matrix_world.copy()
        e.parent = rig; e.parent_type = "BONE"; e.parent_bone = "Head"; e.matrix_world = mw
        extra.append(e)
    bpy.ops.mesh.primitive_cylinder_add(vertices=12, radius=0.02, depth=0.72, location=(-0.275, -0.1, 0.8))
    pipe_o = link(bpy.context.object); pipe_o.name = NAME + "Pipe"; pipe_o.rotation_euler = (math.radians(70), 0, 0)
    pipe_o.data.materials.append(bpy.data.materials.get("FacelingPipe") or bpy.data.materials.new("FacelingPipe"))
    bpy.context.view_layer.update(); mw = pipe_o.matrix_world.copy()
    pipe_o.parent = rig; pipe_o.parent_type = "BONE"; pipe_o.parent_bone = "Hand.R"; pipe_o.matrix_world = mw

bpy.ops.object.select_all(action="DESELECT")
for o in list(low.values()) + [rig] + ([pipe_o] if pipe_o else []) + extra: o.select_set(True)
bpy.ops.export_scene.fbx(filepath=os.path.join(OUTDIR, NAME + ".fbx"), use_selection=True, object_types={"ARMATURE", "MESH"},
                         apply_unit_scale=True, apply_scale_options="FBX_SCALE_ALL", axis_forward="-Z", axis_up="Y",
                         add_leaf_bones=False, bake_anim=False, mesh_smooth_type="FACE", use_armature_deform_only=True)
print("HQ_EXPORT", NAME, {k: len(v.data.polygons) for k, v in low.items()})
