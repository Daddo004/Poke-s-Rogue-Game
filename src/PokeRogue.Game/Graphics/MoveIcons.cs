using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PokeRogue.Core.Data;

namespace PokeRogue.Game.Graphics;

/// <summary>
/// Move icons: a type-coloured tile with a pixel glyph. Thundershock's bolt is the one approved in the 0.6 HUD mock-up;
/// the others are 7×7 fills outlined automatically ('w' white, 'a'/'b'/'c' accents), each approved with its move in phase 2.4.
/// Moves without a glyph yet show a generic star.
/// </summary>
public sealed class MoveIcons
{
    private static readonly Color Outline = new(40, 36, 56);
    private static readonly Color Paper = new(244, 240, 228);

    private readonly Texture2D _generic;
    private readonly Dictionary<string, Texture2D> _glyphs = new(StringComparer.Ordinal);

    public MoveIcons(GraphicsDevice device)
    {
        _glyphs["thundershock"] = PixelArt.FromPattern(device,
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

        _generic = PixelArt.FromPattern(device,
        [
            "...o...",
            "..owo..",
            "oowwwoo",
            ".owwwo.",
            "owwowwo",
            "oo...oo",
        ], new Dictionary<char, Color> { ['o'] = Outline, ['w'] = Paper });

        // Phase 2.4, group 1: Normale, Acqua, Elettro, Ghiaccio.
        Add(device, "swift", ["...w...", "..www..", "wwwwwww", ".wwaww.", "..www..", ".ww.ww.", ".w...w."], a: new(255, 224, 96));
        Add(device, "hyper-voice", [".....w.", "...w..w", "ww..w.w", "ww..w.w", "ww..w.w", "...w..w", ".....w."]);
        Add(device, "water-gun", [".....a.", "....a.a", "wwww.a.", "wwwwwwa", "wwww.a.", "....a.a", ".....a."], a: new(150, 210, 255));
        Add(device, "bubble", [".www...", "wwaaw.w", "waaaw..", "waaaw..", ".www...", ".....ww", ".....ww"], a: new(150, 210, 255));
        Add(device, "thunder-wave", ["..www..", ".w...w.", "w..a..w", "w.aaa.w", "w..a..w", ".w...w.", "..www.."], a: new(255, 236, 96));
        Add(device, "ice-shard", ["....www", "...waaw", "..waaw.", ".waaw..", "waaw.w.", "www.waw", ".....w."], a: new(170, 225, 255));
        Add(device, "aurora-beam", [".....ab", "....abc", "...abc.", "..abc..", ".abc...", "wwc....", "ww....."],
            a: new(255, 160, 220), b: new(255, 236, 140), c: new(140, 226, 255));

        // Phase 2.4, group 2: Lotta, Veleno, Volante, Psico.
        Add(device, "karate-chop", ["....www", "...wwww", "..wwww.", ".wwww..", "wwww.a.", ".ww.a..", "...a..."], a: new(255, 170, 120));
        Add(device, "aura-sphere", ["..aaa..", ".awwwa.", "awwbwwa", "awbbbwa", "awwbwwa", ".awwwa.", "..aaa.."], a: new(110, 160, 255), b: new(190, 220, 255));
        Add(device, "poison-sting", ["ww.....", "www....", ".www...", "..www..", "...wwa.", "....aa.", ".....a."], a: new(200, 120, 240));
        Add(device, "gust", ["...www.", "......w", "wwwwww.", ".......", "wwwww..", ".....w.", "..www.."]);
        Add(device, "confusion", [".......", "..www..", ".waaaw.", "wawbwaw", ".waaaw.", "..www..", "......."], a: new(255, 150, 200), b: new(90, 30, 70));
        Add(device, "hypnosis", ["...w...", "...w...", "...w...", "..www..", ".waaaw.", ".waaaw.", "..www.."], a: new(255, 214, 96));
        Add(device, "smog", [".......", "...ww..", ".wwaaw.", "waaaaaw", "waaaaaw", ".wwwww.", "......."], a: new(200, 150, 230));
        Add(device, "aerial-ace", [".wwww..", "...www.", "....www", "....www", "....www", "...www.", ".wwww.."]);

        // Phase 2.4, group 3: Coleottero, Roccia, Spettro, Drago.
        Add(device, "x-scissor", ["w.....w", "ww...ww", ".ww.ww.", "..www..", ".ww.ww.", "ww...ww", "w.....w"]);
        Add(device, "leech-life", [".wwwww.", ".wwwww.", "..www..", "..ww...", "..w..a.", "....aaa", "....aaa"], a: new(235, 50, 70));
        Add(device, "rock-tomb", [".....w.", ".w...w.", ".w..www", "www.www", "wwwwwww", "aaaaaaa", "......."], a: new(170, 140, 100));
        Add(device, "shadow-ball", ["..www..", ".waaaw.", "wabaaaw", "waaaaaw", "waaaaaw", ".waaaw.", "..www.."], a: new(80, 50, 130), b: new(200, 180, 255));
        Add(device, "night-shade", ["..www..", ".wwwww.", "wawwwaw", "wwwwwww", "wwwwwww", "wwwwwww", "w.w.w.w"], a: new(40, 20, 60));
        Add(device, "twister", ["wwwwwww", ".wwwww.", "..wwww.", "..www..", "...ww..", "...w...", "..w...."]);
        Add(device, "draco-meteor", ["w.w....", ".w.w...", "..w.w..", "...waa.", "....aaa", "....aaa", ".....a."], a: new(255, 150, 60));
        Add(device, "rock-slide", [".ww....", "waaw...", ".ww....", "...www.", "..waaaw", "..waaaw", "...www."], a: new(190, 170, 140));

        // Phase 2.4, group 4: Buio, Acciaio, and the first moves of Fuoco, Erba, Terra.
        Add(device, "bite", [".wwwww.", "w.w.w.w", ".......", ".......", "w.w.w.w", ".wwwww.", "......."]);
        Add(device, "dark-pulse", ["..aaa..", ".a...a.", "a.www.a", "a.wbw.a", "a.www.a", ".a...a.", "..aaa.."], a: new(150, 110, 200), b: new(40, 20, 50));
        Add(device, "gyro-ball", ["..www..", ".wbbaw.", "wbaaaaw", "waaaaaw", "waaaaaw", ".waaaw.", "..www.."], a: new(150, 160, 180), b: new(230, 235, 245));
        Add(device, "flash-cannon", [".......", ".ww....", "wwwaaaa", "wwwaaaa", "wwwaaaa", ".ww....", "......."], a: new(220, 230, 255));
        Add(device, "ember", ["...w...", "..ww...", "..waw..", ".waaaw.", ".waaaw.", "..www..", "......."], a: new(255, 200, 80));
        Add(device, "flame-wheel", ["..w.w..", ".waaaw.", "wa...aw", ".a...a.", "wa...aw", ".waaaw.", "..w.w.."], a: new(255, 150, 60));
        Add(device, "razor-leaf", ["....www", "..wwwaw", ".wwwaww", ".wwaww.", ".waww..", "waw....", "w......"], a: new(150, 220, 100));
        Add(device, "vine-whip", ["....aa.", "...aaw.", "....w..", "...w...", "..w....", ".w.....", "w......"], a: new(150, 220, 100));
        Add(device, "bonemerang", [".......", ".......", "ww...ww", ".wwwww.", "ww...ww", ".......", "......."]);
        Add(device, "mud-bomb", [".......", "..aa...", ".awaa.a", "aaaaaa.", ".aaaaa.", "a.aa.a.", "......."], a: new(150, 105, 60));
        Add(device, "earthquake", ["aaawaaa", "aaawwaa", "aaaawaa", "aaawaaa", "aaawwaa", "aaaawaa", "aaawaaa"], a: new(150, 110, 70));

        // Phase 2.6: the evolved moves.
        Add(device, "thunder", [".aa.aa.", "aaaaaaa", "....ww.", "...ww..", "..wwww.", "...ww..", "..w...."], a: new(96, 96, 120));
        Add(device, "flamethrower", ["....w..", "...ww..", "...waw.", ".w.waaw", "wwwaaaw", "waawaaw", ".wwwww."], a: new(255, 200, 80));
        Add(device, "air-slash", ["www....", "..aww..", "...aaw.", "....aaw", "....aaw", ".....aw", "......w"], a: new(170, 200, 255));
        Add(device, "bone-rush", ["ww..ww.", ".wwww..", "ww..ww.", ".......", ".ww..ww", "..wwww.", ".ww..ww"]);
        Add(device, "leaf-storm", ["wawwwaw", ".wwaww.", "..awww.", "..wwa..", "...ww..", "...a...", "..w...."], a: new(150, 220, 100));
        Add(device, "toxic", ["...w...", "..waw..", "..waw..", ".waaaw.", "wawaaaw", "waaaaaw", ".wwwww."], a: new(200, 120, 240));
        Add(device, "power-whip", ["....aaa", "...aaaw", "..w.ww.", ".w.ww..", "..ww.w.", ".ww....", "w..w..."], a: new(150, 220, 100));
        Add(device, "ice-beam", ["w..w..w", ".w.w.w.", "..www..", "wwwawww", "..www..", ".w.w.w.", "w..w..w"], a: new(170, 225, 255));
        Add(device, "stone-edge", [".....ww", "....waw", "...waaw", "..waaw.", ".waaw..", "waaw...", "ww....."], a: new(190, 170, 140));
        Add(device, "earth-power", ["..c.c..", ".c.c.c.", "...c...", "..aba..", ".aabaa.", "aaabaaa", "aaaaaaa"],
            a: new(150, 110, 70), b: new(40, 26, 18), c: new(255, 170, 60));
        Add(device, "flare-blitz", [".....w.", "....ww.", "w..waw.", "..waaw.", "w.waaaw", "..waaw.", "...ww.."], a: new(255, 200, 80));
        Add(device, "fissure", ["aaawbwa", "aawbwaa", "aawbbwa", "awbbbwa", "aawbbwa", "awbwaaa", "aawbwaa"], a: new(150, 110, 70), b: new(40, 26, 18));
    }

    public Texture2D Glyph(string moveId) => _glyphs.GetValueOrDefault(moveId, _generic);

    /// <summary>Whether the move has its own glyph (otherwise it shows the generic star).</summary>
    public bool HasGlyph(string moveId) => _glyphs.ContainsKey(moveId);

    /// <summary>Draws the icon filling <paramref name="area"/>; the glyph is scaled by an integer factor.</summary>
    public void Draw(SpriteBatch batch, Primitives p, Rectangle area, string moveId, PokemonType type, int glyphScale = 1)
    {
        var color = TypePalette.Of(type);
        p.Rect(batch, area, color * 0.9f);
        p.Rect(batch, new Rectangle(area.X, area.Y, area.Width, System.Math.Max(2, area.Height / 10)), Color.White * 0.35f);
        p.Rect(batch, new Rectangle(area.X, area.Bottom - System.Math.Max(2, area.Height / 10), area.Width, System.Math.Max(2, area.Height / 10)), Color.Black * 0.25f);

        var glyph = Glyph(moveId);
        var size = new Vector2(glyph.Width, glyph.Height) * glyphScale;
        batch.Draw(glyph, new Vector2(area.Center.X - (int)(size.X / 2), area.Center.Y - (int)(size.Y / 2)), null, Color.White, 0f, Vector2.Zero, glyphScale, SpriteEffects.None, 0f);
    }

    private void Add(GraphicsDevice device, string moveId, string[] fill, Color a = default, Color b = default, Color c = default)
    {
        var palette = new Dictionary<char, Color> { ['w'] = Paper };
        foreach (var (key, color) in new[] { ('a', a), ('b', b), ('c', c) })
        {
            if (color != default)
                palette[key] = color;
        }

        _glyphs[moveId] = PixelArt.FromPatternOutlined(device, fill, palette, Outline);
    }
}
