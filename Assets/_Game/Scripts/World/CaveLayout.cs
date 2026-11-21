using System.Collections.Generic;
using UnityEngine;

namespace Backrooms.World
{
    /// <summary>
    /// Level 8 - "Cave System" (Backrooms Wiki, CC BY-SA 3.0):
    /// An untamed, non-man-made karst cave network of damp limestone rock, natural chambers,
    /// claustrophobic crawlways, stalactites, stalagmites, subterranean puddles, and deep sinkholes.
    /// Pitch black: zero artificial lights. Light comes only from the player's flashlight.
    /// A sinkhole in the cavern floor plunges down into the darkened streets of Level 9.
    /// </summary>
    public static class CaveLayout
    {
        public static ChunkLayout Generate(uint seed, int cx, int cz, int layer, LevelTheme theme)
        {
            var L = new ChunkLayout();
            float s = MazeLayout.ChunkSize; // 16m
            float x0 = cx * s;
            float z0 = cz * s;

            var rockMat = new SurfaceRef(Surface.Concrete, 0); // Rock/limestone concrete texture
            var floorMat = new SurfaceRef(Surface.Floor, 0);

            void Box(Vector3 center, Vector3 size, SurfaceRef material) =>
                L.Boxes.Add(SurfaceBox.Uniform(new Bounds(center, size), material, BoxMeshBuilder.Faces.All));

            // Chunk (0, 0): Entrance from Level 7 (flooded karst grotto)
            if (cx == 0 && cz == 0)
            {
                BuildCaveEntrance(L, s, rockMat, floorMat);
                return L;
            }

            // Check if this chunk contains the Pitfall Sinkhole to Level 9
            bool isSinkhole = (cx == 2 && cz == 2);
            if (isSinkhole)
            {
                BuildSinkholeExit(L, x0, z0, s, rockMat, floorMat);
                return L;
            }

            // Procedural Organic Cavern Generation
            // We divide each 16m chunk into a 4x4 cellular grid of 4m cells
            const int cells = 4;
            float cellSize = s / cells;

            // Generate organic floor and ceiling elevations using multi-frequency coherent noise
            float baseFloorY = 0f;
            float baseCeilY = 3.8f + Mathf.PerlinNoise((cx + 50) * 0.25f, (cz + 50) * 0.25f) * 3.6f;

            // Uneven rocky floor and rocky ceiling
            L.Quads.Add(new SurfaceQuad
            {
                Area = new Rect(x0, z0, s, s),
                Height = baseFloorY,
                FacingUp = true,
                Surface = floorMat
            });
            L.Quads.Add(new SurfaceQuad
            {
                Area = new Rect(x0, z0, s, s),
                Height = baseCeilY,
                FacingUp = false,
                Surface = rockMat
            });

            // Populate cells with natural rock pillars, narrow passages, stalactites, and stalagmites
            for (int ix = 0; ix < cells; ix++)
            {
                for (int iz = 0; iz < cells; iz++)
                {
                    float cellX = x0 + (ix + 0.5f) * cellSize;
                    float cellZ = z0 + (iz + 0.5f) * cellSize;
                    uint h = WorldHash.Hash(seed, (int)(cellX * 10), (int)(cellZ * 10), 0x888);
                    float roll = WorldHash.Float(h, 0);

                    // Continuous walkable path: ensure center cells or cardinal edges stay passable
                    bool isPath = (ix == 1 || ix == 2) || (iz == 1 || iz == 2);

                    // Solid limestone rock pillar/wall filling this cell
                    if (!isPath && roll < 0.65f)
                    {
                        float pW = cellSize * (0.8f + WorldHash.Float(h, 1) * 0.2f);
                        float pD = cellSize * (0.8f + WorldHash.Float(h, 2) * 0.2f);
                        float pH = baseCeilY - baseFloorY;
                        Box(new Vector3(cellX, baseFloorY + pH * 0.5f, cellZ), new Vector3(pW, pH, pD), rockMat);
                    }
                    // Open cave passage with stalactites, stalagmites, or puddles
                    else
                    {
                        // Hanging stalactite rock clusters from the ceiling
                        if (roll > 0.45f)
                        {
                            float stLen = 1.2f + WorldHash.Float(h, 3) * 1.6f;
                            float stW = 0.6f + WorldHash.Float(h, 4) * 0.5f;
                            Vector3 stPos = new Vector3(cellX + (WorldHash.Float(h, 5) - 0.5f) * 1.5f, baseCeilY - stLen * 0.5f, cellZ + (WorldHash.Float(h, 6) - 0.5f) * 1.5f);
                            Box(stPos, new Vector3(stW, stLen, stW), rockMat);

                            // Tapered tip
                            Box(stPos - new Vector3(0, stLen * 0.5f + 0.2f, 0), new Vector3(stW * 0.5f, 0.4f, stW * 0.5f), rockMat);
                        }

                        // Rising stalagmite rock formations on the ground
                        if (roll < 0.25f)
                        {
                            float smH = 0.8f + WorldHash.Float(h, 7) * 1.1f;
                            float smW = 0.7f + WorldHash.Float(h, 8) * 0.4f;
                            Vector3 smPos = new Vector3(cellX + (WorldHash.Float(h, 9) - 0.5f) * 1.2f, baseFloorY + smH * 0.5f, cellZ + (WorldHash.Float(h, 10) - 0.5f) * 1.2f);
                            Box(smPos, new Vector3(smW, smH, smW), rockMat);
                        }

                        // Subterranean puddle
                        if (roll > 0.3f && roll < 0.55f)
                        {
                            L.Props.Add(new PropSpec
                            {
                                Kind = PropKind.Puddle,
                                Pose = Matrix4x4.TRS(new Vector3(cellX, baseFloorY + 0.02f, cellZ), Quaternion.Euler(0, roll * 360f, 0), Vector3.one * 1.4f)
                            });
                        }

                        // Rare abandoned survival equipment left behind by doomed explorers
                        if (roll > 0.94f)
                        {
                            L.Props.Add(new PropSpec
                            {
                                Kind = PropKind.Lantern,
                                Pose = Matrix4x4.TRS(new Vector3(cellX, baseFloorY + 0.1f, cellZ), Quaternion.identity, Vector3.one)
                            });
                            L.Props.Add(new PropSpec
                            {
                                Kind = PropKind.AlmondWater,
                                Pose = Matrix4x4.TRS(new Vector3(cellX + 0.4f, baseFloorY + 0.05f, cellZ + 0.2f), Quaternion.Euler(0, 45, 0), Vector3.one)
                            });
                        }
                    }
                }
            }

            return L;
        }

        static void BuildCaveEntrance(ChunkLayout L, float s, SurfaceRef rock, SurfaceRef floor)
        {
            void EBox(Vector3 c, Vector3 sz, SurfaceRef mat) =>
                L.Boxes.Add(SurfaceBox.Uniform(new Bounds(c, sz), mat, BoxMeshBuilder.Faces.All));

            float ceilY = 4.5f;

            // Cavern grotto room: 14m x 14m
            EBox(new Vector3(s * 0.5f, -0.5f, s * 0.5f), new Vector3(s, 1f, s), floor);
            EBox(new Vector3(s * 0.5f, ceilY + 0.5f, s * 0.5f), new Vector3(s, 1f, s), rock);

            // Perimeter jagged cavern boundary with openings to adjacent caves
            EBox(new Vector3(s * 0.5f, ceilY * 0.5f, s - 0.8f), new Vector3(s, ceilY, 1.6f), rock); // North
            EBox(new Vector3(s * 0.5f, ceilY * 0.5f, 0.8f), new Vector3(s, ceilY, 1.6f), rock);     // South
            EBox(new Vector3(0.8f, ceilY * 0.5f, s * 0.5f), new Vector3(1.6f, ceilY, s), rock);     // West
            // East side open to explore

            // Submerged grotto pool where the wanderer climbs out of Level 7
            Vector3 poolPos = new Vector3(5f, 0.01f, 8f);
            L.Props.Add(new PropSpec
            {
                Kind = PropKind.Puddle,
                Pose = Matrix4x4.TRS(poolPos, Quaternion.identity, Vector3.one * 3f)
            });

            // Return entrance back to Level 7
            L.Exits.Add(new ExitSpec
            {
                Kind = ExitKind.Hatch,
                Target = "7",
                Pose = Matrix4x4.TRS(poolPos, Quaternion.Euler(0, 180, 0), Vector3.one)
            });

            // Explorer camp remains
            L.Props.Add(new PropSpec
            {
                Kind = PropKind.SleepingBag,
                Pose = Matrix4x4.TRS(new Vector3(10f, 0.05f, 6f), Quaternion.Euler(0, 30, 0), Vector3.one)
            });
            L.Props.Add(new PropSpec
            {
                Kind = PropKind.Lantern,
                Pose = Matrix4x4.TRS(new Vector3(11.2f, 0.05f, 6.5f), Quaternion.identity, Vector3.one)
            });
            L.Props.Add(new PropSpec
            {
                Kind = PropKind.Battery,
                Pose = Matrix4x4.TRS(new Vector3(10.8f, 0.05f, 5.8f), Quaternion.identity, Vector3.one)
            });
        }

        static void BuildSinkholeExit(ChunkLayout L, float x0, float z0, float s, SurfaceRef rock, SurfaceRef floor)
        {
            void SBox(Vector3 c, Vector3 sz, SurfaceRef mat) =>
                L.Boxes.Add(SurfaceBox.Uniform(new Bounds(c, sz), mat, BoxMeshBuilder.Faces.All));

            Vector3 center = new Vector3(x0 + s * 0.5f, 0f, z0 + s * 0.5f);
            float ceilY = 5.0f;

            // Ceil & floor surrounding the sinkhole
            SBox(new Vector3(center.x, ceilY + 0.5f, center.z), new Vector3(s, 1f, s), rock);

            // A 6m x 6m fractured black chasm pitfall in the center of the cavern floor
            float pitW = 6f;
            // Floor sections surrounding the pit
            SBox(new Vector3(center.x, -0.5f, z0 + 2.5f), new Vector3(s, 1f, 5f), floor);
            SBox(new Vector3(center.x, -0.5f, z0 + s - 2.5f), new Vector3(s, 1f, 5f), floor);
            SBox(new Vector3(x0 + 2.5f, -0.5f, center.z), new Vector3(5f, 1f, pitW), floor);
            SBox(new Vector3(x0 + s - 2.5f, -0.5f, center.z), new Vector3(5f, 1f, pitW), floor);

            // The deep pit shaft dropping down into Level 9
            float pitDepth = -14f;
            SBox(new Vector3(center.x, pitDepth * 0.5f, z0 + 5f), new Vector3(pitW + 2f, Mathf.Abs(pitDepth), 1f), rock);
            SBox(new Vector3(center.x, pitDepth * 0.5f, z0 + s - 5f), new Vector3(pitW + 2f, Mathf.Abs(pitDepth), 1f), rock);
            SBox(new Vector3(x0 + 5f, pitDepth * 0.5f, center.z), new Vector3(1f, Mathf.Abs(pitDepth), pitW), rock);
            SBox(new Vector3(x0 + s - 5f, pitDepth * 0.5f, center.z), new Vector3(1f, Mathf.Abs(pitDepth), pitW), rock);

            // Pit trigger hatch leading to Level 9 (falling into the Darkened Suburbs)
            L.Exits.Add(new ExitSpec
            {
                Kind = ExitKind.Hatch,
                Target = "9",
                Pose = Matrix4x4.TRS(new Vector3(center.x, pitDepth + 0.5f, center.z), Quaternion.identity, Vector3.one)
            });
        }
    }
}
