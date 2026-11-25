using System;
using System.Collections.Generic;
using UnityEngine;

namespace Backrooms.World
{
    /// Graphics revamp Stage C: the shared kit that turns box-built rooms into built rooms on every level.
    /// - Wall corners get a small 45° cut (BoxMeshBuilder.AddBevelledBox) wherever both faces show.
    /// - Flat baseboard strips become the Blender skirting profile (KitBaseboard), in the trim's own material.
    /// - Acoustic tile ceilings (the "Ceiling*" materials, Level 0's family) get real T-bar rails on the texture's
    ///   0.5 m grid, stopping at light panels, plus sprinkler heads on a 3 m grid, the odd smoke detector, sagging
    ///   water-logged tiles and missing tiles. Models: Art/RevampKit.blend (build script Art/build_revampkit.py).
    public static class KitDressing
    {
        public const float Bevel = 0.02f;
        const float Tile = 0.5f;               // the ceiling texture's 6x6 grid repeats every 3 m
        const float RailWidth = 0.024f, RailDrop = 0.012f;

        /// Tall solid walls, blocks and pillars get bevelled corners; thin trims and low parts don't.
        public static bool Bevels(in SurfaceBox b) =>
            b.Solid && b.Box.size.y >= 1.2f && Mathf.Min(b.Box.size.x, b.Box.size.z) >= 0.08f;

        /// Which corners of `b` may be cut: not those another solid box touches (two boxes side by side, each drawing
        /// its end face, would otherwise show a dark groove down the join). Bits as AddBevelledBox.
        public static int FreeCorners(in SurfaceBox b, List<SurfaceBox> solids)
        {
            const float e = 0.012f;
            Vector3 lo = b.Box.min, hi = b.Box.max;
            int mask = 15;
            for (int i = 0; i < solids.Count; i++)
            {
                Vector3 olo = solids[i].Box.min, ohi = solids[i].Box.max;
                if (olo.x > hi.x + e || ohi.x < lo.x - e || olo.z > hi.z + e || ohi.z < lo.z - e) continue;   // far away
                if (olo == lo && ohi == hi) continue;                       // itself
                if (ohi.y <= lo.y + e || olo.y >= hi.y - e) continue;       // not beside it
                bool Touches(float x, float z) => x >= olo.x - e && x <= ohi.x + e && z >= olo.z - e && z <= ohi.z + e;
                if ((mask & 1) != 0 && Touches(hi.x, hi.z)) mask &= ~1;
                if ((mask & 2) != 0 && Touches(hi.x, lo.z)) mask &= ~2;
                if ((mask & 4) != 0 && Touches(lo.x, hi.z)) mask &= ~4;
                if ((mask & 8) != 0 && Touches(lo.x, lo.z)) mask &= ~8;
                if (mask == 0) break;
            }
            return mask;
        }

        /// If `b` is a flat baseboard strip (about 10 cm high, a centimetre deep, along a wall), adds the profiled
        /// skirting model in its place and returns true.
        public static bool Baseboard(BoxMeshBuilder into, in SurfaceBox b, Func<SurfaceRef, int> sub)
        {
            var s = b.Box.size;
            // (Chair rails along the walls match too; the same profile suits them.)
            if (b.Solid || s.y < 0.06f || s.y > 0.2f) return false;
            bool alongX = s.z <= 0.03f && s.x >= 0.3f, alongZ = s.x <= 0.03f && s.z >= 0.3f;
            if (!alongX && !alongZ) return false;
            var f = b.Faces;
            Vector3 a = b.Box.min, c = b.Box.max;
            Vector3 start; Quaternion rot; float length; SurfaceRef face;
            if (alongX && (f & BoxMeshBuilder.Faces.PosZ) != 0) { start = new Vector3(a.x, a.y, a.z); rot = Quaternion.identity; length = s.x; face = b.PosZ; }
            else if (alongX && (f & BoxMeshBuilder.Faces.NegZ) != 0) { start = new Vector3(c.x, a.y, c.z); rot = Quaternion.Euler(0f, 180f, 0f); length = s.x; face = b.NegZ; }
            else if (alongZ && (f & BoxMeshBuilder.Faces.PosX) != 0) { start = new Vector3(a.x, a.y, c.z); rot = Quaternion.Euler(0f, 90f, 0f); length = s.z; face = b.PosX; }
            else if (alongZ && (f & BoxMeshBuilder.Faces.NegX) != 0) { start = new Vector3(c.x, a.y, a.z); rot = Quaternion.Euler(0f, -90f, 0f); length = s.z; face = b.NegX; }
            else return false;
            if (!ModelLibrary.Has("KitBaseboard")) return false;
            int slot = sub(face);
            int pieces = Mathf.Max(1, Mathf.CeilToInt(length - 0.05f));   // ~1 m pieces keep the texture in meters
            float piece = length / pieces;
            var dir = rot * Vector3.right;
            // The model runs from x = -1 to 0 with its front towards +z; shift it to 0..1 and stretch to the piece.
            var toZero = Matrix4x4.Translate(Vector3.right);
            for (int i = 0; i < pieces; i++)
            {
                var pose = Matrix4x4.TRS(start + dir * (piece * i), rot, new Vector3(piece, s.y / 0.1f, 1f)) * toZero;
                ModelLibrary.AddMapped(into, "KitBaseboard", pose, _ => slot);
            }
            return true;
        }

        /// Rails, sprinklers, detectors and tile damage on every acoustic tile ceiling in the chunk.
        /// `rails` takes the structure's material slots (rails reuse the ceiling material, landing on its grid
        /// lines); `fixtures` uses the prop paints.
        public static void Ceilings(ChunkLayout layout, BoxMeshBuilder rails, BoxMeshBuilder fixtures,
                                    Func<SurfaceRef, int> sub, Func<SurfaceRef, Material> resolve, uint seed)
        {
            var panels = new List<Rect>();
            foreach (var p in layout.Panels)
            {
                Vector2 half = p.AlongX ? new Vector2(0.5f, 0.25f) : new Vector2(0.25f, 0.5f);
                panels.Add(new Rect(p.Position.x - half.x, p.Position.z - half.y, half.x * 2, half.y * 2));
            }
            foreach (var fx in layout.Fixtures)
                panels.Add(new Rect(fx.Position.x - 0.5f, fx.Position.z - 0.5f, 1f, 1f));

            var cut = new List<Vector2>();
            foreach (var q in layout.Quads)
            {
                if (q.FacingUp || q.Visual) continue;
                var mat = resolve(q.Surface);
                if (mat == null || !mat.name.StartsWith("Ceiling", StringComparison.Ordinal)) continue;
                var r = q.Area;
                float h = q.Height;
                int slot = sub(q.Surface);
                int floorKey = Mathf.RoundToInt(h * 10f);

                // Rails along Z at each grid x, then along X at each grid z, broken where a light panel sits.
                for (int k = Mathf.CeilToInt(r.xMin / Tile - 0.001f); k * Tile < r.xMax - 0.001f; k++)
                    Rail(rails, slot, true, k * Tile, r.yMin, r.yMax, h, panels, cut);
                for (int k = Mathf.CeilToInt(r.yMin / Tile - 0.001f); k * Tile < r.yMax - 0.001f; k++)
                    Rail(rails, slot, false, k * Tile, r.xMin, r.xMax, h, panels, cut);

                // Whole tiles inside this quad and clear of light panels.
                for (int ix = Mathf.CeilToInt(r.xMin / Tile - 0.001f); (ix + 1) * Tile <= r.xMax + 0.001f; ix++)
                for (int iz = Mathf.CeilToInt(r.yMin / Tile - 0.001f); (iz + 1) * Tile <= r.yMax + 0.001f; iz++)
                {
                    var tile = new Rect(ix * Tile, iz * Tile, Tile, Tile);
                    bool underPanel = false;
                    foreach (var p in panels) if (p.Overlaps(new Rect(tile.x + 0.02f, tile.y + 0.02f, Tile - 0.04f, Tile - 0.04f))) { underPanel = true; break; }
                    if (underPanel) continue;
                    var centre = new Vector3(tile.center.x, h, tile.center.y);
                    float roll = WorldHash.Value(seed, ix, iz, 0x7E11 + floorKey);
                    if (roll < 0.003f)
                    {
                        // A missing tile: the dark plenum above, framed by the rails.
                        fixtures.AddHorizontal(new Rect(tile.x + 0.012f, tile.y + 0.012f, Tile - 0.024f, Tile - 0.024f), h - 0.002f, false, false, (int)PropPaint.Void);
                        continue;
                    }
                    if (roll < 0.009f)
                    {
                        ModelLibrary.AddMapped(rails, "KitSagTile", Matrix4x4.Translate(centre), _ => slot);
                        continue;
                    }
                    if (Mod(ix, 6) == 3 && Mod(iz, 6) == 3)
                        ModelLibrary.Add(fixtures, WorldHash.Value(seed, ix, iz, 0x5D07) < 0.08f ? "KitSmokeDetector" : "KitSprinkler", Matrix4x4.Translate(centre));
                }
            }
        }

        static int Mod(int a, int m) => ((a % m) + m) % m;

        /// One rail at `line` (x when `alongZ`, else z) from `from` to `to`, minus the stretches over light panels.
        static void Rail(BoxMeshBuilder into, int slot, bool alongZ, float line, float from, float to, float h, List<Rect> panels, List<Vector2> cut)
        {
            cut.Clear();
            foreach (var p in panels)
            {
                float across0 = alongZ ? p.xMin : p.yMin, across1 = alongZ ? p.xMax : p.yMax;
                if (line <= across0 + 0.01f || line >= across1 - 0.01f) continue;   // a rail on the panel's edge stays
                cut.Add(alongZ ? new Vector2(p.yMin, p.yMax) : new Vector2(p.xMin, p.xMax));
            }
            cut.Sort((a, b) => a.x.CompareTo(b.x));
            float s = from;
            foreach (var c in cut)
            {
                if (c.x > s) Segment(into, slot, alongZ, line, s, Mathf.Min(c.x, to), h);
                s = Mathf.Max(s, c.y);
                if (s >= to) return;
            }
            if (s < to) Segment(into, slot, alongZ, line, s, to, h);
        }

        static void Segment(BoxMeshBuilder into, int slot, bool alongZ, float line, float a, float b, float h)
        {
            if (b - a < 0.02f) return;
            var size = alongZ ? new Vector3(RailWidth, RailDrop, b - a) : new Vector3(b - a, RailDrop, RailWidth);
            var centre = alongZ ? new Vector3(line, h - RailDrop / 2f, (a + b) / 2f) : new Vector3((a + b) / 2f, h - RailDrop / 2f, line);
            var faces = BoxMeshBuilder.Faces.NegY | (alongZ ? BoxMeshBuilder.Faces.PosX | BoxMeshBuilder.Faces.NegX : BoxMeshBuilder.Faces.PosZ | BoxMeshBuilder.Faces.NegZ);
            into.AddBox(new Bounds(centre, size), faces, slot);
        }
    }
}
