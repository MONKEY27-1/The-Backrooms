# Builds the Facelings (wiki Entity 9) from scratch, three rigged models with the same bone names:
#   Faceling.fbx         - an ordinary adult: plain clothes, and a head with no face at all
#   FacelingChild.fbx    - a child, same, smaller with a bigger head
#   FacelingEvolved.fbx  - Level 3's Evolved: hunched, ragged, a malformed face (pin eyes, a lipless open mouth,
#                          fused nostrils), a length of pipe in its right hand
# Clothes are separate material slots (shirt, trousers, shoes, skin, hair) so the game recolours each one.
# Run: blender -b Art/Monsters.blend --python Art/build_faceling.py
import bpy, bmesh, math, os, random, sys
from mathutils import Vector
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from monster_kit import mat, collection, humanoid, head, rig, export

HERE = os.path.dirname(bpy.data.filepath) or os.path.dirname(os.path.abspath(__file__))
OUTDIR = os.path.abspath(os.path.join(HERE, "..", "Assets", "_Game", "Resources", "Monsters"))
os.makedirs(OUTDIR, exist_ok=True)

shirt = mat("FacelingShirt", (0.5, 0.5, 0.55), 0.8)
pants = mat("FacelingPants", (0.2, 0.22, 0.28), 0.8)
skinm = mat("FacelingSkin", (0.75, 0.6, 0.5), 0.5)
shoes = mat("FacelingShoes", (0.08, 0.07, 0.06), 0.6)
hairm = mat("FacelingHair", (0.1, 0.07, 0.05), 0.7)
pit = mat("FacelingPit", (0.05, 0.02, 0.02), 0.9)
eyem = mat("FacelingEyeDot", (0.02, 0.02, 0.02), 0.2)
pipe = mat("FacelingPipe", (0.35, 0.33, 0.3), 0.4)

def dress(body, s, hip=0.95):
    for m in (shirt, pants, skinm, shoes): body.data.materials.append(m)
    for poly in body.data.polygons:
        z = poly.center.z / s
        x = abs(poly.center.x) / s
        i = 0                                        # shirt
        if z < 0.08: i = 3
        elif x > 0.23 and 0.7 < z < 0.95: i = 2      # hands
        elif z < hip: i = 1
        elif z > 1.5: i = 2                          # neck
        poly.material_index = i

def hair_cap(prefix, link, h, r):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=32, ring_count=16, radius=r * 1.07, location=h.location + Vector((0, r * 0.1, r * 0.18)))
    c = link(bpy.context.object); c.name = prefix + "Hair"
    c.scale = (0.9, 1.0, 0.95); bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    bm = bmesh.new(); bm.from_mesh(c.data)
    bmesh.ops.delete(bm, geom=[v for v in bm.verts if v.co.z < -r * 0.1 or (v.co.y < -r * 0.45 and v.co.z < r * 0.5)], context="VERTS")
    bm.to_mesh(c.data); bm.free()
    c.data.materials.append(hairm); bpy.ops.object.shade_smooth()
    return c

def faceless(prefix, link, center, r, scale):
    h = head(prefix, link, center, r, scale)
    bm = bmesh.new(); bm.from_mesh(h.data)
    for v in bm.verts:   # the face is just smooth skin: a faint brow and cheek shape, nothing else
        x, y, z = v.co
        if y < -r * 0.5 and abs(x) < r * 0.6 and -r * 0.3 < z < r * 0.25: v.co.y += r * 0.04 * (1 - abs(x) / (r * 0.6))
    bm.to_mesh(h.data); bm.free()
    h.data.materials.append(skinm)
    return h

def build(name, prefix, s, head_r, head_scale, evolved=False):
    col = collection(name)
    body, pos, link = humanoid(prefix, col, s, 1.0, 0.95 if not evolved else 0.85, 0.07)
    dress(body, s)
    hc = pos["neck1"] + Vector((0, -0.01, head_r * 0.85))
    if evolved:
        h = head(prefix, link, hc, head_r, head_scale)
        bm = bmesh.new(); bm.from_mesh(h.data)
        for v in bm.verts:
            x, y, z = v.co
            if y < -head_r * 0.4:
                d = math.hypot(x, z + head_r * 0.35)            # a lipless mouth, hanging open
                if d < head_r * 0.28: v.co.y += head_r * 0.35 * (1 - (d / (head_r * 0.28)) ** 2)
                if abs(x) < head_r * 0.12 and -head_r * 0.12 < z < head_r * 0.02: v.co.y -= head_r * 0.06   # fused nostril lump
                if 0.25 * head_r < z < 0.42 * head_r: v.co.y -= head_r * 0.05   # heavy brow
        bm.to_mesh(h.data); bm.free()
        h.data.materials.append(skinm)
        extras = []
        for sx in (1, -1):   # pin-prick eyes under the brow
            bpy.ops.mesh.primitive_uv_sphere_add(segments=8, ring_count=6, radius=head_r * 0.07, location=hc + Vector((sx * head_r * 0.3, -head_r * 0.86, head_r * 0.22)))
            e = link(bpy.context.object); e.name = f"{prefix}Eye{'L' if sx > 0 else 'R'}"; e.data.materials.append(eyem); extras.append((e, "Head"))
        bpy.ops.mesh.primitive_uv_sphere_add(segments=12, ring_count=8, radius=head_r * 0.26, location=hc + Vector((0, -head_r * 0.62, -head_r * 0.35)))
        m = link(bpy.context.object); m.name = prefix + "Mouth"; m.data.materials.append(pit); extras.append((m, "Head"))
        # a length of pipe in the right hand
        hand = pos["palmR"]
        bpy.ops.mesh.primitive_cylinder_add(vertices=10, radius=0.022, depth=0.75, location=hand + Vector((0, -0.12, -0.05)))
        p = link(bpy.context.object); p.name = prefix + "Pipe"; p.rotation_euler = (math.radians(70), 0, 0)
        p.data.materials.append(pipe); extras.append((p, "Hand.R"))
        parts = [(h, "Head")] + extras
    else:
        h = faceless(prefix, link, hc, head_r, head_scale)
        hair = hair_cap(prefix, link, h, head_r)
        parts = [(h, "Head"), (hair, "Head")]
    rig(prefix, col, link, pos, hc + Vector((0, 0, head_r * 1.2)), parts, [body])
    export(col, os.path.join(OUTDIR, name + ".fbx"))
    return sum(len(o.data.vertices) for o in col.objects if o.type == "MESH")

random.seed(9)
a = build("Faceling", "Faceling", 1.0, 0.105, (0.85, 0.95, 1.1))
c = build("FacelingChild", "FacelingChild", 0.62, 0.095, (0.88, 0.95, 1.02))
e = build("FacelingEvolved", "FacelingEvolved", 1.02, 0.11, (0.9, 1.0, 1.05), evolved=True)
print("FACELING_OK", a, c, e)
