using UnityEngine;

namespace RoofingSimulator.Gameplay
{
    /// <summary>
    /// Shared procedural DETAIL textures for the roof layers (wood deck, old/new shingles,
    /// felt). They are near-white grayscale with grain/streaks so they MULTIPLY against each
    /// piece's colour — the slabs keep their tint but stop looking flat, at no per-cell cost
    /// (textures are generated once and shared by every cell).
    /// </summary>
    public static class RoofTextureLibrary
    {
        public enum Surface { AsphaltShingle, OldShingle, Felt, WoodDeck, Metal, Brick }

        private const int Res = 128;
        private static Texture2D asphalt, oldS, felt, wood, metal, brick;

        public static Texture2D Get(Surface s)
        {
            switch (s)
            {
                case Surface.AsphaltShingle: return asphalt != null ? asphalt : (asphalt = Granular(0.93f, 0.10f));
                case Surface.OldShingle:     return oldS != null ? oldS : (oldS = Weathered());
                case Surface.Felt:           return felt != null ? felt : (felt = FeltTex());
                case Surface.Metal:          return metal != null ? metal : (metal = MetalTex());
                case Surface.Brick:          return brick != null ? brick : (brick = BrickTex());
                default:                     return wood != null ? wood : (wood = WoodTex());
            }
        }

        // ----- Generators (return a tiling grayscale detail map) -----

        private static Texture2D Granular(float mean, float range)
        {
            var px = new Color[Res * Res];
            for (int y = 0; y < Res; y++)
                for (int x = 0; x < Res; x++)
                {
                    float n = (Hash(x, y) - 0.5f) * range
                            + (Mathf.PerlinNoise(x * 0.25f, y * 0.25f) - 0.5f) * range * 0.6f;
                    float v = Mathf.Clamp01(mean + n);
                    px[y * Res + x] = new Color(v, v, v, 1f);
                }
            return Make(px);
        }

        private static Texture2D Weathered()
        {
            var px = new Color[Res * Res];
            for (int y = 0; y < Res; y++)
                for (int x = 0; x < Res; x++)
                {
                    float streak = (Mathf.PerlinNoise(x * 0.04f, y * 0.30f) - 0.5f) * 0.18f;
                    float grain = (Hash(x, y) - 0.5f) * 0.14f;
                    float v = Mathf.Clamp01(0.82f + streak + grain);
                    px[y * Res + x] = new Color(v, v, v * 0.98f, 1f);
                }
            return Make(px);
        }

        private static Texture2D FeltTex()
        {
            var px = new Color[Res * Res];
            for (int y = 0; y < Res; y++)
                for (int x = 0; x < Res; x++)
                {
                    float n = (Hash(x * 2, y) - 0.5f) * 0.08f
                            + (Mathf.PerlinNoise(x * 0.6f, y * 0.15f) - 0.5f) * 0.05f;
                    float v = Mathf.Clamp01(0.95f + n);
                    px[y * Res + x] = new Color(v, v, v, 1f);
                }
            return Make(px);
        }

        private static Texture2D WoodTex()
        {
            var px = new Color[Res * Res];
            for (int y = 0; y < Res; y++)
                for (int x = 0; x < Res; x++)
                {
                    float grain = Mathf.Sin((y + Mathf.PerlinNoise(x * 0.02f, y * 0.08f) * 6f) * 0.9f) * 0.06f;
                    float fiber = (Mathf.PerlinNoise(x * 0.5f, y * 0.05f) - 0.5f) * 0.08f;
                    float seam = (x % 42 < 2) ? -0.15f : 0f;   // occasional plank seam
                    float v = Mathf.Clamp01(0.90f + grain + fiber + seam);
                    px[y * Res + x] = new Color(v, v * 0.98f, v * 0.95f, 1f);
                }
            return Make(px);
        }

        private static Texture2D BrickTex()
        {
            var px = new Color[Res * Res];
            for (int y = 0; y < Res; y++)
                for (int x = 0; x < Res; x++)
                {
                    int course = y / 16;                       // brick courses 16 px tall
                    int xo = course % 2 == 0 ? x : x + 16;      // stagger every other course
                    bool mortar = y % 16 < 2 || xo % 32 < 2;    // 2 px mortar joints
                    float grain = (Hash(x, y) - 0.5f) * 0.10f;
                    float v = Mathf.Clamp01((mortar ? 0.68f : 0.93f) + grain);
                    // Mortar reads neutral; brick faces keep a warm bias for the tint.
                    px[y * Res + x] = mortar
                        ? new Color(v, v, v, 1f)
                        : new Color(v, v * 0.93f, v * 0.88f, 1f);
                }
            return Make(px);
        }

        private static Texture2D MetalTex()
        {
            var px = new Color[Res * Res];
            for (int y = 0; y < Res; y++)
                for (int x = 0; x < Res; x++)
                {
                    float scratch = (Mathf.PerlinNoise(x * 0.5f, y * 0.04f) - 0.5f) * 0.10f; // brushed/scratched
                    float grain = (Hash(x, y) - 0.5f) * 0.05f;
                    float v = Mathf.Clamp01(0.93f + scratch + grain);
                    px[y * Res + x] = new Color(v, v, v, 1f);
                }
            return Make(px);
        }

        // ----- Helpers -----

        private static Texture2D Make(Color[] px)
        {
            var t = new Texture2D(Res, Res, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Repeat };
            t.SetPixels(px); t.Apply();
            return t;
        }

        private static float Hash(int a, int b)
        {
            int h = a * 374761393 + b * 668265263;
            h = (h ^ (h >> 13)) * 1274126177;
            return ((h ^ (h >> 16)) & 0xFFFF) / 65535f;
        }
    }
}
