using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PokeRogue.Core.Run.Progression;
using PokeRogue.Game.Graphics;
using PokeRogue.Game.Scenes;

namespace PokeRogue.Game.Preview;

/// <summary>
/// Phase 2.5 preview of the held items: every item of content/data/items.json on a card with its icon doubled (as the
/// HUD crops it), its number of levels, its name and what each level adds. New items are marked; items whose icon has a
/// palette swap appear twice, as in the games and recoloured.
/// </summary>
public sealed class ItemsPreviewScene(GameServices services) : Scene(services)
{
    private const int Columns = 6;

    private static readonly Color Background = new(24, 20, 40);
    private static readonly Color Paper = new(244, 240, 228);
    private static readonly Color Ink = new(16, 12, 28);
    private static readonly Color Dim = new(160, 152, 190);
    private static readonly Color Gold = new(248, 208, 72);

    public override void Update()
    {
    }

    public override void Draw(SpriteBatch batch)
    {
        var p = Services.Primitives;
        var small = Services.Fonts.Small(Services.Style);
        var screen = new Rectangle(0, 0, Services.Screen.Width, Services.Screen.Height);

        // Recoloured icons get a second card, so the original and the swap can be compared.
        var cards = new List<(Core.Run.Items.ItemDefinition Item, bool Original)>();
        foreach (var item in Services.Items.Value.All)
        {
            cards.Add((item, ItemIcons.IsRecoloured(item.IconKey)));
            if (ItemIcons.IsRecoloured(item.IconKey))
                cards.Add((item, false));
        }

        batch.Begin(samplerState: SamplerState.PointClamp);
        p.Rect(batch, screen, Background);
        UiDraw.TextOutlined(batch, small, "ANTEPRIMA  -  STRUMENTI", new Vector2(8, 8), Paper, Ink);

        var rows = (cards.Count + Columns - 1) / Columns;
        var area = new Rectangle(6, 22, screen.Width - 12, screen.Height - 26);
        var cardWidth = area.Width / Columns;
        var cardHeight = area.Height / rows;
        for (var i = 0; i < cards.Count; i++)
        {
            var card = new Rectangle(area.X + i % Columns * cardWidth + 2, area.Y + i / Columns * cardHeight + 2, cardWidth - 4, cardHeight - 4);
            DrawCard(batch, card, cards[i].Item, cards[i].Original);
        }

        batch.End();
    }

    private void DrawCard(SpriteBatch batch, Rectangle card, Core.Run.Items.ItemDefinition item, bool original)
    {
        var p = Services.Primitives;
        var small = Services.Fonts.Small(Services.Style);
        var body = Services.Fonts.Body(Services.Style);
        UiDraw.Frame(batch, p, card, Services.Style.Theme);

        // The icon doubled, cropped like the HUD slots, with one mark per level underneath.
        var frame = new Rectangle(card.Center.X - 26, card.Y + 5, 52, 52);
        UiDraw.Frame(batch, p, frame, Services.Style.Theme);
        Services.ItemIcons.Draw(batch, item.IconKey, new Rectangle(frame.X + 2, frame.Y + 2, 48, 48), original);

        var marksWidth = item.MaxLevel * 4 - 2;
        for (var level = 0; level < item.MaxLevel; level++)
            p.Rect(batch, new Rectangle(card.Center.X - marksWidth / 2 + level * 4, frame.Bottom + 3, 2, 2), Gold);

        var tag = ItemIcons.IsRecoloured(item.IconKey) ? (original ? "ORIGINALE" : "RICOLORATO") : item.Enabled ? null : "NUOVO";
        if (tag is not null)
            UiDraw.TextOutlined(batch, small, tag, new Vector2(card.X + 4, card.Y + 4), Gold, Ink);

        UiDraw.TextCentered(batch, body, item.NameIt, card.Center.X, frame.Bottom + 7, Paper, Ink);
        var lines = UiDraw.Wrap(small, LevelUp.Describe(item.PerLevel), card.Width - 8);
        for (var line = 0; line < lines.Count && line < 3; line++)
            UiDraw.TextCentered(batch, small, lines[line], card.Center.X, frame.Bottom + 26 + line * 9, Dim);
    }
}
