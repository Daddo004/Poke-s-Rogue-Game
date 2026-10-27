using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using PokeRogue.Core.Meta.Cards;
using PokeRogue.Core.Run.Characters;
using PokeRogue.Game.Graphics;
using PokeRogue.Game.Ui;

namespace PokeRogue.Game.Scenes.Menus;

/// <summary>
/// The collectible cards (phase 10): the album with every line on the left and the chosen line's set on the right, its
/// bond and its packs; a card opens in the viewer (<see cref="CardsScene"/>.Viewer), a pack in the opening
/// (<see cref="CardsScene"/>.Pack).
/// </summary>
public sealed partial class CardsScene : MenuScene
{
    private const int ListRows = 13;
    private const int ListRow = 22;
    private const int GridColumns = 8;
    private const int GridRows = 3;
    private const int ThumbWidth = 46;
    private const int ThumbHeight = 64;
    private const int PitchX = 52;
    private const int PitchY = 70;

    private static readonly Color Locked = new(30, 26, 46);
    private static readonly Color Open = new(52, 46, 80);

    private readonly IReadOnlyList<CharacterDefinition> _lines;
    private int _line;
    private int _listScroll;
    private int _cursor;
    private int _gridScroll;
    private bool _inGrid;

    public CardsScene(GameServices services, string? lineId = null) : base(services)
    {
        _lines = services.Characters.All;
        var start = lineId ?? _lines.FirstOrDefault(l => Meta.CardLine(l).Packs > 0)?.Id ?? Meta.Data.LastCharacter;
        _line = Math.Max(0, _lines.ToList().FindIndex(l => l.Id == start));
        _listScroll = Math.Clamp(_line - ListRows / 2, 0, Math.Max(0, _lines.Count - ListRows));
    }

    private CharacterDefinition Line => _lines[_line];

    private IReadOnlyList<CardDefinition> Set => Services.Cards.Of(Line);

    private CardDefinition? Selected => _inGrid && _cursor < Set.Count ? Set[_cursor] : null;

    protected override void UpdateMenu()
    {
        if (_viewing is not null)
        {
            UpdateViewer();
            return;
        }

        if (_pack is not null)
        {
            UpdatePack();
            return;
        }

        if (Back())
        {
            if (_inGrid)
                _inGrid = false;
            else
                Services.Switch(new MainMenuScene(Services, cursor: MainMenuScene.CardsRow));
            return;
        }

        if (Pressed(Keys.B, Buttons.Y) || ClickedOn("bustina") is not null)
        {
            StartPack();
            return;
        }

        if (Pressed(Keys.Q, Buttons.LeftShoulder))
            ChooseLine(_line - 1);
        if (Pressed(Keys.E, Buttons.RightShoulder))
            ChooseLine(_line + 1);
        if ((Pointed("linea") ?? ClickedOn("linea")) is { } line)
        {
            ChooseLine(line);
            _inGrid = false;
        }

        if (Pointed("carta") is { } pointed)
        {
            _inGrid = true;
            _cursor = pointed;
        }

        if (ClickedOn("carta") is { } clicked)
        {
            _inGrid = true;
            _cursor = clicked;
            OpenViewer();
            return;
        }

        if (!_inGrid)
        {
            if (Up())
                ChooseLine(_line - 1);
            if (Down())
                ChooseLine(_line + 1);
            if (Right() || Confirm())
            {
                _inGrid = true;
                _cursor = Math.Min(_cursor, Set.Count - 1);
            }

            return;
        }

        var move = (Right() ? 1 : 0) + (Up() ? -GridColumns : 0) + (Down() ? GridColumns : 0);
        if (Left())
        {
            if (_cursor % GridColumns == 0)
                _inGrid = false;
            else
                move--;
        }

        if (move != 0 && _cursor + move >= 0 && _cursor + move < Set.Count)
            _cursor += move;
        if (_cursor / GridColumns < _gridScroll)
            _gridScroll = _cursor / GridColumns;
        if (_cursor / GridColumns >= _gridScroll + GridRows)
            _gridScroll = _cursor / GridColumns - GridRows + 1;

        if (Confirm())
            OpenViewer();
    }

    /// <summary>Previews: the cursor on card <paramref name="cursor"/> of the set.</summary>
    public void Focus(int cursor)
    {
        _inGrid = true;
        _cursor = cursor;
        _gridScroll = Math.Max(0, cursor / GridColumns - GridRows + 1);
    }

    private void ChooseLine(int index)
    {
        if (index < 0 || index >= _lines.Count || index == _line)
            return;
        _line = index;
        _cursor = 0;
        _gridScroll = 0;
        if (_line < _listScroll)
            _listScroll = _line;
        if (_line >= _listScroll + ListRows)
            _listScroll = _line - ListRows + 1;
    }

    protected override void BeforeDraw(SpriteBatch batch)
    {
        if (_viewing is not null)
        {
            BeforeDrawViewer(batch);
            return;
        }

        if (_pack is not null)
        {
            BeforeDrawPack(batch);
            return;
        }

        // The faces on show, a few per frame (downloaded from the API first if they are not on disk).
        var visible = Set.Skip(_gridScroll * GridColumns).Take(GridColumns * GridRows).Where(Meta.IsOwned);
        Services.CardArt.LoadFaces(visible);
    }

    protected override void DrawMenu(SpriteBatch batch)
    {
        if (_viewing is not null)
        {
            DrawViewer(batch);
            return;
        }

        if (_pack is not null)
        {
            DrawPack(batch);
            return;
        }

        DrawHeader(batch, "CARTE");
        var owned = Meta.Data.Cards.Count;
        var summary = $"COLLEZIONE {owned}/{Services.Cards.All.Count}     BUSTINE DA APRIRE {Meta.PacksWaiting}";
        UiDraw.TextWithNumbers(batch, Small, SmallNumbers, summary, new Vector2(110, 14), Dim);
        DrawList(batch);
        DrawSet(batch);
        DrawFooter(batch, _inGrid
            ? "[FRECCE] SCEGLI     [INVIO] AMMIRA     [B] APRI BUSTINA     [ESC] LINEE"
            : "[SU/GIÙ] LINEA     [INVIO] CARTE     [B] APRI BUSTINA     [ESC] INDIETRO");
    }

    // ---- The lines ----

    private void DrawList(SpriteBatch batch)
    {
        var p = Services.Primitives;
        var panel = new Rectangle(16, 36, 170, 300);
        UiDraw.Frame(batch, p, panel, Services.Style.Theme);
        for (var row = 0; row < ListRows && _listScroll + row < _lines.Count; row++)
        {
            var index = _listScroll + row;
            var line = _lines[index];
            var box = new Rectangle(panel.X + 4, panel.Y + 4 + row * ListRow, panel.Width - 12, ListRow - 2);
            var progress = Meta.CardLine(line);
            var set = Services.Cards.Of(line);
            var have = set.Count(Meta.IsOwned);
            Clickable(box, "linea", index);
            p.Rect(batch, box, index == _line ? new Color(60, 48, 24) : have > 0 ? new Color(40, 34, 64) : new Color(28, 24, 44));
            if (Services.Sprites.Portrait(line.First.Id) is { } portrait)
                batch.Draw(portrait, new Rectangle(box.X + 1, box.Y + 1, 18, 18), progress.Runs > 0 || have > 0 ? Color.White : new Color(70, 64, 104));
            UiDraw.Text(batch, Small, line.NameIt.ToUpperInvariant(), new Vector2(box.X + 23, box.Y + 3), index == _line ? Gold : have > 0 ? Paper : Dim);
            var count = $"{have}/{set.Count}";
            UiDraw.Text(batch, SmallNumbers, count, new Vector2(box.X + 23, box.Y + 10), have == set.Count ? Gold : Dim);

            if (progress.Packs > 0)
                DrawPackBadge(batch, new Rectangle(box.Right - 22, box.Y + 4, 18, 12), progress.Packs);
            if (index == _line && !_inGrid)
                DrawCursor(batch, box);
            else
                DrawHover(batch, box);
        }

        // Scroll bar.
        var track = new Rectangle(panel.Right - 5, panel.Y + 4, 2, ListRows * ListRow - 2);
        p.Rect(batch, track, Color.White * 0.08f);
        var thumb = Math.Max(8, track.Height * ListRows / Math.Max(ListRows, _lines.Count));
        p.Rect(batch, new Rectangle(track.X, track.Y + (track.Height - thumb) * _listScroll / Math.Max(1, _lines.Count - ListRows), 2, thumb), Dim);
    }

    /// <summary>A small red pack with the number waiting.</summary>
    private void DrawPackBadge(SpriteBatch batch, Rectangle r, int count)
    {
        var p = Services.Primitives;
        p.Rect(batch, r, Age / 25 % 2 == 0 ? new Color(200, 60, 50) : new Color(230, 90, 70));
        p.Rect(batch, new Rectangle(r.X, r.Y, r.Width, 2), new Color(240, 200, 190));
        UiDraw.TextCentered(batch, SmallNumbers, count.ToString(), r.Center.X, r.Y + 1, Paper);
    }

    // ---- The set ----

    private void DrawSet(SpriteBatch batch)
    {
        var p = Services.Primitives;
        var panel = new Rectangle(192, 36, 432, 300);
        UiDraw.Frame(batch, p, panel, Services.Style.Theme);
        var line = Line;
        var progress = Meta.CardLine(line);
        var set = Set;

        UiDraw.Text(batch, Body, line.NameIt, new Vector2(panel.X + 8, panel.Y + 4), Paper);
        var types = line.First.Types;
        for (var i = 0; i < types.Count; i++)
            DrawTypePill(batch, new Rectangle(panel.X + 10 + (int)UiDraw.Measure(Body, line.NameIt).X + 6 + i * 52, panel.Y + 9, 48, 11), types[i]);

        // The bond: level, bar to the next.
        var catalog = Services.Cards;
        var level = catalog.BondLevel(progress.Bond);
        var from = catalog.BondPoints(level);
        var to = catalog.BondPoints(level + 1);
        UiDraw.TextWithNumbers(batch, Small, SmallNumbers, $"LEGAME LV {level}", new Vector2(panel.X + 8, panel.Y + 24), Gold);
        var bar = new Rectangle(panel.X + 72, panel.Y + 25, 110, 6);
        UiDraw.Bar(batch, p, bar, (progress.Bond - from) / (float)Math.Max(1, to - from), Gold, new Color(40, 32, 20), Ink);
        UiDraw.Text(batch, SmallNumbers, $"{progress.Bond}/{to}", new Vector2(bar.Right + 6, panel.Y + 21), Dim);

        // The pack button.
        var button = new Rectangle(panel.Right - 120, panel.Y + 6, 112, 26);
        var ready = progress.Packs > 0;
        Clickable(button, "bustina");
        UiDraw.Frame(batch, p, button, ready ? new UiTheme(new Color(120, 36, 30, 240), new Color(255, 180, 160), new Color(200, 80, 60), Ink, Paper, Dim) : Services.Style.Theme);
        UiDraw.TextCentered(batch, Small, ready ? $"APRI BUSTINA ({progress.Packs})" : "NESSUNA BUSTINA", button.Center.X, button.Y + 5, ready ? Paper : Dim);
        UiDraw.TextCentered(batch, Small, "[B]", button.Center.X, button.Y + 15, ready ? Gold : Dim);
        if (ready)
            DrawHover(batch, button);

        // The cards, faces drawn smooth.
        batch.End();
        batch.BeginVirtual(samplerState: SamplerState.LinearClamp);
        var art = Services.CardArt;
        var first = _gridScroll * GridColumns;
        for (var i = first; i < Math.Min(set.Count, first + GridColumns * GridRows); i++)
        {
            var card = set[i];
            var slot = i - first;
            var cell = new Rectangle(panel.X + 9 + slot % GridColumns * PitchX, panel.Y + 40 + slot / GridColumns * PitchY, ThumbWidth, ThumbHeight);
            Clickable(cell, "carta", i);
            if (Meta.IsOwned(card) && art.Face(card) is { } face)
                batch.Draw(face, cell, Color.White);
            else
                batch.Draw(art.Rounded(1), cell, Meta.IsOpen(card) ? Open : Locked);
        }

        batch.End();
        batch.BeginVirtual(samplerState: SamplerState.PointClamp);
        for (var i = first; i < Math.Min(set.Count, first + GridColumns * GridRows); i++)
        {
            var slot = i - first;
            var cell = new Rectangle(panel.X + 9 + slot % GridColumns * PitchX, panel.Y + 40 + slot / GridColumns * PitchY, ThumbWidth, ThumbHeight);
            if (!Meta.IsOwned(set[i]) || art.Face(set[i]) is null)
                DrawMissing(batch, set[i], cell);
            if (Meta.IsNew(set[i]))
            {
                var tag = new Rectangle(cell.Right - 26, cell.Y - 3, 28, 9);
                p.Rect(batch, tag, Gold);
                UiDraw.TextCentered(batch, Small, "NUOVA", tag.Center.X, tag.Y + 1, Ink);
            }

            if (_inGrid && i == _cursor)
                DrawCursor(batch, cell);
            else
                DrawHover(batch, cell);
        }

        if (set.Count > GridColumns * GridRows)
        {
            var rows = (set.Count + GridColumns - 1) / GridColumns;
            UiDraw.TextWithNumbers(batch, Small, SmallNumbers, $"RIGA {_gridScroll + 1}-{Math.Min(rows, _gridScroll + GridRows)} DI {rows}",
                new Vector2(panel.Right - 90, panel.Y + 252), Dim);
        }

        DrawSelectedInfo(batch, new Rectangle(panel.X + 8, panel.Y + 262, panel.Width - 16, 32), set.Count(Meta.IsOwned), set.Count);
    }

    /// <summary>On a card not owned (its dark shape is drawn already): its rarity mark, and whether the packs can give it yet.</summary>
    private void DrawMissing(SpriteBatch batch, CardDefinition card, Rectangle cell)
    {
        var art = Services.CardArt;
        var open = Meta.IsOpen(card);
        if (Meta.IsOwned(card))
        {
            UiDraw.TextCentered(batch, Small, art.Unavailable(card) ? "OFFLINE" : "...", cell.Center.X, cell.Center.Y - 4, Dim);
            return;
        }

        var symbol = art.Symbol(card.Rarity);
        var count = CardArt.SymbolCount(card.Rarity);
        var width = count * 6 - 1;
        for (var i = 0; i < count; i++)
            batch.Draw(symbol, new Rectangle(cell.Center.X - width / 2 + i * 6, cell.Center.Y - 10, 5, 5 * symbol.Height / symbol.Width), open ? Gold * 0.8f : Dim * 0.5f);
        UiDraw.TextCentered(batch, Small, open ? "IN BUSTINA" : "???", cell.Center.X, cell.Center.Y + 2, open ? Gold * 0.8f : Dim * 0.5f);
        if (Meta.CardLine(card.Line).Reached.Contains(card.Species.Key) && Services.Sprites.Portrait(card.Species.Id) is { } portrait)
            batch.Draw(portrait, new Rectangle(cell.Center.X - 8, cell.Bottom - 20, 16, 16), Color.White * 0.35f);
    }

    private void DrawSelectedInfo(SpriteBatch batch, Rectangle area, int owned, int total)
    {
        Services.Primitives.Rect(batch, area, Color.White * 0.04f);
        if (Selected is not { } card)
        {
            UiDraw.TextWithNumbers(batch, Small, SmallNumbers, $"CARTE DI QUESTA LINEA {owned}/{total}", new Vector2(area.X + 6, area.Y + 6), Paper);
            DrawWrapped(batch, "Gioca con questa linea per far crescere il Legame: ogni livello, ogni nuovo stadio e la prima vittoria a ogni difficoltà danno una bustina.",
                area.X + 6, area.Y + 16, area.Width - 12, 2, Dim, 8);
            return;
        }

        // A card not owned keeps its secret: only the species and the rarity show.
        var mine = Meta.IsOwned(card);
        var name = mine ? $"{card.Tcg.Name} · {card.Tcg.Set}" : card.Species.NameIt;
        UiDraw.Text(batch, Small, $"{name.ToUpperInvariant()}   {card.Spec.NameIt.ToUpperInvariant()}", new Vector2(area.X + 6, area.Y + 5), mine ? Gold : Paper);
        UiDraw.Text(batch, SmallNumbers, $"N. {card.Number:D3}", new Vector2(area.Right - 44, area.Y + 2), Dim);
        var status = mine ? "Nella tua collezione: [INVIO] per ammirarla."
            : Meta.IsOpen(card) ? "Può uscire dalle bustine di questa linea."
            : card.Spec.HintIt;
        DrawWrapped(batch, status, area.X + 6, area.Y + 17, area.Width - 12, 2, Meta.IsOwned(card) || Meta.IsOpen(card) ? Paper : Dim, 8);
    }
}
