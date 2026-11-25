using UnityEngine;

namespace Backrooms.World
{
    /// Level 3 "Electrical Station" props. The machinery is Blender-made (Art/Level3Props.blend -> Resources/Models/L3*);
    /// each has a blocky stand-in so the level still works if a model fails to import.
    public static partial class PropLibrary
    {
        /// How far a Level 3 bulb hangs below its ceiling: short cords in the tunnels, long pendants in the halls.
        public static float BulbDrop(float ceiling, int variant) => ceiling > 4.5f ? 1.6f + (variant % 3) * 0.7f : 0.4f + (variant % 3) * 0.25f;

        static bool BuildLevel3(PropSpec prop, BoxMeshBuilder mesh, float ceiling)
        {
            var p = prop.Pose;
            int v = prop.Variant;
            void Box(Matrix4x4 m, PropPaint paint, float cx, float cy, float cz, float sx, float sy, float sz) =>
                mesh.AddOrientedBox(m, new Vector3(cx, cy, cz), new Vector3(sx, sy, sz), (int)paint);
            Matrix4x4 Sub(float x, float y, float z, float yaw) => p * Matrix4x4.TRS(new Vector3(x, y, z), Quaternion.Euler(0f, yaw, 0f), Vector3.one);

            switch (prop.Kind)
            {
                case PropKind.Generator:
                    if (ModelLibrary.Add(mesh, "L3Generator", p)) return true;
                    Box(p, PropPaint.Yellow, 0, 1.1f, 0, 4f, 2.2f, 1.8f);
                    return true;
                case PropKind.Transformer:
                    if (ModelLibrary.Add(mesh, "L3Transformer", p)) return true;
                    Box(p, PropPaint.Green, 0, 0.9f, 0, 1.6f, 1.8f, 1.2f);
                    return true;
                case PropKind.Conveyor:
                    if (ModelLibrary.Add(mesh, "L3Conveyor", p)) return true;
                    Box(p, PropPaint.Metal, 0, 0.8f, 0, 1f, 0.2f, 2f);
                    return true;
                case PropKind.ConveyorMotor:
                    if (ModelLibrary.Add(mesh, "L3ConveyorMotor", p)) return true;
                    Box(p, PropPaint.Green, -0.75f, 0.6f, 0, 0.5f, 1.2f, 0.5f);
                    return true;
                case PropKind.BreakerPanel:
                    if (ModelLibrary.Add(mesh, "L3BreakerPanel", p)) return true;
                    Box(p, PropPaint.Metal, 0, 1.45f, 0.11f, 0.8f, 1.1f, 0.22f);
                    return true;
                case PropKind.SludgePipe:
                    if (ModelLibrary.Add(mesh, "L3SludgePipe", p)) return true;
                    Box(p, PropPaint.Rust, 0, 0.55f, 0.22f, 2f, 0.28f, 0.28f);
                    return true;
                case PropKind.Plinth:
                    if (ModelLibrary.Add(mesh, "L3Plinth", p)) return true;
                    Box(p, PropPaint.White, 0, 0.45f, 0, 1.6f, 0.9f, 1.6f);
                    return true;
                case PropKind.BarFence:   // a fixed run of the bar-gate model (fences round the transformer bays)
                    if (ModelLibrary.Add(mesh, "L3BarGate", p)) return true;
                    for (int i = 0; i < 13; i++) Box(p, PropPaint.Rust, -0.9f + i * 0.15f, 1.5f, 0, 0.04f, 3f, 0.04f);
                    return true;
                case PropKind.CableTray:   // a 2 m ladder tray along local x, hung on rods; variant picks how full it is
                {
                    float y = ceiling - 0.45f - (v % 2) * 0.25f;
                    foreach (float z in new[] { -0.22f, 0.22f }) Box(p, PropPaint.Metal, 0, y, z, 2f, 0.08f, 0.02f);
                    for (int i = 0; i < 6; i++) Box(p, PropPaint.Metal, -0.83f + i * 0.33f, y - 0.03f, 0, 0.03f, 0.015f, 0.44f);
                    int cables = 3 + v % 4;
                    for (int i = 0; i < cables; i++)
                        Box(p, i % 3 == 0 ? PropPaint.Orange : PropPaint.Dark, 0, y + 0.0f + (i / 4) * 0.045f, -0.17f + (i % 4) * 0.1f, 2f, 0.04f, 0.04f);
                    foreach (float x in new[] { -0.9f, 0.9f })
                        foreach (float z in new[] { -0.24f, 0.24f }) Box(p, PropPaint.Dark, x, (y + ceiling) / 2f, z, 0.015f, ceiling - y, 0.015f);
                    if (v % 5 == 0) Box(Sub(0.4f, y - 0.35f, 0.1f, 20f), PropPaint.Dark, 0, 0, 0, 0.03f, 0.7f, 0.03f);   // a cable hanging loose
                    return true;
                }
                case PropKind.CageBulb:   // a bare bulb in a wire cage on a cord (the light itself is a panel)
                {
                    float drop = BulbDrop(ceiling, v), y = ceiling - drop;
                    Box(p, PropPaint.Dark, 0, (y + ceiling) / 2f + 0.06f, 0, 0.012f, drop - 0.12f, 0.012f);   // the cord
                    Box(p, PropPaint.Metal, 0, y + 0.04f, 0, 0.06f, 0.07f, 0.06f);                             // the socket
                    Box(p, v % 4 == 0 ? PropPaint.Dark : PropPaint.Warm, 0, y - 0.06f, 0, 0.07f, 0.1f, 0.07f); // the bulb; dead ones dark
                    for (int i = 0; i < 6; i++)   // cage wires round the bulb, and a ring at the bottom
                    {
                        float a = i * Mathf.PI / 3f;
                        Box(p, PropPaint.Dark, Mathf.Cos(a) * 0.075f, y - 0.06f, Mathf.Sin(a) * 0.075f, 0.007f, 0.16f, 0.007f);
                    }
                    Box(p, PropPaint.Dark, 0, y - 0.14f, 0, 0.12f, 0.007f, 0.12f);
                    return true;
                }
                case PropKind.SludgePool:   // a spreading black slick; the SludgeZone special does the harm
                {
                    var rng = new WorldRng((uint)(v * 7919 + 13));
                    for (int i = 0; i < 6; i++)
                    {
                        float a = rng.Value() * 360f, r = rng.Value() * 0.5f;
                        var m = Sub(Mathf.Cos(a) * r, 0.006f + i * 0.0006f, Mathf.Sin(a) * r, rng.Value() * 180f);
                        Box(m, PropPaint.Oil, 0, 0, 0, 0.7f + rng.Value() * 0.9f, 0.004f, 0.5f + rng.Value() * 0.7f);
                    }
                    return true;
                }
            }
            return false;
        }
    }
}
