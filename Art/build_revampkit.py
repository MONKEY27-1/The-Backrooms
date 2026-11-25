# Graphics revamp, Stage C: the shared ceiling/wall kit. Run headless:
#   blender -b --factory-startup --python Art/build_revampkit.py
# Saves Art/RevampKit.blend and exports each piece to Assets/_Game/Resources/Models/<name>.fbx.
# Materials are named after PropPaint (or "Trim"/"Tile", which the game maps to the surface's own material).
import bpy, bmesh, math, os
from mathutils import Vector

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT = os.path.join(ROOT, "Assets", "_Game", "Resources", "Models")
PREVIEW = {"Trim": (0.8, 0.75, 0.6, 1), "Tile": (0.9, 0.88, 0.8, 1), "Metal": (0.6, 0.6, 0.62, 1),
           "White": (0.9, 0.9, 0.88, 1), "Brass": (0.7, 0.55, 0.25, 1), "Red": (0.8, 0.05, 0.03, 1), "Dark": (0.05, 0.05, 0.05, 1)}

for o in list(bpy.data.objects): bpy.data.objects.remove(o, do_unlink=True)

def mat(name):
    m = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    m.diffuse_color = PREVIEW.get(name, (0.5, 0.5, 0.5, 1))
    return m

def new_obj(name, bm, mats, loc_x):
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me); bm.free()
    for m in mats: me.materials.append(mat(m))
    o = bpy.data.objects.new(name, me)
    bpy.context.scene.collection.objects.link(o)
    for p in me.polygons: p.use_smooth = False
    return o

def export(o):
    for x in bpy.context.selected_objects: x.select_set(False)
    o.select_set(True); bpy.context.view_layer.objects.active = o
    loc = o.location.copy(); o.location = (0, 0, 0)
    bpy.ops.export_scene.fbx(filepath=os.path.join(OUT, o.name + ".fbx"), use_selection=True, object_types={"MESH"},
                             apply_unit_scale=True, apply_scale_options="FBX_SCALE_ALL", axis_forward="-Z", axis_up="Y",
                             mesh_smooth_type="FACE", add_leaf_bones=False, bake_anim=False, bake_space_transform=True)
    o.location = loc
    print("KIT", o.name, len(o.data.vertices), "verts", tuple(round(d, 3) for d in o.dimensions))

def uv_meters(bm, axes):
    """UVs straight from position in meters (the game's materials tile in meters)."""
    lay = bm.loops.layers.uv.verify()
    for f in bm.faces:
        for l in f.loops:
            l[lay].uv = axes(l.vert.co, f.normal)

# ---- KitBaseboard: 1 m of skirting board along +X (0..1). Back flat on the wall at y=0, front towards -Y.
# Profile (y depth, z height): a flat board with a rounded bullnose top and a small bead at the floor.
prof = [(0.0, 0.0), (-0.0145, 0.0), (-0.0145, 0.006), (-0.012, 0.009), (-0.012, 0.088)]
for i in range(1, 7):                                   # bullnose: quarter circle r=12 mm up to the wall at 0.1
    a = math.radians(90 * i / 6)
    prof.append((-0.012 * math.cos(a), 0.088 + 0.012 * math.sin(a)))
bm = bmesh.new()
rings = [[bm.verts.new((x, y, z)) for (y, z) in prof] for x in (0.0, 1.0)]
for i in range(len(prof) - 1):
    bm.faces.new((rings[0][i], rings[1][i], rings[1][i + 1], rings[0][i + 1]))
bm.faces.new(list(reversed(rings[0])))                  # end caps (hidden where pieces meet)
bm.faces.new(rings[1])
bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
uv_meters(bm, lambda p, n: (p.x, p.z) if abs(n.x) < 0.5 else (p.y, p.z))
export(new_obj("KitBaseboard", bm, ["Trim"], 0))

def cyl(bm, r, z0, z1, segs=14):
    ret = bmesh.ops.create_cone(bm, cap_ends=True, segments=segs, radius1=r, radius2=r, depth=z1 - z0)
    for v in ret["verts"]: v.co.z += (z0 + z1) / 2
    return ret["verts"]

def faces_of(verts):
    s = set(verts)
    return [f for f in {f for v in verts for f in v.link_faces} if all(v in s for v in f.verts)]

# ---- KitSprinkler: pendant fire sprinkler. Origin on the ceiling plane, hangs down (-Z).
bm = bmesh.new()
parts = [(0.032, -0.004, 0.0, 0), (0.011, -0.03, -0.004, 1), (0.006, -0.048, -0.03, 1), (0.018, -0.052, -0.048, 1)]
for r, z0, z1, m in parts:
    for f in faces_of(cyl(bm, r, z0, z1)): f.material_index = m
for sx in (-1, 1):                                       # the two frame arms down to the deflector
    v = bmesh.ops.create_cube(bm, size=1.0)["verts"]
    for p in v: p.co = Vector((p.co.x * 0.003 + sx * 0.008, p.co.y * 0.004, p.co.z * 0.044 - 0.026))
    for f in faces_of(v): f.material_index = 1
uv_meters(bm, lambda p, n: (p.x, p.z))
export(new_obj("KitSprinkler", bm, ["White", "Brass"], 0))

# ---- KitSmokeDetector: round, white, a red LED. Origin on the ceiling plane.
bm = bmesh.new()
for f in faces_of(cyl(bm, 0.065, -0.03, 0.0, 24)): f.material_index = 0
for f in faces_of(cyl(bm, 0.045, -0.038, -0.03, 24)): f.material_index = 0
v = bmesh.ops.create_cube(bm, size=1.0)["verts"]
for p in v: p.co = Vector((p.co.x * 0.004 + 0.03, p.co.y * 0.004, p.co.z * 0.003 - 0.0395))
for f in faces_of(v): f.material_index = 1
uv_meters(bm, lambda p, n: (p.x, p.y))
export(new_obj("KitSmokeDetector", bm, ["White", "Red"], 0))

# ---- KitSagTile: a water-logged tile bowing down out of its grid square. Origin = the tile's centre on the
# ceiling plane; 0.48 m square; UVs land inside one tile of the 6x6 ceiling texture (tiled every 3 m in game).
bm = bmesh.new()
N = 8
grid = []
for j in range(N + 1):
    row = []
    for i in range(N + 1):
        x = -0.24 + 0.48 * i / N; y = -0.24 + 0.48 * j / N
        sag = 0.045 * math.cos(x / 0.24 * math.pi / 2) * math.cos(y / 0.24 * math.pi / 2)
        row.append(bm.verts.new((x, y, -0.004 - sag)))
    grid.append(row)
for j in range(N):
    for i in range(N):
        bm.faces.new((grid[j][i], grid[j + 1][i], grid[j + 1][i + 1], grid[j][i + 1]))   # faces down
bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
for f in bm.faces:
    if f.normal.z > 0: f.normal_flip()
lay = bm.loops.layers.uv.verify()
for f in bm.faces:
    for l in f.loops:
        l[lay].uv = (l.vert.co.x + 0.25, l.vert.co.y + 0.25)
export(new_obj("KitSagTile", bm, ["Tile"], 0))

bpy.ops.wm.save_as_mainfile(filepath=os.path.join(ROOT, "Art", "RevampKit.blend"))
print("KIT_OK")
