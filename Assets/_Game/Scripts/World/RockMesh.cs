using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Backrooms.World
{
    /// Free-form geometry for natural places (Level 7's sea floor, Level 8's caves): smooth terrain from a density field,
    /// boulders, stalagmites, bones and weeds. Plain data, so layouts can fill it on worker threads.
    public sealed class RockMesh
    {
        public readonly List<Vector3> V = new List<Vector3>(), N = new List<Vector3>();   // world space
        public readonly List<int> T = new List<int>();
        public SurfaceRef Surface;
        public bool Collide;

        public RockMesh(SurfaceRef surface, bool collide = true) { Surface = surface; Collide = collide; }

        public Mesh ToMesh(string name, Vector3 origin)
        {
            var local = new List<Vector3>(V.Count);
            foreach (var v in V) local.Add(v - origin);
            var mesh = new Mesh { name = name, indexFormat = V.Count > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
            mesh.SetVertices(local);
            mesh.SetNormals(N);
            mesh.SetTriangles(T, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        int Add(Vector3 v, Vector3 n) { V.Add(v); N.Add(n); return V.Count - 1; }

        // ---------- noise ----------

        static float Lattice(int x, int y, int z)
        {
            unchecked
            {
                uint h = (uint)x * 374761393u + (uint)y * 668265263u + (uint)z * 2246822519u;
                h = (h ^ (h >> 13)) * 1274126177u;
                return ((h ^ (h >> 16)) & 0xFFFF) / 32767.5f - 1f;
            }
        }

        /// Smooth value noise, about -1..1.
        public static float Noise(float x, float y, float z)
        {
            int ix = Mathf.FloorToInt(x), iy = Mathf.FloorToInt(y), iz = Mathf.FloorToInt(z);
            float fx = x - ix, fy = y - iy, fz = z - iz;
            fx = fx * fx * (3f - 2f * fx); fy = fy * fy * (3f - 2f * fy); fz = fz * fz * (3f - 2f * fz);
            float a = Mathf.Lerp(Lattice(ix, iy, iz), Lattice(ix + 1, iy, iz), fx), b = Mathf.Lerp(Lattice(ix, iy + 1, iz), Lattice(ix + 1, iy + 1, iz), fx);
            float c = Mathf.Lerp(Lattice(ix, iy, iz + 1), Lattice(ix + 1, iy, iz + 1), fx), d = Mathf.Lerp(Lattice(ix, iy + 1, iz + 1), Lattice(ix + 1, iy + 1, iz + 1), fx);
            return Mathf.Lerp(Mathf.Lerp(a, b, fy), Mathf.Lerp(c, d, fy), fz);
        }

        // ---------- terrain ----------

        /// The surface between air (negative) and rock (positive) over one chunk: 34 x ny x 34 samples a metre apart, sample
        /// (1, 0, 1) on the chunk corner. Neighbouring chunks meet exactly because the field is a function of the world.
        public const int Samples = (int)MazeLayout.ChunkSize + 2;

        public static int Index(int i, int j, int k, int ny) => (i * ny + j) * Samples + k;

        public void SurfaceNets(float[] f, int ny, Vector3 corner)
        {
            const int n = Samples;
            int At(int i, int j, int k) => (i * ny + j) * n + k;
            var cell = new int[n * ny * n];   // vertex index + 1
            int Vertex(int i, int j, int k)
            {
                int at = At(i, j, k);
                if (cell[at] > 0) return cell[at] - 1;
                Vector3 sum = Vector3.zero;
                int count = 0;
                void Edge(int ax, int ay, int az, int bx, int by, int bz)
                {
                    float a = f[At(i + ax, j + ay, k + az)], b = f[At(i + bx, j + by, k + bz)];
                    if ((a < 0f) == (b < 0f)) return;
                    sum += Vector3.Lerp(new Vector3(ax, ay, az), new Vector3(bx, by, bz), a / (a - b));
                    count++;
                }
                Edge(0, 0, 0, 1, 0, 0); Edge(0, 1, 0, 1, 1, 0); Edge(0, 0, 1, 1, 0, 1); Edge(0, 1, 1, 1, 1, 1);
                Edge(0, 0, 0, 0, 1, 0); Edge(1, 0, 0, 1, 1, 0); Edge(0, 0, 1, 0, 1, 1); Edge(1, 0, 1, 1, 1, 1);
                Edge(0, 0, 0, 0, 0, 1); Edge(1, 0, 0, 1, 0, 1); Edge(0, 1, 0, 0, 1, 1); Edge(1, 1, 0, 1, 1, 1);
                var p = count > 0 ? sum / count : Vector3.one * 0.5f;
                float F(int x, int y, int z) => f[At(i + x, j + y, k + z)];
                float gx = Mathf.Lerp(Mathf.Lerp(F(1, 0, 0) - F(0, 0, 0), F(1, 1, 0) - F(0, 1, 0), p.y), Mathf.Lerp(F(1, 0, 1) - F(0, 0, 1), F(1, 1, 1) - F(0, 1, 1), p.y), p.z);
                float gy = Mathf.Lerp(Mathf.Lerp(F(0, 1, 0) - F(0, 0, 0), F(1, 1, 0) - F(1, 0, 0), p.x), Mathf.Lerp(F(0, 1, 1) - F(0, 0, 1), F(1, 1, 1) - F(1, 0, 1), p.x), p.z);
                float gz = Mathf.Lerp(Mathf.Lerp(F(0, 0, 1) - F(0, 0, 0), F(1, 0, 1) - F(1, 0, 0), p.x), Mathf.Lerp(F(0, 1, 1) - F(0, 1, 0), F(1, 1, 1) - F(1, 1, 0), p.x), p.y);
                int index = Add(corner + new Vector3(i - 1 + p.x, j + p.y, k - 1 + p.z), new Vector3(-gx, -gy, -gz).normalized);
                cell[at] = index + 1;
                return index;
            }
            void Quad(int a, int b, int c, int d, bool flip)
            {
                if (flip) { T.Add(a); T.Add(c); T.Add(b); T.Add(a); T.Add(d); T.Add(c); }
                else { T.Add(a); T.Add(b); T.Add(c); T.Add(a); T.Add(c); T.Add(d); }
            }
            for (int i = 1; i < n - 1; i++)
            for (int j = 1; j < ny - 1; j++)
            for (int k = 1; k < n - 1; k++)
            {
                bool air = f[At(i, j, k)] < 0f;
                if (air != f[At(i + 1, j, k)] < 0f) Quad(Vertex(i, j - 1, k - 1), Vertex(i, j, k - 1), Vertex(i, j, k), Vertex(i, j - 1, k), air);
                if (air != f[At(i, j + 1, k)] < 0f) Quad(Vertex(i - 1, j, k - 1), Vertex(i - 1, j, k), Vertex(i, j, k), Vertex(i, j, k - 1), air);
                if (air != f[At(i, j, k + 1)] < 0f) Quad(Vertex(i - 1, j - 1, k), Vertex(i, j - 1, k), Vertex(i, j, k), Vertex(i - 1, j, k), air);
            }
        }

        // ---------- shapes ----------

        /// A tube along a path: stalagmites, ribs, fingers, stems. Radius per point; a near-zero end radius closes it.
        public void Tube(IReadOnlyList<Vector3> path, IReadOnlyList<float> radius, int sides = 7, float lumpy = 0f, bool inside = false)
        {
            int start = V.Count, rings = path.Count;
            var side = Vector3.zero;
            for (int r = 0; r < rings; r++)
            {
                var along = (path[Mathf.Min(r + 1, rings - 1)] - path[Mathf.Max(r - 1, 0)]).normalized;
                if (r == 0) side = Vector3.Cross(along, Mathf.Abs(along.y) > 0.9f ? Vector3.right : Vector3.up);
                side = Vector3.ProjectOnPlane(side, along).normalized;
                var other = Vector3.Cross(along, side);
                for (int s = 0; s <= sides; s++)
                {
                    float a = s % sides * Mathf.PI * 2f / sides;
                    var o = side * Mathf.Cos(a) + other * Mathf.Sin(a);
                    var at = path[r] + o * radius[r];
                    if (lumpy > 0f) at += o * (Noise(at.x * 1.7f, at.y * 1.7f, at.z * 1.7f) * lumpy * radius[r]);
                    Add(at, inside ? -o : o);
                }
            }
            for (int r = 0; r < rings - 1; r++)
            for (int s = 0; s < sides; s++)
            {
                int a = start + r * (sides + 1) + s, b = a + sides + 1;
                if (inside) { T.Add(a); T.Add(b); T.Add(a + 1); T.Add(b); T.Add(b + 1); T.Add(a + 1); }
                else { T.Add(a); T.Add(a + 1); T.Add(b); T.Add(b); T.Add(a + 1); T.Add(b + 1); }
            }
        }

        /// A lumpy ellipsoid: boulders, skulls, palms of hands.
        public void Blob(Vector3 c, Vector3 r, Quaternion turn, float lumpy = 0.25f, int sides = 10)
        {
            int start = V.Count, rings = sides / 2 + 1;
            for (int i = 0; i <= rings; i++)
            for (int s = 0; s <= sides; s++)
            {
                float a = s % sides * Mathf.PI * 2f / sides, b = i * Mathf.PI / rings - Mathf.PI / 2f;
                var d = new Vector3(Mathf.Cos(a) * Mathf.Cos(b), Mathf.Sin(b), Mathf.Sin(a) * Mathf.Cos(b));
                float k = 1f + Noise(c.x + d.x * 1.3f, c.y + d.y * 1.3f, c.z + d.z * 1.3f) * lumpy;
                Add(c + turn * Vector3.Scale(d, r) * k, turn * new Vector3(d.x / r.x, d.y / r.y, d.z / r.z).normalized);
            }
            for (int i = 0; i < rings; i++)
            for (int s = 0; s < sides; s++)
            {
                int a = start + i * (sides + 1) + s, b = a + sides + 1;
                T.Add(a); T.Add(b); T.Add(a + 1); T.Add(b); T.Add(b + 1); T.Add(a + 1);
            }
        }

        /// A flat four-sided sheet, solid from both sides (roof planes, gables).
        public void Panel(Vector3 a, Vector3 b, Vector3 c, Vector3 d)
        {
            var n = Vector3.Cross(b - a, c - a).normalized;
            foreach (float s in new[] { 1f, -1f })
            {
                int i = Add(a, n * s);
                Add(b, n * s); Add(c, n * s); Add(d, n * s);
                if (s > 0f) { T.Add(i); T.Add(i + 1); T.Add(i + 2); T.Add(i); T.Add(i + 2); T.Add(i + 3); }
                else { T.Add(i); T.Add(i + 2); T.Add(i + 1); T.Add(i); T.Add(i + 3); T.Add(i + 2); }
            }
        }

        /// A flat leaf or fin (drawn with a two-sided material).
        public void Leaf(Vector3 a, Vector3 b, Vector3 wide)
        {
            var n = Vector3.Cross(b - a, wide).normalized;
            int i = Add(a, n);
            Add((a + b) / 2f + wide, n); Add(b, n); Add((a + b) / 2f - wide, n);
            T.Add(i); T.Add(i + 1); T.Add(i + 2); T.Add(i); T.Add(i + 2); T.Add(i + 3);
        }
    }
}
