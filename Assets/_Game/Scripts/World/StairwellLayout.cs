using UnityEngine;

namespace Backrooms.World
{
    /// Level 9223372036854775807, "An Endless Ending" (AMM Backrooms wiki, CC BY-SA 3.0, adapted):
    /// an endless concrete spiral staircase in a shaft, entered from a 10 m entrance room with an elevator.
    /// The wiki describes floating, shifting/spiraling steps, a bright white void beyond boundary pillars,
    /// fog, and no entities. Approximated here with a continuous polygonal spiral of thin concrete treads,
    /// an open central well, pillared boundary openings with bright boundary light, and streamed 8 m segments.
    public static class StairwellLayout
    {
        const float Segment = DistrictLayout.FloorHeight;

        public static ChunkLayout Generate(uint seed, int cx, int cz, int layer, float ceiling)
        {
            // One continuous vertical shaft at chunk (0,0); neighbours stream empty so the void is visible.
            var L = new ChunkLayout();
            if (cx != 0 || cz != 0) return L;

            float s = MazeLayout.ChunkSize;
            float baseY = layer * Segment;
            float topY = baseY;
            float bottomY = baseY - Segment;
            var wall = new SurfaceRef(Surface.Wall, 0);
            var stepMat = new SurfaceRef(Surface.Floor, 0);
            var trim = new SurfaceRef(Surface.Trim);
            var floorMat = new SurfaceRef(Surface.Floor, 0);
            var ceilMat = new SurfaceRef(Surface.Ceiling, 0);

            void Box(Vector3 center, Vector3 size, SurfaceRef material) =>
                L.Boxes.Add(SurfaceBox.Uniform(new Bounds(center, size), material, BoxMeshBuilder.Faces.All));

            Vector2 centerXZ = new Vector2(s * 0.5f, s * 0.5f);
            const float stepInner = 1.75f;   // inner edge of the walkable spiral
            const float stepOuter = 3.35f;   // keep the spiral within the open lobby shaft
            const float boundaryR = 5.15f;   // pillars/void boundary
            const int steps = 144;           // three tightly spaced, walkable turns per 8 m segment
            const float turns = 3f;
            float rise = Segment / steps;
            float angleOffset = -Mathf.PI * 0.5f + layer * Mathf.PI * 2f * turns; // opening stays south of the lobby path; phase continues across layers

            // Concrete spiral treads around the central void. Detached side pieces are omitted so each tread reads as one step.
            for (int i = 0; i < steps; i++)
            {
                float t = (i + 0.5f) / steps;
                float a = angleOffset + t * Mathf.PI * 2f * turns;
                // The first tread is level with the lobby floor; every following tread descends one riser.
                float y = topY - i * rise;
                float midR = (stepInner + stepOuter) * 0.5f;
                float radial = stepOuter - stepInner;
                float arc = 2f * Mathf.PI * midR * turns / steps;
                float x = centerXZ.x + Mathf.Cos(a) * midR;
                float z = centerXZ.y + Mathf.Sin(a) * midR;
                // A narrow radial tread follows the curve instead of overlapping AABBs that fill the shaft.
                L.OrientedBoxes.Add(new SurfaceOrientedBox
                {
                    Center = new Vector3(x, y - 0.06f, z),
                    Size = new Vector3(radial, 0.12f, Mathf.Max(0.48f, arc + 0.08f)),
                    Yaw = -a * Mathf.Rad2Deg,
                    Surface = stepMat,
                });
            }

            // Boundary pillars with open gaps to a bright white void/light beyond (wiki: pillars block the boundary).
            const int pillars = 16;
            for (int i = 0; i < pillars; i++)
            {
                // Keep the south boundary open at the entrance; its pillar otherwise blocks the lobby-to-stair path.
                if (layer == 0 && i == pillars * 3 / 4) continue;
                float a = i * Mathf.PI * 2f / pillars;
                float nx = Mathf.Cos(a), nz = Mathf.Sin(a);
                float tx = -nz, tz = nx;
                Vector3 p = new Vector3(centerXZ.x + nx * boundaryR, (topY + bottomY) * 0.5f, centerXZ.y + nz * boundaryR);
                Vector3 tangential = new Vector3(Mathf.Abs(tx), 0f, Mathf.Abs(tz));
                Vector3 radialDir = new Vector3(Mathf.Abs(nx), 0f, Mathf.Abs(nz));
                Vector3 pSize = new Vector3(0.34f, 0f, 0.34f) + tangential * 0.42f + radialDir * 0.18f;
                pSize.y = Segment;
                Box(p, pSize, wall);

                // Bright boundary glow seen through gaps between pillars.
                float ga = a + Mathf.PI / pillars;
                Vector3 gpos = new Vector3(centerXZ.x + Mathf.Cos(ga) * (boundaryR + 0.55f), (topY + bottomY) * 0.5f, centerXZ.y + Mathf.Sin(ga) * (boundaryR + 0.55f));
                L.Panels.Add(new PanelSpec
                {
                    Position = gpos,
                    Bare = true,
                    Emitter = true,
                    State = FixtureState.Lit,
                    Range = 14f,
                    Id = (int)WorldHash.Hash(seed, cx * 1000 + layer * 97 + i, cz * 1000 + 733, 0x922),
                    Tint = new Color(1f, 1f, 1f, 1f),
                });
                // A faint pale surface behind some gaps hints at the white void beyond the pillars.
                if (i % 2 == 0)
                {
                    Vector3 voidPos = new Vector3(centerXZ.x + Mathf.Cos(ga) * (boundaryR + 0.95f), (topY + bottomY) * 0.5f, centerXZ.y + Mathf.Sin(ga) * (boundaryR + 0.95f));
                    Vector3 vTangential = new Vector3(Mathf.Abs(-Mathf.Sin(ga)), 0f, Mathf.Abs(Mathf.Cos(ga)));
                    Vector3 vRadial = new Vector3(Mathf.Abs(Mathf.Cos(ga)), 0f, Mathf.Abs(Mathf.Sin(ga)));
                    Vector3 vSize = new Vector3(0.08f, Segment, 0.08f) + vTangential * 1.35f + vRadial * 0.04f;
                    Box(voidPos, vSize, ceilMat);
                }
            }

            // Entrance: a 10 m room with an elevator and a controlled opening onto the spiral (layer 0 only).
            if (layer == 0)
            {
                const float roomHalf = 5f;
                const float roomH = 3.5f;
                float rx0 = centerXZ.x - roomHalf, rx1 = centerXZ.x + roomHalf;
                float rz0 = centerXZ.y - roomHalf, rz1 = centerXZ.y + roomHalf;

                // Solid floor and ceiling over the shaft except for a small stair opening at the spiral's top.
                // (The spiral descends continuously around the well, so a large open hole would let the player
                // step off the wrong side and fall far down the shaft.)
                float sx0 = 12.35f, sx1 = 19.65f, sz0 = 12.35f, sz1 = 19.65f; // expose the full first turn while retaining an elevator-side walkway
                L.Quads.Add(new SurfaceQuad { Area = new Rect(rx0, rz0, rx1 - rx0, sz0 - rz0), Height = 0f, FacingUp = true, Surface = floorMat });
                L.Quads.Add(new SurfaceQuad { Area = new Rect(rx0, sz1, rx1 - rx0, rz1 - sz1), Height = 0f, FacingUp = true, Surface = floorMat });
                L.Quads.Add(new SurfaceQuad { Area = new Rect(rx0, sz0, sx0 - rx0, sz1 - sz0), Height = 0f, FacingUp = true, Surface = floorMat });
                L.Quads.Add(new SurfaceQuad { Area = new Rect(sx1, sz0, rx1 - sx1, sz1 - sz0), Height = 0f, FacingUp = true, Surface = floorMat });
                L.Quads.Add(new SurfaceQuad { Area = new Rect(rx0, rz0, rx1 - rx0, rz1 - rz0), Height = roomH, FacingUp = false, Surface = ceilMat });

                const float wallT = 0.18f;
                float doorW = 1.35f;
                void RoomWall(bool alongX, float fixedAt, float from, float to, float gapCenter, bool hasGap)
                {
                    if (!hasGap)
                    {
                        var c = alongX ? new Vector3((from + to) / 2, roomH / 2, fixedAt) : new Vector3(fixedAt, roomH / 2, (from + to) / 2);
                        Box(c, alongX ? new Vector3(to - from, roomH, wallT) : new Vector3(wallT, roomH, to - from), wall);
                        return;
                    }
                    float gapMin = gapCenter - doorW / 2f, gapMax = gapCenter + doorW / 2f;
                    foreach (var seg in new[] { new Vector2(from, gapMin), new Vector2(gapMax, to) })
                    {
                        if (seg.y - seg.x <= 0.02f) continue;
                        var c = alongX ? new Vector3((seg.x + seg.y) / 2, roomH / 2, fixedAt) : new Vector3(fixedAt, roomH / 2, (seg.x + seg.y) / 2);
                        Box(c, alongX ? new Vector3(seg.y - seg.x, roomH, wallT) : new Vector3(wallT, roomH, seg.y - seg.x), wall);
                    }
                    float lintelH = roomH - 2.15f;
                    var lintel = alongX ? new Vector3(gapCenter, 2.15f + lintelH / 2, fixedAt) : new Vector3(fixedAt, 2.15f + lintelH / 2, gapCenter);
                    Box(lintel, alongX ? new Vector3(doorW, lintelH, wallT) : new Vector3(wallT, lintelH, doorW), wall);
                }

                // Elevator doorway on the east wall; solid concrete enclosure otherwise.
                RoomWall(true, rz0, rx0, rx1, 0f, false);
                RoomWall(true, rz1, rx0, rx1, 0f, false);
                RoomWall(false, rx0, rz0, rz1, 0f, false);
                RoomWall(false, rx1, rz0, rz1, centerXZ.y, true);

                // Open elevator car beyond the east doorway (exit back to Level 0).
                float frameX = rx1 + wallT / 2f;
                float postZ = doorW / 2f + 0.08f;
                Box(new Vector3(frameX, 1.2f, centerXZ.y - postZ), new Vector3(0.12f, 2.4f, 0.16f), trim);
                Box(new Vector3(frameX, 1.2f, centerXZ.y + postZ), new Vector3(0.12f, 2.4f, 0.16f), trim);
                Box(new Vector3(frameX, 2.48f, centerXZ.y), new Vector3(0.12f, 0.24f, doorW + 0.16f), trim);

                const float cabinDepth = 1.64f;
                float cabinW = doorW + 0.2f;
                float cabinX = rx1 + wallT + cabinDepth / 2f;
                Box(new Vector3(cabinX, 0.04f, centerXZ.y), new Vector3(cabinDepth, 0.08f, cabinW), floorMat);
                Box(new Vector3(cabinX, 1.2f, centerXZ.y - cabinW / 2f), new Vector3(cabinDepth, 2.4f, 0.12f), wall);
                Box(new Vector3(cabinX, 1.2f, centerXZ.y + cabinW / 2f), new Vector3(cabinDepth, 2.4f, 0.12f), wall);
                Box(new Vector3(cabinX + cabinDepth / 2f, 1.2f, centerXZ.y), new Vector3(0.12f, 2.4f, cabinW), wall);
                Box(new Vector3(cabinX, 2.43f, centerXZ.y), new Vector3(cabinDepth, 0.1f, cabinW), wall);

                var elevatorAt = new Vector3(rx1 + 0.55f, 0f, centerXZ.y);
                var elevatorPose = Matrix4x4.TRS(elevatorAt, Quaternion.LookRotation(Vector3.right), Vector3.one);
                var elevatorTrigger = new Bounds(elevatorAt + Vector3.up + Vector3.right * 0.35f, new Vector3(0.9f, 2f, 1.15f));
                L.Exits.Add(new ExitSpec { Kind = ExitKind.Elevator, Target = "0", Pose = elevatorPose, Trigger = elevatorTrigger });

                // A few pale boundary lights bleed into the entrance room for the "bright void beyond" contrast.
                for (int i = 0; i < 8; i++)
                {
                    float a = i * Mathf.PI * 2f / 8f;
                    L.Panels.Add(new PanelSpec
                    {
                        Position = new Vector3(centerXZ.x + Mathf.Cos(a) * 4.6f, 2.9f, centerXZ.y + Mathf.Sin(a) * 4.6f),
                        Bare = true,
                        Emitter = true,
                        State = FixtureState.Lit,
                        Range = 10f,
                        Id = (int)WorldHash.Hash(seed, 5000 + i, 7700, 0x9221),
                        Tint = new Color(0.98f, 0.99f, 1f, 1f),
                    });
                }
            }

            // No entities are present; the darkness, fog, and endless descent/ascent provide the atmosphere.
            return L;
        }
    }
}
