using UnityEngine;

namespace Backrooms.EditorTools
{
    /// Texture generation. Level 0 surfaces combine procedural patterns with CC0 photo textures (ambientCG):
    /// the photo gives real paper, fibre and tile detail; the pattern and tint give the Backrooms look.
    public static class ProceduralTextures
    {
        public const int PatternCount = 6;

        /// Wallpaper, 1024 px = 1 m. Patterns: 0 stripes, 1 chevrons, 2 diamonds, 3 dots, 4 plain, 5 damask.
        /// `paper` adds real paper texture; `stains` (optional) adds water damage.
        public static Texture2D Wallpaper(int pattern, Color tint, Texture2D paper, Texture2D stains)
        {
            const int size = 1024;
            var paperPx = paper.GetPixels32();
            var stainPx = stains != null ? stains.GetPixels32() : null;
            float mean = MeanLuminance(paperPx);
            return Make(size, (x, y) =>
            {
                float shade = 1f;
                switch (pattern)
                {
                    case 0:   // fine pinstripes with alternating bands
                        if (x % 64 < 5) shade -= 0.07f;
                        if ((x / 128) % 2 == 0) shade -= 0.025f;
                        break;
                    case 1:   // chevrons, like the original photo
                    {
                        float u = (x % 96) / 96f, v = (y % 96) / 96f;
                        float f = Mathf.Repeat(v + Mathf.Abs(u - 0.5f), 1f);
                        if (Mathf.Abs(f - 0.5f) < 0.055f) shade -= 0.08f;
                        break;
                    }
                    case 2:   // diamond outlines between stripes
                    {
                        float u = (x % 64) / 64f, v = (y % 64) / 64f;
                        if (u < 0.04f) shade -= 0.07f;
                        float d = Mathf.Abs(u - 0.5f) + Mathf.Abs(v - 0.5f);
                        if (d < 0.2f && d > 0.14f) shade -= 0.07f;
                        break;
                    }
                    case 3:   // small dots on offset rows
                    {
                        int row = y / 48;
                        float cx = (x + (row % 2) * 24) % 48 - 24, cy = y % 48 - 24;
                        if (cx * cx + cy * cy < 30f) shade -= 0.09f;
                        break;
                    }
                    case 4:   // plain, with a faint vertical grain
                        shade -= 0.015f * Mathf.Sin(x * 0.7f);
                        break;
                    case 5:   // a simple damask motif on offset rows
                    {
                        int row = y / 128;
                        float u = ((x + (row % 2) * 64) % 128) / 128f, v = (y % 128) / 128f;
                        float f = Mathf.Sin(u * Mathf.PI) * Mathf.Sin(v * Mathf.PI) + 0.35f * Mathf.Cos(u * 4f * Mathf.PI) * Mathf.Sin(v * 2f * Mathf.PI);
                        if (f > 0.55f && f < 0.68f) shade -= 0.07f;
                        break;
                    }
                }
                shade *= Luminance(Sample(paperPx, paper.width, paper.height, x, y, size)) / mean;
                if (stainPx != null)
                    shade *= Mathf.Lerp(1f, Luminance(Sample(stainPx, stains.width, stains.height, x, y, size)), 0.55f);
                return tint * shade;
            });
        }

        /// The three maps of a hung wallpaper: colour, normal (tangent space, OpenGL) and HDRP mask (G AO, A smoothness).
        public sealed class WallpaperMaps { public Texture2D Albedo, Normal, Mask; }

        /// Hung wallpaper, 2048 px = 2 m (use tiling 0.5): four 0.5 m strips with seams, each from a slightly different
        /// roll (tone) and hung slightly off (pattern drop). Printed pattern with soft edges and uneven ink, real paper
        /// grain from the photo, a few bubbles. Tiles seamlessly. Damage is a separate, larger detail map (WallDamage).
        /// `worn` = heavier damage. `strips` = false for plain painted walls (no seams).
        public static WallpaperMaps WallpaperSet(int pattern, Color tint, Texture2D paper, bool worn, int seed, bool strips = true)
        {
            const int S = 2048, Strip = 512;
            var paperPx = paper.GetPixels32();
            float mean = MeanLuminance(paperPx);
            var albedo = new Color32[S * S];
            var height = new float[S * S];
            var mask = new Color32[S * S];
            var ink = Color.Lerp(tint * 0.89f, new Color(0.55f, 0.47f, 0.25f), 0.14f);

            var rng = new System.Random(seed);
            // Bubbles: soft lumps where the paste failed.
            int bubbles = worn ? 6 : 2;
            var bub = new (float x, float y, float r)[bubbles];
            for (int i = 0; i < bubbles; i++) bub[i] = ((float)rng.NextDouble() * S, (float)rng.NextDouble() * S, 14f + (float)rng.NextDouble() * 40f);

            for (int y = 0; y < S; y++)
            for (int x = 0; x < S; x++)
            {
                int s = strips ? x / Strip : 0;
                // Each strip hangs a few mm off its neighbour, so the pattern doesn't quite line up across the seam.
                int drop = strips ? (int)(Hash01(s, 0, seed ^ 0x51) * 9f) - 4 : 0;
                float px = x, py = y + drop;
                float cover = strips || pattern != 4 ? Ink(pattern, px, py) : 0f;
                cover *= 0.78f + 0.22f * Fbm(x / (float)S, y / (float)S, 64, 2, seed ^ 0x1A);   // uneven print

                // Paper grain from the photo (sampled twice per tile: the photo covers 1 m).
                var pp = paperPx[((y % 1024) * paper.height / 1024) * paper.width + (x % 1024) * paper.width / 1024];
                float grain = Luminance(pp) / mean - 1f;
                float shade = 1f + grain * 0.9f;

                // Roll-to-roll tone. (Stains, drips and yellowing are in the large detail map: see WallDamage.)
                float roll = strips ? 1f + (Hash01(s, 1, seed ^ 0x77) - 0.5f) * 0.045f : 1f;
                var c = Color.Lerp(tint, ink, cover) * shade * roll;

                // Seams: a fine dark gap, a lifted edge catching light, and dirt that collects along it.
                float seamDark = 0f, groove = 0f;
                if (strips)
                {
                    float d = Mathf.Min(x % Strip, Strip - x % Strip);
                    bool open = Fbm(x / (float)S, y / (float)S, 16, 2, seed ^ 0x4D + s) > (worn ? 0.6f : 0.7f);   // where it's lifting
                    float gap = open ? 2.6f : 1.2f;
                    if (d < gap) { seamDark = open ? 0.16f : 0.07f; groove = (1f - d / gap) * (open ? 1f : 0.5f); }
                    else if (d < gap + 3f && x % Strip < Strip / 2) c *= 1.015f;   // the lifted edge
                    c *= 1f - 0.02f * Mathf.Exp(-d / 10f);   // seams should be found up close, not seen from across the room
                    c *= 1f - seamDark;
                }

                float bump = 0f;
                for (int i = 0; i < bubbles; i++)
                {
                    float dx = Wrap(x - bub[i].x, S), dy = Wrap(y - bub[i].y, S);
                    float r2 = (dx * dx + dy * dy) / (bub[i].r * bub[i].r);
                    if (r2 < 1f) bump += (1f - r2) * (1f - r2);
                }

                int k = y * S + x;
                albedo[k] = new Color(c.r, c.g, c.b, 1f);
                height[k] = grain * 0.5f + cover * 0.35f - groove * 1.4f + bump * 0.45f;
                float smooth = Mathf.Lerp(0.5f, 0.75f, cover) * (1f - groove * 0.6f);
                // B = 1: the detail map (WallDamage) applies everywhere.
                mask[k] = new Color32(0, (byte)(255 * (1f - groove * 0.55f)), 255, (byte)(255 * Mathf.Clamp01(smooth)));
            }

            return new WallpaperMaps
            {
                Albedo = FromPixels(S, albedo, false),
                Normal = FromPixels(S, NormalsFrom(height, S, 2.2f), true),
                Mask = FromPixels(S, mask, true),
            };
        }

        /// Water damage for a wall, as an HDRP detail map (linear; R albedo with 0.5 = no change, G/A normal, B smoothness).
        /// It tiles at a much larger, odd size than the wallpaper (about 7 m), so stains don't repeat with the pattern:
        /// slow yellowing, water stains with brown tide lines, drips, and faint patchy repairs/wear. `worn` = more and darker.
        public static Texture2D WallDamage(bool worn, int seed)
        {
            const int S = 2048;   // over ~7 m: about 3 px per cm, plenty for soft stains
            var px = new Color32[S * S];
            var rng = new System.Random(seed);
            int drips = worn ? 8 : 3;
            var drip = new (float x, float top, float len, float w, float k)[drips];
            for (int i = 0; i < drips; i++)
                drip[i] = ((float)rng.NextDouble() * S, (float)rng.NextDouble() * S, 90f + (float)rng.NextDouble() * 300f,
                           4f + (float)rng.NextDouble() * 8f, 0.12f + (float)rng.NextDouble() * (worn ? 0.22f : 0.12f));
            int patches = worn ? 7 : 3;
            var patch = new (float x, float y, float sx, float sy, float rot, float k, float smooth)[patches];
            for (int i = 0; i < patches; i++)
            {
                float sx = 40f + (float)rng.NextDouble() * 180f, sy = 30f + (float)rng.NextDouble() * 140f;
                float k = ((float)rng.NextDouble() - 0.5f) * (worn ? 0.18f : 0.1f);   // lighter repairs or darker worn patches
                patch[i] = ((float)rng.NextDouble() * S, (float)rng.NextDouble() * S, sx, sy,
                            (float)rng.NextDouble() * Mathf.PI, k, -0.05f + (float)rng.NextDouble() * 0.08f);
            }
            for (int y = 0; y < S; y++)
            for (int x = 0; x < S; x++)
            {
                float u = x / (float)S, v = y / (float)S;
                float age = Fbm(u, v, 3, 4, seed ^ 0x2B);
                float dark = Edge(0.43f, 0.70f, age) * 0.08f;   // broad, low-contrast yellowing

                float b = Fbm(u, v, 5, 5, seed ^ 0x3C);
                float inside = Edge(0.65f, 0.82f, b);            // sparse, diffuse stains, not a hard threshold
                float rim = Mathf.Exp(-Mathf.Pow((b - 0.66f) * 40f, 2f)) * (worn ? 0.09f : 0.05f);   // a faint tide line at the edge
                dark += inside * (worn ? 0.2f : 0.13f) + rim;

                float dripDark = 0f;
                for (int i = 0; i < drips; i++)
                {
                    float dx = Wrap(x - drip[i].x, S), dy = Wrap(drip[i].top - y, S);
                    if (dy < 0f || dy > drip[i].len) continue;
                    float across = Mathf.Abs(dx - Mathf.Sin(y * 0.05f + i) * 1.2f) / drip[i].w;
                    float along = dy / drip[i].len;
                    if (across < 1f) dripDark = Mathf.Max(dripDark, drip[i].k * (1f - across * across) * Mathf.Min(1f, along * 6f) * (1f - along));
                }
                dark += dripDark * 0.5f;

                float patchDark = 0f, patchSmooth = 0f;
                for (int i = 0; i < patches; i++)
                {
                    float dx = Wrap(x - patch[i].x, S), dy = Wrap(y - patch[i].y, S);
                    float cs = Mathf.Cos(patch[i].rot), sn = Mathf.Sin(patch[i].rot);
                    float rx = (dx * cs + dy * sn) / patch[i].sx, ry = (-dx * sn + dy * cs) / patch[i].sy;
                    float d = rx * rx + ry * ry;
                    if (d >= 1.2f) continue;
                    float w = 1f - Edge(0.55f, 1.15f, d);
                    patchDark += patch[i].k * w;
                    patchSmooth += patch[i].smooth * w;
                }
                dark += patchDark;

                float r = 0.5f - Mathf.Clamp01(dark) * 0.5f;    // HDRP detail albedo remaps around neutral grey
                float smooth = Mathf.Clamp01(0.5f - inside * 0.12f + patchSmooth);   // stained/patched paper varies in sheen
                px[y * S + x] = new Color(r, 0.5f, smooth, 0.5f);
            }
            return FromPixels(S, px, true);
        }

        /// How much ink a pattern lays at (x, y), 0..1, with soft (anti-aliased) edges. 1024 px = 1 m.
        static float Ink(int pattern, float x, float y)
        {
            float Line(float dist, float half) => 1f - Edge(half - 0.8f, half + 0.8f, dist);
            switch (pattern)
            {
                case 0:   // fine pinstripes with alternating bands
                {
                    float m = Mod(x, 64f);
                    return Mathf.Max(Line(Mathf.Min(m, 64f - m), 2.5f) * 0.55f, Mod(x, 256f) < 128f ? 0.07f : 0f);
                }
                case 1:   // chevrons
                {
                    float u = Mod(x, 96f) / 96f, v = Mod(y, 96f) / 96f;
                    float f = Mathf.Repeat(v + Mathf.Abs(u - 0.5f), 1f);
                    return Line(Mathf.Abs(f - 0.5f) * 96f * 0.707f, 3.2f);
                }
                case 2:   // diamond outlines between stripes
                {
                    float u = Mod(x, 64f), v = Mod(y, 64f);
                    float stripe = Line(Mathf.Min(u, 64f - u), 1.6f);
                    float d = (Mathf.Abs(u - 32f) + Mathf.Abs(v - 32f)) * 0.707f;
                    return Mathf.Max(stripe, Line(Mathf.Abs(d - midDiamond), 1.8f));
                }
                case 3:   // small dots on offset rows
                {
                    float row = Mathf.Floor(y / 48f);
                    float cx = Mod(x + Mod(row, 2f) * 24f, 48f) - 24f, cy = Mod(y, 48f) - 24f;
                    return Line(Mathf.Sqrt(cx * cx + cy * cy), 5.5f);
                }
                case 4:   // plain, with a faint vertical grain
                    return 0.08f * (0.5f + 0.5f * Mathf.Sin(x * 0.7f));
                default:  // a simple damask motif on offset rows
                {
                    float row = Mathf.Floor(y / 128f);
                    float u = Mod(x + Mod(row, 2f) * 64f, 128f) / 128f, v = Mod(y, 128f) / 128f;
                    float f = Mathf.Sin(u * Mathf.PI) * Mathf.Sin(v * Mathf.PI) + 0.35f * Mathf.Cos(u * 4f * Mathf.PI) * Mathf.Sin(v * 2f * Mathf.PI);
                    return Edge(0f, 1f, 1f - Mathf.Abs(f - 0.615f) / 0.075f);
                }
            }
        }

        const float midDiamond = 12f;

        /// 0 below e0, 1 above e1, smooth between (GLSL smoothstep; Unity's Mathf.SmoothStep is a different thing).
        static float Edge(float e0, float e1, float x) { float t = Mathf.Clamp01((x - e0) / (e1 - e0)); return t * t * (3f - 2f * t); }

        static float Mod(float a, float m) => a - m * Mathf.Floor(a / m);
        static float Wrap(float d, int size) { d = Mod(d + size / 2f, size) - size / 2f; return d; }

        static float Hash01(int x, int y, int seed)
        {
            unchecked
            {
                uint h = (uint)x * 374761393u + (uint)y * 668265263u + (uint)seed * 2246822519u;
                h = (h ^ (h >> 13)) * 1274126177u;
                return (h ^ (h >> 16)) / 4294967295f;
            }
        }

        /// Tileable value-noise fBm on the unit square: `cells` lattice cells across, `octaves` doubling each time. 0..1.
        static float Fbm(float u, float v, int cells, int octaves, int seed)
        {
            float sum = 0f, amp = 0.5f, norm = 0f;
            for (int o = 0; o < octaves; o++, cells *= 2, amp *= 0.5f)
            {
                float fx = u * cells, fy = v * cells;
                int ix = Mathf.FloorToInt(fx), iy = Mathf.FloorToInt(fy);
                float tx = fx - ix, ty = fy - iy;
                tx = tx * tx * (3f - 2f * tx); ty = ty * ty * (3f - 2f * ty);
                int x0 = ((ix % cells) + cells) % cells, y0 = ((iy % cells) + cells) % cells, x1 = (x0 + 1) % cells, y1 = (y0 + 1) % cells;
                float a = Mathf.Lerp(Hash01(x0, y0, seed + o), Hash01(x1, y0, seed + o), tx);
                float b = Mathf.Lerp(Hash01(x0, y1, seed + o), Hash01(x1, y1, seed + o), tx);
                sum += Mathf.Lerp(a, b, ty) * amp;
                norm += amp;
            }
            return sum / norm;
        }

        /// Tangent-space normals (OpenGL, +y up) from a tiling height field.
        static Color32[] NormalsFrom(float[] h, int size, float strength)
        {
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float l = h[y * size + (x + size - 1) % size], r = h[y * size + (x + 1) % size];
                float d = h[((y + size - 1) % size) * size + x], u = h[((y + 1) % size) * size + x];
                var n = new Vector3((l - r) * strength, (d - u) * strength, 1f).normalized;
                px[y * size + x] = new Color(n.x * 0.5f + 0.5f, n.y * 0.5f + 0.5f, n.z * 0.5f + 0.5f, 1f);
            }
            return px;
        }

        static Texture2D FromPixels(int size, Color32[] px, bool linear)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false, linear);
            tex.SetPixels32(px);
            tex.Apply();
            return tex;
        }

        /// A photo re-coloured to `target`, keeping its detail. `contrast` scales the detail.
        public static Texture2D Tint(Texture2D photo, Color target, float contrast, Texture2D stains = null, float stainAmount = 0f)
        {
            var px = photo.GetPixels32();
            var stainPx = stains != null ? stains.GetPixels32() : null;
            float mean = MeanLuminance(px);
            int w = photo.width, h = photo.height;
            return Make(w, h, (x, y) =>
            {
                float l = Luminance(px[y * w + x]);
                float shade = Mathf.Max(0.05f, 1f + (l - mean) * contrast * 2f);
                if (stainPx != null)
                    shade *= Mathf.Lerp(1f, Luminance(stainPx[(y * stains.height / h) * stains.width + x * stains.width / w]), stainAmount);
                return target * shade;
            });
        }

        /// A seamless tangent-space normal map for the Poolrooms' broad swell. Twelve whole-cycle waves plus a
        /// periodic domain warp break up the old four-wave grid; at a 15 m tile the repeat is no longer readable.
        public static Texture2D WaterRipples()
        {
            const int size = 1024;
            float tau = Mathf.PI * 2f;
            var waves = new (int x, int y, float amp, float phase)[]
            {
                (1, 0, 0.52f, 0.3f), (0, 1, 0.48f, 1.7f), (1, 1, 0.42f, 2.9f), (2, -1, 0.34f, 0.9f),
                (-1, 2, 0.30f, 2.2f), (2, 2, 0.24f, 4.1f), (3, -2, 0.21f, 1.2f), (-2, 3, 0.18f, 3.3f),
                (4, 1, 0.16f, 5.0f), (1, -4, 0.15f, 0.6f), (3, 4, 0.12f, 2.6f), (5, -3, 0.10f, 4.4f),
            };
            var height = new float[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float u0 = (float)x / size, v0 = (float)y / size;
                float a = tau * (2f * u0 + v0) + 0.4f, b = tau * (-u0 + 3f * v0) + 2.1f;
                float u = Mathf.Repeat(u0 + 0.022f * Mathf.Sin(a) + 0.014f * Mathf.Sin(b), 1f);
                float v = Mathf.Repeat(v0 + 0.022f * Mathf.Cos(a + 1.1f) + 0.014f * Mathf.Cos(b + 0.7f), 1f);
                float h = 0f;
                foreach (var w in waves) h += w.amp * Mathf.Sin(tau * (w.x * u + w.y * v) + w.phase);
                height[y * size + x] = h;
            }
            return FromPixels(size, NormalsFrom(height, size, 2.35f), true);
        }

        /// Fine ripples on their own UV transform. Two independently scrolling layers stop the surface from
        /// reading as one sliding texture.
        public static Texture2D WaterRippleDetail()
        {
            const int size = 512;
            float tau = Mathf.PI * 2f;
            var waves = new (int x, int y, float amp, float phase)[]
            {
                (7, 3, 0.34f, 0.2f), (-5, 6, 0.31f, 2.4f), (8, -7, 0.27f, 4.2f), (11, 5, 0.23f, 1.0f),
                (-9, 11, 0.20f, 3.1f), (13, -8, 0.16f, 5.3f), (15, 12, 0.13f, 0.8f), (-17, 9, 0.10f, 2.7f),
            };
            var height = new float[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float u0 = (float)x / size, v0 = (float)y / size;
                float a = tau * (3f * u0 + 2f * v0) + 1.1f, b = tau * (-2f * u0 + 3f * v0) + 3.7f;
                float u = Mathf.Repeat(u0 + 0.012f * Mathf.Sin(a) + 0.009f * Mathf.Sin(b), 1f);
                float v = Mathf.Repeat(v0 + 0.012f * Mathf.Cos(a + 0.9f) + 0.009f * Mathf.Cos(b + 1.4f), 1f);
                float h = 0f;
                foreach (var w in waves) h += w.amp * Mathf.Sin(tau * (w.x * u + w.y * v) + w.phase);
                height[y * size + x] = h;
            }
            // HDRP's detail normal comes from _DetailMap.ag (R/B drive albedo/smoothness and are neutral at 0.5),
            // not from a separate _DetailNormalMap, which the Lit shader does not even declare.
            var packed = new Color32[height.Length];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                int xm = (x + size - 1) % size, xp = (x + 1) % size;
                int ym = (y + size - 1) % size, yp = (y + 1) % size;
                float dx = (height[y * size + xm] - height[y * size + xp]) * 1.55f;
                float dy = (height[ym * size + x] - height[yp * size + x]) * 1.55f;
                float inv = 1f / Mathf.Sqrt(dx * dx + dy * dy + 1f);
                packed[y * size + x] = new Color32(128, (byte)(127.5f + dx * inv * 127.5f), 128, (byte)(127.5f + dy * inv * 127.5f));
            }
            return FromPixels(size, packed, true);
        }

        /// Branching caustics for the pool floor: bright where interfering waves cancel. RGB and alpha both
        /// carry the pattern so the same texture can drive colour, opacity and emission on an overlay quad.
        public static Texture2D WaterCaustics()
        {
            const int size = 512;
            float tau = Mathf.PI * 2f;
            var waves = new (int x, int y, float amp, float phase)[]
            {
                (2, 0, 0.30f, 0f), (0, 2, 0.28f, 1.3f), (3, 2, 0.24f, 2.6f), (-2, 3, 0.22f, 0.7f),
                (4, -3, 0.18f, 3.9f), (5, 4, 0.15f, 1.8f), (-6, 5, 0.12f, 5.1f),
            };
            var height = new float[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float u0 = (float)x / size, v0 = (float)y / size;
                float a = tau * (2f * u0 - v0) + 0.9f, b = tau * (u0 + 2f * v0) + 2.8f;
                float u = Mathf.Repeat(u0 + 0.018f * Mathf.Sin(a) + 0.013f * Mathf.Sin(b), 1f);
                float v = Mathf.Repeat(v0 + 0.018f * Mathf.Cos(a + 1.2f) + 0.013f * Mathf.Cos(b + 0.6f), 1f);
                float h = 0f;
                foreach (var w in waves) h += w.amp * Mathf.Cos(tau * (w.x * u + w.y * v) + w.phase);
                height[y * size + x] = h;
            }
            var px = new Color32[size * size];
            for (int i = 0; i < px.Length; i++)
            {
                float c = Mathf.Clamp01(Mathf.Pow(Mathf.Exp(-(height[i] * height[i]) / 0.0512f), 1.35f));
                px[i] = new Color32((byte)(c * 210f), (byte)(c * 245f), (byte)(c * 255f), (byte)(c * 235f));
            }
            return FromPixels(size, px, true);
        }

        /// Static for the TV screen: random grey speckle, sampled point-filtered and scrolled at runtime.
        public static Texture2D Noise()
        {
            var rng = new System.Random(9);
            return Make(256, (x, y) =>
            {
                float v = (float)rng.NextDouble();
                return new Color(v, v, v);
            });
        }

        public static Texture2D Load(string path)
        {
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            tex.LoadImage(System.IO.File.ReadAllBytes(path));
            return tex;
        }

        static Color32 Sample(Color32[] px, int w, int h, int x, int y, int size) =>
            px[(y * h / size) * w + (x * w / size)];

        static float Luminance(Color32 c) => (0.299f * c.r + 0.587f * c.g + 0.114f * c.b) / 255f;

        static float MeanLuminance(Color32[] px)
        {
            double sum = 0;
            for (int i = 0; i < px.Length; i += 7) sum += Luminance(px[i]);
            return Mathf.Max(0.01f, (float)(sum / ((px.Length + 6) / 7)));
        }

        static Texture2D Make(int size, System.Func<int, int, Color> pixel) => Make(size, size, pixel);

        static Texture2D Make(int w, int h, System.Func<int, int, Color> pixel)
        {
            var pixels = new Color32[w * h];
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                pixels[y * w + x] = pixel(x, y);
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.SetPixels32(pixels);
            tex.Apply();
            return tex;
        }
    }
}
