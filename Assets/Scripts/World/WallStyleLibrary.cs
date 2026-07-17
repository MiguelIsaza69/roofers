using System.Collections.Generic;
using UnityEngine;

namespace RoofingSimulator.World
{
    /// <summary>
    /// Procedurally builds matte "clay" wall materials with no imported assets: a softly
    /// mottled albedo plus a generated normal map (and baked groove shading) so each surface
    /// reads as 3D without any extra geometry. Ten named styles; the gallery shows them all
    /// and a level can later pick one at random via <see cref="GetRandom"/>.
    ///
    /// Textures are generated once per style and cached, then the material is shared, so the
    /// cost is a one-off at first use and tiny at runtime.
    /// </summary>
    public static class WallStyleLibrary
    {
        public const int Count = 9;
        private const int Res = 256;

        private static readonly Dictionary<int, Material> cache = new Dictionary<int, Material>();

        public static string NameOf(int style) => Wrap(style) switch
        {
            0 => "Estuco",
            1 => "Ladrillo",
            2 => "Tablon horizontal",
            3 => "Tabla y liston",
            4 => "Bloque de concreto",
            5 => "Piedra",
            6 => "Adobe",
            7 => "Tejuela de madera",
            _ => "Panel de vinilo"
        };

        public static Material Get(int style)
        {
            style = Wrap(style);
            if (cache.TryGetValue(style, out var m) && m != null) return m;
            m = Build(style);
            cache[style] = m;
            return m;
        }

        /// <summary>Pick a random wall style (use the level seed by calling Random.InitState first).</summary>
        public static Material GetRandom(out int style)
        {
            style = Random.Range(0, Count);
            return Get(style);
        }

        private static int Wrap(int s) => ((s % Count) + Count) % Count;

        // ----- Style dispatch -----

        private static Material Build(int style)
        {
            var albedo = new Color[Res * Res];
            var height = new float[Res * Res];
            float smooth;

            switch (style)
            {
                case 0: Stucco(albedo, height); smooth = 0.07f; break;
                case 1: Brick(albedo, height); smooth = 0.05f; break;
                case 2: Clapboard(albedo, height); smooth = 0.12f; break;
                case 3: BoardBatten(albedo, height); smooth = 0.10f; break;
                case 4: CinderBlock(albedo, height); smooth = 0.05f; break;
                case 5: Fieldstone(albedo, height); smooth = 0.06f; break;
                case 6: Adobe(albedo, height); smooth = 0.05f; break;
                case 7: WoodShingle(albedo, height); smooth = 0.09f; break;
                case 8: Vinyl(albedo, height); smooth = 0.28f; break;
                default: Stucco(albedo, height); smooth = 0.07f; break;
            }

            return Compose(albedo, height, smooth);
        }

        // ----- Patterns (fill albedo + height in [0,1]) -----

        private static void Stucco(Color[] a, float[] h)
        {
            Color baseC = new Color(0.85f, 0.80f, 0.69f);
            for (int y = 0; y < Res; y++)
                for (int x = 0; x < Res; x++)
                {
                    int i = y * Res + x;
                    float bump = Mathf.PerlinNoise(x * 0.18f, y * 0.18f) * 0.6f
                               + Mathf.PerlinNoise(x * 0.45f + 30f, y * 0.45f + 30f) * 0.4f;
                    h[i] = 0.45f + bump * 0.25f;
                    float v = (Mathf.PerlinNoise(x * 0.05f + 10f, y * 0.05f + 10f) - 0.5f) * 0.12f;
                    a[i] = Mul(baseC, 1f + v);
                }
        }

        private static void Brick(Color[] a, float[] h)
        {
            Color mortar = new Color(0.80f, 0.78f, 0.73f);
            Color brick = new Color(0.62f, 0.27f, 0.21f);
            int rh = Res / 8;          // 8 courses
            int bw = Res / 4;          // 4 bricks across
            int mort = Mathf.RoundToInt(Res * 0.03f);
            for (int y = 0; y < Res; y++)
                for (int x = 0; x < Res; x++)
                {
                    int i = y * Res + x;
                    Warp(x, y, 6f, 0.05f, out int wx, out int wy);
                    int row = wy / rh, yin = wy % rh;
                    int offset = (row % 2 == 0) ? 0 : bw / 2;
                    int xx = (wx + offset) % Res, xin = xx % bw;
                    if (yin < mort || xin < mort)
                    {
                        h[i] = 0.25f + Mathf.PerlinNoise(x * 0.3f, y * 0.3f) * 0.05f;
                        a[i] = Mul(mortar, 0.95f + Mathf.PerlinNoise(x * 0.2f, y * 0.2f) * 0.1f);
                    }
                    else
                    {
                        int bid = row * 1000 + xx / bw;
                        float j = (Hash(bid, 7) - 0.5f) * 0.18f;
                        float edge = Mathf.Min(Mathf.Min(yin - mort, rh - 1 - yin), Mathf.Min(xin - mort, bw - 1 - xin));
                        float ef = Mathf.Clamp01(edge / 5f);
                        h[i] = 0.6f + 0.2f * ef + Mathf.PerlinNoise(x * 0.5f, y * 0.5f) * 0.05f;
                        a[i] = Mul(brick, 1f + j);
                    }
                }
        }

        private static void Clapboard(Color[] a, float[] h)
        {
            Color paint = new Color(0.56f, 0.68f, 0.78f);
            int ph = Res / 8, groove = Mathf.RoundToInt(Res * 0.014f);
            for (int y = 0; y < Res; y++)
                for (int x = 0; x < Res; x++)
                {
                    int i = y * Res + x;
                    Warp(x, y, 4f, 0.05f, out int wx, out int wy);
                    int p = wy / ph, yin = wy % ph;
                    float grain = (Mathf.PerlinNoise(x * 0.05f, p * 3.3f) - 0.5f) * 0.06f;
                    if (yin < groove) { h[i] = 0.2f; a[i] = Mul(paint, 0.7f); }
                    else
                    {
                        float t = (yin - groove) / (float)(ph - groove);
                        h[i] = 0.45f + t * 0.4f;            // proud toward the top of each lap
                        a[i] = Mul(paint, 1f + grain + (t - 0.5f) * 0.05f);
                    }
                }
        }

        private static void BoardBatten(Color[] a, float[] h)
        {
            Color board = new Color(0.55f, 0.19f, 0.16f);
            int spacing = Res / 6, batW = Mathf.RoundToInt(Res * 0.06f);
            for (int y = 0; y < Res; y++)
                for (int x = 0; x < Res; x++)
                {
                    int i = y * Res + x;
                    Warp(x, y, 4f, 0.05f, out int wx, out int wy);
                    int xin = wx % spacing; bool bat = xin < batW;
                    float grain = (Mathf.PerlinNoise(x * 3.3f, y * 0.05f) - 0.5f) * 0.06f;
                    if (bat)
                    {
                        float e = Mathf.Clamp01(Mathf.Min(xin, batW - 1 - xin) / 3f);
                        h[i] = 0.55f + 0.3f * e;
                        a[i] = Mul(board, 1.04f + grain);
                    }
                    else
                    {
                        h[i] = 0.4f + Mathf.PerlinNoise(x * 0.2f, y * 0.2f) * 0.05f;
                        a[i] = Mul(board, 0.96f + grain);
                    }
                }
        }

        private static void CinderBlock(Color[] a, float[] h)
        {
            Color blk = new Color(0.72f, 0.72f, 0.69f), mortar = new Color(0.60f, 0.60f, 0.58f);
            int bw = Res / 3, bh = Res / 6, mort = Mathf.RoundToInt(Res * 0.025f);
            for (int y = 0; y < Res; y++)
                for (int x = 0; x < Res; x++)
                {
                    int i = y * Res + x;
                    Warp(x, y, 5f, 0.05f, out int wx, out int wy);
                    int row = wy / bh, yin = wy % bh;
                    int col = wx / bw, xin = wx % bw;
                    if (yin < mort || xin < mort) { h[i] = 0.3f; a[i] = Mul(mortar, 1f); }
                    else
                    {
                        float j = (Hash(row, col) - 0.5f) * 0.08f;
                        float spk = Mathf.PerlinNoise(x * 0.6f, y * 0.6f) * 0.06f;
                        h[i] = 0.6f + spk;
                        a[i] = Mul(blk, 1f + j + spk * 0.3f);
                    }
                }
        }

        private static void Fieldstone(Color[] a, float[] h)
        {
            const int K = 24;
            var px = new float[K]; var py = new float[K];
            for (int k = 0; k < K; k++) { px[k] = Hash(k, 1) * Res; py[k] = Hash(k, 2) * Res; }

            Color mortar = new Color(0.72f, 0.68f, 0.60f);
            for (int y = 0; y < Res; y++)
                for (int x = 0; x < Res; x++)
                {
                    int i = y * Res + x;
                    float d1 = 1e9f, d2 = 1e9f; int near = 0;
                    for (int k = 0; k < K; k++)
                    {
                        float dx = Mathf.Abs(x - px[k]); dx = Mathf.Min(dx, Res - dx);
                        float dy = Mathf.Abs(y - py[k]); dy = Mathf.Min(dy, Res - dy);
                        float d = dx * dx + dy * dy;
                        if (d < d1) { d2 = d1; d1 = d; near = k; }
                        else if (d < d2) d2 = d;
                    }
                    float edge = Mathf.Sqrt(d2) - Mathf.Sqrt(d1);
                    if (edge < 3f) { h[i] = 0.3f; a[i] = Mul(mortar, 1f); }
                    else
                    {
                        float g = 0.55f + Hash(near, 5) * 0.35f;
                        Color stone = Hash(near, 9) > 0.5f
                            ? new Color(g, g, g)
                            : new Color(g, g * 0.85f, g * 0.70f);
                        float dome = Mathf.Clamp01(edge / 14f);
                        h[i] = 0.45f + dome * 0.4f + Mathf.PerlinNoise(x * 0.4f, y * 0.4f) * 0.05f;
                        a[i] = stone;
                    }
                }
        }

        private static void Adobe(Color[] a, float[] h)
        {
            Color baseC = new Color(0.78f, 0.55f, 0.36f);
            for (int y = 0; y < Res; y++)
                for (int x = 0; x < Res; x++)
                {
                    int i = y * Res + x;
                    h[i] = 0.4f + Mathf.PerlinNoise(x * 0.03f, y * 0.03f) * 0.35f;
                    float v = (Mathf.PerlinNoise(x * 0.08f + 5f, y * 0.08f + 5f) - 0.5f) * 0.12f;
                    Color c = Mul(baseC, 1f + v);
                    if (Hash(x, y) > 0.985f) c = Mul(c, 0.7f); // bits of straw
                    a[i] = c;
                }
        }

        private static void WoodShingle(Color[] a, float[] h)
        {
            Color cedar = new Color(0.60f, 0.45f, 0.30f);
            int rh = Res / 7, sw = Res / 8;
            int gap = Mathf.RoundToInt(Res * 0.014f), lap = Mathf.RoundToInt(rh * 0.25f);
            for (int y = 0; y < Res; y++)
                for (int x = 0; x < Res; x++)
                {
                    int i = y * Res + x;
                    Warp(x, y, 4f, 0.05f, out int wx, out int wy);
                    int row = wy / rh, yin = wy % rh;
                    int off = (row % 2 == 0) ? 0 : sw / 2;
                    int xx = (wx + off) % Res, xin = xx % sw;
                    int sid = row * 97 + xx / sw;
                    float grain = (Mathf.PerlinNoise(x * 0.04f, sid * 2.1f) - 0.5f) * 0.10f;
                    float weather = (Hash(sid, 3) - 0.5f) * 0.18f;
                    Color c = Mul(cedar, 1f + grain + weather);
                    if (xin < gap) { h[i] = 0.25f; a[i] = Mul(c, 0.6f); }
                    else if (yin < lap) { h[i] = 0.4f; a[i] = Mul(c, 0.8f); }   // shaded by the course above
                    else
                    {
                        float t = (yin - lap) / (float)(rh - lap);
                        h[i] = 0.5f + t * 0.35f;
                        a[i] = c;
                    }
                }
        }

        private static void Vinyl(Color[] a, float[] h)
        {
            Color col = new Color(0.22f, 0.58f, 0.55f);
            int ph = Res / 6, groove = Mathf.RoundToInt(Res * 0.012f);
            for (int y = 0; y < Res; y++)
                for (int x = 0; x < Res; x++)
                {
                    int i = y * Res + x;
                    Warp(x, y, 3f, 0.05f, out int wx, out int wy);
                    int yin = wy % ph;
                    if (yin < groove) { h[i] = 0.3f; a[i] = Mul(col, 0.85f); }
                    else
                    {
                        float t = (yin - groove) / (float)(ph - groove);
                        float curve = Mathf.Sin(t * Mathf.PI) * 0.12f; // gentle convex panel
                        h[i] = 0.5f + curve;
                        a[i] = Mul(col, 0.98f + curve * 0.2f);
                    }
                }
        }

        // ----- Compose into a material (mottled+baked albedo, normal map, matte) -----

        private static Material Compose(Color[] albedo, float[] height, float smoothness)
        {
            var alb = new Texture2D(Res, Res, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Repeat };
            var baked = new Color[albedo.Length];
            for (int y = 0; y < Res; y++)
                for (int x = 0; x < Res; x++)
                {
                    int i = y * Res + x;
                    float hl = height[i];
                    float hx = height[y * Res + (x + 1) % Res];
                    float hy = height[((y + 1) % Res) * Res + x];
                    // Cheap relief shading baked in so it never looks flat even if the
                    // normal map is ignored: ridges brighten, grooves darken.
                    float shade = Mathf.Clamp(1f + ((hl - hx) + (hl - hy)) * 1.6f, 0.78f, 1.18f);
                    baked[i] = Mul(albedo[i], shade);
                }
            alb.SetPixels(baked); alb.Apply();

            var nrm = BuildNormal(height);

            var mat = new Material(Shader.Find("Standard") ?? Shader.Find("Sprites/Default"));
            mat.mainTexture = alb;
            if (mat.HasProperty("_BumpMap"))
            {
                mat.SetTexture("_BumpMap", nrm);
                mat.EnableKeyword("_NORMALMAP");
            }
            if (mat.HasProperty("_Glossiness")) mat.SetFloat("_Glossiness", smoothness);
            if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", 0f);
            return mat;
        }

        private static Texture2D BuildNormal(float[] height)
        {
            var tex = new Texture2D(Res, Res, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Repeat };
            var px = new Color[Res * Res];
            const float strength = 2.2f;
            for (int y = 0; y < Res; y++)
                for (int x = 0; x < Res; x++)
                {
                    float hL = height[y * Res + (x - 1 + Res) % Res];
                    float hR = height[y * Res + (x + 1) % Res];
                    float hD = height[((y - 1 + Res) % Res) * Res + x];
                    float hU = height[((y + 1) % Res) * Res + x];
                    Vector3 n = new Vector3((hL - hR) * strength, (hD - hU) * strength, 1f).normalized;
                    float ex = n.x * 0.5f + 0.5f, ey = n.y * 0.5f + 0.5f, ez = n.z * 0.5f + 0.5f;
                    // R/B = straight RGB path, A = x for the DXT5nm (AG) path → works either way on PC.
                    px[y * Res + x] = new Color(ex, ey, ez, ex);
                }
            tex.SetPixels(px); tex.Apply();
            return tex;
        }

        // ----- Tiny helpers -----

        private static Color Mul(Color c, float f) => new Color(c.r * f, c.g * f, c.b * f, 1f);

        /// <summary>
        /// Domain-warp: nudge the lookup coordinate by smooth noise so the otherwise dead-straight
        /// brick/siding/block lines come out gently wavy — much less "square" and more hand-made.
        /// </summary>
        private static void Warp(int x, int y, float amp, float freq, out int wx, out int wy)
        {
            float ox = (Mathf.PerlinNoise(x * freq, y * freq) - 0.5f) * amp;
            float oy = (Mathf.PerlinNoise((x + 37) * freq, (y + 37) * freq) - 0.5f) * amp;
            wx = WrapI(Mathf.FloorToInt(x + ox));
            wy = WrapI(Mathf.FloorToInt(y + oy));
        }

        private static int WrapI(int v) => ((v % Res) + Res) % Res;

        /// <summary>Deterministic 0..1 hash for per-brick / per-stone jitter.</summary>
        private static float Hash(int a, int b)
        {
            int h = a * 374761393 + b * 668265263;
            h = (h ^ (h >> 13)) * 1274126177;
            return ((h ^ (h >> 16)) & 0xFFFF) / 65535f;
        }
    }
}
