using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace PokeRogue.Game.Graphics;

/// <summary>Loads PNGs at runtime (no content pipeline) as premultiplied-alpha textures, matching <see cref="BlendState.AlphaBlend"/>.</summary>
public static class TextureLoader
{
    public static Texture2D Load(GraphicsDevice device, string path)
    {
        using var stream = File.OpenRead(path);
        var texture = Texture2D.FromStream(device, stream);
        var pixels = new Color[texture.Width * texture.Height];
        texture.GetData(pixels);
        for (var i = 0; i < pixels.Length; i++)
            pixels[i] = Color.FromNonPremultiplied(pixels[i].R, pixels[i].G, pixels[i].B, pixels[i].A);
        texture.SetData(pixels);
        return texture;
    }

    /// <summary>Same shape as <paramref name="source"/>, fully white: used for hit flashes and evolution glow.</summary>
    public static Texture2D CreateSilhouette(GraphicsDevice device, Texture2D source)
    {
        var pixels = new Color[source.Width * source.Height];
        source.GetData(pixels);
        for (var i = 0; i < pixels.Length; i++)
        {
            var a = pixels[i].A;
            pixels[i] = new Color(a, a, a, a);
        }

        var silhouette = new Texture2D(device, source.Width, source.Height);
        silhouette.SetData(pixels);
        return silhouette;
    }

    /// <summary>
    /// A shiny look (phase 3.5): every coloured pixel turned round the colour wheel by <paramref name="degrees"/>, keeping
    /// its brightness. Greys, black outlines and whites stay as they are. Works on premultiplied pixels.
    /// </summary>
    public static Texture2D CreateHueShift(GraphicsDevice device, Texture2D source, float degrees)
    {
        var pixels = new Color[source.Width * source.Height];
        source.GetData(pixels);
        for (var i = 0; i < pixels.Length; i++)
        {
            var p = pixels[i];
            if (p.A == 0)
                continue;

            var alpha = p.A / 255f;
            var (h, s, v) = ToHsv(p.R / 255f / alpha, p.G / 255f / alpha, p.B / 255f / alpha);
            if (s < 0.18f)
                continue;

            var (r, g, b) = FromHsv((h + degrees / 360f) % 1f, s, v);
            pixels[i] = new Color(r * alpha, g * alpha, b * alpha, alpha);
        }

        var shifted = new Texture2D(device, source.Width, source.Height);
        shifted.SetData(pixels);
        return shifted;
    }

    private static (float H, float S, float V) ToHsv(float r, float g, float b)
    {
        var max = MathF.Max(r, MathF.Max(g, b));
        var min = MathF.Min(r, MathF.Min(g, b));
        var delta = max - min;
        var h = delta < 1e-5f ? 0f
            : max == r ? (g - b) / delta / 6f
            : max == g ? ((b - r) / delta + 2f) / 6f
            : ((r - g) / delta + 4f) / 6f;
        return (h < 0f ? h + 1f : h, max <= 0f ? 0f : delta / max, max);
    }

    private static (float R, float G, float B) FromHsv(float h, float s, float v)
    {
        var sector = h * 6f;
        var i = (int)MathF.Floor(sector) % 6;
        var f = sector - MathF.Floor(sector);
        var p = v * (1f - s);
        var q = v * (1f - s * f);
        var t = v * (1f - s * (1f - f));
        return i switch
        {
            0 => (v, t, p),
            1 => (q, v, p),
            2 => (p, v, t),
            3 => (p, q, v),
            4 => (t, p, v),
            _ => (v, p, q),
        };
    }
}
