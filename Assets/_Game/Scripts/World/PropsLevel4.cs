using UnityEngine;

namespace Backrooms.World
{
    /// Level 4 "Abandoned Office" props. Windows, coolers, vending machines and fountains are Blender-made
    /// (Art/Level4Props.blend -> Resources/Models/L4*); the traces of removed furniture are built here.
    public static partial class PropLibrary
    {
        static int PaintIndex(string material) => System.Enum.TryParse(material, true, out PropPaint p) ? (int)p : (int)PropPaint.Wood;

        /// A Level 4 window. Variants 0-1: blacked out; 2: blinds down over black glass; 3: blacked out with paper
        /// taped over it. `clear` leaves the glass out (the lure window draws its own daylight pane).
        public static void OfficeWindow(BoxMeshBuilder mesh, Matrix4x4 pose, int variant, bool clear)
        {
            string model = variant % 4 == 2 && !clear ? "L4WindowBlinds" : "L4Window";
            int glass = clear ? -1 : (int)PropPaint.Dark;
            if (!ModelLibrary.AddMapped(mesh, model, pose, n => n == "Glass" ? glass : PaintIndex(n)))
            {
                mesh.AddOrientedBox(pose, new Vector3(0f, 1.55f, 0.03f), new Vector3(1.6f, 1.4f, 0.06f), (int)PropPaint.Metal);
                if (!clear) mesh.AddOrientedBox(pose, new Vector3(0f, 1.55f, 0.065f), new Vector3(1.48f, 1.28f, 0.01f), (int)PropPaint.Dark);
            }
            if (variant % 4 == 3 && !clear)   // sheets of paper taped over the black, one corner peeling
                for (int i = 0; i < 5; i++)
                {
                    float x = -0.5f + (i % 3) * 0.48f, y = 1.2f + (i / 3) * 0.55f;
                    mesh.AddOrientedBox(pose * Matrix4x4.TRS(new Vector3(x, y, 0.03f), Quaternion.Euler(0f, 0f, (i * 37 % 9) - 4f), Vector3.one),
                                        Vector3.zero, new Vector3(0.21f, 0.29f, 0.003f), (int)PropPaint.White);
                }
        }

        static bool BuildLevel4(PropSpec prop, BoxMeshBuilder mesh, float ceiling)
        {
            var p = prop.Pose;
            int v = prop.Variant;
            void Box(Matrix4x4 m, PropPaint paint, float cx, float cy, float cz, float sx, float sy, float sz) =>
                mesh.AddOrientedBox(m, new Vector3(cx, cy, cz), new Vector3(sx, sy, sz), (int)paint);

            switch (prop.Kind)
            {
                case PropKind.OfficeWindow:
                    OfficeWindow(mesh, p, v, false);
                    return true;
                case PropKind.WaterCooler:   // placed by its wall frame: stands just off the wall
                {
                    var at = p * Matrix4x4.Translate(new Vector3(0f, 0f, 0.22f));
                    if (!ModelLibrary.Add(mesh, "L4WaterCooler", at)) Box(at, PropPaint.White, 0, 0.68f, 0, 0.32f, 1.36f, 0.32f);
                    return true;
                }
                case PropKind.VendingMachine:   // against the wall; the body colour changes from machine to machine
                {
                    var at = p * Matrix4x4.Translate(new Vector3(0f, 0f, 0.44f));
                    int body = v % 3 == 0 ? (int)PropPaint.Red : v % 3 == 1 ? (int)PropPaint.Blue : (int)PropPaint.Dark;
                    if (!ModelLibrary.AddMapped(mesh, "L4VendingMachine", at, n => n == "Red" ? body : PaintIndex(n)))
                        Box(at, PropPaint.Red, 0, 0.92f, 0, 0.9f, 1.85f, 0.85f);
                    return true;
                }
                case PropKind.DrinkFountain:
                    if (!ModelLibrary.Add(mesh, "L4Fountain", p)) Box(p, PropPaint.Metal, 0, 0.86f, 0.17f, 0.4f, 0.12f, 0.3f);
                    return true;
                case PropKind.CarpetDent:   // where furniture stood for years: pressed-in feet and a faint outline
                {
                    float w = v % 3 == 0 ? 1.4f : v % 3 == 1 ? 0.9f : 0.5f, d = v % 3 == 0 ? 0.7f : v % 3 == 1 ? 0.45f : 0.6f;
                    foreach (float sx in new[] { -1f, 1f })
                        foreach (float sz in new[] { -1f, 1f })
                            Box(p, PropPaint.Dark, sx * w / 2f, 0.003f, sz * d / 2f, 0.05f, 0.002f, 0.05f);
                    if (v % 2 == 0)
                    {
                        Box(p, PropPaint.Dark, 0, 0.002f, -d / 2f, w, 0.001f, 0.01f);
                        Box(p, PropPaint.Dark, 0, 0.002f, d / 2f, w, 0.001f, 0.01f);
                    }
                    return true;
                }
                case PropKind.FloorOutlet:   // a floor box with its cables cut off short
                    Box(p, PropPaint.Metal, 0, 0.01f, 0, 0.14f, 0.02f, 0.14f);
                    Box(p * Matrix4x4.TRS(new Vector3(0.02f, 0.02f, 0f), Quaternion.Euler(0f, 20f, 70f), Vector3.one), PropPaint.Dark, 0.04f, 0f, 0f, 0.09f, 0.012f, 0.012f);
                    Box(p * Matrix4x4.TRS(new Vector3(-0.02f, 0.02f, 0.02f), Quaternion.Euler(0f, 140f, 55f), Vector3.one), PropPaint.Blue, 0.03f, 0f, 0f, 0.07f, 0.01f, 0.01f);
                    if (v % 2 == 0) Box(p * Matrix4x4.TRS(new Vector3(0f, 0.004f, -0.03f), Quaternion.Euler(0f, 260f, 5f), Vector3.one), PropPaint.Orange, 0.2f, 0f, 0f, 0.4f, 0.008f, 0.008f);
                    return true;
                case PropKind.MoldTray:   // a seed tray of grey-green mold, tended by someone (the cult's farm)
                    Box(p, PropPaint.Dark, 0, 0.025f, 0, 0.55f, 0.05f, 0.35f);
                    Box(p, PropPaint.Leaf, 0, 0.055f, 0, 0.5f, 0.02f, 0.3f);
                    for (int i = 0; i < 6; i++)
                        Box(p, i % 2 == 0 ? PropPaint.White : PropPaint.Green, -0.18f + (i % 3) * 0.18f, 0.07f, -0.07f + (i / 3) * 0.14f, 0.09f, 0.025f, 0.08f);
                    return true;
                case PropKind.BadgeReader:
                    Box(p, PropPaint.Dark, 0, 1.25f, 0.015f, 0.08f, 0.12f, 0.03f);
                    Box(p, PropPaint.Red, 0, 1.29f, 0.031f, 0.012f, 0.012f, 0.004f);
                    return true;
            }
            return false;
        }
    }
}
