using System.Collections.Generic;
using UnityEngine;

namespace StandingStonesOfValheim
{
    // The Elder Futhark rune of each god's stone.
    enum Rune { Tiwaz, Mannaz, Othala, Ingwaz, Eihwaz, Hagalaz, Berkano, Isa, Algiz, Thurisaz, Sowilo, Ansuz, Fehu, Kenaz, Uruz, Laguz }

    // The runes are drawn in code as strokes: the HUD icon of the chosen stone (a dark stone with the rune
    // glowing in its colour) and the glowing rune carved on the pillar itself.
    static class Runes
    {
        // Strokes in a unit square, y up: each array is a line through its points.
        static Vector2[][] Strokes(Rune rune)
        {
            switch (rune)
            {
                case Rune.Tiwaz: return L(P(0.5f, 0.1f, 0.5f, 0.9f), P(0.25f, 0.65f, 0.5f, 0.9f, 0.75f, 0.65f));
                case Rune.Mannaz: return L(P(0.3f, 0.1f, 0.3f, 0.9f), P(0.7f, 0.1f, 0.7f, 0.9f), P(0.3f, 0.9f, 0.7f, 0.55f), P(0.7f, 0.9f, 0.3f, 0.55f));
                case Rune.Othala: return L(P(0.22f, 0.1f, 0.72f, 0.6f, 0.5f, 0.88f, 0.28f, 0.6f, 0.78f, 0.1f));
                case Rune.Ingwaz: return L(P(0.5f, 0.15f, 0.78f, 0.5f, 0.5f, 0.85f, 0.22f, 0.5f, 0.5f, 0.15f));
                case Rune.Eihwaz: return L(P(0.5f, 0.1f, 0.5f, 0.9f), P(0.5f, 0.9f, 0.72f, 0.72f), P(0.5f, 0.1f, 0.28f, 0.28f));
                case Rune.Hagalaz: return L(P(0.3f, 0.1f, 0.3f, 0.9f), P(0.7f, 0.1f, 0.7f, 0.9f), P(0.3f, 0.62f, 0.7f, 0.38f));
                case Rune.Berkano: return L(P(0.35f, 0.1f, 0.35f, 0.9f), P(0.35f, 0.9f, 0.68f, 0.7f, 0.35f, 0.5f, 0.68f, 0.3f, 0.35f, 0.1f));
                case Rune.Isa: return L(P(0.5f, 0.1f, 0.5f, 0.9f));
                case Rune.Algiz: return L(P(0.5f, 0.1f, 0.5f, 0.9f), P(0.25f, 0.85f, 0.5f, 0.5f, 0.75f, 0.85f));
                case Rune.Thurisaz: return L(P(0.36f, 0.1f, 0.36f, 0.9f), P(0.36f, 0.72f, 0.66f, 0.5f, 0.36f, 0.28f));
                case Rune.Sowilo: return L(P(0.66f, 0.9f, 0.34f, 0.6f, 0.66f, 0.4f, 0.34f, 0.1f));
                case Rune.Ansuz: return L(P(0.36f, 0.1f, 0.36f, 0.9f), P(0.36f, 0.9f, 0.7f, 0.68f), P(0.36f, 0.68f, 0.7f, 0.46f));
                case Rune.Fehu: return L(P(0.36f, 0.1f, 0.36f, 0.9f), P(0.36f, 0.6f, 0.72f, 0.86f), P(0.36f, 0.38f, 0.72f, 0.64f));
                case Rune.Kenaz: return L(P(0.64f, 0.85f, 0.36f, 0.5f, 0.64f, 0.15f));
                case Rune.Uruz: return L(P(0.3f, 0.1f, 0.3f, 0.9f, 0.7f, 0.66f, 0.7f, 0.1f));
                default: return L(P(0.42f, 0.1f, 0.42f, 0.9f, 0.7f, 0.64f)); // Laguz
            }
        }

        static Vector2[][] L(params Vector2[][] lines) { return lines; }

        static Vector2[] P(params float[] xy)
        {
            Vector2[] points = new Vector2[xy.Length / 2];
            for (int i = 0; i < points.Length; i++) points[i] = new Vector2(xy[2 * i], xy[2 * i + 1]);
            return points;
        }

        // How far (in the unit square) p is from the rune's nearest stroke.
        static float Distance(Vector2[][] strokes, Vector2 p)
        {
            float best = 10f;
            foreach (Vector2[] line in strokes)
                for (int i = 0; i + 1 < line.Length; i++)
                {
                    Vector2 a = line[i], ab = line[i + 1] - a;
                    float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(ab.sqrMagnitude, 1e-6f));
                    best = Mathf.Min(best, (a + ab * t - p).magnitude);
                }
            return best;
        }

        static readonly Dictionary<string, Sprite> icons = new Dictionary<string, Sprite>();

        // The HUD icon: the rune glowing on a rounded grey stone.
        internal static Sprite Icon(Stone stone)
        {
            return Icon(stone.Rune, stone.Glow, stone.Id);
        }

        internal static Sprite Icon(Rune rune, Color glow, string key)
        {
            Sprite sprite;
            if (icons.TryGetValue(key, out sprite)) return sprite;
            const int size = 64;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            Vector2[][] strokes = Strokes(rune);
            Color[] pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    // Like the game's own effect icons: the shape alone, bright with a dark rim, no
                    // background; a soft glow of the stone's colour around it.
                    Vector2 uv = new Vector2((x + 0.5f) / size, (y + 0.5f) / size);
                    // Room above the rune: the HUD writes the effect's name over the icon's top edge.
                    Vector2 runeUv = new Vector2((uv.x - 0.5f) / 0.78f + 0.5f, (uv.y - 0.44f) / 0.76f + 0.5f);
                    float d = Distance(strokes, runeUv);
                    float core = Mathf.Clamp01((0.075f - d) / 0.018f);
                    float rim = Mathf.Clamp01((0.115f - d) / 0.018f);
                    float halo = Mathf.Clamp01((0.24f - d) / 0.13f);
                    Color bright = Color.Lerp(glow, Color.white, 0.35f + 0.35f * Mathf.Clamp01(1f - d / 0.05f));
                    Color c = new Color(glow.r, glow.g, glow.b, halo * halo * 0.45f);
                    c = Color.Lerp(c, new Color(0.06f, 0.05f, 0.04f, 1f), rim);
                    c = Color.Lerp(c, bright, core);
                    c.a = Mathf.Max(c.a, Mathf.Max(rim, core));
                    pixels[y * size + x] = c;
                }
            tex.SetPixels(pixels);
            tex.Apply();
            sprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
            sprite.name = "SSoV_Icon_" + key;
            icons[key] = sprite;
            return sprite;
        }

        static Sprite dot;

        // Heimdall's map mark for an enemy coming for you: a red dot with a dark rim.
        internal static Sprite Dot()
        {
            if (dot != null) return dot;
            const int size = 32;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            Color[] pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float d = new Vector2(x + 0.5f - size / 2f, y + 0.5f - size / 2f).magnitude / (size / 2f);
                    Color c = d < 0.62f ? new Color(0.95f, 0.15f, 0.1f) : new Color(0.1f, 0.02f, 0.02f);
                    c.a = Mathf.Clamp01((0.95f - d) * 12f);
                    pixels[y * size + x] = c;
                }
            tex.SetPixels(pixels);
            tex.Apply();
            dot = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
            dot.name = "SSoV_Heimdall";
            return dot;
        }

        // The rune alone, glowing on a clear background, to put on the pillar.
        internal static Sprite Carving(Stone stone)
        {
            const int size = 128;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            Vector2[][] strokes = Strokes(stone.Rune);
            Color glow = stone.Glow;
            Color[] pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float d = Distance(strokes, new Vector2((x + 0.5f) / size, (y + 0.5f) / size));
                    float core = Mathf.Clamp01((0.045f - d) / 0.012f);
                    float halo = Mathf.Clamp01((0.12f - d) / 0.075f);
                    Color c = Color.Lerp(glow, Color.white, core * 0.35f);
                    c.a = Mathf.Max(core, halo * halo * 0.55f);
                    pixels[y * size + x] = c;
                }
            tex.SetPixels(pixels);
            tex.Apply();
            Sprite sprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
            sprite.name = "SSoV_Rune_" + stone.Id;
            return sprite;
        }
    }
}
