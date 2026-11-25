# The gore kit (GAME_PLAN §24, Stage C): torn stumps, guts, ribs, bones, meat, a flayed hide, a cocoon. Adults only.
# One FBX, every piece its own mesh with baked colour and normal maps. z up, "out of the body" is +z for stumps and -y for
# things worn on the front.
# Run: blender -b --factory-startup --python Art/build_gorekit.py   (saves Art/GoreKit.blend; exports GoreKit.fbx and GoreTex/)
import os
HERE = os.path.dirname(os.path.abspath(__file__))
exec(open(os.path.join(HERE, "build_wretch.py"), encoding="utf-8").read().split("# ================================================================ the skeletal one")[0])   # helpers: loft, curve, ball, fuse, paint, bake, spikes
TEX = os.path.join(OUT, "GoreTex"); os.makedirs(TEX, exist_ok=True)
SHOTS = os.environ.get("GORE_SHOTS", "")
random.seed(1313)
R = random.uniform
MEAT, DEEP, FAT, BONE_, GUT, HIDE = (0.46, 0.055, 0.05), (0.17, 0.012, 0.016), (0.8, 0.62, 0.36), (0.84, 0.76, 0.6), (0.68, 0.33, 0.29), (0.78, 0.6, 0.5)
V = Vector
low = {}

def tube(pts, r, n=None, r2=None):
    """A smooth tube through points; r tapers to r2."""
    k = len(pts) - 1
    return loft("T", curve([(V(p), r + ((r2 if r2 is not None else r) - r) * i / k, r + ((r2 if r2 is not None else r) - r) * i / k, 2) for i, p in enumerate(pts)], n or max(6, k * 7)), 16)

def finish(o, name, tris, size, bone=lambda p: 0.0, gut=lambda p: 0.0, hide=lambda p, n: 0.0, amp=0.004, grain=40, axis=2, rough=0.3, cage=0.006, dirty=0.0):
    """Wet torn flesh: muscle fibres along an axis, clots, fat and veins; bone, gut and skin where asked."""
    me = o.data
    base = [v.co.copy() for v in me.vertices]; nrm = [v.normal.copy() for v in me.vertices]
    def fibre(p):
        q = V((p.x, p.y, p.z)); q[axis] *= 0.16
        return noise.noise(q * grain * 2.2 + V((3, 1, 7)))
    B = [bone(p) for p in base]; G = [gut(p) for p in base]; H = [hide(p, n) for p, n in zip(base, nrm)]
    for v, p, n, b, g, h in zip(me.vertices, base, nrm, B, G, H):
        soft_ = 1 - max(b * 0.85, g * 0.7, h * 0.7)
        v.co = p + n * (amp * soft_ * (0.7 * fibre(p) + 0.6 * noise.noise(p * grain) + 0.35 * noise.noise(p * grain * 3)) + 0.0012 * g * noise.noise(p * 90) + 0.0007 * b * noise.noise(p * 140))
    me.update()
    def c(i, _, n):
        p = base[i]
        f = fibre(p)
        col_ = mix(MEAT, DEEP, soft(f + 0.6 * noise.noise(p * grain * 0.6), -0.25, 0.55))
        col_ = mix(col_, FAT, 0.8 * soft(noise.noise(p * grain * 1.1 + V((11, 0, 4))) + 0.3 * noise.noise(p * grain * 3), 0.36, 0.52))
        col_ = mix(col_, (0.62, 0.12, 0.1), 0.5 * soft(noise.noise(p * grain * 4), 0.2, 0.5))
        if G[i]:
            g = mix(GUT, (0.5, 0.16, 0.17), soft(noise.noise(p * 22), -0.1, 0.45))
            vein = max(0.0, 1 - abs(noise.noise(p * 34 + V((5, 5, 5)))) * 16)
            col_ = mix(col_, mix(g, (0.33, 0.04, 0.09), 0.75 * vein), G[i])
        if B[i]: col_ = mix(col_, mix(BONE_, (0.5, 0.16, 0.1), soft(noise.noise(p * 30), 0.0, 0.5)), B[i])
        if H[i]: col_ = mix(col_, mix(HIDE, (0.5, 0.28, 0.24), soft(noise.noise(p * 12) + 0.5 * noise.noise(p * 50), 0.0, 0.5)), H[i])
        col_ = mix(col_, (0.2, 0.004, 0.008), 0.9 * soft(noise.noise(p * grain * 0.45 + V((2, 9, 4))) + 0.25 * noise.noise(p * grain * 2), 0.12, 0.35))   # blood over everything
        return mix(col_, (0.1, 0.07, 0.04), dirty * soft(noise.noise(p * 15), -0.1, 0.4)) if dirty else col_
    paint(o, c)
    low[name] = bake(o, name, tris, size, rough, cage)

def flaps(n, r, length, w, z=0.0, out=0.5):
    """Torn strips round a rim."""
    ps = []
    for i in range(n):
        a = 2 * math.pi * i / n + R(-0.15, 0.15); d = V((math.cos(a), math.sin(a), 0)); L = length * R(0.4, 1.25); t = d.cross(V((0, 0, 1))) * R(-0.4, 0.4)
        p0 = d * r * 0.8 + V((0, 0, z)); p1 = d * r * 1.02 + V((0, 0, z + L * 0.45)); p2 = p1 + (d * out + t) * L * 0.5 + V((0, 0, L * R(0.1, 0.55)))
        ww = w * R(0.6, 1.5)
        ps.append(loft("F", curve([(p0, ww * 1.5, w * 0.55, 2), (p1, ww, w * 0.35, 2), (p2, ww * 0.35, w * 0.2, 2)], 12), 10))
    return ps

# ---------------------------------------------------------------- stumps: the torn end of a limb (radius 0.06, +z out)
def stump(name, bones, tris):
    ps = [ball("B", (0, 0, -0.006), 0.058, (1, 1, 0.42))] + flaps(17, 0.056, 0.05, 0.011)
    for i in range(16):   # ends of muscle and vessels
        a, d = R(0, 6.3), R(0.012, 0.044); p = V((math.cos(a) * d, math.sin(a) * d, 0)); r = R(0.004, 0.011)
        ps.append(tube([p, p + V((R(-.006, .006), R(-.006, .006), R(0.018, 0.034)))], r, 6, r * 0.8))
    segs = []
    for (x, y, r, h) in bones:
        top = V((x + R(-.004, .004), y, h)); ps.append(tube([(x, y, -0.01), top], r, 10, r * 0.95)); segs.append((x, y, r))
        for k in range(4): ps.append(tube([top + V((R(-r, r) * 0.7, R(-r, r) * 0.7, -0.01)), top + V((R(-r, r), R(-r, r), R(0.006, 0.016)))], r * 0.45, 5, 0.001))   # splintered
    o = fuse(ps, "H", 0.0021, 2)
    finish(o, name, tris, 512, bone=lambda p: max([0.0] + [soft(r * 1.25 - math.hypot(p.x - x, p.y - y), 0, 0.002) for x, y, r in segs]) * soft(p.z, 0.02, 0.026), grain=70, amp=0.003,
           hide=lambda p, n: soft(math.hypot(p.x, p.y), 0.056, 0.061) * soft(0.03 - p.z, 0, 0.01) * 0.8)
stump("GoreStump", [(0.012, 0, 0.0125, 0.05), (-0.018, 0.004, 0.009, 0.04)], 1800)

# ---------------------------------------------------------------- a torn spine hanging from a torn waist (hangs along +z)
ps = [ball("B", (0, 0, 0), 0.06, (2.0, 1.45, 0.5))]
for p in flaps(22, 0.06, 0.09, 0.017): p.scale = (2.0, 1.45, 1); active(p); bpy.ops.object.transform_apply(scale=True, location=False, rotation=False); ps.append(p)
cen = []
for i in range(7):
    c = V((0.012 * math.sin(i * 0.8), 0.035 + 0.01 * i, 0.01 + 0.036 * i)); cen.append(c)
    ps += [ball("Vt", c, 0.021 - i * 0.0008, (1.1, 1, 0.72)), tube([c, c + V((0, 0.04, -0.006))], 0.008, 6, 0.003), tube([c - V((0.035, -0.01, 0)), c + V((0.035, 0.01, 0))], 0.006, 8)]
ps.append(tube([c for c in cen], 0.012))
for i in range(12):
    c = random.choice(cen); ps.append(tube([c, c + V((R(-.05, .05), R(-.04, .04), R(0, .03))), c + V((R(-.06, .06), R(-.05, .05), R(0.02, .09)))], R(0.003, 0.007), 10, 0.0015))
o = fuse(ps, "H", 0.003, 2)
finish(o, "GoreSpine", 3600, 512, bone=lambda p: max([0.0] + [soft(0.029 - (p - c).length, 0, 0.004) for c in cen]) * soft(p.z, 0.03, 0.045) * 0.9, grain=45)

# ---------------------------------------------------------------- an opened belly (worn on the front, faces -y)
def coils(n, rx, rz, y, r):
    out = []
    for _ in range(n):
        p = V((R(-rx, rx) * 0.7, y + R(-0.012, 0.006), R(-rz, rz) * 0.7)); pts = [p]; h = R(0, 6.3)
        for k in range(8):
            h += R(-1.3, 1.3); q = pts[-1] + V((math.cos(h) * 0.036, R(-0.012, 0.012), math.sin(h) * 0.036))
            q.x = max(-rx, min(rx, q.x)); q.z = max(-rz, min(rz, q.z)); q.y = max(y - 0.03, min(y + 0.01, q.y)); pts.append(q)
        out.append(tube(pts, r * R(0.85, 1.15)))
    return out
ps = [ball("B", (0, 0.015, 0), 0.1, (1.05, 0.3, 1.45))]
def torn(rim, n, wide, thick, long_):
    """Broad overlapping rags of skin and fat peeled back from a wound that faces -y."""
    out_ = []
    for i in range(n):
        a = 2 * math.pi * (i + R(-0.45, 0.45)) / n; d = rim(a); out = V((d.x, 0, d.z)) - V((0, 0, rim(0).z * 0 )); out = out.normalized(); t = V((-out.z, 0, out.x)); L = long_ * R(0.25, 1.0) ** 1.5 + 0.006; w = wide * R(0.5, 1.35)
        for k in (-1, 0, 1):   # three fused tongues of different reach make one ragged edge
            q = d + t * (w * 0.55 * k); l = L * R(0.5, 1.15)
            out_.append(loft("F", curve([(q - out * 0.012 + V((0, 0.014, 0)), w * 0.5, thick * 1.6, 2.4), (q + out * l * 0.35 + V((0, -thick * 1.2, 0)), w * 0.46, thick, 2.4), (q + out * l + t * R(-.006, .006) + V((0, -thick * R(0.2, 1.6), 0)), w * R(0.2, 0.36), thick * 0.5, 2)], 12), 12))
    return out_
ps += torn(lambda a: V((math.cos(a) * 0.1, 0, math.sin(a) * 0.145)), 22, 0.026, 0.005, 0.034)
G_ = coils(6, 0.07, 0.1, -0.028, 0.0155)
G_.append(tube([(0.02, -0.04, -0.09), (0.03, -0.075, -0.15), (0.0, -0.07, -0.25), (-0.035, -0.06, -0.3), (-0.02, -0.055, -0.22), (0.015, -0.05, -0.12)], 0.0145, 50))   # a loop hanging out
ribs_ = []
for i in range(4):
    for sx in (1, -1):
        z = 0.14 - 0.028 * i; a, b = V((0.1 * sx, 0.0, z + 0.015)), V((0.028 * sx * (1 + i * 0.35), -0.035, z - 0.02)); ribs_.append((a, b)); ps.append(tube([a, (a + b) / 2 + V((0, -0.012, 0)), b], 0.0075, 14, 0.006))
pathsG = [[v.co.copy() for v in g.data.vertices] for g in G_]
from mathutils.kdtree import KDTree
def near(sets, reach):
    n = sum(len(s) for s in sets); k = KDTree(n); i = 0
    for s in sets:
        for p in s: k.insert(p, i); i += 1
    k.balance(); return lambda p: soft(reach * 1.6 - k.find(p)[2], 0, reach * 0.6)
isg = near(pathsG, 0.005)
def segd(p, a, b): d = b - a; u = max(0, min(1, (p - a).dot(d) / d.length_squared)); return (a + d * u - p).length
o = fuse(ps + G_, "H", 0.0034, 2)
finish(o, "GoreBelly", 5200, 1024, gut=lambda p: isg(p) * soft(-p.y, 0.005, 0.02), bone=lambda p: max(soft(0.016 - segd(p, a, b), 0, 0.005) for a, b in ribs_) * soft(p.z, 0.02, 0.04) * 0.8, grain=38,
       hide=lambda p, n: soft(math.hypot(p.x / 0.105, p.z / 0.15), 1.05, 1.2) * soft(-n.y, 0, 0.5) * (1 if p.z > -0.16 else 0))

# ---------------------------------------------------------------- guts on the floor
G_ = []
for _ in range(7):
    p = V((R(-.1, .1), R(-.1, .1), R(0.012, 0.05))); pts = [p]; h = R(0, 6.3)
    for k in range(11):
        h += R(-1.1, 1.1); q = pts[-1] + V((math.cos(h), math.sin(h), 0)) * 0.04; q.z = max(0.012, min(0.075, q.z + R(-0.015, 0.012)))
        if q.length > 0.17: q *= 0.17 / q.length; q.z = max(0.012, q.z)
        pts.append(q)
    G_.append(tube(pts, R(0.0125, 0.0175)))
pts = [V((0.1, 0, 0.02))]; h = 0.2
for k in range(15): h += R(-0.8, 0.8); pts.append(pts[-1] + V((math.cos(h), math.sin(h), 0)) * 0.05); pts[-1].z = 0.013
G_.append(tube(pts, 0.0135))
isg = near([[v.co.copy() for v in g.data.vertices] for g in G_], 0.006)
o = fuse(G_ + [ball("L", (-0.03, 0.04, 0.012), 0.085, (1.3, 1, 0.35)), ball("L", (0.06, -0.06, 0.01), 0.06, (1.3, 1, 0.4))], "H", 0.0036, 2)
finish(o, "GoreGuts", 5200, 1024, gut=lambda p: isg(p) * (0.4 + 0.6 * soft(p.z, 0.0, 0.012)), grain=35, amp=0.003)

# ---------------------------------------------------------------- meat
for k, (nm, r) in enumerate((("GoreChunkA", 0.05), ("GoreChunkB", 0.036), ("GoreChunkC", 0.065))):
    ps = [ball("C", (R(-.6, .6) * r, R(-.6, .6) * r, R(-.4, .4) * r), r * R(0.5, 0.9), (R(0.7, 1.4), R(0.7, 1.3), R(0.5, 1))) for _ in range(5)]
    for _ in range(7): d = V((R(-1, 1), R(-1, 1), R(-1, 1))).normalized(); e = d.cross(V((R(-1, 1), R(-1, 1), R(-1, 1)))).normalized(); ps.append(loft('F', curve([(d * r * 0.55, r * 0.34, r * 0.16, 2), (d * r * 0.85 + e * r * 0.35, r * 0.25, r * 0.09, 2), (d * r * 0.8 + e * r * R(0.7, 1.15), r * 0.08, r * 0.03, 2)], 10), 10))
    if k == 2: ps.append(tube([(-0.09, 0.01, 0.01), (0.1, -0.01, 0.0)], 0.014, 12))
    o = fuse(ps, "H", 0.0024, 1)
    finish(o, nm, 900, 256, bone=(lambda p: soft(0.02 - math.hypot(p.y, p.z), 0, 0.006) * soft(abs(p.x), 0.055, 0.07)) if k == 2 else (lambda p: 0.0), grain=55, amp=0.005, axis=0)
ps = [ball("C", (0, 0, 0), 0.042, (1, 0.8, 1.1)), ball("C", (0.012, -0.006, -0.035), 0.028, (0.9, 0.8, 1.2)), ball("C", (-0.02, 0, 0.03), 0.026), ball("C", (0.022, 0.004, 0.03), 0.022)]
for a, b, r in (((0, 0, 0.03), (-0.012, 0, 0.085), 0.012), ((0.014, 0, 0.03), (0.034, 0.004, 0.075), 0.0095), ((-0.02, 0, 0.03), (-0.045, 0.006, 0.066), 0.008)): ps.append(tube([a, b], r, 9, r * 0.9))
o = fuse(ps, "H", 0.002, 3); finish(o, "GoreHeart", 1100, 256, gut=lambda p: 0.35, grain=45, amp=0.002)

# ---------------------------------------------------------------- a picked ribcage (upright, front -y, spine base at 0)
ps, seg = [], []
def bone_tube(pts, r, r2=None):
    for a, b in zip(pts, pts[1:]): seg.append((V(a), V(b), max(r, r2 or 0)))
    ps.append(tube(pts, r, None, r2))
for i in range(15):
    c = V((0.006 * math.sin(i), 0.02 * math.sin(i / 14 * 3.1) + 0.06, 0.02 + i * 0.034)); seg.append((c, c + V((0, 0.02, 0)), 0.024))
    ps += [ball("Vt", c, 0.022, (1.1, 0.95, 0.72)), tube([c, c + V((0, 0.035, -0.012))], 0.007, 6, 0.003)]
ps.append(tube([(0, 0.06, 0), (0, 0.08, 0.25), (0, 0.06, 0.5)], 0.012, 20))
for i in range(10):
    z = 0.48 - 0.034 * i; w = 0.075 + 0.075 * math.sin(min(1, (i + 1) / 6.5) * 1.57); full = i < 7
    for sx in (1, -1):
        if random.random() < 0.16: f = R(0.35, 0.6)   # snapped
        else: f = 1 if full else 0.7
        pts = []
        for k in range(9):
            u = k / 8 * f; a = 0.2 + u * 2.75
            pts.append(V((sx * math.sin(a) * w, 0.07 + math.cos(a) * 0.1 - 0.115 + 0.035, z - 0.075 * u * u - 0.02 * u)))
        bone_tube(pts, 0.0072, 0.006)
bone_tube([(0, -0.085, 0.44), (0, -0.095, 0.34), (0, -0.095, 0.26)], 0.013, 0.008)
for _ in range(26):   # what's left between them
    a, b, _r = random.choice(seg); p = a.lerp(b, R(0, 1)); ps.append(tube([p, p + V((R(-.02, .02), R(-.02, .02), -R(0.015, 0.035))), p + V((R(-.03, .03), R(-.02, .02), -R(0.04, 0.085)))], R(0.004, 0.009), 9, 0.002))
for sx in (1, -1): ps.append(loft("M", curve([((sx * 0.03, 0.09, 0.05), 0.03, 0.014, 2), ((sx * 0.04, 0.105, 0.25), 0.035, 0.014, 2), ((sx * 0.03, 0.085, 0.46), 0.028, 0.012, 2)], 24), 14))
o = fuse(ps, "H", 0.0034, 1)
finish(o, "GoreRibs", 6500, 1024, bone=lambda p: max(soft(r * 1.5 - segd(p, a, b), 0, 0.003) for a, b, r in seg) * 0.92, grain=40, amp=0.003, rough=0.45)

# ---------------------------------------------------------------- a skull with some of the face left on
C0 = V((0, 0.02, 0.13))
ps = [ball("S", C0, 0.085, (0.84, 1.05, 0.95)), ball("S", (0, -0.035, 0.075), 0.055, (0.95, 0.75, 1)), ball("J", (0, -0.035, 0.028), 0.048, (0.95, 0.85, 0.5))] + [ball("K", (0.05 * s, -0.04, 0.085), 0.019) for s in (1, -1)]
o = fuse(ps, "H", 0.0028, 5)
holes = [(V((0.031 * s, -0.078, 0.107)), V((0.02, 0.03, 0.018))) for s in (1, -1)] + [(V((0, -0.083, 0.076)), V((0.011, 0.025, 0.016))), (V((0, -0.07, 0.041)), V((0.032, 0.03, 0.0045)))]
for c, r in holes:
    cut = ball("Cut", c, 1, r); m = o.modifiers.new("B", "BOOLEAN"); m.operation = "DIFFERENCE"; m.object = cut; m.solver = "EXACT"; apply_all(o); bpy.data.objects.remove(cut, do_unlink=True)
o.data.remesh_voxel_size = 0.0024; active(o); bpy.ops.object.voxel_remesh()
inhole = lambda p: max(0.0, *[1.15 - V(((p.x - c.x) / r.x, (p.y - c.y) / r.y, (p.z - c.z) / r.z)).length for c, r in holes])
finish(o, "GoreSkull", 2600, 512, bone=lambda p: (1 - soft(noise.noise(p * 16 + V((4, 4, 4))) + 0.5 * noise.noise(p * 45), 0.0, 0.14)) * (1 - min(1, inhole(p) * 8)), grain=60, amp=0.003, rough=0.45)
tz = []
for i in range(14):
    a = (i / 13 - 0.5) * 2.0
    for up in (1, -1): tz.append((V((math.sin(a) * 0.033, -0.036 - 0.042 * math.cos(a), 0.041 + up * 0.008)), (0, -0.1, -up), 0.0095, 0.0036, (0, 0, 0)))
low["GoreSkullTeeth"] = spikes("GoreSkullTeeth", tz, TOOTH)

# ---------------------------------------------------------------- a gnawed forearm and hand (elbow at 0, along -z)
w = V((0.01, -0.01, -0.26))
ps = [loft("A", curve([((0, 0, -0.05), 0.034, 0.036, 2), ((0.002, 0, -0.1), 0.038, 0.038, 2), ((0.006, -0.004, -0.19), 0.027, 0.026, 2), (w, 0.025, 0.018, 2), (w + V((0, -0.008, -0.085)), 0.042, 0.016, 2.2)], 34), 24)]
for f in range(4):
    r0 = w + V((0.03 - 0.02 * f, -0.01, -0.085)); L = (0.07, 0.083, 0.08, 0.06)[f]; k1 = r0 + V((0.003, -0.016 - 0.008 * f, -L * 0.55)); ps.append(tube([r0 + V((0, 0, 0.02)), r0, k1, k1 + V((0, -0.026, -L * 0.28))], 0.0098, 18, 0.0075))
ps.append(tube([w + V((0.03, -0.01, -0.02)), w + V((0.052, -0.026, -0.06)), w + V((0.05, -0.045, -0.095))], 0.0115, 14, 0.008))
for (x, y, r) in ((0.012, 0.004, 0.0095), (-0.012, -0.004, 0.0085)): ps.append(tube([(x, y, -0.09), (x * 1.2, y, 0.0)], r, 12)); ps.append(ball("K", (x * 1.2, y, 0.0), r * 1.35))
for i in range(13): a = R(0, 6.3); d = V((math.cos(a), math.sin(a), 0)); ps.append(tube([d * 0.03 + V((0, 0, -0.07)), d * R(0.03, 0.045) + V((0, 0, -0.04)), d * R(0.02, 0.06) + V((0, 0, R(-0.03, 0.01)))], R(0.005, 0.009), 9, 0.002))
o = fuse(ps, "H", 0.0024, 2)
bite = lambda p: soft(noise.noise(p * 17 + V((1, 2, 3))), 0.12, 0.25) * soft(p.z, -0.3, -0.2)
finish(o, "GoreArm", 3200, 512, bone=lambda p: soft(0.026 - math.hypot(p.x, p.y), 0, 0.006) * soft(p.z, -0.045, -0.03), hide=lambda p, n: soft(-p.z, 0.06, 0.085) * (1 - bite(p)), grain=55, amp=0.0028)
nz = []
for f in range(4): r0 = w + V((0.03 - 0.02 * f, -0.01, -0.085)); L = (0.07, 0.083, 0.08, 0.06)[f]; nz.append((r0 + V((0.003, -0.041 - 0.008 * f, -L * 0.8)), (0, -0.3, -1), 0.009, 0.005, (0, 0, 0)))

# ---------------------------------------------------------------- where a face was (worn by a Faceling body: origin at its head bone)
FC = V((0, 0.0, 0.14))
o = ball("H", FC, 0.1, (0.86, 1.1, 1.14)); ps = [o]
rags_ = torn(lambda a: V((math.cos(a) * 0.072, 0, math.sin(a) * 0.096)), 20, 0.015, 0.0035, 0.013)
for q in rags_: q.location = FC + V((0, -0.062, 0)); active(q); bpy.ops.object.transform_apply(location=True, rotation=False, scale=False)
ps += rags_
o = fuse(ps, "H", 0.0028, 2)
b = bmesh.new(); b.from_mesh(o.data); bmesh.ops.delete(b, geom=[v for v in b.verts if v.co.y > -0.045 and math.hypot(v.co.x / 0.086, (v.co.z - FC.z) / 0.11) < 1.1], context="VERTS"); b.to_mesh(o.data); b.free()
sock = [FC + V((0.034 * s, -0.095, 0.024)) for s in (1, -1)]; nose = FC + V((0, -0.11, -0.018))
for v in o.data.vertices:
    p = v.co
    for s in sock: p.y += 0.03 * math.exp(-((p - s).length / 0.02) ** 2)
    p.y += 0.018 * math.exp(-(math.hypot((p.x - nose.x) / 0.011, (p.z - nose.z) / 0.017)) ** 2)
    p.y -= 0.01 * math.exp(-((abs(p.x) - 0.045) / 0.016) ** 2 - ((p.z - FC.z + 0.015) / 0.013) ** 2)
    if abs(p.z - FC.z + 0.056) < 0.02 and abs(p.x) < 0.045: p.y += 0.008
pit = lambda p: max([math.exp(-((p - s).length / 0.03) ** 2) for s in sock])
finish(o, "GoreFace", 2600, 512, bone=lambda p: 0.55 * soft(noise.noise(p * 19), 0.1, 0.3) * (1 - pit(p)) + 0.5 * math.exp(-((abs(p.x) - 0.045) / 0.014) ** 2 - ((p.z - FC.z + 0.015) / 0.01) ** 2), grain=60, amp=0.0035)
tz = []
for i in range(13):
    a = (i / 12 - 0.5) * 1.9
    for up in (1, -1): tz.append((FC + V((math.sin(a) * 0.046, -0.04 - 0.062 * math.cos(a), -0.056 + up * 0.0105)), (0, -0.12, -up), 0.0115, 0.0046, (0, 0, 0)))
low["GoreFaceTeeth"] = spikes("GoreFaceTeeth", tz, TOOTH)
low["GoreFaceEye"] = eyes("GoreFaceEye", [(sock[0] + V((0, 0.013, 0)), 0.0125, (0.35, -1, 0.5))])   # one left

# ---------------------------------------------------------------- a flayed hide, hung by the shoulders (top at 0, front -y)
def flat_(keys): return loft("K", curve([(V(p), rx, 0.007, 2) for p, rx in keys], 26), 22)
ps = [flat_([((0, 0, 0.03), 0.02), ((0, 0, 0.005), 0.1), ((0, 0, -0.05), 0.19), ((0, 0.008, -0.14), 0.185), ((0, 0.01, -0.32), 0.15), ((0, 0, -0.46), 0.145), ((0, 0.005, -0.58), 0.17), ((0, 0.01, -0.68), 0.15), ((0, 0.01, -0.73), 0.03)]),
      flat_([((0, 0, 0.0), 0.05), ((0.01, -0.05, -0.02), 0.06), ((0.015, -0.075, -0.1), 0.085), ((0.01, -0.07, -0.19), 0.06), ((0, -0.06, -0.235), 0.012)])]   # the head flopped forward
for s in (1, -1):
    ps += [flat_([((0.1 * s, 0, 0.01), 0.02), ((0.17 * s, 0, -0.04), 0.06), ((0.222 * s, 0.005, -0.17), 0.058), ((0.24 * s, 0, -0.4), 0.044), ((0.235 * s, -0.01, -0.62), 0.028), ((0.24 * s, -0.01, -0.7), 0.044), ((0.24 * s, -0.01, -0.765), 0.034)]),
           flat_([((0.08 * s, 0.01, -0.5), 0.02), ((0.088 * s, 0.01, -0.62), 0.092), ((0.1 * s, 0, -0.85), 0.072), ((0.1 * s, 0.01, -1.02), 0.052), ((0.105 * s, 0.01, -1.15), 0.056), ((0.11 * s, 0, -1.34), 0.032), ((0.115 * s, -0.01, -1.42), 0.045), ((0.12 * s, -0.02, -1.47), 0.012)])]
    for f in range(4): ps.append(tube([(0.24 * s + (f - 1.5) * 0.018, -0.01, -0.75), (0.24 * s + (f - 1.5) * 0.026, -0.005, -0.84 - 0.01 * (f % 3))], 0.0078, 8, 0.005))
o = fuse(ps, "H", 0.0046, 1)
for v in o.data.vertices:
    p = v.co; k = soft(-p.z, 0, 0.5); p.x += 0.016 * noise.noise(V((p.x * 6, 5, p.z * 11))) + 0.007 * noise.noise(V((p.x * 20, 5, p.z * 34)))   # no straight edges
    p.y += 0.022 * math.sin(p.x * 34 + p.z * 5) * k + 0.02 * noise.noise(V((p.x * 7, 0, p.z * 4))) + 0.012 * noise.noise(V((p.x * 22, 3, p.z * 7)))
hd = lambda p: p.y < -0.035 and -0.23 < p.z < -0.03 and abs(p.x) < 0.12
b = bmesh.new(); b.from_mesh(o.data)   # eyes, mouth, and tears
hole = lambda p: (hd(p) and (math.hypot((p.x - 0.045) / 0.017, (p.z + 0.1) / 0.011) < 1 or math.hypot((p.x + 0.02) / 0.017, (p.z + 0.1) / 0.011) < 1 or math.hypot((p.x - 0.012) / 0.034, (p.z + 0.185) / 0.016) < 1)) or (noise.noise(V((p.x * 13, 0, p.z * 9))) + 0.4 * noise.noise(V((p.x * 40, 1, p.z * 40))) > 0.56)
bmesh.ops.delete(b, geom=[f for f in b.faces if hole(f.calc_center_median())], context="FACES"); bmesh.ops.delete(b, geom=[v for v in b.verts if not v.link_faces], context="VERTS"); b.to_mesh(o.data); b.free()
finish(o, "GoreHide", 5200, 1024, hide=lambda p, n: soft(-n.y, -0.1, 0.25) * 0.92, grain=30, amp=0.0015, rough=0.4)

# ---------------------------------------------------------------- a moth's cocoon with someone in it (back on the wall at y = 0, base at 0)
ps = [loft("C", curve([((0, -0.1, 0.15), 0.06, 0.05, 2), ((0, -0.13, 0.4), 0.19, 0.12, 2), ((0.01, -0.15, 0.75), 0.23, 0.15, 2), ((0, -0.16, 1.15), 0.26, 0.17, 2), ((0, -0.16, 1.42), 0.2, 0.15, 2), ((0.02, -0.17, 1.62), 0.15, 0.14, 2), ((0.02, -0.14, 1.85), 0.06, 0.06, 2)], 60), 44),
      ball("Hd", (0.02, -0.25, 1.6), 0.1, (0.85, 1, 1.15)), ball("Kn", (-0.1, -0.26, 0.72), 0.085), ball("Kn", (0.1, -0.24, 0.66), 0.08), ball("Sh", (0.17, -0.2, 1.36), 0.09), ball("Sh", (-0.16, -0.2, 1.36), 0.09)]
for i in range(38):   # strands to the wall
    z = R(0.15, 1.85); s = random.choice((1, -1)); a = V((s * R(0.05, 0.18), -0.12, z)); e = V((s * R(0.25, 0.5), -0.005, z + R(-0.3, 0.35))); ps.append(tube([a, a.lerp(e, 0.3) + V((0, 0.0, -0.02)), a.lerp(e, 0.65) + V((0, 0.02, -0.045)), e], R(0.018, 0.034), 26, R(0.007, 0.011)))
for i in range(20): z = R(0.35, 1.7); ps.append(tube([(-0.2, -0.16, z), (0, -0.315 + 0.07 * abs(z - 1), z + R(-.1, .1)), (0.2, -0.16, z + R(-.15, .15))], R(0.006, 0.014), 18))
e0 = V((-0.13, -0.22, 1.12)); ha = V((-0.17, -0.55, 1.0))   # a forearm got out
ARMSEG = (e0, ha + V((0.0, -0.15, -0.1)))
ps.append(loft("A", curve([(e0, 0.042, 0.042, 2), (e0.lerp(ha, 0.5), 0.032, 0.034, 2), (ha, 0.022, 0.017, 2), (ha + V((0, -0.07, -0.03)), 0.037, 0.014, 2)], 26), 20))
for f in range(4): r0 = ha + V((0.027 - 0.018 * f, -0.07, -0.03)); ps.append(tube([r0, r0 + V((0, -0.045, 0.004)), r0 + V((0, -0.065, -0.04))], 0.0092, 14, 0.007))
o = fuse(ps, "H", 0.0058, 3)
me = o.data; base = [v.co.copy() for v in me.vertices]
arm_ = lambda p: soft(0.062 - segd(p, *ARMSEG), 0, 0.012) * soft(-p.y, 0.33, 0.36)
for v, p, n in zip(me.vertices, base, [v.normal.copy() for v in me.vertices]):
    if arm_(p) > 0.5: continue
    v.co = p + n * (0.011 * max(0, 1 - abs(noise.noise(V((p.x * 9, p.y * 9, p.z * 5)))) * 5) + 0.006 * noise.noise(p * 20))
    q = p - V((0.02, -0.34, 1.6))   # the face pressed into it: sockets and an open mouth
    for c in (V((0.033, 0, 0.02)), V((-0.033, 0, 0.02))): v.co.y += 0.028 * math.exp(-((q - c).xz.length / 0.02) ** 2) * (1 if q.y < 0.07 else 0)
    v.co.y += 0.045 * math.exp(-(math.hypot(q.x / 0.026, (q.z + 0.055) / 0.034)) ** 2) * (1 if q.y < 0.09 else 0)
me.update()
def cc(i, _, n):
    p = base[i]; c = mix((0.5, 0.33, 0.14), (0.2, 0.1, 0.04), soft(noise.noise(p * 6) + 0.5 * noise.noise(p * 19), -0.3, 0.5))
    c = mix(c, (0.72, 0.58, 0.34), 0.6 * max(0, 1 - abs(noise.noise(V((p.x * 9, p.y * 9, p.z * 5)))) * 7))
    c = mix(c, (0.23, 0.01, 0.012), 0.85 * soft(noise.noise(V((p.x * 9, p.y * 9, p.z * 2.5)) + V((7, 1, 2))), 0.1, 0.4) * soft(1.6 - p.z, 0, 0.3))
    return mix(c, mix((0.62, 0.5, 0.45), (0.25, 0.02, 0.02), soft(noise.noise(p * 26), 0.0, 0.3)), arm_(p))
paint(o, cc); low["GoreCocoon"] = bake(o, "GoreCocoon", 7000, 1024, 0.3, 0.012)

# ---------------------------------------------------------------- out
for k, v in low.items(): v.name = k; v.data.name = k
bpy.ops.object.select_all(action="DESELECT")
for v in low.values(): v.select_set(True)
bpy.ops.export_scene.fbx(filepath=os.path.join(OUT, "GoreKit.fbx"), use_selection=True, object_types={"MESH"}, apply_unit_scale=True, apply_scale_options="FBX_SCALE_ALL", axis_forward="-Z", axis_up="Y", bake_anim=False, mesh_smooth_type="FACE")
print("GORE", {k: len(v.data.polygons) for k, v in low.items()})
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(HERE, "GoreKit.blend"))

if SHOTS:
    scene.cycles.samples = 24; scene.render.resolution_x = 1500; scene.render.resolution_y = 900; scene.view_settings.view_transform = "Standard"
    wd = bpy.data.worlds.new("W"); scene.world = wd; wd.use_nodes = True; wd.node_tree.nodes["Background"].inputs[0].default_value = (0.2, 0.2, 0.2, 1)
    for loc, e in (((2, -3, 3), 260), ((-3, -2, 1.5), 90)):
        l = bpy.data.lights.new("L", "AREA"); l.energy = e; l.size = 1.5; lo = link(bpy.data.objects.new("L", l)); lo.location = loc; lo.rotation_euler = (-V(loc)).to_track_quat("-Z", "Y").to_euler()
    small = ["GoreStump", "GoreSpine", "GoreBelly", "GoreGuts", "GoreChunkA", "GoreChunkC", "GoreHeart", "GoreSkull", "GoreArm", "GoreFace"]
    spots_ = {n: V(((i % 5 - 2) * 0.45, 0, 0.35 - 0.5 * (i // 5))) for i, n in enumerate(small)}
    for n, p in spots_.items():
        low[n].location = p - (V((0, 0, 0.14)) if n == "GoreFace" else V()); low[n].rotation_euler = (math.radians(55), 0, 0) if n in ("GoreStump", "GoreGuts") else (math.radians(20), 0, 0.5) if n == "GoreSpine" else (0, 0, 0)
    for a, b_ in (("GoreSkullTeeth", "GoreSkull"), ("GoreFaceTeeth", "GoreFace"), ("GoreFaceEye", "GoreFace")): low[a].location = low[b_].location
    big = {"GoreRibs": V((-1, 4, -0.2)), "GoreHide": V((0, 4, 0.9)), "GoreCocoon": V((1, 4, -0.75))}
    for n, p in big.items(): low[n].location = p
    cam = link(bpy.data.objects.new("Cam", bpy.data.cameras.new("Cam"))); scene.camera = cam
    def shot(name, loc, look, lens):
        cam.location = loc; cam.data.lens = lens; cam.rotation_euler = (V(look) - V(loc)).to_track_quat("-Z", "Y").to_euler(); scene.render.filepath = os.path.join(SHOTS, name); bpy.ops.render.render(write_still=True)
    for n in big: low[n].hide_render = True
    shot("gore_small.png", (0, -2.6, 0.1), (0, 0, 0.08), 38)
    for n in low: low[n].hide_render = n not in big
    shot("gore_big.png", (-0.4, 0.4, 0.2), (0, 4, 0.1), 44)
