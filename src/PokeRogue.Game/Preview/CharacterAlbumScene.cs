using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PokeRogue.Core.Run.Characters;
using PokeRogue.Game.Graphics;
using PokeRogue.Game.Scenes;

namespace PokeRogue.Game.Preview;

/// <summary>
/// Phase 4.5: the album of every playable character, nine to a page (--pagina N): portrait, name and types, the
/// portraits of its evolutions, ability, main move, special move and how it is unlocked.
/// </summary>
public sealed class CharacterAlbumScene(GameServices services, int page) : Scene(services)
{
    public const int PerPage = 9;

    private static readonly Color Ink = new(16, 12, 28);
    private static readonly Color Paper = new(244, 240, 228);
    private static readonly Color Dim = new(160, 152, 190);
    private static readonly Color Gold = new(248, 208, 72);

    public override void Update()
    {
    }

    public override void Draw(SpriteBatch batch)
    {
        var all = Services.Characters.All;
        var pages = (all.Count + PerPage - 1) / PerPage;
        var index = Math.Clamp(page, 1, pages);
        Services.Device.Clear(new Color(24, 20, 36));
        batch.BeginVirtual(samplerState: SamplerState.PointClamp);
        UiDraw.TextOutlined(batch, Services.Fonts.Body(Services.Style), $"PERSONAGGI  {index}/{pages}", new Vector2(10, 7), Gold, Ink);
        UiDraw.Text(batch, Services.Fonts.Small(Services.Style), $"{all.Count} LINEE EVOLUTIVE", new Vector2(540, 11), Dim);

        var characters = all.Skip((index - 1) * PerPage).Take(PerPage).ToList();
        for (var i = 0; i < characters.Count; i++)
            DrawCard(batch, characters[i], new Rectangle(10 + i % 3 * 208, 30 + i / 3 * 109, 200, 104));

        batch.End();
    }

    private void DrawCard(SpriteBatch batch, CharacterDefinition character, Rectangle card)
    {
        var p = Services.Primitives;
        var body = Services.Fonts.Body(Services.Style);
        var small = Services.Fonts.Small(Services.Style);
        var legendary = character.Group is CharacterGroup.Legendary or CharacterGroup.Mythical;
        UiDraw.Frame(batch, p, card, legendary ? UiTheme.Dark with { Border = Gold, Highlight = new Color(150, 116, 48) } : UiTheme.Dark);

        var portraitBox = new Rectangle(card.X + 5, card.Y + 5, 42, 42);
        p.Rect(batch, portraitBox, Ink);
        if (Services.Sprites.Portrait(character.First.Id) is { } portrait)
            batch.Draw(portrait, new Rectangle(portraitBox.X + 1, portraitBox.Y + 1, 40, 40), Color.White);

        UiDraw.Text(batch, body, character.NameIt, new Vector2(card.X + 52, card.Y + 3), Paper);
        var types = character.First.Types;
        for (var t = 0; t < types.Count; t++)
            UiDraw.Pill(batch, p, small, new Rectangle(card.X + 52 + t * 40, card.Y + 22, 38, 10), TypePalette.Of(types[t]), TypePalette.NameIt(types[t]).ToUpperInvariant());

        // The rest of the line, small portraits in order; Eevee's seven fit in one row.
        for (var s = 1; s < character.Stages.Count; s++)
        {
            var box = new Rectangle(card.X + 52 + (s - 1) * 20, card.Y + 36, 18, 18);
            p.Rect(batch, box, Ink);
            if (Services.Sprites.Portrait(character.Stages[s].Species.Id) is { } stage)
                batch.Draw(stage, new Rectangle(box.X + 1, box.Y + 1, 16, 16), Color.White);
        }

        if (character.Stages.Count == 1)
            UiDraw.Text(batch, small, "NON SI EVOLVE", new Vector2(card.X + 52, card.Y + 41), Dim);

        var ability = character.Ability;
        UiDraw.Text(batch, small, $"ABILITÀ  {ability.NameIt.ToUpperInvariant()}{(ability.Active ? " (ATTIVA)" : "")}", new Vector2(card.X + 6, card.Y + 58), Paper);

        var move = character.MainMove;
        Services.MoveIcons.Draw(batch, p, new Rectangle(card.X + 5, card.Y + 69, 14, 14), move.Id, move.Type);
        UiDraw.Text(batch, small, move.NameIt.ToUpperInvariant(), new Vector2(card.X + 23, card.Y + 72), Paper);
        var special = Services.Specials.Value[character.Special];
        var specialText = special.NameIt.ToUpperInvariant();
        UiDraw.Text(batch, small, specialText, new Vector2(card.Right - 6 - UiDraw.Measure(small, specialText).X, card.Y + 72), TypePalette.Of(special.Type));

        var unlock = character.Unlock.Kind switch
        {
            UnlockKind.Start => "SUBITO",
            UnlockKind.Shop => $"NEGOZIO  {character.Unlock.Price}",
            UnlockKind.Fossil => $"FOSSILE  {character.Unlock.Price}",
            UnlockKind.Egg => "UOVO",
            UnlockKind.Mission => "MISSIONE",
            _ => "SCONFIGGILO",
        };
        UiDraw.Text(batch, small, unlock, new Vector2(card.X + 6, card.Y + 89), Dim);
    }
}
