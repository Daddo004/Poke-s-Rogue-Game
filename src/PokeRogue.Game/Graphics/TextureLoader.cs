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
}
