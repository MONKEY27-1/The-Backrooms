using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Backrooms.World
{
    /// Builds one mesh out of boxes and quads. World-aligned boxes get UVs in world meters, so a texture
    /// continues seamlessly across separate wall pieces and chunk borders. Submeshes grow on demand.
    public sealed class BoxMeshBuilder
    {
        [Flags]
        public enum Faces { PosX = 1, NegX = 2, PosY = 4, NegY = 8, PosZ = 16, NegZ = 32, Sides = PosX | NegX | PosZ | NegZ, All = 63 }

        readonly List<Vector3> vertices = new List<Vector3>();
        readonly List<Vector3> normals = new List<Vector3>();
        readonly List<Vector2> uvs = new List<Vector2>();
        readonly List<List<int>> submeshes = new List<List<int>>();
        readonly Vector3 origin;

        public BoxMeshBuilder(Vector3 origin, int submeshCount)
        {
            this.origin = origin;
            for (int i = 0; i < submeshCount; i++) submeshes.Add(new List<int>());
        }

        public int SubmeshCount => submeshes.Count;
        public int VertexCount => vertices.Count;

        public void AddBox(Bounds box, Faces faces, int submesh) =>
            AddBox(box, faces, submesh, submesh, submesh, submesh, submesh, submesh);

        /// A world-aligned box with a submesh per face. Corners go bottom-left, top-left, top-right, bottom-right
        /// as seen from outside (Unity's clockwise front face).
        public void AddBox(Bounds box, Faces faces, int posX, int negX, int posY, int negY, int posZ, int negZ)
        {
            Vector3 a = box.min, b = box.max;
            if ((faces & Faces.PosX) != 0)
                Quad(posX, Vector3.right, new Vector3(b.x, a.y, a.z), new Vector3(b.x, b.y, a.z), new Vector3(b.x, b.y, b.z), new Vector3(b.x, a.y, b.z), p => new Vector2(p.z, p.y));
            if ((faces & Faces.NegX) != 0)
                Quad(negX, Vector3.left, new Vector3(a.x, a.y, b.z), new Vector3(a.x, b.y, b.z), new Vector3(a.x, b.y, a.z), new Vector3(a.x, a.y, a.z), p => new Vector2(-p.z, p.y));
            if ((faces & Faces.PosZ) != 0)
                Quad(posZ, Vector3.forward, new Vector3(b.x, a.y, b.z), new Vector3(b.x, b.y, b.z), new Vector3(a.x, b.y, b.z), new Vector3(a.x, a.y, b.z), p => new Vector2(-p.x, p.y));
            if ((faces & Faces.NegZ) != 0)
                Quad(negZ, Vector3.back, new Vector3(a.x, a.y, a.z), new Vector3(a.x, b.y, a.z), new Vector3(b.x, b.y, a.z), new Vector3(b.x, a.y, a.z), p => new Vector2(p.x, p.y));
            if ((faces & Faces.PosY) != 0)
                Quad(posY, Vector3.up, new Vector3(a.x, b.y, a.z), new Vector3(a.x, b.y, b.z), new Vector3(b.x, b.y, b.z), new Vector3(b.x, b.y, a.z), p => new Vector2(p.x, p.z));
            if ((faces & Faces.NegY) != 0)
                Quad(negY, Vector3.down, new Vector3(b.x, a.y, a.z), new Vector3(b.x, a.y, b.z), new Vector3(a.x, a.y, b.z), new Vector3(a.x, a.y, a.z), p => new Vector2(-p.x, p.z));
        }

        /// Like AddBox, but every vertical edge where both side faces show is cut at 45° by `bevel` meters (real wall
        /// corners are never knife-sharp; the cut catches a line of light). Edges where a face is hidden (a wall that
        /// continues into the next box) stay square, so joins don't get grooves. Top/bottom follow the cut outline.
        /// `corners` limits which vertical edges may be cut (bits: 1 = +x+z, 2 = +x-z, 4 = -x+z, 8 = -x-z).
        public void AddBevelledBox(Bounds box, Faces faces, float bevel, int corners, int posX, int negX, int posY, int negY, int posZ, int negZ)
        {
            Vector3 a = box.min, b = box.max;
            bool px = (faces & Faces.PosX) != 0, nx = (faces & Faces.NegX) != 0, pz = (faces & Faces.PosZ) != 0, nz = (faces & Faces.NegZ) != 0;
            bool cPP = px && pz && (corners & 1) != 0, cPN = px && nz && (corners & 2) != 0;   // which corners get cut
            bool cNP = nx && pz && (corners & 4) != 0, cNN = nx && nz && (corners & 8) != 0;
            float c = bevel;
            if (px) Quad(posX, Vector3.right, new Vector3(b.x, a.y, a.z + (cPN ? c : 0)), new Vector3(b.x, b.y, a.z + (cPN ? c : 0)), new Vector3(b.x, b.y, b.z - (cPP ? c : 0)), new Vector3(b.x, a.y, b.z - (cPP ? c : 0)), p => new Vector2(p.z, p.y));
            if (nx) Quad(negX, Vector3.left, new Vector3(a.x, a.y, b.z - (cNP ? c : 0)), new Vector3(a.x, b.y, b.z - (cNP ? c : 0)), new Vector3(a.x, b.y, a.z + (cNN ? c : 0)), new Vector3(a.x, a.y, a.z + (cNN ? c : 0)), p => new Vector2(-p.z, p.y));
            if (pz) Quad(posZ, Vector3.forward, new Vector3(b.x - (cPP ? c : 0), a.y, b.z), new Vector3(b.x - (cPP ? c : 0), b.y, b.z), new Vector3(a.x + (cNP ? c : 0), b.y, b.z), new Vector3(a.x + (cNP ? c : 0), a.y, b.z), p => new Vector2(-p.x, p.y));
            if (nz) Quad(negZ, Vector3.back, new Vector3(a.x + (cNN ? c : 0), a.y, a.z), new Vector3(a.x + (cNN ? c : 0), b.y, a.z), new Vector3(b.x - (cPN ? c : 0), b.y, a.z), new Vector3(b.x - (cPN ? c : 0), a.y, a.z), p => new Vector2(p.x, p.y));
            // The cuts take the material of the face they turn away from, wallpaper wrapping round the corner.
            void Cut(bool on, int sub, Vector3 n, float x0, float z0, float x1, float z1)
            {
                if (on) Quad(sub, n.normalized, new Vector3(x0, a.y, z0), new Vector3(x0, b.y, z0), new Vector3(x1, b.y, z1), new Vector3(x1, a.y, z1), p => new Vector2(p.x + p.z, p.y));
            }
            Cut(cPP, posX, new Vector3(1, 0, 1), b.x, b.z - c, b.x - c, b.z);
            Cut(cNP, posZ, new Vector3(-1, 0, 1), a.x + c, b.z, a.x, b.z - c);
            Cut(cNN, negX, new Vector3(-1, 0, -1), a.x, a.z + c, a.x + c, a.z);
            Cut(cPN, negZ, new Vector3(1, 0, -1), b.x - c, a.z, b.x, a.z + c);
            // Top and bottom: the outline, corners cut where the sides are, as a fan from the middle.
            var outline = new List<Vector2>();
            void Corner(bool cut, float x, float z, float ax, float az, float bx2, float bz2)
            {
                if (cut) { outline.Add(new Vector2(ax, az)); outline.Add(new Vector2(bx2, bz2)); }
                else outline.Add(new Vector2(x, z));
            }
            // Clockwise seen from above (x right, z up the page): (a,a) -> (a,b) -> (b,b) -> (b,a).
            Corner(cNN, a.x, a.z, a.x + c, a.z, a.x, a.z + c);
            Corner(cNP, a.x, b.z, a.x, b.z - c, a.x + c, b.z);
            Corner(cPP, b.x, b.z, b.x - c, b.z, b.x, b.z - c);
            Corner(cPN, b.x, a.z, b.x, a.z + c, b.x - c, a.z);
            void Cap(int sub, float y, bool up)
            {
                int start = vertices.Count;
                var n = up ? Vector3.up : Vector3.down;
                var mid = new Vector3((a.x + b.x) / 2, y, (a.z + b.z) / 2);
                vertices.Add(mid - origin); normals.Add(n); uvs.Add(new Vector2(mid.x, mid.z));
                foreach (var o in outline) { vertices.Add(new Vector3(o.x, y, o.y) - origin); normals.Add(n); uvs.Add(o); }
                var tris = Tris(sub);
                for (int i = 0; i < outline.Count; i++)
                {
                    int i0 = start + 1 + i, i1 = start + 1 + (i + 1) % outline.Count;
                    tris.Add(start);
                    if (up) { tris.Add(i0); tris.Add(i1); } else { tris.Add(i1); tris.Add(i0); }
                }
            }
            if ((faces & Faces.PosY) != 0) Cap(posY, b.y, true);
            if ((faces & Faces.NegY) != 0) Cap(negY, a.y, false);
        }

        /// A sloped strip: top from A to B (centre line), `width` across, solid down to `bottom` (or `thickness`
        /// below the top). Faces: top, both sides, the ends, and the underside when it doesn't reach the ground.
        public void AddSlope(Vector3 a, Vector3 b, float width, float bottom, float thickness, int top, int side)
        {
            var run = b - a;
            run.y = 0f;
            var dir = run.normalized;
            var right = Vector3.Cross(Vector3.up, dir) * (width / 2f);
            Vector3 a0 = a - right, a1 = a + right, b0 = b - right, b1 = b + right;
            Vector3 Low(Vector3 p) => thickness > 0f ? p + Vector3.down * thickness : new Vector3(p.x, bottom, p.z);
            Vector3 A0 = Low(a0), A1 = Low(a1), B0 = Low(b0), B1 = Low(b1);
            Vector2 Along(Vector3 p) => new Vector2(Vector3.Dot(p, dir), p.y);
            Face(top, a0, b0, b1, a1, Vector3.up, p => new Vector2(p.x, p.z));
            Face(side, A0, a0, b0, B0, -right, Along);
            Face(side, A1, a1, b1, B1, right, Along);
            if (Vector3.Distance(a0, A0) > 0.01f) Face(side, A0, a0, a1, A1, -dir, p => new Vector2(Vector3.Dot(p, right.normalized), p.y));
            if (Vector3.Distance(b0, B0) > 0.01f) Face(side, B0, b0, b1, B1, dir, p => new Vector2(Vector3.Dot(p, right.normalized), p.y));
            if (thickness > 0f) Face(side, A0, B0, B1, A1, Vector3.down, p => new Vector2(p.x, p.z));
        }

        /// A sphere (a balloon), world-space centre.
        public void AddSphere(Vector3 center, Vector3 radii, int submesh, int rings = 8, int segments = 12)
        {
            Vector3 P(int r, int s)
            {
                float t = Mathf.PI * r / rings, a = 2f * Mathf.PI * s / segments;
                return center + new Vector3(Mathf.Sin(t) * Mathf.Cos(a) * radii.x, Mathf.Cos(t) * radii.y, Mathf.Sin(t) * Mathf.Sin(a) * radii.z);
            }
            for (int r = 0; r < rings; r++)
                for (int s = 0; s < segments; s++)
                {
                    Vector3 a = P(r, s), b = P(r + 1, s), c = P(r + 1, s + 1), d = P(r, s + 1);
                    AddQuad(a, b, c, d, (a + b + c + d) / 4f - center, submesh);
                }
        }

        /// A free quad facing `outward`, textured by projection on its dominant axis.
        public void AddQuad(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, Vector3 outward, int submesh)
        {
            var n = new Vector3(Mathf.Abs(outward.x), Mathf.Abs(outward.y), Mathf.Abs(outward.z));
            Func<Vector3, Vector2> uv = n.y >= n.x && n.y >= n.z ? (p => new Vector2(p.x, p.z)) : n.x >= n.z ? (Func<Vector3, Vector2>)(p => new Vector2(p.z, p.y)) : (p => new Vector2(p.x, p.y));
            Face(submesh, p0, p1, p2, p3, outward, uv);
        }

        /// A quad wound so it faces `outward`.
        void Face(int submesh, Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, Vector3 outward, Func<Vector3, Vector2> uv)
        {
            var n = Vector3.Cross(p1 - p0, p2 - p0);
            if (Vector3.Dot(n, outward) < 0f) (p1, p3) = (p3, p1);
            Quad(submesh, Vector3.Cross(p1 - p0, p2 - p0).normalized, p0, p1, p2, p3, uv);
        }

        /// A floor (facing up) or ceiling (facing down) rectangle; `rotated` turns the texture 90 degrees.
        public void AddHorizontal(Rect area, float y, bool up, bool rotated, int submesh)
        {
            var p0 = new Vector3(area.xMin, y, area.yMin);
            var p1 = new Vector3(area.xMin, y, area.yMax);
            var p2 = new Vector3(area.xMax, y, area.yMax);
            var p3 = new Vector3(area.xMax, y, area.yMin);
            Func<Vector3, Vector2> uv = rotated ? (p => new Vector2(p.z, -p.x)) : (Func<Vector3, Vector2>)(p => new Vector2(p.x, p.z));
            if (up) Quad(submesh, Vector3.up, p0, p1, p2, p3, uv);
            else Quad(submesh, Vector3.down, p3, p2, p1, p0, uv);
        }

        /// A box of `size` centred at `center` in the space of `pose` (so it can be rotated, tilted or stretched).
        /// UVs come from the box's own coordinates in meters.
        public void AddOrientedBox(Matrix4x4 pose, Vector3 center, Vector3 size, int submesh)
        {
            Vector3 a = center - size / 2, b = center + size / 2;
            Vector3 P(float x, float y, float z) => pose.MultiplyPoint3x4(new Vector3(x, y, z));
            Vector3 N(Vector3 n) => pose.MultiplyVector(n).normalized;
            OrientedQuad(submesh, N(Vector3.right), P(b.x, a.y, a.z), P(b.x, b.y, a.z), P(b.x, b.y, b.z), P(b.x, a.y, b.z), new Vector2(a.z, a.y), new Vector2(b.z, b.y));
            OrientedQuad(submesh, N(Vector3.left), P(a.x, a.y, b.z), P(a.x, b.y, b.z), P(a.x, b.y, a.z), P(a.x, a.y, a.z), new Vector2(a.z, a.y), new Vector2(b.z, b.y));
            OrientedQuad(submesh, N(Vector3.forward), P(b.x, a.y, b.z), P(b.x, b.y, b.z), P(a.x, b.y, b.z), P(a.x, a.y, b.z), new Vector2(a.x, a.y), new Vector2(b.x, b.y));
            OrientedQuad(submesh, N(Vector3.back), P(a.x, a.y, a.z), P(a.x, b.y, a.z), P(b.x, b.y, a.z), P(b.x, a.y, a.z), new Vector2(a.x, a.y), new Vector2(b.x, b.y));
            OrientedQuad(submesh, N(Vector3.up), P(a.x, b.y, a.z), P(a.x, b.y, b.z), P(b.x, b.y, b.z), P(b.x, b.y, a.z), new Vector2(a.x, a.z), new Vector2(b.x, b.z));
            OrientedQuad(submesh, N(Vector3.down), P(b.x, a.y, a.z), P(b.x, a.y, b.z), P(a.x, a.y, b.z), P(a.x, a.y, a.z), new Vector2(a.x, a.z), new Vector2(b.x, b.z));
        }

        /// Appends a model's triangles at `pose`, submesh i of the model going into submesh `submeshMap[i]`.
        /// A model's arrays, read from the engine once. (Mesh.vertices etc. copy everything on every call, which made
        /// each chunk with dozens of troffers take ~300 ms to build.)
        sealed class MeshData { public Vector3[] V, N; public Vector2[] UV; public int[][] Tris; }
        static readonly Dictionary<Mesh, MeshData> meshData = new Dictionary<Mesh, MeshData>();

        static MeshData DataOf(Mesh mesh)
        {
            if (meshData.TryGetValue(mesh, out var d)) return d;
            d = new MeshData { V = mesh.vertices, N = mesh.normals, UV = mesh.uv, Tris = new int[mesh.subMeshCount][] };
            for (int s = 0; s < d.Tris.Length; s++) d.Tris[s] = mesh.GetTriangles(s);
            meshData[mesh] = d;
            return d;
        }

        public void AddMesh(Mesh mesh, Matrix4x4 pose, int[] submeshMap)
        {
            var d = DataOf(mesh);
            Vector3[] v = d.V, n = d.N;
            var uv = d.UV;
            int start = vertices.Count;
            for (int i = 0; i < v.Length; i++)
            {
                vertices.Add(pose.MultiplyPoint3x4(v[i]) - origin);
                normals.Add(i < n.Length ? pose.MultiplyVector(n[i]).normalized : Vector3.up);
                uvs.Add(i < uv.Length ? uv[i] : Vector2.zero);
            }
            for (int s = 0; s < d.Tris.Length; s++)
            {
                if (submeshMap[s] < 0) continue;   // this part left out
                var tris = Tris(submeshMap[s]);
                foreach (int t in d.Tris[s]) tris.Add(start + t);
            }
        }

        /// A cylinder (pipe, round pillar) along the local x axis of `pose`, centred at `center`.
        public void AddOrientedCylinder(Matrix4x4 pose, Vector3 center, float radius, float length, int submesh, int sides = 10, bool caps = true)
        {
            Vector3 P(float x, float a) => pose.MultiplyPoint3x4(center + new Vector3(x, Mathf.Cos(a) * radius, Mathf.Sin(a) * radius));
            Vector3 N(float a) => pose.MultiplyVector(new Vector3(0f, Mathf.Cos(a), Mathf.Sin(a))).normalized;
            float h = length / 2, circ = 2f * Mathf.PI * radius;
            var tris = Tris(submesh);
            for (int i = 0; i < sides; i++)
            {
                float a0 = i * 2f * Mathf.PI / sides, a1 = (i + 1) * 2f * Mathf.PI / sides;
                int start = vertices.Count;
                vertices.Add(P(-h, a0) - origin); vertices.Add(P(h, a0) - origin); vertices.Add(P(h, a1) - origin); vertices.Add(P(-h, a1) - origin);
                normals.Add(N(a0)); normals.Add(N(a0)); normals.Add(N(a1)); normals.Add(N(a1));
                float u0 = circ * i / sides, u1 = circ * (i + 1) / sides;
                uvs.Add(new Vector2(-h, u0)); uvs.Add(new Vector2(h, u0)); uvs.Add(new Vector2(h, u1)); uvs.Add(new Vector2(-h, u1));
                tris.Add(start); tris.Add(start + 2); tris.Add(start + 1);
                tris.Add(start); tris.Add(start + 3); tris.Add(start + 2);
                if (!caps) continue;
                foreach (float x in new[] { -h, h })
                {
                    int c = vertices.Count;
                    var n = pose.MultiplyVector(new Vector3(Mathf.Sign(x), 0f, 0f)).normalized;
                    vertices.Add(pose.MultiplyPoint3x4(center + new Vector3(x, 0f, 0f)) - origin); vertices.Add(P(x, a0) - origin); vertices.Add(P(x, a1) - origin);
                    normals.Add(n); normals.Add(n); normals.Add(n);
                    uvs.Add(Vector2.zero); uvs.Add(new Vector2(Mathf.Cos(a0), Mathf.Sin(a0)) * radius); uvs.Add(new Vector2(Mathf.Cos(a1), Mathf.Sin(a1)) * radius);
                    if (x > 0) { tris.Add(c); tris.Add(c + 1); tris.Add(c + 2); } else { tris.Add(c); tris.Add(c + 2); tris.Add(c + 1); }
                }
            }
        }

        /// An inflatable ring lying in the pose's local x/z plane. `alt`, when given, colours every other
        /// major segment so the classic lifebuoy stripes can be built from one call.
        public void AddTorus(Matrix4x4 pose, Vector3 center, float major, float minor, int submesh,
                             int majorSegments = 18, int minorSegments = 8, int alt = -1)
        {
            float tau = 2f * Mathf.PI;
            Vector3 P(int i, int j)
            {
                float a = tau * i / majorSegments, b = tau * j / minorSegments;
                float r = major + minor * Mathf.Cos(b);
                return pose.MultiplyPoint3x4(center + new Vector3(Mathf.Cos(a) * r, Mathf.Sin(b) * minor, Mathf.Sin(a) * r));
            }
            Vector3 N(int i, int j)
            {
                float a = tau * i / majorSegments, b = tau * j / minorSegments;
                return pose.MultiplyVector(new Vector3(Mathf.Cos(a) * Mathf.Cos(b), Mathf.Sin(b), Mathf.Sin(a) * Mathf.Cos(b))).normalized;
            }
            for (int i = 0; i < majorSegments; i++)
            for (int j = 0; j < minorSegments; j++)
                AddQuad(P(i, j), P(i + 1, j), P(i + 1, j + 1), P(i, j + 1),
                        N(i, j), alt >= 0 && (i & 1) == 0 ? alt : submesh);
        }

        public Mesh ToMesh(string name)
        {
            var mesh = new Mesh { name = name };
            if (vertices.Count > 65535) mesh.indexFormat = IndexFormat.UInt32;
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.subMeshCount = submeshes.Count;
            for (int i = 0; i < submeshes.Count; i++) mesh.SetTriangles(submeshes[i], i);
            mesh.RecalculateTangents();   // normal maps need tangents
            mesh.RecalculateBounds();
            return mesh;
        }

        List<int> Tris(int submesh)
        {
            while (submeshes.Count <= submesh) submeshes.Add(new List<int>());
            return submeshes[submesh];
        }

        void Quad(int submesh, Vector3 normal, Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, Func<Vector3, Vector2> uv)
        {
            int start = vertices.Count;
            Vertex(p0); Vertex(p1); Vertex(p2); Vertex(p3);
            var tris = Tris(submesh);
            tris.Add(start); tris.Add(start + 1); tris.Add(start + 2);
            tris.Add(start); tris.Add(start + 2); tris.Add(start + 3);

            void Vertex(Vector3 p)
            {
                vertices.Add(p - origin);
                normals.Add(normal);
                uvs.Add(uv(p));
            }
        }

        void OrientedQuad(int submesh, Vector3 normal, Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, Vector2 uvMin, Vector2 uvMax)
        {
            int start = vertices.Count;
            vertices.Add(p0 - origin); vertices.Add(p1 - origin); vertices.Add(p2 - origin); vertices.Add(p3 - origin);
            for (int i = 0; i < 4; i++) normals.Add(normal);
            uvs.Add(uvMin); uvs.Add(new Vector2(uvMin.x, uvMax.y)); uvs.Add(uvMax); uvs.Add(new Vector2(uvMax.x, uvMin.y));
            var tris = Tris(submesh);
            tris.Add(start); tris.Add(start + 1); tris.Add(start + 2);
            tris.Add(start); tris.Add(start + 2); tris.Add(start + 3);
        }
    }
}
