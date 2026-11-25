# Wretches (wiki Entity 15): what people become when the Backrooms wear them down. Two models:
#   Wretch      skeletal, red-brown, flaking, full of holes and pustules, lipless and lidless, sludge running from it, in
#               rags. Extras the game shows on some: eyes in the wrong places, a third arm, skeletal wings, shirt, trousers.
#   WretchMass  a heap of flesh that is almost all eyes and mouths, dragging itself on stumps of limbs.
# Run: blender -b --factory-startup --python Art/build_wretch.py     (saves Art/Wretch.blend; exports FBX and WretchTex/)
# Faces -Y, z up, feet at 0.
import os
HERE = os.path.dirname(os.path.abspath(__file__))
exec(open(os.path.join(HERE, "build_partygoer.py"), encoding="utf-8").read().split("# ------------------------------------------------ body")[0])   # the shared helpers (loft, curve, ball, fuse...)
TEX = os.path.join(OUT, "WretchTex"); os.makedirs(TEX, exist_ok=True)
SHOTS = os.environ.get("WR_SHOTS", "")
random.seed(15)

RAW, SKIN, FLAKE, DARK, SLUDGE, PUS = (0.31, 0.09, 0.06), (0.4, 0.18, 0.11), (0.55, 0.38, 0.27), (0.06, 0.018, 0.012), (0.17, 0.07, 0.025), (0.72, 0.6, 0.3)
def paint(o, fn):
    me = o.data
    ca = me.color_attributes.get("Col") or me.color_attributes.new("Col", "FLOAT_COLOR", "POINT")
    buf = np.ones((len(me.vertices), 4), np.float32)
    for i, v in enumerate(me.vertices): buf[i, :3] = lin(fn(i, v.co.copy(), v.normal.copy()))
    ca.data.foreach_set("color", buf.ravel())
def ridged(p, s):
    d = noise.voronoi(p * s)[0]; return min(1.0, (d[1] - d[0]) * 2.2)
def spots(p, s, seed, size):
    """1 inside scattered round spots (a fraction of the cells), 0 outside."""
    d, pts = noise.voronoi(p * s + Vector((seed, 0, 0)))
    if noise.cell(pts[0] * 7.3) < 0.15: return 0.0
    return 1 - soft(d[0], size * 0.55, size)
def disease(o, drips, inside=None, extra=None):
    """Holes, pustules and flaking, as shape and colour together."""
    me = o.data
    base = [v.co.copy() for v in me.vertices]; nrm = [v.normal.copy() for v in me.vertices]
    hole = [spots(p, 13, 3, 0.26) * (0.5 + 0.5 * (noise.noise(p * 3) > -0.1)) for p in base]
    pust = [spots(p, 24, 31, 0.24) * soft(noise.noise(p * 4 + Vector((7, 7, 7))), -0.05, 0.2) for p in base]
    flk = [ridged(p + Vector((noise.noise(p * 30), noise.noise(p * 30 + Vector((5, 0, 0))), noise.noise(p * 30 + Vector((0, 5, 0))))) * 0.02, 70) for p in base]
    peel = [soft(noise.noise(p * 16) + 0.55 * noise.noise(p * 47) + 0.25 * noise.noise(p * 110), 0.02, 0.1) for p in base]   # sheets of dead skin with hard edges
    ins = [bool(inside and inside(p)) for p in base]
    for v, p, n, h, pu, f, k, pl in zip(me.vertices, base, nrm, hole, pust, flk, ins, peel):
        if k: v.co = p + n * 0.003 * noise.noise(p * 40); continue
        d = -0.014 * h + 0.007 * pu * (1 - h) - 0.0009 * (1 - f) + 0.004 * noise.noise(p * 9) + 0.0026 * pl + 0.002 * max(0.0, 1 - abs(noise.noise(Vector((p.x * 14, p.y * 14, p.z * 5)))) * 12)
        v.co = p + n * (d + (extra(p) if extra else 0))
    me.update()
    def col_at(i, p, n):
        p = base[i]
        if ins[i]: return mix(RAW, DARK, min(1, max(0, 0.6 + noise.noise(p * 25) * 0.5)))
        c = mix(SKIN, RAW, soft(noise.noise(p * 4) + 0.4 * noise.noise(p * 13), -0.35, 0.45))
        c = mix(c, FLAKE, 0.62 * peel[i] * (0.6 + 0.4 * noise.noise(p * 60)))
        c = mix(c, (0.33, 0.07, 0.05), 0.75 * max(0.0, 1 - abs(peel[i] - 0.5) * 2.2))   # raw at the edges
        c = mix(c, DARK, (1 - flk[i]) * 0.22)
        c = mix(c, PUS, pust[i] ** 4 * 0.55 * (1 - hole[i])); c = mix(c, RAW, pust[i] * (1 - pust[i]) * 1.6)
        c = mix(c, DARK, min(1, hole[i] * 1.2))
        # sludge runs down from the holes and the mouth
        run = soft(noise.noise(Vector((p.x * 26, p.y * 26, p.z * 1.7))), 0.18, 0.42) * soft(noise.noise(p * 3 + Vector((9, 2, 1))), -0.15, 0.25)
        return mix(c, SLUDGE, min(1.0, 0.9 * max(run, drips(p))))
    paint(o, col_at)

def hmat(o):
    m = bpy.data.materials.new("High"); m.use_nodes = True; nt = m.node_tree; nt.nodes.clear()
    out = nt.nodes.new("ShaderNodeOutputMaterial"); em = nt.nodes.new("ShaderNodeEmission"); at = nt.nodes.new("ShaderNodeVertexColor"); at.layer_name = "Col"
    nt.links.new(at.outputs[0], em.inputs[0]); nt.links.new(em.outputs[0], out.inputs[0])
    o.data.materials.clear(); o.data.materials.append(m)
scene.render.engine = "CYCLES"; scene.cycles.samples = 2; scene.cycles.device = "CPU"
def bake(hi, name, tris, size, rough=0.6, cage=0.007):
    """The painted dense mesh -> a game mesh with its colour and normal maps."""
    lo = hi.copy(); lo.data = hi.data.copy(); link(lo); lo.name = name
    active(lo); t0 = lo.modifiers.new("T", "TRIANGULATE"); bpy.ops.object.modifier_apply(modifier=t0.name)
    d = lo.modifiers.new("D", "DECIMATE"); d.ratio = min(1.0, tris / len(lo.data.polygons)); apply_all(lo); active(lo)
    bpy.ops.object.mode_set(mode="EDIT"); bpy.ops.mesh.select_all(action="SELECT"); bpy.ops.uv.smart_project(angle_limit=math.radians(66), island_margin=0.003); bpy.ops.object.mode_set(mode="OBJECT")
    for o in (lo, hi):
        for p in o.data.polygons: p.use_smooth = True
    hmat(hi)
    m = bpy.data.materials.new(name); m.use_nodes = True; nt = m.node_tree
    lo.data.materials.clear(); lo.data.materials.append(m)
    t = nt.nodes.new("ShaderNodeTexImage"); nt.nodes.active = t; imgs = {}
    for kind in ("color", "normal"):
        im = bpy.data.images.new(f"{name}_{kind}", size, size)
        if kind == "normal": im.colorspace_settings.name = "Non-Color"
        t.image = im
        bpy.ops.object.select_all(action="DESELECT"); hi.select_set(True); lo.select_set(True); bpy.context.view_layer.objects.active = lo
        bk = scene.render.bake; bk.use_selected_to_active = True; bk.cage_extrusion = cage; bk.max_ray_distance = cage * 2.5; bk.margin = 6
        bpy.ops.object.bake(type="EMIT" if kind == "color" else "NORMAL")
        im.filepath_raw = os.path.join(TEX, f"{name}_{kind}.png"); im.file_format = "PNG"; im.save(); imgs[kind] = im
    b = next(n for n in nt.nodes if n.type == "BSDF_PRINCIPLED"); b.inputs["Roughness"].default_value = rough
    t.image = imgs["color"]; nt.links.new(t.outputs[0], b.inputs["Base Color"])
    t2 = nt.nodes.new("ShaderNodeTexImage"); t2.image = imgs["normal"]; nm = nt.nodes.new("ShaderNodeNormalMap")
    nt.links.new(t2.outputs[0], nm.inputs["Color"]); nt.links.new(nm.outputs[0], b.inputs["Normal"])
    bpy.data.objects.remove(hi, do_unlink=True)
    return lo
def flat(name, color, rough=0.4):
    m = bpy.data.materials.get(name) or bpy.data.materials.new(name); m.use_nodes = True
    b = next(n for n in m.node_tree.nodes if n.type == "BSDF_PRINCIPLED"); b.inputs["Base Color"].default_value = (*lin(color), 1); b.inputs["Roughness"].default_value = rough
    return m
EYE, IRIS, TOOTH = flat("WretchEye", (0.6, 0.37, 0.27), 0.12), flat("WretchIris", (0.05, 0.035, 0.03), 0.1), flat("WretchTeeth", (0.6, 0.5, 0.3))
def eyes(name, spec):
    """spec: (centre, radius, looking). Lidless: a wet ball with a small dark iris."""
    b = bmesh.new()
    for c, r, d in spec:
        d = Vector(d).normalized(); q = Vector((0, 0, 1)).rotation_difference(d).to_matrix().to_4x4(); q.translation = Vector(c)
        ball_ = bmesh.ops.create_uvsphere(b, u_segments=20, v_segments=18, radius=r, matrix=q)
        for f in {f for v in ball_["verts"] for f in v.link_faces}:
            f.smooth = True; f.material_index = 1 if (f.calc_center_median() - Vector(c)).normalized().dot(d) > 0.95 else 0
    me = bpy.data.meshes.new(name); b.to_mesh(me); b.free(); me.materials.append(EYE); me.materials.append(IRIS)
    return link(bpy.data.objects.new(name, me))
def spikes(name, spec, m):
    """spec: (root, direction, length, radius, bend)."""
    b = bmesh.new()
    for root, d, L, r, bend in spec:
        d = Vector(d).normalized(); s1 = d.orthogonal().normalized(); s2 = d.cross(s1); bend = Vector(bend); prev = None
        for k in range(5):
            u = k / 4; w = r * (1 - u) ** 0.7 + 0.0004; c = Vector(root) + d * L * u + bend * u * u
            ring = [b.verts.new(c + (s1 * math.cos(a) + s2 * math.sin(a)) * w) for a in np.linspace(0, 2 * math.pi, 7)[:-1]]
            if prev:
                for j in range(6): b.faces.new((prev[j], prev[(j + 1) % 6], ring[(j + 1) % 6], ring[j]))
            prev = ring
        b.faces.new(prev)
    bmesh.ops.recalc_face_normals(b, faces=b.faces)
    me = bpy.data.meshes.new(name); b.to_mesh(me); b.free(); me.materials.append(m)
    for p in me.polygons: p.use_smooth = True
    return link(bpy.data.objects.new(name, me))

# ================================================================ the skeletal one
torso = loft("Torso", curve([((0, 0.0, 0.83), 0.07, 0.07, 2.2), ((0, 0.01, 0.92), 0.13, 0.085, 2.4), ((0, 0.02, 1.03), 0.085, 0.06, 2.0), ((0, 0.015, 1.12), 0.08, 0.06, 2.0),
    ((0, 0.0, 1.24), 0.13, 0.098, 2.2), ((0, 0.005, 1.36), 0.14, 0.098, 2.2), ((0, 0.015, 1.43), 0.13, 0.075, 2.0), ((0, 0.01, 1.48), 0.05, 0.048, 2.0), ((0, -0.02, 1.58), 0.04, 0.044, 2.0)], 46), 48)
parts = [torso]
WR = {}
for sx in (1, -1):
    s = "L" if sx > 0 else "R"
    sh, el, wr = Vector((0.175 * sx, 0.015, 1.41)), Vector((0.235 * sx, 0.04, 1.1)), Vector((0.25 * sx, -0.01, 0.8)); WR[s] = (sh, el, wr)
    parts += [ball("Sh", sh, 0.05), loft("Arm", curve([(Vector((0.09 * sx, 0.01, 1.42)), 0.05, 0.05, 2), (sh, 0.042, 0.042, 2), (sh.lerp(el, 0.4), 0.029, 0.031, 2), (el, 0.034, 0.034, 2), (el.lerp(wr, 0.3), 0.03, 0.03, 2), (wr, 0.019, 0.016, 2), (wr + Vector((0.003 * sx, -0.01, -0.08)), 0.034, 0.014, 2)], 34), 24)]
    for f in range(4):
        root = wr + Vector((0.003 * sx, -0.034 + 0.021 * f, -0.07)); L = (0.1, 0.12, 0.115, 0.09)[f]
        k1 = root + Vector((0.01 * sx, -0.012, -L * 0.5)); k2 = k1 + Vector((-0.012 * sx, -0.012, -L * 0.45))
        parts += [loft("F", curve([(root, 0.0105, 0.0105, 2), (k1, 0.0105, 0.0105, 2), (k1.lerp(k2, 0.5), 0.007, 0.007, 2), (k2, 0.007, 0.007, 2)], 14), 12)]
    parts.append(loft("Leg", curve([((0.075 * sx, 0.0, 0.94), 0.06, 0.06, 2), ((0.09 * sx, 0.0, 0.84), 0.06, 0.062, 2), ((0.092 * sx, -0.005, 0.64), 0.042, 0.046, 2), ((0.09 * sx, -0.012, 0.48), 0.046, 0.046, 2),
        ((0.09 * sx, 0.012, 0.37), 0.036, 0.042, 2), ((0.088 * sx, 0.012, 0.2), 0.024, 0.027, 2), ((0.087 * sx, 0.01, 0.08), 0.03, 0.034, 2)], 40), 28))
    parts.append(loft("Foot", curve([((0.087 * sx, 0.05, 0.045), 0.028, 0.03, 2), ((0.088 * sx, 0.0, 0.045), 0.032, 0.034, 2), ((0.092 * sx, -0.1, 0.026), 0.042, 0.022, 2.4), ((0.094 * sx, -0.17, 0.016), 0.036, 0.013, 2.4)], 18), 24))
HC = Vector((0, -0.045, 1.68))
parts += [ball("Skull", HC, 0.1, (0.8, 1.0, 1.02)), ball("Jaw", HC + Vector((0, -0.035, -0.1)), 0.062, (0.82, 0.95, 0.9)), ball("Cheek", HC + Vector((0, -0.05, -0.04)), 0.075, (0.86, 0.7, 0.6))]
body = fuse(parts, "BodyHigh", 0.0042, 6)
MOUTH, MR = HC + Vector((0, -0.105, -0.095)), Vector((0.04, 0.06, 0.042))
SOCK = [HC + Vector((0.034 * sx, -0.088, 0.008)) for sx in (1, -1)]
for c, r in ((MOUTH, MR), (SOCK[0], Vector((0.021,) * 3)), (SOCK[1], Vector((0.021,) * 3)), (HC + Vector((0, -0.102, -0.035)), Vector((0.011, 0.02, 0.016)))):
    cut = ball("Cut", c, 1, r); bo = body.modifiers.new("B", "BOOLEAN"); bo.operation = "DIFFERENCE"; bo.object = cut; bo.solver = "EXACT"; apply_all(body); bpy.data.objects.remove(cut, do_unlink=True)
body.data.remesh_voxel_size = 0.0038; active(body); bpy.ops.object.voxel_remesh()
def in_mouth(p):
    q = p - MOUTH; m = Vector((q.x / MR.x, q.y / MR.y, q.z / MR.z)).length < 1.06
    return m or any((p - s).length < 0.0235 for s in SOCK) or (abs(p.x) < 0.012 and abs(p.z - HC.z + 0.035) < 0.017 and p.y < HC.y - 0.075)
def gaunt(p):
    d = 0.0
    if 1.12 < p.z < 1.42 and abs(p.x) < 0.17: d += 0.011 * math.sin((p.z + abs(p.x) * 0.5 - p.y * 0.25) * 105) * soft(p.z, 1.12, 1.18) * soft(1.42 - p.z, 0, 0.06)
    if p.y > 0.02 and abs(p.x) < 0.035 and 0.9 < p.z < 1.55: d += 0.018 * (0.5 + 0.5 * math.sin(p.z * 115)) * (1 - abs(p.x) / 0.035)
    for sx in (1, -1):
        d += 0.016 * math.exp(-((p - Vector((0.08 * sx, -0.06, 1.425))).length / 0.045) ** 2) + 0.02 * math.exp(-((p - Vector((0.1 * sx, -0.07, 0.96))).length / 0.04) ** 2)   # collarbones, hips
        d += 0.012 * math.exp(-((p - Vector((0.08 * sx, 0.09, 1.33))).length / 0.06) ** 2) + 0.01 * math.exp(-((p - Vector((0.045 * sx, -0.12, 1.665))).length / 0.02) ** 2)      # shoulder blades, cheekbones
        d -= 0.014 * math.exp(-((p - Vector((0.07 * sx, -0.1, 1.62))).length / 0.025) ** 2)
    d -= 0.025 * math.exp(-((p - Vector((0, -0.07, 1.05))).length / 0.08) ** 2)   # the belly sucked in
    return d
def chin(p): return max(0.0, math.exp(-((p.x + 0.008 * math.sin(p.z * 70)) / 0.03) ** 2) * soft(p.z, 1.15, 1.5) * soft(1.62 - p.z, 0, 0.02)) if p.y < 0.02 else 0.0
disease(body, chin, in_mouth, gaunt)
low = {"Body": bake(body, "WretchBody", 15000, 2048)}

# teeth where teeth go, too many and the wrong sizes, and some where they don't; nails the same
tz = []
for i in range(13):
    a = (i / 12 - 0.5) * 2.4 + random.uniform(-0.06, 0.06)
    for up in (1, -1): tz.append((MOUTH + Vector((math.sin(a) * 0.033, 0.018 - 0.034 * math.cos(a), 0.033 * up * (0.45 + 0.55 * math.cos(a * 0.8)) + random.uniform(-0.003, 0.003))), (random.uniform(-0.5, 0.5), random.uniform(-0.5, 0.2), -up), random.choice((0.01, 0.014, 0.018, 0.027)), random.uniform(0.0028, 0.0055), (0, 0, 0)))
for i in range(9): tz.append((HC + Vector((0.075 + random.uniform(-0.01, 0.01), random.uniform(-0.05, 0.02), random.uniform(-0.06, 0.0))), (1, random.uniform(-0.6, 0), random.uniform(-0.4, 0.4)), random.uniform(0.01, 0.022), 0.005, (0, 0, 0)))
for i in range(8): tz.append((Vector((random.uniform(-0.06, 0.02), -0.085, 1.28 + random.uniform(-0.05, 0.05))), (random.uniform(-0.3, 0.3), -1, random.uniform(-0.3, 0.3)), random.uniform(0.012, 0.028), 0.0055, (0, 0, 0)))
low["Teeth"] = spikes("WretchTeethMesh", tz, TOOTH)
nz = []
for s, sx in (("L", 1), ("R", -1)):
    wr = WR[s][2]
    for f in range(4):
        L = (0.1, 0.12, 0.115, 0.09)[f]
        nz.append((wr + Vector((0.001 * sx, -0.034 + 0.021 * f - 0.024, -0.07 - L * 0.95)), (-0.1 * sx, -0.35, -1), random.uniform(0.035, 0.07), 0.0062, (0, 0.012, 0)))
    for i in range(3): nz.append((WR[s][1] + Vector((random.uniform(-0.01, 0.01), 0.026, random.uniform(-0.08, 0.04))), (0.3 * sx, 1, random.uniform(-0.6, 0.2)), random.uniform(0.02, 0.05), 0.006, (0, 0, 0)))
low["Nails"] = spikes("WretchNails", nz, TOOTH)
low["Eyes"] = eyes("WretchEyes", [(s + Vector((0, 0.005, 0)), 0.0178, (0.25 * (1 if s.x > 0 else -1), -1, 0.25 if s.x > 0 else -0.15)) for s in SOCK])
low["EyesA"] = eyes("WretchExtraEyesA", [((0.085, -0.085, 1.36), 0.024, (0.3, -1, 0.2)), ((0.05, -0.095, 1.3), 0.015, (0, -1, -0.3)), ((-0.17, -0.04, 1.4), 0.021, (-0.7, -0.6, 0.4)), ((0.02, -0.075, 1.5), 0.016, (0, -1, 0.1))])
low["EyesB"] = eyes("WretchExtraEyesB", [((0, -0.125, 1.745), 0.02, (0, -1, 0.5)), ((-0.07, -0.085, 1.72), 0.016, (-0.8, -0.8, 0.2)), ((0.078, -0.03, 1.66), 0.018, (1, -0.4, 0)), ((0.245, 0.005, 1.2), 0.018, (0.9, -0.5, 0)), ((0, 0.13, 1.35), 0.024, (0, 1, 0.1))])

# rags: the body's own shape, let out, torn and filthy
def rags(name, region, colour, off, seed, tris):
    src = low["Body"]; g = src.copy(); g.data = src.data.copy(); link(g); g.name = name
    b = bmesh.new(); b.from_mesh(g.data)
    rnd = random.Random(seed); sd = Vector((seed, 0, 0))
    bmesh.ops.delete(b, geom=[f for f in b.faces if not region(f.calc_center_median())], context="FACES")
    bmesh.ops.subdivide_edges(b, edges=b.edges[:], cuts=1, use_grid_fill=True)
    for _ in range(40): bmesh.ops.smooth_vert(b, verts=b.verts[:], factor=0.5, use_axis_x=True, use_axis_y=True, use_axis_z=True)
    b.normal_update()
    for v in b.verts: v.co += v.normal * (off + 0.005 * noise.noise(Vector((v.co.x * 34, v.co.y * 34, v.co.z * 9)) + sd) + 0.003 * noise.noise(v.co * 40 + sd))
    bmesh.ops.delete(b, geom=[f for f in b.faces if noise.noise(f.calc_center_median() * 11 + sd) + 0.5 * noise.noise(f.calc_center_median() * 30 + sd) > 0.28], context="FACES")
    lone = [v for v in b.verts if not v.link_faces]; bmesh.ops.delete(b, geom=lone, context="VERTS")
    b.to_mesh(g.data); b.free()
    so = g.modifiers.new("T", "SOLIDIFY"); so.thickness = 0.003; so.offset = 0; apply_all(g)
    def cp(i, p, n):
        c = mix(colour, mix(colour, (0.1, 0.08, 0.06), 0.75), soft(noise.noise(p * 7 + sd) + 0.6 * noise.noise(p * 22), -0.2, 0.5))
        weave = 0.5 + 0.5 * math.sin(p.z * 900) * math.sin((p.x + p.y) * 900)
        return mix(mix(c, SLUDGE, 0.85 * soft(noise.noise(Vector((p.x * 14, p.y * 14, p.z * 4)) + sd), 0.1, 0.45)), DARK, 0.12 * weave)
    paint(g, cp)
    return bake(g, name, tris, 512, 0.85, 0.004)
low["Shirt"] = rags("WretchShirt", lambda c: 0.98 < c.z < 1.47 and abs(c.x) < 0.21 and not (abs(c.x) < 0.06 and c.y < 0 and c.z > 1.4), (0.75, 0.75, 0.72), 0.022, 5, 4500)
low["Trousers"] = rags("WretchTrousers", lambda c: 0.25 < c.z < 0.97 and abs(c.x) < 0.17, (0.5, 0.52, 0.6), 0.02, 11, 4500)

# a third arm out of the ribs, and bone wings with rags of skin
a0, a1, a2 = Vector((0.1, -0.05, 1.25)), Vector((0.3, -0.12, 1.12)), Vector((0.24, -0.4, 1.1))
ps = [loft("A", curve([(Vector((0.05, -0.01, 1.27)), 0.05, 0.05, 2), (a0, 0.036, 0.036, 2), (a0.lerp(a1, 0.5), 0.022, 0.024, 2), (a1, 0.028, 0.028, 2), (a1.lerp(a2, 0.5), 0.02, 0.022, 2), (a2, 0.016, 0.014, 2), (a2 + Vector((-0.01, -0.07, 0)), 0.026, 0.012, 2)], 30), 20)]
for f in range(3):
    r0 = a2 + Vector((-0.03 + 0.02 * f, -0.06, 0)); ps.append(loft("F", curve([(r0, 0.009, 0.009, 2), (r0 + Vector((0, -0.05, 0.01)), 0.009, 0.009, 2), (r0 + Vector((0, -0.1, -0.03)), 0.005, 0.005, 2)], 12), 10))
xa = fuse(ps, "XHigh", 0.0038, 4); disease(xa, lambda p: 0.0); low["Arm"] = bake(xa, "WretchExtraArm", 2200, 512)
wing, WB = [], {}
for sx in (1, -1):
    s = "L" if sx > 0 else "R"
    w0, w1, w2 = Vector((0.06 * sx, 0.08, 1.36)), Vector((0.3 * sx, 0.26, 1.62)), Vector((0.5 * sx, 0.3, 1.3)); WB[s] = (w0, w1, w2)
    wing += [loft("W", curve([(w0, 0.024, 0.024, 2), (w0.lerp(w1, 0.5) + Vector((0, 0, 0.02)), 0.014, 0.014, 2), (w1, 0.021, 0.021, 2), (w1.lerp(w2, 0.5) + Vector((0.02 * sx, 0, 0)), 0.011, 0.011, 2), (w2, 0.006, 0.006, 2)], 30), 14)]
    tips = [w2, Vector((0.36 * sx, 0.3, 1.08)), Vector((0.2 * sx, 0.24, 1.0))]
    for t in tips[1:]: wing.append(loft("W", curve([(w1, 0.014, 0.014, 2), (w1.lerp(t, 0.5) + Vector((0.03 * sx, 0.01, 0)), 0.008, 0.008, 2), (t, 0.004, 0.004, 2)], 22), 12))
wi = fuse(wing, "WHigh", 0.0035, 2); disease(wi, lambda p: 0.0); low["Wings"] = bake(wi, "WretchWings", 3500, 512)

# ---------------- rig
arm = bpy.data.armatures.new("WretchRig"); rig = link(bpy.data.objects.new("WretchRig", arm)); active(rig)
bpy.ops.object.mode_set(mode="EDIT")
def bone(a_, n, a, b, parent=None):
    e = a_.edit_bones.new(n); e.head = a; e.tail = b
    if parent: e.parent = a_.edit_bones[parent]
bone(arm, "Hips", (0, 0.01, 0.9), (0, 0.015, 1.06)); bone(arm, "Spine", (0, 0.015, 1.06), (0, 0.0, 1.24), "Hips"); bone(arm, "Chest", (0, 0, 1.24), (0, 0.015, 1.46), "Spine")
bone(arm, "Neck", (0, 0.01, 1.48), (0, -0.02, 1.58), "Chest"); bone(arm, "Head", (0, -0.02, 1.58), (0, -0.05, 1.8), "Neck")
for s, sx in (("L", 1), ("R", -1)):
    sh, el, wr = WR[s]
    bone(arm, f"UpperArm.{s}", sh, el, "Chest"); bone(arm, f"Forearm.{s}", el, wr, f"UpperArm.{s}"); bone(arm, f"Hand.{s}", wr, wr + Vector((0, -0.03, -0.2)), f"Forearm.{s}")
    bone(arm, f"Thigh.{s}", (0.09 * sx, 0, 0.9), (0.09 * sx, -0.012, 0.48), "Hips"); bone(arm, f"Shin.{s}", (0.09 * sx, -0.012, 0.48), (0.087 * sx, 0.01, 0.07), f"Thigh.{s}"); bone(arm, f"Foot.{s}", (0.087 * sx, 0.01, 0.07), (0.094 * sx, -0.16, 0.02), f"Shin.{s}")
bpy.ops.object.mode_set(mode="OBJECT")
bpy.ops.object.select_all(action="DESELECT"); low["Body"].select_set(True); rig.select_set(True); bpy.context.view_layer.objects.active = rig
bpy.ops.object.parent_set(type="ARMATURE_AUTO")
active(rig); bpy.ops.object.mode_set(mode="EDIT")
bone(arm, "ExtraA", a0, a1, "Chest"); bone(arm, "ExtraB", a1, a2 + (a2 - a1) * 0.4, "ExtraA")
for s in "LR": bone(arm, f"WingA.{s}", WB[s][0], WB[s][1], "Chest"); bone(arm, f"WingB.{s}", WB[s][1], WB[s][2], f"WingA.{s}")
bpy.ops.object.mode_set(mode="OBJECT")
def bind(o, r, fn):
    o.vertex_groups.clear()
    for v in o.data.vertices:
        for n, w in fn(v.co):
            g = o.vertex_groups.get(n) or o.vertex_groups.new(name=n); g.add([v.index], w, "ADD")
    o.parent = r
    if not o.modifiers: o.modifiers.new("Armature", "ARMATURE")
    o.modifiers[0].object = r
from mathutils.kdtree import KDTree
bd = low["Body"].data; kd = KDTree(len(bd.vertices))
for v in bd.vertices: kd.insert(v.co, v.index)
kd.balance(); gn = [g.name for g in low["Body"].vertex_groups]
def as_body(p):   # moves with the skin nearest to it
    return [(gn[g.group], g.weight) for g in bd.vertices[kd.find(p)[1]].groups if g.weight > 0.01]
def nearest(bones):
    def fn(p):
        best = (9, None)
        for n, a, b in bones:
            d = b - a; u = max(0, min(1, (p - a).dot(d) / d.length_squared)); k = (a + d * u - p).length
            if k < best[0]: best = (k, n)
        return [(best[1], 1.0)]
    return fn
for k in ("Shirt", "Trousers", "Teeth", "Nails", "Eyes", "EyesA", "EyesB"): bind(low[k], rig, as_body)
for v in low["Eyes"].data.vertices: pass
bind(low["Eyes"], rig, lambda p: [("Head", 1.0)])
bind(low["Arm"], rig, nearest([("ExtraA", a0, a1), ("ExtraB", a1, a2 + (a2 - a1) * 0.4)]))
bind(low["Wings"], rig, nearest([(f"WingA.{s}", WB[s][0], WB[s][1]) for s in "LR"] + [(f"WingB.{s}", WB[s][1], t) for s, sx in (("L", 1), ("R", -1)) for t in (WB[s][2], Vector((0.36 * sx, 0.3, 1.08)), Vector((0.2 * sx, 0.24, 1.0)))]))
def export(objs, r, name):
    bpy.ops.object.select_all(action="DESELECT")
    for o in [*objs, r]: o.select_set(True)
    bpy.ops.export_scene.fbx(filepath=os.path.join(OUT, name + ".fbx"), use_selection=True, object_types={"ARMATURE", "MESH"}, apply_unit_scale=True, apply_scale_options="FBX_SCALE_ALL",
                             axis_forward="-Z", axis_up="Y", add_leaf_bones=False, bake_anim=False, mesh_smooth_type="FACE", use_armature_deform_only=False)
export(low.values(), rig, "Wretch")
print("WRETCH", {k: len(v.data.polygons) for k, v in low.items()})

# ================================================================ the mass
LOBES = [(Vector(c), r) for c, r in (((0, 0.05, 0.55), 0.52), ((0.36, -0.12, 0.42), 0.36), ((-0.38, -0.05, 0.5), 0.38), ((0.1, -0.15, 0.98), 0.4), ((-0.22, 0.12, 1.08), 0.32), ((0.2, 0.3, 0.8), 0.36), ((0.02, -0.36, 0.5), 0.3), ((0.02, 0.0, 1.36), 0.24), ((-0.3, -0.3, 0.3), 0.26), ((0.35, 0.3, 0.3), 0.3))]
ps = [ball("L", c, r) for c, r in LOBES]
LIMBS = {}
for i, (base, knee, end) in enumerate((((0.5, -0.3, 0.5), (0.85, -0.5, 0.5), (0.95, -0.75, 0.03)), ((-0.5, -0.3, 0.55), (-0.85, -0.4, 0.55), (-1.0, -0.65, 0.03)), ((0.4, 0.4, 0.4), (0.8, 0.5, 0.35), (0.95, 0.6, 0.03)), ((-0.35, 0.35, 0.4), (-0.75, 0.5, 0.4), (-0.95, 0.5, 0.03)), ((0.05, -0.4, 1.1), (0.15, -0.75, 1.25), (0.05, -1.0, 1.0)))):
    b, k, e = map(Vector, (base, knee, end)); LIMBS[i] = (b, k, e); o = b * 0.6 + Vector((0, 0, b.z * 0.4))
    ps.append(loft("Limb", curve([(o, 0.13, 0.13, 2), (b, 0.085, 0.085, 2), (b.lerp(k, 0.5), 0.05, 0.055, 2), (k, 0.062, 0.062, 2), (k.lerp(e, 0.5), 0.042, 0.046, 2), (e, 0.04, 0.034, 2)], 40), 22))
    d = (e - k).normalized()
    for f in (-1, 0, 1):
        sd = Vector((f * 0.035, 0, 0)); ps.append(loft("F", curve([(e + sd * 0.5, 0.018, 0.018, 2), (e + sd * 1.6 + Vector((0, -0.07, 0.02 if i == 4 else 0)), 0.015, 0.015, 2), (e + sd * 2.2 + Vector((0, -0.14, -0.02 if i == 4 else 0)), 0.006, 0.006, 2)], 12), 10))
mass = fuse(ps, "MassHigh", 0.011, 12)
from mathutils.bvhtree import BVHTree
def surface(d, frm=Vector((0, 0, 0.62))):
    bv = surface.bv; far = frm + Vector(d).normalized() * 3; h = bv.ray_cast(far, (frm - far).normalized()); return h[0], h[1]
surface.bv = BVHTree.FromObject(mass, bpy.context.evaluated_depsgraph_get())
mouths, eyesp, rnd = [], [], random.Random(3)
for d, w, h in (((0.05, -1, 0.15), 0.2, 0.13), ((0.75, -0.6, 0.0), 0.13, 0.09), ((-0.8, -0.5, 0.25), 0.15, 0.08), ((0.2, -0.5, 0.75), 0.12, 0.1), ((-0.6, 0.7, 0.3), 0.14, 0.1), ((0.7, 0.6, 0.4), 0.15, 0.09), ((-0.15, 0.1, 1), 0.1, 0.07), ((-0.35, -0.9, -0.25), 0.12, 0.07)):
    p, n = surface(d); mouths.append((p, n, w, h))
tries = 0
while len(eyesp) < 34 and tries < 4000:
    tries += 1; d = Vector((rnd.uniform(-1, 1), rnd.uniform(-1.2, 0.8), rnd.uniform(-0.3, 1.1)))
    if d.length < 0.3: continue
    p, n = surface(d); r = rnd.choice((0.028, 0.035, 0.045, 0.06, 0.085))
    if p is None or p.z < 0.12 or any((p - m[0]).length < m[2] + r * 1.2 for m in mouths) or any((p - e[0]).length < (r + e[1]) * 1.25 for e in eyesp) or any((p - k).length < 0.15 for L in LIMBS.values() for k in L[1:]): continue
    eyesp.append((p, r, n))
mteeth = []
def frame(n): u = Vector((0, 0, 1)); u = (u - n * u.dot(n)); u = u.normalized() if u.length > 0.2 else n.orthogonal().normalized(); return u.cross(n).normalized(), u
for p, n, w, h in mouths:
    s_, u_ = frame(n)   # pressed in rather than cut out: the boolean turned these into bumps
    for v in mass.data.vertices:
        q = v.co - p; k = math.hypot(q.dot(s_) / (w * 0.82), q.dot(u_) / (h * 0.82))
        if k < 1 and abs(q.dot(n)) < 0.2: v.co -= n * (0.17 * math.sqrt(1 - k * k) + 0.02)
    N = int(w * 130)
    for i in range(N):
        a = 2 * math.pi * i / N; o = s_ * math.cos(a) * w * 0.78 + u_ * math.sin(a) * h * 0.78
        mteeth.append((p + o - n * 0.012, (-o.normalized() * 0.75 + n * rnd.uniform(0.3, 0.8)), rnd.choice((0.04, 0.06, 0.09)), rnd.uniform(0.009, 0.016), -o.normalized() * 0.012))
mass.data.remesh_voxel_size = 0.0095; active(mass); bpy.ops.object.voxel_remesh()
def in_m(p):
    for c, n, w, h in mouths:
        q = p - c; s_, u_ = frame(n)
        if math.hypot(q.dot(s_) / (w * 0.82), q.dot(u_) / (h * 0.82)) < 1.0 and -0.25 < q.dot(n) < -0.015: return True
    return False
def lids(p):
    d = 0.0
    for c, r, n in eyesp:
        k = (p - c).length / r
        if k < 1.7: d = max(d, 0.35 * r * math.exp(-((k - 1.15) / 0.22) ** 2))
    for c, n, w, h in mouths:
        q = p - c; s_, u_ = frame(n); k = math.hypot(q.dot(s_) / (w * 0.82), q.dot(u_) / (h * 0.82))
        if abs(q.dot(n)) < 0.1 and 0.9 < k < 1.5: d = max(d, 0.022 * math.exp(-((k - 1.18) / 0.13) ** 2))
    return d + 0.02 * noise.noise(p * 5) + 0.012 * noise.noise(p * 12)
disease(mass, lambda p: max([0.0] + [math.exp(-((p.x - c.x) / (w * 0.5)) ** 2) * soft(c.z - p.z, 0.0, 0.08) * soft(p.z, c.z - 0.45, c.z - 0.1) for c, n, w, h in mouths if (p - c).length < 0.5]), in_m, lids)
mlow = {"Body": bake(mass, "WretchMassBody", 17000, 2048, 0.5, 0.015)}
mlow["Teeth"] = spikes("WretchMassTeeth", mteeth, TOOTH)
mlow["Eyes"] = eyes("WretchMassEyes", [(c - n * r * 0.25, r, (n + Vector((rnd.uniform(-0.35, 0.35), rnd.uniform(-0.5, 0.1), rnd.uniform(-0.3, 0.3))))) for c, r, n in eyesp])
ma = bpy.data.armatures.new("WretchMassRig"); mr = link(bpy.data.objects.new("WretchMassRig", ma)); active(mr)
bpy.ops.object.mode_set(mode="EDIT")
bone(ma, "Root", (0, 0, 0.05), (0, 0, 0.5)); LB = {}
for i, (c, r) in enumerate(LOBES[1:8]): bone(ma, f"Lobe{i}", c * 0.5 + Vector((0, 0, 0.3)), c, "Root"); LB[f"Lobe{i}"] = (c, r)
for i, (b, k, e) in LIMBS.items(): bone(ma, f"LimbA{i}", b, k, "Root"); bone(ma, f"LimbB{i}", k, e, f"LimbA{i}")
bpy.ops.object.mode_set(mode="OBJECT")
def mass_w(p):
    out = {}
    for i, (b, k, e) in LIMBS.items():
        for n, a, c in ((f"LimbA{i}", b, k), (f"LimbB{i}", k, e + (e - k).normalized() * 0.15)):
            d = c - a; u = (p - a).dot(d) / d.length_squared
            if u < -0.15: continue
            dist = (a + d * min(1, max(0, u)) - p).length
            if dist < 0.13: out[n] = (1 - dist / 0.13) * (soft(u, -0.15, 0.25) if n.startswith("LimbA") else 1) * 3
    for n, (c, r) in LB.items():
        k = (p - c).length / (r * 1.25)
        if k < 1: out[n] = out.get(n, 0) + (1 - k)
    out["Root"] = 0.25
    t = sum(out.values()); return [(n, w / t) for n, w in out.items() if w / t > 0.02]
for o in mlow.values(): bind(o, mr, mass_w)
for o in (mlow["Teeth"], mlow["Eyes"]):   # each tooth and eye rigid: every vertex takes its island's weights
    n = len(o.data.vertices); par = list(range(n))
    def fnd(x):
        while par[x] != x: par[x] = par[par[x]]; x = par[x]
        return x
    for e in o.data.edges: par[fnd(e.vertices[0])] = fnd(e.vertices[1])
    isl = {}
    for i in range(n): isl.setdefault(fnd(i), []).append(i)
    o.vertex_groups.clear()
    for vs in isl.values():
        for nme, w in mass_w(sum((o.data.vertices[i].co for i in vs), Vector()) / len(vs)):
            g = o.vertex_groups.get(nme) or o.vertex_groups.new(name=nme); g.add(vs, w, "REPLACE")
export(mlow.values(), mr, "WretchMass")
print("MASS", {k: len(v.data.polygons) for k, v in mlow.items()}, "eyes", len(eyesp))
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(HERE, "Wretch.blend"))

# ---------------- review renders
if SHOTS:
    scene.cycles.samples = 20; scene.render.resolution_x = 620; scene.render.resolution_y = 820; scene.view_settings.view_transform = "Standard"
    w = bpy.data.worlds.new("W"); scene.world = w; w.use_nodes = True; w.node_tree.nodes["Background"].inputs[0].default_value = (0.12, 0.12, 0.12, 1)
    for loc, e in (((2, -3, 3), 300), ((-3, -1.5, 2), 110), ((0, 3, 3), 150)):
        l = bpy.data.lights.new("L", "AREA"); l.energy = e; l.size = 1.2; lo = link(bpy.data.objects.new("L", l)); lo.location = loc; lo.rotation_euler = (Vector((0, 0, 1)) - Vector(loc)).to_track_quat("-Z", "Y").to_euler()
    cam = link(bpy.data.objects.new("Cam", bpy.data.cameras.new("Cam"))); scene.camera = cam
    def shot(name, loc, look, lens=50, hide=()):
        for o in [*low.values(), *mlow.values()]: o.hide_render = o in hide
        cam.location = loc; cam.data.lens = lens; cam.rotation_euler = (Vector(look) - Vector(loc)).to_track_quat("-Z", "Y").to_euler()
        scene.render.filepath = os.path.join(SHOTS, "wr_" + name + ".png"); bpy.ops.render.render(write_still=True)
    M = list(mlow.values()); X = [low[k] for k in ("EyesA", "EyesB", "Arm", "Wings", "Shirt")]
    shot("front", (0.4, -3.6, 1.2), (0, 0, 0.92), 50, M + X); shot("face", (0.2, -0.8, 1.68), (0, 0, 1.62), 50, M + X); shot("extras", (-1.2, -3.4, 1.5), (0, 0, 0.98), 50, M)
    shot("mass", (0.9, -4.2, 1.5), (0, 0, 0.65), 50, list(low.values()))
