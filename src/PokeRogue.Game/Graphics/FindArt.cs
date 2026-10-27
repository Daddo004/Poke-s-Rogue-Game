using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace PokeRogue.Game.Graphics;

/// <summary>
/// The fossils and eggs hidden on the levels (phase 7.4), drawn as pixel patterns (the PokeAPI item sprites were not
/// downloaded): a stone with the fossil's shape, the orange amber, and an egg in the colours of who will hatch.
/// </summary>
public sealed class FindArt(GraphicsDevice device)
{
    private static readonly Color Ink = new(16, 12, 28);
    private readonly Dictionary<string, Texture2D> _icons = new(StringComparer.Ordinal);

    /// <summary>The icon of the find that brings <paramref name="characterId"/>.</summary>
    public Texture2D Icon(string characterId)
    {
        if (!_icons.TryGetValue(characterId, out var icon))
            _icons[characterId] = icon = Build(characterId);
        return icon;
    }

    /// <summary>Whether the find is an egg (it hatches) rather than a fossil (revived in the shop).</summary>
    public static bool IsEgg(string characterId) => characterId is "togepi" or "riolu" or "manaphy";

    private Texture2D Build(string id)
    {
        var stone = new Dictionary<char, Color> { ['s'] = new(150, 126, 100), ['l'] = new(190, 166, 132), ['d'] = new(104, 86, 70), ['f'] = new(232, 220, 196), ['g'] = new(86, 70, 56) };
        string[] rows = id switch
        {
            // A spiral shell (Omanyte).
            "omanyte" => ["...ssss...", "..slllls..", ".sl.ffff.s", "slffddfffs", "slfd.ggfls", "sffdgdfdls", "slffffdfls", ".slffffls.", "..sddddd..", "...ssss..."],
            // A dome (Kabuto).
            "kabuto" => ["...ffff...", "..ffllff..", ".fflddlff.", "ffld..dlff", "fld....dlf", "fld....dlf", "sssssssss.", "sdsdsdsds.", "ssssssssss", ".dddddddd."],
            // A root (Lileep).
            "lileep" => ["....ff....", "...flf....", "..flff.f..", "..fff.flf.", "...ff.ff..", "..ff.fff..", ".ff..ff...", "ff...ff...", "f...ff.f..", "...ff...f."],
            // A claw (Anorith).
            "anorith" => [".......ff.", "......flf.", ".....flf..", "....flf...", "...flf.ff.", "..flf.flf.", ".flf.flf..", "flfffff...", "ffff......", ".dd......."],
            // A skull (Cranidos).
            "cranidos" => ["..ffffff..", ".ffllllff.", "ffllllllff", "fld.ll.dlf", "fl..ll..lf", "fllllllllf", ".ffl..lff.", "..fdffdf..", "..f.ff.f..", "...ffff..."],
            // A shield (Shieldon).
            "shieldon" => ["ffffffffff", "fllllllllf", "flddllddlf", "fllllllllf", "fldllllldf", ".flllllf..", ".fldddlf..", "..fllllf..", "...fllf...", "....ff...."],
            _ => [],
        };
        if (rows.Length > 0)
            return PixelArt.FromPatternOutlined(device, rows, stone, Ink);

        if (id == "aerodactyl")
        {
            // Amber with a mosquito inside.
            return PixelArt.FromPatternOutlined(device,
                ["...aaaa...", "..aAAAAa..", ".aAAaAAAa.", "aAAkAkAAAa", "aAAAkAAAAa", "aAAkAkAAaa", "aAAAAAAaaa", ".aAAAAaaa.", "..aaaaaa..", "...aaaa..."],
                new Dictionary<char, Color> { ['a'] = new(200, 110, 30), ['A'] = new(244, 170, 60), ['k'] = new(90, 50, 20) }, Ink);
        }

        // An egg in the colours of who hatches: Togepi's red and blue triangles, Riolu's blue and black, Manaphy's blue with a red heart.
        var (main, mark, light) = id switch
        {
            "riolu" => (new Color(110, 150, 220), new Color(40, 40, 60), new Color(170, 200, 240)),
            "manaphy" => (new Color(90, 170, 230), new Color(230, 70, 70), new Color(170, 220, 250)),
            _ => (new Color(244, 238, 222), new Color(220, 70, 70), new Color(255, 255, 255)),
        };
        var egg = new Dictionary<char, Color> { ['e'] = main, ['m'] = mark, ['l'] = light, ['b'] = id == "togepi" ? new Color(70, 110, 220) : mark };
        return PixelArt.FromPatternOutlined(device,
            ["...eeee...", "..elleee..", ".ellemeee.", ".elmmmeee.", "eeeeeeebee", "eeeeeebbbe", "eemeeeeeee", "emmmeeeeee", ".eeeeeeee.", "..eeeeee.."],
            egg, Ink);
    }
}
