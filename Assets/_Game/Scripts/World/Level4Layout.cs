using System.Collections.Generic;
using UnityEngine;

namespace Backrooms.World
{
    /// Level 4 "Abandoned Office" (Backrooms Wiki, https://backrooms-wiki.wikidot.com/level-4 , CC BY-SA 3.0):
    /// an empty office building, mostly without furniture. Some rooms have windows; most are blacked out, and the
    /// few clear ones are traps. Water coolers, vending machines and fountains of almond water all over. Few
    /// entities. People were here: M.E.G.'s Base Omega, a small cult farming mold, a group of hoarders. Now only
    /// their rooms are left.
    ///
    /// District kinds: OfficeCubicles = office floors (corridors, offices, conference and break rooms), OpenPlan =
    /// open-plan floors, plus the shared Corridors archetype. Furniture is gone; its traces are not.
    public static partial class DistrictLayout
    {
        sealed partial class Plan
        {
            const float LureChance = 0.05f;   // of windows: the rest are blacked out

            static bool OfficeRoom(ZoneKind k) =>
                k == ZoneKind.Office || k == ZoneKind.Conference || k == ZoneKind.Open || k == ZoneKind.Cubicles || k == ZoneKind.Reception
                || k == ZoneKind.Room || k == ZoneKind.BreakRoom || k == ZoneKind.CopyRoom || k == ZoneKind.HotelRoom;

            // ---------- open-plan floors ----------

            /// A whole floor with the desks taken out: columns, carpet pressed into rows of footprints, cut floor
            /// boxes, a few offices built along it, very rarely a row of cubicles nobody cleared.
            void BuildOpenPlan()
            {
                var all = new RectInt(0, 0, D, D);
                int id = NewZone(ZoneKind.Open, all);
                var z = zones[id];
                z.Height = 2.8f;
                z.Lights = LightLayout.Grid;
                Paint(all, id);
                for (int x = 1; x < D - 1; x++)
                for (int zz = 1; zz < D - 1; zz++)
                    if (Mod(lx * D + x, 5) == 2 && Mod(lz * D + zz, 5) == 2 && Allowed(x, zz)) Pillar(x, zz, false, 0.6f);

                for (int k = rng.Range(2, 5); k > 0; k--)   // offices and meeting rooms built out from the floor
                {
                    int w = rng.Range(3, 6), d = rng.Range(3, 6), x0 = rng.Range(1, D - 1 - w), z0 = rng.Range(1, D - 1 - d);
                    var r = new RectInt(x0, z0, w, d);
                    bool ok = AllAllowed(new RectInt(x0 - 1, z0 - 1, w + 2, d + 2));
                    for (int x = x0 - 1; x <= x0 + w && ok; x++) for (int zz = z0 - 1; zz <= z0 + d && ok; zz++) if (used[x, zz] || zone[x, zz] != id) ok = false;
                    if (!ok) continue;
                    int rid = Room(r, rng.Value() < 0.6f ? ZoneKind.Office : ZoneKind.Conference, 1);
                    Furnish(rid);
                }

                bool keptRow = rng.Value() < 0.15f;
                for (int x = 1; x < D - 1; x++)
                for (int zz = 1; zz < D - 1; zz++)
                {
                    if (zone[x, zz] != id || used[x, zz] || !Allowed(x, zz)) continue;
                    int wx = lx * D + x, wz = lz * D + zz;
                    if (Mod(wz, 3) == 1 && Mod(wx, 2) == 0 && rng.Value() < 0.55f)
                        props.Add(new PropSpec { Kind = PropKind.CarpetDent, Variant = 0 + 3 * rng.Range(0, 4), Pose = Matrix4x4.TRS(CellCenter(x, zz), Quaternion.Euler(0f, (rng.Value() - 0.5f) * 4f, 0f), Vector3.one) });
                    else if (rng.Value() < 0.03f)
                        props.Add(new PropSpec { Kind = PropKind.FloorOutlet, Variant = rng.Range(0, 12), Pose = Matrix4x4.TRS(CellCenter(x, zz), Quaternion.Euler(0f, rng.Value() * 360f, 0f), Vector3.one) });
                }
                if (keptRow)   // one row of cubicles still standing, desks and chairs and all
                {
                    int zz = rng.Range(4, D - 6), x0 = rng.Range(2, D - 12);
                    for (int x = x0; x < x0 + 8; x++)
                    {
                        if (!FreeStanding(x, zz) || !Allowed(x, zz)) continue;
                        Wall(x, zz, x + 1, zz, EdgeKind.LowWall, null, out _);
                        if ((x - x0) % 2 == 0) Wall(x, zz, x, zz + 1, EdgeKind.LowWall, null, out _);
                        used[x, zz] = true;
                        Put(PropKind.Desk, CellCenter(x, zz) + new Vector3(0f, 0f, -0.9f), 0f, 0, z.Height, true, new Vector3(1.4f, 0.75f, 0.7f));
                        Put(PropKind.OfficeChair, CellCenter(x, zz) + new Vector3(0f, 0f, 0.1f), rng.Value() * 60f - 30f, 0, z.Height, false);
                    }
                }
                for (int i = rng.Range(1, 4); i > 0; i--) PlaceInZone(id, PropKind.OfficeChair, false);   // the lone chair
            }

            // ---------- traces instead of furniture ----------

            /// What Level 4 rooms have instead of furniture (called from Furnish): footprints in the carpet, cut cables,
            /// sometimes a chair; break rooms keep their water; one cubicle floor in eight was never cleared.
            void Level4Furnish(int id)
            {
                var z = zones[id];
                var r = z.Rect;
                int cells = r.width * r.height;
                void Dent(int x, int zz, int variant, float yaw)
                {
                    if (x < 0 || zz < 0 || x >= D || zz >= D || zone[x, zz] != id || used[x, zz]) return;
                    props.Add(new PropSpec { Kind = PropKind.CarpetDent, Variant = variant, Pose = Matrix4x4.TRS(CellCenter(x, zz) + new Vector3(Range(-0.2f, 0.2f), 0f, Range(-0.2f, 0.2f)), Quaternion.Euler(0f, yaw, 0f), Vector3.one) });
                }
                switch (z.Kind)
                {
                    case ZoneKind.Cubicles:
                        if (rng.Value() < 0.12f) { Cubicles(id); return; }
                        for (int zz = r.yMin + 1; zz < r.yMax; zz += 2)
                            for (int x = r.xMin; x < r.xMax; x++)
                                if (rng.Value() < 0.7f) Dent(x, zz, 3 * rng.Range(0, 4), 0f);
                        break;
                    case ZoneKind.Conference:   // the long table: a row of footprints down the middle, chairs either side
                    {
                        bool alongX = r.width >= r.height;
                        int mid = alongX ? r.yMin + r.height / 2 : r.xMin + r.width / 2;
                        for (int a = (alongX ? r.xMin : r.yMin) + 1; a < (alongX ? r.xMax : r.yMax) - 1; a++)
                            Dent(alongX ? a : mid, alongX ? mid : a, 0, alongX ? 0f : 90f);
                        if (rng.Value() < 0.3f) PlaceOnWall(id, PropKind.Whiteboard);
                        break;
                    }
                    case ZoneKind.BreakRoom:
                        if (WallSpotIn(id, out var bp)) Dispenser(bp, rng.Value() < 0.5f ? PropKind.VendingMachine : PropKind.WaterCooler);
                        if (rng.Value() < 0.5f && WallSpotIn(id, out var fp)) Dispenser(fp, PropKind.DrinkFountain);
                        if (rng.Value() < 0.4f) PlaceOnWall(id, PropKind.Fridge);
                        break;
                    default:
                        for (int i = rng.Range(1, cells / 6 + 2); i > 0; i--) Dent(rng.Range(r.xMin, r.xMax), rng.Range(r.yMin, r.yMax), rng.Range(0, 12), rng.Range(0, 4) * 90f);
                        break;
                }
                if (rng.Value() < 0.3f)
                {
                    int x = rng.Range(r.xMin, r.xMax), zz = rng.Range(r.yMin, r.yMax);
                    if (x >= 0 && zz >= 0 && x < D && zz < D && zone[x, zz] == id && !used[x, zz])
                        props.Add(new PropSpec { Kind = PropKind.FloorOutlet, Variant = rng.Range(0, 12), Pose = Matrix4x4.TRS(CellCenter(x, zz), Quaternion.Euler(0f, rng.Value() * 360f, 0f), Vector3.one) });
                }
                if (rng.Value() < 0.12f) PlaceInZone(id, PropKind.OfficeChair, false);
            }

            /// A free stretch of wall in a zone (its frame faces into the zone).
            bool WallSpotIn(int id, out Matrix4x4 pose)
            {
                var r = zones[id].Rect;
                for (int attempt = 0; attempt < 14; attempt++)
                {
                    int x = rng.Range(r.xMin, r.xMax), zz = rng.Range(r.yMin, r.yMax);
                    if (x < 0 || zz < 0 || x >= D || zz >= D || zone[x, zz] != id || used[x, zz] || !WallOfCell(x, zz, out pose)) continue;
                    used[x, zz] = true;
                    return true;
                }
                pose = default;
                return false;
            }

            /// A cooler, vending machine or fountain against a wall, and the spot you drink from.
            void Dispenser(Matrix4x4 wall, PropKind kind)
            {
                Vector3 at = wall.GetColumn(3);
                var c = CellOf(at + wall.MultiplyVector(Vector3.forward) * 0.5f);
                float h = HeightAt(Mathf.Clamp(c.x, 0, D - 1), Mathf.Clamp(c.y, 0, D - 1));
                props.Add(new PropSpec { Kind = kind, Variant = rng.Range(0, 12), Pose = wall, Ceiling = h });
                float depth = kind == PropKind.VendingMachine ? 0.95f : kind == PropKind.WaterCooler ? 0.45f : 0.4f;
                Special(SpecialKind.WaterSpot, wall * Matrix4x4.Translate(new Vector3(0f, 0f, depth / 2f)), new Vector3(kind == PropKind.VendingMachine ? 0.95f : 0.5f, kind == PropKind.DrinkFountain ? 1.1f : 1.8f, depth));
                if (kind == PropKind.VendingMachine)
                {
                    blockers.Add(LocalBox(wall, new Vector3(0f, 0.92f, 0.44f), new Vector3(0.9f, 1.85f, 0.86f)));
                    placed.Add((props.Count - 1, blockers.Count - 1));
                }
            }

            // ---------- after the walls are final ----------

            /// Windows on the office walls (blacked out, but for the odd lure), fountains and machines along the
            /// corridors, and now and then one of the abandoned sites.
            void Level4Finish()
            {
                if (theme.id != "4") return;
                var keys = new List<int>(edges.Keys);
                keys.Sort();
                foreach (int key in keys)
                {
                    if (edges[key].Kind != EdgeKind.Wall) continue;
                    foreach (bool side in new[] { true, false })
                    {
                        if (!WallFrame(key, side, out var pose, out var cell) || Void(cell.x, cell.y) || used[cell.x, cell.y]) continue;
                        var k = zones[zone[cell.x, cell.y]].Kind;
                        if (HeightAt(cell.x, cell.y) < 2.5f) continue;
                        float r = rng.Value();
                        if (OfficeRoom(k) && r < 0.07f)
                        {
                            used[cell.x, cell.y] = true;
                            if (rng.Value() < LureChance) Special(SpecialKind.LureWindow, pose, Vector3.zero);
                            else props.Add(new PropSpec { Kind = PropKind.OfficeWindow, Variant = rng.Range(0, 12), Pose = pose, Ceiling = HeightAt(cell.x, cell.y) });
                        }
                        else if (k == ZoneKind.Corridor && r < 0.015f) { used[cell.x, cell.y] = true; Dispenser(pose, PropKind.DrinkFountain); }
                        else if (k == ZoneKind.Corridor && r < 0.022f) { used[cell.x, cell.y] = true; Dispenser(pose, PropKind.VendingMachine); }
                        else if (OfficeRoom(k) && r < 0.08f) { used[cell.x, cell.y] = true; Dispenser(pose, PropKind.WaterCooler); }
                    }
                }
                if (rng.Value() < 0.25f) AbandonedSite();
            }

            void AbandonedSite()
            {
                int best = -1, bestCells = 0;
                for (int i = 0; i < zones.Count; i++)
                {
                    var k = zones[i].Kind;
                    if (k != ZoneKind.Office && k != ZoneKind.Conference && k != ZoneKind.Room && k != ZoneKind.Reception && k != ZoneKind.HotelRoom) continue;
                    int n = 0;
                    var r = zones[i].Rect;
                    for (int x = Mathf.Max(0, r.xMin); x < Mathf.Min(D, r.xMax); x++)
                    for (int zz = Mathf.Max(0, r.yMin); zz < Mathf.Min(D, r.yMax); zz++)
                        if (zone[x, zz] == i) n++;
                    if (n >= 9 && n <= 40 && Mathf.Abs(n - 20) < Mathf.Abs(bestCells - 20)) { best = i; bestCells = n; }   // a room about 80 m², not a hall
                }
                if (best < 0) return;
                float roll = rng.Value();
                if (roll < 0.4f) BaseOmega(best);
                else if (roll < 0.7f) MoldFarm(best);
                else Hoard(best);
            }

            void WallWriting(int id, string title, string text)
            {
                if (WallSpotIn(id, out var pose)) readables.Add(new ReadableSpec { Kind = ReadableKind.Writing, Pose = pose * Matrix4x4.Translate(new Vector3(0f, 1.45f, 0f)), Title = title, Text = text });   // eye height
            }

            void FloorNote(int id, string title, string text)
            {
                var r = zones[id].Rect;
                for (int attempt = 0; attempt < 10; attempt++)
                {
                    int x = rng.Range(r.xMin, r.xMax), zz = rng.Range(r.yMin, r.yMax);
                    if (x < 0 || zz < 0 || x >= D || zz >= D || zone[x, zz] != id || used[x, zz]) continue;
                    used[x, zz] = true;
                    readables.Add(new ReadableSpec { Kind = ReadableKind.Note, Pose = Matrix4x4.TRS(CellCenter(x, zz) + Vector3.up * 0.01f, Quaternion.Euler(0f, rng.Value() * 360f, 0f), Vector3.one), Title = title, Text = text });
                    return;
                }
            }

            /// A M.E.G. outpost, packed up in a hurry: bedrolls, crates, a radio, supplies they couldn't carry.
            void BaseOmega(int id)
            {
                var z = zones[id];
                z.Dead = 0f;
                z.Pool = false;
                for (int i = 0; i < 3; i++) PlaceInZone(id, PropKind.SleepingBag, false);
                for (int i = 0; i < 2; i++) PlaceInZone(id, PropKind.Lantern, false);
                for (int i = 0; i < 3; i++) PlaceInZone(id, PropKind.StackedBoxes, true);
                PlaceInZone(id, PropKind.Phone, false);
                PlaceOnWall(id, PropKind.Desk);
                for (int i = 0; i < 3; i++) PlaceInZone(id, PropKind.AlmondWater, false);
                for (int i = 0; i < 2; i++) PlaceInZone(id, PropKind.Battery, false);
                WallWriting(id, "Stencilled on the wall", "M.E.G.\nBASE OMEGA\nOUTPOST 4\n\nEVACUATED. FALL BACK TO THE STAIRS (LEVEL 5)");
                FloorNote(id, "Base Omega supply log, last page",
                    "Almond water: plenty, the coolers never run dry. Batteries: low. Morale: low.\n\n" +
                    "Reminder to everyone: the east windows stay covered. Dawes uncovered one to 'see the street'. " +
                    "He stood there smiling for a minute, then walked into it. No broken glass. No Dawes.\n\n" +
                    "We're moving up the stairs tonight. Leave what you can't carry.");
            }

            /// The cult's room: shelves of mold trays under a single light, offerings, scripture on the wall.
            void MoldFarm(int id)
            {
                var z = zones[id];
                z.Dead = 1f;
                z.Pool = true;
                z.Sick = true;
                var r = z.Rect;
                int racks = 0;
                for (int attempt = 0; attempt < 20 && racks < 3; attempt++)
                {
                    int x = rng.Range(r.xMin + 1, r.xMax - 1), zz = rng.Range(r.yMin, r.yMax);
                    if (x < 1 || zz < 0 || x >= D - 1 || zz >= D || zone[x, zz] != id || zone[x - 1, zz] != id || zone[x + 1, zz] != id || used[x, zz] || used[x - 1, zz] || used[x + 1, zz]) continue;
                    used[x, zz] = used[x - 1, zz] = used[x + 1, zz] = true;
                    var rack = Matrix4x4.TRS(CellCenter(x, zz), Quaternion.identity, Vector3.one);
                    props.Add(new PropSpec { Kind = PropKind.ShelfRack, Variant = 0, Pose = rack, Ceiling = z.Height });
                    foreach (float y in new[] { 0.82f, 1.47f })
                        foreach (float sx in new[] { -0.75f, 0f, 0.75f })
                            if (rng.Value() < 0.85f) props.Add(new PropSpec { Kind = PropKind.MoldTray, Pose = rack * Matrix4x4.TRS(new Vector3(sx, y, 0f), Quaternion.Euler(0f, rng.Value() * 10f - 5f, 0f), Vector3.one), Ceiling = z.Height });
                    racks++;
                }
                for (int i = 0; i < 2; i++) PlaceInZone(id, PropKind.MossPatch, false);
                for (int i = 0; i < 3; i++) PlaceInZone(id, PropKind.Lantern, false);
                WallWriting(id, "Painted on the wall in something brown", "AMOR INCREMENTUM\n\nIT GROWS IN THE DARK.\nFEED IT. GIVE IT SOMETHING HOLY.\nIT LOVES THOSE WHO GIVE.");
                FloorNote(id, "Torn from a notebook",
                    "Found the mold people. Six of them, maybe seven, all very calm. They wouldn't let me past until I'd given them " +
                    "something 'holy'. I had my grandmother's medal. They took it like it was food.\n\nThey're gone now. The trays are still damp. " +
                    "Somebody is still watering them.");
            }

            /// The hoarders' room: a little of everything, nothing useful, a sign on the wall.
            void Hoard(int id)
            {
                var kinds = new[] { PropKind.CardboardBox, PropKind.StackedBoxes, PropKind.CrtTv, PropKind.Phone, PropKind.FloorLamp, PropKind.WoodChair,
                                    PropKind.Bucket, PropKind.TrashCan, PropKind.OfficeChair, PropKind.Rug, PropKind.Lantern, PropKind.WetFloorSign };
                // Piles: a few cells each heaped with three or four things, not one per cell like furniture.
                var r = zones[id].Rect;
                int piles = 0;
                for (int attempt = 0; attempt < 30 && piles < 8; attempt++)
                {
                    int x = rng.Range(r.xMin, r.xMax), zz = rng.Range(r.yMin, r.yMax);
                    if (x < 0 || zz < 0 || x >= D || zz >= D || zone[x, zz] != id || used[x, zz]) continue;
                    used[x, zz] = true;
                    piles++;
                    var c = CellCenter(x, zz);
                    for (int i = rng.Range(3, 5); i > 0; i--)
                    {
                        var k = kinds[rng.Range(0, kinds.Length)];
                        if (k == PropKind.Rug && i > 1) k = PropKind.CardboardBox;   // one rug at most, under the pile
                        props.Add(new PropSpec { Kind = k, Variant = rng.Range(0, 12), Ceiling = zones[id].Height,
                                                 Pose = Matrix4x4.TRS(c + new Vector3(Range(-0.6f, 0.6f), 0f, Range(-0.6f, 0.6f)), Quaternion.Euler(0f, rng.Value() * 360f, 0f), Vector3.one) });
                    }
                }
                PlaceOnWall(id, PropKind.Bookshelf);
                WallWriting(id, "A cardboard sign nailed to the wall", "TRADE ONLY\nSTRANGE THINGS ONLY\n\n(we have water. we have batteries. we don't want them)");
                FloorNote(id, "Inventory, page 31",
                    "One shoe, left, wet.\nOne clock with no hands (still ticks).\nThree photographs of this room, taken from inside the wall.\n" +
                    "One window pane, clear. DO NOT unwrap.\nOne smile (in a jar).\n\nTotal: not enough.");
            }
        }
    }
}
