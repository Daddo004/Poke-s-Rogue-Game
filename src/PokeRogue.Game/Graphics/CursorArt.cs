using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace PokeRogue.Game.Graphics;

/// <summary>
/// The mouse pointer (phase 8.1), drawn by the game in its own pixels so it is as crisp as the rest of the screen:
/// an arrow, and a pointing glove over something that can be clicked.
/// </summary>
public sealed class CursorArt(GraphicsDevice device)
{
    private static readonly Dictionary<char, Color> Palette = new()
    {
        ['k'] = new Color(16, 12, 28),
        ['w'] = new Color(248, 246, 236),
        ['s'] = new Color(184, 176, 214),
    };

    public Texture2D Arrow { get; } = PixelArt.FromPattern(device,
    [
        "k.........",
        "kk........",
        "kwk.......",
        "kwwk......",
        "kwwwk.....",
        "kwwwwk....",
        "kwwwwwk...",
        "kwwwwwwk..",
        "kwwwwwssk.",
        "kwwwwsskkk",
        "kwwkwsk...",
        "kwk.kssk..",
        "kk..kssk..",
        "k....kssk.",
        ".....kkk..",
    ], Palette);

    /// <summary>The glove's fingertip, where it points.</summary>
    public static readonly Point HandTip = new(5, 0);

    public Texture2D Hand { get; } = PixelArt.FromPattern(device,
    [
        "....kk.......",
        "...kwwk......",
        "...kwwk......",
        "...kwwk......",
        "...kwwkkk.kk.",
        "...kwwkwwkwwk",
        "kk.kwwkwwkwwk",
        "kwkkwwwwwwwsk",
        "kwwkwwwwwwwsk",
        ".kwwwwwwwwwsk",
        "..kwwwwwwwssk",
        "...kwwwwwssk.",
        "....kkkkkkk..",
    ], Palette);

    public void Draw(SpriteBatch batch, Vector2 at, bool hand)
    {
        var position = new Vector2(MathF.Round(at.X), MathF.Round(at.Y));
        if (hand)
            batch.Draw(Hand, position - HandTip.ToVector2(), Color.White);
        else
            batch.Draw(Arrow, position, Color.White);
    }
}
