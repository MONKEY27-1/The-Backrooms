using System;
using System.Collections.Generic;
using UnityEngine;

namespace Backrooms.World
{
    /// Graphics revamp Stage D: grime. One merged decal mesh per chunk (HDRP/Decal on thin quads, so it blends into
    /// the surface under it): floor stains, dirt along the foot of walls, mould at the bottom, leaks from the top.
    /// The material and its 2x2 atlas are made by EditorTools/GrimeMaterials.cs.
    public static class GrimeDressing
    {
        public enum Cell { Stain, Mould, Leak, Dirt }
        const float Lift = 0.003f;   // off the surface, against z-fighting

        static Material material;
        public static Material Material => material != null ? material : material = Resources.Load<Material>("GrimeDecal");

        static readonly string[] Dirty = { "Carpet", "Wallpaper", "Plaster", "OldPlaster", "Concrete", "Hall", "Office", "Pipe", "Tunnel", "TarnishedBrick", "Manila", "Hotel", "Red", "DryRed" };

        /// Surfaces that get grime and colour variation (not water, frost, pool tile or trims).
        public static bool Takes(string materialName)
        {
            foreach (var p in Dirty) if (materialName.StartsWith(p, StringComparison.Ordinal)) return true;
            return false;
        }

        /// The chunk's grime in chunk-local space, or null if it has none.
        public static Mesh Build(ChunkLayout layout, Vector3 origin, Func<SurfaceRef, Material> resolve, uint seed)
        {
            var v = new List<Vector3>(); var n = new List<Vector3>(); var uv = new List<Vector2>(); var tris = new List<int>();
            var takes = new Dictionary<SurfaceRef, bool>();   // Material.name allocates, so ask once per surface
            bool Ok(SurfaceRef s)
            {
                if (takes.TryGetValue(s, out bool known)) return known;
                var m = resolve(s);
                return takes[s] = m != null && Takes(m.name);
            }

            int id = 0;
            float Roll(int salt) => WorldHash.Value(seed, id, salt, 0x6D1E);

            foreach (var q in layout.Quads)
            {
                id++;
                if (!q.FacingUp || q.Visual || !Ok(q.Surface)) continue;
                float expected = q.Area.width * q.Area.height * 0.012f;   // about one stain per 80 m²; denser cost frames
                int count = Mathf.Min(40, Mathf.FloorToInt(expected + Roll(0)));
                for (int i = 0; i < count; i++)
                {
                    float size = Mathf.Min(0.6f + 1.4f * Roll(10 + i * 4), Mathf.Min(q.Area.width, q.Area.height));
                    float x = Mathf.Lerp(q.Area.xMin + size / 2, q.Area.xMax - size / 2, Roll(11 + i * 4));
                    float z = Mathf.Lerp(q.Area.yMin + size / 2, q.Area.yMax - size / 2, Roll(12 + i * 4));
                    Add(Cell.Stain, new Vector3(x, q.Height, z), Vector3.up, Vector3.right, Vector3.forward, size, size, Roll(13 + i * 4) < 0.5f);
                }
            }

            foreach (var b in layout.Boxes)
            {
                id++;
                if (!b.Solid || b.Box.size.y < 1.2f) continue;
                Wall(b, BoxMeshBuilder.Faces.PosX, b.PosX, Vector3.right, 100);
                Wall(b, BoxMeshBuilder.Faces.NegX, b.NegX, Vector3.left, 200);
                Wall(b, BoxMeshBuilder.Faces.PosZ, b.PosZ, Vector3.forward, 300);
                Wall(b, BoxMeshBuilder.Faces.NegZ, b.NegZ, Vector3.back, 400);
            }

            if (v.Count == 0) return null;
            var mesh = new Mesh { name = "Grime" };
            mesh.SetVertices(v); mesh.SetNormals(n); mesh.SetUVs(0, uv); mesh.SetTriangles(tris, 0);
            mesh.RecalculateTangents();
            return mesh;

            void Wall(SurfaceBox b, BoxMeshBuilder.Faces face, SurfaceRef surface, Vector3 normal, int salt)
            {
                if ((b.Faces & face) == 0 || !Ok(surface)) return;
                var right = Vector3.Cross(Vector3.up, -normal);   // as seen looking at the wall
                var e = b.Box.extents;
                float half = Mathf.Abs(Vector3.Dot(right, e)) - 0.03f;   // clear of the bevelled corners
                if (half < 0.3f) return;
                var mid = b.Box.center + normal * Mathf.Abs(Vector3.Dot(normal, e));
                float bottom = b.Box.min.y, height = b.Box.size.y;
                void Place(Cell cell, float w, float h, float centreY, int s)
                {
                    w = Mathf.Min(w, half * 2);
                    float along = Mathf.Lerp(-half + w / 2, half - w / 2, Roll(s));
                    Add(cell, new Vector3(mid.x, centreY, mid.z) + right * along, normal, right, Vector3.up, w, h, Roll(s + 1) < 0.5f);
                }
                if (Roll(salt) < 0.16f) Place(Cell.Dirt, 1.5f + 2f * Roll(salt + 1), 0.45f, bottom + 0.225f, salt + 2);
                if (Roll(salt + 4) < 0.03f) Place(Cell.Mould, 0.9f, 0.9f, bottom + 0.45f, salt + 5);
                if (Roll(salt + 7) < 0.04f) { float h = Mathf.Min(1.4f, height); Place(Cell.Leak, 0.8f, h, bottom + height - h / 2, salt + 8); }
            }

            // Corners clockwise as seen from outside, like BoxMeshBuilder.
            void Add(Cell cell, Vector3 centre, Vector3 normal, Vector3 right, Vector3 up, float w, float h, bool flip)
            {
                int s = v.Count;
                var c = centre + normal * Lift - origin;
                Vector3 r = right * (w / 2), u = up * (h / 2);
                v.Add(c - r - u); v.Add(c - r + u); v.Add(c + r + u); v.Add(c + r - u);
                for (int i = 0; i < 4; i++) n.Add(normal);
                float u0 = ((int)cell & 1) * 0.5f + 0.004f, u1 = u0 + 0.492f, v0 = ((int)cell >> 1) * 0.5f + 0.004f, v1 = v0 + 0.492f;
                if (flip) (u0, u1) = (u1, u0);
                uv.Add(new Vector2(u0, v0)); uv.Add(new Vector2(u0, v1)); uv.Add(new Vector2(u1, v1)); uv.Add(new Vector2(u1, v0));
                tris.Add(s); tris.Add(s + 1); tris.Add(s + 2); tris.Add(s); tris.Add(s + 2); tris.Add(s + 3);
            }
        }
    }
}
