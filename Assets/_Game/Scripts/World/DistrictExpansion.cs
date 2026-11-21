using System.Collections.Generic;
using UnityEngine;

namespace Backrooms.World
{
    /// <summary>
    /// Bespoke architectural district layouts for the expanded Backrooms levels:
    /// - Level 3 "Electrical Station": roaring machinery halls, generator banks, high-voltage transformers,
    ///   suspended cable trays, industrial pipes, raised maintenance catwalks, and violently flickering cage lights.
    /// - Level 4 "Abandoned Office": sprawling cubicle farms with partition dividers, water cooler clusters
    ///   stocked with Almond Water, conference rooms with whiteboards, and void-facing windows.
    /// - Level 5 "The Hotel": grand 1920s hotel wings with authentic carpeted hallways, repeating numbered
    ///   guest suites, opulent ballroom lounges with chandeliers, and a sweltering subterranean boiler room.
    /// </summary>
    public static partial class DistrictLayout
    {
        sealed partial class Plan
        {
            // ---------- Level 3: Electrical Station ----------

            public void BuildElectricalStation()
            {
                var all = new RectInt(0, 0, D, D);
                int hid = NewZone(ZoneKind.Hall, all);
                var hz = zones[hid];
                hz.Height = 4.8f;
                hz.Lights = LightLayout.Staggered;
                hz.Baseboard = false;
                hz.ChairRail = false;
                Paint(all, hid);

                // Main central maintenance corridor (width 4 cells) with transformer bays on both sides
                int midX = D / 2;
                bool alongX = rng.Value() < 0.5f;

                // Generator banks and high-voltage transformer rows
                for (int x = 2; x < D - 2; x += 3)
                {
                    for (int z = 2; z < D - 2; z += 3)
                    {
                        if (!OpenCell(x, z) || used[x, z] || !Allowed(x, z)) continue;

                        // Leave the central runway open
                        if (alongX && Mathf.Abs(z - midX) <= 1) continue;
                        if (!alongX && Mathf.Abs(x - midX) <= 1) continue;

                        used[x, z] = true;
                        var c = CellCenter(x, z);

                        // Giant humming generator / boiler transformer unit
                        props.Add(new PropSpec
                        {
                            Kind = PropKind.Boiler,
                            Pose = Matrix4x4.TRS(c, Quaternion.Euler(0f, (x * 37 + z * 19) % 4 * 90f, 0f), Vector3.one * 1.3f),
                            Ceiling = hz.Height
                        });

                        // Conduits and pipes connecting transformers
                        props.Add(new PropSpec
                        {
                            Kind = PropKind.CeilingPipes,
                            Pose = Matrix4x4.TRS(c + Vector3.up * (hz.Height - 0.4f), Quaternion.identity, Vector3.one),
                            Ceiling = hz.Height
                        });

                        // Hanging wires and electrical cabinets
                        if (rng.Value() < 0.6f)
                        {
                            props.Add(new PropSpec
                            {
                                Kind = PropKind.HangingWires,
                                Pose = Matrix4x4.TRS(c + Vector3.up * hz.Height, Quaternion.Euler(0f, rng.Range(0, 360), 0f), Vector3.one),
                                Ceiling = hz.Height
                            });
                        }
                    }
                }

                // Industrial perimeter walls with maintenance access doorways
                for (int w = 0; w < 3; w++)
                {
                    int rx = rng.Range(2, D - 6);
                    int rz = rng.Range(2, D - 6);
                    var room = new RectInt(rx, rz, 5, 5);
                    int rid = NewZone(ZoneKind.Storage, room);
                    zones[rid].Height = 3.6f;
                    zones[rid].Lights = LightLayout.Sparse;
                    Paint(room, rid);
                    RoomWalls(room, 1, EdgeKind.Doorway);

                    // Scavengeable batteries and almond water in maintenance rooms
                    PlaceInZone(rid, PropKind.Battery, false);
                    PlaceInZone(rid, PropKind.AlmondWater, false);
                    PlaceInZone(rid, PropKind.Ladder, false);
                }
            }

            // ---------- Level 4: Abandoned Office ----------

            public void BuildOfficeCubicles()
            {
                var all = new RectInt(0, 0, D, D);

                // Central corridor with large wings of cubicle pods
                int midZ = D / 2;
                var hallNorth = new RectInt(2, midZ + 2, D - 4, (D / 2) - 4);
                var hallSouth = new RectInt(2, 2, D - 4, (D / 2) - 4);
                var corridor = new RectInt(0, midZ - 1, D, 3);

                int cid = NewZone(ZoneKind.Corridor, corridor);
                zones[cid].Height = 2.8f;
                zones[cid].Lights = LightLayout.CenterStrip;
                Paint(corridor, cid);

                // North Wing: Cubicle Farm
                int nid = NewZone(ZoneKind.Cubicles, hallNorth);
                zones[nid].Height = 2.8f;
                zones[nid].Lights = LightLayout.Grid;
                Paint(hallNorth, nid);
                RoomWalls(hallNorth, 2, EdgeKind.Wide);

                // Build authentic 2x2 cubicle pods with low partition walls
                for (int z = hallNorth.yMin + 1; z < hallNorth.yMax - 2; z += 3)
                {
                    for (int x = hallNorth.xMin + 1; x < hallNorth.xMax - 2; x += 3)
                    {
                        // Cubicle divider walls
                        Wall(x, z, x + 2, z, EdgeKind.LowWall, null, out _);
                        Wall(x + 1, z, x + 1, z + 2, EdgeKind.LowWall, null, out _);

                        // Modular desks and chairs inside pod
                        props.Add(new PropSpec
                        {
                            Kind = PropKind.Desk,
                            Pose = Matrix4x4.TRS(CellCenter(x, z + 1), Quaternion.Euler(0, 0, 0), Vector3.one),
                            Ceiling = 2.8f
                        });
                        props.Add(new PropSpec
                        {
                            Kind = PropKind.OfficeChair,
                            Pose = Matrix4x4.TRS(CellCenter(x, z), Quaternion.Euler(0, rng.Range(-40, 40), 0), Vector3.one),
                            Ceiling = 2.8f
                        });
                        props.Add(new PropSpec
                        {
                            Kind = PropKind.FilingCabinet,
                            Pose = Matrix4x4.TRS(CellCenter(x + 1, z + 1), Quaternion.Euler(0, 90, 0), Vector3.one),
                            Ceiling = 2.8f
                        });
                    }
                }

                // Central Water Cooler Oasis (Almond Water hub) in the North Wing
                Vector3 coolerPos = CellCenter(hallNorth.xMin + hallNorth.width / 2, hallNorth.yMin + hallNorth.height / 2);
                for (int i = 0; i < 3; i++)
                {
                    props.Add(new PropSpec
                    {
                        Kind = PropKind.AlmondWater,
                        Pose = Matrix4x4.TRS(coolerPos + new Vector3((i - 1) * 0.7f, 0, 0), Quaternion.identity, Vector3.one),
                        Ceiling = 2.8f
                    });
                }

                // South Wing: Executive Conference Suite & Break Room
                int sid = NewZone(ZoneKind.Conference, hallSouth);
                zones[sid].Height = 3.2f;
                zones[sid].Lights = LightLayout.Rows;
                Paint(hallSouth, sid);
                RoomWalls(hallSouth, 2, EdgeKind.Doorway);

                // Long executive conference table & whiteboards
                Vector3 confCenter = CellCenter(hallSouth.xMin + hallSouth.width / 2, hallSouth.yMin + hallSouth.height / 2);
                props.Add(new PropSpec
                {
                    Kind = PropKind.ConferenceTable,
                    Pose = Matrix4x4.TRS(confCenter, Quaternion.identity, Vector3.one),
                    Ceiling = 3.2f
                });
                for (int c = -2; c <= 2; c++)
                {
                    props.Add(new PropSpec
                    {
                        Kind = PropKind.OfficeChair,
                        Pose = Matrix4x4.TRS(confCenter + new Vector3(c * 0.9f, 0, 0.75f), Quaternion.Euler(0, 180, 0), Vector3.one),
                        Ceiling = 3.2f
                    });
                    props.Add(new PropSpec
                    {
                        Kind = PropKind.OfficeChair,
                        Pose = Matrix4x4.TRS(confCenter + new Vector3(c * 0.9f, 0, -0.75f), Quaternion.identity, Vector3.one),
                        Ceiling = 3.2f
                    });
                }
                props.Add(new PropSpec
                {
                    Kind = PropKind.Whiteboard,
                    Pose = Matrix4x4.TRS(CellCenter(hallSouth.xMin + 1, hallSouth.yMin + hallSouth.height / 2), Quaternion.Euler(0, 90, 0), Vector3.one),
                    Ceiling = 3.2f
                });

                // Void-facing exterior windows (The hazardous window traps of Level 4)
                props.Add(new PropSpec
                {
                    Kind = PropKind.WindowToWall,
                    Pose = Matrix4x4.TRS(CellCenter(hallSouth.xMax - 1, hallSouth.yMin + 2), Quaternion.Euler(0, -90, 0), Vector3.one),
                    Ceiling = 3.2f
                });
            }

            // ---------- Level 5: The Hotel (Terror Hotel) ----------

            public void BuildHotelSuites()
            {
                var all = new RectInt(0, 0, D, D);

                // 1 in 3 hotel districts is a subterranean Boiler Room
                bool isBoilerRoom = (Mathf.Abs(dx + dz) % 3 == 0);
                if (isBoilerRoom)
                {
                    BuildHotelBoilerRoom(all);
                    return;
                }

                // Authentic 1920s Hotel Layout:
                // An elegant central corridor with repeating guest rooms on both sides,
                // opening into an opulent chandelier ballroom lounge.
                int midX = D / 2;
                var hallway = new RectInt(midX - 1, 0, 2, D);
                int hid = NewZone(ZoneKind.Corridor, hallway);
                var hz = zones[hid];
                hz.Height = 3.0f;
                hz.Lights = LightLayout.CenterStrip;
                hz.ChairRail = true;
                hz.Baseboard = true;
                Paint(hallway, hid);

                // Guest suites on Left (West) and Right (East)
                int roomSize = 4;
                for (int z = 1; z < D - roomSize; z += roomSize)
                {
                    // West Suite
                    var westRoom = new RectInt(1, z, midX - 2, roomSize);
                    int wid = NewZone(ZoneKind.HotelRoom, westRoom);
                    zones[wid].Height = 3.0f;
                    zones[wid].Baseboard = true;
                    Paint(westRoom, wid);
                    RoomWalls(westRoom, 0, EdgeKind.Doorway);
                    // Door opening toward central hallway
                    Opening(midX - 1, z + 1, midX - 1, z + 2, EdgeKind.Doorway);

                    // Furnish hotel suite: Bed, nightstand, vintage floor lamp
                    PlaceInZone(wid, PropKind.Bed, true);
                    PlaceInZone(wid, PropKind.Nightstand, true);
                    PlaceInZone(wid, PropKind.FloorLamp, false);

                    // East Suite: Guest room or Small Ballroom Lounge
                    var eastRoom = new RectInt(midX + 1, z, midX - 2, roomSize);
                    int eid = NewZone(z == 1 ? ZoneKind.Atrium : ZoneKind.HotelRoom, eastRoom);
                    zones[eid].Height = (z == 1) ? 5.2f : 3.0f; // Ballroom has high ceiling
                    zones[eid].Baseboard = true;
                    Paint(eastRoom, eid);
                    RoomWalls(eastRoom, 0, EdgeKind.Doorway);
                    Opening(midX + 1, z + 1, midX + 1, z + 2, EdgeKind.Doorway);

                    if (z == 1) // Grand Ballroom Lounge
                    {
                        PlaceInZone(eid, PropKind.Sofa, true);
                        PlaceInZone(eid, PropKind.Armchair, true);
                        PlaceInZone(eid, PropKind.CoffeeTable, true);
                        PlaceInZone(eid, PropKind.OctagonTable, true);
                        PlaceInZone(eid, PropKind.Rug, false);

                        // Warm chandelier ceiling glow
                        Vector3 ballCenter = CellCenter(eastRoom.xMin + eastRoom.width / 2, eastRoom.yMin + eastRoom.height / 2);
                        panels.Add(new PanelSpec
                        {
                            Position = ballCenter + Vector3.up * 4.9f,
                            Bare = true,
                            Emitter = true,
                            State = FixtureState.Lit,
                            Range = 14f,
                            Id = (int)(dx * 133 + dz * 77 + z),
                            Tint = new Color(1.0f, 0.82f, 0.55f) // Warm vintage chandelier light
                        });
                    }
                    else
                    {
                        PlaceInZone(eid, PropKind.Bed, true);
                        PlaceInZone(eid, PropKind.Nightstand, true);
                    }
                }
            }

            void BuildHotelBoilerRoom(RectInt all)
            {
                int bid = NewZone(ZoneKind.Hall, all);
                var bz = zones[bid];
                bz.Height = 4.2f;
                bz.Lights = LightLayout.Sparse;
                bz.Baseboard = false;
                bz.ChairRail = false;
                Paint(all, bid);

                // Industrial boilers and steam pipes in sweltering heat
                for (int x = 4; x < D - 4; x += 5)
                {
                    for (int z = 4; z < D - 4; z += 5)
                    {
                        var c = CellCenter(x, z);
                        props.Add(new PropSpec
                        {
                            Kind = PropKind.Boiler,
                            Pose = Matrix4x4.TRS(c, Quaternion.Euler(0, (x * 17 + z * 31) % 360, 0), Vector3.one * 1.5f),
                            Ceiling = bz.Height
                        });
                        props.Add(new PropSpec
                        {
                            Kind = PropKind.WallPipes,
                            Pose = Matrix4x4.TRS(c + Vector3.up * 1.5f, Quaternion.identity, Vector3.one),
                            Ceiling = bz.Height
                        });
                    }
                }

                // Dark descending iron hatch / boiler door leading down to Level 6
                Vector3 hatchPos = CellCenter(D / 2, D / 2);
                props.Add(new PropSpec
                {
                    Kind = PropKind.DoorToNowhere,
                    Pose = Matrix4x4.TRS(hatchPos, Quaternion.identity, Vector3.one),
                    Ceiling = bz.Height
                });
            }
        }
    }
}
