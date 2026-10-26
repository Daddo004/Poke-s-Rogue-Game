using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using PokeRogue.Core.Meta;
using PokeRogue.Core.Run.Characters;
using PokeRogue.Core.Run.Trainers;
using PokeRogue.Game.Graphics;
using PokeRogue.Game.Ui;
using PokeRogue.Game.Vfx;

namespace PokeRogue.Game.Scenes.Menus;

/// <summary>
/// The trainer select, after the Pokémon (Block 6): the trainers' heads on the left, locked ones dark; the chosen one on
/// the right with passive, active ability, grades and challenge team. [INVIO] plays with an unlocked trainer, [S]
/// challenges one (phase 5.3: beating their team unlocks them), [F] buys a grade (the PowerUps of Vampire Survivors,
/// dearer the more are owned) and [R] refunds that trainer's grades.
/// </summary>
public sealed class TrainerSelectScene : MenuScene
{
    private const int Columns = 6;
    private const int VisibleRows = 4;
    private const int Pitch = 48;

    private static readonly Color LockedTint = new(40, 36, 60);

    private readonly CharacterDefinition _character;
    private readonly IReadOnlyList<TrainerDefinition> _all;
    private int _cursor;
    private int _scroll;

    public TrainerSelectScene(GameServices services, CharacterDefinition character, string? trainerId = null) : base(services)
    {
        _character = character;
        _all = services.Trainers.Value.All;
        var id = trainerId ?? Own.Id;
        _cursor = Math.Max(0, _all.ToList().FindIndex(t => t.Id == id));
        Scroll();
    }

    /// <summary>The trainer you play with in a challenge: the last one played, if still yours, or Ash.</summary>
    private TrainerDefinition Own =>
        Services.Trainers.Value.Find(Meta.Data.LastTrainer ?? "") is { } last && Meta.IsUnlocked(last) ? last : Services.Trainers.Value[TrainerLibrary.DefaultId];

    private TrainerDefinition Selected => _all[_cursor];

    protected override void UpdateMenu()
    {
        if (Left())
            Move(-1);
        if (Right())
            Move(1);
        if (Up())
            Move(-Columns);
        if (Down())
            Move(Columns);

        var trainer = Selected;
        var unlocked = Meta.IsUnlocked(trainer);
        if (Back())
            Services.Switch(new PokemonSelectScene(Services, _character.Id));
        else if (Confirm())
        {
            if (unlocked)
                Services.Switch(MainMenuScene.Run(Services, _character, trainer));
            else if (trainer.Team.Count > 0)
                AskChallenge(trainer);
            else
                Notice("SI SBLOCCA CON UNA MISSIONE", Dim);
        }
        else if (Pressed(Keys.S, Buttons.X))
        {
            if (trainer.Team.Count == 0)
                Notice($"{trainer.NameIt.ToUpperInvariant()} NON HA UNA SQUADRA DA SFIDARE", Dim);
            else
                AskChallenge(trainer);
        }
        else if (Pressed(Keys.F, Buttons.Y))
        {
            if (!unlocked)
                Notice("PRIMA SCONFIGGILO IN UNA SFIDA", Red);
            else if (Meta.TrainerGradePrice(trainer) is { } price)
                AskToBuy($"un grado per {trainer.NameIt}", price, () => Meta.BuyTrainerGrade(trainer), $"{trainer.NameIt.ToUpperInvariant()} È PIÙ FORTE!");
            else
                Notice("GRADI AL MASSIMO", Dim);
        }
        else if (Pressed(Keys.R))
        {
            var spent = Meta.Data.TrainerProgress.GetValueOrDefault(trainer.Id)?.Coins ?? 0;
            if (spent == 0)
                Notice("NESSUN GRADO DA RIMBORSARE", Dim);
            else
                Ask($"Rimborsare i gradi di {trainer.NameIt}?", $"TORNANO {UiFormat.Number(spent)} MONETE",
                    () => Notice($"RIMBORSATE {UiFormat.Number(Meta.RefundTrainer(trainer))} MONETE", Green));
        }
    }

    private void AskChallenge(TrainerDefinition trainer)
    {
        var own = Own;
        Ask($"Sfidare {trainer.NameIt}?", $"CON {_character.NameIt.ToUpperInvariant()} E {own.NameIt.ToUpperInvariant()}: ALLA FINE DEL LIVELLO ARRIVA LA SUA SQUADRA",
            () => Services.Switch(MainMenuScene.Run(Services, _character, own, trainer)));
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
        DrawHeader(batch, "SCEGLI IL TUO ALLENATORE");
        DrawGrid(batch);
        DrawHint(batch, new Rectangle(16, 238, 284, 52));
        DrawMedals(batch, new Rectangle(16, 296, 284, 40));
        DrawDetail(batch, new Rectangle(316, 40, 308, 296));
        DrawFooter(batch, Meta.IsUnlocked(Selected)
            ? "[INVIO] GIOCA     [S] SFIDA     [F] POTENZIA     [R] RIMBORSA     [ESC] INDIETRO"
            : "[INVIO] SFIDA     [ESC] INDIETRO");
    }

    /// <summary>A trainer's head on their type's colour, as on the HUD's card; dark while locked.</summary>
    private void DrawHead(SpriteBatch batch, TrainerDefinition trainer, Rectangle box, bool unlocked)
    {
        var p = Services.Primitives;
        var color = TrainerVfx.ColorOf(trainer);
        p.Rect(batch, box, Color.Lerp(color, Ink, unlocked ? 0.35f : 0.75f));
        p.Rect(batch, new Rectangle(box.X, box.Y + box.Height / 2, box.Width, box.Height - box.Height / 2), Color.Lerp(color, Ink, unlocked ? 0.5f : 0.8f));
        if (Services.TrainerPortraits.Sprite(trainer) is { } sprite)
            batch.Draw(sprite, box, Services.TrainerPortraits.Head(trainer), unlocked ? Color.White : LockedTint);
    }

    private void DrawGrid(SpriteBatch batch)
    {
        var p = Services.Primitives;
        for (var i = _scroll * Columns; i < Math.Min(_all.Count, (_scroll + VisibleRows) * Columns); i++)
        {
            var trainer = _all[i];
            var slot = i - _scroll * Columns;
            var cell = new Rectangle(16 + slot % Columns * Pitch, 40 + slot / Columns * Pitch, 44, 44);
            var unlocked = Meta.IsUnlocked(trainer);
            UiDraw.Frame(batch, p, cell, Services.Style.Theme);
            DrawHead(batch, trainer, new Rectangle(cell.X + 4, cell.Y + 4, 36, 36), unlocked);
            if (!unlocked)
            {
                var q = UiDraw.Measure(Body, "?");
                UiDraw.TextOutlined(batch, Body, "?", new Vector2(cell.Center.X - q.X / 2, cell.Center.Y - q.Y / 2), Dim, Ink);
            }
            else if (Meta.TrainerGrade(trainer) is > 0 and var grade)
            {
                DrawPips(batch, cell.X + 5, cell.Bottom - 7, grade, Meta.Shop.Trainers.MaxGrade, 3);
            }

            if (trainer.Id == Own.Id)
                UiDraw.TextOutlined(batch, Small, "TU", new Vector2(cell.X + 4, cell.Y + 3), Gold, Ink);
            if (i == _cursor)
                DrawCursor(batch, cell);
        }

        var rows = (_all.Count + Columns - 1) / Columns;
        var track = new Rectangle(305, 40, 3, VisibleRows * Pitch - 4);
        p.Rect(batch, track, Color.White * 0.08f);
        var thumb = Math.Max(8, track.Height * VisibleRows / rows);
        p.Rect(batch, new Rectangle(track.X, track.Y + (track.Height - thumb) * _scroll / Math.Max(1, rows - VisibleRows), 3, thumb), Dim);
    }

    private void DrawHint(SpriteBatch batch, Rectangle panel)
    {
        var trainer = Selected;
        UiDraw.Frame(batch, Services.Primitives, panel, Services.Style.Theme);
        var x = panel.X + 8;
        if (Meta.IsUnlocked(trainer))
        {
            UiDraw.Text(batch, Small, $"SBLOCCATO  {trainer.NameIt}", new Vector2(x, panel.Y + 6), Green);
            UiDraw.TextWithNumbers(batch, Small, SmallNumbers, $"GRADO {Meta.TrainerGrade(trainer)}/{Meta.Shop.Trainers.MaxGrade}", new Vector2(x, panel.Y + 18), Dim);
            UiDraw.Text(batch, Small, trainer.Id == Own.Id ? "È IL TUO ALLENATORE NELLE SFIDE" : "[INVIO] PER GIOCARE CON LUI", new Vector2(x, panel.Y + 30), Dim);
            return;
        }

        UiDraw.Text(batch, Small, $"BLOCCATO  {trainer.NameIt}", new Vector2(x, panel.Y + 6), Paper);
        DrawWrapped(batch, trainer.Team.Count > 0
            ? "SCONFIGGI LA SUA SQUADRA IN UNA SFIDA: ARRIVA ALLA FINE DEL LIVELLO, AL POSTO DEL BOSS."
            : "SI SBLOCCA CON UNA MISSIONE.", x, panel.Y + 18, panel.Width - 16, 3, Dim);
    }

    /// <summary>The medals: gym leaders beaten in a challenge. Some of the shop's upgrades need them.</summary>
    private void DrawMedals(SpriteBatch batch, Rectangle panel)
    {
        var p = Services.Primitives;
        UiDraw.Frame(batch, p, panel, Services.Style.Theme);
        UiDraw.TextWithNumbers(batch, Small, SmallNumbers, $"MEDAGLIE {Meta.Medals}", new Vector2(panel.X + 8, panel.Y + 7), Paper);
        var leaders = _all.Where(t => t.Role == TrainerRole.GymLeader).ToList();
        for (var i = 0; i < leaders.Count; i++)
        {
            var at = new Rectangle(panel.X + 8 + i * 8, panel.Y + 22, 6, 6);
            var won = Meta.IsDefeated(leaders[i]);
            p.Rect(batch, new Rectangle(at.X - 1, at.Y - 1, 8, 8), Ink);
            p.Rect(batch, at, won ? TrainerVfx.ColorOf(leaders[i]) : new Color(50, 44, 76));
        }
    }

    private void DrawDetail(SpriteBatch batch, Rectangle panel)
    {
        var p = Services.Primitives;
        var theme = Services.Style.Theme;
        var trainer = Selected;
        var unlocked = Meta.IsUnlocked(trainer);
        var upgraded = Meta.Upgraded(trainer);
        UiDraw.Frame(batch, p, panel, theme);

        // The figure on the right, on a field of their colour.
        var field = new Rectangle(panel.Right - 94, panel.Y + 8, 86, 86);
        p.Rect(batch, field, Color.Lerp(TrainerVfx.ColorOf(trainer), Ink, 0.55f));
        if (Services.TrainerPortraits.Sprite(trainer) is { } sprite)
            batch.Draw(sprite, new Rectangle(field.X + 3, field.Y + 3, 80, 80), unlocked ? Color.White : LockedTint);

        UiDraw.Text(batch, Body, trainer.NameIt, new Vector2(326, 46), theme.Text);
        UiDraw.Text(batch, Small, $"{trainer.TitleIt.ToUpperInvariant()} DI {trainer.Region.ToString().ToUpperInvariant()}", new Vector2(326, 64), theme.TextDim);
        if (trainer.Type is { } type)
            DrawTypePill(batch, new Rectangle(326, 76, 48, 11), type);

        // Passive.
        UiDraw.Text(batch, Small, "PASSIVA", new Vector2(326, 96), Gold);
        UiDraw.Text(batch, Small, trainer.Passive.NameIt.ToUpperInvariant(), new Vector2(372, 96), theme.Text);
        DrawWrapped(batch, trainer.Passive.DescriptionIt, 326, 107, 196, 3, theme.TextDim);

        // Active.
        UiDraw.Text(batch, Small, "ATTIVA [Q]", new Vector2(326, 142), Gold);
        UiDraw.Text(batch, Small, trainer.Active.NameIt.ToUpperInvariant(), new Vector2(392, 142), theme.Text);
        DrawWrapped(batch, trainer.Active.DescriptionIt, 326, 153, 288, 3, theme.TextDim);
        var cooldown = MathF.Round(upgraded.Active.Cooldown).ToString(System.Globalization.CultureInfo.InvariantCulture);
        UiDraw.TextWithNumbers(batch, Small, SmallNumbers, $"RICARICA {cooldown} S", new Vector2(326, 186), Meta.TrainerGrade(trainer) > 0 ? Green : theme.Text);

        // Grades.
        var box = new Rectangle(324, 200, 292, 40);
        p.Rect(batch, box, Color.White * 0.05f);
        UiDraw.Text(batch, Small, "GRADI", new Vector2(330, 205), Gold);
        DrawPips(batch, 370, 206, Meta.TrainerGrade(trainer), Meta.Shop.Trainers.MaxGrade, 5);
        var perGrade = (int)MathF.Round(Meta.Shop.Trainers.PassivePerGrade * 100f);
        var cut = (int)MathF.Round(Meta.Shop.Trainers.CooldownPerGrade * 100f);
        UiDraw.TextWithNumbers(batch, Small, SmallNumbers, $"OGNI GRADO: PASSIVA +{perGrade}%, RICARICA -{cut}%", new Vector2(330, 220), theme.TextDim);
        if (!unlocked)
            UiDraw.Text(batch, Small, "BLOCCATO", new Vector2(608 - UiDraw.Measure(Small, "BLOCCATO").X, 205), theme.TextDim);
        else if (Meta.TrainerGradePrice(trainer) is { } price)
            DrawPrice(batch, price, 608, 205);
        else
            UiDraw.Text(batch, Small, "MAX", new Vector2(608 - UiDraw.Measure(Small, "MAX").X, 205), Gold);

        // The challenge's team.
        UiDraw.Text(batch, Small, "SQUADRA DELLA SFIDA", new Vector2(326, 250), Gold);
        if (trainer.Team.Count == 0)
        {
            UiDraw.Text(batch, Small, "NESSUNA: NON SI SFIDA", new Vector2(326, 264), theme.TextDim);
            return;
        }

        for (var i = 0; i < trainer.Team.Count; i++)
        {
            var frame = new Rectangle(326 + i * 48, 262, 44, 44);
            UiDraw.Frame(batch, p, frame, theme);
            if (Services.Sprites.Portrait(trainer.Team[i].SpeciesId) is { } portrait)
                batch.Draw(portrait, new Rectangle(frame.X + 2, frame.Y + 2, 40, 40), Color.White);
        }

        var defeated = Meta.IsDefeated(trainer);
        UiDraw.Text(batch, Small, defeated ? "SCONFITTO!" : "DA SCONFIGGERE", new Vector2(326, 312), defeated ? Green : theme.TextDim);
        if (trainer.Team.Count > 0)
            UiDraw.Text(batch, Small, "[S] SFIDA", new Vector2(608 - UiDraw.Measure(Small, "[S] SFIDA").X, 312), Paper);
    }
}
