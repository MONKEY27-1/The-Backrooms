# Builds the Deathmoths (wiki Entity 4) from scratch, plus their hive:
#   MothMale      33 cm wingspan, slim, huge feathered antennae, a needle proboscis
#   MothFemale    43 cm, heavier, hooked mandibles, thick abdomen
#   MothPraetorian ~1 m tall on eight long legs, stunted wings
#   MothEmpress   1.5 m body, ~3 m wings, a long swollen abdomen
#   MothHive      a resin growth: ribbed mounds with bones in them, egg clusters and quilled larvae
# Each moth: Body (head, thorax, abdomen, fur, antennae, legs), WingL / WingR with their origin at the root so the
# game flaps them, and (Praetorian, Empress) Leg0..n with origins at the hip. One painted texture per caste.
# Faces -Y, Z up. Run: blender -b Art/Deathmoths.blend --python Art/build_deathmoth.py
import bpy, bmesh, math, os, random
import numpy as np
from mathutils import Vector, Matrix

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.abspath(os.path.join(HERE, "..", "Assets", "_Game", "Resources", "Monsters"))
TEX = os.path.join(OUT, "MothTex")
os.makedirs(TEX, exist_ok=True)

# ---------------- painting ----------------
def noise2(w, h, cells, rng):
    """Smooth value noise, 0..1, tileable enough for a skin."""
    g = rng.random((cells + 2, cells + 2))
    y, x = np.mgrid[0:h, 0:w]
    u, v = x / w * cells, y / h * cells
    i, j = u.astype(int), v.astype(int)
    fu, fv = u - i, v - j
    fu, fv = fu * fu * (3 - 2 * fu), fv * fv * (3 - 2 * fv)
    return (g[j, i] * (1 - fu) + g[j, i + 1] * fu) * (1 - fv) + (g[j + 1, i] * (1 - fu) + g[j + 1, i + 1] * fu) * fv

def fbm(w, h, cells, rng, octaves=4):
    out, amp, tot = np.zeros((h, w)), 1.0, 0.0
    for o in range(octaves):
        out += noise2(w, h, cells * 2 ** o, rng) * amp; tot += amp; amp *= 0.5
    return out / tot

def lerp(a, b, t): return a + (np.asarray(b) - a) * t[..., None]

def save(name, rgb):
    h, w = rgb.shape[:2]
    img = bpy.data.images.get(name)
    if img: bpy.data.images.remove(img)
    img = bpy.data.images.new(name, w, h, alpha=False)
    px = np.ones((h, w, 4), dtype=np.float32); px[..., :3] = np.clip(rgb, 0, 1)
    img.pixels.foreach_set(px.ravel())
    img.filepath_raw = os.path.join(TEX, name + ".png"); img.file_format = "PNG"; img.save()
    return img

def paint(name, base, dark, pale, accent, skull, seed):
    """Left half: the body, v runs head (1) to tail (0), u round it (0.5 = the back). Right half: a wing pair,
    u root to tip, v hind (0) to fore (1)."""
    rng = np.random.default_rng(seed)
    S = 1024; H = S // 2
    img = np.zeros((S, S, 3))
    # body
    v, u = np.mgrid[0:S, 0:H]; v = v / S; u = u / H
    n, f = fbm(H, S, 6, rng), fbm(H, S, 60, rng, 3)
    b = lerp(np.full((S, H, 3), base, dtype=float), dark, n * 0.6)
    seg = np.where(v < 0.55, 0.5 + 0.5 * np.cos(v / 0.55 * 2 * math.pi * 7), 0)      # abdomen rings
    b = lerp(b, pale, np.clip(seg, 0, 1) ** 4 * 0.55)
    b = lerp(b, dark, np.clip(-np.cos(v / 0.55 * 2 * math.pi * 7), 0, 1) ** 3 * (v < 0.55) * 0.8)
    b *= (0.72 + 0.5 * f)[..., None]
    if skull:   # the death's head, on the back of the thorax
        cx, cy = (u - 0.5) / 0.2, (v - 0.72) / 0.085
        face = np.clip(1.15 - np.sqrt(cx ** 2 + np.where(cy > 0, cy, cy * 0.8) ** 2), 0, 1)
        jaw = np.clip(1 - np.abs(cx) / 0.55, 0, 1) * (np.abs(cy + 0.95) < 0.4)
        m = np.clip(np.maximum(face * 4, jaw * 2), 0, 1) * (0.7 + 0.3 * f)
        b = lerp(b, pale, m * 0.9)
        for ex in (-0.38, 0.38):
            b = lerp(b, [0.02, 0.015, 0.01], np.clip((0.3 - np.sqrt((cx - ex) ** 2 + (cy - 0.05) ** 2)) * 8, 0, 1))
        b = lerp(b, [0.03, 0.02, 0.015], np.clip((0.13 - np.sqrt(cx ** 2 + ((cy + 0.4) / 1.3) ** 2)) * 12, 0, 1))
        teeth = (np.abs(cy + 0.95) < 0.25) * (np.cos(cx * 28) > 0.55) * (np.abs(cx) < 0.45)
        b = lerp(b, [0.05, 0.03, 0.02], teeth * 0.8)
    img[:, :H] = b
    # wings
    v, u = np.mgrid[0:S, 0:H]; v = v / S; u = u / H
    n, f = fbm(H, S, 5, rng), fbm(H, S, 80, rng, 3)
    w = lerp(np.full((S, H, 3), base, dtype=float), dark, np.clip(n * 1.2 - 0.2, 0, 1) * 0.7)
    wob = (fbm(H, S, 9, rng) - 0.5) * 0.12
    for c, width, col, k in ((0.28, 0.035, dark, 0.9), (0.36, 0.018, pale, 0.7), (0.62, 0.05, dark, 0.85), (0.70, 0.02, pale, 0.75), (0.9, 0.06, dark, 0.9)):
        w = lerp(w, col, np.exp(-((u + wob - c) / width) ** 2) * k)                     # bands across the wing
    for hind in (0, 1):                                                                  # veins fanning from each root
        v0 = 0.25 if hind else 0.75
        ang = np.arctan2(v - v0, u + 0.06)
        for a in np.linspace(-0.55, 0.55, 8):
            w = lerp(w, dark, np.exp(-((ang - a) * (u + 0.08) / 0.0045) ** 2) * 0.75)
        ex, ey = 0.52, v0                                                                # an eye-spot on each
        d = np.sqrt(((u - ex) * 1.0) ** 2 + ((v - ey) * 1.5) ** 2)
        w = lerp(w, dark, np.clip((0.115 - d) * 40, 0, 1) * 0.95)
        w = lerp(w, accent, np.clip((0.095 - d) * 40, 0, 1) * 0.95)
        w = lerp(w, [0.02, 0.015, 0.015], np.clip((0.06 - d) * 50, 0, 1))
        hl = np.sqrt((u - ex + 0.02) ** 2 + ((v - ey - 0.015) * 1.5) ** 2)
        w = lerp(w, pale, np.clip((0.016 - hl) * 90, 0, 1))
    w *= (0.68 + 0.55 * f)[..., None]                                                    # scale dust
    img[:, H:] = w
    return save(name, img)

# ---------------- building blocks ----------------
class Mesh:
    """Collects geometry with UVs and one material index, then makes an object."""
    def __init__(s): s.v, s.f, s.uv, s.m = [], [], [], []
    def face(s, pts, uvs, mat=0):
        i = len(s.v); s.v += [tuple(p) for p in pts]; s.f.append(tuple(range(i, i + len(pts)))); s.uv.append(list(uvs)); s.m.append(mat)
    def grid(s, rows, uvrows, mat=0):
        """rows of points (shared verts): a smooth skin."""
        i0 = len(s.v); n = len(rows[0])
        for r in rows: s.v += [tuple(p) for p in r]
        for a in range(len(rows) - 1):
            for b in range(n - 1):
                q = (i0 + a * n + b, i0 + a * n + b + 1, i0 + (a + 1) * n + b + 1, i0 + (a + 1) * n + b)
                s.f.append(q); s.uv.append([uvrows[a][b], uvrows[a][b + 1], uvrows[a + 1][b + 1], uvrows[a + 1][b]]); s.m.append(mat)
    def tube(s, path, radii, sides=8, mat=0, uv=lambda t, a: (0.25, 0.9)):
        rows, uvs = [], []
        up = Vector((0.13, 0.2, 1)).normalized()
        for k, p in enumerate(path):
            t = k / (len(path) - 1)
            d = ((path[min(k + 1, len(path) - 1)]) - (path[max(k - 1, 0)])).normalized()
            x = d.cross(up).normalized(); y = d.cross(x).normalized()
            rows.append([p + (x * math.cos(a) + y * math.sin(a)) * radii[k] for a in [j / sides * math.tau for j in range(sides + 1)]])
            uvs.append([uv(t, j / sides) for j in range(sides + 1)])
        s.grid(rows, uvs, mat)
    def make(s, name, col, mats, origin=Vector((0, 0, 0))):
        me = bpy.data.meshes.new(name)
        me.from_pydata([tuple(Vector(p) - origin) for p in s.v], [], s.f)
        layer = me.uv_layers.new(name="UVMap")
        k = 0
        for poly, uvs in zip(me.polygons, s.uv):
            poly.use_smooth = True
            poly.material_index = s.m[poly.index]
            for j in range(poly.loop_total): layer.data[poly.loop_start + j].uv = uvs[j]
        bm = bmesh.new(); bm.from_mesh(me); bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=1e-6); bm.to_mesh(me); bm.free()
        for m in mats: me.materials.append(m)
        o = bpy.data.objects.new(name, me); col.objects.link(o); o.location = origin
        return o

def bodyuv(u, v): return (u * 0.5, v)          # left half of the texture
def winguv(u, v): return (0.5 + u * 0.5, v)    # right half

def catmull(pts, n):
    """A smooth curve through the points."""
    out, P = [], [Vector(p) for p in pts]
    P = [P[0] * 2 - P[1]] + P + [P[-1] * 2 - P[-2]]
    for i in range(1, len(P) - 2):
        for k in range(n):
            t = k / n; a, b, c, d = P[i - 1], P[i], P[i + 1], P[i + 2]
            out.append(0.5 * ((2 * b) + (c - a) * t + (2 * a - 5 * b + 4 * c - d) * t * t + (3 * b - a - 3 * c + d) * t ** 3))
    out.append(P[-2]); return out

def wing(side, span, chord, root, col, mats, name, rng, ragged=0.0, stunt=1.0):
    """Fore and hind wing: leading and trailing edges as curves, lofted, cambered, with a scalloped, worn rim."""
    m = Mesh()
    def panel(lead, trail, v0, v1, dz):
        L, T = catmull(lead, 7), catmull(trail, 7)
        n = len(L); rows, uvs = [], []
        for i in range(n):
            t = i / (n - 1); row, ur = [], []
            for j in range(11):
                k = j / 10
                p = L[i].lerp(T[i], k)
                rim = max(0.0, t - 0.8) / 0.2
                if j == 10 or i == n - 1:
                    p = p + (T[i] - L[i]).normalized() * (math.sin(t * 46) * 0.012 * span + (rng.random() - 0.5) * ragged * span)
                z = dz + 0.05 * span * math.sin(k * math.pi) * (1 - t * 0.5) - 0.05 * span * t * t + 0.006 * span * math.sin(t * 34 + k * 5)
                row.append(root + Vector((p.x * side * span * stunt, p.y * chord * stunt, z)))
                ur.append(winguv(min(1, t * 0.98 + 0.01), v0 + (v1 - v0) * (1 - k)))
            rows.append(row); uvs.append(ur)
        if side < 0: rows = [r[::-1] for r in rows]; uvs = [r[::-1] for r in uvs]
        m.grid(rows, uvs, 0)
    # (x along the span 0..1, y: - is forward)
    panel([(0.0, -0.1, 0), (0.25, -0.5, 0), (0.6, -0.78, 0), (0.92, -0.8, 0), (1.0, -0.62, 0)],
          [(0.0, 0.12, 0), (0.22, 0.14, 0), (0.48, 0.1, 0), (0.75, -0.02, 0), (0.98, -0.5, 0)], 0.5, 1.0, 0.01 * span)
    panel([(0.0, 0.06, 0), (0.22, 0.02, 0), (0.45, 0.02, 0), (0.62, 0.14, 0), (0.7, 0.38, 0)],
          [(0.0, 0.3, 0), (0.12, 0.6, 0), (0.28, 0.86, 0), (0.5, 0.84, 0), (0.68, 0.46, 0)], 0.0, 0.5, -0.012 * span)
    return m.make(name, col, mats, root)

def leg(m, hip, dirx, diry, reach, drop, r, mat, rng, spines=10):
    """Coxa, femur up to a high knee, tibia down, hooked tarsus; spines along the tibia."""
    knee = hip + Vector((dirx * reach * 0.48, diry * reach * 0.48, drop * 0.22))
    ankle = hip + Vector((dirx * reach * 0.86, diry * reach * 0.86, -drop * 0.72))
    toe = hip + Vector((dirx * reach, diry * reach, -drop))
    path = [hip, hip.lerp(knee, 0.5) + Vector((0, 0, r)), knee, knee.lerp(ankle, 0.5), ankle, toe]
    m.tube(path, [r * 1.25, r * 1.5, r * 0.85, r * 1.0, r * 0.6, r * 0.2], 7, mat, lambda t, a: bodyuv(0.15 + a * 0.1, 0.9 - t * 0.05))
    for i in range(spines):
        t = (i + 0.5) / spines; p = knee.lerp(ankle, t)
        d = Vector((rng.uniform(-1, 1), rng.uniform(-1, 1), rng.uniform(-0.8, 0.1))).normalized()
        m.tube([p, p + d * r * 2.6], [r * 0.28, 0.0], 4, mat, lambda t, a: bodyuv(0.2, 0.9))
    for c in (-1, 1):
        m.tube([toe, toe + Vector((dirx * r * 2 + diry * c * r, diry * r * 2 - dirx * c * r, -r * 0.4))], [r * 0.3, 0.0], 4, mat, lambda t, a: bodyuv(0.2, 0.9))

def make_mats(name, img):
    out = []
    for part, rough in (("Skin", 0.75), ("Eye", 0.08), ("Horn", 0.3)):
        m = bpy.data.materials.get(name + part) or bpy.data.materials.new(name + part)
        m.use_nodes = True; nt = m.node_tree
        p = next(n for n in nt.nodes if n.type == "BSDF_PRINCIPLED"); p.inputs["Roughness"].default_value = rough
        if part == "Skin":
            t = next((n for n in nt.nodes if n.type == "TEX_IMAGE"), None) or nt.nodes.new("ShaderNodeTexImage")
            t.image = img; nt.links.new(t.outputs["Color"], p.inputs["Base Color"])
        else: p.inputs["Base Color"].default_value = (0.02, 0.012, 0.01, 1) if part == "Eye" else (0.06, 0.035, 0.02, 1)
        out.append(m)
    return out

def moth(name, img, L, fat, span, chord, seed, antenna, female=False, legs=6, legreach=0.8, legdrop=0.45, tail=1.0, fur=260, stunt=1.0, separate_legs=False, ragged=0.01):
    """L = head-to-tail body length. The thorax sits at the origin."""
    col = bpy.data.collections.get(name)
    if col:
        for o in list(col.objects): bpy.data.objects.remove(o, do_unlink=True)
    else:
        col = bpy.data.collections.new(name); bpy.context.scene.collection.children.link(col)
    rng = random.Random(seed); mats = make_mats(name, img); m = Mesh()
    th = L * 0.16 * fat                                     # thorax radius
    # body profile from head to tail: (y, z, rx, rz, texture v)
    prof = [(-0.42, 0.0, 0.02, 0.02, 1.0), (-0.40, 0.0, 0.5, 0.48, 0.97), (-0.34, 0.01, 0.66, 0.62, 0.93), (-0.28, 0.02, 0.55, 0.52, 0.88),
            (-0.22, 0.04, 0.86, 0.82, 0.84), (-0.12, 0.06, 1.05, 1.02, 0.77), (0.0, 0.05, 1.0, 0.95, 0.7), (0.08, 0.03, 0.72, 0.7, 0.62), (0.11, 0.02, 0.6, 0.58, 0.57)]
    rows, uvs, sides = [], [], 24
    pts = [(y * L, z * th, rx * th, rz * th, tv) for y, z, rx, rz, tv in prof]
    ab0, abL = 0.11 * L, 0.5 * L * tail                      # abdomen: seven swollen rings, tapering, drooping
    for i in range(1, 50):
        t = i / 49
        ring = 1 + 0.07 * math.cos(t * 7 * math.tau + math.pi)
        r = (0.62 + 0.42 * math.sin(min(1, t * 1.5) * math.pi * 0.62)) * (1 - t ** 3 * 0.96) * ring * (1.18 if female else 0.86)
        pts.append((ab0 + abL * t, 0.02 * th - 0.75 * th * t * t * (1.2 if tail > 1 else 0.6), r * th, r * th * 0.94, 0.55 * (1 - t)))
    for y, z, rx, rz, tv in pts:
        rows.append([Vector((math.sin(a) * rx, y, z - math.cos(a) * rz)) for a in [j / sides * math.tau for j in range(sides + 1)]])
        uvs.append([bodyuv(j / sides, tv) for j in range(sides + 1)])
    m.grid(rows, uvs, 0)
    # fur: tapered tufts laid back over head and thorax, thinner round the abdomen rings
    for i in range(fur):
        a = rng.uniform(0, math.tau); thor = rng.random() < 0.72
        k = rng.uniform(2, 7) if thor else rng.uniform(9, len(pts) - 8)
        y, z, rx, rz, tv = pts[int(k)]
        if 3.5 < k < 7.2 and abs(a - math.pi) < 1.0: continue      # leave the death's head on the back bare
        n = Vector((math.sin(a), 0, -math.cos(a)))
        p = Vector((n.x * rx * 0.96, y, z + n.z * rz * 0.96))
        ln = th * (rng.uniform(0.3, 0.62) if thor else rng.uniform(0.12, 0.26)); w = th * (0.085 if thor else 0.06)
        tip = p + (n * 0.65 + Vector((rng.uniform(-.25, .25), rng.uniform(0.5, 1.1), rng.uniform(-.2, .2)))).normalized() * ln
        s_ = n.cross(Vector((0, 1, 0))).normalized() * w
        uv0 = bodyuv(a / math.tau % 1, tv)
        mid = p.lerp(tip, 0.5) + n * ln * 0.12
        m.face([p - s_, p + s_, mid + s_ * 0.6, mid - s_ * 0.6], [uv0] * 4); m.face([mid - s_ * 0.6, mid + s_ * 0.6, tip], [uv0] * 3)
    head = Vector((0, -0.36 * L, 0.01 * th))
    def ball(c, r, sc, mat, seg=14):
        R, U = [], []
        for i in range(seg // 2 + 1):
            ph = -math.pi / 2 + math.pi * i / (seg // 2)
            R.append([c + Vector((math.cos(ph) * math.cos(a) * r * sc[0], math.cos(ph) * math.sin(a) * r * sc[1], math.sin(ph) * r * sc[2])) for a in [j / seg * math.tau for j in range(seg + 1)]])
            U.append([bodyuv(0.2, 0.9)] * (seg + 1))
        m.grid(R, U, mat)
    for sx in (1, -1):
        ball(head + Vector((sx * th * 0.52, -0.02 * L, th * 0.02)), th * 0.34, (0.8, 1, 1.05), 1)              # compound eyes
        ball(head + Vector((sx * th * 0.16, -0.015 * L, th * 0.55)), th * 0.07, (1, 1, 1), 1, 8)               # ocelli
        # antenna: a curved stem with two rows of barbs
        base = head + Vector((sx * th * 0.25, -0.03 * L, th * 0.42))
        stem = catmull([base, base + Vector((sx * antenna * 0.35, -antenna * 0.42, antenna * 0.3)), base + Vector((sx * antenna * 0.72, -antenna * 0.62, antenna * 0.26)), base + Vector((sx * antenna * 0.95, -antenna * 0.55, antenna * 0.08))], 8)
        m.tube(stem, [th * 0.055 * (1 - 0.7 * i / len(stem)) for i in range(len(stem))], 5, 2, lambda t, a: bodyuv(0.2, 0.9))
        for i in range(2, len(stem) - 1):
            t = i / len(stem); d = (stem[i + 1] - stem[i]).normalized()
            ln = antenna * (0.34 if not female else 0.16) * math.sin(min(1, t * 1.15) * math.pi) ** 0.7
            for s2 in (1, -1):
                o_ = (d.cross(Vector((0, 0, 1))).normalized() * s2 + d * 0.5).normalized() * ln
                w = d * antenna * 0.018
                m.face([stem[i] - w, stem[i] + w, stem[i] + o_ + Vector((0, 0, -ln * 0.15))], [bodyuv(0.25, 0.8)] * 3, 0)
        if female:   # hooked mandibles
            j = catmull([head + Vector((sx * th * 0.2, -0.05 * L, -th * 0.3)), head + Vector((sx * th * 0.55, -0.05 * L - th * 0.55, -th * 0.45)), head + Vector((sx * th * 0.08, -0.05 * L - th * 1.0, -th * 0.5))], 6)
            m.tube(j, [th * 0.16 * (1 - i / (len(j) - 1) * 0.97) for i in range(len(j))], 8, 2, lambda t, a: bodyuv(0.2, 0.9))
    if not female:   # proboscis, curled then straight down
        j = catmull([head + Vector((0, -0.05 * L, -th * 0.2)), head + Vector((0, -0.05 * L - th * 0.6, -th * 0.35)), head + Vector((0, -0.05 * L - th * 0.5, -th * 1.1)), head + Vector((0, -0.05 * L - th * 0.2, -th * 1.6))], 6)
        m.tube(j, [th * 0.09 * (1 - i / (len(j) - 1) * 0.95) for i in range(len(j))], 6, 2, lambda t, a: bodyuv(0.2, 0.9))
    # legs
    made = []
    for i in range(legs):
        sx = 1 if i % 2 == 0 else -1; row = i // 2; rows_n = legs // 2
        f = row / max(1, rows_n - 1)
        hip = Vector((sx * th * 0.55, (-0.2 + 0.24 * f) * L, -th * 0.55))
        ang = math.radians(-48 + 96 * f)
        dx, dy = sx * math.cos(ang), math.sin(ang)
        lm = Mesh() if separate_legs else m
        leg(lm, hip, dx, dy, legreach * L, legdrop * L, th * 0.12 if separate_legs else th * 0.1, 2, rng, 16 if separate_legs else 5)
        if separate_legs: made.append((lm, hip))
    objs = [m.make(name + "Body", col, mats)]
    for i, (lm, hip) in enumerate(made): objs.append(lm.make(f"{name}Leg{i}", col, mats, hip))
    for s_, nm in ((1, "WingL"), (-1, "WingR")):
        objs.append(wing(s_, span / 2 - th * 0.5, chord, Vector((s_ * th * 0.55, -0.1 * L, th * 0.62)), col, mats, name + nm, rng, ragged, stunt))
    export(objs, name)
    print("MOTH", name, sum(len(o.data.polygons) for o in objs), "faces")
    return col

def export(objs, name):
    bpy.ops.object.select_all(action="DESELECT")
    for o in objs: o.select_set(True)
    bpy.ops.export_scene.fbx(filepath=os.path.join(OUT, name + ".fbx"), use_selection=True, object_types={"MESH"}, apply_unit_scale=True,
                             apply_scale_options="FBX_SCALE_ALL", axis_forward="-Z", axis_up="Y", bake_space_transform=False, bake_anim=False, mesh_smooth_type="FACE")

# ---------------- the four castes (base, dark, pale, accent in sRGB) ----------------
bpy.context.scene.unit_settings.scale_length = 1.0
male = paint("MothMale", [0.5, 0.42, 0.33], [0.17, 0.12, 0.09], [0.82, 0.75, 0.6], [0.62, 0.4, 0.2], True, 41)
fem = paint("MothFemale", [0.42, 0.25, 0.17], [0.1, 0.05, 0.04], [0.85, 0.66, 0.42], [0.7, 0.16, 0.08], True, 42)
pra = paint("MothPraetorian", [0.17, 0.14, 0.13], [0.04, 0.03, 0.03], [0.76, 0.7, 0.58], [0.55, 0.07, 0.05], True, 43)
emp = paint("MothEmpress", [0.74, 0.67, 0.55], [0.28, 0.18, 0.13], [0.95, 0.91, 0.8], [0.66, 0.2, 0.1], True, 44)
moth("MothMale", male, 0.10, 0.95, 0.33, 0.17, 1, 0.05, False, 6, 0.55, 0.42, fur=170)
moth("MothFemale", fem, 0.13, 1.2, 0.43, 0.21, 2, 0.04, True, 6, 0.55, 0.42, fur=220)
moth("MothPraetorian", pra, 0.62, 0.95, 0.9, 0.34, 3, 0.2, True, 8, 1.15, 0.95, tail=0.95, fur=420, stunt=0.85, separate_legs=True, ragged=0.05)
moth("MothEmpress", emp, 1.15, 1.0, 3.0, 1.35, 4, 0.5, True, 6, 0.72, 0.5, tail=1.75, fur=700, separate_legs=True, ragged=0.02)

# ---------------- the hive ----------------
def hive(name, W, HH, D, blobs, ropes, hollows, bones, larvae, voxel, faces, columns):
    """W half-width, HH height, D how far it bulges out (-Y) from the wall (+Y)."""
    rng = np.random.default_rng(45); S = 1024
    v, u = np.mgrid[0:S, 0:S] / S
    n, f, big = fbm(S, S, 8, rng), fbm(S, S, 70, rng, 3), fbm(S, S, 3, rng)
    c = lerp(np.full((S, S, 3), [0.36, 0.24, 0.13], dtype=float), [0.12, 0.07, 0.04], n)
    c = lerp(c, [0.62, 0.45, 0.22], np.clip(np.sin((v + big * 0.25) * 60) - 0.45, 0, 1) * 0.8)     # drawn-out resin strands
    c = lerp(c, [0.45, 0.16, 0.12], np.clip(big * 1.6 - 0.8, 0, 1) * 0.6)                         # digested matter showing through
    img = save(name, c * (0.7 + 0.55 * f)[..., None])
    col = bpy.data.collections.get(name)
    if col:
        for o in list(col.objects): bpy.data.objects.remove(o, do_unlink=True)
    else:
        col = bpy.data.collections.new(name); bpy.context.scene.collection.children.link(col)
    mats = make_mats(name, img)
    for nm, colr, rough in (("Egg", (0.8, 0.72, 0.5, 1), 0.25), ("Bone", (0.7, 0.64, 0.5, 1), 0.6)):
        m = bpy.data.materials.get(name + nm) or bpy.data.materials.new(name + nm); m.use_nodes = True
        p = next(x for x in m.node_tree.nodes if x.type == "BSDF_PRINCIPLED"); p.inputs["Base Color"].default_value = colr; p.inputs["Roughness"].default_value = rough
        mats.append(m)
    rn = random.Random(6); m = Mesh()
    # the resin mass: overlapping blobs and ropes, fused by a voxel remesh so it reads as one secretion. Backs onto +Y.
    rn = random.Random(6)
    parts = []
    def blob(c, r, sc):
        bpy.ops.mesh.primitive_uv_sphere_add(segments=20, ring_count=12, radius=r, location=c)
        o_ = bpy.context.object; o_.scale = sc; parts.append(o_)
    def rope(path, r0, r1):
        t_ = Mesh(); n_ = len(path)
        t_.tube(path, [0.001] + [r0 + (r1 - r0) * k / (n_ - 1) + 0.012 * math.sin(k * 2.1) for k in range(1, n_ - 1)] + [0.001], 8)
        me_ = bpy.data.meshes.new("rope"); me_.from_pydata(t_.v, [], t_.f); o_ = bpy.data.objects.new("rope", me_); bpy.context.scene.collection.objects.link(o_); parts.append(o_)
    sc = W / 1.05
    for k in range(blobs):   # heaped cells, biggest at the floor, thinning as it climbs; deepest in the middle
        t_ = rn.random() ** 1.5; z = HH * t_; w_ = W * (1 - t_ * (0.8 if columns == 0 else 0.25)); x = rn.uniform(-w_, w_)
        deep = D * (1.25 - 0.95 * t_) * (1 - (abs(x) / W) ** 2 * 0.55)
        blob((x, -rn.random() * deep * 0.8, z), rn.uniform(0.14, 0.34) * (1.15 - t_ * 0.6) * min(1.7, sc ** 0.6), (rn.uniform(0.8, 1.25), rn.uniform(0.6, 0.95), rn.uniform(0.8, 1.5)))
    for k in range(ropes):   # ropes running up the wall, out over the floor, and slung between
        x = rn.uniform(-0.87, 0.87) * W; z = rn.uniform(0.12, 0.75) * HH
        if k % 13 < 7: end = Vector((max(-W, min(W, x * 1.2 + rn.uniform(-0.25, 0.25) * sc)), -0.02 - (rn.random() * D if columns else 0), rn.uniform(0.86, 1.0) * HH))
        elif k % 13 < 10: end = Vector((x + rn.uniform(-0.4, 0.4), -D * rn.uniform(1.9, 2.9) if columns == 0 else -D * rn.uniform(1.15, 1.5), 0.02))
        else: end = Vector((-x * 0.6, -D * rn.uniform(0.7, 1.1), z + rn.uniform(-0.3, 0.4)))
        st = Vector((x, -D * 0.75 * (1 - (abs(x) / W) ** 2 * 0.5), z)); mid = st.lerp(end, 0.5) + Vector((rn.uniform(-.12, .12), -0.16, -0.1 * sc))
        rope(catmull([st, mid, end], 10), 0.07 * min(1.6, sc ** 0.5), 0.035)
    pillars = []
    for k in range(columns): # columns of resin and remains, floor to ceiling, out in front
        x = (k + 0.5) / columns * 2 * W - W + rn.uniform(-0.25, 0.25); y = -D * rn.uniform(0.75, 1.1); pillars.append((x, y))
        for i_ in range(16):
            t_ = i_ / 15; rr = 0.2 + 0.24 * abs(t_ - 0.5) ** 1.3 * 2 + rn.uniform(-0.03, 0.05)
            blob((x + rn.uniform(-.07, .07), y + rn.uniform(-.07, .07), HH * t_), rr, (1, 1, 1.1))
    bpy.ops.object.select_all(action="DESELECT")
    for o_ in parts: o_.select_set(True)
    bpy.context.view_layer.objects.active = parts[0]; bpy.ops.object.join()
    resin = bpy.context.object
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    resin.data.remesh_voxel_size = voxel; bpy.ops.object.voxel_remesh()
    bm = bmesh.new(); bm.from_mesh(resin.data)
    for (hx, hz, hr) in hollows:      # open brood hollows, lips pushed out
        for v_ in bm.verts:
            d_ = math.hypot(v_.co.x - hx, v_.co.z - hz)
            if v_.co.y < -0.02 and not any(math.hypot(v_.co.x - px, v_.co.y - py) < 0.5 for px, py in pillars):
                if d_ < hr: v_.co.y += 0.22 * (1 - (d_ / hr) ** 2)
                elif d_ < hr * 1.4: v_.co.y -= 0.05 * (1 - abs(d_ - hr * 1.2) / (hr * 0.2)) if abs(d_ - hr * 1.2) < hr * 0.2 else 0
    for v_ in bm.verts: v_.co.y = min(v_.co.y, 0.04); v_.co.z = min(HH, max(v_.co.z, 0.0)); v_.co.x = max(-W * 1.06, min(W * 1.06, v_.co.x))
    for k in range(3): bmesh.ops.smooth_vert(bm, verts=bm.verts, factor=0.4, use_axis_x=True, use_axis_y=True, use_axis_z=True)
    bm.to_mesh(resin.data); bm.free()
    dm = resin.modifiers.new("D", "DECIMATE"); dm.ratio = min(1.0, faces / max(1, len(resin.data.polygons)) / 2); bpy.context.view_layer.objects.active = resin; bpy.ops.object.modifier_apply(modifier="D")
    from mathutils.bvhtree import BVHTree
    tree = BVHTree.FromObject(resin, bpy.context.evaluated_depsgraph_get())
    def on(x, z, sink=0.0):
        hit, n_, _, _ = tree.ray_cast(Vector((x, -3, z)), Vector((0, 1, 0)))
        return (hit - n_ * sink, n_) if hit is not None else (Vector((x, -0.05, z)), Vector((0, -1, 0)))
    m = Mesh()
    for poly in resin.data.polygons:
        pts_ = [resin.data.vertices[k].co for k in poly.vertices]
        m.face(pts_, [(q.x * 0.4 + 0.5 + q.y * 0.15, q.z * 0.4) for q in pts_], 0)
    bpy.data.objects.remove(resin, do_unlink=True)
    for k in range(bones):   # bones, half swallowed
        p, n_ = on(rn.uniform(-0.8, 0.8) * W, rn.uniform(0.08, 1.0) * (1 if columns == 0 else rn.uniform(1, 2.3)), 0.012); d = n_.cross(Vector((rn.uniform(-1, 1), rn.uniform(-1, 1), rn.uniform(-1, 1)))).normalized() * rn.uniform(0.16, 0.34)
        d += n_ * rn.uniform(-0.03, 0.14); q = p + d; r_ = rn.uniform(0.013, 0.02)
        m.tube([p - d * 0.1, p, p.lerp(q, 0.5), q, q + d * 0.07], [r_ * 1.2, r_ * 1.7, r_, r_ * 1.8, r_ * 0.8], 8, 4, lambda t, k_: (k_, t))
    def ball(c, r, sc, mat, seg=10):
        R = []
        for i_ in range(seg // 2 + 1):
            ph = -math.pi / 2 + math.pi * i_ / (seg // 2)
            R.append([c + Vector((math.cos(ph) * math.cos(a_) * r * sc[0], math.cos(ph) * math.sin(a_) * r * sc[1], math.sin(ph) * r * sc[2])) for a_ in [j_ / seg * math.tau for j_ in range(seg + 1)]])
        m.grid(R, [[(0.5, 0.5)] * (seg + 1) for _ in R], mat)
    for hx, hz, hr in hollows:
        for k in range(int(650 * hr * hr)):
            a_, r_ = rn.uniform(0, math.tau), hr * 0.8 * math.sqrt(rn.random())
            p, n_ = on(hx + math.cos(a_) * r_, hz + math.sin(a_) * r_, 0.008); ball(p, rn.uniform(0.02, 0.036), (1, 1, 1.25), 3)
    for k in range(larvae):  # quilled larvae crawling on it
        p, n_ = on(rn.uniform(-0.8, 0.8) * W, rn.uniform(0.07, 0.7) * HH, -0.016); d = n_.cross(Vector((rn.uniform(-1, 1), rn.uniform(-1, 1), rn.uniform(-1, 1)))).normalized()
        path = [p + d * (c_ / 8 - 0.5) * 0.15 + n_ * 0.015 * math.sin(c_ / 8 * math.pi) for c_ in range(9)]
        m.tube(path, [0.02 * (0.45 + 0.55 * math.sin(c_ / 8 * math.pi)) * (1 + 0.18 * (c_ % 2)) for c_ in range(9)], 9, 3, lambda t, k_: (k_, t))
        for c_ in range(24):
            b_ = path[rn.randrange(1, 8)]; q = (n_ + Vector((rn.uniform(-1, 1), rn.uniform(-1, 1), rn.uniform(-1, 1))) * 0.8).normalized()
            m.tube([b_, b_ + q * 0.065], [0.0026, 0.0], 3, 2, lambda t, k_: (0, 0))
    o = m.make(name + "Mesh", col, mats)
    export([o], name)
    print("MOTH", name, len(o.data.polygons), "faces")
hive("MothHiveSmall", 1.05, 2.35, 0.36, 70, 26, ((-0.2, 1.0, 0.24), (0.42, 0.55, 0.2), (-0.6, 0.42, 0.17), (0.25, 1.5, 0.15)), 15, 9, 0.022, 7000, 0)
hive("MothHive", 2.6, 2.7, 1.5, 230, 78, ((-0.2, 1.25, 0.42), (1.1, 0.8, 0.36), (-1.35, 0.95, 0.38), (0.6, 1.9, 0.3), (-0.85, 1.95, 0.26), (1.85, 1.6, 0.3), (-2.0, 0.5, 0.26), (0.35, 0.45, 0.24), (2.1, 0.55, 0.24)), 60, 30, 0.036, 18000, 3)
print("DEATHMOTHS_OK")
