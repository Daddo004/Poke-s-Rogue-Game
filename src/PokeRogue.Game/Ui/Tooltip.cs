using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PokeRogue.Game.Graphics;

namespace PokeRogue.Game.Ui;

/// <summary>
/// A small box by the pointer with a name and a few words (phase 8.1): what a move or a held item of the build does,
/// shown while the mouse rests on its slot. It stays inside the screen.
/// </summary>
public static class Tooltip
{
    private const int Width = 170;
    private static readonly Color Ink = new(16, 12, 28);
    private static readonly Color Dim = new(160, 152, 190);

    /// <summary>
    /// The build's tooltip: the move or held item whose slot the mouse rests on, from the regions tagged "mossa" and
    /// "strumento" (their index is the slot).
    /// </summary>
    public static void ForBuild(SpriteBatch batch, GameServices services, Core.Run.RunState run)
    {
        var input = services.Input;
        if (input.Hover("mossa") is { } move && move < run.Player.Moves.Count)
        {
            var owned = run.Player.Moves[move];
            var level = owned.Definition.IsEvolution ? "EVOLUTA" : $"LIVELLO {owned.Level}/{owned.Definition.MaxLevel}";
            Draw(batch, services, owned.Definition.NameIt, $"{level}. {owned.Definition.DescriptionIt}", owned.Definition.IsEvolution ? new Color(248, 208, 72) : Color.White);
        }
        else if (input.Hover("strumento") is { } slot && slot < run.Player.Items.Count)
        {
            var item = run.Player.Items[slot];
            Draw(batch, services, item.Definition.NameIt, $"LIVELLO {item.Level}/{item.Definition.MaxLevel}. {item.Definition.DescriptionIt}", Color.White);
        }
    }

    public static void Draw(SpriteBatch batch, GameServices services, string title, string detail, Color titleColor)
    {
        var body = services.Fonts.Body(services.Style);
        var small = services.Fonts.Small(services.Style);
        var numbers = services.Fonts.Get(services.Style.NumberFont, services.Style.NumberFontSize / 2f);
        var lines = UiDraw.Wrap(small, detail, Width - 14).Take(6).ToList();
        var height = 24 + lines.Count * 10 + 4;
        var at = services.Input.Pointer + new Vector2(12, 10);
        var box = new Rectangle((int)MathF.Min(at.X, services.Screen.Width - Width - 4), (int)MathF.Min(at.Y, services.Screen.Height - height - 4), Width, height);

        services.Primitives.Rect(batch, new Rectangle(box.X + 2, box.Y + 2, box.Width, box.Height), Ink * 0.5f);
        UiDraw.Frame(batch, services.Primitives, box, services.Style.Theme);
        UiDraw.Text(batch, body, title, new Vector2(box.X + 7, box.Y + 3), titleColor);
        for (var i = 0; i < lines.Count; i++)
            UiDraw.TextWithNumbers(batch, small, numbers, lines[i], new Vector2(box.X + 7, box.Y + 24 + i * 10), Dim);
    }
}
