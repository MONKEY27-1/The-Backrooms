# Faceling (wiki Entity 9), second version: an anatomically shaped human with no face.
# Step 1 (this part): the high-detail body. Parts are shaped separately (torso, limbs, hands, head), fused with a
# voxel remesh so the joins blend like flesh, then smoothed and given anatomy (collarbones, shoulder blades,
# chest, knees, calves). Later steps decimate it for the game, bake normal/detail maps, add real clothes and a rig.
# Run in Blender: exec(open(r"<this file>").read())  - or  blender -b Art/FacelingHQ.blend --python Art/build_faceling_hq.py
import bpy, bmesh, math, random
from mathutils import Vector, Matrix

random.seed(9)
COL = "FacelingHQ"
SHRINK = 1.32   # the skin modifier's box cage shrinks ~25% when subdivided: radii below are the real body sizes

def collection(name):
    col = bpy.data.collections.get(name)
    if col:
        for o in list(col.objects): bpy.data.objects.remove(o, do_unlink=True)
    else:
        col = bpy.data.collections.new(name); bpy.context.scene.collection.children.link(col)
    return col
col = collection(COL)
def link(o):
    for c in o.users_collection: c.objects.unlink(o)
    col.objects.link(o); return o

def apply_all(o):
    bpy.context.view_layer.objects.active = o
    for m in list(o.modifiers): bpy.ops.object.modifier_apply(modifier=m.name)

def skin_chain(name, pts, edges, root=0, subdiv=2):
    """pts: list of (position, (rx, ry)); a skin-modifier body part."""
    me = bpy.data.meshes.new(name)
    me.from_pydata([Vector(p) for p, _ in pts], edges, [])
    o = link(bpy.data.objects.new(name, me))
    sk = o.modifiers.new("Skin", "SKIN"); sk.use_smooth_shade = True; sk.branch_smoothing = 1.0
    for i, (_, r) in enumerate(pts): o.data.skin_vertices[0].data[i].radius = (r[0] * SHRINK, r[1] * SHRINK)   # subsurf shrinks the skin cage
    o.data.skin_vertices[0].data[root].use_root = True
    o.modifiers.new("Sub", "SUBSURF").levels = subdiv
    apply_all(o)
    return o

# ---------------- proportions: 1.75 m, ~7.5 heads ----------------
# Torso + neck: one vertical chain, elliptical sections (width, depth).
T = [((0, 0.0, 0.84), (0.11, 0.085)),   # crotch
     ((0, 0.0, 0.90), (0.14, 0.1)),      # pelvis
     ((0, 0.005, 0.98), (0.128, 0.092)), # lower belly
     ((0, 0.01, 1.06), (0.112, 0.082)),  # waist
     ((0, 0.005, 1.16), (0.122, 0.088)), # lower ribs
     ((0, -0.005, 1.27), (0.135, 0.095)), # chest
     ((0, 0.0, 1.37), (0.14, 0.09)),     # upper chest
     ((0, 0.01, 1.44), (0.12, 0.078)),   # shoulder girdle top
     ((0, 0.0, 1.49), (0.062, 0.058)),   # base of the neck (trapezius)
     ((0, -0.005, 1.54), (0.045, 0.048)),   # neck
     ((0, -0.01, 1.59), (0.042, 0.046))]
torso = skin_chain("Torso", T, [(i, i + 1) for i in range(len(T) - 1)], subdiv=2)

def limb(name, pts):
    return skin_chain(name, pts, [(i, i + 1) for i in range(len(pts) - 1)], subdiv=2)

parts = [torso]
for sx in (1, -1):
    s = "L" if sx > 0 else "R"
    # arm hanging, slightly away from the body: deltoid, biceps, elbow, forearm swell, wrist
    parts.append(limb("Arm" + s, [
        ((0.08 * sx, 0.01, 1.42), (0.07, 0.065)), ((0.205 * sx, 0.01, 1.40), (0.058, 0.06)),
        ((0.235 * sx, 0.015, 1.30), (0.046, 0.048)), ((0.25 * sx, 0.02, 1.20), (0.04, 0.042)),
        ((0.26 * sx, 0.025, 1.10), (0.034, 0.036)),   # elbow
        ((0.265 * sx, 0.015, 1.02), (0.038, 0.04)), ((0.27 * sx, 0.0, 0.92), (0.03, 0.026)),
        ((0.272 * sx, -0.005, 0.84), (0.025, 0.021))]))   # wrist
    # leg: hip, thigh, knee, calf, ankle
    parts.append(limb("Leg" + s, [
        ((0.07 * sx, 0.0, 0.98), (0.07, 0.07)), ((0.09 * sx, 0.0, 0.9), (0.085, 0.085)), ((0.095 * sx, -0.005, 0.80), (0.08, 0.082)),
        ((0.092 * sx, -0.005, 0.66), (0.065, 0.068)), ((0.09 * sx, -0.01, 0.53), (0.05, 0.052)),
        ((0.09 * sx, -0.005, 0.48), (0.048, 0.05)),    # knee
        ((0.09 * sx, 0.015, 0.36), (0.052, 0.058)),    # calf
        ((0.088 * sx, 0.01, 0.22), (0.038, 0.04)), ((0.087 * sx, 0.005, 0.10), (0.03, 0.032)),
        ((0.087 * sx, 0.0, 0.07), (0.03, 0.035))]))
    # foot: heel to ball to toes, flat on the floor
    parts.append(skin_chain("Foot" + s, [
        ((0.087 * sx, 0.03, 0.05), (0.032, 0.03)), ((0.088 * sx, -0.02, 0.045), (0.036, 0.028)),
        ((0.09 * sx, -0.09, 0.03), (0.042, 0.022)), ((0.092 * sx, -0.15, 0.02), (0.04, 0.015))],
        [(0, 1), (1, 2), (2, 3)]))
    # hand: palm, four jointed fingers and an opposed thumb
    wrist = Vector((0.272 * sx, -0.005, 0.87))
    pts = [(tuple(wrist), (0.024, 0.016)), (tuple(wrist + Vector((0.003 * sx, -0.005, -0.06))), (0.037, 0.014))]
    edges = [(0, 1)]
    knuckle_z = wrist.z - 0.095
    for f, (dx, L) in enumerate(((-0.024, 0.075), (-0.008, 0.085), (0.008, 0.08), (0.023, 0.065))):
        base = Vector((wrist.x + 0.002 * sx, wrist.y - 0.008 + dx, knuckle_z))
        prev = 1
        for j, (t, r) in enumerate(((0.0, 0.0105), (0.42, 0.009), (0.72, 0.008), (1.0, 0.0065))):
            p = base + Vector((0.004 * sx * t, -0.01 * t, -L * t))   # slightly curled
            pts.append((tuple(p), (r, r * 0.85))); edges.append((prev, len(pts) - 1)); prev = len(pts) - 1
    tb = wrist + Vector((-0.012 * sx, -0.022, -0.035))
    prev = 0
    for t, r in ((0.0, 0.014), (0.45, 0.011), (1.0, 0.0085)):
        p = tb + Vector((0.006 * sx * t, -0.03 * t, -0.045 * t))
        pts.append((tuple(p), (r, r))); edges.append((prev, len(pts) - 1)); prev = len(pts) - 1
    parts.append(skin_chain("Hand" + s, pts, edges, subdiv=2))

# ---------------- head: a skull with no face ----------------
bpy.ops.mesh.primitive_uv_sphere_add(segments=64, ring_count=40, radius=0.108, location=(0, -0.01, 1.665))
head = link(bpy.context.object); head.name = "Head"
bm = bmesh.new(); bm.from_mesh(head.data)
for v in bm.verts:
    x, y, z = v.co
    p = Vector((x * 0.78, y * 0.95, z * 1.12))                      # narrower, longer than a ball
    if z < -0.02: p.x *= 1.0 - 0.28 * min(1.0, (-z - 0.02) / 0.08)  # tapering to the jaw and chin
    if z < -0.03 and y < 0: p.y *= 1.0 + 0.08 * min(1.0, (-z - 0.03) / 0.07)   # chin forward
    if y > 0.02 and z > -0.02: p.y *= 1.12                          # back of the skull
    if y < -0.06 and 0.015 < z < 0.045: p.y -= 0.006                # a faint brow, where eyes should be under it
    if y < -0.07 and abs(x) < 0.012 and -0.03 < z < 0.015: p.y -= 0.004 * (1 - abs(x) / 0.012)   # the ghost of a nose bridge
    if y < -0.05 and 0.035 < abs(x) < 0.06 and -0.02 < z < 0.0: p.y -= 0.004   # cheekbones
    v.co = p
bm.to_mesh(head.data); bm.free()
parts.append(head)
for sx in (1, -1):   # small ears, the only feature it has
    bpy.ops.mesh.primitive_uv_sphere_add(segments=16, ring_count=10, radius=0.03, location=(0.083 * sx, 0.0, 1.665))
    ear = link(bpy.context.object); ear.name = "Ear"
    ear.scale = (0.25, 0.55, 0.95); bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    parts.append(ear)

# ---------------- fuse ----------------
bpy.ops.object.select_all(action="DESELECT")
for o in parts: o.select_set(True)
bpy.context.view_layer.objects.active = torso
bpy.ops.object.join()
body = bpy.context.object; body.name = "FacelingHigh"
body.data.remesh_voxel_size = 0.004
body.data.use_remesh_fix_poles = True
bpy.ops.object.voxel_remesh()
ls = body.modifiers.new("Laplacian", "LAPLACIANSMOOTH"); ls.iterations = 8; ls.lambda_factor = 1.2; ls.use_volume_preserve = True   # melts the cage seams and ledges
sm = body.modifiers.new("Smooth", "CORRECTIVE_SMOOTH"); sm.factor = 0.6; sm.iterations = 6; sm.smooth_type = "LENGTH_WEIGHTED"
sm.use_only_smooth = True
apply_all(body)

# ---------------- anatomy pass ----------------
def bump(v, center, radius, amount):
    d = (v.co - Vector(center)).length
    if d < radius: v.co += v.normal * amount * (0.5 + 0.5 * math.cos(math.pi * d / radius))
bm = bmesh.new(); bm.from_mesh(body.data); bm.normal_update()
for v in bm.verts:
    for sx in (1, -1):
        bump(v, (0.075 * sx, -0.07, 1.43), 0.06, 0.006)    # collarbones
        bump(v, (0.075 * sx, -0.095, 1.31), 0.07, 0.008)   # chest
        bump(v, (0.08 * sx, 0.1, 1.34), 0.07, 0.007)       # shoulder blades
        bump(v, (0.19 * sx, 0.0, 1.405), 0.05, 0.008)      # deltoids
        bump(v, (0.09 * sx, -0.055, 0.49), 0.035, 0.006)   # kneecaps
        bump(v, (0.09 * sx, 0.06, 0.36), 0.06, 0.006)      # calves
        bump(v, (0.08 * sx, 0.11, 0.86), 0.09, 0.012)      # buttocks
        bump(v, (0.06 * sx, -0.105, 1.05), 0.05, -0.004)   # the soft dip beside the belly
    bump(v, (0, -0.06, 1.51), 0.02, 0.003)                 # throat
    bump(v, (0, 0.1, 1.2), 0.18, -0.006)                   # small of the back
bm.to_mesh(body.data); bm.free()
for p in body.data.polygons: p.use_smooth = True
print("HQ_BODY", len(body.data.vertices), "verts")

# ---------------- step 2: clothes and hair ----------------
def mat(name, color, rough=0.6):
    m = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    m.use_nodes = True
    p = next(n for n in m.node_tree.nodes if n.type == "BSDF_PRINCIPLED")
    p.inputs["Base Color"].default_value = (*color, 1); p.inputs["Roughness"].default_value = rough
    return m
M_SKIN = mat("FacelingSkin", (0.62, 0.48, 0.4), 0.5)
M_SHIRT = mat("FacelingShirt", (0.55, 0.55, 0.6), 0.85)
M_PANTS = mat("FacelingPants", (0.2, 0.23, 0.3), 0.85)
M_SHOES = mat("FacelingShoes", (0.08, 0.07, 0.06), 0.5)
M_HAIR = mat("FacelingHair", (0.09, 0.06, 0.04), 0.6)
body.data.materials.clear(); body.data.materials.append(M_SKIN)

def is_hand(c): return abs(c.x) > 0.24 and c.z < 0.9
def in_shirt(c): return (abs(c.x) < 0.175 and 0.93 < c.z < 1.47) or (abs(c.x) >= 0.175 and 0.89 < c.z < 1.47)
def in_pants(c): return 0.075 < c.z < 0.98 and abs(c.x) < 0.2
def in_shoes(c): return c.z < 0.105

def garment(name, region, offset, m, folds, cuts=()):
    g = body.copy(); g.data = body.data.copy(); g.name = name
    col.objects.link(g)
    bm = bmesh.new(); bm.from_mesh(g.data)
    for co, no in cuts:   # clean, straight hems: slice along each edge line before cutting away
        bmesh.ops.bisect_plane(bm, geom=bm.verts[:] + bm.edges[:] + bm.faces[:], plane_co=Vector(co), plane_no=Vector(no))
    bmesh.ops.delete(bm, geom=[f for f in bm.faces if not region(f.calc_center_median())], context="FACES")
    bm.normal_update()
    for v in bm.verts:
        v.co += v.normal * (offset + folds(v.co) + random.uniform(-0.0006, 0.0006))
    bm.to_mesh(g.data); bm.free()
    g.data.materials.clear(); g.data.materials.append(m)
    so = g.modifiers.new("Thickness", "SOLIDIFY"); so.thickness = 0.004; so.offset = -1; so.use_rim = True
    apply_all(g)
    return g

def soft(x, a, b): return max(0.0, min(1.0, (x - a) / (b - a)))
def shirt_folds(p):
    f = 0.0
    ax = abs(p.x)
    if ax > 0.2:   # sleeves: rings of creases at the elbow, looser cloth bunching above the cuff
        f += 0.0018 * math.sin(p.z * 150 + math.sin(p.y * 40 + p.x * 30) * 3) * math.exp(-((p.z - 1.1) / 0.06) ** 2)
        f += 0.0015 * math.sin(p.z * 110 + math.sin(p.y * 55) * 2) * soft(0.98 - p.z, 0, 0.08)
    else:          # body: pull lines from the armpits, a loose blousy band over the waist, hem flare
        f += 0.0015 * math.sin((p.z + ax * 0.6) * 90 + p.y * 30) * soft(p.z, 1.2, 1.35)
        f += 0.004 * soft(1.02 - abs(p.z - 0.97), 0.95, 1.02) + 0.0015 * math.sin(p.x * 70 + p.y * 50) * math.sin(p.z * 23)
    return f
def pants_folds(p):
    f = 0.002 * math.sin(p.z * 140 + math.sin(p.x * 60) * 1.5) * math.exp(-((p.z - 0.49) / 0.05) ** 2)   # knees
    f += 0.005 * (0.5 + 0.5 * math.sin(p.z * 180 + p.x * 40)) * soft(0.2 - p.z, 0, 0.12)                  # bunched at the ankles
    f += 0.003 * math.sin(p.x * 50) * soft(p.z, 0.85, 0.95)                                                # gathered at the waistband
    return f
shirt = garment("FacelingShirtMesh", in_shirt, 0.008, M_SHIRT, shirt_folds, [((0, 0, 0.89), (0, 0, 1)), ((0, 0, 0.93), (0, 0, 1)), ((0, 0, 1.47), (0, 0, 1)), ((0.175, 0, 0), (1, 0, 0)), ((-0.175, 0, 0), (1, 0, 0))])
pants = garment("FacelingPantsMesh", in_pants, 0.007, M_PANTS, pants_folds, [((0, 0, 0.075), (0, 0, 1)), ((0, 0, 0.98), (0, 0, 1))])
shoes = garment("FacelingShoesMesh", in_shoes, 0.009, M_SHOES, lambda p: 0.004 * soft(0.03 - p.z, 0, 0.02), [((0, 0, 0.105), (0, 0, 1))])   # a sole that sticks out

# hair: the scalp above a hairline, thickened into clumps that run back from the forehead
h = body.copy(); h.data = body.data.copy(); h.name = "FacelingHairMesh"; col.objects.link(h)
bm = bmesh.new(); bm.from_mesh(h.data)
def scalp(c):
    if c.z < 1.6: return False
    front = c.y < -0.04
    line = 1.715 if front else (1.62 - 0.07 * max(0.0, c.y) / 0.1)   # higher at the brow, down over the nape
    return c.z > line and abs(c.x) < 0.095 + (0.0 if front else 0.01)
bmesh.ops.delete(bm, geom=[f for f in bm.faces if not scalp(f.calc_center_median())], context="FACES")
bm.normal_update()
for v in bm.verts:
    ang = math.atan2(v.co.x, v.co.y)
    # fine, uneven strands: several frequencies so it never reads as regular ribs; fuller over the crown and back
    strands = 0.0016 * math.sin(ang * 47 + v.co.z * 21) + 0.0011 * math.sin(ang * 113 + 1.7) + 0.0008 * math.sin(ang * 211 + v.co.z * 60)
    v.co += v.normal * (0.008 + strands + 0.004 * max(0.0, v.co.y) / 0.1)
bm.to_mesh(h.data); bm.free()
h.data.materials.clear(); h.data.materials.append(M_HAIR)
so = h.modifiers.new("Thickness", "SOLIDIFY"); so.thickness = 0.006; so.offset = -1; so.use_rim = True
apply_all(h)

# the body under the clothes is never seen: keep head, neck and hands only
bm = bmesh.new(); bm.from_mesh(body.data)
bmesh.ops.delete(bm, geom=[f for f in bm.faces if (in_shirt(f.calc_center_median()) and f.calc_center_median().z < 1.45) or in_pants(f.calc_center_median()) or in_shoes(f.calc_center_median())], context="FACES")
bm.to_mesh(body.data); bm.free()
print("HQ_CLOTHES", [ (o.name, len(o.data.vertices)) for o in (body, shirt, pants, shoes, h) ])
