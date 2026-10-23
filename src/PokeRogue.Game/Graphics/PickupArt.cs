using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PokeRogue.Core.Run.Pickups;

namespace PokeRogue.Game.Graphics;

/// <summary>Pixel-art pickups approved in phase 0.6: experience gems (blue/green/red) and coins.</summary>
public sealed class PickupArt
{
    private static readonly Color Outline = new(24, 20, 40);

    public PickupArt(GraphicsDevice device)
    {
        GemSmall = Gem(device, new Color(72, 160, 248), new Color(168, 224, 255));
        GemMedium = Gem(device, new Color(64, 200, 112), new Color(176, 248, 176));
        GemLarge = Gem(device, new Color(232, 64, 80), new Color(255, 176, 176));

        Coin = PixelArt.FromPattern(device,
        [
            ".oooo.",
            "oyyyYo",
            "oyYyyo",
            "oyyyyo",
            "oyyyyo",
            ".oooo.",
        ], new Dictionary<char, Color> { ['o'] = new(136, 88, 24), ['y'] = new(248, 200, 56), ['Y'] = new(255, 248, 200) });
    }

    public Texture2D GemSmall { get; }

    public Texture2D GemMedium { get; }

    public Texture2D GemLarge { get; }

    public Texture2D Coin { get; }

    public Texture2D Gem(GemTier tier) => tier switch
    {
        GemTier.Large => GemLarge,
        GemTier.Medium => GemMedium,
        _ => GemSmall,
    };

    /// <summary>Particle colour matching a gem tier.</summary>
    public static Color GemColor(GemTier tier) => tier switch
    {
        GemTier.Large => new Color(255, 120, 130),
        GemTier.Medium => new Color(120, 240, 150),
        _ => new Color(120, 200, 255),
    };

    private static Texture2D Gem(GraphicsDevice device, Color body, Color shine) =>
        PixelArt.FromPattern(device,
        [
            "..o..",
            ".oso.",
            "osbbo",
            "obbbo",
            "obbbo",
            ".obo.",
            "..o..",
        ], new Dictionary<char, Color> { ['o'] = Outline, ['b'] = body, ['s'] = shine });
}
