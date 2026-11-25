using UnityEngine;

namespace Backrooms.World
{
    /// Props for real places (showrooms, storage, supermarkets, clothing stores, malls, offices) and curiosities.
    public static partial class PropLibrary
    {
        /// Footprint (width along local x, depth along local z) used to keep aisles and doorways clear.
        public static Vector2 Footprint(PropKind k) => k switch
        {
            PropKind.Sofa => new Vector2(2.0f, 0.9f),
            PropKind.Armchair => new Vector2(0.9f, 0.9f),
            PropKind.CoffeeTable => new Vector2(1.1f, 0.6f),
            PropKind.Bed => new Vector2(1.6f, 2.1f),
            PropKind.DiningTable => new Vector2(1.6f, 0.9f),
            PropKind.ShelfRack => new Vector2(2.4f, 0.6f),
            PropKind.Pallet => new Vector2(1.2f, 1.0f),
            PropKind.StackedBoxes => new Vector2(1.0f, 0.8f),
            PropKind.Gondola => new Vector2(2.0f, 1.0f),
            PropKind.Checkout => new Vector2(2.0f, 0.8f),
            PropKind.Freezer => new Vector2(1.9f, 0.8f),
            PropKind.ClothingRack => new Vector2(1.5f, 0.5f),
            PropKind.ClothingRound => new Vector2(1.1f, 1.1f),
            PropKind.Counter => new Vector2(2.0f, 0.6f),
            PropKind.Bench => new Vector2(1.6f, 0.45f),
            PropKind.Planter => new Vector2(1.0f, 1.0f),
            PropKind.Fountain => new Vector2(2.6f, 2.6f),
            PropKind.ConferenceTable => new Vector2(3.0f, 1.2f),
            PropKind.BreakCounter => new Vector2(1.9f, 0.65f),
            PropKind.Slide => new Vector2(1.7f, 6.6f),
            PropKind.Floatie => new Vector2(1.2f, 1.2f),
            PropKind.PoolNoodle => new Vector2(1.9f, 0.3f),
            PropKind.RubberDuck => new Vector2(0.7f, 0.8f),
            _ => new Vector2(0.8f, 0.8f),
        };

        static bool BuildMore(PropSpec prop, BoxMeshBuilder mesh, float ceiling)
        {
            if (ModelLibrary.TryBuild(prop, mesh, ceiling)) return true;   // a Blender-made model, when there is one
            if (BuildGarage(prop, mesh, ceiling)) return true;
            if (BuildLevel3(prop, mesh, ceiling)) return true;
            if (BuildLevel4(prop, mesh, ceiling)) return true;
            if (BuildLevel5(prop, mesh, ceiling)) return true;
            if (BuildLevel7(prop, mesh, ceiling)) return true;
            if (BuildLevel9(prop, mesh, ceiling)) return true;
            var p = prop.Pose;
            int v = prop.Variant;
            void Box(Matrix4x4 m, PropPaint paint, float cx, float cy, float cz, float sx, float sy, float sz) =>
                mesh.AddOrientedBox(m, new Vector3(cx, cy, cz), new Vector3(sx, sy, sz), (int)paint);
            Matrix4x4 Sub(float x, float y, float z, float pitch, float yaw, float roll) =>
                p * Matrix4x4.TRS(new Vector3(x, y, z), Quaternion.Euler(pitch, yaw, roll), Vector3.one);
            var fabric = v % 3 == 0 ? PropPaint.Fabric : v % 3 == 1 ? PropPaint.Blue : PropPaint.Red;

            switch (prop.Kind)
            {
                // ---------- showroom ----------
                // ---------- Level 2 / Level 1 / Level 37 ----------
                case PropKind.WallPipes:   // 2-4 pipes running along a wall face, with brackets
                {
                    int n = 2 + v % 3;
                    for (int i = 0; i < n; i++)
                    {
                        float r = 0.035f + ((v + i * 3) % 4) * 0.018f;
                        float y = i < 2 ? 0.35f + i * 0.28f : ceiling - 0.3f - (i - 2) * 0.24f;
                        var paint = (v + i) % 3 == 0 ? PropPaint.Rust : PropPaint.Metal;
                        mesh.AddOrientedCylinder(p, new Vector3(0f, y, r + 0.03f), r, 2.0f, (int)paint, 10, false);
                        for (float bx = -0.5f; bx <= 0.5f; bx += 1f) Box(p, PropPaint.Dark, bx, y, (r + 0.03f) / 2f, 0.04f, r * 2.2f, r + 0.03f);
                    }
                    return true;
                }
                case PropKind.CeilingPipes:   // pipes along the ceiling that clang and leak
                    mesh.AddOrientedCylinder(p, new Vector3(0f, ceiling - 0.22f, 0f), 0.07f, 2.0f, (int)PropPaint.Metal, 10, false);
                    mesh.AddOrientedCylinder(p, new Vector3(0f, ceiling - 0.2f, 0.25f), 0.045f, 2.0f, (int)PropPaint.Rust, 8, false);
                    Box(p, PropPaint.Dark, 0f, ceiling - 0.1f, 0.12f, 0.05f, 0.2f, 0.4f);
                    return true;
                case PropKind.Boiler:   // the loud, hot machinery at a dead end
                    Box(p, PropPaint.Metal, 0, 0.85f, -0.45f, 1.2f, 1.7f, 0.85f);
                    Box(p, PropPaint.Rust, 0, 0.05f, -0.45f, 1.3f, 0.1f, 0.95f);
                    Box(p, PropPaint.Red, 0, 0.45f, -0.015f, 0.5f, 0.06f, 0.02f);   // the glowing slit
                    for (int i = -1; i <= 1; i += 2) Box(p, PropPaint.White, i * 0.3f, 1.35f, -0.01f, 0.16f, 0.16f, 0.03f);
                    mesh.AddOrientedCylinder(Sub(0.35f, 1.7f, -0.6f, 0, 0, 90f), Vector3.right * ((ceiling - 1.7f) / 2f), 0.08f, ceiling - 1.7f, (int)PropPaint.Rust, 10, false);
                    mesh.AddOrientedCylinder(Sub(-0.35f, 1.7f, -0.6f, 0, 0, 90f), Vector3.right * ((ceiling - 1.7f) / 2f), 0.06f, ceiling - 1.7f, (int)PropPaint.Metal, 10, false);
                    return true;
                case PropKind.RoundPillar:   // concrete (Level 1 Gothic) or tiled (Level 37)
                    mesh.AddOrientedCylinder(Sub(0, 0, 0, 0, 0, 90f), Vector3.right * (ceiling / 2f), v == 1 ? 0.32f : 0.42f, ceiling, (int)PropPaint.White, 16, false);
                    return true;

                // ---------- Level 0.3: The Icy Rooms ----------
                case PropKind.IceBlock:   // the pose's scale is the block's size
                    Box(p, PropPaint.Ice, 0, 0.5f, 0, 1f, 1f, 1f);
                    return true;
                case PropKind.Duct:   // a rusty rectangular duct under the ceiling, a grille every few metres
                    Box(p, PropPaint.Rust, 0, ceiling - 0.22f, 0, 2.0f, 0.3f, 0.45f);
                    Box(p, PropPaint.Metal, 0.98f, ceiling - 0.22f, 0, 0.04f, 0.34f, 0.49f);
                    if (v % 3 == 0) Box(p, PropPaint.Dark, 0, ceiling - 0.375f, 0, 0.4f, 0.01f, 0.25f);
                    return true;

                // ---------- signs of people ----------
                case PropKind.SleepingBag:   // unrolled, slept in, a rolled jacket for a pillow
                    Box(p, fabric, 0, 0.06f, 0, 0.8f, 0.12f, 2.0f);
                    Box(Sub(0.05f, 0.13f, 0.3f, 0, 4f, 0), fabric, 0, 0, 0, 0.7f, 0.04f, 0.9f);
                    Box(Sub(0, 0.11f, -0.85f, 0, -8f, 0), PropPaint.Dark, 0, 0, 0, 0.55f, 0.14f, 0.28f);
                    return true;
                case PropKind.Lantern:   // a camping lantern, still glowing faintly
                    Box(p, PropPaint.Metal, 0, 0.02f, 0, 0.16f, 0.04f, 0.16f);
                    Box(p, PropPaint.Glow, 0, 0.13f, 0, 0.11f, 0.18f, 0.11f);
                    Box(p, PropPaint.Metal, 0, 0.24f, 0, 0.16f, 0.03f, 0.16f);
                    Box(p, PropPaint.Metal, 0, 0.3f, 0, 0.1f, 0.012f, 0.012f);
                    return true;
                case PropKind.Can:   // an empty food can, lying on its side or standing
                    if (v % 2 == 0) Box(p, PropPaint.Metal, 0, 0.055f, 0, 0.075f, 0.11f, 0.075f);
                    else Box(Sub(0, 0.038f, 0, 0, 0, 90f), PropPaint.Metal, 0, 0, 0, 0.075f, 0.11f, 0.075f);
                    return true;

                // ---------- Level -1: the data center ----------
                case PropKind.ServerRack:   // 0.6 m wide, 2.1 m tall; front (+z) is a grille with blinking lights
                    Box(p, PropPaint.Dark, 0, 1.05f, 0, 0.6f, 2.1f, 1.0f);
                    for (int i = 0; i < 9; i++)
                    {
                        Box(p, PropPaint.Metal, 0, 0.2f + i * 0.21f, 0.502f, 0.54f, 0.015f, 0.005f);
                        var led = (i + v) % 3 == 0 ? PropPaint.Blue : PropPaint.Green;
                        Box(p, led, -0.2f + ((i * 7 + v) % 4) * 0.05f, 0.27f + i * 0.21f, 0.506f, 0.025f, 0.015f, 0.006f);
                        if ((i + v) % 2 == 0) Box(p, PropPaint.Glow, 0.18f, 0.27f + i * 0.21f, 0.506f, 0.02f, 0.012f, 0.006f);
                    }
                    // Its piece of the cable tray over the row (racks stand edge to edge, so the trays join up),
                    // with a bundle of cables in it and a drop down into the rack.
                    if (ceiling > 2.6f)
                    {
                        float ty = ceiling - 0.35f;
                        Box(p, PropPaint.Metal, 0, ty, 0, 0.62f, 0.02f, 0.4f);
                        Box(p, PropPaint.Metal, 0, ty + 0.05f, 0.2f, 0.62f, 0.1f, 0.015f);
                        Box(p, PropPaint.Metal, 0, ty + 0.05f, -0.2f, 0.62f, 0.1f, 0.015f);
                        Box(p, (v & 1) == 0 ? PropPaint.Blue : PropPaint.Dark, 0, ty + 0.05f, 0, 0.62f, 0.07f, 0.3f);
                        Box(p, PropPaint.Dark, 0.15f, (2.1f + ty) / 2f, 0, 0.06f, ty - 2.1f, 0.06f);
                    }
                    return true;
                case PropKind.ControlConsole:   // the pump controls: a long desk, three screens of static, a red lever
                    Box(p, PropPaint.Metal, 0, 0.42f, 0, 2.2f, 0.84f, 0.8f);
                    Box(Sub(0, 0.92f, 0.1f, -25f, 0, 0), PropPaint.Dark, 0, 0, 0, 2.2f, 0.06f, 0.6f);
                    for (int i = -1; i <= 1; i++)
                    {
                        Box(p, PropPaint.Dark, i * 0.7f, 1.35f, -0.25f, 0.62f, 0.42f, 0.06f);
                        Box(p, PropPaint.Static, i * 0.7f, 1.35f, -0.215f, 0.56f, 0.36f, 0.01f);
                    }
                    Box(p, PropPaint.Metal, 0.9f, 1.05f, 0.25f, 0.06f, 0.3f, 0.06f);
                    Box(p, PropPaint.Red, 0.9f, 1.22f, 0.25f, 0.12f, 0.08f, 0.12f);
                    return true;
                case PropKind.Sofa:
                    Box(p, fabric, 0, 0.22f, 0, 2.0f, 0.24f, 0.9f);
                    Box(p, fabric, 0, 0.42f, 0.05f, 1.7f, 0.16f, 0.75f);
                    Box(p, fabric, 0, 0.62f, -0.36f, 2.0f, 0.6f, 0.18f);
                    Box(p, fabric, 0.92f, 0.45f, 0, 0.16f, 0.4f, 0.9f);
                    Box(p, fabric, -0.92f, 0.45f, 0, 0.16f, 0.4f, 0.9f);
                    return true;
                case PropKind.Armchair:
                    Box(p, fabric, 0, 0.22f, 0, 0.9f, 0.24f, 0.9f);
                    Box(p, fabric, 0, 0.42f, 0.05f, 0.6f, 0.16f, 0.75f);
                    Box(p, fabric, 0, 0.65f, -0.36f, 0.9f, 0.65f, 0.18f);
                    Box(p, fabric, 0.37f, 0.45f, 0, 0.16f, 0.42f, 0.9f);
                    Box(p, fabric, -0.37f, 0.45f, 0, 0.16f, 0.42f, 0.9f);
                    return true;
                case PropKind.CoffeeTable:
                    Box(p, PropPaint.Wood, 0, 0.42f, 0, 1.1f, 0.05f, 0.6f);
                    foreach (float x in new[] { -0.5f, 0.5f })
                    foreach (float z in new[] { -0.25f, 0.25f })
                        Box(p, PropPaint.Wood, x, 0.2f, z, 0.05f, 0.4f, 0.05f);
                    return true;
                case PropKind.Rug:
                    Box(p, v % 2 == 0 ? PropPaint.Red : PropPaint.Fabric, 0, 0.006f, 0, 2.4f, 0.01f, 1.7f);
                    return true;
                case PropKind.FloorLamp:
                    Box(p, PropPaint.Dark, 0, 0.02f, 0, 0.3f, 0.04f, 0.3f);
                    Box(p, PropPaint.Metal, 0, 0.8f, 0, 0.03f, 1.6f, 0.03f);
                    Box(p, PropPaint.White, 0, 1.6f, 0, 0.4f, 0.3f, 0.4f);
                    return true;
                case PropKind.Bed:
                    Box(p, PropPaint.Wood, 0, 0.2f, 0, 1.6f, 0.3f, 2.1f);
                    Box(p, PropPaint.White, 0, 0.45f, 0.05f, 1.5f, 0.2f, 1.95f);
                    Box(p, fabric, 0, 0.56f, 0.25f, 1.52f, 0.04f, 1.4f);
                    Box(p, PropPaint.White, 0, 0.6f, -0.75f, 1.3f, 0.12f, 0.35f);
                    Box(p, PropPaint.Wood, 0, 0.6f, -1.03f, 1.6f, 1.0f, 0.06f);
                    return true;
                case PropKind.Nightstand:
                    Box(p, PropPaint.Wood, 0, 0.28f, 0, 0.5f, 0.56f, 0.45f);
                    Box(p, PropPaint.Metal, 0, 0.42f, 0.23f, 0.12f, 0.02f, 0.02f);
                    return true;
                case PropKind.DiningTable:
                    Box(p, PropPaint.Wood, 0, 0.74f, 0, 1.6f, 0.05f, 0.9f);
                    foreach (float x in new[] { -0.72f, 0.72f })
                    foreach (float z in new[] { -0.38f, 0.38f })
                        Box(p, PropPaint.Wood, x, 0.36f, z, 0.06f, 0.72f, 0.06f);
                    return true;
                case PropKind.Bookshelf:
                    Box(p, PropPaint.Wood, 0, 0.9f, 0.17f, 1.0f, 1.8f, 0.04f);
                    Box(p, PropPaint.Wood, 0.48f, 0.9f, 0.19f, 0.04f, 1.8f, 0.34f);
                    Box(p, PropPaint.Wood, -0.48f, 0.9f, 0.19f, 0.04f, 1.8f, 0.34f);
                    for (int i = 0; i < 5; i++) Box(p, PropPaint.Wood, 0, 0.05f + 0.43f * i, 0.19f, 0.96f, 0.03f, 0.34f);
                    return true;
                case PropKind.PriceTag:
                    Box(p, PropPaint.Metal, 0, 0.5f, 0, 0.03f, 1.0f, 0.03f);
                    Box(Sub(0, 1.05f, 0, -20, 0, 0), PropPaint.Yellow, 0, 0, 0, 0.3f, 0.2f, 0.01f);
                    Box(p, PropPaint.Dark, 0, 0.01f, 0, 0.25f, 0.02f, 0.25f);
                    return true;

                // ---------- storage ----------
                case PropKind.ShelfRack:
                    foreach (float x in new[] { -1.18f, 1.18f })
                    foreach (float z in new[] { -0.28f, 0.28f })
                        Box(p, PropPaint.Blue, x, 1.1f, z, 0.06f, 2.2f, 0.06f);
                    for (int i = 0; i < 4; i++) Box(p, PropPaint.Metal, 0, 0.15f + 0.65f * i, 0, 2.4f, 0.04f, 0.6f);
                    for (int i = 0; i < 3; i++)
                        if ((v >> i & 1) == 1) Box(Sub(-0.6f + 0.6f * i, 0.17f + 0.65f * (v % 3), 0, 0, 7 * i, 0), PropPaint.Cardboard, 0, 0.2f, 0, 0.5f, 0.4f, 0.45f);
                    return true;
                case PropKind.Pallet:
                    Box(p, PropPaint.Wood, 0, 0.13f, 0, 1.2f, 0.03f, 1.0f);
                    for (int i = -1; i <= 1; i++) Box(p, PropPaint.Wood, 0.5f * i, 0.06f, 0, 0.1f, 0.12f, 1.0f);
                    if (v % 2 == 0) Box(p, PropPaint.Cardboard, 0, 0.45f, 0, 1.0f, 0.6f, 0.8f);
                    return true;
                case PropKind.StackedBoxes:
                    for (int i = 0; i < 2 + v % 3; i++)
                        Box(Sub(0.05f * (i % 2), 0.4f * i, 0, 0, 8 * i - 6, 0), PropPaint.Cardboard, 0, 0.2f, 0, 0.9f - 0.1f * (i % 2), 0.4f, 0.7f);
                    return true;
                case PropKind.RollUpDoor:   // a loading door on the wall, shut
                    Box(p, PropPaint.Metal, 0, 1.4f, 0.03f, 1.8f, 2.8f, 0.04f);
                    for (int i = 0; i < 12; i++) Box(p, PropPaint.Dark, 0, 0.12f + 0.23f * i, 0.055f, 1.8f, 0.012f, 0.01f);
                    Box(p, PropPaint.Yellow, 0, 0.05f, 0.06f, 1.9f, 0.1f, 0.02f);
                    return true;

                // ---------- supermarket ----------
                case PropKind.Gondola:   // double-sided shelving, mostly empty
                    Box(p, PropPaint.White, 0, 0.8f, 0, 2.0f, 1.6f, 0.06f);
                    Box(p, PropPaint.White, 0, 0.06f, 0, 2.0f, 0.12f, 1.0f);
                    for (int i = 1; i < 4; i++)
                    {
                        Box(p, PropPaint.Metal, 0, 0.12f + 0.4f * i, 0.25f, 2.0f, 0.03f, 0.45f);
                        Box(p, PropPaint.Metal, 0, 0.12f + 0.4f * i, -0.25f, 2.0f, 0.03f, 0.45f);
                    }
                    if (v % 4 == 0) Box(Sub(0.4f, 0.55f, 0.25f, 0, 10, 0), PropPaint.Cardboard, 0, 0.1f, 0, 0.3f, 0.2f, 0.25f);
                    return true;
                case PropKind.Checkout:
                    Box(p, PropPaint.White, 0, 0.45f, 0, 2.0f, 0.9f, 0.8f);
                    Box(p, PropPaint.Dark, -0.3f, 0.91f, 0, 1.3f, 0.02f, 0.45f);
                    Box(p, PropPaint.Dark, 0.75f, 1.05f, 0.1f, 0.3f, 0.25f, 0.25f);
                    Box(p, PropPaint.Metal, 0.75f, 1.6f, -0.3f, 0.04f, 1.2f, 0.04f);
                    Box(p, PropPaint.Glow, 0.75f, 2.2f, -0.3f, 0.25f, 0.12f, 0.04f);
                    return true;
                case PropKind.Freezer:   // glass-front case against the wall, dark inside
                    Box(p, PropPaint.White, 0, 0.45f, 0.4f, 1.9f, 0.9f, 0.8f);
                    Box(p, PropPaint.Void, 0, 0.92f, 0.4f, 1.8f, 0.02f, 0.7f);
                    Box(p, PropPaint.Glass, 0, 0.95f, 0.4f, 1.85f, 0.02f, 0.75f);
                    return true;

                // ---------- clothing store ----------
                case PropKind.ClothingRack:
                    Box(p, PropPaint.Metal, 0, 1.4f, 0, 1.5f, 0.03f, 0.03f);
                    Box(p, PropPaint.Metal, 0.73f, 0.7f, 0, 0.03f, 1.4f, 0.03f);
                    Box(p, PropPaint.Metal, -0.73f, 0.7f, 0, 0.03f, 1.4f, 0.03f);
                    Box(p, PropPaint.Metal, 0, 0.02f, 0, 1.5f, 0.03f, 0.5f);
                    for (int i = 0; i < v % 5; i++) Box(p, PropPaint.Metal, -0.6f + 0.25f * i, 1.3f, 0, 0.01f, 0.2f, 0.35f);   // empty hangers
                    return true;
                case PropKind.ClothingRound:
                    Box(p, PropPaint.Metal, 0, 1.2f, 0, 1.1f, 0.03f, 0.03f);
                    Box(p, PropPaint.Metal, 0, 1.2f, 0, 0.03f, 0.03f, 1.1f);
                    Box(Sub(0, 0, 0, 0, 45, 0), PropPaint.Metal, 0, 1.2f, 0, 1.1f, 0.03f, 0.03f);
                    Box(p, PropPaint.Metal, 0, 0.6f, 0, 0.04f, 1.2f, 0.04f);
                    Box(p, PropPaint.Metal, 0, 0.02f, 0, 0.5f, 0.03f, 0.5f);
                    return true;
                case PropKind.FittingCurtain:
                    Box(p, PropPaint.Metal, 0, 2.0f, 0, 1.0f, 0.02f, 0.02f);
                    Box(Sub(0.2f, 0, 0, 0, 0, 0), fabric, 0, 1.05f, 0, 0.6f, 1.9f, 0.02f);
                    return true;
                case PropKind.Counter:
                    Box(p, PropPaint.Wood, 0, 0.5f, 0, 2.0f, 1.0f, 0.6f);
                    Box(p, PropPaint.White, 0, 1.02f, 0, 2.05f, 0.04f, 0.65f);
                    if (v % 2 == 0) Box(p, PropPaint.Dark, 0.5f, 1.15f, 0, 0.35f, 0.22f, 0.3f);   // a dead register
                    return true;

                // ---------- mall ----------
                case PropKind.Bench:
                    Box(p, PropPaint.Wood, 0, 0.45f, 0, 1.6f, 0.05f, 0.45f);
                    Box(p, PropPaint.Metal, 0.7f, 0.22f, 0, 0.06f, 0.44f, 0.4f);
                    Box(p, PropPaint.Metal, -0.7f, 0.22f, 0, 0.06f, 0.44f, 0.4f);
                    return true;
                case PropKind.Planter:   // dry soil, nothing growing
                    Box(p, PropPaint.Metal, 0, 0.35f, 0, 1.0f, 0.7f, 1.0f);
                    Box(p, PropPaint.Dark, 0, 0.66f, 0, 0.9f, 0.04f, 0.9f);
                    if (v % 3 == 0) Box(Sub(0.1f, 0.7f, 0, 0, 0, 25), PropPaint.Wood, 0, 0.3f, 0, 0.03f, 0.6f, 0.03f);
                    return true;
                case PropKind.Fountain:   // a dead fountain
                    Box(p, PropPaint.White, 0, 0.25f, 0, 2.6f, 0.5f, 1.1f);
                    Box(Sub(0, 0, 0, 0, 90, 0), PropPaint.White, 0, 0.25f, 0, 2.6f, 0.5f, 1.1f);
                    Box(Sub(0, 0, 0, 0, 45, 0), PropPaint.White, 0, 0.25f, 0, 2.2f, 0.5f, 1.5f);
                    Box(p, PropPaint.Dark, 0, 0.51f, 0, 1.8f, 0.02f, 1.8f);
                    Box(p, PropPaint.White, 0, 0.9f, 0, 0.3f, 0.8f, 0.3f);
                    return true;
                case PropKind.Shutter:   // storefront grille, rolled up near the top
                    Box(p, PropPaint.Metal, 0, 2.35f, 0.15f, 1.96f, 0.3f, 0.3f);
                    for (int i = 0; i < 6; i++) Box(p, PropPaint.Metal, 0, 2.18f - 0.06f * i, 0.04f, 1.9f, 0.02f, 0.02f);
                    return true;

                // ---------- offices ----------
                case PropKind.Copier:
                    Box(p, PropPaint.White, 0, 0.5f, 0, 0.7f, 1.0f, 0.6f);
                    Box(p, PropPaint.Dark, 0, 1.02f, 0, 0.6f, 0.04f, 0.5f);
                    Box(p, PropPaint.White, -0.42f, 0.8f, 0, 0.15f, 0.04f, 0.35f);
                    return true;
                case PropKind.BreakCounter:   // cabinets, counter, a sink
                    Box(p, PropPaint.White, 0, 0.43f, 0.32f, 1.9f, 0.86f, 0.62f);
                    Box(p, PropPaint.Dark, 0, 0.88f, 0.33f, 1.95f, 0.04f, 0.65f);
                    Box(p, PropPaint.Metal, 0.4f, 0.89f, 0.35f, 0.5f, 0.02f, 0.4f);
                    Box(p, PropPaint.White, 0, 1.7f, 0.18f, 1.9f, 0.7f, 0.35f);
                    return true;
                case PropKind.Fridge:
                    Box(p, PropPaint.White, 0, 0.85f, 0.37f, 0.8f, 1.7f, 0.72f);
                    Box(p, PropPaint.Metal, 0.33f, 1.1f, 0.74f, 0.03f, 0.4f, 0.03f);
                    return true;
                case PropKind.ConferenceTable:
                    Box(p, PropPaint.Wood, 0, 0.74f, 0, 3.0f, 0.06f, 1.2f);
                    Box(p, PropPaint.Dark, 0.9f, 0.36f, 0, 0.12f, 0.72f, 0.6f);
                    Box(p, PropPaint.Dark, -0.9f, 0.36f, 0, 0.12f, 0.72f, 0.6f);
                    return true;
                case PropKind.Whiteboard:
                    Box(p, PropPaint.White, 0, 1.4f, 0.015f, 1.6f, 1.0f, 0.03f);
                    Box(p, PropPaint.Metal, 0, 0.88f, 0.05f, 1.6f, 0.03f, 0.08f);
                    return true;

                // ---------- curiosities and wrongness ----------
                case PropKind.CrtTv:   // an old TV on a cart, showing static
                    Box(p, PropPaint.Dark, 0, 0.35f, 0, 0.6f, 0.7f, 0.5f);
                    Box(p, PropPaint.Dark, 0, 0.98f, 0, 0.55f, 0.48f, 0.5f);
                    Box(p, PropPaint.Static, 0, 0.98f, 0.255f, 0.42f, 0.34f, 0.01f);
                    return true;
                case PropKind.Phone:   // a desk phone on the floor, cord trailing off
                    Box(p, PropPaint.Dark, 0, 0.04f, 0, 0.22f, 0.08f, 0.2f);
                    Box(Sub(0, 0.09f, 0, 0, 0, 4), PropPaint.Dark, 0, 0, 0, 0.22f, 0.04f, 0.06f);
                    Box(p, PropPaint.Dark, 0, 0.005f, -0.6f, 0.01f, 0.01f, 1.0f);
                    return true;
                case PropKind.LoneDoor:   // a door frame standing in the open, closed door in it
                    DoorFrame(p, mesh, PropPaint.Wood);
                    DoorFrame(p * Matrix4x4.TRS(new Vector3(0, 0, -0.02f), Quaternion.Euler(0, 180, 0), Vector3.one), mesh, PropPaint.Wood);
                    return true;
                case PropKind.TilePile:
                    for (int i = 0; i < 6 + v % 5; i++)
                        Box(Sub(0.3f * Mathf.Sin(i * 2.3f), 0.025f * i, 0.3f * Mathf.Cos(i * 1.7f), 3 * (i % 3), 37 * i, 2 * (i % 2)), PropPaint.White, 0, 0, 0, 0.5f, 0.02f, 0.5f);
                    return true;
                case PropKind.StairsToCeiling:   // a staircase that climbs straight into the ceiling
                {
                    int steps = Mathf.CeilToInt(ceiling / 0.2f) + 1;
                    for (int i = 0; i < steps; i++)
                        Box(p, PropPaint.White, 0, 0.1f + 0.1f * i, 0.25f * i + 0.125f, 1.0f, 0.2f + 0.2f * i, 0.25f);
                    return true;
                }
                case PropKind.WindowToWall:   // a window whose view is more wall
                    Box(p, PropPaint.White, 0, 1.5f, 0.02f, 1.2f, 0.06f, 0.04f);
                    Box(p, PropPaint.White, 0, 0.9f, 0.02f, 1.2f, 0.06f, 0.04f);
                    Box(p, PropPaint.White, 0.58f, 1.2f, 0.02f, 0.06f, 0.66f, 0.04f);
                    Box(p, PropPaint.White, -0.58f, 1.2f, 0.02f, 0.06f, 0.66f, 0.04f);
                    Box(p, PropPaint.White, 0, 1.2f, 0.02f, 0.03f, 0.6f, 0.03f);
                    Box(p, PropPaint.White, 0, 0.88f, 0.08f, 1.3f, 0.03f, 0.14f);
                    Box(p, PropPaint.Glass, 0, 1.2f, 0.035f, 1.1f, 0.56f, 0.005f);
                    return true;
                case PropKind.OddOutlet:   // an outlet where no outlet should be
                    Box(p, PropPaint.White, 0, v % 2 == 0 ? 2.45f : 1.75f, 0.01f, 0.08f, 0.12f, 0.02f);
                    return true;
                case PropKind.Tripod:   // a compact survey camera stand left behind
                    foreach (float yaw in new[] { 0f, 120f, 240f })
                        Box(Sub(0f, 0f, 0f, 22f, yaw, 0f), PropPaint.Metal, 0f, 0.42f, 0f, 0.045f, 0.92f, 0.045f);
                    Box(p, PropPaint.Dark, 0f, 0.83f, 0f, 0.24f, 0.07f, 0.18f);
                    Box(p, PropPaint.Glass, 0f, 0.84f, 0.095f, 0.1f, 0.05f, 0.015f);
                    return true;

                // ---------- Level 37 waterpark ----------
                case PropKind.Slide:
                {
                    // A real chute: the ramp and rails are AddSlope surfaces inside the solid Props mesh, so the
                    // chunk's MeshCollider carries a walkable top (about 21 degrees, under the 45-degree limit).
                    var chute = (v % 4) switch { 0 => PropPaint.Yellow, 1 => PropPaint.Red, 2 => PropPaint.Blue, _ => PropPaint.Green };
                    Vector3 A = p.MultiplyPoint3x4(new Vector3(0f, 2.5f, -3.1f));
                    Vector3 B = p.MultiplyPoint3x4(new Vector3(0f, 0.1f, 3.1f));
                    mesh.AddSlope(A, B, 1.5f, 0f, 0.14f, (int)chute, (int)PropPaint.White);
                    foreach (float side in new[] { -0.81f, 0.81f })
                    {
                        Vector3 rA = p.MultiplyPoint3x4(new Vector3(side, 2.85f, -3.1f));
                        Vector3 rB = p.MultiplyPoint3x4(new Vector3(side, 0.45f, 3.1f));
                        mesh.AddSlope(rA, rB, 0.12f, 0f, 0.42f, (int)PropPaint.White, (int)PropPaint.White);
                    }
                    Box(p, PropPaint.White, 0f, 2.44f, -3.55f, 1.7f, 0.14f, 1.1f);   // launch platform
                    foreach (float sx in new[] { -0.72f, 0.72f })
                        Box(p, PropPaint.Metal, sx, 1.22f, -3.55f, 0.1f, 2.44f, 0.1f);
                    foreach (float sx in new[] { -0.45f, 0.45f })                       // ladder up the back
                        Box(p, PropPaint.Metal, sx, 1.25f, -4.1f, 0.06f, 2.5f, 0.06f);
                    for (int i = 0; i < 10; i++)   // 0.25 m rungs: under the 0.3 m character step offset
                        Box(p, PropPaint.Dark, 0f, 0.22f + i * 0.25f, -4.1f, 0.9f, 0.06f, 0.06f);
                    return true;
                }
                case PropKind.Floatie:
                {
                    bool stripes = (v & 1) == 0;
                    var body = ((v / 2) % 3) switch { 0 => PropPaint.Red, 1 => PropPaint.Blue, _ => PropPaint.Green };
                    float r = 0.5f + (v % 3) * 0.07f;
                    mesh.AddTorus(p, Vector3.zero, r, 0.15f, (int)body, 18, 8, stripes ? (int)PropPaint.White : -1);
                    mesh.AddOrientedBox(p, new Vector3(r, 0.14f, 0f), new Vector3(0.1f, 0.03f, 0.1f), (int)PropPaint.Dark);   // valve
                    return true;
                }
                case PropKind.PoolNoodle:
                {
                    var colour = (v % 4) switch { 0 => PropPaint.Blue, 1 => PropPaint.Red, 2 => PropPaint.Yellow, _ => PropPaint.Green };
                    float len = 1.7f + (v % 3) * 0.15f;
                    mesh.AddOrientedCylinder(p, Vector3.zero, 0.06f, len, (int)colour, 10);
                    foreach (float s in new[] { -1f, 1f })
                        mesh.AddSphere(p.MultiplyPoint3x4(new Vector3(s * len / 2f, 0f, 0f)), new Vector3(0.06f, 0.06f, 0.06f), (int)colour, 6, 8);
                    return true;
                }
                case PropKind.RubberDuck:
                {
                    float sc = p.GetColumn(0).magnitude;
                    Vector3 R(float x, float y, float z) => new Vector3(x, y, z) * sc;
                    mesh.AddSphere(p.MultiplyPoint3x4(new Vector3(0f, 0.17f, 0f)), R(0.23f, 0.18f, 0.26f), (int)PropPaint.Yellow, 8, 10);
                    mesh.AddSphere(p.MultiplyPoint3x4(new Vector3(0f, 0.42f, 0.2f)), R(0.15f, 0.15f, 0.15f), (int)PropPaint.Yellow, 8, 10);
                    Box(p, PropPaint.Orange, 0f, 0.4f, 0.35f, 0.13f, 0.06f, 0.14f);                 // beak
                    foreach (float sx in new[] { -0.085f, 0.085f })
                        mesh.AddSphere(p.MultiplyPoint3x4(new Vector3(sx, 0.47f, 0.29f)), R(0.022f, 0.022f, 0.022f), (int)PropPaint.Dark, 5, 6);
                    Box(p, PropPaint.Yellow, 0f, 0.28f, -0.26f, 0.12f, 0.13f, 0.1f);               // tail
                    return true;
                }
            }
            return false;
        }
    }
}
