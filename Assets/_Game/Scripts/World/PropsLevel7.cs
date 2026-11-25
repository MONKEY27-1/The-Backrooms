using UnityEngine;

namespace Backrooms.World
{
    /// Level 7 props: bones on the sea floor (the wiki's skeletons in the Twilight and Midnight zones) and the Abyss's tar.
    public static partial class PropLibrary
    {
        static bool BuildLevel7(PropSpec prop, BoxMeshBuilder mesh, float ceiling)
        {
            var p = prop.Pose;
            int v = prop.Variant;
            float H(int i) => ((v * 7919 + i * 104729) % 1000) / 1000f;
            void Box(Matrix4x4 m, PropPaint paint, Vector3 c, Vector3 s) => mesh.AddOrientedBox(m, c, s, (int)paint);
            switch (prop.Kind)
            {
                case PropKind.Bones:   // a skeleton come apart: skull, a curl of ribs, long bones scattered
                {
                    Box(p * Matrix4x4.TRS(new Vector3(0.3f, 0.09f, 0.4f), Quaternion.Euler(H(1) * 40f, H(2) * 360f, 0f), Vector3.one), PropPaint.White, Vector3.zero, new Vector3(0.17f, 0.18f, 0.21f));
                    for (int i = 0; i < 6; i++)
                        Box(p * Matrix4x4.TRS(new Vector3(-0.1f, 0.04f, -0.05f + i * 0.07f), Quaternion.Euler(0f, 0f, 70f + H(i) * 30f), Vector3.one), PropPaint.White, new Vector3(0f, 0.13f, 0f), new Vector3(0.025f, 0.28f, 0.03f));
                    for (int i = 0; i < 4; i++)
                        Box(p * Matrix4x4.TRS(new Vector3((H(i + 10) - 0.5f) * 1.2f, 0.03f, (H(i + 20) - 0.5f) * 1.2f), Quaternion.Euler(0f, H(i + 30) * 360f, 0f), Vector3.one), PropPaint.White, Vector3.zero, new Vector3(0.05f, 0.05f, 0.42f));
                    return true;
                }
                case PropKind.Beacon:   // a marker post at the cave mouth: a green lamp in a cage that never goes out
                    Box(p, PropPaint.Rust, new Vector3(0f, 1.2f, 0f), new Vector3(0.12f, 2.4f, 0.12f));
                    Box(p, PropPaint.Glow, new Vector3(0f, 2.6f, 0f), new Vector3(0.3f, 0.36f, 0.3f));
                    for (int i = 0; i < 4; i++) Box(p * Matrix4x4.Rotate(Quaternion.Euler(0f, i * 45f, 0f)), PropPaint.Metal, new Vector3(0f, 2.6f, 0f), new Vector3(0.42f, 0.44f, 0.02f));
                    return true;
                case PropKind.BoatWreck:
                    if (!ModelLibrary.Add(mesh, "L7Boat", p)) Box(p, PropPaint.Wood, new Vector3(0f, 0.5f, 0f), new Vector3(1.8f, 1f, 5f));
                    return true;
                case PropKind.RoadMarker:   // a M.E.G. post: hazard bands, a plate with a 9 on it, a lamp that still works
                {
                    for (int i = 0; i < 6; i++) Box(p, i % 2 == 0 ? PropPaint.Yellow : PropPaint.Dark, new Vector3(0f, 0.15f + i * 0.3f, 0f), new Vector3(0.09f, 0.3f, 0.09f));
                    Box(p, PropPaint.White, new Vector3(0f, 1.5f, 0.06f), new Vector3(0.34f, 0.46f, 0.02f));
                    void Bar(float x, float y, float w, float h) => Box(p, PropPaint.Dark, new Vector3(-x, 1.5f + y, 0.075f), new Vector3(w, h, 0.01f));
                    Bar(0f, 0.15f, 0.2f, 0.045f); Bar(0f, 0f, 0.2f, 0.045f); Bar(0f, -0.15f, 0.2f, 0.045f);
                    Bar(-0.08f, 0.075f, 0.045f, 0.15f); Bar(0.08f, 0f, 0.045f, 0.3f);   // the number the road is named for
                    Box(p, PropPaint.Warm, new Vector3(0f, 1.87f, 0f), new Vector3(0.12f, 0.14f, 0.12f));
                    Box(p, PropPaint.Metal, new Vector3(0f, 1.96f, 0f), new Vector3(0.18f, 0.03f, 0.18f));
                    return true;
                }
                case PropKind.TarMound:   // a lump of black tar, soft-edged, glossy
                    for (int i = 0; i < 5; i++)
                    {
                        float r = 0.5f + H(i) * 0.9f;
                        Box(p * Matrix4x4.TRS(new Vector3((H(i + 5) - 0.5f) * 1.4f, 0f, (H(i + 9) - 0.5f) * 1.4f), Quaternion.Euler(0f, H(i + 13) * 90f, 0f), Vector3.one), PropPaint.Oil,
                            new Vector3(0f, r * 0.35f, 0f), new Vector3(r * 1.3f, r * 0.7f, r));
                    }
                    return true;
            }
            return false;
        }
    }
}
