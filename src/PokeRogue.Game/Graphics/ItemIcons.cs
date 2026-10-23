using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace PokeRogue.Game.Graphics;

/// <summary>
/// Held item icons: the PokeAPI sprites in content/icons/items (30×30, drawn cropped to their middle 24×24).
/// A few get a palette swap, so items sharing a sprite stay apart at a glance.
/// </summary>
public sealed class ItemIcons(GraphicsDevice device, SpriteLibrary sprites)
{
    /// <summary>The part of each 30×30 sprite that holds the item.</summary>
    public static readonly Rectangle Crop = new(3, 3, 24, 24);

    private static readonly Dictionary<string, (Color From, Color To)[]> Recolors = new(StringComparer.Ordinal)
    {
        // Zinco and Ferro share the vitamin bottle with caps of almost the same yellow: Zinco's cap turns purple.
        ["zinc"] = [(new(189, 132, 49), new(148, 90, 189)), (new(239, 206, 107), new(206, 165, 239)), (new(123, 107, 0), new(90, 49, 123))],
    };

    private readonly Dictionary<string, Texture2D?> _recoloured = new(StringComparer.Ordinal);

    /// <summary>The icon of <paramref name="iconKey"/>; <paramref name="original"/> skips its palette swap (previews).</summary>
    public Texture2D? Get(string iconKey, bool original = false)
    {
        var texture = sprites.Texture(Path.Combine("icons", "items", iconKey + ".png"));
        if (original || texture is null || !Recolors.TryGetValue(iconKey, out var swaps))
            return texture;

        if (!_recoloured.TryGetValue(iconKey, out var recoloured))
            _recoloured[iconKey] = recoloured = Recolour(texture, swaps);
        return recoloured;
    }

    /// <summary>Draws the icon's cropped middle filling <paramref name="area"/>.</summary>
    public void Draw(SpriteBatch batch, string iconKey, Rectangle area, bool original = false, float alpha = 1f)
    {
        if (Get(iconKey, original) is { } icon)
            batch.Draw(icon, area, Crop, Color.White * alpha);
    }

    /// <summary>Whether <paramref name="iconKey"/> has a palette swap.</summary>
    public static bool IsRecoloured(string iconKey) => Recolors.ContainsKey(iconKey);

    private Texture2D Recolour(Texture2D source, (Color From, Color To)[] swaps)
    {
        var pixels = new Color[source.Width * source.Height];
        source.GetData(pixels);
        for (var i = 0; i < pixels.Length; i++)
        {
            foreach (var (from, to) in swaps)
            {
                if (pixels[i].R == from.R && pixels[i].G == from.G && pixels[i].B == from.B && pixels[i].A == 255)
                {
                    pixels[i] = to;
                    break;
                }
            }
        }

        var texture = new Texture2D(device, source.Width, source.Height);
        texture.SetData(pixels);
        return texture;
    }
}
