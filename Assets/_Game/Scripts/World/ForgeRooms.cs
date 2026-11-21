using System;
using System.Collections.Generic;
using UnityEngine;

namespace Backrooms.World
{
    // ---------------------------------------------------------------------------------------------
    // Hand-made rooms written as data (JSON in Assets/_Game/Resources/Forge/approved), built into Level 0
    // by the district generator: about one district in ten gets one.
    // ---------------------------------------------------------------------------------------------

    [Serializable] public sealed class ForgeZone { public int x, z, w, d, paper = 9, carpet; public float height = 2.7f; public string lights = "Grid"; public bool dark; }
    [Serializable] public sealed class ForgeWall { public int x0, z0, x1, z1; public string kind = "wall"; }
    [Serializable] public sealed class ForgeRect { public int x, z, w, d; }
    [Serializable] public sealed class ForgeProp { public string kind; public float x, z, yaw; }
    /// Something on a wall: the edge from (x, z) one cell along "x" or "z", on the +1 or -1 side.
    [Serializable] public sealed class ForgeWallItem { public int x, z, side = 1; public string along = "x"; public string title, text; public float height = 1.5f; }
    [Serializable] public sealed class ForgeNote { public float x, z; public string title, text; }

    [Serializable]
    public sealed class ForgeRoom
    {
        public string name, title, description;
        public int width = 12, depth = 12;   // cells (2 m each)
        public List<ForgeZone> zones = new List<ForgeZone>();
        public List<ForgeWall> walls = new List<ForgeWall>();
        public List<ForgeRect> solids = new List<ForgeRect>();
        public List<ForgeProp> props = new List<ForgeProp>();
        public List<ForgeWallItem> writings = new List<ForgeWallItem>();
        public List<ForgeWallItem> paintings = new List<ForgeWallItem>();
        public List<ForgeNote> notes = new List<ForgeNote>();
    }

    public enum ReadableKind { Writing, Painting, Note }

    public struct ReadableSpec
    {
        public ReadableKind Kind;
        public Matrix4x4 Pose;   // writings and paintings: on the wall face, +z out of the wall; notes: on the floor
        public string Title, Text;
    }

    public static class ForgeLibrary
    {
        const float ApprovedChance = 0.1f;
        static List<ForgeRoom> approved;

        public static void Reload() { approved = null; DistrictLayout.ClearCache(); }

        static void Load()
        {
            if (approved != null) return;
            approved = Read("Forge/approved");
        }

        static List<ForgeRoom> Read(string folder)
        {
            var list = new List<ForgeRoom>();
            foreach (var asset in Resources.LoadAll<TextAsset>(folder))
            {
                try
                {
                    var room = JsonUtility.FromJson<ForgeRoom>(asset.text);
                    if (room == null) continue;
                    room.name = asset.name;
                    list.Add(room);
                }
                catch (Exception e) { Debug.LogWarning($"[Forge] skipped {asset.name}: {e.Message}"); }
            }
            list.Sort((a, b) => string.CompareOrdinal(a.name, b.name));   // stable order, so the world is too
            return list;
        }

        /// The hand-made room built in this Level 0 district, if any.
        public static ForgeRoom RoomAt(uint seed, int dx, int dz, LevelTheme theme)
        {
            if (theme == null || theme.id != "0") return null;
            Load();
            if (approved.Count == 0 || Mathf.Abs(dx) + Mathf.Abs(dz) < 2) return null;
            if (WorldHash.Value(seed, dx, dz, 0xF06E) >= ApprovedChance) return null;
            return approved[(int)(WorldHash.Hash(seed, dx, dz, 0xF06F) % (uint)approved.Count)];
        }

    }

    public static partial class DistrictLayout
    {
        sealed partial class Plan
        {
            readonly List<ReadableSpec> readables = new List<ReadableSpec>();

            static EdgeKind ForgeKind(string k)
            {
                switch ((k ?? "wall").ToLowerInvariant())
                {
                    case "low": return EdgeKind.LowWall;
                    case "arcade": return EdgeKind.Arcade;
                    case "doorway": case "door": return EdgeKind.Doorway;
                    case "arch": return EdgeKind.Arch;
                    case "wide": case "opening": return EdgeKind.Wide;
                    default: return EdgeKind.Wall;
                }
            }

            /// Builds a forged room in the middle of the district, walled, with a door on every side.
            void BuildForged(ForgeRoom room)
            {
                int w = Mathf.Clamp(room.width, 4, D - 6), d = Mathf.Clamp(room.depth, 4, D - 6);
                int ox = (D - w) / 2, oz = (D - d) / 2;
                var all = new RectInt(ox, oz, w, d);
                RectInt Clip(int x, int z, int cw, int cd)
                {
                    int x0 = Mathf.Clamp(x, 0, w), z0 = Mathf.Clamp(z, 0, d);
                    int x1 = Mathf.Clamp(x + cw, 0, w), z1 = Mathf.Clamp(z + cd, 0, d);
                    return new RectInt(ox + x0, oz + z0, x1 - x0, z1 - z0);
                }

                // Zones: the first one also covers the whole room, so no cell is left unpainted.
                var list = room.zones != null && room.zones.Count > 0 ? room.zones : new List<ForgeZone> { new ForgeZone { w = w, d = d } };
                for (int i = 0; i < list.Count; i++)
                {
                    var f = list[i];
                    var r = i == 0 ? all : Clip(f.x, f.z, f.w, f.d);
                    if (r.width <= 0 || r.height <= 0) continue;
                    int id = NewZone(ZoneKind.Origin, r);
                    var z = zones[id];
                    z.Wall = new SurfaceRef(Surface.Wall, Mathf.Clamp(f.paper, 0, wallpapers - 1));
                    z.Floor = new SurfaceRef(Surface.Floor, Mathf.Clamp(f.carpet, 0, carpets - 1));
                    z.Ceiling = new SurfaceRef(Surface.Ceiling, 0);
                    z.Height = Mathf.Clamp(f.height, 2.3f, 6f);
                    bool none = string.Equals(f.lights, "none", StringComparison.OrdinalIgnoreCase);
                    z.Lights = Enum.TryParse(f.lights, true, out LightLayout layout) ? layout : LightLayout.Grid;
                    z.Dead = f.dark || none ? 1f : 0f;
                    z.Baseboard = true; z.ChairRail = f.paper % 2 == 0; z.Pilasters = false;
                    Paint(r, id);
                }

                // Walls: openings first, so the walls leave them alone; walls never seal anything off.
                if (room.walls != null)
                {
                    foreach (bool openings in new[] { true, false })
                        foreach (var wall in room.walls)
                        {
                            var kind = ForgeKind(wall.kind);
                            if (IsOpening(kind) != openings) continue;
                            int x0 = Mathf.Clamp(Mathf.Min(wall.x0, wall.x1), 0, w), x1 = Mathf.Clamp(Mathf.Max(wall.x0, wall.x1), 0, w);
                            int z0 = Mathf.Clamp(Mathf.Min(wall.z0, wall.z1), 0, d), z1 = Mathf.Clamp(Mathf.Max(wall.z0, wall.z1), 0, d);
                            bool alongX = z0 == z1;
                            if (!alongX && x0 != x1) continue;   // only straight lines
                            for (int i = alongX ? x0 : z0; i < (alongX ? x1 : z1); i++)
                            {
                                int ax = ox + (alongX ? i : x0), az = oz + (alongX ? z0 : i);
                                int bx = alongX ? ax + 1 : ax, bz = alongX ? az : az + 1;
                                if (openings) Opening(ax, az, bx, bz, kind);
                                else Wall(ax, az, bx, bz, kind, EdgeKind.Doorway, out _);
                            }
                        }
                }
                if (room.solids != null)
                    foreach (var s in room.solids) { var r = Clip(s.x, s.z, s.w, s.d); if (r.width > 0 && r.height > 0) Solid(r); }

                Opening(ox + w / 2, oz, ox + w / 2 + 1, oz, EdgeKind.Doorway);
                Opening(ox + w / 2, oz + d, ox + w / 2 + 1, oz + d, EdgeKind.Doorway);
                Opening(ox, oz + d / 2, ox, oz + d / 2 + 1, EdgeKind.Doorway);
                Opening(ox + w, oz + d / 2, ox + w, oz + d / 2 + 1, EdgeKind.Doorway);
                RoomWalls(all, 0, EdgeKind.Doorway);

                Vector3 At(float x, float z) => origin + new Vector3((ox + Mathf.Clamp(x, 0f, w)) * C, 0f, (oz + Mathf.Clamp(z, 0f, d)) * C);
                if (room.props != null)
                    foreach (var p in room.props)
                        if (Enum.TryParse(p.kind, true, out PropKind kind) && kind != PropKind.Pit)
                        {
                            var at = At(p.x, p.z);
                            int cx = Mathf.Clamp(Mathf.FloorToInt(ox + p.x), 0, D - 1), cz = Mathf.Clamp(Mathf.FloorToInt(oz + p.z), 0, D - 1);
                            props.Add(new PropSpec { Kind = kind, Pose = Matrix4x4.TRS(at, Quaternion.Euler(0f, p.yaw, 0f), Vector3.one), Ceiling = HeightAt(cx, cz) });
                        }
                void OnWall(List<ForgeWallItem> items, ReadableKind kind)
                {
                    if (items == null) return;
                    foreach (var it in items)
                    {
                        int x = ox + Mathf.Clamp(it.x, 0, w), z = oz + Mathf.Clamp(it.z, 0, d);
                        bool alongX = it.along != "z";
                        int key = alongX ? Key(x, z, x + 1, z) : Key(x, z, x, z + 1);
                        if (!edges.ContainsKey(key) || !Blocking(edges[key].Kind)) continue;   // only on real walls
                        if (!WallFrame(key, it.side >= 0, out var pose, out _)) continue;
                        readables.Add(new ReadableSpec { Kind = kind, Pose = pose * Matrix4x4.Translate(new Vector3(0f, Mathf.Clamp(it.height, 0.3f, 2.2f), 0f)), Title = it.title, Text = it.text });
                    }
                }
                OnWall(room.writings, ReadableKind.Writing);
                OnWall(room.paintings, ReadableKind.Painting);
                if (room.notes != null)
                    foreach (var n in room.notes)
                        readables.Add(new ReadableSpec { Kind = ReadableKind.Note, Pose = Matrix4x4.TRS(At(n.x, n.z), Quaternion.Euler(0f, (n.x * 37f + n.z * 91f) % 360f, 0f), Vector3.one), Title = n.title, Text = n.text });

                Reserve(new RectInt(all.xMin + 1, all.yMin + 1, all.width - 2, all.height - 2));
                Use(all);
            }
        }
    }
}
