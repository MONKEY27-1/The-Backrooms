# Partygoer (wiki Entity 67): tall, leathery, bright yellow; a smile carved into a blank head and smeared with blood;
# thick blocky legs; boneless arms ending in sucker mouths of hooked teeth; a chest shell that springs open on clawed arms.
# Run: blender -b --factory-startup --python Art/build_partygoer.py        (saves Art/Partygoer.blend, exports the FBX and maps)
# Faces -Y, z up, feet at 0, about 2.25 m.
import bpy, bmesh, math, os, random
from mathutils import Vector, noise
import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.abspath(os.path.join(HERE, "..", "Assets", "_Game", "Resources", "Monsters"))
TEX = os.path.join(OUT, "PartygoerTex"); os.makedirs(TEX, exist_ok=True)
SHOTS = os.environ.get("PG_SHOTS", "")
random.seed(67)
bpy.ops.wm.read_factory_settings(use_empty=True)
scene = bpy.context.scene
col = scene.collection
def link(o): col.objects.link(o); return o
def active(o):
    bpy.ops.object.select_all(action="DESELECT"); o.select_set(True); bpy.context.view_layer.objects.active = o
def apply_all(o):
    active(o)
    for m in list(o.modifiers): bpy.ops.object.modifier_apply(modifier=m.name)
def soft(x, a, b): return max(0.0, min(1.0, (x - a) / (b - a)))
def lin(c): return tuple(((v + 0.055) / 1.055) ** 2.4 if v > 0.04045 else v / 12.92 for v in c)
def mix(a, b, t): return tuple(x + (y - x) * t for x, y in zip(a, b))

def loft(name, rings, seg=40, up=None):
    """rings: (centre, rx, ry, squareness 2=ellipse..5=box). Sections face along the path."""
    cs = [Vector(r[0]) for r in rings]
    verts, faces = [], []
    side = Vector((1, 0, 0))
    for k, (c, rx, ry, sq) in enumerate(rings):
        t = (cs[min(k + 1, len(cs) - 1)] - cs[max(k - 1, 0)]).normalized()
        s = (side - t * side.dot(t)).normalized(); f = t.cross(s); side = s
        for i in range(seg):
            a = 2 * math.pi * i / seg; ca, sa = math.cos(a), math.sin(a)
            x = math.copysign(abs(ca) ** (2 / sq), ca) * rx; y = math.copysign(abs(sa) ** (2 / sq), sa) * ry
            verts.append(cs[k] + s * x + f * y)
    for k in range(len(rings) - 1):
        for i in range(seg):
            j = (i + 1) % seg
            faces.append((k * seg + i, k * seg + j, (k + 1) * seg + j, (k + 1) * seg + i))
    faces.append(tuple(range(seg))[::-1]); n = (len(rings) - 1) * seg; faces.append(tuple(range(n, n + seg)))
    me = bpy.data.meshes.new(name); me.from_pydata(verts, [], faces)
    o = link(bpy.data.objects.new(name, me))
    b = bmesh.new(); b.from_mesh(me); bmesh.ops.recalc_face_normals(b, faces=b.faces); b.to_mesh(me); b.free()
    return o

def curve(keys, n):
    """Smooth rings through key rings (Catmull-Rom on every number)."""
    out = []
    for i in range(n):
        t = i / (n - 1) * (len(keys) - 1); k = min(int(t), len(keys) - 2); u = t - k
        p = [np.array([*key[0], *key[1:]], float) for key in (keys[max(k - 1, 0)], keys[k], keys[k + 1], keys[min(k + 2, len(keys) - 1)])]
        v = 0.5 * (2 * p[1] + (p[2] - p[0]) * u + (2 * p[0] - 5 * p[1] + 4 * p[2] - p[3]) * u * u + (3 * p[1] - p[0] - 3 * p[2] + p[3]) * u ** 3)
        out.append(((v[0], v[1], v[2]), max(v[3], 0.004), max(v[4], 0.004), max(v[5], 2.0)))
    return out

def ball(name, at, r, scale=(1, 1, 1)):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=48, ring_count=32, radius=1, location=at)
    o = bpy.context.object; o.name = name; o.scale = [r * s for s in scale]
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    return o

def fuse(parts, name, voxel, smooth):
    bpy.ops.object.select_all(action="DESELECT")
    for p in parts: p.select_set(True)
    bpy.context.view_layer.objects.active = parts[0]
    bpy.ops.object.join(); o = bpy.context.object; o.name = name
    o.data.remesh_voxel_size = voxel; bpy.ops.object.voxel_remesh()
    if smooth:
        m = o.modifiers.new("S", "LAPLACIANSMOOTH"); m.iterations = smooth; m.lambda_factor = 1.0; m.use_volume_preserve = True
        apply_all(o)
    return o

# ------------------------------------------------ body
HEAD = Vector((0, -0.03, 2.03)); HR = 0.185
torso = loft("Torso", curve([((0, 0.02, 0.86), 0.17, 0.14, 2.6), ((0, 0.02, 1.0), 0.215, 0.16, 2.8), ((0, 0.015, 1.15), 0.2, 0.15, 2.4),
    ((0, 0.0, 1.32), 0.245, 0.19, 2.4), ((0, 0.005, 1.52), 0.275, 0.2, 2.4), ((0, 0.02, 1.66), 0.235, 0.16, 2.2),
    ((0, 0.02, 1.75), 0.13, 0.115, 2.0), ((0, 0.0, 1.83), 0.085, 0.085, 2.0), ((0, -0.02, 1.95), 0.08, 0.085, 2.0)], 40), 56)
parts = [torso]
for sx in (1, -1):
    parts.append(loft("Leg", curve([((0.1 * sx, 0.02, 1.02), 0.12, 0.13, 2.6), ((0.135 * sx, 0.015, 0.85), 0.135, 0.15, 3.2), ((0.15 * sx, 0.0, 0.62), 0.118, 0.135, 3.6),
        ((0.155 * sx, -0.005, 0.5), 0.112, 0.125, 3.6), ((0.16 * sx, 0.012, 0.35), 0.122, 0.135, 3.8), ((0.165 * sx, 0.01, 0.14), 0.118, 0.13, 4.2), ((0.165 * sx, 0.0, 0.0), 0.13, 0.15, 4.6)], 30), 48))
    parts.append(loft("Foot", curve([((0.165 * sx, 0.14, 0.065), 0.1, 0.06, 3.4), ((0.168 * sx, 0.02, 0.085), 0.132, 0.085, 4.0), ((0.175 * sx, -0.14, 0.065), 0.145, 0.065, 4.0), ((0.178 * sx, -0.25, 0.045), 0.13, 0.044, 3.4)], 18), 40))
    parts.append(ball("Shoulder", (0.27 * sx, 0.015, 1.6), 0.105, (1, 0.95, 1)))
parts.append(ball("Head", HEAD, HR, (0.98, 0.9, 1.08)))
body = fuse(parts, "BodyHigh", 0.0055, 10)
# the chest cavity the shell covers
CAV = Vector((0, -0.16, 1.43)); CR = Vector((0.15, 0.15, 0.225))
cut = ball("Cut", CAV, 1, CR)
bo = body.modifiers.new("B", "BOOLEAN"); bo.operation = "DIFFERENCE"; bo.object = cut; bo.solver = "EXACT"; apply_all(body)
bpy.data.objects.remove(cut, do_unlink=True)
body.data.remesh_voxel_size = 0.0045; active(body); bpy.ops.object.voxel_remesh()

def ridged(p, s):   # leather: a cracked web of fine creases
    d = noise.voronoi(p * s)[0]
    return min(1.0, (d[1] - d[0]) * 2.2)
EYES = [Vector((0.066, 0, 2.085)), Vector((-0.066, 0, 2.085))]
def face_depth(p):
    """0..1: how deep into the carved smile this point is (a wide arc, ragged where the knife slipped, and two gouged eyes)."""
    if p.y > HEAD.y - 0.06: return 0.0
    n = noise.noise(p * 45) * 0.006 + noise.noise(p * 120) * 0.003
    x, z = p.x, p.z
    best = 0.0
    for e in EYES:
        d = math.hypot((x - e.x) / 0.021, (z - e.z) / 0.042) + n * 25
        best = max(best, 1 - soft(d, 0.72, 1.08))
    cx, cz, R = 0.0, 2.075, 0.13
    a = math.atan2(x - cx, -(z - cz))   # 0 = straight down
    d = abs(math.hypot(x - cx, (z - cz)) - R)
    w = 0.017 * (1 - (abs(a) / 1.22) ** 4) + n * 0.9
    if abs(a) < 1.22 and w > 0: best = max(best, 1 - soft(d, w * 0.65, w * 1.15))
    best = max(best, 0.7 * max(0.0, 1 - math.hypot((abs(a) - 1.2) / 0.07, d / 0.022) ** 2))   # a short cut closes each corner
    return best
def blood_face(p):
    """Smears: round the cuts, dripping down from them, one wiped sideways."""
    if p.z < 1.78 or p.y > HEAD.y: return 0.0
    k = 0.0
    for dx in np.linspace(-0.1, 0.1, 9):
        lane = math.exp(-((p.x - dx - 0.006 * math.sin(p.z * 60 + dx * 90)) / (0.006 + 0.004 * abs(math.sin(dx * 300)))) ** 2)
        a = abs(dx) / 0.13; top = 2.075 - 0.13 * math.sqrt(max(0.0, 1 - a * a)); L = 0.05 + 0.12 * abs(math.sin(dx * 777))
        if p.z < top: k = max(k, lane * soft(p.z, top - L, top - L * 0.4))
    for e in EYES:
        for o in (-0.01, 0.009):
            lane = math.exp(-((p.x - e.x - o) / 0.006) ** 2)
            if p.z < e.z: k = max(k, lane * soft(p.z, e.z - 0.09 - o * 3, e.z - 0.02))
    near = max(face_depth(p + Vector((ox, 0, oz))) for ox in (-0.014, 0, 0.014) for oz in (-0.014, 0, 0.014))
    k = max(k, near * (0.55 + 0.45 * noise.noise(p * 30)))
    smear = math.exp(-((p.z - 2.0 - (p.x - 0.05) * 0.5) / 0.03) ** 2) * soft(p.x, 0.02, 0.06) * (0.5 + 0.6 * noise.noise(Vector((p.x * 12, p.y * 12, p.z * 130))))
    return max(0.0, min(1.0, max(k, smear)))

YEL, YEL2, DIRT, RED, DARK, FLESH = (0.96, 0.78, 0.07), (0.86, 0.6, 0.04), (0.42, 0.27, 0.05), (0.36, 0.015, 0.012), (0.07, 0.008, 0.008), (0.5, 0.1, 0.09)
def paint(o, fn):
    me = o.data
    ca = me.color_attributes.get("Col") or me.color_attributes.new("Col", "FLOAT_COLOR", "POINT")
    buf = np.ones((len(me.vertices), 4), np.float32)
    for i, v in enumerate(me.vertices): buf[i, :3] = lin(fn(v.co.copy(), v.normal.copy()))
    ca.data.foreach_set("color", buf.ravel())
def shape(o, fn):
    nrm = [v.normal.copy() for v in o.data.vertices]
    for v, n in zip(o.data.vertices, nrm): v.co = fn(v.co.copy(), n)
    o.data.update()
def in_cavity(p): q = Vector(((p.x - CAV.x) / CR.x, (p.y - CAV.y) / CR.y, (p.z - CAV.z) / CR.z)); return q.length < 1.04 and p.y > -0.5
def folds(p):
    f = 0.0
    for z0, w, k in ((0.5, 0.06, 70), (0.12, 0.05, 110), (0.93, 0.05, 80), (1.15, 0.07, 60), (1.82, 0.05, 110)):   # knees, ankles, hips, waist, neck
        f += math.sin(p.z * k + noise.noise(p * 7) * 3) * math.exp(-((p.z - z0) / w) ** 2)
    return f
def vein(p):
    q = p + Vector((noise.noise(p * 5), noise.noise(p * 5 + Vector((9, 0, 0))), 0)) * 0.05
    return max(0.0, 1 - abs(noise.noise(Vector((q.x * 9, q.y * 9, q.z * 3)))) * 14) * (0.5 + 0.5 * noise.noise(p * 2))
def anatomy(p):
    d = 0.0
    if 0.98 < p.z < 1.7: d += 0.012 * max(0.0, math.sin((p.z + abs(p.x) * 0.55) * 78)) * soft(abs(p.x), 0.06, 0.16) * soft(p.z, 1.0, 1.15) * soft(1.68 - p.z, 0, 0.15)   # ribs
    if p.y > 0.03 and abs(p.x) < 0.05 and 0.9 < p.z < 1.85: d += 0.022 * (0.5 + 0.5 * math.sin(p.z * 85)) * (1 - abs(p.x) / 0.05)                                     # the spine
    for sx in (1, -1): d += 0.02 * math.exp(-((p - Vector((0.1 * sx, -0.12, 1.68))).length / 0.06) ** 2) - 0.02 * math.exp(-((p - Vector((0.07 * sx, -0.14, 1.08))).length / 0.09) ** 2)
    return d
def body_shape(p, n):
    if in_cavity(p): return p + n * (0.012 * noise.noise(p * 22) + 0.006 * math.sin(p.z * 90 + p.x * 20))
    d = -0.0011 * (1 - ridged(p, 75)) - 0.0008 * (1 - ridged(p, 150)) + 0.009 * noise.noise(p * 5) + 0.0055 * folds(p) + 0.0035 * vein(p) + anatomy(p)
    fd = face_depth(p)
    q = p + n * d
    if fd > 0: q = q + Vector((0, 1, 0)) * (0.05 * fd)
    return q
face_cache = {}
rest_face = [face_depth(v.co) for v in body.data.vertices]
rest_blood = [blood_face(v.co) for v in body.data.vertices]
rest_crack = [ridged(v.co, 75) for v in body.data.vertices]
rest_cav = [in_cavity(v.co) for v in body.data.vertices]
shape(body, body_shape)
ids = {}
def body_paint(p, n):
    i = body_paint.i; body_paint.i += 1
    if rest_cav[i]: return mix(FLESH, DARK, min(1, max(0, 0.45 + noise.noise(p * 20) * 0.6)))
    t = 0.5 + 0.5 * noise.noise(p * 4)
    c = mix(YEL, YEL2, t * 0.7)
    c = mix(c, DIRT, (1 - rest_crack[i]) * 0.2)
    c = mix(c, DIRT, 0.6 * soft(noise.noise(p * 3.3 + Vector((3, 1, 0))) + 0.5 * noise.noise(p * 11), 0.05, 0.6))   # old stains
    c = mix(c, (0.5, 0.2, 0.06), 0.35 * max(0.0, vein(p)))
    c = mix(c, DIRT, 0.75 * soft(0.5 - p.z + noise.noise(p * 9) * 0.25, 0, 0.5))       # filthy feet and shins
    c = mix(c, RED, 0.85 * soft(0.32 - p.z + noise.noise(p * 14) * 0.3, 0.1, 0.35) * (1 if noise.noise(p * 3 + Vector((5, 5, 5))) > 0.05 else 0))
    sp = noise.voronoi(p * 11)[0][0]
    if p.z > 0.9 and p.y < 0 and sp < 0.07 + 0.14 * noise.noise(p * 3): c = mix(c, RED, 0.85)   # spatter down the front
    c = mix(c, RED, rest_blood[i] * 0.95)
    run = soft(noise.noise(Vector((p.x * 22, p.y * 22, p.z * 2.2))) + 0.35 * noise.noise(p * 14), 0.2, 0.5) * soft(p.z, 1.0, 1.75) * soft(1.95 - p.z, 0, 0.06)   # dried runs down the neck and chest
    c = mix(c, RED, 0.8 * run * (1 if p.y < 0.03 else 0))
    return mix(c, DARK, min(1, rest_face[i] * 1.3))
body_paint.i = 0
paint(body, body_paint)

# ------------------------------------------------ arms: boneless, long, ending in a toothed cup
NB = 7   # bones per arm
def arm_path(sx):
    return [((0.25 * sx, 0.015, 1.62), 0.085, 0.08, 2), ((0.36 * sx, 0.01, 1.52), 0.07, 0.07, 2), ((0.42 * sx, 0.0, 1.3), 0.058, 0.058, 2), ((0.44 * sx, -0.01, 1.05), 0.054, 0.054, 2),
            ((0.45 * sx, -0.02, 0.8), 0.05, 0.05, 2), ((0.455 * sx, -0.03, 0.58), 0.046, 0.046, 2), ((0.46 * sx, -0.04, 0.44), 0.056, 0.056, 2), ((0.462 * sx, -0.045, 0.34), 0.088, 0.088, 2), ((0.463 * sx, -0.047, 0.3), 0.092, 0.092, 2)]
arms, teeth_objs, chains = {}, {}, {}
for s, sx in (("L", 1), ("R", -1)):
    rings = curve(arm_path(sx), 80)
    a = loft("Arm", rings, 36)
    end, prev = Vector(rings[-1][0]), Vector(rings[-6][0]); axis = (end - prev).normalized()
    cup = ball("Cup", end + axis * 0.012, 0.068, (1, 1, 1.25))
    bo = a.modifiers.new("B", "BOOLEAN"); bo.operation = "DIFFERENCE"; bo.object = cup; bo.solver = "EXACT"; apply_all(a)
    bpy.data.objects.remove(cup, do_unlink=True)
    a = fuse([a], "Arm" + s + "High", 0.0038, 3)
    pts = [Vector(rings[int(round(i * (len(rings) - 1) / NB))][0]) for i in range(NB + 1)]; chains[s] = pts
    centre = [Vector(r[0]) for r in rings]
    def along(p):
        k = min(range(len(centre)), key=lambda i: (centre[i] - p).length_squared); return k / (len(centre) - 1)
    al = [along(v.co) for v in a.data.vertices]
    inside = [(v.co - (end + axis * 0.012)).length < 0.087 and v.normal.dot((end + axis * 0.03) - v.co) > 0 for v in a.data.vertices]
    crack = [ridged(v.co, 80) for v in a.data.vertices]
    def arm_shape(p, n, it=iter(range(10 ** 9))):
        i = next(it); t = al[i]
        if inside[i]: return p + n * (0.005 * math.sin(math.atan2(p.x - end.x, p.y - end.y) * 12))
        ring = math.sin(t * 260 + noise.noise(p * 8) * 1.5)                                         # a worm's rings
        return p + n * (0.0035 * ring * (0.35 + 0.65 * t) - 0.0016 * (1 - crack[i]) + 0.004 * noise.noise(p * 9))
    shape(a, arm_shape)
    def arm_paint(p, n, it=iter(range(10 ** 9))):
        i = next(it); t = al[i]
        if inside[i]: return mix(FLESH, DARK, min(1, max(0, 0.35 + noise.noise(p * 30) * 0.6 + (0.5 if (p - end).dot(axis) < -0.02 else 0))))
        c = mix(YEL, YEL2, 0.35 + 0.35 * noise.noise(p * 5)); c = mix(c, DIRT, (1 - crack[i]) * 0.2); c = mix(c, DIRT, 0.5 * soft(noise.noise(p * 4) + 0.5 * noise.noise(p * 12), 0.05, 0.6))
        c = mix(c, DIRT, 0.3 * max(0.0, -math.sin(t * 260 + noise.noise(p * 8) * 1.5)))
        return mix(c, RED, min(1.0, max(0.0, soft(t, 0.74, 0.97) * 1.25 + noise.noise(p * 16) * 0.5 * soft(t, 0.5, 0.8))))
    paint(a, arm_paint)
    arms[s] = (a, al)
    # hooked teeth: an outer ring curling inwards and a deeper one
    tb = bmesh.new()
    s1 = axis.orthogonal().normalized(); s2 = axis.cross(s1)
    for count, r, depth, L in ((13, 0.074, 0.0, 0.06), (9, 0.05, -0.035, 0.045)):
        for i in range(count):
            ang = 2 * math.pi * (i + random.uniform(-0.15, 0.15)) / count
            out = s1 * math.cos(ang) + s2 * math.sin(ang)
            root = end + axis * depth + out * r; L2 = L * random.uniform(0.8, 1.15); prev_ring = None
            for k in range(6):
                u = k / 5; w = 0.0105 * (1 - u) ** 0.8 * (L / 0.06) + 0.0003
                cpos = root + axis * (L2 * math.sin(u * 1.5) * 0.9) - out * (L2 * 0.75 * u * u)
                ta = out.cross(axis)
                ring = [tb.verts.new(cpos + (ta * math.cos(q) + out * math.sin(q)) * w) for q in np.linspace(0, 2 * math.pi, 7)[:-1]]
                if prev_ring:
                    for j in range(6): tb.faces.new((prev_ring[j], prev_ring[(j + 1) % 6], ring[(j + 1) % 6], ring[j]))
                prev_ring = ring
            tb.faces.new(prev_ring)
    me = bpy.data.meshes.new("Teeth"); bmesh.ops.recalc_face_normals(tb, faces=tb.faces); tb.to_mesh(me); tb.free()
    teeth_objs[s] = link(bpy.data.objects.new("PartygoerTeeth" + s, me))

# ------------------------------------------------ chest shell: two overlapping halves of plates
shells = {}
SC, SR = Vector((0, 0.0, 1.42)), Vector((0.25, 0.268, 0.34))
for s, sx in (("L", 1), ("R", -1)):
    b = bmesh.new(); NU, NV = 44, 70; grid = []
    for iv in range(NV + 1):
        v = iv / NV; row = []
        for iu in range(NU + 1):
            u = iu / NU
            lo = -0.035 if sx > 0 else -0.0                        # the left half laps over the right
            edge = 1.0 - 0.05 * abs(math.sin(v * math.pi * 6))       # a scalloped outer rim
            az = (lo + (1.13 * edge - lo) * u); el = (v - 0.5) * 2.0 * 1.12 * (1 - 0.12 * u)
            p = Vector((math.sin(az) * math.cos(el) * SR.x * 1.14, -math.cos(az) * math.cos(el) * SR.y, math.sin(el) * SR.z))
            plate = (v * 6) % 1.0
            lift = 1 + 0.055 * plate ** 2 + 0.012 * math.sin(u * 40) * plate + 0.02 * math.exp(-((u - 0.02) / 0.06) ** 2) + (0.02 if sx > 0 else 0)
            p = Vector((p.x * lift * sx, p.y * lift, p.z))
            p += SC; row.append(b.verts.new(p + Vector((1, 1, 1)) * 0.003 * noise.noise(p * 40)))
        grid.append(row)
    for iv in range(NV):
        for iu in range(NU): b.faces.new((grid[iv][iu], grid[iv][iu + 1], grid[iv + 1][iu + 1], grid[iv + 1][iu]))
    bmesh.ops.recalc_face_normals(b, faces=b.faces)
    me = bpy.data.meshes.new("Shell"); b.to_mesh(me); b.free()
    o = link(bpy.data.objects.new("Shell" + s + "High", me))
    c = sum((f.center for f in me.polygons), Vector()) / len(me.polygons)
    if sum(1 for f in me.polygons if f.normal.y < 0) < len(me.polygons) / 2:
        active(o); bpy.ops.object.mode_set(mode="EDIT"); bpy.ops.mesh.select_all(action="SELECT"); bpy.ops.mesh.flip_normals(); bpy.ops.object.mode_set(mode="OBJECT")
    m = o.modifiers.new("T", "SOLIDIFY"); m.thickness = 0.016; m.offset = -1
    m = o.modifiers.new("Sub", "SUBSURF"); m.levels = 1; apply_all(o)
    def shell_paint(p, n):
        plate = (((math.asin(max(-1, min(1, (p.z - SC.z) / SR.z))) / 1.12) + 1) * 0.5 * 6) % 1.0
        c = mix((0.85, 0.55, 0.05), (0.55, 0.28, 0.03), min(1, max(0, 1 - plate + 0.4 * noise.noise(p * 9))))
        c = mix(c, DIRT, 0.35 * (1 - ridged(p, 50)))
        if n.dot(Vector((0, -1, 0))) < -0.2: c = mix(FLESH, DARK, 0.4 + 0.4 * noise.noise(p * 20))      # raw inside
        seam = math.exp(-((p.x) / 0.03) ** 2)
        c = mix(c, RED, min(1.0, max(0.0, seam * (0.9 + 0.5 * noise.noise(p * 18)))))
        if noise.voronoi(p * 13)[0][0] < 0.17: c = mix(c, RED, 0.9)
        return c
    paint(o, shell_paint); shells[s] = o

# ------------------------------------------------ the clawed arms inside (modelled reaching out; the game folds them away)
claw_objs, claw_bones = [], {}
for s, sx in (("L", 1), ("R", -1)):
    for k, z in enumerate((1.53, 1.36)):
        a0 = Vector((0.06 * sx, -0.06, z)); a1 = Vector((0.17 * sx, -0.33, z + (0.1 if k == 0 else -0.02))); a2 = Vector((0.06 * sx, -0.58, z + (0.04 if k == 0 else 0.0)))
        ps = [loft("C", curve([(a0, 0.034, 0.034, 2), (a0.lerp(a1, 0.5) + Vector((0.01 * sx, 0, 0.01)), 0.024, 0.026, 2), (a1, 0.03, 0.03, 2)], 14), 16), ball("J", a1, 0.034),
              loft("C", curve([(a1, 0.026, 0.026, 2), (a1.lerp(a2, 0.5), 0.02, 0.021, 2), (a2, 0.026, 0.02, 2)], 14), 16), ball("H", a2, 0.033, (1, 0.9, 0.8))]
        dirn = (a2 - a1).normalized()
        for f in (-1, 0, 1):
            sd = Vector((f * 0.028, 0, 0.012 * (1 - abs(f))))
            tip = a2 + sd * 2.4 + dirn * 0.15 + Vector((-0.05 * sx, 0, -0.06))
            ps.append(loft("T", curve([(a2 + sd, 0.013, 0.013, 2), (a2 + sd * 2.4 + dirn * 0.085 + Vector((0, 0, 0.012)), 0.009, 0.011, 2), (tip, 0.002, 0.002, 2)], 12), 10))
        o = fuse(ps, f"Claw{s}{k}High", 0.0035, 2)
        shape(o, lambda p, n: p + n * (0.003 * noise.noise(p * 35) - 0.0015 * (1 - ridged(p, 60))))
        def claw_paint(p, n, a1=a1, a2=a2):
            c = mix(FLESH, (0.62, 0.3, 0.1), 0.4 + 0.5 * noise.noise(p * 14))
            t = soft((p - a2).dot(dirn), 0.02, 0.09); c = mix(c, (0.12, 0.07, 0.04), t)
            return mix(c, RED, min(1, max(0, 0.35 + 0.8 * noise.noise(p * 25))))
        paint(o, claw_paint); claw_objs.append((o, s, k)); claw_bones[(s, k)] = (a0, a1, a2)

# ------------------------------------------------ game meshes, baked maps
scene.render.engine = "CYCLES"; scene.cycles.samples = 2; scene.cycles.device = "CPU"
def hmat(o):
    m = bpy.data.materials.new("High"); m.use_nodes = True; nt = m.node_tree; nt.nodes.clear()
    out = nt.nodes.new("ShaderNodeOutputMaterial"); em = nt.nodes.new("ShaderNodeEmission"); at = nt.nodes.new("ShaderNodeVertexColor"); at.layer_name = "Col"
    nt.links.new(at.outputs[0], em.inputs[0]); nt.links.new(em.outputs[0], out.inputs[0])
    o.data.materials.clear(); o.data.materials.append(m)
def join_copy(objs, name):
    cs = []
    for o in objs:
        c = o.copy(); c.data = o.data.copy(); link(c); cs.append(c)
    bpy.ops.object.select_all(action="DESELECT")
    for c in cs: c.select_set(True)
    bpy.context.view_layer.objects.active = cs[0]
    if len(cs) > 1: bpy.ops.object.join()
    o = bpy.context.object; o.name = name; return o
low = {}
for part, objs, target, size in (("Body", [body], 9000, 2048), ("Arms", [arms["L"][0], arms["R"][0]], 5000, 1024), ("Shell", list(shells.values()), 2500, 1024), ("Claws", [c[0] for c in claw_objs], 3000, 512)):
    hi = join_copy(objs, part + "HI"); hmat(hi)
    lo = join_copy(objs, "Partygoer" + part)
    d = lo.modifiers.new("D", "DECIMATE"); d.ratio = min(1.0, target / len(lo.data.polygons) / 1.0)
    apply_all(lo); active(lo)
    bpy.ops.object.mode_set(mode="EDIT"); bpy.ops.mesh.select_all(action="SELECT"); bpy.ops.uv.smart_project(angle_limit=math.radians(66), island_margin=0.003); bpy.ops.object.mode_set(mode="OBJECT")
    for p in lo.data.polygons: p.use_smooth = True
    for p in hi.data.polygons: p.use_smooth = True
    m = bpy.data.materials.new("Partygoer" + part); m.use_nodes = True; nt = m.node_tree
    lo.data.materials.clear(); lo.data.materials.append(m)
    t = nt.nodes.new("ShaderNodeTexImage"); nt.nodes.active = t
    imgs = {}
    for kind in ("color", "normal"):
        im = bpy.data.images.new(f"Partygoer_{part}_{kind}", size, size)
        if kind == "normal": im.colorspace_settings.name = "Non-Color"
        t.image = im
        bpy.ops.object.select_all(action="DESELECT"); hi.select_set(True); lo.select_set(True); bpy.context.view_layer.objects.active = lo
        bk = scene.render.bake; bk.use_selected_to_active = True; bk.cage_extrusion = 0.006; bk.max_ray_distance = 0.014; bk.margin = 6
        bpy.ops.object.bake(type="EMIT" if kind == "color" else "NORMAL")
        im.filepath_raw = os.path.join(TEX, f"Partygoer_{part}_{kind}.png"); im.file_format = "PNG"; im.save(); imgs[kind] = im
    bsdf = next(n for n in nt.nodes if n.type == "BSDF_PRINCIPLED"); bsdf.inputs["Roughness"].default_value = 0.25 if part in ("Shell", "Claws") else 0.42
    t.image = imgs["color"]; nt.links.new(t.outputs[0], bsdf.inputs["Base Color"])
    t2 = nt.nodes.new("ShaderNodeTexImage"); t2.image = imgs["normal"]; nm = nt.nodes.new("ShaderNodeNormalMap")
    nt.links.new(t2.outputs[0], nm.inputs["Color"]); nt.links.new(nm.outputs[0], bsdf.inputs["Normal"])
    low[part] = lo; bpy.data.objects.remove(hi, do_unlink=True)
for o in [body, arms["L"][0], arms["R"][0], *shells.values(), *[c[0] for c in claw_objs]]: bpy.data.objects.remove(o, do_unlink=True)
tm = bpy.data.materials.new("PartygoerTeeth"); tm.use_nodes = True
next(n for n in tm.node_tree.nodes if n.type == "BSDF_PRINCIPLED").inputs["Base Color"].default_value = (*lin((0.82, 0.75, 0.55)), 1)
for t in teeth_objs.values():
    t.data.materials.append(tm)
    for p in t.data.polygons: p.use_smooth = True

# ------------------------------------------------ rig
arm = bpy.data.armatures.new("PartygoerRig"); rig = link(bpy.data.objects.new("PartygoerRig", arm)); active(rig)
bpy.ops.object.mode_set(mode="EDIT")
def bone(n, a, b, parent=None):
    e = arm.edit_bones.new(n); e.head = a; e.tail = b
    if parent: e.parent = arm.edit_bones[parent]
bone("Hips", (0, 0.02, 0.95), (0, 0.02, 1.12)); bone("Spine", (0, 0.02, 1.12), (0, 0.0, 1.35), "Hips"); bone("Chest", (0, 0, 1.35), (0, 0.02, 1.7), "Spine")
bone("Neck", (0, 0.02, 1.74), (0, -0.01, 1.88), "Chest"); bone("Head", (0, -0.01, 1.88), (0, -0.03, 2.22), "Neck")
for s, sx in (("L", 1), ("R", -1)):
    bone(f"Thigh.{s}", (0.13 * sx, 0.015, 0.92), (0.155 * sx, -0.005, 0.5), "Hips"); bone(f"Shin.{s}", (0.155 * sx, -0.005, 0.5), (0.165 * sx, 0.01, 0.1), f"Thigh.{s}")
    bone(f"Foot.{s}", (0.165 * sx, 0.01, 0.1), (0.178 * sx, -0.25, 0.03), f"Shin.{s}")
    for i in range(NB): bone(f"Arm{i}.{s}", chains[s][i], chains[s][i + 1], "Chest" if i == 0 else f"Arm{i - 1}.{s}")
    bone(f"Shell.{s}", (0.25 * sx, -0.09, 1.42), (0.25 * sx, -0.09, 1.6), "Chest")
    for k in range(2):
        a0, a1, a2 = claw_bones[(s, k)]
        bone(f"ClawA{k}.{s}", a0, a1, "Chest"); bone(f"ClawB{k}.{s}", a1, a2 + (a2 - a1) * 0.4, f"ClawA{k}.{s}")
bpy.ops.object.mode_set(mode="OBJECT")
# body: automatic weights on its own bones only
keep = ("Hips", "Spine", "Chest", "Neck", "Head", "Thigh", "Shin", "Foot")
for b in arm.bones: b.use_deform = b.name.startswith(keep)
bpy.ops.object.select_all(action="DESELECT"); low["Body"].select_set(True); rig.select_set(True); bpy.context.view_layer.objects.active = rig
bpy.ops.object.parent_set(type="ARMATURE_AUTO")
for b in arm.bones: b.use_deform = True
unweighted = sum(1 for v in low["Body"].data.vertices if not v.groups)
def bind(o, fn):
    for v in o.data.vertices:
        for n, w in fn(v.co):
            g = o.vertex_groups.get(n) or o.vertex_groups.new(name=n); g.add([v.index], w, "REPLACE")
    o.parent = rig; m = o.modifiers.new("Armature", "ARMATURE"); m.object = rig
def arm_w(p):
    s = "L" if p.x > 0 else "R"; pts = chains[s]; best = (9, 0)
    for i in range(NB):
        a, d = pts[i], pts[i + 1] - pts[i]; u = max(0, min(1, (p - a).dot(d) / d.length_squared)); dist = (a + d * u - p).length
        if dist < best[0]: best = (dist, i + u)
    t = best[1] - 0.5; i = int(math.floor(t)); u = t - i
    w = []
    for j, k in ((i, 1 - u), (i + 1, u)): w.append((f"Arm{max(0, min(NB - 1, j))}.{s}", k))
    if w[0][0] == w[1][0]: w = [(w[0][0], 1.0)]
    return w
bind(low["Arms"], arm_w)
for s, t in teeth_objs.items(): bind(t, lambda p, s=s: [(f"Arm{NB - 1}.{s}", 1.0)])
# the two halves are separate islands; the left one overlaps the middle, so split by island, not by x
def islands(o):
    n = len(o.data.vertices); par = list(range(n))
    def f(x):
        while par[x] != x: par[x] = par[par[x]]; x = par[x]
        return x
    for e in o.data.edges: par[f(e.vertices[0])] = f(e.vertices[1])
    return [f(i) for i in range(n)]
isl = islands(low["Shell"]); means = {}
for i, r in enumerate(isl): means.setdefault(r, []).append(low["Shell"].data.vertices[i].co.x)
it = iter(range(10 ** 9))
bind(low["Shell"], lambda p: [("Shell.L" if np.mean(means[isl[next(it)]]) > 0 else "Shell.R", 1.0)])
def claw_w(p):
    s = "L" if p.x > 0 else "R"; best = (9, None)
    for (bs, k), (a0, a1, a2) in claw_bones.items():
        for n, a, b in ((f"ClawA{k}.{bs}", a0, a1), (f"ClawB{k}.{bs}", a1, a2 + (a2 - a1) * 0.4)):
            d = b - a; u = max(0, min(1, (p - a).dot(d) / d.length_squared)); dist = (a + d * u - p).length
            if dist < best[0]: best = (dist, n)
    return [(best[1], 1.0)]
bind(low["Claws"], claw_w)

bpy.ops.wm.save_as_mainfile(filepath=os.path.join(HERE, "Partygoer.blend"))
bpy.ops.object.select_all(action="DESELECT")
for o in [*low.values(), *teeth_objs.values(), rig]: o.select_set(True)
bpy.ops.export_scene.fbx(filepath=os.path.join(OUT, "Partygoer.fbx"), use_selection=True, object_types={"ARMATURE", "MESH"}, apply_unit_scale=True, apply_scale_options="FBX_SCALE_ALL",
                         axis_forward="-Z", axis_up="Y", add_leaf_bones=False, bake_anim=False, mesh_smooth_type="FACE", use_armature_deform_only=False)
print("PARTYGOER", {k: len(v.data.polygons) for k, v in low.items()}, "teeth", sum(len(t.data.polygons) for t in teeth_objs.values()), "unweighted", unweighted)

# ------------------------------------------------ review renders
if SHOTS:
    scene.cycles.samples = 24; scene.render.resolution_x = 620; scene.render.resolution_y = 900; scene.view_settings.view_transform = "Standard"
    w = bpy.data.worlds.new("W"); scene.world = w; w.use_nodes = True; w.node_tree.nodes["Background"].inputs[0].default_value = (0.1, 0.1, 0.1, 1)
    for loc, e in (((2, -3, 3), 260), ((-3, -1.5, 2), 90), ((0, 3, 3), 130)):
        l = bpy.data.lights.new("L", "AREA"); l.energy = e; l.size = 1.2; lo = link(bpy.data.objects.new("L", l)); lo.location = loc; lo.rotation_euler = (Vector((0, 0, 1.2)) - Vector(loc)).to_track_quat("-Z", "Y").to_euler()
    cam = link(bpy.data.objects.new("Cam", bpy.data.cameras.new("Cam"))); scene.camera = cam
    def shot(name, loc, look, lens=50):
        cam.location = loc; cam.data.lens = lens; cam.rotation_euler = (Vector(look) - Vector(loc)).to_track_quat("-Z", "Y").to_euler()
        scene.render.filepath = os.path.join(SHOTS, "pg_" + name + ".png"); bpy.ops.render.render(write_still=True)
    pb = rig.pose.bones
    def claws(scale):
        for b in pb:
            if b.name.startswith("ClawA"): b.scale = (scale,) * 3
    claws(0.03)
    shot("front", (0, -4.6, 1.3), (0, 0, 1.12)); shot("side", (3.6, -2.8, 1.4), (0, 0, 1.12)); shot("face", (0.25, -1.1, 2.05), (0, 0, 2.0)); shot("cup", (1.1, -0.9, 0.35), (0.46, -0.04, 0.4), 60)
    claws(1)
    for s, sx in (("L", 1), ("R", -1)):
        pb[f"Shell.{s}"].rotation_mode = "XYZ"; pb[f"Shell.{s}"].rotation_euler = (0, math.radians(115 * sx), 0)
        for i in range(1, NB):
            pb[f"Arm{i}.{s}"].rotation_mode = "XYZ"; pb[f"Arm{i}.{s}"].rotation_euler = (math.radians(-16), 0, 0)
    shot("open", (1.3, -3.6, 1.6), (0, -0.1, 1.2))
