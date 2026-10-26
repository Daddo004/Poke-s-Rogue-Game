using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using PokeRogue.Core.Meta;
using PokeRogue.Core.Run.Characters;
using PokeRogue.Game.Graphics;
using PokeRogue.Game.Ui;

namespace PokeRogue.Game.Scenes.Menus;

/// <summary>
/// The character select of the art direction (phase 0.6), which is also the Pokémon shop (phase 6.2): the roster on the
/// left, locked ones dark; the chosen one on the right with its line, ability, stats and moves. A locked character is
/// bought here; [F] turns to the upgrades: the secondary move, then the special, and five grades in each stat (the
/// vitamins), refunded with [R].
/// </summary>
public sealed class PokemonSelectScene : MenuScene
{
    private const int Columns = 6;
    private const int VisibleRows = 4;
    private const int Pitch = 48;

    /// <summary>Upgrade rows: the six stats, then the secondary move and the special.</summary>
    private const int UpgradeRows = 8;

    private static readonly Color LockedTint = new(36, 32, 56);

    private readonly IReadOnlyList<CharacterDefinition> _all;
    private readonly MenuPokemon _showcase;
    private int _cursor;
    private int _scroll;
    private bool _upgrading;
    private int _row;

    public PokemonSelectScene(GameServices services, string? characterId = null) : base(services)
    {
        _all = services.Characters.All;
        _showcase = new MenuPokemon(services);
        var id = characterId ?? Meta.Data.LastCharacter ?? CharacterLibrary.DefaultId;
        _cursor = Math.Max(0, _all.ToList().FindIndex(c => c.Id == id));
        Scroll();
    }

    /// <summary>Previews: open on the upgrades of the selected character, at row <paramref name="row"/>.</summary>
    public void OpenUpgrades(int row)
    {
        _upgrading = true;
        _row = row;
    }

    private CharacterDefinition Selected => _all[_cursor];

    protected override void UpdateMenu()
    {
        _showcase.Show(Selected.First.Id, Meta.IsUnlocked(Selected) ? Core.Data.PmdAnimations.Walk : Core.Data.PmdAnimations.Idle);
        _showcase.Update();

        if (_upgrading)
        {
            UpdateUpgrades();
            return;
        }

        if (Left())
            Move(-1);
        if (Right())
            Move(1);
        if (Up())
            Move(-Columns);
        if (Down())
            Move(Columns);

        var character = Selected;
        if (Back())
            Services.Switch(new MainMenuScene(Services));
        else if (Pressed(Keys.F, Buttons.Y))
        {
            if (Meta.IsUnlocked(character))
                OpenUpgrades(0);
            else
                Notice("PRIMA SBLOCCALO", Red);
        }
        else if (Confirm())
            Choose();
    }

    /// <summary>[INVIO] on the grid: on to the trainer with a character of yours, or the offer to buy a locked one (also previews).</summary>
    public void Choose()
    {
        var character = Selected;
        if (Meta.IsUnlocked(character))
            Services.Switch(new TrainerSelectScene(Services, character));
        else if (Meta.Price(character) is { } price)
            AskToBuy(character.NameIt, price, () => Meta.Buy(character), $"{character.NameIt.ToUpperInvariant()} È TUO!");
        else
            Notice("NON È IN VENDITA: GUARDA COME SI SBLOCCA", Dim);
    }

    private void UpdateUpgrades()
    {
        if (Up())
            _row = (_row + UpgradeRows - 1) % UpgradeRows;
        if (Down())
            _row = (_row + 1) % UpgradeRows;

        var character = Selected;
        if (Back() || Pressed(Keys.F, Buttons.Y))
        {
            _upgrading = false;
            return;
        }

        if (Pressed(Keys.R, Buttons.X))
        {
            var spent = Meta.Data.CharacterProgress.GetValueOrDefault(character.Id)?.GradeCoins ?? 0;
            if (spent == 0)
                Notice("NESSUN GRADO DA RIMBORSARE", Dim);
            else
                Ask($"Rimborsare i gradi di {character.NameIt}?", $"TORNANO {UiFormat.Number(spent)} MONETE", () =>
                    Notice($"RIMBORSATE {UiFormat.Number(Meta.RefundGrades(character))} MONETE", Green));
            return;
        }

        if (!Confirm())
            return;

        if (_row < 6)
        {
            var stat = Meta.Shop.Pokemon.Stats[_row];
            if (Meta.GradePrice(character, stat.Stat) is { } price)
                AskToBuy($"un grado di {stat.NameIt}", price, () => Meta.BuyGrade(character, stat.Stat), $"{stat.NameIt.ToUpperInvariant()} AUMENTATO!");
            else
                Notice("GRADI AL MASSIMO", Dim);
        }
        else if (_row == 6)
        {
            if (Meta.HasSecondary(character))
                Notice("GIÀ SBLOCCATA", Dim);
            else
                AskToBuy(character.SecondaryMove.NameIt, Meta.SecondaryPrice(character), () => Meta.BuySecondary(character), "MOSSA SECONDARIA SBLOCCATA!");
        }
        else if (Meta.HasSpecial(character))
            Notice("GIÀ SBLOCCATA", Dim);
        else if (!Meta.HasSecondary(character))
            Notice("PRIMA SBLOCCA LA MOSSA SECONDARIA", Red);
        else
            AskToBuy(Services.Specials.Value[character.Special].NameIt, Meta.SpecialPrice(character), () => Meta.BuySpecial(character), "MOSSA SPECIALE SBLOCCATA!");
    }

    private void Move(int delta)
    {
        var next = _cursor + delta;
        if (next < 0 || next >= _all.Count)
            return;
        _cursor = next;
        Scroll();
    }

    private void Scroll()
    {
        var row = _cursor / Columns;
        if (row < _scroll)
            _scroll = row;
        if (row >= _scroll + VisibleRows)
            _scroll = row - VisibleRows + 1;
    }

    protected override void DrawMenu(SpriteBatch batch)
    {
        DrawHeader(batch, "SCEGLI IL TUO POKÉMON");
        DrawGrid(batch);
        DrawHint(batch, new Rectangle(16, 238, 284, 52));
        DrawRoster(batch, new Rectangle(16, 296, 284, 40));
        DrawDetail(batch, new Rectangle(316, 40, 308, 296));
        DrawFooter(batch, _upgrading
            ? "[SU/GIÙ] SCEGLI     [INVIO] COMPRA     [R] RIMBORSA I GRADI     [F] FINE"
            : Meta.IsUnlocked(Selected)
                ? "[INVIO] SELEZIONA     [F] POTENZIA     [ESC] INDIETRO"
                : "[INVIO] COMPRA     [ESC] INDIETRO");
    }

    private void DrawGrid(SpriteBatch batch)
    {
        var p = Services.Primitives;
        var theme = Services.Style.Theme;
        for (var i = _scroll * Columns; i < Math.Min(_all.Count, (_scroll + VisibleRows) * Columns); i++)
        {
            var character = _all[i];
            var slot = i - _scroll * Columns;
            var cell = new Rectangle(16 + slot % Columns * Pitch, 40 + slot / Columns * Pitch, 44, 44);
            var unlocked = Meta.IsUnlocked(character);
            UiDraw.Frame(batch, p, cell, theme);
            if (Services.Sprites.Portrait(character.First.Id) is { } portrait)
                batch.Draw(portrait, new Rectangle(cell.X + 2, cell.Y + 2, 40, 40), unlocked ? Color.White : LockedTint);

            if (!unlocked && Meta.Price(character) is { } price)
            {
                // For sale: the price on a dark strip along the bottom.
                p.Rect(batch, new Rectangle(cell.X + 2, cell.Bottom - 12, 40, 10), Ink * 0.85f);
                var text = UiFormat.Number(price);
                var width = UiDraw.Measure(SmallNumbers, text).X;
                UiDraw.Text(batch, SmallNumbers, text, new Vector2(cell.Center.X - width / 2f + 4, cell.Bottom - 14), Meta.Coins >= price ? Gold : Dim);
                batch.Draw(Services.Pickups.Coin, new Vector2(cell.Center.X - width / 2f - 6, cell.Bottom - 11), Color.White);
            }
            else if (!unlocked)
            {
                var q = UiDraw.Measure(Body, "?");
                UiDraw.TextOutlined(batch, Body, "?", new Vector2(cell.Center.X - q.X / 2, cell.Center.Y - q.Y / 2), theme.TextDim, Ink);
            }

            if (i == _cursor)
                DrawCursor(batch, cell);
        }

        // Scroll bar beside the grid.
        var rows = (_all.Count + Columns - 1) / Columns;
        var track = new Rectangle(305, 40, 3, VisibleRows * Pitch - 4);
        p.Rect(batch, track, Color.White * 0.08f);
        var thumb = Math.Max(8, track.Height * VisibleRows / rows);
        p.Rect(batch, new Rectangle(track.X, track.Y + (track.Height - thumb) * _scroll / Math.Max(1, rows - VisibleRows), 3, thumb), Dim);
    }

    /// <summary>Under the grid: whether the selected character is yours, and how to get it if not.</summary>
    private void DrawHint(SpriteBatch batch, Rectangle panel)
    {
        var character = Selected;
        UiDraw.Frame(batch, Services.Primitives, panel, Services.Style.Theme);
        var x = panel.X + 8;
        if (Meta.IsUnlocked(character))
        {
            UiDraw.Text(batch, Small, $"SBLOCCATO  {character.NameIt}", new Vector2(x, panel.Y + 6), Green);
            var wins = Meta.Data.CharacterWins.GetValueOrDefault(character.Id);
            var grades = Meta.Shop.Pokemon.Stats.Sum(s => Meta.Grade(character, s.Stat));
            var max = Meta.Shop.Pokemon.Stats.Count * Meta.Shop.Pokemon.MaxGrade;
            UiDraw.TextWithNumbers(batch, Small, SmallNumbers, $"PARTITE VINTE {wins}     GRADI {grades}/{max}", new Vector2(x, panel.Y + 18), Dim);
            UiDraw.Text(batch, Small, "[F] SBLOCCA MOSSE E ALLENA LE STATISTICHE", new Vector2(x, panel.Y + 30), Dim);
            return;
        }

        UiDraw.Text(batch, Small, $"BLOCCATO  {character.NameIt}", new Vector2(x, panel.Y + 6), Paper);
        if (Meta.Price(character) is { } price)
        {
            UiDraw.TextWithNumbers(batch, Small, SmallNumbers, $"ACQUISTALO PER {UiFormat.Number(price)} MONETE", new Vector2(x, panel.Y + 18), Gold);
            if (character.Unlock.HintIt is { Length: > 0 } hint)
                DrawWrapped(batch, hint, x, panel.Y + 30, panel.Width - 16, 2, Dim);
            return;
        }

        if (character.Unlock.Kind == UnlockKind.Mission && Meta.Missions.Unlocking(character.Id) is { } mission)
        {
            var (current, target) = Meta.Progress(mission);
            DrawWrapped(batch, $"MISSIONE: {mission.DescriptionIt}", x, panel.Y + 18, panel.Width - 16, 2, Dim);
            UiDraw.TextWithNumbers(batch, Small, SmallNumbers, $"{Math.Min(current, target)}/{target}", new Vector2(x, panel.Y + 39), Gold);
            return;
        }

        DrawWrapped(batch, character.Unlock.HintIt, x, panel.Y + 18, panel.Width - 16, 3, Dim);
    }

    private void DrawRoster(SpriteBatch batch, Rectangle panel)
    {
        var p = Services.Primitives;
        UiDraw.Frame(batch, p, panel, Services.Style.Theme);
        var unlocked = _all.Count(Meta.IsUnlocked);
        UiDraw.TextWithNumbers(batch, Small, SmallNumbers, $"POKÉMON SBLOCCATI {unlocked}/{_all.Count}", new Vector2(panel.X + 8, panel.Y + 8), Paper);
        UiDraw.Bar(batch, p, new Rectangle(panel.X + 8, panel.Y + 22, panel.Width - 16, 8), unlocked / (float)_all.Count, Gold, new Color(40, 32, 20), Ink);
    }

    private void DrawDetail(SpriteBatch batch, Rectangle panel)
    {
        var p = Services.Primitives;
        var theme = Services.Style.Theme;
        var character = Selected;
        var species = character.First;
        var unlocked = Meta.IsUnlocked(character);
        UiDraw.Frame(batch, p, panel, theme);

        UiDraw.Text(batch, Body, character.NameIt, new Vector2(326, 46), theme.Text);
        UiDraw.TextWithNumbers(batch, Small, SmallNumbers, $"#{species.Id:D3}  {species.GenusIt}", new Vector2(326, 64), theme.TextDim);
        for (var i = 0; i < species.Types.Count; i++)
            DrawTypePill(batch, new Rectangle(326 + i * 52, 76, 48, 11), species.Types[i]);

        // The line, right-aligned; long ones (Eevee) show the first few.
        var line = character.Stages.Take(5).ToList();
        UiDraw.Text(batch, Small, "LINEA", new Vector2(616 - UiDraw.Measure(Small, "LINEA").X, 46), theme.TextDim);
        for (var i = 0; i < line.Count; i++)
        {
            if (Services.Sprites.Portrait(line[i].Species.Id) is { } portrait)
                batch.Draw(portrait, new Rectangle(616 - (line.Count - i) * 26 + 2, 58, 24, 24), i == 0 ? Color.White : new Color(150, 150, 170));
        }

        if (character.Stages.Count > line.Count)
            UiDraw.TextWithNumbers(batch, Small, SmallNumbers, $"+{character.Stages.Count - line.Count}", new Vector2(616 - line.Count * 26 - 14, 66), theme.TextDim);

        // Ability.
        var ability = character.Ability;
        UiDraw.Text(batch, Small, ability.Active ? "ABILITÀ  [SPAZIO]" : "ABILITÀ", new Vector2(326, 94), Gold);
        UiDraw.Text(batch, Small, ability.NameIt.ToUpperInvariant(), new Vector2(326, 105), theme.Text);
        DrawWrapped(batch, ability.DescriptionIt, 326, 116, 172, 4, theme.TextDim);

        // The showcase.
        p.Ellipse(batch, new Vector2(560, 152), 56, 14, Color.White * 0.07f);
        _showcase.Draw(batch, new Vector2(560, 150), 2f, unlocked || Meta.Price(character) is not null ? Color.White : LockedTint);
        if (!unlocked)
            UiDraw.TextCentered(batch, Small, "BLOCCATO", 560, 160, Red, Ink);

        DrawStats(batch, character, unlocked);
        DrawMoves(batch, character, unlocked);
    }

    /// <summary>Base stats as bars, the grades bought as pips and the bonus they give; while upgrading, the price of the next grade.</summary>
    private void DrawStats(SpriteBatch batch, CharacterDefinition character, bool unlocked)
    {
        var p = Services.Primitives;
        var theme = Services.Style.Theme;
        var stats = Meta.Shop.Pokemon.Stats;
        var base_ = character.First.BaseStats;
        int[] values = [base_.Hp, base_.Attack, base_.Defense, base_.SpAttack, base_.SpDefense, base_.Speed];
        string[] labels = ["PS", "ATT", "DIF", "ATT SP", "DIF SP", "VEL"];

        UiDraw.Text(batch, Small, "STATISTICHE", new Vector2(326, 162), Gold);
        UiDraw.Text(batch, Small, "GRADI", new Vector2(494, 162), theme.TextDim);
        for (var i = 0; i < stats.Count; i++)
        {
            var y = 174 + i * 12;
            var selected = _upgrading && _row == i;
            if (selected)
                p.Rect(batch, new Rectangle(322, y - 2, 296, 12), Gold * 0.14f);

            UiDraw.Text(batch, Small, labels[i], new Vector2(326, y), selected ? Paper : theme.TextDim);
            var value = values[i];
            var color = value >= 90 ? new Color(88, 208, 96) : value >= 60 ? new Color(248, 200, 40) : new Color(232, 120, 64);
            UiDraw.Bar(batch, p, new Rectangle(374, y + 1, 92, 6), value / 160f, color, new Color(24, 20, 40), Ink);
            UiDraw.Text(batch, SmallNumbers, value.ToString(), new Vector2(472, y - 2), theme.Text);

            var grade = Meta.Grade(character, stats[i].Stat);
            DrawPips(batch, 495, y + 1, grade, Meta.Shop.Pokemon.MaxGrade);
            if (grade > 0)
                UiDraw.TextWithNumbers(batch, Small, SmallNumbers, BonusText(stats[i], grade), new Vector2(530, y), Green);

            if (selected && unlocked)
            {
                if (Meta.GradePrice(character, stats[i].Stat) is { } price)
                    DrawPrice(batch, price, 614, y);
                else
                    UiDraw.Text(batch, Small, "MAX", new Vector2(614 - UiDraw.Measure(Small, "MAX").X, y), Gold);
            }
        }
    }

    /// <summary>"+8%": what <paramref name="grade"/> grades of a stat give.</summary>
    private static string BonusText(StatGradeSpec stat, int grade)
    {
        var b = stat.PerGrade * grade;
        var value = b.MaxHpPercent + b.PhysicalPowerPercent + b.SpecialPowerPercent + b.PhysicalResistPercent + b.SpecialResistPercent + b.MoveSpeedPercent;
        return $"+{MathF.Round(value * 100f, 1).ToString(System.Globalization.CultureInfo.InvariantCulture).Replace('.', ',')}%";
    }

    /// <summary>The three moves as in the art direction: the main one always yours, the secondary and the special with their price.</summary>
    private void DrawMoves(SpriteBatch batch, CharacterDefinition character, bool unlocked)
    {
        var p = Services.Primitives;
        var theme = Services.Style.Theme;
        var special = Services.Specials.Value[character.Special];
        (string Slot, Core.Data.PokemonType Type, string Name, bool Owned, int? Price, int Row)[] moves =
        [
            ("Principale", character.MainMove.Type, character.MainMove.NameIt, true, null, -1),
            ("Secondaria", character.SecondaryMove.Type, character.SecondaryMove.NameIt, Meta.HasSecondary(character), Meta.SecondaryPrice(character), 6),
            ("Speciale", special.Type, special.NameIt, Meta.HasSpecial(character), Meta.SpecialPrice(character), 7),
        ];

        for (var i = 0; i < moves.Length; i++)
        {
            var (slot, type, name, owned, price, row) = moves[i];
            var y = 250 + i * 28;
            var box = new Rectangle(324, y, 292, 24);
            var selected = _upgrading && _row == row;
            p.Rect(batch, box, selected ? Gold * 0.14f : Color.White * 0.05f);
            if (selected)
                p.Outline(batch, box, Age / 20 % 2 == 0 ? Gold : Color.Lerp(Gold, Color.White, 0.5f), 1);

            UiDraw.Text(batch, Small, slot.ToUpperInvariant(), new Vector2(330, y + 2), owned ? Gold : theme.TextDim);
            DrawTypePill(batch, new Rectangle(330, y + 12, 44, 9), type);
            UiDraw.Text(batch, Body, name, new Vector2(382, y + 4), owned ? theme.Text : theme.TextDim);
            if (owned)
                UiDraw.Text(batch, Small, "SBLOCCATA", new Vector2(608 - UiDraw.Measure(Small, "SBLOCCATA").X, y + 9), Green);
            else if (price is { } cost && unlocked)
                DrawPrice(batch, cost, 608, y + 9);
            else
                UiDraw.Text(batch, Small, "BLOCCATA", new Vector2(608 - UiDraw.Measure(Small, "BLOCCATA").X, y + 9), theme.TextDim);
        }
    }
}
