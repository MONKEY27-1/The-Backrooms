using System.Collections.Generic;
using UnityEngine;

namespace Backrooms.World
{
    public enum CuriosityKind
    {
        None, LoneChair, ChairCircle, LoneDoor, CrtTv, RingingPhone, TilePile, StairsToCeiling,
        WindowToWall, OddOutlets, TallDoor, TinyDoor, AlmondCache, LoneDesk,
        ChairStack, ChairPile, ScatteredPapers, SupplyCorner, PhoneCluster, TvCluster, TvPhoneWrong,
        DoorPair, DoorAndChair, DeskSprawl, BoxSprawl, TippedFurniture, ChairFacingCorner, EquipmentCorner,
        ChairWallRow, DeskCorner, PaperTrail, SupplySpill, TvFacingWall, PhoneLine, MixedElectronics,
        StackedCrates, BoxAndChair, WaitingSeats, DoorAtWall, LoneBucket, OpenCabinetMess,
        TripodTrace, SurveyCorner, ArrowFloorMarks, TallyCorner, WrongFacingPair, RepeatedSigns,
    }

    /// Decides what (if anything) appears in a curiosity slot. Implemented by the adaptive encounter layer.
    public interface ICuriosityResolver
    {
        CuriosityKind Resolve(CuriositySlot slot);
    }

    public static class CuriosityLibrary
    {
        public static bool Fits(CuriosityKind kind, CuriositySlot slot)
        {
            switch (kind)
            {
                case CuriosityKind.WindowToWall:
                case CuriosityKind.OddOutlets:
                case CuriosityKind.TallDoor:
                case CuriosityKind.TinyDoor:
                case CuriosityKind.RepeatedSigns:
                    return slot.OnWall;
                case CuriosityKind.ChairCircle:
                case CuriosityKind.StairsToCeiling:
                case CuriosityKind.ChairPile:
                case CuriosityKind.DeskSprawl:
                case CuriosityKind.BoxSprawl:
                case CuriosityKind.WaitingSeats:
                    return !slot.OnWall && slot.BigRoom;
                case CuriosityKind.LoneChair:
                case CuriosityKind.LoneDoor:
                case CuriosityKind.CrtTv:
                case CuriosityKind.RingingPhone:
                case CuriosityKind.TilePile:
                case CuriosityKind.AlmondCache:
                case CuriosityKind.LoneDesk:
                case CuriosityKind.ChairStack:
                case CuriosityKind.ScatteredPapers:
                case CuriosityKind.SupplyCorner:
                case CuriosityKind.PhoneCluster:
                case CuriosityKind.TvCluster:
                case CuriosityKind.TvPhoneWrong:
                case CuriosityKind.DoorPair:
                case CuriosityKind.DoorAndChair:
                case CuriosityKind.TippedFurniture:
                case CuriosityKind.ChairFacingCorner:
                case CuriosityKind.EquipmentCorner:
                case CuriosityKind.ChairWallRow:
                case CuriosityKind.DeskCorner:
                case CuriosityKind.PaperTrail:
                case CuriosityKind.SupplySpill:
                case CuriosityKind.TvFacingWall:
                case CuriosityKind.PhoneLine:
                case CuriosityKind.MixedElectronics:
                case CuriosityKind.StackedCrates:
                case CuriosityKind.BoxAndChair:
                case CuriosityKind.DoorAtWall:
                case CuriosityKind.LoneBucket:
                case CuriosityKind.OpenCabinetMess:
                case CuriosityKind.TripodTrace:
                case CuriosityKind.SurveyCorner:
                case CuriosityKind.ArrowFloorMarks:
                case CuriosityKind.TallyCorner:
                case CuriosityKind.WrongFacingPair:
                    return !slot.OnWall;
                default:
                    return true;
            }
        }

        /// How rare each curiosity is, before the adaptive layer favours ones you haven't seen.
        public static float BaseWeight(CuriosityKind kind) => kind switch
        {
            CuriosityKind.LoneChair => 1.05f,
            CuriosityKind.ChairCircle => 0.5f,
            CuriosityKind.LoneDoor => 0.72f,
            CuriosityKind.CrtTv => 0.6f,
            CuriosityKind.RingingPhone => 0.4f,
            CuriosityKind.TilePile => 0.9f,
            CuriosityKind.StairsToCeiling => 0.3f,
            CuriosityKind.WindowToWall => 0.7f,
            CuriosityKind.OddOutlets => 0.85f,
            CuriosityKind.TallDoor => 0.5f,
            CuriosityKind.TinyDoor => 0.5f,
            CuriosityKind.AlmondCache => 0.55f,
            CuriosityKind.LoneDesk => 0.72f,
            CuriosityKind.ChairStack => 0.68f,
            CuriosityKind.ChairPile => 0.42f,
            CuriosityKind.ScatteredPapers => 0.82f,
            CuriosityKind.SupplyCorner => 0.68f,
            CuriosityKind.PhoneCluster => 0.42f,
            CuriosityKind.TvCluster => 0.34f,
            CuriosityKind.TvPhoneWrong => 0.38f,
            CuriosityKind.DoorPair => 0.42f,
            CuriosityKind.DoorAndChair => 0.52f,
            CuriosityKind.DeskSprawl => 0.38f,
            CuriosityKind.BoxSprawl => 0.52f,
            CuriosityKind.TippedFurniture => 0.52f,
            CuriosityKind.ChairFacingCorner => 0.58f,
            CuriosityKind.EquipmentCorner => 0.48f,
            CuriosityKind.ChairWallRow => 0.4f,
            CuriosityKind.DeskCorner => 0.38f,
            CuriosityKind.PaperTrail => 0.46f,
            CuriosityKind.SupplySpill => 0.36f,
            CuriosityKind.TvFacingWall => 0.34f,
            CuriosityKind.PhoneLine => 0.3f,
            CuriosityKind.MixedElectronics => 0.28f,
            CuriosityKind.StackedCrates => 0.34f,
            CuriosityKind.BoxAndChair => 0.36f,
            CuriosityKind.WaitingSeats => 0.24f,
            CuriosityKind.DoorAtWall => 0.34f,
            CuriosityKind.LoneBucket => 0.3f,
            CuriosityKind.OpenCabinetMess => 0.32f,
            CuriosityKind.TripodTrace => 0.16f,
            CuriosityKind.SurveyCorner => 0.22f,
            CuriosityKind.ArrowFloorMarks => 0.2f,
            CuriosityKind.TallyCorner => 0.18f,
            CuriosityKind.WrongFacingPair => 0.3f,
            CuriosityKind.RepeatedSigns => 0.16f,
            _ => 0f,
        };

        /// Props for a curiosity; `ringing` asks the streamer to attach a ringing-phone sound.
        public static void Build(CuriosityKind kind, CuriositySlot slot, List<PropSpec> into, out bool ringing)
        {
            ringing = false;
            var p = slot.Pose;
            var h = slot.Ceiling;
            void Add(PropKind k, Matrix4x4 m, int v = 0) => into.Add(new PropSpec { Kind = k, Pose = m, Variant = v, Ceiling = h });
            Matrix4x4 At(float x, float z, float yaw, float y = 0f) => p * Matrix4x4.TRS(new Vector3(x, y, z), Quaternion.Euler(0f, yaw, 0f), Vector3.one);
            Matrix4x4 AtS(float x, float z, float yaw, Vector3 scale, float y = 0f, float pitch = 0f, float roll = 0f) =>
                p * Matrix4x4.TRS(new Vector3(x, y, z), Quaternion.Euler(pitch, yaw, roll), scale);
            switch (kind)
            {
                case CuriosityKind.LoneChair:   // one chair facing a wall it can't see
                    Add(PropKind.WoodChair, p);
                    break;
                case CuriosityKind.ChairCircle:   // chairs in a circle, facing inward, nobody there
                    for (int i = 0; i < 8; i++)
                    {
                        float a = i * Mathf.PI * 2f / 8f;
                        Add(PropKind.WoodChair, At(Mathf.Cos(a) * 1.8f, Mathf.Sin(a) * 1.8f, -a * Mathf.Rad2Deg - 90f));
                    }
                    break;
                case CuriosityKind.LoneDoor:
                    Add(PropKind.LoneDoor, p);
                    break;
                case CuriosityKind.CrtTv:
                    Add(PropKind.CrtTv, p);
                    break;
                case CuriosityKind.RingingPhone:
                    Add(PropKind.Phone, p);
                    ringing = true;
                    break;
                case CuriosityKind.TilePile:
                    Add(PropKind.TilePile, p, 3);
                    Add(PropKind.FallenTile, At(0.8f, 0.6f, 30f));
                    break;
                case CuriosityKind.StairsToCeiling:
                    Add(PropKind.StairsToCeiling, p);
                    break;
                case CuriosityKind.WindowToWall:
                    Add(PropKind.WindowToWall, p);
                    break;
                case CuriosityKind.OddOutlets:   // a column of outlets climbing the wall
                    for (int i = 0; i < 6; i++) Add(PropKind.Outlet, p * Matrix4x4.Translate(new Vector3(0f, 0.32f * i, 0f)));
                    break;
                case CuriosityKind.TallDoor:
                    Add(PropKind.DoorToNowhere, p * Matrix4x4.Scale(new Vector3(1f, 1.3f, 1f)));
                    break;
                case CuriosityKind.TinyDoor:
                    Add(PropKind.DoorToNowhere, p * Matrix4x4.Scale(new Vector3(0.35f, 0.35f, 1f)));
                    break;
                case CuriosityKind.AlmondCache:   // someone's stash
                    Add(PropKind.CardboardBox, p, 1);
                    Add(PropKind.AlmondWater, At(0.5f, 0.2f, 10f), 2);
                    Add(PropKind.AlmondWater, At(-0.4f, 0.4f, 70f), 5);
                    Add(PropKind.Battery, At(0.2f, -0.5f, 0f), 1);
                    break;
                case CuriosityKind.LoneDesk:   // a desk alone in the open, its chair pushed back
                    Add(PropKind.Desk, At(0f, -0.35f, 0f));
                    Add(PropKind.OfficeChair, At(0.1f, 0.9f, 160f));
                    Add(PropKind.Phone, At(0.3f, -0.1f, 0f) * Matrix4x4.Translate(new Vector3(0f, 0.76f, 0f)));
                    break;
                case CuriosityKind.ChairStack:   // chairs stacked where no one would stack them
                    Add(PropKind.WoodChair, At(0f, 0f, 0f));
                    Add(PropKind.WoodChair, AtS(0.05f, 0.05f, 18f, new Vector3(1f, 1f, 1f), 0.46f, 8f, 180f));
                    Add(PropKind.OfficeChair, AtS(-0.08f, -0.02f, -35f, new Vector3(1f, 1f, 1f), 0.9f, 14f, 12f));
                    break;
                case CuriosityKind.ChairPile:   // a heap of chairs left in the middle of nowhere
                    for (int i = 0; i < 9; i++)
                    {
                        float a = i * 2.399f;
                        float r = 0.25f + (i % 3) * 0.35f;
                        float y = (i / 3) * 0.18f;
                        Add(i % 2 == 0 ? PropKind.WoodChair : PropKind.OfficeChair,
                            AtS(Mathf.Cos(a) * r, Mathf.Sin(a) * r, i * 47f, Vector3.one, y, (i % 4) * 18f, (i % 3) * 25f));
                    }
                    break;
                case CuriosityKind.ScatteredPapers:   // papers or thin sheets scattered across the floor
                    for (int i = 0; i < 14; i++)
                    {
                        float a = i * 1.73f;
                        float r = 0.15f + (i % 5) * 0.22f;
                        float w = 0.55f + (i % 3) * 0.18f, d = 0.45f + ((i + 1) % 3) * 0.16f;
                        Add(PropKind.CardboardBox, AtS(Mathf.Cos(a) * r, Mathf.Sin(a) * r, i * 29f, new Vector3(w, 0.03f, d), 0.01f + (i % 2) * 0.005f));
                    }
                    break;
                case CuriosityKind.SupplyCorner:   // an abandoned supply corner
                    Add(PropKind.CardboardBox, At(-0.2f, -0.1f, 12f), 2);
                    Add(PropKind.CardboardBox, At(0.35f, 0.25f, -18f), 1);
                    Add(PropKind.Bucket, At(0.55f, -0.45f, 0f));
                    Add(PropKind.AlmondWater, At(-0.45f, 0.35f, 40f), 3);
                    Add(PropKind.Battery, At(-0.1f, 0.45f, 0f), 1);
                    Add(PropKind.Can, At(0.1f, -0.55f, 70f), 1);
                    Add(PropKind.Lantern, At(0.0f, 0.55f, 0f));
                    break;
                case CuriosityKind.PhoneCluster:   // too many phones in the wrong place
                    for (int i = 0; i < 5; i++)
                    {
                        float a = i * 1.256f;
                        float r = 0.15f + (i % 2) * 0.25f;
                        Add(PropKind.Phone, At(Mathf.Cos(a) * r, Mathf.Sin(a) * r, i * 73f));
                    }
                    ringing = true;
                    break;
                case CuriosityKind.TvCluster:   // televisions gathered together, facing nothing in particular
                    Add(PropKind.CrtTv, At(-0.6f, 0.0f, 15f));
                    Add(PropKind.CrtTv, At(0.6f, 0.15f, -25f));
                    Add(PropKind.CrtTv, At(0.0f, -0.7f, 95f));
                    break;
                case CuriosityKind.TvPhoneWrong:   // electronics arranged without purpose
                    Add(PropKind.CrtTv, At(-0.4f, -0.2f, 35f));
                    Add(PropKind.Phone, At(0.35f, 0.25f, 0f));
                    Add(PropKind.Phone, At(0.55f, -0.35f, 140f));
                    Add(PropKind.CrtTv, At(0.2f, 0.7f, 180f));
                    ringing = true;
                    break;
                case CuriosityKind.DoorPair:   // doors placed as if they expected walls
                    Add(PropKind.LoneDoor, At(-0.7f, 0f, 0f));
                    Add(PropKind.LoneDoor, At(0.7f, 0f, 180f));
                    break;
                case CuriosityKind.DoorAndChair:   // an arrangement that implies someone waited
                    Add(PropKind.LoneDoor, At(0f, -0.5f, 0f));
                    Add(PropKind.WoodChair, At(0f, 0.6f, 180f));
                    break;
                case CuriosityKind.DeskSprawl:   // office furniture spilled into the open
                    Add(PropKind.Desk, At(-0.4f, -0.2f, 20f));
                    Add(PropKind.OfficeChair, AtS(0.6f, 0.5f, 210f, Vector3.one, 0f, 0f, 90f));
                    Add(PropKind.FilingCabinet, At(0.8f, -0.5f, -35f));
                    Add(PropKind.Phone, At(-0.5f, -0.1f, 45f) * Matrix4x4.Translate(new Vector3(0f, 0.76f, 0f)));
                    Add(PropKind.CardboardBox, At(-0.9f, 0.6f, 10f), 0);
                    break;
                case CuriosityKind.BoxSprawl:   // boxes and clutter arranged too deliberately
                    for (int i = 0; i < 7; i++)
                    {
                        float a = i * 0.9f;
                        float r = 0.2f + (i % 4) * 0.3f;
                        Add(i % 3 == 0 ? PropKind.Crate : PropKind.CardboardBox, At(Mathf.Cos(a) * r, Mathf.Sin(a) * r, i * 37f), i % 3);
                    }
                    Add(PropKind.TrashCan, At(0.1f, 0.0f, 0f));
                    break;
                case CuriosityKind.TippedFurniture:   // furniture left in the wrong attitude
                    Add(PropKind.Desk, AtS(-0.2f, 0.0f, 25f, Vector3.one, 0.35f, 0f, 90f));
                    Add(PropKind.OfficeChair, AtS(0.7f, 0.5f, 160f, Vector3.one, 0.25f, 80f, 0f));
                    Add(PropKind.CardboardBox, At(0.0f, -0.7f, 70f), 1);
                    break;
                case CuriosityKind.ChairFacingCorner:   // turned toward nothing anyone can see
                    Add(PropKind.WoodChair, At(0f, 0f, 45f));
                    Add(PropKind.WoodChair, At(0.9f, 0.2f, 60f));
                    break;
                case CuriosityKind.EquipmentCorner:   // traces of work that stopped abruptly
                    Add(PropKind.Ladder, At(-0.2f, -0.2f, 15f));
                    Add(PropKind.Bucket, At(0.45f, 0.1f, 0f));
                    Add(PropKind.CardboardBox, At(0.2f, 0.55f, -20f), 2);
                    Add(PropKind.Can, At(-0.55f, 0.4f, 30f), 0);
                    Add(PropKind.Lantern, At(-0.4f, -0.55f, 0f));
                    break;
                case CuriosityKind.ChairWallRow:   // chairs lined up facing a blank wall
                    for (int i = 0; i < 4; i++)
                        Add(i % 2 == 0 ? PropKind.WoodChair : PropKind.OfficeChair, At(-0.9f + i * 0.6f, 0f, 0f));
                    break;
                case CuriosityKind.DeskCorner:   // an office corner assembled in open space
                    Add(PropKind.Desk, At(-0.2f, 0f, 12f));
                    Add(PropKind.OfficeChair, At(0.55f, 0.7f, 150f));
                    Add(PropKind.FilingCabinet, At(-0.85f, 0.55f, -8f));
                    Add(PropKind.TrashCan, At(0.9f, -0.35f, 0f));
                    break;
                case CuriosityKind.PaperTrail:   // scattered sheets leading nowhere obvious
                    for (int i = 0; i < 10; i++)
                        Add(PropKind.CardboardBox, AtS(-1.2f + i * 0.28f, (i % 3 - 1) * 0.35f, 15f + i * 23f, new Vector3(0.52f, 0.03f, 0.42f), 0.01f + (i % 2) * 0.004f));
                    break;
                case CuriosityKind.SupplySpill:   // supplies dropped and never recovered
                    Add(PropKind.CardboardBox, AtS(-0.1f, -0.1f, 40f, Vector3.one, 0.1f, 0f, 80f), 1);
                    Add(PropKind.Bucket, At(0.5f, 0.3f, 10f));
                    Add(PropKind.Can, At(-0.55f, 0.25f, -20f), 1);
                    Add(PropKind.Battery, At(0.1f, 0.55f, 0f), 0);
                    Add(PropKind.AlmondWater, At(-0.35f, -0.55f, 55f), 2);
                    break;
                case CuriosityKind.TvFacingWall:   // a screen turned to the wall
                    Add(PropKind.CrtTv, At(0f, 0f, 180f));
                    Add(PropKind.WoodChair, At(0f, 0.85f, 0f));
                    break;
                case CuriosityKind.PhoneLine:   // phones left in a careful row
                    for (int i = 0; i < 4; i++)
                        Add(PropKind.Phone, At(-0.75f + i * 0.5f, 0f, i * 19f));
                    if ((slot.Id & 1) == 0) ringing = true;
                    break;
                case CuriosityKind.MixedElectronics:   // devices with no shared purpose
                    Add(PropKind.CrtTv, At(-0.35f, 0.1f, 25f));
                    Add(PropKind.Phone, At(0.45f, -0.2f, 80f));
                    Add(PropKind.Battery, At(0.1f, 0.55f, 0f), 1);
                    Add(PropKind.Can, At(-0.6f, -0.45f, 15f), 0);
                    break;
                case CuriosityKind.StackedCrates:   // crates stacked too neatly
                    Add(PropKind.Crate, At(-0.2f, 0f, 8f), 0);
                    Add(PropKind.Crate, At(0.25f, 0.15f, -12f), 1);
                    Add(PropKind.CardboardBox, AtS(0.0f, 0.05f, 35f, Vector3.one, 0.42f, 0f, 0f), 2);
                    Add(PropKind.CardboardBox, AtS(0.05f, -0.05f, 20f, Vector3.one, 0.76f, 0f, 0f), 0);
                    break;
                case CuriosityKind.BoxAndChair:   // an odd pairing, left deliberately
                    Add(PropKind.CardboardBox, At(-0.25f, 0f, 10f), 1);
                    Add(PropKind.WoodChair, AtS(0.35f, 0.2f, 120f, Vector3.one, 0f, 0f, 0f));
                    Add(PropKind.Bucket, At(0.0f, 0.55f, 0f));
                    break;
                case CuriosityKind.WaitingSeats:   // seats arranged as if for someone who never came
                    for (int i = 0; i < 6; i++)
                        Add(i % 3 == 0 ? PropKind.OfficeChair : PropKind.WoodChair, At(-1.0f + (i % 3) * 1.0f, -0.5f + (i / 3) * 1.0f, i < 3 ? 0f : 180f));
                    break;
                case CuriosityKind.DoorAtWall:   // a door frame standing near a wall it doesn't belong to
                    Add(PropKind.LoneDoor, At(0f, 0.25f, 0f));
                    Add(PropKind.CardboardBox, At(0.45f, -0.4f, 30f), 0);
                    break;
                case CuriosityKind.LoneBucket:   // a single object left with intent
                    Add(PropKind.Bucket, At(0f, 0f, 0f));
                    Add(PropKind.TilePile, At(0.45f, 0.35f, 20f), 1);
                    break;
                case CuriosityKind.OpenCabinetMess:   // storage left open and disturbed
                    Add(PropKind.FilingCabinet, AtS(-0.2f, 0f, 15f, Vector3.one, 0.1f, 0f, 90f));
                    Add(PropKind.CardboardBox, At(0.45f, 0.35f, -25f), 2);
                    Add(PropKind.Can, At(0.2f, -0.5f, 60f), 1);
                    Add(PropKind.Battery, At(-0.55f, -0.35f, 0f), 1);
                    break;
                case CuriosityKind.TripodTrace:   // equipment traces without the people
                    Add(PropKind.Tripod, At(-0.1f, 0f, 20f));
                    Add(PropKind.CardboardBox, At(0.45f, 0.35f, -10f), 1);
                    Add(PropKind.Can, At(-0.5f, 0.45f, 40f), 0);
                    break;
                case CuriosityKind.SurveyCorner:   // survey/ORD-like traces, non-systemic
                    Add(PropKind.Crate, At(-0.2f, 0f, 8f), 0);
                    Add(PropKind.Bucket, At(0.45f, -0.3f, 0f));
                    Add(PropKind.Lantern, At(0.2f, 0.55f, 0f));
                    Add(PropKind.Can, At(-0.55f, 0.35f, 25f), 1);
                    Add(PropKind.CardboardBox, At(-0.15f, -0.55f, -15f), 2);
                    break;
                case CuriosityKind.ArrowFloorMarks:   // floor clutter arranged like misleading marks
                    for (int i = 0; i < 5; i++)
                        Add(PropKind.CardboardBox, AtS(-0.6f + i * 0.3f, -0.2f + (i % 2) * 0.4f, 90f + i * 15f, new Vector3(0.35f, 0.03f, 0.18f), 0.01f));
                    Add(PropKind.TrashCan, At(0.8f, 0f, 0f));
                    break;
                case CuriosityKind.TallyCorner:   // a place someone may have counted from
                    Add(PropKind.WoodChair, At(0f, 0.2f, 40f));
                    Add(PropKind.Can, At(0.45f, -0.25f, 10f), 0);
                    Add(PropKind.CardboardBox, At(-0.45f, -0.15f, -20f), 0);
                    break;
                case CuriosityKind.WrongFacingPair:   // two objects facing each other for no reason
                    Add(PropKind.OfficeChair, At(-0.35f, 0f, 90f));
                    Add(PropKind.CrtTv, At(0.35f, 0f, -90f));
                    break;
                case CuriosityKind.RepeatedSigns:   // wall fixtures repeated in a way that feels wrong
                    for (int i = 0; i < 4; i++)
                        Add(PropKind.Outlet, p * Matrix4x4.Translate(new Vector3(-0.45f + i * 0.3f, 0.2f + (i % 2) * 0.9f, 0f)), i);
                    Add(PropKind.LightSwitch, p * Matrix4x4.Translate(new Vector3(0.15f, 1.45f, 0f)), 0);
                    break;
            }
        }
    }
}
