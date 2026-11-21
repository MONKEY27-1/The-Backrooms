using System.Collections.Generic;
using UnityEngine;

namespace Backrooms.World
{
    /// <summary>
    /// Level 7 - "Thalassophobia" (Backrooms Wiki, CC BY-SA 3.0):
    /// An endless, freezing, dark ocean contained under a crushing concrete ceiling 28m above.
    /// Wanderers enter through a damp concrete entrance bunker at chunk (0,0) with stairs leading
    /// directly down into the water. The surrounding ocean features submerged rooms, titanic pillars,
    /// floating rusty catwalks, sunken concrete ruins, and an underwater cavern trench leading to Level 8.
    /// </summary>
    public static class OceanLayout
    {
        public const float OceanWaterLevel = 4.0f;
        public const float OceanCeilingHeight = 28.0f;

        public static ChunkLayout Generate(uint seed, int cx, int cz, int layer, LevelTheme theme)
        {
            var L = new ChunkLayout();
            float s = MazeLayout.ChunkSize; // 16m
            float x0 = cx * s;
            float z0 = cz * s;
            var chunkRect = new Rect(x0, z0, s, s);

            var wallMat = new SurfaceRef(Surface.Wall, 0);
            var floorMat = new SurfaceRef(Surface.Concrete, 0);
            var ceilMat = new SurfaceRef(Surface.Ceiling, 0);
            var trimMat = new SurfaceRef(Surface.Trim, 0);

            void Box(Vector3 center, Vector3 size, SurfaceRef material) =>
                L.Boxes.Add(SurfaceBox.Uniform(new Bounds(center, size), material, BoxMeshBuilder.Faces.All));

            // Universal dark ocean water plane across every chunk
            L.Water.Add(new SurfaceQuad
            {
                Area = chunkRect,
                Height = OceanWaterLevel,
                FacingUp = true
            });

            // Massive oppressive monolithic concrete ceiling high overhead
            L.Quads.Add(new SurfaceQuad
            {
                Area = chunkRect,
                Height = OceanCeilingHeight,
                FacingUp = false,
                Surface = ceilMat
            });

            // Chunk (0, 0): The Canon Entrance Bunker
            if (cx == 0 && cz == 0)
            {
                BuildEntranceBunker(L, s, wallMat, floorMat, ceilMat, trimMat);
                return L;
            }

            // Procedural Ocean Depth & Submerged Architecture for other chunks
            float depthNoise = Mathf.PerlinNoise((cx + 100) * 0.18f, (cz + 100) * 0.18f);
            float seabedY = Mathf.Lerp(-8f, -26f, depthNoise);

            // Submerged seabed floor
            L.Quads.Add(new SurfaceQuad
            {
                Area = chunkRect,
                Height = seabedY,
                FacingUp = true,
                Surface = floorMat
            });

            // Check if this chunk is the Deep Submerged Trench (Exit to Level 8)
            bool isTrenchExit = (cx == 3 && cz == 3);
            if (isTrenchExit)
            {
                BuildUnderwaterTrenchExit(L, x0, z0, s, seabedY, wallMat, floorMat);
                return L;
            }

            // Pseudo-random architectural features derived deterministically from chunk coords
            uint chunkHash = WorldHash.Hash(seed, cx, cz, 0x777);
            float featureRoll = WorldHash.Float(chunkHash, 1);

            // Feature 1: Titanic Concrete Monolith Pillar rising from abyss to ceiling
            if (featureRoll < 0.22f)
            {
                float pillarW = 4.5f + WorldHash.Float(chunkHash, 2) * 3f;
                float pillarD = 4.5f + WorldHash.Float(chunkHash, 3) * 3f;
                Vector3 pCenter = new Vector3(x0 + s * 0.5f, (seabedY + OceanCeilingHeight) * 0.5f, z0 + s * 0.5f);
                Box(pCenter, new Vector3(pillarW, OceanCeilingHeight - seabedY, pillarD), wallMat);

                // Solitary navigation beacon atop or attached to the monolith just above water
                Vector3 beaconPos = new Vector3(x0 + s * 0.5f, OceanWaterLevel + 1.8f, z0 + s * 0.5f + pillarD * 0.5f + 0.3f);
                L.Panels.Add(new PanelSpec
                {
                    Position = beaconPos,
                    Bare = true,
                    Emitter = true,
                    State = FixtureState.Lit,
                    Range = 12f,
                    Id = (int)chunkHash,
                    Tint = new Color(1.8f, 0.4f, 0.2f) // Warning beacon red/amber
                });

                // Steel service ladder hanging from the monolith into the ocean
                L.Props.Add(new PropSpec
                {
                    Kind = PropKind.WallLadder,
                    Pose = Matrix4x4.TRS(
                        new Vector3(x0 + s * 0.5f, OceanWaterLevel + 0.5f, z0 + s * 0.5f + pillarD * 0.5f + 0.1f),
                        Quaternion.Euler(0, 180, 0),
                        Vector3.one)
                });
            }
            // Feature 2: Submerged Ruined Room / Sunken Building Block
            else if (featureRoll < 0.45f)
            {
                float roomTop = Mathf.Lerp(seabedY + 3f, OceanWaterLevel + 0.8f, WorldHash.Float(chunkHash, 4));
                float roomH = roomTop - seabedY;
                Vector3 rCenter = new Vector3(x0 + s * 0.5f, seabedY + roomH * 0.5f, z0 + s * 0.5f);
                float rW = 9f, rD = 9f;

                // Hollow submerged perimeter walls
                float wallThick = 0.5f;
                // North/South walls
                Box(new Vector3(rCenter.x, rCenter.y, rCenter.z + rD * 0.5f), new Vector3(rW, roomH, wallThick), wallMat);
                Box(new Vector3(rCenter.x, rCenter.y, rCenter.z - rD * 0.5f), new Vector3(rW, roomH, wallThick), wallMat);
                // East/West walls with doorway gaps
                Box(new Vector3(rCenter.x - rW * 0.5f, rCenter.y, rCenter.z), new Vector3(wallThick, roomH, rD * 0.4f), wallMat);
                Box(new Vector3(rCenter.x + rW * 0.5f, rCenter.y, rCenter.z), new Vector3(wallThick, roomH, rD * 0.4f), wallMat);

                // Submerged flooded crate or rusted pipes inside
                L.Props.Add(new PropSpec
                {
                    Kind = PropKind.Crate,
                    Pose = Matrix4x4.TRS(new Vector3(rCenter.x - 2f, seabedY + 0.5f, rCenter.y - 1f), Quaternion.identity, Vector3.one * 1.2f)
                });
                L.Props.Add(new PropSpec
                {
                    Kind = PropKind.Pipes,
                    Pose = Matrix4x4.TRS(new Vector3(rCenter.x, seabedY + 0.8f, rCenter.z + 3f), Quaternion.Euler(0, 90, 0), Vector3.one)
                });
            }
            // Feature 3: Rusted Catwalk Pontoon floating on water
            else if (featureRoll < 0.65f)
            {
                Vector3 pontoonCenter = new Vector3(x0 + s * 0.5f, OceanWaterLevel + 0.15f, z0 + s * 0.5f);
                Box(pontoonCenter, new Vector3(4f, 0.3f, 10f), trimMat);

                // Ladder from water onto pontoon
                L.Props.Add(new PropSpec
                {
                    Kind = PropKind.WallLadder,
                    Pose = Matrix4x4.TRS(new Vector3(pontoonCenter.x + 2.05f, OceanWaterLevel - 0.2f, pontoonCenter.z), Quaternion.Euler(0, -90, 0), Vector3.one)
                });

                // Supply crate left by prior explorers
                if (WorldHash.Float(chunkHash, 5) < 0.5f)
                {
                    L.Props.Add(new PropSpec
                    {
                        Kind = PropKind.Crate,
                        Pose = Matrix4x4.TRS(new Vector3(pontoonCenter.x, OceanWaterLevel + 0.55f, pontoonCenter.z - 2f), Quaternion.identity, Vector3.one)
                    });
                }
            }

            return L;
        }

        static void BuildEntranceBunker(ChunkLayout L, float s, SurfaceRef wall, SurfaceRef floor, SurfaceRef ceil, SurfaceRef trim)
        {
            // Concrete bunker platform emerging from the water
            // Floor of bunker is dry at y = 5.2m (1.2m above water level)
            float bunkerFloorY = 5.2f;
            float bunkerCeilY = 9.5f;
            float bunkerH = bunkerCeilY - bunkerFloorY;
            Vector3 center = new Vector3(s * 0.5f, bunkerFloorY + bunkerH * 0.5f, s * 0.5f);

            void BBox(Vector3 c, Vector3 sz, SurfaceRef mat) =>
                L.Boxes.Add(SurfaceBox.Uniform(new Bounds(c, sz), mat, BoxMeshBuilder.Faces.All));

            // Bunker solid foundation extending down into the water
            BBox(new Vector3(center.x, 2.0f, center.z), new Vector3(12f, 6.4f, 12f), floor);

            // Bunker Ceiling
            BBox(new Vector3(center.x, bunkerCeilY + 0.25f, center.z), new Vector3(12f, 0.5f, 12f), ceil);

            // Bunker Walls: North, West, East closed; South open with steps leading down into dark ocean
            float wThick = 0.6f;
            // North wall
            BBox(new Vector3(center.x, center.y, center.z + 5.7f), new Vector3(12f, bunkerH, wThick), wall);
            // West wall
            BBox(new Vector3(center.x - 5.7f, center.y, center.z), new Vector3(wThick, bunkerH, 12f), wall);
            // East wall
            BBox(new Vector3(center.x + 5.7f, center.y, center.z), new Vector3(wThick, bunkerH, 12f), wall);

            // South wall has a 4m wide blast opening overlooking the black water
            BBox(new Vector3(center.x - 4f, center.y, center.z - 5.7f), new Vector3(4f, bunkerH, wThick), wall);
            BBox(new Vector3(center.x + 4f, center.y, center.z - 5.7f), new Vector3(4f, bunkerH, wThick), wall);

            // Concrete steps descending from dry bunker floor (5.2m) into the ocean (3.6m)
            int steps = 6;
            for (int i = 0; i < steps; i++)
            {
                float stepY = bunkerFloorY - (i + 1) * 0.26f;
                float stepZ = (center.z - 5.4f) - i * 0.7f;
                BBox(new Vector3(center.x, stepY - 0.5f, stepZ), new Vector3(3.8f, 1.0f, 0.7f), floor);
            }

            // Fluorescent lamp in the entrance bunker (flickering bunker light)
            L.Panels.Add(new PanelSpec
            {
                Position = new Vector3(center.x, bunkerCeilY - 0.1f, center.z),
                AlongX = true,
                Bare = false,
                Emitter = true,
                State = FixtureState.Lit,
                Range = 10f,
                Id = 7001,
                Tint = new Color(0.9f, 0.95f, 1.0f)
            });

            // Entrance Door / Staircase returning to Level 6
            L.Exits.Add(new ExitSpec
            {
                Kind = ExitKind.Stairs,
                Target = "6",
                Pose = Matrix4x4.TRS(new Vector3(center.x, bunkerFloorY, center.z + 5.3f), Quaternion.Euler(0, 180, 0), Vector3.one)
            });
        }

        static void BuildUnderwaterTrenchExit(ChunkLayout L, float x0, float z0, float s, float seabedY, SurfaceRef wall, SurfaceRef floor)
        {
            // Deep sunken abyss trench descending to -32m
            float trenchBottom = -32f;
            Vector3 center = new Vector3(x0 + s * 0.5f, (seabedY + trenchBottom) * 0.5f, z0 + s * 0.5f);

            void TBox(Vector3 c, Vector3 sz, SurfaceRef mat) =>
                L.Boxes.Add(SurfaceBox.Uniform(new Bounds(c, sz), mat, BoxMeshBuilder.Faces.All));

            // Trench walls forming a rectangular chasm
            float w = 8f, d = 8f, h = seabedY - trenchBottom;
            TBox(new Vector3(center.x, center.y, center.z + d * 0.5f), new Vector3(w + 2f, h, 1f), wall);
            TBox(new Vector3(center.x, center.y, center.z - d * 0.5f), new Vector3(w + 2f, h, 1f), wall);
            TBox(new Vector3(center.x - w * 0.5f, center.y, center.z), new Vector3(1f, h, d), wall);
            TBox(new Vector3(center.x + w * 0.5f, center.y, center.z), new Vector3(1f, h, d), wall);

            // Trench rocky floor
            TBox(new Vector3(center.x, trenchBottom - 0.5f, center.z), new Vector3(w, 1f, d), floor);

            // Submerged hatch / cave opening into Level 8
            L.Exits.Add(new ExitSpec
            {
                Kind = ExitKind.Hatch,
                Target = "8",
                Pose = Matrix4x4.TRS(new Vector3(center.x, trenchBottom + 0.1f, center.z), Quaternion.identity, Vector3.one)
            });

            // Emergency marine marker glow at the trench bottom
            L.Panels.Add(new PanelSpec
            {
                Position = new Vector3(center.x, trenchBottom + 1.2f, center.z),
                Bare = true,
                Emitter = true,
                State = FixtureState.Lit,
                Range = 14f,
                Id = 7888,
                Tint = new Color(0.2f, 1.2f, 0.8f) // Deep-sea bioluminescent cyan
            });
        }
    }
}
