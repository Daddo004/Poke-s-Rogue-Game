using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PokeRogue.Core.Run.Moves;
using PokeRogue.Game.Graphics;
using PokeRogue.Game.Scenes;
using PokeRogue.Game.Ui;

namespace PokeRogue.Game.Preview;

/// <summary>
/// Phase 2.6 preview of the evolved moves: for each one, the maxed move plus its item, the new icon enlarged and at
/// its HUD size in the gold slot, its name, type and archetype.
/// </summary>
public sealed class EvolutionsPreviewScene(GameServices services) : Scene(services)
{
    private static readonly Color Background = new(24, 20, 40);
    private static readonly Color Paper = new(244, 240, 228);
    private static readonly Color Ink = new(16, 12, 28);
    private static readonly Color Dim = new(160, 152, 190);
    private static readonly Color Gold = new(248, 208, 72);

    private readonly Hud _hud = new(services);

    public override void Update()
    {
    }

    public override void Draw(SpriteBatch batch)
    {
        var p = Services.Primitives;
        var small = Services.Fonts.Small(Services.Style);
        var body = Services.Fonts.Body(Services.Style);
        var moves = Services.Moves.Value;
        var evolutions = moves.All.Where(m => m.IsEvolution).ToList();
        var screen = new Rectangle(0, 0, Services.Screen.Width, Services.Screen.Height);

        batch.BeginVirtual(samplerState: SamplerState.PointClamp);
        p.Rect(batch, screen, Background);
        UiDraw.TextOutlined(batch, small, "ANTEPRIMA 2.6  -  LE MOSSE EVOLUTE: MOSSA AL MASSIMO + STRUMENTO", new Vector2(8, 8), Paper, Ink);

        const int columns = 4;
        var area = new Rectangle(8, 22, screen.Width - 16, screen.Height - 26);
        var cardWidth = area.Width / columns;
        var cardHeight = area.Height / 3;
        for (var i = 0; i < evolutions.Count; i++)
        {
            var evolution = evolutions[i];
            var from = moves[evolution.EvolvesFrom!];
            var item = Services.Items.Value[evolution.EvolveItem!];
            var card = new Rectangle(area.X + i % columns * cardWidth + 2, area.Y + i / columns * cardHeight + 2, cardWidth - 4, cardHeight - 4);
            UiDraw.Frame(batch, p, card, Services.Style.Theme);

            // Recipe: the maxed move + the item, then an arrow to the evolution, enlarged in a gold frame.
            var row = card.Y + 18;
            var baseSlot = new Rectangle(card.X + 6, row, 24, 24);
            UiDraw.Frame(batch, p, baseSlot, Services.Style.Theme);
            Services.MoveIcons.Draw(batch, p, new Rectangle(baseSlot.X + 2, baseSlot.Y + 2, 20, 20), from.Id, from.Type);
            UiDraw.TextCentered(batch, body, "+", baseSlot.Right + 6, row + 3, Gold, Ink);
            var itemSlot = new Rectangle(baseSlot.Right + 12, row, 24, 24);
            UiDraw.Frame(batch, p, itemSlot, Services.Style.Theme);
            Services.ItemIcons.Draw(batch, item.IconKey, new Rectangle(itemSlot.X, itemSlot.Y, ItemIcons.Crop.Width, ItemIcons.Crop.Height));
            Arrow(batch, itemSlot.Right + 4, row + 12);

            var big = new Rectangle(card.Right - 58, card.Y + 6, 50, 50);
            UiDraw.Frame(batch, p, big, _hud.EvolvedTheme);
            Services.MoveIcons.Draw(batch, p, new Rectangle(big.X + 3, big.Y + 3, 44, 44), evolution.Id, evolution.Type, glyphScale: 4);

            // As it sits in the HUD, under the recipe.
            _hud.DrawMoveSlot(batch, new Rectangle(card.X + 6, card.Y + 50, 24, 24), new MoveInstance(evolution));

            UiDraw.TextCentered(batch, body, evolution.NameIt, card.Center.X + 14, card.Y + 58, Gold, Ink);
            UiDraw.TextCentered(batch, small, $"{TypePalette.NameIt(evolution.Type).ToUpperInvariant()}  -  {ArchetypeName(evolution).ToUpperInvariant()}", card.Center.X, card.Y + 80, Dim, Ink);
            UiDraw.TextCentered(batch, small, $"{from.NameIt.ToUpperInvariant()} + {item.NameIt.ToUpperInvariant()}", card.Center.X, card.Y + 92, Dim, Ink);
        }

        batch.End();
    }

    private void Arrow(SpriteBatch batch, int x, int y)
    {
        var p = Services.Primitives;
        p.Rect(batch, new Rectangle(x, y - 1, 8, 3), Gold);
        for (var k = 0; k < 4; k++)
            p.Rect(batch, new Rectangle(x + 8 + k, y - 3 + k, 1, 7 - k * 2), Gold);
    }

    private static string ArchetypeName(MoveDefinition move) => move.Archetype switch
    {
        MoveArchetype.Projectile => "Proiettile",
        MoveArchetype.Homing => "A ricerca",
        MoveArchetype.Boomerang => "Boomerang",
        MoveArchetype.Orbit => move.Permanent ? "Orbita perenne" : "Orbita",
        MoveArchetype.Aura => "Aura",
        MoveArchetype.Slash => "Fendente",
        MoveArchetype.Beam => "Raggio",
        MoveArchetype.Strike => "Dal suolo",
        MoveArchetype.Arc => "Catena",
        MoveArchetype.Zone => move.SpawnAtPlayer ? "Scia" : "Zona",
        MoveArchetype.Ring => "Anello",
        _ => move.Archetype.ToString(),
    };
}
