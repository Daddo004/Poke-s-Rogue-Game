using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PokeRogue.Core.Combat;

namespace PokeRogue.Game.Graphics;

/// <summary>How a status shows above an enemy's head (the phase 2.3 preview compares all three).</summary>
public enum StatusIconStyle
{
    /// <summary>The coloured symbol with a dark outline.</summary>
    Symbol,

    /// <summary>The symbol on a tile of the status colour, like the games' status labels.</summary>
    Badge,

    /// <summary>No icon: tint and effects only.</summary>
    None,
}

/// <summary>Status condition art: four tones per status and a 7×7 pixel symbol, as an outlined symbol or a badge (both 9×9).</summary>
public sealed class StatusIcons
{
    public const int Size = 9;

    private readonly Dictionary<StatusKind, (Texture2D Symbol, Texture2D Badge)> _icons = [];

    public StatusIcons(GraphicsDevice device)
    {
        foreach (var kind in Enum.GetValues<StatusKind>())
        {
            if (kind == StatusKind.None)
                continue;

            var art = Art(kind);
            _icons[kind] = (BuildSymbol(device, art), BuildBadge(device, art));
        }

        SmallZ = PixelArt.FromPattern(device, [".ooo.", "owwwo", "oowoo", "owwwo", ".ooo."], Ink(new Color(240, 240, 255)));
        BigZ = PixelArt.FromPattern(device, [".oooo.", "owwwwo", "ooowoo", "oowooo", "owwwwo", ".oooo."], Ink(new Color(240, 240, 255)));
        Star = PixelArt.FromPattern(device, [".ooo.", "oowoo", "owwwo", "oowoo", ".ooo."], new Dictionary<char, Color> { ['o'] = new(96, 64, 8), ['w'] = new(255, 228, 72) });
    }

    /// <summary>Little "z" drifting up from a sleeping Pokémon.</summary>
    public Texture2D SmallZ { get; }

    /// <summary>The same "Z", grown.</summary>
    public Texture2D BigZ { get; }

    /// <summary>A star circling a confused Pokémon's head.</summary>
    public Texture2D Star { get; }

    public Texture2D? Icon(StatusKind kind, StatusIconStyle style) =>
        style == StatusIconStyle.None || !_icons.TryGetValue(kind, out var icons) ? null
        : style == StatusIconStyle.Badge ? icons.Badge : icons.Symbol;

    /// <summary>Main, light, dark and ink tones of a status (badge tile, highlights, outline, symbol on the badge).</summary>
    public static StatusArt Art(StatusKind kind) => kind switch
    {
        StatusKind.Burn => new(new(240, 112, 48), new(255, 214, 96), new(112, 28, 12), Color.White,
        [
            "...m...",
            "..mm...",
            "..mmm..",
            ".mmlmm.",
            ".mlllm.",
            ".mlwlm.",
            "..mmm..",
        ]),
        StatusKind.Poison => new(new(168, 80, 208), new(230, 184, 255), new(60, 22, 84), Color.White,
        [
            "...m...",
            "...m...",
            "..mmm..",
            ".mlmmm.",
            ".lmmms.",
            ".mmmss.",
            "..sss..",
        ]),
        StatusKind.Paralysis => new(new(248, 208, 48), new(255, 248, 184), new(100, 68, 8), new(100, 68, 8),
        [
            "....lm.",
            "...lm..",
            "..lm...",
            ".lmmmm.",
            "...mm..",
            "..mm...",
            ".mm....",
        ]),
        StatusKind.Freeze => new(new(112, 196, 248), new(236, 250, 255), new(28, 68, 124), Color.White,
        [
            "...l...",
            ".m.l.m.",
            "..mlm..",
            "lllllll",
            "..mlm..",
            ".m.l.m.",
            "...l...",
        ]),
        StatusKind.Sleep => new(new(150, 142, 196), new(244, 244, 255), new(46, 42, 80), Color.White,
        [
            "....mmm",
            ".....m.",
            "....mmm",
            "llll...",
            "..l....",
            ".l.....",
            "llll...",
        ]),
        _ => new(new(240, 104, 176), new(255, 210, 234), new(92, 22, 70), Color.White,
        [
            "mmmmmmm",
            "......m",
            "mmmmm.m",
            "m...m.m",
            "m.mmm.m",
            "m.....m",
            "mmmmmmm",
        ]),
    };

    /// <summary>Colours of the symbol: m main, l light, s shade, w white-hot.</summary>
    private static Dictionary<char, Color> SymbolPalette(StatusArt art) => new()
    {
        ['m'] = art.Main,
        ['l'] = art.Light,
        ['s'] = Color.Lerp(art.Main, art.Dark, 0.45f),
        ['w'] = Color.White,
    };

    /// <summary>The symbol with a dark outline, so it reads on grass and on sprites.</summary>
    private static Texture2D BuildSymbol(GraphicsDevice device, StatusArt art) =>
        PixelArt.FromPatternOutlined(device, art.Glyph, SymbolPalette(art), art.Dark);

    private static Texture2D BuildBadge(GraphicsDevice device, StatusArt art)
    {
        var pixels = new Color[Size * Size];
        for (var y = 0; y < Size; y++)
        {
            for (var x = 0; x < Size; x++)
            {
                var edge = x == 0 || y == 0 || x == Size - 1 || y == Size - 1;
                var corner = (x == 0 || x == Size - 1) && (y == 0 || y == Size - 1);
                pixels[y * Size + x] = corner ? Color.Transparent : edge ? art.Dark : y == 1 ? Color.Lerp(art.Main, Color.White, 0.3f) : art.Main;
            }
        }

        // The symbol in the status' ink, its highlights a step lighter.
        for (var y = 0; y < art.Glyph.Length; y++)
        {
            for (var x = 0; x < art.Glyph[y].Length; x++)
            {
                var c = art.Glyph[y][x];
                if (c == '.')
                    continue;

                pixels[(y + 1) * Size + x + 1] = c is 'l' or 'w' ? Color.Lerp(art.Ink, art.Light, 0.5f) : c == 's' ? Color.Lerp(art.Ink, art.Main, 0.35f) : art.Ink;
            }
        }

        var texture = new Texture2D(device, Size, Size);
        texture.SetData(pixels);
        return texture;
    }

    private static Dictionary<char, Color> Ink(Color fill) => new() { ['o'] = new(36, 32, 60), ['w'] = fill };
}

/// <summary>Tones of one status: <paramref name="Glyph"/> is 7×7, 'm' main, 'l' light, 's' shade, 'w' white-hot.</summary>
public readonly record struct StatusArt(Color Main, Color Light, Color Dark, Color Ink, string[] Glyph);
