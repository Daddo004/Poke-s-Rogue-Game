using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PokeRogue.Game.Graphics;

namespace PokeRogue.Game.Preview.Artboard;

/// <summary>Procedural pixel-art pieces used by the art-direction mock-ups (gems, coins, glyphs, ground).</summary>
public sealed class ArtboardAssets
{
    public ArtboardAssets(GraphicsDevice device)
    {
        Grass = PixelArt.Grass(device, 128, 7);

        var outline = new Color(24, 20, 40);
        GemSmall = Gem(device, new Color(72, 160, 248), new Color(168, 224, 255), outline);
        GemMedium = Gem(device, new Color(64, 200, 112), new Color(176, 248, 176), outline);
        GemLarge = Gem(device, new Color(232, 64, 80), new Color(255, 176, 176), outline);

        Coin = PixelArt.FromPattern(device,
        [
            ".oooo.",
            "oyyyYo",
            "oyYyyo",
            "oyyyyo",
            "oyyyyo",
            ".oooo.",
        ], new Dictionary<char, Color> { ['o'] = new(136, 88, 24), ['y'] = new(248, 200, 56), ['Y'] = new(255, 248, 200) });

        Bolt = PixelArt.FromPattern(device,
        [
            "....oooo",
            "...oyyo.",
            "..oyyo..",
            ".oyyyooo",
            "oyyyyyyo",
            "ooooyyo.",
            "...oyo..",
            "..oyo...",
            "..oo....",
        ], new Dictionary<char, Color> { ['o'] = new(96, 64, 8), ['y'] = new(255, 224, 64) });

        Swoosh = PixelArt.FromPattern(device,
        [
            "........",
            "oooooo..",
            "...wwwo.",
            "oooooowo",
            "..wwwwo.",
            "oooooo..",
            "........",
        ], new Dictionary<char, Color> { ['o'] = new(80, 80, 72), ['w'] = new(240, 240, 224) });

        Skull = PixelArt.FromPattern(device,
        [
            ".oooo.",
            "owwwwo",
            "owowow",
            "owwwwo",
            ".owwo.",
            ".o.o..",
        ], new Dictionary<char, Color> { ['o'] = new(24, 20, 40), ['w'] = new(240, 236, 220) });
    }

    public Texture2D Grass { get; }

    public Texture2D GemSmall { get; }

    public Texture2D GemMedium { get; }

    public Texture2D GemLarge { get; }

    public Texture2D Coin { get; }

    public Texture2D Bolt { get; }

    public Texture2D Swoosh { get; }

    public Texture2D Skull { get; }

    private static Texture2D Gem(GraphicsDevice device, Color body, Color shine, Color outline) =>
        PixelArt.FromPattern(device,
        [
            "..o..",
            ".oso.",
            "osbbo",
            "obbbo",
            "obbbo",
            ".obo.",
            "..o..",
        ], new Dictionary<char, Color> { ['o'] = outline, ['b'] = body, ['s'] = shine });
}
