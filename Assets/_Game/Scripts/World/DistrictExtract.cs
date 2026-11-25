using System.Collections.Generic;
using UnityEngine;

namespace Backrooms.World
{
    public static partial class DistrictLayout
    {
        sealed partial class Plan
        {
            /// One cell of a Hole field: a walkway rim around a square pit lined with the room's walls, PitDepth deep
            /// with a black bottom. Shallow enough to stay above the tallest ceiling of the floor below.
            static void PitShaft(ChunkLayout L, Rect cell, float floorY, SurfaceRef floor, SurfaceRef wall, bool rot)
            {
                const float m = PitRim, t = 0.1f, d = PitDepth;
                var a = new Rect(cell.xMin + m, cell.yMin + m, cell.width - 2f * m, cell.height - 2f * m);   // the opening
                // The rim: four strips of floor around the opening.
                L.Quads.Add(new SurfaceQuad { Area = new Rect(cell.xMin, cell.yMin, cell.width, m), Height = floorY, FacingUp = true, Rotated = rot, Surface = floor });
                L.Quads.Add(new SurfaceQuad { Area = new Rect(cell.xMin, a.yMax, cell.width, m), Height = floorY, FacingUp = true, Rotated = rot, Surface = floor });
                L.Quads.Add(new SurfaceQuad { Area = new Rect(cell.xMin, a.yMin, m, a.height), Height = floorY, FacingUp = true, Rotated = rot, Surface = floor });
                L.Quads.Add(new SurfaceQuad { Area = new Rect(a.xMax, a.yMin, m, a.height), Height = floorY, FacingUp = true, Rotated = rot, Surface = floor });
                // The shaft: walls just outside the opening (under the rim), so their faces line the pit.
                float y = floorY - d / 2f;
                var sides = BoxMeshBuilder.Faces.Sides;
                L.Boxes.Add(SurfaceBox.Uniform(new Bounds(new Vector3(a.center.x, y, a.yMin - t / 2f), new Vector3(a.width + 2f * t, d, t)), wall, sides));
                L.Boxes.Add(SurfaceBox.Uniform(new Bounds(new Vector3(a.center.x, y, a.yMax + t / 2f), new Vector3(a.width + 2f * t, d, t)), wall, sides));
                L.Boxes.Add(SurfaceBox.Uniform(new Bounds(new Vector3(a.xMin - t / 2f, y, a.center.y), new Vector3(t, d, a.height)), wall, sides));
                L.Boxes.Add(SurfaceBox.Uniform(new Bounds(new Vector3(a.xMax + t / 2f, y, a.center.y), new Vector3(t, d, a.height)), wall, sides));
                L.Boxes.Add(SurfaceBox.Uniform(new Bounds(new Vector3(a.center.x, floorY - d - t / 2f, a.center.y), new Vector3(a.width, t, a.height)), new SurfaceRef(Surface.Void), BoxMeshBuilder.Faces.PosY));
            }

            /// Everything of this district that chunk (cx, cz) owns. Cells, vertices and the edges starting at a
            /// vertex belong to the chunk containing that cell, so every piece is built exactly once.
            public ChunkLayout Extract(int cx, int cz)
            {
                var L = new ChunkLayout();
                int lx0 = cx * ChunkCells - dx * D, lz0 = cz * ChunkCells - dz * D;
                bool Mine(int x, int z) => x >= lx0 && x < lx0 + ChunkCells && z >= lz0 && z < lz0 + ChunkCells;
                var me = new Vector2Int(cx, cz);
                bool MinePos(Vector3 p) => ChunkStreamer.ChunkOf(p) == me;

                for (int x = lx0; x < lx0 + ChunkCells; x++)
                for (int z = lz0; z < lz0 + ChunkCells; z++)
                {
                    int id = zone[x, z];
                    if (id == SolidZone) continue;
                    var zz = zones[id];
                    float h = HeightAt(x, z);
                    var area = new Rect(origin.x + x * C, origin.z + z * C, C, C);
                    if (pit[x, z]) PitShaft(L, area, -zz.Sunk, zz.Floor, zz.Wall, zz.FloorRot);
                    else if (!hole[x, z]) L.Quads.Add(new SurfaceQuad { Area = area, Height = -zz.Sunk, FacingUp = true, Rotated = zz.FloorRot, Surface = zz.Floor });
                    if (!zz.Shaft)
                    {
                        L.Quads.Add(new SurfaceQuad { Area = area, Height = h, FacingUp = false, Surface = zz.NoCeiling ? new SurfaceRef(Surface.Void) : zz.Ceiling });
                        // Seen from the floor above (down a stairwell, balcony or pitfall) a ceiling must still look solid.
                        if (HasFloors(theme) && floor < TopFloor) L.Quads.Add(new SurfaceQuad { Area = area, Height = h + 0.01f, FacingUp = true, Surface = zz.Ceiling, Visual = true });
                    }
                    // A water value alone isn't enough: shafts, pits and holes have no floor to flood, and a
                    // flooded room that was later raised would put the surface under its own tiles.
                    float floorY = -zz.Sunk;
                    if (zz.Water != 0f && !pit[x, z] && !hole[x, z] && !zz.Shaft
                        && !float.IsNaN(zz.Water) && !float.IsInfinity(zz.Water)
                        && zz.Water > floorY + 0.01f)
                    {
                        bool deep = zz.Sunk > 0.5f;
                        (deep ? L.WaterDeep : L.Water).Add(new SurfaceQuad { Area = area, Height = zz.Water, FacingUp = true });
                        // Caustics sit a centimetre above the tile, below the surface. Deep basins get the
                        // dimmer copy so the water reads as deep instead of merely dark.
                        (deep ? L.WaterBedDeep : L.WaterBed).Add(new SurfaceQuad { Area = area, Height = floorY + 0.012f, FacingUp = true });
                    }
                    Soffit(L, x, z, x - 1, z, Key(x, z, x, z + 1), true);
                    Soffit(L, x, z, x, z - 1, Key(x, z, x + 1, z), false);
                    FloorStep(L, x, z, x - 1, z, Key(x, z, x, z + 1), true);
                    FloorStep(L, x, z, x, z - 1, Key(x, z, x + 1, z), false);
                    BorderFace(L, x, z, h);
                    Rafters(L, x, z, h, zz);
                }

                foreach (var kv in edges)
                {
                    Decode(kv.Key, out int x, out int z, out _);
                    if (Mine(x, z)) EmitEdge(L.Boxes, kv.Key, kv.Value, true);
                }

                for (int x = lx0; x < lx0 + ChunkCells; x++)
                for (int z = lz0; z < lz0 + ChunkCells; z++)
                    EmitPost(L, x, z);

                foreach (var r in solids) EmitSolid(L, r, lx0, lz0);
                foreach (var b in extraBoxes) if (MinePos(b.Box.center)) L.Boxes.Add(b);
                bool InVoid(Vector3 p) { var c = CellOf(p); return Void(c.x, c.y); }
                foreach (var (box, zid) in hangers)
                    if (MinePos(box.center) && !InVoid(box.center))
                        L.Boxes.Add(SurfaceBox.Uniform(box, zones[zid].Wall, BoxMeshBuilder.Faces.Sides | BoxMeshBuilder.Faces.NegY, false));
                foreach (var (box, zid) in pillars)
                    if (MinePos(box.center) && !InVoid(box.center))
                    {
                        // Never taller than the ceiling it holds up (copy drift lowers ceilings; seen from above it poked through).
                        var pc = CellOf(box.center);
                        var b = box;
                        if (pc.x >= 0 && pc.y >= 0 && pc.x < D && pc.y < D && b.max.y > HeightAt(pc.x, pc.y)) b.SetMinMax(b.min, new Vector3(b.max.x, HeightAt(pc.x, pc.y), b.max.z));
                        L.Boxes.Add(SurfaceBox.Uniform(b, zid >= 0 ? zones[zid].Wall : new SurfaceRef(Surface.Wall), BoxMeshBuilder.Faces.Sides));
                    }
                for (int i = 0; i < props.Count; i++)
                    if (!removedProps.Contains(i) && MinePos(props[i].Pose.GetColumn(3))
                        && (!InVoid(props[i].Pose.GetColumn(3)) || props[i].Kind == PropKind.WallLadder || props[i].Kind == PropKind.WallLamp)) L.Props.Add(props[i]);   // these belong in their shafts
                for (int i = 0; i < blockers.Count; i++)
                    if (!removedBlockers.Contains(i) && MinePos(blockers[i].center)) L.Blockers.Add(blockers[i]);
                foreach (var kv in edges)
                {
                    if (!IsOpening(kv.Value.Kind)) continue;
                    Decode(kv.Key, out int x, out int z, out _);
                    if (Mine(x, z)) L.Openings.Add(PassageBounds(kv.Key));
                }
                foreach (var e in exits) if (MinePos(e.Trigger.center) && (e.Kind == ExitKind.Hatch || !InVoid(e.Trigger.center))) L.Exits.Add(e);
                foreach (var p in pits) if (MinePos(p.center)) L.Pits.Add(p);
                foreach (var p in panels) if (MinePos(p.Position)) L.Panels.Add(p);
                foreach (var s in slots) if (MinePos(s.Pose.GetColumn(3)) && !InVoid(s.Pose.GetColumn(3))) L.Curiosities.Add(s);
                foreach (var r in readables) if (MinePos(r.Pose.GetColumn(3))) L.Readables.Add(r);
                foreach (var b in boards) if (MinePos(b.Pose.GetColumn(3))) L.Boards.Add(b);
                foreach (var r in damp) if (MinePos(new Vector3(r.center.x, 0f, r.center.y))) L.Damp.Add(new SurfaceQuad { Area = r, Height = 0.004f, FacingUp = true });
                foreach (var p in planks) if (MinePos(p.Pose.GetColumn(3))) L.Planks.Add(p);
                foreach (var s in slopes) if (MinePos((s.A + s.B) / 2f)) L.Slopes.Add(s);
                foreach (var s in specials) if (MinePos(s.Pose.GetColumn(3))) L.Specials.Add(s);
                return L;
            }

            Zone ZoneAt(int x, int z) => OpenCell(x, z) ? zones[zone[x, z]] : null;

            SurfaceRef WallRef(int x, int z)
            {
                var zz = ZoneAt(x, z);
                return zz != null ? zz.Wall : new SurfaceRef(Surface.Wall, x < 0 || z < 0 || x >= D || z >= D ? 0 : palette[0]);
            }

            /// Where two cells meet without a full wall and their ceilings differ, a drywall step faces the taller one.
            void Soffit(ChunkLayout L, int ax, int az, int bx, int bz, int key, bool west)
            {
                if (bx < 0 || bz < 0 || !OpenCell(bx, bz) || !OpenCell(ax, az)) return;
                bool shaft = IsShaft(ax, az) || IsShaft(bx, bz);   // headers over openings stop at the room's ceiling, not the shaft's
                if (!shaft && edges.TryGetValue(key, out var e) && e.Kind != EdgeKind.LowWall) return;   // walls and headers already reach the top
                float ha = HeightAt(ax, az), hb = HeightAt(bx, bz);
                if (Mathf.Abs(ha - hb) < 0.01f) return;
                float lo = Mathf.Min(ha, hb), hi = Mathf.Max(ha, hb);
                bool aTaller = ha > hb;
                var face = WallRef(aTaller ? ax : bx, aTaller ? az : bz);
                Bounds b;
                BoxMeshBuilder.Faces faces;
                if (west)
                {
                    b = new Bounds(origin + new Vector3(ax * C, (lo + hi) / 2, (az + 0.5f) * C), new Vector3(0.02f, hi - lo, C));
                    faces = aTaller ? BoxMeshBuilder.Faces.PosX : BoxMeshBuilder.Faces.NegX;
                }
                else
                {
                    b = new Bounds(origin + new Vector3((ax + 0.5f) * C, (lo + hi) / 2, az * C), new Vector3(C, hi - lo, 0.02f));
                    faces = aTaller ? BoxMeshBuilder.Faces.PosZ : BoxMeshBuilder.Faces.NegZ;
                }
                L.Boxes.Add(SurfaceBox.Uniform(b, face, faces, false));
            }

            /// Where the floor drops (a sunken room, the pool basin) with no wall, the drop gets a face.
            void FloorStep(ChunkLayout L, int ax, int az, int bx, int bz, int key, bool west)
            {
                if (bx < 0 || bz < 0 || !OpenCell(bx, bz) || !OpenCell(ax, az)) return;
                float sa = SunkAt(ax, az), sb = SunkAt(bx, bz);
                if (poolStairMouths.Contains(key) || Mathf.Abs(sa - sb) < 0.01f) return;
                if (edges.TryGetValue(key, out var e) && Blocking(e.Kind) && e.Kind != EdgeKind.LowWall) return;   // the wall already reaches down
                bool aDeeper = sa > sb;
                float lo = -Mathf.Max(sa, sb), hi = -Mathf.Min(sa, sb);
                var face = WallRef(aDeeper ? ax : bx, aDeeper ? az : bz);
                Bounds b;
                BoxMeshBuilder.Faces faces;
                if (west)
                {
                    b = new Bounds(origin + new Vector3(ax * C, (lo + hi) / 2, (az + 0.5f) * C), new Vector3(0.02f, hi - lo, C));
                    faces = aDeeper ? BoxMeshBuilder.Faces.PosX : BoxMeshBuilder.Faces.NegX;
                }
                else
                {
                    b = new Bounds(origin + new Vector3((ax + 0.5f) * C, (lo + hi) / 2, az * C), new Vector3(C, hi - lo, 0.02f));
                    faces = aDeeper ? BoxMeshBuilder.Faces.PosZ : BoxMeshBuilder.Faces.NegZ;
                }
                L.Boxes.Add(SurfaceBox.Uniform(b, face, faces, false));
            }

            /// Neighbouring districts may have different ceiling heights at their shared border. Each side covers the
            /// gap above its own ceiling with a face pointing at the neighbour; above the neighbour's ceiling it is hidden.
            void BorderFace(ChunkLayout L, int x, int z, float h)
            {
                float top = ds.wallHeight + 0.2f;
                if (h >= top) return;
                var face = WallRef(x, z);
                void Add(Bounds b, BoxMeshBuilder.Faces f) => L.Boxes.Add(SurfaceBox.Uniform(b, face, f, false));
                if (x == 0 && !edges.ContainsKey(Key(0, z, 0, z + 1)))
                    Add(new Bounds(origin + new Vector3(0f, (h + top) / 2, (z + 0.5f) * C), new Vector3(0.02f, top - h, C)), BoxMeshBuilder.Faces.NegX);
                if (x == D - 1 && !edges.ContainsKey(Key(D, z, D, z + 1)))
                    Add(new Bounds(origin + new Vector3(D * C, (h + top) / 2, (z + 0.5f) * C), new Vector3(0.02f, top - h, C)), BoxMeshBuilder.Faces.PosX);
                if (z == 0 && !edges.ContainsKey(Key(x, 0, x + 1, 0)))
                    Add(new Bounds(origin + new Vector3((x + 0.5f) * C, (h + top) / 2, 0f), new Vector3(C, top - h, 0.02f)), BoxMeshBuilder.Faces.NegZ);
                if (z == D - 1 && !edges.ContainsKey(Key(x, D, x + 1, D)))
                    Add(new Bounds(origin + new Vector3((x + 0.5f) * C, (h + top) / 2, D * C), new Vector3(C, top - h, 0.02f)), BoxMeshBuilder.Faces.PosZ);
            }

            /// Half the size of the post at a vertex (0 = no post), and whether only low walls meet there.
            float PostHalf(int vx, int vz, out bool low)
            {
                float half = 0f;
                bool onlyLow = true;
                bool w = false, e = false, s = false, n = false;
                void Check(int key, ref bool full)
                {
                    if (!edges.TryGetValue(key, out var edge)) return;
                    half = Mathf.Max(half, (edge.Thick ? ThickWall : T) / 2);
                    if (edge.Kind != EdgeKind.LowWall) onlyLow = false;
                    full = edge.Kind == EdgeKind.Wall || edge.Kind == EdgeKind.Run;
                }
                if (vx > 0) Check(Key(vx - 1, vz, vx, vz), ref w);
                if (vx < D) Check(Key(vx, vz, vx + 1, vz), ref e);
                if (vz > 0) Check(Key(vx, vz - 1, vx, vz), ref s);
                if (vz < D) Check(Key(vx, vz, vx, vz + 1), ref n);
                // Pilasters: a column bump partway along a long straight wall.
                bool straight = (w && e && !s && !n) || (s && n && !w && !e);
                if (straight && pilasters && !OnBorder(vx, vz) && WorldHash.Value(seed, dx * D + vx, dz * D + vz, 131) < 0.3f)
                {
                    var zz = ZoneAt(Mathf.Min(vx, D - 1), Mathf.Min(vz, D - 1));
                    if (zz != null && zz.Pilasters) half = 0.3f;
                }
                low = onlyLow;
                return half;
            }

            void EmitPost(ChunkLayout L, int vx, int vz)
            {
                float half = PostHalf(vx, vz, out bool low);
                if (half <= 0f) return;
                float top = low ? LowWallHeight : ds.wallHeight, bottom = 0f;
                for (int ix = vx - 1; ix <= vx; ix++)
                for (int iz = vz - 1; iz <= vz; iz++)
                {
                    bottom = Mathf.Min(bottom, -SunkAt(ix, iz));
                    if (low && IsHole(ix, iz)) top = RailHeight;
                    if (!low && IsShaft(ix, iz)) top = FloorHeight - 0.01f;
                }
                var c = origin + new Vector3(vx * C, (top + bottom) / 2, vz * C);
                SurfaceRef Q(int ax, int az, int bx, int bz) => OpenCell(ax, az) ? WallRef(ax, az) : WallRef(bx, bz);
                var trimRef = new SurfaceRef(Surface.Trim, trim);
                L.Boxes.Add(new SurfaceBox
                {
                    Box = new Bounds(c, new Vector3(half * 2, top - bottom, half * 2)),
                    Faces = BoxMeshBuilder.Faces.Sides | (low ? BoxMeshBuilder.Faces.PosY : 0),
                    PosX = Q(vx, vz, vx, vz - 1),
                    NegX = Q(vx - 1, vz, vx - 1, vz - 1),
                    PosZ = Q(vx, vz, vx - 1, vz),
                    NegZ = Q(vx, vz - 1, vx - 1, vz - 1),
                    PosY = trimRef,
                    NegY = trimRef,
                    Solid = true,
                });
            }

            void EmitSolid(ChunkLayout L, RectInt r, int lx0, int lz0)
            {
                int x0 = Mathf.Max(r.xMin, lx0), x1 = Mathf.Min(r.xMax, lx0 + ChunkCells);
                int z0 = Mathf.Max(r.yMin, lz0), z1 = Mathf.Min(r.yMax, lz0 + ChunkCells);
                if (x0 >= x1 || z0 >= z1) return;
                float h = T / 2, top = ds.wallHeight;
                float ax = x0 * C - (x0 == r.xMin ? h : 0f), bx = x1 * C + (x1 == r.xMax ? h : 0f);
                float az = z0 * C - (z0 == r.yMin ? h : 0f), bz = z1 * C + (z1 == r.yMax ? h : 0f);
                var box = new Bounds(origin + new Vector3((ax + bx) / 2, top / 2, (az + bz) / 2), new Vector3(bx - ax, top, bz - az));
                BoxMeshBuilder.Faces faces = 0;
                if (x1 == r.xMax) faces |= BoxMeshBuilder.Faces.PosX;
                if (x0 == r.xMin) faces |= BoxMeshBuilder.Faces.NegX;
                if (z1 == r.yMax) faces |= BoxMeshBuilder.Faces.PosZ;
                if (z0 == r.yMin) faces |= BoxMeshBuilder.Faces.NegZ;
                int mx = (r.xMin + r.xMax) / 2, mz = (r.yMin + r.yMax) / 2;
                L.Boxes.Add(new SurfaceBox
                {
                    Box = box,
                    Faces = faces,
                    PosX = WallRef(r.xMax, mz),
                    NegX = WallRef(r.xMin - 1, mz),
                    PosZ = WallRef(mx, r.yMax),
                    NegZ = WallRef(mx, r.yMin - 1),
                    Solid = true,
                });
                // Baseboards along the visible faces.
                void Strip(Vector3 center, Vector3 size, BoxMeshBuilder.Faces f, Zone zz)
                {
                    if (zz == null || !zz.Baseboard) return;
                    L.Boxes.Add(SurfaceBox.Uniform(new Bounds(center, size), zz.Trim, f | BoxMeshBuilder.Faces.PosY, false));
                }
                var o = origin;
                if ((faces & BoxMeshBuilder.Faces.PosX) != 0)
                    Strip(o + new Vector3(bx + 0.006f, 0.05f, (az + bz) / 2), new Vector3(0.012f, 0.1f, bz - az), BoxMeshBuilder.Faces.PosX, ZoneAt(r.xMax, mz));
                if ((faces & BoxMeshBuilder.Faces.NegX) != 0)
                    Strip(o + new Vector3(ax - 0.006f, 0.05f, (az + bz) / 2), new Vector3(0.012f, 0.1f, bz - az), BoxMeshBuilder.Faces.NegX, ZoneAt(r.xMin - 1, mz));
                if ((faces & BoxMeshBuilder.Faces.PosZ) != 0)
                    Strip(o + new Vector3((ax + bx) / 2, 0.05f, bz + 0.006f), new Vector3(bx - ax, 0.1f, 0.012f), BoxMeshBuilder.Faces.PosZ, ZoneAt(mx, r.yMax));
                if ((faces & BoxMeshBuilder.Faces.NegZ) != 0)
                    Strip(o + new Vector3((ax + bx) / 2, 0.05f, az - 0.006f), new Vector3(bx - ax, 0.1f, 0.012f), BoxMeshBuilder.Faces.NegZ, ZoneAt(mx, r.yMin - 1));
            }

            /// One wall edge: a full wall, low partition, cased doorway, wide opening or arch, with trim.
            void EmitEdge(List<SurfaceBox> into, int key, Edge e, bool trims)
            {
                if (e.Kind == EdgeKind.Flicker) return;   // the exit renders its own flickering wall
                Decode(key, out int x, out int z, out int dir);
                int x1 = dir == 0 ? x + 1 : x, z1 = dir == 0 ? z : z + 1;
                float ha = Mathf.Max(T / 2, PostHalf(x, z, out _)), hb = Mathf.Max(T / 2, PostHalf(x1, z1, out _));
                var start = origin + new Vector3(x * C, 0f, z * C);
                var along = dir == 0 ? Vector3.right : Vector3.forward;
                var normal = dir == 0 ? Vector3.forward : Vector3.right;
                float t = e.Thick ? ThickWall : T, top = ds.wallHeight;
                Sides(key, out var pc, out var nc);
                float floorY = -Mathf.Max(SunkAt(pc.x, pc.y), SunkAt(nc.x, nc.y));
                if (IsShaft(pc.x, pc.y) || IsShaft(nc.x, nc.y)) top = FloorHeight - 0.01f;
                var posRef = WallRef(pc.x, pc.y);
                var negRef = WallRef(nc.x, nc.y);
                var pz = ZoneAt(pc.x, pc.y);
                var nz = ZoneAt(nc.x, nc.y);
                var trimRef = pz?.Trim ?? nz?.Trim ?? new SurfaceRef(Surface.Trim, trim);
                var longFaces = dir == 0 ? BoxMeshBuilder.Faces.PosZ | BoxMeshBuilder.Faces.NegZ : BoxMeshBuilder.Faces.PosX | BoxMeshBuilder.Faces.NegX;
                var capFaces = dir == 0 ? BoxMeshBuilder.Faces.PosX | BoxMeshBuilder.Faces.NegX : BoxMeshBuilder.Faces.PosZ | BoxMeshBuilder.Faces.NegZ;

                void Piece(float from, float to, float y0, float y1, BoxMeshBuilder.Faces extra)
                {
                    if (y0 < 0.001f) y0 = floorY;   // pieces standing on the floor reach a sunken floor too
                    if (to - from < 0.001f || y1 - y0 < 0.001f) return;
                    var c = start + along * ((from + to) / 2) + Vector3.up * ((y0 + y1) / 2);
                    float len = to - from;
                    var size = dir == 0 ? new Vector3(len, y1 - y0, t) : new Vector3(t, y1 - y0, len);
                    var b = new SurfaceBox { Box = new Bounds(c, size), Faces = longFaces | extra, PosY = trimRef, NegY = trimRef, Solid = true };
                    if (dir == 0) { b.PosZ = posRef; b.NegZ = negRef; b.PosX = trimRef; b.NegX = trimRef; }
                    else { b.PosX = posRef; b.NegX = negRef; b.PosZ = trimRef; b.NegZ = trimRef; }
                    into.Add(b);
                }

                void Strip(float from, float to, float y0, float y1, float depth, Zone side, float sign)
                {
                    if (to - from < 0.001f) return;
                    if (side != null) { y0 -= side.Sunk; y1 -= side.Sunk; }   // baseboards follow a sunken floor
                    var c = start + along * ((from + to) / 2) + Vector3.up * ((y0 + y1) / 2) + normal * (sign * (t / 2 + depth / 2));
                    float len = to - from;
                    var size = dir == 0 ? new Vector3(len, y1 - y0, depth) : new Vector3(depth, y1 - y0, len);
                    into.Add(SurfaceBox.Uniform(new Bounds(c, size), side != null ? side.Trim : trimRef, BoxMeshBuilder.Faces.All & ~BoxMeshBuilder.Faces.NegY, false));
                }

                void Trims(float from, float to)
                {
                    if (!trims) return;
                    foreach (var (side, sign) in new[] { (pz, 1f), (nz, -1f) })
                    {
                        if (side == null) continue;
                        if (side.Baseboard) Strip(from, to, 0f, 0.1f, 0.012f, side, sign);
                        if (side.ChairRail) Strip(from, to, 0.88f, 0.93f, 0.02f, side, sign);
                    }
                }

                switch (e.Kind)
                {
                    case EdgeKind.Wall:
                    case EdgeKind.Run:
                    {
                        // Copy drift: a wall that stops 40 cm short of the ceiling, so you see over it into the next room.
                        float wallTop = e.Short ? Mathf.Min(HeightAt(Mathf.Clamp(pc.x, 0, D - 1), Mathf.Clamp(pc.y, 0, D - 1)),
                                                            HeightAt(Mathf.Clamp(nc.x, 0, D - 1), Mathf.Clamp(nc.y, 0, D - 1))) - 0.4f : top;
                        int before = into.Count;
                        Piece(ha, C - hb, 0f, wallTop, e.Short ? BoxMeshBuilder.Faces.PosY : 0);
                        if (e.Doubled && into.Count > before)   // the same wall twice, a hair apart: the surfaces fight and shimmer
                        {
                            var twin = into[before];
                            twin.Box.center += normal * 0.012f + along * 0.02f;
                            twin.Solid = false;
                            into.Add(twin);
                        }
                        Trims(ha, C - hb);
                        break;
                    }
                    case EdgeKind.LowWall:   // railings around an opening are lower, so you can look over them
                        Piece(ha, C - hb, 0f, IsHole(pc.x, pc.y) || IsHole(nc.x, nc.y) ? RailHeight : LowWallHeight, BoxMeshBuilder.Faces.PosY);
                        Trims(ha, C - hb);
                        break;
                    case EdgeKind.Doorway:
                    {
                        float d0 = (C - DoorWidth) / 2, d1 = d0 + DoorWidth;
                        float door = e.Header > 0f ? e.Header : DoorHeight;   // copy drift can misjudge the height
                        Piece(ha, d0, 0f, top, capFaces);
                        Piece(d1, C - hb, 0f, top, capFaces);
                        Piece(d0, d1, door, top, BoxMeshBuilder.Faces.NegY);
                        Trims(ha, d0);
                        Trims(d1, C - hb);
                        if (trims)
                            foreach (var (side, sign) in new[] { (pz, 1f), (nz, -1f) })
                            {
                                if (side == null) continue;
                                Strip(d0 - 0.07f, d0, 0f, door + 0.07f, 0.02f, side, sign);
                                Strip(d1, d1 + 0.07f, 0f, door + 0.07f, 0.02f, side, sign);
                                Strip(d0 - 0.07f, d1 + 0.07f, door, door + 0.07f, 0.02f, side, sign);
                            }
                        break;
                    }
                    case EdgeKind.Wide:
                        Piece(ha, C - hb, WideHeight, top, BoxMeshBuilder.Faces.NegY);
                        break;
                    case EdgeKind.Arcade:   // a half wall with an arch above it, as in the second photo
                        Piece(ha, C - hb, 0f, 1.1f, BoxMeshBuilder.Faces.PosY);
                        Trims(ha, C - hb);
                        goto case EdgeKind.Arch;
                    case EdgeKind.Arch:
                    {
                        float w = C - hb - ha, mid = (ha + C - hb) / 2, rise = Mathf.Min(0.6f, w / 2);
                        const int slices = 8;
                        for (int i = 0; i < slices; i++)
                        {
                            float u0 = ha + w * i / slices, u1 = ha + w * (i + 1) / slices, nx = ((u0 + u1) / 2 - mid) / (w / 2);
                            Piece(u0, u1, 2.0f + rise * Mathf.Sqrt(Mathf.Max(0f, 1f - nx * nx)), top, BoxMeshBuilder.Faces.NegY);
                        }
                        break;
                    }
                }
            }
        }
    }
}
