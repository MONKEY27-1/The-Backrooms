using UnityEngine;

namespace Backrooms.World
{
    /// Level 9 props. The lamp, letterbox, bathroom, stove, swings, grill and bin are Blender models (Art/Level9Props.blend).
    public static partial class PropLibrary
    {
        static bool BuildLevel9(PropSpec prop, BoxMeshBuilder mesh, float ceiling)
        {
            var p = prop.Pose;
            int v = prop.Variant;
            float H(int i) => ((v * 7919 + i * 104729) % 1000) / 1000f;
            void Box(Matrix4x4 m, PropPaint paint, float x, float y, float z, float sx, float sy, float sz) => mesh.AddOrientedBox(m, new Vector3(x, y, z), new Vector3(sx, sy, sz), (int)paint);
            void Model(string name, PropPaint paint, float w, float h, float d) { if (!ModelLibrary.Add(mesh, name, p)) Box(p, paint, 0f, h / 2f, 0f, w, h, d); }
            switch (prop.Kind)
            {
                case PropKind.StreetLamp:   // variant 1: lit
                    if (!ModelLibrary.AddMapped(mesh, "L9StreetLamp", p, n => n == "Warm" ? (int)(v == 1 ? PropPaint.Warm : PropPaint.Glass) : System.Enum.TryParse(n, out PropPaint q) ? (int)q : (int)PropPaint.Metal))
                        Box(p, PropPaint.Metal, 0f, 3.1f, 0f, 0.15f, 6.2f, 0.15f);
                    return true;
                case PropKind.Mailbox: Model("L9Mailbox", PropPaint.Metal, 0.2f, 1.3f, 0.4f); return true;
                case PropKind.Toilet: Model("L9Toilet", PropPaint.White, 0.45f, 0.75f, 0.7f); return true;
                case PropKind.Bathtub: Model("L9Bathtub", PropPaint.White, 1.6f, 0.56f, 0.74f); return true;
                case PropKind.Stove: Model("L9Stove", PropPaint.White, 0.75f, 0.9f, 0.65f); return true;
                case PropKind.SwingSet: Model("L9Swing", PropPaint.Rust, 3.2f, 0.1f, 0.1f); return true;
                case PropKind.Grill: Model("L9Grill", PropPaint.Dark, 0.6f, 1f, 0.6f); return true;
                case PropKind.WheelieBin: if (!ModelLibrary.AddMapped(mesh, "L9Bin", p, n => (int)(n == "Green" ? PropPaint.Leaf : PropPaint.Dark))) Box(p, PropPaint.Leaf, 0f, 0.5f, 0f, 0.5f, 1f, 0.55f); return true;
                case PropKind.HouseWindow:   // unit-sized, scaled to the opening: x along the wall, y up from the sill
                {
                    Vector3 s = p.lossyScale;
                    var r = Matrix4x4.TRS(p.GetColumn(3), p.rotation, Vector3.one);
                    Box(r, PropPaint.Glass, 0f, s.y / 2f, 0f, s.x, s.y, 0.012f);
                    foreach (float x in new[] { -s.x / 2f + 0.03f, 0f, s.x / 2f - 0.03f }) Box(r, PropPaint.White, x, s.y / 2f, 0f, 0.06f, s.y, 0.06f);
                    foreach (float y in new[] { 0.03f, s.y / 2f, s.y - 0.03f }) Box(r, PropPaint.White, 0f, y, 0f, s.x, 0.06f, 0.06f);
                    foreach (float z in new[] { -0.11f, 0.11f })   // casing on both faces
                    {
                        foreach (float x in new[] { -s.x / 2f - 0.05f, s.x / 2f + 0.05f }) Box(r, PropPaint.White, x, s.y / 2f, z, 0.1f, s.y + 0.2f, 0.06f);
                        Box(r, PropPaint.White, 0f, s.y + 0.06f, z, s.x + 0.24f, 0.12f, 0.07f);
                        Box(r, PropPaint.White, 0f, -0.04f, z * 1.2f, s.x + 0.24f, 0.08f, 0.12f);
                    }
                    return true;
                }
                case PropKind.PlankFence:   // a 2 m panel
                    for (int i = 0; i < 12; i++)
                    {
                        if (H(i) < 0.04f) continue;
                        float h = 1.72f + H(i + 20) * 0.08f;
                        Box(p * Matrix4x4.Rotate(Quaternion.Euler(0f, 0f, (H(i + 40) - 0.5f) * 1.5f)), PropPaint.Wood, -0.917f + i * 0.1667f, h / 2f, 0f, 0.155f, h, 0.022f);
                    }
                    foreach (float y in new[] { 0.35f, 1.4f }) Box(p, PropPaint.Wood, 0f, y, 0.04f, 2f, 0.09f, 0.05f);
                    Box(p, PropPaint.Wood, -1f, 0.95f, 0.05f, 0.1f, 1.9f, 0.1f);
                    return true;
                case PropKind.ChainFence:   // a 4 m panel of wire, too tall to climb
                    foreach (float x in new[] { -2f, 0f }) Box(p, PropPaint.Metal, x, 1.5f, 0f, 0.07f, 3f, 0.07f);
                    foreach (float y in new[] { 0.1f, 2.95f }) Box(p, PropPaint.Metal, 0f, y, 0f, 4f, 0.04f, 0.04f);
                    for (int i = 0; i < 21; i++)
                    {
                        Box(p, PropPaint.Metal, -2f + i * 0.2f, 1.5f, 0f, 0.008f, 2.9f, 0.008f);
                        if (i < 15) Box(p, PropPaint.Metal, 0f, 0.1f + i * 0.2f, 0f, 4f, 0.008f, 0.008f);
                    }
                    return true;
                case PropKind.ArrowSign:   // +z is the way it points
                    Box(p, PropPaint.Metal, 0f, 1.25f, 0f, 0.06f, 2.5f, 0.06f);
                    foreach (float x in new[] { -0.02f, 0.02f })
                    {
                        Box(p, PropPaint.White, x * 1.5f, 2.2f, 0.1f, 0.02f, 0.42f, 1f);
                        Box(p, PropPaint.Dark, x * 2.1f, 2.2f, 0.05f, 0.02f, 0.07f, 0.6f);
                        foreach (float k in new[] { -1f, 1f }) Box(p * Matrix4x4.TRS(new Vector3(x * 2.1f, 2.2f + k * 0.07f, 0.4f), Quaternion.Euler(k * 45f, 0f, 0f), Vector3.one), PropPaint.Dark, 0f, 0f, -0.04f, 0.02f, 0.07f, 0.26f);
                    }
                    return true;
                case PropKind.KeyRack:   // twelve hooks, eleven keys
                    Box(p, PropPaint.Wood, 0f, 0f, 0.015f, 0.8f, 0.5f, 0.03f);
                    for (int i = 0; i < 12; i++)
                    {
                        float x = -0.3f + i % 4 * 0.2f, y = 0.17f - i / 4 * 0.16f;
                        Box(p, PropPaint.Brass, x, y, 0.045f, 0.015f, 0.015f, 0.04f);
                        if (i == 10) continue;
                        Box(p, PropPaint.Brass, x, y - 0.05f, 0.05f, 0.012f, 0.06f, 0.006f);
                        Box(p, PropPaint.Red, x, y - 0.1f, 0.05f, 0.05f, 0.06f, 0.008f);
                    }
                    return true;
                case PropKind.PinnedPapers:
                    for (int i = 0; i < 5; i++)
                    {
                        var m = p * Matrix4x4.TRS(new Vector3((H(i) - 0.5f) * 0.6f, (H(i + 9) - 0.5f) * 0.9f, 0.012f + i * 0.003f), Quaternion.Euler(0f, 0f, (H(i + 18) - 0.5f) * 24f), Vector3.one);
                        Box(m, i % 3 == 0 ? PropPaint.Cardboard : PropPaint.White, 0f, 0f, 0f, 0.21f, 0.28f, 0.002f);
                        for (int l = 0; l < 4; l++) Box(m, PropPaint.Dark, 0f, 0.07f - l * 0.045f, 0.002f, 0.15f - H(i + l) * 0.05f, 0.008f, 0.001f);
                        Box(m, PropPaint.Red, 0f, 0.125f, 0.004f, 0.015f, 0.015f, 0.01f);
                    }
                    return true;
                case PropKind.KnotRail:   // rope, tied twice round the rail
                    for (int i = 0; i < 4; i++) Box(p * Matrix4x4.TRS(new Vector3(i * 0.03f - 0.045f, 0f, 0f), Quaternion.Euler(i * 11f, 0f, i % 2 * 24f - 12f), Vector3.one), PropPaint.Cardboard, 0f, -0.03f, 0f, 0.024f, 0.12f, 0.12f);
                    Box(p * Matrix4x4.Rotate(Quaternion.Euler(0f, 0f, 14f)), PropPaint.Cardboard, 0.03f, -0.22f, 0.05f, 0.022f, 0.3f, 0.022f);
                    Box(p, PropPaint.Cardboard, -0.03f, -0.16f, 0.06f, 0.022f, 0.2f, 0.022f);
                    return true;
                case PropKind.TapeMachine:   // a reel-to-reel recorder on the table
                    Box(p, PropPaint.Dark, 0f, 0.09f, 0f, 0.42f, 0.18f, 0.34f);
                    Box(p, PropPaint.Metal, 0f, 0.185f, 0f, 0.38f, 0.012f, 0.3f);
                    foreach (float x in new[] { -0.095f, 0.095f })
                    {
                        mesh.AddSphere(p.MultiplyPoint3x4(new Vector3(x, 0.2f, -0.03f)), new Vector3(0.08f, 0.008f, 0.08f), (int)PropPaint.Mahogany, 4, 14);
                        Box(p, PropPaint.White, x, 0.21f, -0.03f, 0.025f, 0.01f, 0.025f);
                    }
                    for (int i = 0; i < 5; i++) Box(p, PropPaint.White, -0.12f + i * 0.06f, 0.195f, 0.11f, 0.04f, 0.02f, 0.03f);
                    Box(p, PropPaint.Red, 0.17f, 0.195f, 0.11f, 0.02f, 0.02f, 0.02f);
                    return true;
            }
            return false;
        }
    }
}
