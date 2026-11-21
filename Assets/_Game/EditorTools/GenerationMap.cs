using System.IO;
using Backrooms.World;
using UnityEditor;
using UnityEngine;

namespace Backrooms.EditorTools
{
    /// Backrooms > Render Level 0 Map: a top-down floor plan of the generator's output (walls dark, floor tinted by
    /// ceiling height, props grey, exits green, the origin rooms outlined) saved as Screenshots/level0-map.png.
    public static class GenerationMap
    {
        [MenuItem("Backrooms/Render Level 0 Map")]
        public static void RenderMenu() => Render("0", 0, 0, 8, "level0-map");

        /// Renders `chunks` x `chunks` chunks with chunk (cx0, cz0) at the bottom-left. 4 px per metre.
        public static string Render(string levelId, int cx0, int cz0, int chunks, string file)
        {
            var catalog = AssetDatabase.LoadAssetAtPath<LevelCatalog>("Assets/_Game/Data/LevelCatalog.asset");
            var level = catalog.Find(levelId);
            uint seed = WorldHash.LevelSeed(WorldHash.DefaultWorldSeed, level.id);
            const int ppm = 4;
            float size = MazeLayout.ChunkSize;
            int px = Mathf.RoundToInt(chunks * size * ppm);
            var tex = new Texture2D(px, px, TextureFormat.RGB24, false);
            var pixels = new Color32[px * px];
            var origin = new Vector2(cx0 * size, cz0 * size);

            void Fill(Rect r, Color32 c)
            {
                int x0 = Mathf.Max(0, Mathf.FloorToInt((r.xMin - origin.x) * ppm)), x1 = Mathf.Min(px, Mathf.CeilToInt((r.xMax - origin.x) * ppm));
                int y0 = Mathf.Max(0, Mathf.FloorToInt((r.yMin - origin.y) * ppm)), y1 = Mathf.Min(px, Mathf.CeilToInt((r.yMax - origin.y) * ppm));
                for (int y = y0; y < y1; y++)
                for (int x = x0; x < x1; x++)
                    pixels[y * px + x] = c;
            }
            Rect Flat(Bounds b) => new Rect(b.min.x, b.min.z, b.size.x, b.size.z);

            DistrictLayout.ClearCache();
            for (int cx = cx0; cx < cx0 + chunks; cx++)
            for (int cz = cz0; cz < cz0 + chunks; cz++)
            {
                var L = ChunkStreamer.GenerateChunk(level, seed, cx, cz);
                foreach (var q in L.Quads)
                    if (q.FacingUp)   // floors: lighter = lower ceiling is unknown here, so tint by surface variant
                    {
                        byte v = (byte)(200 + (q.Surface.Variant * 17) % 40);
                        Fill(q.Area, new Color32(v, (byte)(v - 15), (byte)(v - 70), 255));
                    }
                foreach (var w in L.Water) Fill(w.Area, new Color32(90, 140, 200, 255));
                foreach (var b in L.Boxes)
                {
                    if (b.Box.min.y > 1.2f) continue;   // headers, hanging things
                    bool low = b.Box.max.y < 1.6f;
                    Fill(Flat(b.Box), b.Solid ? (low ? new Color32(120, 110, 90, 255) : new Color32(40, 35, 30, 255)) : new Color32(150, 140, 120, 255));
                }
                foreach (var p in L.Pits) Fill(Flat(p), new Color32(0, 0, 0, 255));
                foreach (var e in L.Exits) Fill(Flat(e.Trigger), new Color32(40, 200, 60, 255));
                foreach (var p in L.Props)
                {
                    Vector3 at = p.Pose.GetColumn(3);
                    Fill(new Rect(at.x - 0.3f, at.z - 0.3f, 0.6f, 0.6f), new Color32(110, 110, 120, 255));
                }
            }
            // District grid (64 m) as faint lines.
            for (int i = 0; i < px; i++)
                for (float g = Mathf.Ceil(origin.x / 64f) * 64f; (g - origin.x) * ppm < px; g += 64f)
                {
                    int x = Mathf.RoundToInt((g - origin.x) * ppm), y = Mathf.RoundToInt((g - origin.y) * ppm);
                    if (x >= 0 && x < px) pixels[i * px + x] = new Color32(200, 60, 60, 255);
                    if (y >= 0 && y < px) pixels[y * px + i] = new Color32(200, 60, 60, 255);
                }
            tex.SetPixels32(pixels);
            tex.Apply();
            string dir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Screenshots"));
            Directory.CreateDirectory(dir);
            string path = Path.Combine(dir, file + ".png");
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            Debug.Log($"[Map] saved {path}");
            return path;
        }
    }
}
