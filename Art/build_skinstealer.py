# Builds the Skin-Stealer (wiki Entity 10) from scratch, as two rigged models with the same bone names:
#   SkinStealerWanderer.fbx - the disguise: an ordinary lost survivor (jacket, jeans, backpack, a wet patch on the side)
#   SkinStealer.fbx         - what's underneath: tall, thin, pale yellow, sucker-pocked skin, sunken white eyes,
#                             strips of the stolen skin still hanging off it
# Run: blender -b Art/Monsters.blend --python Art/build_skinstealer.py
import bpy, bmesh, math, os, random
from mathutils import Vector

HERE = os.path.dirname(bpy.data.filepath) or os.path.dirname(os.path.abspath(__file__))
OUTDIR = os.path.abspath(os.path.join(HERE, "..", "Assets", "_Game", "Resources", "Monsters"))

import sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from monster_kit import mat, collection, humanoid, head, rig, export

os.makedirs(OUTDIR, exist_ok=True)

# =============== the disguise: a lost wanderer ===============
random.seed(10)
col = collection("SkinStealerWanderer")
jeans = mat("WandererJeans", (0.12, 0.15, 0.22), 0.8)
jacket = mat("WandererJacket", (0.2, 0.22, 0.13), 0.75)
skinm = mat("WandererSkin", (0.62, 0.45, 0.36), 0.5)
shoes = mat("WandererShoes", (0.05, 0.045, 0.04), 0.6)
wound = mat("WandererWound", (0.07, 0.06, 0.05), 0.08)   # the wet patch: glossy, clear fluid soaking the jacket
hairm = mat("WandererHair", (0.06, 0.045, 0.035), 0.7)
packm = mat("WandererPack", (0.25, 0.08, 0.06), 0.7)
body, pos, link = humanoid("Wanderer", col, 1.0, 1.0, 1.0, 0.07)
for m in (jeans, jacket, skinm, shoes, wound): body.data.materials.append(m)
for poly in body.data.polygons:   # paint the clothes by region
    c = poly.center
    i = 1   # jacket
    if c.z < 0.08: i = 3
    elif abs(c.x) > 0.23 and 0.7 < c.z < 0.95: i = 2   # hands
    elif c.z < 0.95: i = 0
    elif c.z > 1.5: i = 2                              # neck
    if 1.05 < c.z < 1.22 and 0.08 < c.x < 0.17 and c.y < 0.05: i = 4   # the wound, low on the left side
    poly.material_index = i
h = head("Wanderer", link, pos["neck1"] + Vector((0, -0.01, 0.085)), 0.105, (0.85, 0.95, 1.08))
bm = bmesh.new(); bm.from_mesh(h.data)
for v in bm.verts:   # a plain face: nose, brow, cheekbones, eye sockets
    x, y, z = v.co
    if y < -0.06:
        if abs(x) < 0.015 and -0.03 < z < 0.02: v.co.y -= 0.022 * (1 - abs(x) / 0.015)
        if 0.025 < abs(x) < 0.05 and 0.015 < z < 0.035: v.co.y += 0.008
        if 0.01 < z < 0.04 and abs(x) < 0.06: v.co.y -= 0.004
bm.to_mesh(h.data); bm.free()
h.data.materials.append(skinm)
bpy.ops.mesh.primitive_uv_sphere_add(segments=32, ring_count=16, radius=0.112, location=h.location + Vector((0, 0.012, 0.02)))
hair = link(bpy.context.object); hair.name = "WandererHair"
hair.scale = (0.88, 0.98, 1.0); bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
bm = bmesh.new(); bm.from_mesh(hair.data)
bmesh.ops.delete(bm, geom=[v for v in bm.verts if v.co.z < -0.01 or (v.co.y < -0.05 and v.co.z < 0.05)], context="VERTS")
bm.to_mesh(hair.data); bm.free()
hair.data.materials.append(hairm); bpy.ops.object.shade_smooth()
bpy.ops.mesh.primitive_cube_add(size=1, location=(0, 0.17, 1.22))
pack = link(bpy.context.object); pack.name = "WandererPack"
pack.scale = (0.3, 0.16, 0.42); bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
bev = pack.modifiers.new("Bevel", "BEVEL"); bev.width = 0.03; bev.segments = 3
bpy.context.view_layer.objects.active = pack; bpy.ops.object.modifier_apply(modifier="Bevel")
pack.data.materials.append(packm)
top = pos["neck1"] + Vector((0, -0.01, 0.25))
rig("Wanderer", col, link, pos, top, [(h, "Head"), (hair, "Head"), (pack, "Chest")], [body])
export(col, os.path.join(OUTDIR, "SkinStealerWanderer.fbx"))
wander_verts = sum(len(o.data.vertices) for o in col.objects if o.type == "MESH")

# =============== the true form ===============
random.seed(11)
col = collection("SkinStealer")
yellow = mat("StealerSkin", (0.5, 0.43, 0.2), 0.3)
socket = mat("StealerSocket", (0.04, 0.03, 0.02), 0.9)
eyem = mat("StealerEye", (0.92, 0.92, 0.88), 0.2)
shred = mat("StealerShred", (0.42, 0.1, 0.08), 0.25)   # raw underside: wet, red
body, pos, link = humanoid("Stealer", col, 1.3, 1.35, 0.8, 0.12, finger=0.2, finger_r=0.013, subdiv=3)
bm = bmesh.new(); bm.from_mesh(body.data); bm.normal_update()
def cell(p, k):   # distance to the nearest point of a jittered grid: little sucker pits all over
    q = p * k
    best = 9.0
    for dx in (-1, 0, 1):
        for dy in (-1, 0, 1):
            for dz in (-1, 0, 1):
                c = Vector((math.floor(q.x) + dx, math.floor(q.y) + dy, math.floor(q.z) + dz))
                random.seed(int(c.x * 73856093) ^ int(c.y * 19349663) ^ int(c.z * 83492791))
                c += Vector((random.random(), random.random(), random.random()))
                best = min(best, (q - c).length)
    return best
for v in bm.verts:
    d = cell(v.co, 20.0)
    v.co -= v.normal * (0.011 * max(0.0, 1 - d * 2.6))   # a pit at each cell centre
    x, y, z = v.co
    if 1.65 < z < 1.95 and abs(x) > 0.04 and abs(x) < 0.2: v.co += v.normal * 0.012 * max(0.0, math.sin(z * 95))   # ribs
bm.to_mesh(body.data); bm.free()
body.data.materials.append(yellow)
hc = pos["neck1"] + Vector((0, -0.02, 0.12))
h = head("Stealer", link, hc, 0.12, (0.78, 0.95, 1.25))
bm = bmesh.new(); bm.from_mesh(h.data)
for v in bm.verts:   # deep sockets, no nose, a thin slit of a mouth
    x, y, z = v.co
    if y < -0.04:
        for ex in (0.038, -0.038):
            d = math.hypot(x - ex, z - 0.03)
            if d < 0.042: v.co.y += 0.05 * (1 - (d / 0.042) ** 2)
        if abs(z + 0.06) < 0.006 and abs(x) < 0.04: v.co.y += 0.012
bm.to_mesh(h.data); bm.free()
h.data.materials.append(yellow)
eyes = []
for sd, ex in (("L", 0.038), ("R", -0.038)):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=12, ring_count=8, radius=0.009, location=hc + Vector((ex, -0.06, 0.03 * 1.25)))
    e = link(bpy.context.object); e.name = "StealerEye" + sd; e.data.materials.append(eyem); eyes.append(e)
# torn strips of stolen skin hanging from the shoulders and hips
sb = bmesh.new()
for i in range(10):
    side = 1 if i % 2 else -1
    at = Vector((side * random.uniform(0.08, 0.26) * 1.3, random.uniform(-0.12, 0.12), random.choice((1.85, 1.75, 1.3, 1.25))))
    w, L = random.uniform(0.04, 0.08), random.uniform(0.18, 0.4)
    prev = None
    for k in range(5):
        t = k / 4
        p = at + Vector((random.uniform(-0.01, 0.01), (-0.06 if at.y < 0 else 0.06) * t, -L * t))
        a, b = sb.verts.new(p + Vector((-w / 2 * (1 - 0.6 * t), 0, 0))), sb.verts.new(p + Vector((w / 2 * (1 - 0.6 * t), 0, 0)))
        if prev: sb.faces.new((prev[0], prev[1], b, a))
        prev = (a, b)
sm = bpy.data.meshes.new("StealerShreds"); sb.to_mesh(sm); sb.free()
shreds = link(bpy.data.objects.new("StealerShreds", sm)); shreds.data.materials.append(shred)
top = hc + Vector((0, 0, 0.16))
rig("Stealer", col, link, pos, top, [(h, "Head"), (eyes[0], "Head"), (eyes[1], "Head"), (shreds, "Chest")], [body])
export(col, os.path.join(OUTDIR, "SkinStealer.fbx"))
print("STEALER_OK", wander_verts, sum(len(o.data.vertices) for o in col.objects if o.type == "MESH"))
