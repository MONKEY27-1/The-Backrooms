using System.Collections.Generic;
using UnityEngine;

namespace Backrooms.World
{
    /// <summary>
    /// Level 9 - "The Suburbs" / "Darkened Suburbs" (Backrooms Wiki, CC BY-SA 3.0):
    /// An infinite suburban neighborhood frozen in perpetual midnight.
    /// Paved asphalt roads, concrete curbs and sidewalks, manicured front lawns,
    /// residential houses with gabled roofs and porches, driveways with cars,
    /// and tall sodium-vapor streetlights casting eerie amber halos through low fog.
    /// </summary>
    public static class SuburbsLayout
    {
        public const float SkyHeight = 24.0f;

        public static ChunkLayout Generate(uint seed, int cx, int cz, int layer, LevelTheme theme)
        {
            var L = new ChunkLayout();
            float s = MazeLayout.ChunkSize; // 16m
            float x0 = cx * s;
            float z0 = cz * s;

            var roadMat = new SurfaceRef(Surface.Concrete, 0); // Asphalt roadway
            var curbMat = new SurfaceRef(Surface.Trim, 0);     // Concrete sidewalk & curb
            var grassMat = new SurfaceRef(Surface.Floor, 0);   // Dark lawn
            var houseWall = new SurfaceRef(Surface.Wall, 0);   // Siding/brick house exterior
            var roofMat = new SurfaceRef(Surface.Ceiling, 0);  // Dark shingled roof
            var woodMat = new SurfaceRef(Surface.Wood, 0);     // Interior wood floor

            // Midnight sky / cloud cover far above
            L.Quads.Add(new SurfaceQuad
            {
                Area = new Rect(x0, z0, s, s),
                Height = SkyHeight,
                FacingUp = false,
                Surface = roofMat
            });

            // Determine chunk classification:
            // Every 3 chunks in X or Z is an East-West or North-South street, creating an authentic suburban block grid!
            bool isStreetX = (Mathf.Abs(cz) % 3 == 0); // East-West road
            bool isStreetZ = (Mathf.Abs(cx) % 3 == 0); // North-South road
            bool isIntersection = isStreetX && isStreetZ;

            // Chunk (0, 0): The Arrival Storm Drain from Level 8
            if (cx == 0 && cz == 0)
            {
                BuildArrivalManhole(L, s, roadMat, curbMat);
            }

            // Road / Intersection Chunks
            if (isStreetX || isStreetZ)
            {
                BuildStreet(L, x0, z0, s, isStreetX, isStreetZ, isIntersection, roadMat, curbMat);
            }
            // Residential Property Lot Chunks (Houses, lawns, driveways)
            else
            {
                BuildResidentialLot(L, seed, cx, cz, x0, z0, s, grassMat, curbMat, houseWall, roofMat, woodMat);
            }

            return L;
        }

        static void BuildStreet(ChunkLayout L, float x0, float z0, float s, bool isX, bool isZ, bool isInter, SurfaceRef road, SurfaceRef curb)
        {
            void SBox(Vector3 c, Vector3 sz, SurfaceRef mat) =>
                L.Boxes.Add(SurfaceBox.Uniform(new Bounds(c, sz), mat, BoxMeshBuilder.Faces.All));

            Vector3 center = new Vector3(x0 + s * 0.5f, 0f, z0 + s * 0.5f);

            // Full asphalt road surface
            SBox(new Vector3(center.x, -0.2f, center.z), new Vector3(s, 0.4f, s), road);

            // Sidewalks along road borders
            float walkW = 2.0f;
            float walkH = 0.15f;
            if (!isInter)
            {
                if (isX) // East-West street: sidewalks on North and South edges
                {
                    SBox(new Vector3(center.x, walkH * 0.5f, z0 + walkW * 0.5f), new Vector3(s, walkH, walkW), curb);
                    SBox(new Vector3(center.x, walkH * 0.5f, z0 + s - walkW * 0.5f), new Vector3(s, walkH, walkW), curb);
                }
                else // North-South street: sidewalks on West and East edges
                {
                    SBox(new Vector3(x0 + walkW * 0.5f, walkH * 0.5f, center.z), new Vector3(walkW, walkH, s), curb);
                    SBox(new Vector3(x0 + s - walkW * 0.5f, walkH * 0.5f, center.z), new Vector3(walkW, walkH, s), curb);
                }
            }

            // Tall Sodium-Vapor Streetlight standing on sidewalk
            Vector3 lightPos = new Vector3(center.x + (isX ? 4f : -walkW * 0.5f - 0.5f), 5.5f, center.z + (isX ? -walkW * 0.5f - 0.5f : 4f));
            // Pole
            SBox(new Vector3(lightPos.x, 2.75f, lightPos.z), new Vector3(0.2f, 5.5f, 0.2f), curb);
            // Arm
            SBox(new Vector3(lightPos.x + 0.6f, 5.5f, lightPos.z), new Vector3(1.2f, 0.15f, 0.15f), curb);
            // Lamp fixture & warm amber light
            L.Panels.Add(new PanelSpec
            {
                Position = new Vector3(lightPos.x + 1.1f, 5.3f, lightPos.z),
                Bare = true,
                Emitter = true,
                State = FixtureState.Lit,
                Range = 16f,
                Id = (int)(x0 * 37 + z0 * 19),
                Tint = new Color(1.0f, 0.68f, 0.28f) // Classic low-pressure sodium streetlight amber
            });

            // Street clutter: trash cans or abandoned car parked at curb
            uint hash = WorldHash.Hash(0x999, (int)x0, (int)z0, 4);
            if (WorldHash.Float(hash, 0) < 0.35f)
            {
                L.Props.Add(new PropSpec
                {
                    Kind = PropKind.TrashCan,
                    Pose = Matrix4x4.TRS(new Vector3(center.x + 2f, 0.15f, z0 + 1.2f), Quaternion.identity, Vector3.one)
                });
            }
            if (WorldHash.Float(hash, 1) < 0.25f && !isInter)
            {
                L.Props.Add(new PropSpec
                {
                    Kind = WorldHash.Float(hash, 2) < 0.5f ? PropKind.OldCar : PropKind.TarpCar,
                    Pose = Matrix4x4.TRS(new Vector3(center.x - 2f, 0.05f, center.z + (isX ? 3.5f : 0f)), Quaternion.Euler(0, isX ? 90 : 0, 0), Vector3.one)
                });
            }
        }

        static void BuildResidentialLot(ChunkLayout L, uint seed, int cx, int cz, float x0, float z0, float s,
            SurfaceRef grass, SurfaceRef curb, SurfaceRef wall, SurfaceRef roof, SurfaceRef wood)
        {
            void HBox(Vector3 c, Vector3 sz, SurfaceRef mat) =>
                L.Boxes.Add(SurfaceBox.Uniform(new Bounds(c, sz), mat, BoxMeshBuilder.Faces.All));

            Vector3 center = new Vector3(x0 + s * 0.5f, 0f, z0 + s * 0.5f);

            // Front & back yard lawn
            HBox(new Vector3(center.x, -0.1f, center.z), new Vector3(s, 0.2f, s), grass);

            // Concrete walkway from sidewalk to front door
            HBox(new Vector3(x0 + 4f, 0.05f, z0 + 3.5f), new Vector3(1.2f, 0.1f, 7f), curb);

            // Concrete driveway leading to garage
            HBox(new Vector3(x0 + 12f, 0.05f, z0 + 3.5f), new Vector3(3.2f, 0.1f, 7f), curb);

            // 3D Suburban House Structure (10m wide x 8m deep x 5m tall)
            float houseW = 10f, houseD = 8f, houseH = 4.8f;
            Vector3 houseCenter = new Vector3(center.x, houseH * 0.5f, z0 + s - houseD * 0.5f - 1f);

            // Perimeter walls of the house
            float wThick = 0.4f;
            // Back wall (North)
            HBox(new Vector3(houseCenter.x, houseCenter.y, houseCenter.z + houseD * 0.5f), new Vector3(houseW, houseH, wThick), wall);
            // Side walls (West & East)
            HBox(new Vector3(houseCenter.x - houseW * 0.5f, houseCenter.y, houseCenter.z), new Vector3(wThick, houseH, houseD), wall);
            HBox(new Vector3(houseCenter.x + houseW * 0.5f, houseCenter.y, houseCenter.z), new Vector3(wThick, houseH, houseD), wall);

            // Front wall (South) with front door opening and garage door
            HBox(new Vector3(houseCenter.x - 3f, houseCenter.y, houseCenter.z - houseD * 0.5f), new Vector3(3.5f, houseH, wThick), wall);
            HBox(new Vector3(houseCenter.x + 3.5f, houseCenter.y, houseCenter.z - houseD * 0.5f), new Vector3(3f, houseH, wThick), wall);
            // Header above front door
            HBox(new Vector3(houseCenter.x - 0.5f, houseH - 0.7f, houseCenter.z - houseD * 0.5f), new Vector3(1.5f, 1.4f, wThick), wall);

            // Gabled pitched roof blocks
            HBox(new Vector3(houseCenter.x, houseH + 0.6f, houseCenter.z), new Vector3(houseW + 1.2f, 0.6f, houseD + 1.2f), roof);
            HBox(new Vector3(houseCenter.x, houseH + 1.5f, houseCenter.z), new Vector3(houseW * 0.7f, 1.2f, houseD * 0.7f), roof);

            // House interior floor
            HBox(new Vector3(houseCenter.x, 0.05f, houseCenter.z), new Vector3(houseW - 0.8f, 0.1f, houseD - 0.8f), wood);

            // Porch light over front entrance
            Vector3 porchLightPos = new Vector3(houseCenter.x - 0.5f, 2.8f, houseCenter.z - houseD * 0.5f - 0.3f);
            L.Panels.Add(new PanelSpec
            {
                Position = porchLightPos,
                Bare = true,
                Emitter = true,
                State = FixtureState.Lit,
                Range = 8f,
                Id = (int)(x0 * 53 + z0 * 31),
                Tint = new Color(1.0f, 0.85f, 0.5f) // Warm incandescent porch lamp
            });

            // Special: Anomalous house front door acting as exit to Level 0 / 11
            bool isExitHouse = (cx == 2 && cz == 1);
            if (isExitHouse)
            {
                L.Exits.Add(new ExitSpec
                {
                    Kind = ExitKind.Door,
                    Target = "0",
                    Pose = Matrix4x4.TRS(new Vector3(houseCenter.x - 0.5f, 0.05f, houseCenter.z - houseD * 0.5f), Quaternion.identity, Vector3.one)
                });
            }

            // Parked car in driveway
            uint h = WorldHash.Hash(seed, cx, cz, 0x909);
            if (WorldHash.Float(h, 0) < 0.6f)
            {
                L.Props.Add(new PropSpec
                {
                    Kind = WorldHash.Float(h, 1) < 0.5f ? PropKind.OldCar : PropKind.TarpCar,
                    Pose = Matrix4x4.TRS(new Vector3(x0 + 12f, 0.1f, z0 + 5.5f), Quaternion.Euler(0, 180, 0), Vector3.one)
                });
            }
        }

        static void BuildArrivalManhole(ChunkLayout L, float s, SurfaceRef road, SurfaceRef curb)
        {
            Vector3 center = new Vector3(s * 0.5f, 0.02f, s * 0.5f);

            // Sewer grate manhole cover
            L.Props.Add(new PropSpec
            {
                Kind = PropKind.Puddle,
                Pose = Matrix4x4.TRS(center, Quaternion.identity, Vector3.one * 1.8f)
            });

            // Manhole ladder leading back up into Level 8
            L.Exits.Add(new ExitSpec
            {
                Kind = ExitKind.Hatch,
                Target = "8",
                Pose = Matrix4x4.TRS(center, Quaternion.Euler(0, 180, 0), Vector3.one)
            });
        }
    }
}
