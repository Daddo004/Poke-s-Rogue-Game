using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using PokeRogue.Core.Meta.Cards;
using PokeRogue.Game.Graphics;
using PokeRogue.Game.Ui;

namespace PokeRogue.Game.Scenes.Menus;

/// <summary>The viewer: one owned card large, leaning towards the mouse with its shine, turned over with [F].</summary>
public sealed partial class CardsScene
{
    private static readonly Vector2 ViewCenter = new(196, 182);
    private static readonly Vector2 ViewSize = new(218, 300);

    private CardDefinition? _viewing;
    private CardView? _view;
    private Texture2D? _composed;
    private int _viewAge;

    private void OpenViewer()
    {
        if (Selected is not { } card || !Meta.IsOwned(card))
        {
            if (Selected is { } locked)
                Notice(Meta.IsOpen(locked) ? "NON ANCORA TROVATA: APRI LE BUSTINE DI QUESTA LINEA" : "NON ANCORA DISPONIBILE", Red);
            return;
        }

        Show(card);
    }

    /// <summary>Opens <paramref name="card"/> in the viewer (previews too).</summary>
    public void Show(CardDefinition card)
    {
        _viewing = card;
        _view ??= new CardView(Services.CardArt);
        _view.Reset();
        _viewAge = 0;
        Meta.MarkSeen(card);
        Services.Sounds.Play("menu-conferma");
    }

    /// <summary>TCGdex names TCG Pocket's rarities in French even in Italian: said here as the Italian app does.</summary>
    private static string RarityName(string apiRarity) => (apiRarity switch
    {
        "Une Diamant" => "1 diamante",
        "deux Diamant" => "2 diamanti",
        "Trois Diamant" => "3 diamanti",
        "Quatre Diamant" => "4 diamanti",
        "Une Étoile" => "1 stella",
        "deux Étoiles" => "2 stelle",
        "Trois Étoiles" => "3 stelle",
        "Couronne" => "corona",
        "Un Chromatique" => "1 cromatica",
        "Deux Chromatique" => "2 cromatiche",
        _ => apiRarity,
    }).ToUpperInvariant();

    private void UpdateViewer()
    {
        _viewAge++;
        var input = Services.Input;
        _view!.Update(input.PointerAwake ? input.Pointer : null, ViewCenter, ViewSize, _viewAge);

        if (Back() || ClickedOn("chiudi") is not null)
        {
            _viewing = null;
            return;
        }

        if (Pressed(Keys.F, Buttons.X) || ClickedOn("gira") is not null)
            _view.Flip();

        var owned = Set.Where(Meta.IsOwned).ToList();
        var index = owned.IndexOf(_viewing!);
        var step = (Left() || ClickedOn("prima") is not null ? -1 : 0) + (Right() || ClickedOn("dopo") is not null ? 1 : 0);
        if (step != 0 && owned.Count > 1)
        {
            var next = owned[(index + step + owned.Count) % owned.Count];
            _cursor = Set.ToList().IndexOf(next);
            Show(next);
        }
    }

    private void BeforeDrawViewer(SpriteBatch batch)
    {
        var art = Services.CardArt;
        art.Back(batch);
        art.LoadFaces([_viewing!], 1);
        _composed = art.Face(_viewing!) is { } face ? _view!.Compose(batch, face, _viewing!.Rarity, _viewAge) : null;
    }

    private void DrawViewer(SpriteBatch batch)
    {
        var p = Services.Primitives;
        var card = _viewing!;
        var art = Services.CardArt;

        // A spotlight behind the card.
        batch.End();
        batch.BeginVirtual(samplerState: SamplerState.LinearClamp);
        var light = card.Rarity == CardRarity.Crown ? new Color(255, 210, 120) : TypePalette.Of(card.Species.Types[0]);
        batch.Draw(art.Glow, new Rectangle((int)ViewCenter.X - 220, (int)ViewCenter.Y - 200, 440, 400), light * 0.35f);
        batch.End();

        var back = art.Back(batch);
        if (_composed is not null)
            _view!.Draw(batch, _composed, back, ViewCenter, ViewSize);
        else
            _view!.Draw(batch, back, back, ViewCenter, ViewSize);

        batch.BeginVirtual(samplerState: SamplerState.PointClamp);
        Clickable(new Rectangle((int)(ViewCenter.X - ViewSize.X / 2), (int)(ViewCenter.Y - ViewSize.Y / 2), (int)ViewSize.X, (int)ViewSize.Y), "gira");
        if (_composed is null)
            UiDraw.TextCentered(batch, Small, art.Unavailable(card) ? "IMMAGINE NON DISPONIBILE: SERVE LA RETE" : "SCARICAMENTO DALL'API...",
                ViewCenter.X, ViewCenter.Y + ViewSize.Y / 2 - 20, Paper, Ink);

        // What the card is: its name, rarity, expansion and illustrator as the API gives them; then the line's records.
        var panel = new Rectangle(352, 36, 272, 300);
        UiDraw.Frame(batch, p, panel, Services.Style.Theme);
        var x = panel.X + 12;
        UiDraw.Text(batch, Services.Fonts.Title(Services.Style), card.Tcg.Name, new Vector2(x, panel.Y + 6), Paper);
        var symbol = art.Symbol(card.Rarity);
        for (var i = 0; i < CardArt.SymbolCount(card.Rarity); i++)
            batch.Draw(symbol, new Rectangle(x + i * 10, panel.Y + 44, 8, 8 * symbol.Height / symbol.Width), card.Rarity >= CardRarity.FullArt ? Gold : Paper);
        UiDraw.Text(batch, Small, $"{card.Spec.NameIt.ToUpperInvariant()} ({RarityName(card.Tcg.Rarity)})",
            new Vector2(x + CardArt.SymbolCount(card.Rarity) * 10 + 4, panel.Y + 44), Gold);
        UiDraw.TextWithNumbers(batch, Small, SmallNumbers, $"ESPANSIONE {card.Tcg.Set.ToUpperInvariant()}", new Vector2(x, panel.Y + 58), Paper);
        if (card.Tcg.Illustrator is { Length: > 0 } illustrator)
            UiDraw.Text(batch, Small, $"ILLUSTRAZIONE DI {illustrator.ToUpperInvariant()}", new Vector2(x, panel.Y + 70), Dim);
        UiDraw.TextWithNumbers(batch, Small, SmallNumbers, $"CARTA N. {card.Number:D3} DI {Services.Cards.All.Count}   DA API TCGDEX", new Vector2(x, panel.Y + 82), Dim);

        var progress = Meta.CardLine(card.Line);
        var level = Services.Cards.BondLevel(progress.Bond);
        UiDraw.Text(batch, Body, $"Legame con {card.Line.NameIt}", new Vector2(x, panel.Y + 98), Paper);
        var rows = new (string Label, string Value)[]
        {
            ("LIVELLO DI LEGAME", level.ToString()),
            ("PARTITE", UiFormat.Number(progress.Runs)),
            ("POKÉMON SCONFITTI", UiFormat.Number((int)Math.Min(int.MaxValue, progress.Kills))),
            ("LIVELLO MASSIMO", progress.BestLevel.ToString()),
            ("PARTITA PIÙ LUNGA", UiFormat.Clock(progress.BestTicks)),
            ("VITTORIE", progress.Won.Count == 0 ? "NESSUNA" : string.Join(" ", progress.Won.Order().Select(d => d switch
            {
                Core.Meta.Difficulty.Normal => "NORMALE",
                Core.Meta.Difficulty.Hard => "DIFFICILE",
                _ => "ULTRA",
            }))),
        };
        for (var i = 0; i < rows.Length; i++)
        {
            var y = panel.Y + 120 + i * 14;
            p.Rect(batch, new Rectangle(x - 4, y - 2, panel.Width - 16, 12), Color.White * (i % 2 == 0 ? 0.04f : 0.02f));
            UiDraw.Text(batch, Small, rows[i].Label, new Vector2(x, y), Dim);
            var value = rows[i].Value;
            UiDraw.TextWithNumbers(batch, Small, SmallNumbers, value, new Vector2(panel.Right - 14 - UiDraw.Measure(Small, value).X, y), Paper);
        }

        var have = Set.Count(Meta.IsOwned);
        UiDraw.TextWithNumbers(batch, Small, SmallNumbers, $"CARTE DELLA LINEA {have}/{Set.Count}", new Vector2(x, panel.Y + 214), Gold);
        DrawWrapped(batch, _view!.ShowsBack ? "Il dorso, uguale per tutte le carte." : "Muovi il mouse sulla carta per inclinarla: la luce e i riflessi la seguono.",
            x, panel.Y + 230, panel.Width - 24, 3, Dim);

        DrawFooter(batch, "[MOUSE] INCLINA     [F] GIRA     [FRECCE] ALTRA CARTA     [ESC] ALBUM");
    }
}
