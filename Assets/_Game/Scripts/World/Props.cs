using UnityEngine;

namespace Backrooms.World
{
    public enum PropKind
    {
        OfficeChair, Desk, FilingCabinet, CardboardBox, TrashCan,                          // office leftovers
        WetFloorSign, Bucket, FallenTile, Ladder, HangingWires,                             // maintenance mess
        Outlet, LightSwitch, Vent, ExitSign, DoorToNowhere,                                 // wall details
        AlmondWater, Battery,                                                               // supplies
        Crate, Pipes, Puddle,                                                               // Level 1 / 2
        OctagonTable, WoodChair, WoodFloor,                                                 // Manila Room
        Pit,                                                                                // Level 0 "Holes"
        Sofa, Armchair, CoffeeTable, Rug, FloorLamp, Bed, Nightstand, DiningTable, Bookshelf, PriceTag,   // showroom
        ShelfRack, Pallet, StackedBoxes, RollUpDoor,                                        // storage
        Gondola, Checkout, Freezer,                                                         // supermarket
        ClothingRack, ClothingRound, FittingCurtain, Counter,                               // clothing store
        Bench, Planter, Fountain, Shutter,                                                  // mall
        Copier, BreakCounter, Fridge, ConferenceTable, Whiteboard,                          // offices
        CrtTv, Phone, LoneDoor, TilePile, StairsToCeiling, WindowToWall, OddOutlet,         // curiosities and wrongness
        ServerRack, ControlConsole,                                                         // Level -1 data center
        SleepingBag, Lantern, Can,                                                          // signs of people: an old camp
        IceBlock, Duct,                                                                     // Level 0.3 The Icy Rooms
        WallPipes, CeilingPipes, Boiler, RoundPillar,                                       // Level 2 / Level 1 / Level 37
        Slide, Floatie, PoolNoodle, RubberDuck,                                              // Level 37 waterpark
        WallLadder, WallLamp, StallLines, WheelStop, ParkingSign, PillarNumber, Barrier,    // Level 1 parking garage
        SpeedBump, OilStain, TarpCar, OldCar,
        Scaffold, WorkLamp, TrafficCone, PlasticSheet, BlockWall,                           // Ouroboros sector
        Vines, Plant, MossPatch,                                                            // Garden sector
        NeonStrip, Cables,                                                                  // Fabled sector
        Tripod,                                                                              // Level 0 survey equipment
        Generator, Transformer, Conveyor, ConveyorMotor, BreakerPanel, SludgePipe, Plinth,  // Level 3 (Blender models)
        CableTray, CageBulb, SludgePool, BarFence,                                          // Level 3 (built here)
        OfficeWindow, WaterCooler, VendingMachine, DrinkFountain,                           // Level 4 (Blender models)
        CarpetDent, FloorOutlet, BadgeReader, MoldTray,                                     // Level 4 (built here)
        Chandelier, CageElevator, Sconce, Gramophone, MahjongTable, HotelBed,              // Level 5 (Blender models)
        Bones, TarMound, Beacon,                                                            // Level 7 (built here)
        Glowstick, Exterminator,                                                            // throwables (Smiler lure, wiki Object 3)
        FirstAid,                                                                           // heals Hound wounds
        Repellent,                                                                          // Deathmoth repellent can
        Flare,                                                                              // road flare (sets a Partygoer alight)
        BoatWreck, RoadMarker,                                                              // Level 7's boats, Level 8's 9th Road posts
        StreetLamp, Mailbox, Toilet, Bathtub, Stove, SwingSet, Grill, WheelieBin,           // Level 9 (Blender models)
        HouseWindow, PlankFence, ChainFence, ArrowSign,                                     // Level 9 (built here)
        KeyRack, PinnedPapers, KnotRail, TapeMachine,                                       // Level 9's three story houses
    }

    /// One material per paint; the order is the submesh order of the prop meshes.
    public enum PropPaint { Metal, Dark, Wood, Cardboard, White, Yellow, Red, Blue, Green, Glow, Water, Void, Fabric, Glass, Static, Ice, Rust, Oil, Warm, NeonPink, NeonCyan, Leaf, Tarp, Concrete, Orange, Brass, Mahogany }

    public struct PropSpec
    {
        public PropKind Kind;
        public Matrix4x4 Pose;   // origin on the floor; for wall props, local +z points away from the wall
        public int Variant;
        public float Ceiling;    // ceiling height above this prop (0 = the level default)
    }

    /// Props assembled from boxes, like the walls. shortcut: blocky placeholders; detailed models come from Blender later.
    public static partial class PropLibrary
    {
        static readonly System.Collections.Generic.HashSet<PropKind> WallProps = new System.Collections.Generic.HashSet<PropKind>
        {
            PropKind.Desk, PropKind.FilingCabinet, PropKind.Ladder, PropKind.Outlet, PropKind.LightSwitch, PropKind.Vent,
            PropKind.ExitSign, PropKind.DoorToNowhere, PropKind.Pipes, PropKind.Bookshelf, PropKind.RollUpDoor, PropKind.Freezer,
            PropKind.Shutter, PropKind.BreakCounter, PropKind.Fridge, PropKind.Whiteboard, PropKind.WindowToWall, PropKind.OddOutlet,
            PropKind.BreakerPanel, PropKind.SludgePipe,
            PropKind.OfficeWindow, PropKind.WaterCooler, PropKind.VendingMachine, PropKind.DrinkFountain, PropKind.BadgeReader,
            PropKind.Sconce,
        };

        static readonly System.Collections.Generic.HashSet<PropKind> SolidProps = new System.Collections.Generic.HashSet<PropKind>
        {
            PropKind.OfficeChair, PropKind.Desk, PropKind.FilingCabinet, PropKind.CardboardBox, PropKind.TrashCan, PropKind.WetFloorSign,
            PropKind.Bucket, PropKind.Ladder, PropKind.Crate, PropKind.OctagonTable, PropKind.WoodChair, PropKind.Sofa, PropKind.Armchair,
            PropKind.CoffeeTable, PropKind.FloorLamp, PropKind.Bed, PropKind.Nightstand, PropKind.DiningTable, PropKind.Bookshelf,
            PropKind.PriceTag, PropKind.ShelfRack, PropKind.Pallet, PropKind.StackedBoxes, PropKind.Gondola, PropKind.Checkout,
            PropKind.Freezer, PropKind.ClothingRack, PropKind.ClothingRound, PropKind.Counter, PropKind.Bench, PropKind.Planter,
            PropKind.Fountain, PropKind.Copier, PropKind.BreakCounter, PropKind.Fridge, PropKind.ConferenceTable, PropKind.CrtTv,
            PropKind.LoneDoor, PropKind.StairsToCeiling, PropKind.ServerRack, PropKind.ControlConsole, PropKind.IceBlock, PropKind.Boiler, PropKind.RoundPillar,
            PropKind.Slide, PropKind.Floatie, PropKind.PoolNoodle, PropKind.RubberDuck,
            PropKind.Barrier, PropKind.TarpCar, PropKind.OldCar, PropKind.Scaffold, PropKind.WorkLamp, PropKind.TrafficCone, PropKind.BlockWall,
            PropKind.Tripod, PropKind.BoatWreck,
            PropKind.StreetLamp, PropKind.Mailbox, PropKind.Toilet, PropKind.Bathtub, PropKind.Stove, PropKind.SwingSet, PropKind.Grill, PropKind.WheelieBin,
            PropKind.HouseWindow, PropKind.PlankFence, PropKind.ChainFence,
            PropKind.Generator, PropKind.Transformer, PropKind.Conveyor, PropKind.ConveyorMotor, PropKind.Plinth, PropKind.BarFence,
            PropKind.WaterCooler, PropKind.VendingMachine,
            PropKind.CageElevator, PropKind.Gramophone, PropKind.MahjongTable, PropKind.HotelBed,
        };

        public static bool IsWallProp(PropKind k) => WallProps.Contains(k);

        /// Solid props get collision; decor (flat, tiny, on walls or ceilings) doesn't.
        public static bool IsSolid(PropKind k) => SolidProps.Contains(k);

        public static void Build(PropSpec prop, BoxMeshBuilder mesh, float ceiling)
        {
            if (prop.Ceiling > 0f) ceiling = prop.Ceiling;
            if (BuildMore(prop, mesh, ceiling)) return;
            var p = prop.Pose;
            int v = prop.Variant;
            void Box(Matrix4x4 m, PropPaint paint, float cx, float cy, float cz, float sx, float sy, float sz) =>
                mesh.AddOrientedBox(m, new Vector3(cx, cy, cz), new Vector3(sx, sy, sz), (int)paint);
            Matrix4x4 Sub(float x, float y, float z, float pitch, float yaw, float roll) =>
                p * Matrix4x4.TRS(new Vector3(x, y, z), Quaternion.Euler(pitch, yaw, roll), Vector3.one);

            switch (prop.Kind)
            {
                case PropKind.OfficeChair:
                    Box(p, PropPaint.Dark, 0, 0.45f, 0, 0.5f, 0.08f, 0.5f);
                    Box(p, PropPaint.Dark, 0, 0.8f, -0.22f, 0.48f, 0.55f, 0.06f);
                    Box(p, PropPaint.Dark, 0.27f, 0.62f, 0, 0.05f, 0.04f, 0.35f);
                    Box(p, PropPaint.Dark, -0.27f, 0.62f, 0, 0.05f, 0.04f, 0.35f);
                    Box(p, PropPaint.Metal, 0, 0.24f, 0, 0.06f, 0.4f, 0.06f);
                    Box(p, PropPaint.Metal, 0, 0.04f, 0, 0.62f, 0.05f, 0.07f);
                    Box(p, PropPaint.Metal, 0, 0.04f, 0, 0.07f, 0.05f, 0.62f);
                    break;
                case PropKind.Desk:
                    Box(p, PropPaint.Wood, 0, 0.74f, 0.35f, 1.4f, 0.04f, 0.7f);
                    Box(p, PropPaint.Metal, 0.67f, 0.36f, 0.35f, 0.04f, 0.72f, 0.66f);
                    Box(p, PropPaint.Metal, -0.67f, 0.36f, 0.35f, 0.04f, 0.72f, 0.66f);
                    Box(p, PropPaint.Metal, 0, 0.45f, 0.04f, 1.3f, 0.5f, 0.03f);
                    break;
                case PropKind.FilingCabinet:
                    Box(p, PropPaint.Metal, 0, 0.66f, 0.31f, 0.47f, 1.32f, 0.62f);
                    for (int i = 0; i < 4; i++) Box(p, PropPaint.Dark, 0, 0.25f + 0.32f * i, 0.63f, 0.16f, 0.03f, 0.03f);
                    break;
                case PropKind.CardboardBox:
                    Box(p, PropPaint.Cardboard, 0, 0.2f, 0, 0.5f, 0.4f, 0.4f);
                    if (v % 2 == 1) Box(Sub(0.03f, 0.4f, 0, 0, 18, 0), PropPaint.Cardboard, 0, 0.15f, 0, 0.4f, 0.3f, 0.35f);
                    break;
                case PropKind.TrashCan:
                    Box(p, PropPaint.Dark, 0, 0.25f, 0, 0.34f, 0.5f, 0.34f);
                    break;
                case PropKind.WetFloorSign:
                    Box(Sub(0, 0, 0.12f, -12, 0, 0), PropPaint.Yellow, 0, 0.32f, 0, 0.3f, 0.64f, 0.02f);
                    Box(Sub(0, 0, -0.12f, 12, 0, 0), PropPaint.Yellow, 0, 0.32f, 0, 0.3f, 0.64f, 0.02f);
                    Box(p, PropPaint.Yellow, 0, 0.63f, 0, 0.3f, 0.03f, 0.06f);
                    break;
                case PropKind.Bucket:   // under a ceiling leak
                    Box(p, v % 2 == 0 ? PropPaint.Red : PropPaint.Blue, 0, 0.15f, 0, 0.3f, 0.3f, 0.3f);
                    Box(p, PropPaint.Water, 0, 0.26f, 0, 0.26f, 0.02f, 0.26f);
                    Box(p, PropPaint.Metal, 0, 0.36f, 0, 0.3f, 0.02f, 0.02f);
                    Box(p, PropPaint.Dark, 0, ceiling - 0.004f, 0, 0.5f, 0.005f, 0.5f);
                    break;
                case PropKind.FallenTile:   // a ceiling tile on the floor, and the black gap it left
                    Box(Sub(0, 0.02f, 0, 4, v * 37 % 90, 0), PropPaint.White, 0, 0, 0, 0.6f, 0.02f, 0.6f);
                    Box(p, PropPaint.Void, 0, ceiling - 0.004f, 0, 0.6f, 0.005f, 0.6f);
                    break;
                case PropKind.Ladder:   // leaning against the wall
                {
                    var m = Sub(0, 0, 0.45f, -12, 0, 0);
                    Box(m, PropPaint.Metal, 0.22f, 1.1f, 0, 0.05f, 2.2f, 0.05f);
                    Box(m, PropPaint.Metal, -0.22f, 1.1f, 0, 0.05f, 2.2f, 0.05f);
                    for (float y = 0.3f; y < 2.2f; y += 0.3f) Box(m, PropPaint.Metal, 0, y, 0, 0.44f, 0.03f, 0.04f);
                    break;
                }
                case PropKind.HangingWires:
                    for (int i = 0; i < 3; i++)
                    {
                        float length = 0.3f + 0.15f * ((v + i) % 4);
                        Box(Sub(0.1f * i - 0.1f, ceiling, 0.07f * i, 0, 0, 6 * (i - 1)), PropPaint.Dark, 0, -length / 2, 0, 0.02f, length, 0.02f);
                    }
                    Box(p, PropPaint.Void, 0, ceiling - 0.004f, 0, 0.6f, 0.005f, 0.6f);
                    break;
                case PropKind.Outlet:
                    Box(p, PropPaint.White, 0, 0.3f, 0.01f, 0.08f, 0.12f, 0.02f);
                    break;
                case PropKind.LightSwitch:
                    Box(p, PropPaint.White, 0, 1.2f, 0.01f, 0.08f, 0.12f, 0.02f);
                    Box(p, PropPaint.Metal, 0, 1.2f, 0.025f, 0.02f, 0.04f, 0.015f);
                    break;
                case PropKind.Vent:
                {
                    float y = v % 2 == 0 ? ceiling - 0.35f : 0.25f;
                    Box(p, PropPaint.Metal, 0, y, 0.015f, 0.6f, 0.3f, 0.03f);
                    for (int i = -1; i <= 1; i++) Box(p, PropPaint.Dark, 0, y + 0.08f * i, 0.032f, 0.54f, 0.02f, 0.01f);
                    break;
                }
                case PropKind.ExitSign:   // points nowhere
                    Box(p, PropPaint.Glow, 0, ceiling - 0.35f, 0.04f, 0.36f, 0.15f, 0.08f);
                    break;
                case PropKind.DoorToNowhere:
                    DoorFrame(p, mesh, PropPaint.Wood);
                    break;
                case PropKind.AlmondWater:   // dented metal thermoses; caps grey, green, red or blue
                    for (int i = 0; i <= v % 3; i++)
                    {
                        var m = Sub(0.13f * i, 0, 0.06f * (i % 2), 0, 25 * i, 0);
                        Box(m, PropPaint.Metal, 0, 0.15f, 0, 0.09f, 0.3f, 0.09f);
                        var cap = (v + i) % 4 == 0 ? PropPaint.Metal : (v + i) % 4 == 1 ? PropPaint.Green : (v + i) % 4 == 2 ? PropPaint.Red : PropPaint.Blue;
                        Box(m, cap, 0, 0.315f, 0, 0.07f, 0.03f, 0.07f);
                    }
                    break;
                case PropKind.Battery:
                    for (int i = 0; i <= v % 2; i++)
                    {
                        Box(p, PropPaint.Dark, 0.05f * i, 0.03f, 0, 0.03f, 0.06f, 0.03f);
                        Box(p, PropPaint.Yellow, 0.05f * i, 0.065f, 0, 0.03f, 0.01f, 0.03f);
                    }
                    break;
                case PropKind.Glowstick:   // a few unsnapped glowsticks, rubber-banded
                    for (int i = 0; i <= v % 3; i++) Box(Sub(0.03f * i, 0, 0, 0, 12 * i, 0), PropPaint.Green, 0, 0.012f, 0, 0.022f, 0.022f, 0.17f);
                    break;
                case PropKind.Repellent:   // a spray can: dull green, white cap, a moth-brown band
                    Box(p, PropPaint.Green, 0, 0.09f, 0, 0.062f, 0.18f, 0.062f);
                    Box(p, PropPaint.Rust, 0, 0.1f, 0, 0.064f, 0.05f, 0.064f);
                    Box(p, PropPaint.Metal, 0, 0.185f, 0, 0.05f, 0.012f, 0.05f);
                    Box(p, PropPaint.White, 0, 0.21f, 0, 0.034f, 0.035f, 0.034f);
                    break;
                case PropKind.Flare:   // a red paper tube with a white cap, on its side
                    Box(p, PropPaint.Red, 0, 0.02f, 0, 0.036f, 0.036f, 0.24f);
                    Box(p, PropPaint.White, 0, 0.02f, 0.135f, 0.04f, 0.04f, 0.03f);
                    break;
                case PropKind.FirstAid:   // a battered white tin with a red cross
                    Box(p, PropPaint.White, 0, 0.05f, 0, 0.26f, 0.1f, 0.18f);
                    Box(p, PropPaint.Red, 0, 0.101f, 0, 0.12f, 0.004f, 0.035f);
                    Box(p, PropPaint.Red, 0, 0.101f, 0, 0.035f, 0.004f, 0.12f);
                    break;
                case PropKind.Exterminator:   // brown glass bottle, red cap, hand-written label
                    Box(p, PropPaint.Rust, 0, 0.09f, 0, 0.07f, 0.18f, 0.07f);
                    Box(p, PropPaint.Rust, 0, 0.2f, 0, 0.03f, 0.05f, 0.03f);
                    Box(p, PropPaint.Red, 0, 0.235f, 0, 0.035f, 0.02f, 0.035f);
                    Box(p, PropPaint.White, 0, 0.09f, 0.036f, 0.05f, 0.06f, 0.004f);
                    break;
                case PropKind.Crate:
                    Box(p, PropPaint.Wood, 0, 0.45f, 0, 0.9f, 0.9f, 0.9f);
                    Box(p, PropPaint.Dark, 0, 0.45f, 0, 0.92f, 0.08f, 0.92f);
                    if (v % 3 == 0) Box(Sub(0.05f, 0.9f, 0, 0, 15, 0), PropPaint.Wood, 0, 0.35f, 0, 0.7f, 0.7f, 0.7f);
                    break;
                case PropKind.Pipes:
                    for (int i = 0; i <= v % 3; i++)
                        Box(p, i % 2 == 0 ? PropPaint.Metal : PropPaint.Dark, 0, ceiling - 0.3f - 0.25f * i, 0.13f, 1.96f, 0.16f, 0.16f);
                    if (v % 4 == 0) Box(p, PropPaint.Metal, 0.6f, ceiling / 2, 0.1f, 0.12f, ceiling, 0.12f);
                    break;
                case PropKind.Puddle:   // variant 1: the thick black liquid that leaks from Level 2's pipes
                    Box(p, v == 1 ? PropPaint.Oil : PropPaint.Water, 0, 0.003f, 0, 1.2f, 0.004f, 0.8f);
                    break;
                case PropKind.OctagonTable:
                    Box(p, PropPaint.Wood, 0, 0.74f, 0, 1.1f, 0.05f, 1.1f);
                    Box(Sub(0, 0, 0, 0, 45, 0), PropPaint.Wood, 0, 0.74f, 0, 1.1f, 0.05f, 1.1f);
                    Box(p, PropPaint.Wood, 0, 0.36f, 0, 0.12f, 0.72f, 0.12f);
                    break;
                case PropKind.WoodChair:
                    Box(p, PropPaint.Wood, 0, 0.45f, 0, 0.45f, 0.05f, 0.45f);
                    Box(p, PropPaint.Wood, 0, 0.75f, -0.2f, 0.45f, 0.55f, 0.04f);
                    foreach (float x in new[] { -0.19f, 0.19f })
                    foreach (float z in new[] { -0.19f, 0.19f })
                        Box(p, PropPaint.Wood, x, 0.22f, z, 0.04f, 0.44f, 0.04f);
                    break;
                case PropKind.WoodFloor:   // floorboards over the carpet, 8 x 8 m
                    for (int i = 0; i < 20; i++)
                        Box(p, i % 2 == 0 ? PropPaint.Wood : PropPaint.Cardboard, -3.8f + 0.4f * i, 0.005f, 0, 0.4f, 0.01f, 7.8f);
                    break;
                case PropKind.Pit:   // pitch black: reads as a bottomless hole
                    Box(p, PropPaint.Void, 0, 0.003f, 0, 1.5f, 0.004f, 1.5f);
                    break;
            }
        }

        /// What a door, elevator or stairwell exit looks like, on the wall frame `p`.
        public static void BuildExit(ExitKind kind, Matrix4x4 p, BoxMeshBuilder mesh)
        {
            void Box(PropPaint paint, float cx, float cy, float cz, float sx, float sy, float sz) =>
                mesh.AddOrientedBox(p, new Vector3(cx, cy, cz), new Vector3(sx, sy, sz), (int)paint);
            switch (kind)
            {
                case ExitKind.Door:
                    DoorFrame(p, mesh, PropPaint.Wood);
                    break;
                case ExitKind.Elevator:   // brushed doors open a crack onto darkness, a lit call button
                    Box(PropPaint.Metal, 0, 2.15f, 0.03f, 1.5f, 0.12f, 0.06f);
                    Box(PropPaint.Metal, 0.7f, 1.05f, 0.03f, 0.1f, 2.1f, 0.06f);
                    Box(PropPaint.Metal, -0.7f, 1.05f, 0.03f, 0.1f, 2.1f, 0.06f);
                    Box(PropPaint.Void, 0, 1.05f, 0.005f, 1.3f, 2.1f, 0.01f);
                    Box(PropPaint.Metal, 0.34f, 1.05f, 0.02f, 0.6f, 2.08f, 0.02f);
                    Box(PropPaint.Metal, -0.34f, 1.05f, 0.02f, 0.6f, 2.08f, 0.02f);
                    Box(PropPaint.Dark, 0.95f, 1.15f, 0.02f, 0.12f, 0.25f, 0.03f);
                    Box(PropPaint.Glow, 0.95f, 1.15f, 0.04f, 0.05f, 0.05f, 0.02f);
                    break;
                case ExitKind.Stairs:   // a few steps up into a dark stairwell opening
                    Box(PropPaint.Void, 0, 1.1f, 0.005f, 1.0f, 2.2f, 0.01f);
                    Box(PropPaint.White, 0.53f, 1.12f, 0.03f, 0.06f, 2.24f, 0.06f);
                    Box(PropPaint.White, -0.53f, 1.12f, 0.03f, 0.06f, 2.24f, 0.06f);
                    Box(PropPaint.White, 0, 2.24f, 0.03f, 1.12f, 0.06f, 0.06f);
                    for (int i = 0; i < 3; i++) Box(PropPaint.Metal, 0, 0.09f + 0.18f * i, 0.6f - 0.2f * i, 1.0f, 0.18f, 0.2f);
                    Box(PropPaint.Metal, 0.5f, 0.9f, 0.4f, 0.04f, 0.04f, 0.7f);
                    break;
            }
        }

        /// A way out, marked so you can tell it from the locked doors: it stands ajar with light spilling round
        /// the edge, a caged lamp burns above it, and the destination's number is painted on a plate.
        public static void BuildMarkedExit(string target, Matrix4x4 p, BoxMeshBuilder mesh)
        {
            BuildMarkedFrame(target, p, mesh);
            // The leaf, open a hand's width: hinged on the left, swung out into the room.
            var leaf = p * Matrix4x4.TRS(new Vector3(-0.45f, 0f, 0.03f), Quaternion.Euler(0f, 28f, 0f), Vector3.one);
            mesh.AddOrientedBox(leaf, new Vector3(0.45f, 1.02f, 0f), new Vector3(0.9f, 2.04f, 0.04f), (int)PropPaint.Metal);
            mesh.AddOrientedBox(leaf, new Vector3(0.81f, 1.0f, 0.035f), new Vector3(0.05f, 0.05f, 0.05f), (int)PropPaint.Dark);
        }

        /// A marked exit without its leaf: frame, dark opening, the lamp and the number plate.
        public static void BuildMarkedFrame(string target, Matrix4x4 p, BoxMeshBuilder mesh)
        {
            void Box(PropPaint paint, float cx, float cy, float cz, float sx, float sy, float sz) =>
                mesh.AddOrientedBox(p, new Vector3(cx, cy, cz), new Vector3(sx, sy, sz), (int)paint);
            DoorFrameOnly(p, mesh, true);
            // The caged lamp.
            Box(PropPaint.Dark, 0f, 2.62f, 0.06f, 0.12f, 0.12f, 0.12f);
            Box(PropPaint.Warm, 0f, 2.6f, 0.16f, 0.09f, 0.09f, 0.09f);
            foreach (float x in new[] { -0.06f, 0.06f }) Box(PropPaint.Metal, x, 2.6f, 0.22f, 0.008f, 0.12f, 0.008f);
            // The number plate: white segments on a dark plate.
            Box(PropPaint.Dark, 0f, 2.33f, 0.02f, 0.12f + 0.12f * target.Length, 0.22f, 0.02f);
            Digits(p, target, new Vector3(0f, 2.33f, 0.035f), mesh);
        }

        /// The frame of a door on a wall; with `open` the opening behind it is dark (it leads somewhere) and a
        /// little light leaks round the edges.
        public static void DoorFrameOnly(Matrix4x4 p, BoxMeshBuilder mesh, bool open)
        {
            void Box(PropPaint paint, float cx, float cy, float cz, float sx, float sy, float sz) =>
                mesh.AddOrientedBox(p, new Vector3(cx, cy, cz), new Vector3(sx, sy, sz), (int)paint);
            Box(PropPaint.White, 0.5f, 1.03f, 0.02f, 0.06f, 2.06f, 0.04f);
            Box(PropPaint.White, -0.5f, 1.03f, 0.02f, 0.06f, 2.06f, 0.04f);
            Box(PropPaint.White, 0, 2.08f, 0.02f, 1.06f, 0.06f, 0.04f);
            // The opening stands a little proud of the wall, so a chair rail or baseboard never shows across it.
            Box(open ? PropPaint.Void : PropPaint.Rust, 0, 1.02f, 0.025f, 0.9f, 2.04f, 0.006f);
            if (open) Box(PropPaint.Warm, 0.43f, 1.02f, 0.03f, 0.03f, 1.9f, 0.004f);   // light round the edge
        }

        /// Seven-segment digits (and '.' / '-') centred at `at` in the frame `p`.
        static void Digits(Matrix4x4 p, string text, Vector3 at, BoxMeshBuilder mesh, PropPaint paint = PropPaint.White)
        {
            const float w = 0.09f, h = 0.16f, t = 0.018f, gap = 0.12f;
            float x = at.x - (text.Length - 1) * gap / 2f;
            foreach (char ch in text)
            {
                void Seg(float cx, float cy, bool horizontal) =>
                    mesh.AddOrientedBox(p, new Vector3(x + cx, at.y + cy, at.z), horizontal ? new Vector3(w, t, 0.01f) : new Vector3(t, h / 2f, 0.01f), (int)paint);
                if (ch == '.') { mesh.AddOrientedBox(p, new Vector3(x, at.y - h / 2f + t / 2f, at.z), new Vector3(t, t, 0.01f), (int)paint); x += gap * 0.5f; continue; }
                // segments: a top, b top-right, c bottom-right, d bottom, e bottom-left, f top-left, g middle
                string on = ch switch
                {
                    '0' => "abcdef", '1' => "bc", '2' => "abged", '3' => "abgcd", '4' => "fgbc", '5' => "afgcd",
                    '6' => "afgedc", '7' => "abc", '8' => "abcdefg", '9' => "abfgcd", '-' => "g", 'P' => "abfeg", _ => "",
                };
                foreach (char s in on)
                    switch (s)
                    {
                        case 'a': Seg(0f, h / 2f, true); break;
                        case 'g': Seg(0f, 0f, true); break;
                        case 'd': Seg(0f, -h / 2f, true); break;
                        case 'b': Seg(w / 2f, h / 4f, false); break;
                        case 'c': Seg(w / 2f, -h / 4f, false); break;
                        case 'e': Seg(-w / 2f, -h / 4f, false); break;
                        case 'f': Seg(-w / 2f, h / 4f, false); break;
                    }
                x += gap;
            }
        }

        /// A door flush on a wall: frame, slab and knob. Used for exits and doors that lead nowhere.
        public static void DoorFrame(Matrix4x4 p, BoxMeshBuilder mesh, PropPaint slab)
        {
            if (ModelLibrary.Add(mesh, "PanelDoor", p)) return;   // the Blender door, when it's there
            void Box(PropPaint paint, float cx, float cy, float cz, float sx, float sy, float sz) =>
                mesh.AddOrientedBox(p, new Vector3(cx, cy, cz), new Vector3(sx, sy, sz), (int)paint);
            Box(PropPaint.White, 0.5f, 1.03f, 0.02f, 0.06f, 2.06f, 0.04f);
            Box(PropPaint.White, -0.5f, 1.03f, 0.02f, 0.06f, 2.06f, 0.04f);
            Box(PropPaint.White, 0, 2.08f, 0.02f, 1.06f, 0.06f, 0.04f);
            Box(slab, 0, 1.02f, 0.015f, 0.9f, 2.04f, 0.03f);
            Box(PropPaint.Metal, 0.36f, 1.0f, 0.05f, 0.05f, 0.05f, 0.05f);
        }
    }
}
