using UnityEngine;

namespace Backrooms.World
{
    /// Level 1's props: the parking garage, the warehouse, and the Ouroboros / Garden / Fabled sectors (our own takes on
    /// the wiki's sectors). Box-built; cars use Blender models when they're there.
    public static partial class PropLibrary
    {
        /// A car body lofted from a side profile: bonnet, windscreen, roof, rear window, boot. Front is local -z.
        /// Under a tarp the same shape is puffed out, wrinkled, and hangs nearly to the floor.
        static void Car(Matrix4x4 p, BoxMeshBuilder mesh, bool tarp, int v)
        {
            float[] zs = { -2.25f, -2.12f, -1.45f, -0.8f, -0.32f, 0.85f, 1.35f, 2.05f, 2.25f };
            float[] top = { 0.58f, 0.8f, 0.88f, 0.94f, 1.36f, 1.36f, 0.98f, 0.9f, 0.62f };
            const float W = 1.78f, roofW = 1.32f;
            float sill = tarp ? 0.14f : 0.3f, puff = tarp ? 0.07f : 0f;
            var paintBody = tarp ? PropPaint.Tarp : (v % 4 == 0 ? PropPaint.Red : v % 4 == 1 ? PropPaint.Blue : v % 4 == 2 ? PropPaint.White : PropPaint.Green);
            var paintGlass = tarp ? PropPaint.Tarp : PropPaint.Glass;
            Vector3 Wrinkle(Vector3 q, float z)
            {
                if (!tarp) return q;
                float n = Mathf.PerlinNoise(z * 2.3f + v, q.y * 3.1f + q.x * 1.7f) - 0.5f;
                return q + new Vector3(Mathf.Sign(q.x) * n * 0.05f, n * 0.03f, 0f);
            }
            Vector3[] Ring(int i)
            {
                float z = zs[i], t = top[i] + puff, belt = Mathf.Min(t, 0.86f + puff);
                float wTop = top[i] > 1.0f ? roofW + puff * 2f : W * 0.94f + puff * 2f;
                float w = W + puff * 2f + (tarp ? 0.06f : 0f);
                return new[]
                {
                    Wrinkle(new Vector3(-w / 2f, sill, z), z), Wrinkle(new Vector3(-W / 2f - puff, belt, z), z), Wrinkle(new Vector3(-wTop / 2f, t, z), z),
                    Wrinkle(new Vector3(wTop / 2f, t, z), z), Wrinkle(new Vector3(W / 2f + puff, belt, z), z), Wrinkle(new Vector3(w / 2f, sill, z), z),
                };
            }
            Vector3 P(Vector3 q) => p.MultiplyPoint3x4(q);
            Vector3 N(Vector3 q) => p.MultiplyVector(q);
            for (int i = 0; i + 1 < zs.Length; i++)
            {
                var a = Ring(i);
                var b = Ring(i + 1);
                bool cabin = top[i] > 1.0f || top[i + 1] > 1.0f;
                for (int k = 0; k < 5; k++)
                {
                    var mid = (a[k] + a[k + 1] + b[k] + b[k + 1]) / 4f;
                    var outward = k == 2 ? Vector3.up + Vector3.forward * (top[i + 1] - top[i]) * -0.5f : new Vector3(mid.x, (k == 1 || k == 3) ? 0.6f : 0f, 0f);
                    bool glass = cabin && (k == 1 || k == 3 || (k == 2 && Mathf.Abs(top[i + 1] - top[i]) > 0.2f));
                    mesh.AddQuad(P(a[k]), P(a[k + 1]), P(b[k + 1]), P(b[k]), N(outward), (int)(glass ? paintGlass : paintBody));
                }
            }
            // The ends.
            foreach (var (i, dir) in new[] { (0, -1f), (zs.Length - 1, 1f) })
            {
                var r = Ring(i);
                mesh.AddQuad(P(r[0]), P(r[1]), P(r[4]), P(r[5]), N(Vector3.forward * dir), (int)paintBody);
                mesh.AddQuad(P(r[1]), P(r[2]), P(r[3]), P(r[4]), N(Vector3.forward * dir), (int)paintBody);
            }
            // Wheels (under a tarp you only see the bottoms of the tyres).
            foreach (float x in new[] { -0.8f, 0.8f })
                foreach (float z in new[] { -1.38f, 1.42f })
                    mesh.AddOrientedCylinder(p, new Vector3(x, 0.32f, z), 0.32f, 0.22f, (int)PropPaint.Dark, 14, true);
            if (tarp) return;
            foreach (float x in new[] { -0.62f, 0.62f })
            {
                mesh.AddOrientedBox(p, new Vector3(x, 0.66f, -2.26f), new Vector3(0.36f, 0.12f, 0.02f), (int)PropPaint.White);   // headlamps
                mesh.AddOrientedBox(p, new Vector3(x, 0.72f, 2.26f), new Vector3(0.34f, 0.1f, 0.02f), (int)PropPaint.Red);      // tail lamps
            }
            foreach (float z in new[] { -2.3f, 2.3f }) mesh.AddOrientedBox(p, new Vector3(0f, 0.42f, z), new Vector3(1.8f, 0.14f, 0.1f), (int)PropPaint.Metal);   // bumpers
        }

        /// A balloon on a string: tied at `knot`, floating at `at` (both local to `p`).
        public static void Balloon(Matrix4x4 p, Vector3 knot, Vector3 at, PropPaint colour, BoxMeshBuilder mesh)
        {
            Vector3 a = p.MultiplyPoint3x4(knot), b = p.MultiplyPoint3x4(at + Vector3.down * 0.2f);
            var s = Matrix4x4.TRS((a + b) / 2f, Quaternion.LookRotation(b - a), Vector3.one);
            mesh.AddOrientedBox(s, Vector3.zero, new Vector3(0.006f, 0.006f, (b - a).magnitude), (int)PropPaint.White);
            mesh.AddSphere(p.MultiplyPoint3x4(at), new Vector3(0.16f, 0.2f, 0.16f), (int)colour);
            mesh.AddOrientedBox(Matrix4x4.Translate(p.MultiplyPoint3x4(at + Vector3.down * 0.205f)), Vector3.zero, new Vector3(0.03f, 0.03f, 0.03f), (int)colour);
        }

        static bool BuildGarage(PropSpec prop, BoxMeshBuilder mesh, float ceiling)
        {
            var p = prop.Pose;
            int v = prop.Variant;
            void Box(Matrix4x4 m, PropPaint paint, float cx, float cy, float cz, float sx, float sy, float sz) =>
                mesh.AddOrientedBox(m, new Vector3(cx, cy, cz), new Vector3(sx, sy, sz), (int)paint);
            Matrix4x4 Sub(float x, float y, float z, float pitch, float yaw, float roll) =>
                p * Matrix4x4.TRS(new Vector3(x, y, z), Quaternion.Euler(pitch, yaw, roll), Vector3.one);
            float Hash(int i) => WorldHash.Value((uint)(v * 7919 + 17), i, 0, 0x6A);

            switch (prop.Kind)
            {
                case PropKind.WallLadder:   // fixed to the wall (+z out of it), `ceiling` tall
                {
                    float h = ceiling;
                    foreach (float x in new[] { -0.22f, 0.22f }) Box(p, PropPaint.Metal, x, h / 2f, 0.1f, 0.05f, h, 0.05f);
                    for (float y = 0.3f; y < h - 0.05f; y += 0.3f) Box(p, PropPaint.Metal, 0f, y, 0.1f, 0.44f, 0.035f, 0.035f);
                    for (float y = 1f; y < h; y += 2f) foreach (float x in new[] { -0.22f, 0.22f }) Box(p, PropPaint.Dark, x, y, 0.04f, 0.04f, 0.04f, 0.08f);
                    return true;
                }
                case PropKind.WallLamp:   // a caged bulkhead lamp
                    Box(p, PropPaint.Dark, 0f, 0f, 0.025f, 0.18f, 0.22f, 0.05f);
                    Box(p, PropPaint.Warm, 0f, 0f, 0.08f, 0.11f, 0.14f, 0.07f);
                    foreach (float x in new[] { -0.06f, 0f, 0.06f }) Box(p, PropPaint.Metal, x, 0f, 0.12f, 0.01f, 0.18f, 0.01f);
                    return true;
                case PropKind.StallLines:   // painted bays: lines 5 m long every 2.6 m, a strip at the head
                {
                    int n = 3 + v % 4;
                    var paint = v % 3 == 0 ? PropPaint.Yellow : PropPaint.White;
                    for (int i = 0; i <= n; i++) Box(p, paint, (i - n / 2f) * 2.6f, 0.004f, 2.5f, 0.12f, 0.006f, 5f);
                    return true;
                }
                case PropKind.WheelStop:
                    Box(p, PropPaint.Concrete, 0f, 0.07f, 0f, 1.8f, 0.14f, 0.18f);
                    foreach (float x in new[] { -0.6f, 0f, 0.6f }) Box(p, PropPaint.Yellow, x, 0.141f, 0f, 0.3f, 0.004f, 0.18f);
                    return true;
                case PropKind.ParkingSign:   // hangs from the ceiling: "P" and the deck number, an arrow
                {
                    float y = ceiling - 0.75f;
                    foreach (float x in new[] { -0.45f, 0.45f }) Box(p, PropPaint.Metal, x, (y + ceiling) / 2f, 0f, 0.02f, ceiling - y, 0.02f);
                    Box(p, PropPaint.Blue, 0f, y, 0f, 1.3f, 0.4f, 0.04f);
                    var face = Sub(0f, 0f, 0.025f, 0f, 0f, 0f);
                    Digits(face, "P" + Mathf.Abs(v % 10), new Vector3(-0.3f, y, 0f), mesh);
                    float dir = (v / 10) % 2 == 0 ? 1f : -1f;   // arrow toward the ramp
                    Box(face, PropPaint.White, 0.25f, y, 0f, 0.36f, 0.05f, 0.01f);
                    Box(Sub(0.25f + 0.13f * dir, y + 0.05f, 0.025f, 0f, 0f, -40f * dir), PropPaint.White, 0f, 0f, 0f, 0.15f, 0.04f, 0.01f);
                    Box(Sub(0.25f + 0.13f * dir, y - 0.05f, 0.025f, 0f, 0f, 40f * dir), PropPaint.White, 0f, 0f, 0f, 0.15f, 0.04f, 0.01f);
                    return true;
                }
                case PropKind.PillarNumber:   // a painted band round a pillar with its bay number on every face
                {
                    float half = 0.3f + (v / 100) * 0.05f;
                    string num = (v % 100).ToString();
                    for (int s = 0; s < 4; s++)
                    {
                        var f = Sub(0f, 0f, 0f, 0f, s * 90f, 0f);
                        Box(f, PropPaint.Yellow, 0f, 1.9f, half + 0.004f, half * 2f, 0.5f, 0.006f);
                        Digits(f * Matrix4x4.Translate(new Vector3(0f, 0f, half + 0.012f)), num, new Vector3(0f, 1.9f, 0f), mesh, PropPaint.Dark);
                    }
                    return true;
                }
                case PropKind.Barrier:   // a concrete jersey barrier with hazard stripes
                    Box(p, PropPaint.Concrete, 0f, 0.15f, 0f, 2f, 0.3f, 0.6f);
                    Box(p, PropPaint.Concrete, 0f, 0.5f, 0f, 2f, 0.4f, 0.3f);
                    Box(p, PropPaint.Concrete, 0f, 0.76f, 0f, 2f, 0.12f, 0.2f);
                    for (int i = 0; i < 4; i++) Box(Sub(-0.75f + i * 0.5f, 0.5f, 0.151f, 0f, 0f, 35f), PropPaint.Yellow, 0f, 0f, 0f, 0.12f, 0.45f, 0.004f);
                    return true;
                case PropKind.SpeedBump:
                    for (int i = 0; i < 8; i++) Box(p, i % 2 == 0 ? PropPaint.Yellow : PropPaint.Dark, (i - 3.5f) * 0.5f, 0.04f, 0f, 0.5f, 0.08f, 0.35f);
                    return true;
                case PropKind.OilStain:
                    Box(Sub(0f, 0.003f, 0f, 0f, v * 37f, 0f), PropPaint.Oil, 0f, 0f, 0f, 0.9f + Hash(1) * 0.8f, 0.004f, 0.6f + Hash(2) * 0.6f);
                    return true;
                case PropKind.TarpCar:   // a car-shaped lump under a dusty tarp, tyres showing underneath
                    if (ModelLibrary.Add(mesh, "TarpCar", p)) return true;
                    Car(p, mesh, true, v);
                    return true;
                case PropKind.OldCar:   // an old boxy sedan, left where it was parked
                    if (ModelLibrary.Add(mesh, "OldCar", p)) return true;
                    Car(p, mesh, false, v);
                    return true;
                // ---------- Ouroboros: forever being built ----------
                case PropKind.Scaffold:
                {
                    float h = Mathf.Min(ceiling - 0.2f, 4f);
                    foreach (float x in new[] { -1f, 1f }) foreach (float z in new[] { -0.5f, 0.5f }) Box(p, PropPaint.Metal, x, h / 2f, z, 0.05f, h, 0.05f);
                    for (float y = 1.4f; y < h; y += 1.4f)
                    {
                        Box(p, PropPaint.Wood, 0f, y, 0f, 2.2f, 0.05f, 1.1f);
                        Box(Sub(0f, y - 0.7f, 0.5f, 0f, 0f, 32f), PropPaint.Metal, 0f, 0f, 0f, 2.3f, 0.04f, 0.04f);
                    }
                    return true;
                }
                case PropKind.WorkLamp:   // a work light on a tripod, pointed at a half-built wall
                    foreach (float a in new[] { 0f, 120f, 240f }) Box(Sub(0f, 0.6f, 0f, 0f, a, 0f) * Matrix4x4.Rotate(Quaternion.Euler(18f, 0f, 0f)), PropPaint.Metal, 0f, 0f, 0.18f, 0.03f, 1.25f, 0.03f);
                    Box(p, PropPaint.Yellow, 0f, 1.35f, 0f, 0.3f, 0.25f, 0.15f);
                    Box(p, PropPaint.Warm, 0f, 1.35f, 0.08f, 0.24f, 0.18f, 0.02f);
                    return true;
                case PropKind.TrafficCone:
                    Box(p, PropPaint.Dark, 0f, 0.015f, 0f, 0.36f, 0.03f, 0.36f);
                    for (int i = 0; i < 5; i++)
                    {
                        float s = 0.26f - i * 0.045f;
                        Box(p, i == 2 ? PropPaint.White : PropPaint.Red, 0f, 0.07f + i * 0.12f, 0f, s, 0.12f, s);
                    }
                    return true;
                case PropKind.BlockWall:   // a half-built wall: courses of block, the top course unfinished
                {
                    int courses = 3 + v % 4;
                    for (int c = 0; c < courses; c++)
                        for (int b = 0; b < 5; b++)
                        {
                            if (c == courses - 1 && b > v % 5) break;
                            float off = c % 2 == 0 ? 0f : 0.2f;
                            Box(p, PropPaint.Concrete, -0.8f + b * 0.4f + off, 0.1f + c * 0.2f, 0f, 0.39f, 0.19f, 0.2f);
                        }
                    return true;
                }
                case PropKind.PlasticSheet:   // hangs from the ceiling, milky and half see-through
                    Box(Sub(0f, 0f, 0f, 0f, 0f, (Hash(3) - 0.5f) * 4f), PropPaint.Glass, 0f, ceiling / 2f + 0.1f, 0f, 2.6f, ceiling - 0.2f, 0.01f);
                    return true;
                // ---------- Garden: the concrete is giving way ----------
                case PropKind.Vines:   // strands down a wall from the ceiling
                {
                    int n = 4 + v % 5;
                    for (int i = 0; i < n; i++)
                    {
                        float x = (Hash(i) - 0.5f) * 1.8f, len = 0.8f + Hash(i + 20) * (ceiling - 1f);
                        Box(p, PropPaint.Leaf, x, ceiling - len / 2f, 0.03f, 0.03f, len, 0.02f);
                        for (float y = ceiling - 0.2f; y > ceiling - len; y -= 0.25f)
                            Box(Sub(x + (Hash((int)(y * 10) + i) - 0.5f) * 0.12f, y, 0.05f, 0f, 0f, Hash(i * 3 + (int)(y * 7)) * 90f), PropPaint.Leaf, 0f, 0f, 0f, 0.12f, 0.08f, 0.02f);
                    }
                    return true;
                }
                case PropKind.Plant:   // a clump pushing up through a crack
                    for (int i = 0; i < 9; i++)
                    {
                        float a = i * 40f + Hash(i) * 20f, h = 0.4f + Hash(i + 9) * 0.9f;
                        Box(Sub(0f, 0f, 0f, 0f, a, 0f) * Matrix4x4.Rotate(Quaternion.Euler(18f + Hash(i + 3) * 25f, 0f, 0f)), PropPaint.Leaf, 0f, h / 2f, 0f, 0.14f, h, 0.02f);
                    }
                    return true;
                case PropKind.MossPatch:
                    Box(Sub(0f, 0.004f, 0f, 0f, v * 53f, 0f), PropPaint.Leaf, 0f, 0f, 0f, 1.4f + Hash(1), 0.008f, 1f + Hash(2));
                    return true;
                // ---------- Fabled: neon and cables over old wood ----------
                case PropKind.NeonStrip:   // a coloured tube along a wall
                    mesh.AddOrientedCylinder(p, new Vector3(0f, 2.2f + (v % 3) * 0.25f, 0.06f), 0.02f, 2f, (int)(v % 2 == 0 ? PropPaint.NeonPink : PropPaint.NeonCyan), 8, true);
                    return true;
                case PropKind.Cables:   // drooping cables from the ceiling
                    for (int i = 0; i < 4; i++)
                    {
                        float x = (Hash(i) - 0.5f) * 2f, sag = 0.3f + Hash(i + 4) * 0.8f;
                        for (int k = 0; k < 6; k++)
                        {
                            float t0 = k / 6f, t1 = (k + 1) / 6f;
                            float y0 = ceiling - 0.05f - 4f * sag * t0 * (1 - t0), y1 = ceiling - 0.05f - 4f * sag * t1 * (1 - t1);
                            var a = new Vector3(x, y0, -1.5f + 3f * t0);
                            var b = new Vector3(x, y1, -1.5f + 3f * t1);
                            var m = p * Matrix4x4.TRS((a + b) / 2f, Quaternion.LookRotation(b - a), Vector3.one);
                            mesh.AddOrientedBox(m, Vector3.zero, new Vector3(0.025f, 0.025f, (b - a).magnitude + 0.01f), (int)(i == 1 ? PropPaint.Red : PropPaint.Dark));
                        }
                    }
                    return true;
                default:
                    return false;
            }
        }
    }
}
