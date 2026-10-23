using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PokeRogue.Core.Data;
using PokeRogue.Core.Run.Moves;
using PokeRogue.Game.Graphics;
using PokeRogue.Game.Scenes;

namespace PokeRogue.Game.Preview;

/// <summary>
/// Phase 2.4 preview of move icons: every move of the chosen types ("--tipi Normal,Water"; all when omitted), each on a card
/// with the icon enlarged, at its real HUD and results sizes, and its name and type.
/// </summary>
public sealed class MoveIconsPreviewScene : Scene
{
    private static readonly Color Background = new(24, 20, 40);
    private static readonly Color Paper = new(244, 240, 228);
    private static readonly Color Ink = new(16, 12, 28);
    private static readonly Color Dim = new(160, 152, 190);

    private readonly IReadOnlyList<MoveDefinition> _moves;

    public MoveIconsPreviewScene(GameServices services, string? types) : base(services)
    {
        var wanted = types?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(t => Enum.Parse<PokemonType>(t, ignoreCase: true))
            .ToList();
        var all = services.Moves.Value.All;
        _moves = all.Where(m => wanted is null || wanted.Contains(m.Type))
            .OrderBy(m => wanted?.IndexOf(m.Type) ?? (int)m.Type)
            .ThenBy(m => all.ToList().IndexOf(m))
            .ToList();
    }

    public override void Update()
    {
    }

    public override void Draw(SpriteBatch batch)
    {
        var p = Services.Primitives;
        var small = Services.Fonts.Small(Services.Style);
        var body = Services.Fonts.Body(Services.Style);
        var screen = new Rectangle(0, 0, Services.Screen.Width, Services.Screen.Height);

        batch.Begin(samplerState: SamplerState.PointClamp);
        p.Rect(batch, screen, Background);
        UiDraw.TextOutlined(batch, small, "ANTEPRIMA  -  ICONE DELLE MOSSE", new Vector2(8, 8), Paper, Ink);

        var columns = Math.Max(1, Math.Min(_moves.Count, _moves.Count > 8 ? 6 : 4));
        var rows = (_moves.Count + columns - 1) / columns;
        var area = new Rectangle(8, 24, screen.Width - 16, screen.Height - 32);
        var cardWidth = area.Width / columns;
        var cardHeight = Math.Min(160, area.Height / Math.Max(1, rows));
        var big = cardHeight >= 140 ? 3 : 2;

        for (var i = 0; i < _moves.Count; i++)
        {
            var move = _moves[i];
            var card = new Rectangle(area.X + i % columns * cardWidth + 3, area.Y + i / columns * cardHeight + 3, cardWidth - 6, cardHeight - 6);
            UiDraw.Frame(batch, p, card, Services.Style.Theme);

            // Enlarged, then at the HUD's 20 px and the results' 16 px.
            var tile = 22 * big;
            Services.MoveIcons.Draw(batch, p, new Rectangle(card.Center.X - tile / 2, card.Y + 8, tile, tile), move.Id, move.Type, glyphScale: big);
            var hud = new Rectangle(card.Center.X - 24, card.Y + 14 + tile, 24, 24);
            UiDraw.Frame(batch, p, hud, Services.Style.Theme);
            Services.MoveIcons.Draw(batch, p, new Rectangle(hud.X + 2, hud.Y + 2, 20, 20), move.Id, move.Type);
            var results = new Rectangle(card.Center.X + 4, card.Y + 16 + tile, 20, 20);
            UiDraw.Frame(batch, p, results, Services.Style.Theme);
            Services.MoveIcons.Draw(batch, p, new Rectangle(results.X + 2, results.Y + 2, 16, 16), move.Id, move.Type);

            var nameY = hud.Bottom + 4;
            UiDraw.TextCentered(batch, body, move.NameIt, card.Center.X, nameY, Paper, Ink);
            var status = Services.MoveIcons.HasGlyph(move.Id) ? (move.Enabled ? "" : "  -  NUOVA") : "  -  SENZA ICONA";
            UiDraw.TextCentered(batch, small, TypePalette.NameIt(move.Type).ToUpperInvariant() + status, card.Center.X, nameY + 18, Dim, Ink);
        }

        batch.End();
    }
}
