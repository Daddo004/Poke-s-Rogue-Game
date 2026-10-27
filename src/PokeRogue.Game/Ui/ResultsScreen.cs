using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using PokeRogue.Core.Run;
using PokeRogue.Game.Graphics;
using PokeRogue.Game.Input;

namespace PokeRogue.Game.Ui;

public enum ResultsChoice
{
    Restart,
    Quit,
}

/// <summary>
/// End-of-run report: the Pokémon and its totals on the left, damage per move and held items on the right,
/// and the most defeated species (a first taste of the Pokédex).
/// </summary>
public sealed class ResultsScreen(GameServices services, Hud hud)
{
    private const int MaxSpeciesShown = 7;

    /// <summary>Six move rows of this pitch still leave room for the items line under the table.</summary>
    private const int MoveRowPitch = 21;

    private static readonly Color Ink = new(16, 12, 28);
    private static readonly Color Paper = new(244, 240, 228);
    private static readonly Color Gold = new(248, 208, 72);
    private static readonly Color Red = new(232, 72, 56);
    private static readonly Color Dim = new(160, 152, 190);

    private RunState? _run;
    private Core.Meta.RunReport? _report;
    private int _age;

    /// <summary>The "NOVITÀ" panel over the results (Block 6): missions completed and characters or trainers unlocked.</summary>
    private bool _newsOpen;

    public bool IsOpen => _run is not null;

    public bool NewsOpen => _newsOpen;

    /// <param name="report">What the end of the run brought to the save (Block 6); null for runs not recorded (previews).</param>
    public void Open(RunState run, Core.Meta.RunReport? report = null)
    {
        _run = run;
        _report = report;
        _newsOpen = report is { HasNews: true };
        _age = 0;
    }

    public void Close() => _run = null;

    public ResultsChoice? Update(InputState? input)
    {
        if (_run is null)
            return null;

        _age++;
        if (input is null || _age < 20)
            return null;

        var confirm = input.Pressed(Keys.Enter) || input.Pressed(Keys.Space) || input.Pressed(Buttons.A) || input.Pressed(Buttons.Start);
        if (_newsOpen)
        {
            if (confirm || input.Pressed(Keys.Escape) || input.Pressed(Buttons.B))
            {
                _newsOpen = false;
                _age = 10;
            }

            return null;
        }

        if (confirm)
            return ResultsChoice.Restart;
        if (input.Pressed(Keys.Escape) || input.Pressed(Buttons.B))
            return ResultsChoice.Quit;
        return null;
    }

    public void Draw(SpriteBatch batch)
    {
        if (_run is not { } run)
            return;

        var p = services.Primitives;
        var theme = services.Style.Theme;
        var victory = run.Outcome == RunOutcome.Victory;

        batch.Begin(samplerState: SamplerState.PointClamp);
        p.Rect(batch, new Rectangle(0, 0, services.Screen.Width, services.Screen.Height), Color.Black * 0.78f);

        UiDraw.TextCentered(batch, services.Fonts.Title(services.Style), victory ? "VITTORIA" : "SCONFITTA", services.Screen.Width / 2f, 6, victory ? Gold : Red, Ink);
        DrawBank(batch, run);

        var left = new Rectangle(16, 56, 208, 280);
        UiDraw.Frame(batch, p, left, theme);
        DrawPokemon(batch, run, left);

        var moves = new Rectangle(236, 56, 388, 186);
        UiDraw.Frame(batch, p, moves, theme);
        DrawBuild(batch, run, moves);

        var defeated = new Rectangle(236, 250, 388, 86);
        UiDraw.Frame(batch, p, defeated, theme);
        DrawDefeated(batch, run, defeated);

        UiDraw.TextCentered(batch, services.Fonts.Small(services.Style), $"[INVIO] RICOMINCIA          [ESC] {QuitLabel}", services.Screen.Width / 2f, 344, Dim, Ink);
        if (_newsOpen && _report is { } report)
            DrawNews(batch, report);
        batch.End();
    }

    /// <summary>What [ESC] does: "MENU" when the run came from the menu (Block 6), "ESCI" otherwise.</summary>
    public string QuitLabel { get; set; } = "ESCI";

    /// <summary>Under the title: the level, and the coins this run put in the bank (Block 6).</summary>
    private void DrawBank(SpriteBatch batch, RunState run)
    {
        var small = services.Fonts.Small(services.Style);
        var numbers = services.Fonts.Get(services.Style.NumberFont, services.Style.NumberFontSize / 2f);
        var text = run.Spawner.Timeline.NameIt.ToUpperInvariant();
        if (_report is { } report)
            text += $"     +{UiFormat.Number(report.CoinsBanked + report.MissionCoins)} MONETE IN BANCA, ORA {UiFormat.Number(report.Bank)}";
        var width = UiDraw.Measure(small, text).X;
        UiDraw.TextWithNumbers(batch, small, numbers, text, new Vector2(services.Screen.Width / 2f - width / 2f, 42), _report is null ? Dim : Gold);
    }

    /// <summary>
    /// The news of the run (Block 6), over the results until a key is pressed: each mission completed with its reward, then
    /// the characters and trainers unlocked, with their portraits.
    /// </summary>
    private void DrawNews(SpriteBatch batch, Core.Meta.RunReport report)
    {
        var p = services.Primitives;
        var small = services.Fonts.Small(services.Style);
        var body = services.Fonts.Body(services.Style);
        var numbers = services.Fonts.Get(services.Style.NumberFont, services.Style.NumberFontSize / 2f);
        p.Rect(batch, new Rectangle(0, 0, services.Screen.Width, services.Screen.Height), Color.Black * 0.6f);

        var unlocks = report.NewCharacters.Count + report.NewTrainers.Count + report.Fossils.Count;
        var rows = Math.Min(report.Missions.Count, 6);
        var opened = OpenedText(report);
        var height = 52 + rows * 26 + (unlocks > 0 ? 70 : 0) + (opened is null ? 0 : 30);
        var panel = new Rectangle(140, Math.Max(16, 180 - height / 2), 360, height);
        UiDraw.Frame(batch, p, panel, hud.EvolvedTheme);
        UiDraw.TextCentered(batch, services.Fonts.Title(services.Style), "NOVITÀ!", panel.Center.X, panel.Y + 4, Gold, Ink);

        var y = panel.Y + 42;
        for (var i = 0; i < rows; i++)
        {
            var mission = report.Missions[i];
            p.Rect(batch, new Rectangle(panel.X + 10, y, panel.Width - 20, 23), Color.White * 0.05f);
            UiDraw.Text(batch, small, "MISSIONE", new Vector2(panel.X + 16, y + 3), Dim);
            UiDraw.Text(batch, body, mission.NameIt, new Vector2(panel.X + 16, y + 9), Paper);
            var reward = Hud.RewardText(mission, services);
            UiDraw.TextWithNumbers(batch, small, numbers, reward, new Vector2(panel.Right - 16 - UiDraw.Measure(small, reward).X, y + 9), Gold);
            y += 26;
        }

        if (report.Missions.Count > rows)
            UiDraw.TextWithNumbers(batch, small, numbers, $"E ALTRE {report.Missions.Count - rows}", new Vector2(panel.X + 16, y - 4), Dim);

        if (unlocks > 0)
        {
            UiDraw.Text(batch, small, "SBLOCCATI", new Vector2(panel.X + 16, y + 4), Gold);
            var x = panel.X + 16;
            foreach (var character in report.NewCharacters.Take(7))
            {
                var frame = new Rectangle(x, y + 16, 44, 44);
                UiDraw.Frame(batch, p, frame, services.Style.Theme);
                if (services.Sprites.Portrait(character.First.Id) is { } portrait)
                    batch.Draw(portrait, new Rectangle(frame.X + 2, frame.Y + 2, 40, 40), Color.White);
                x += 48;
            }

            foreach (var trainer in report.NewTrainers.Take(7))
            {
                var frame = new Rectangle(x, y + 16, 44, 44);
                UiDraw.Frame(batch, p, frame, services.Style.Theme);
                if (services.TrainerPortraits.Sprite(trainer) is { } sprite)
                    batch.Draw(sprite, new Rectangle(frame.X + 4, frame.Y + 4, 36, 36), services.TrainerPortraits.Head(trainer), Color.White);
                x += 48;
            }

            // A fossil found (phase 7.4): its stone, to revive in the shop.
            foreach (var fossil in report.Fossils.Take(7))
            {
                var frame = new Rectangle(x, y + 16, 44, 44);
                UiDraw.Frame(batch, p, frame, services.Style.Theme);
                var art = services.Finds.Icon(fossil.Id);
                batch.Draw(art, new Rectangle(frame.Center.X - art.Width, frame.Center.Y - art.Height, art.Width * 2, art.Height * 2), Color.White);
                UiDraw.TextCentered(batch, small, "FOSSILE", frame.Center.X, frame.Bottom + 1, Gold, Ink);
                x += 48;
            }

            y += 70;
        }

        if (opened is not null)
            UiDraw.Text(batch, small, opened, new Vector2(panel.X + 16, y + 6), Paper);

        UiDraw.TextCentered(batch, small, "[INVIO] CONTINUA", panel.Center.X, panel.Bottom + 6, Dim, Ink);
    }

    /// <summary>"SI APRE: MONTE LUNA, BOSCO SMERALDO A DIFFICILE" (Block 7); null when nothing opened.</summary>
    private string? OpenedText(Core.Meta.RunReport report)
    {
        if (report.Opened.Count == 0)
            return null;

        var worlds = services.Worlds.Value;
        var names = report.Opened.Select(key =>
        {
            var parts = key.Split('/');
            var name = worlds.Find(parts[0])?.NameIt.ToUpperInvariant() ?? parts[0];
            return parts[1] == nameof(Core.Meta.Difficulty.Normal) ? name
                : $"{name} A {worlds.Difficulty(Enum.Parse<Core.Meta.Difficulty>(parts[1])).NameIt.ToUpperInvariant()}";
        }).Take(3).ToList();
        return $"SI APRE: {string.Join(", ", names)}";
    }

    private void DrawPokemon(SpriteBatch batch, RunState run, Rectangle panel)
    {
        var p = services.Primitives;
        var small = services.Fonts.Small(services.Style);
        var numbers = services.Fonts.Numbers(services.Style);
        var species = run.Player.Species;

        var portraitFrame = new Rectangle(panel.X + 8, panel.Y + 8, 84, 84);
        UiDraw.Frame(batch, p, portraitFrame, services.Style.Theme);
        if (services.Sprites.Portrait(species.Id) is { } portrait)
            batch.Draw(portrait, new Rectangle(portraitFrame.X + 2, portraitFrame.Y + 2, 80, 80), run.Outcome == RunOutcome.Defeat ? new Color(150, 150, 170) : Color.White);

        UiDraw.Text(batch, services.Fonts.Body(services.Style), species.NameIt, new Vector2(panel.X + 100, panel.Y + 8), Paper);
        UiDraw.Text(batch, numbers, $"Lv {run.Player.Level}", new Vector2(panel.X + 100, panel.Y + 26), Paper);
        for (var i = 0; i < species.Types.Count; i++)
        {
            var type = species.Types[i];
            UiDraw.Pill(batch, p, small, new Rectangle(panel.X + 100 + i * 52, panel.Y + 54, 48, 10), TypePalette.Of(type), TypePalette.NameIt(type).ToUpperInvariant());
        }

        (string Label, string Value, Color Color)[] rows =
        [
            ("TEMPO", UiFormat.Clock(run.Ticks), Paper),
            ("POKÉMON SCONFITTI", UiFormat.Number(run.Stats.Kills), Paper),
            ("MONETE", UiFormat.Number(run.Stats.CoinsCollected), Gold),
            ("DANNI SUBITI", UiFormat.Number((long)run.Stats.DamageTaken), Paper),
            ("PS RECUPERATI", UiFormat.Number((long)run.Player.HpRecovered), Paper),
        ];

        for (var i = 0; i < rows.Length; i++)
        {
            var y = panel.Y + 104 + i * 34;
            p.Rect(batch, new Rectangle(panel.X + 8, y, panel.Width - 16, 28), Color.White * 0.04f);
            UiDraw.Text(batch, small, rows[i].Label, new Vector2(panel.X + 14, y + 10), Dim);
            var width = UiDraw.Measure(numbers, rows[i].Value).X;
            UiDraw.TextOutlined(batch, numbers, rows[i].Value, new Vector2(panel.Right - 14 - width, y + 3), rows[i].Color, Ink);
        }
    }

    private void DrawBuild(SpriteBatch batch, RunState run, Rectangle panel)
    {
        var p = services.Primitives;
        var small = services.Fonts.Small(services.Style);
        var body = services.Fonts.Body(services.Style);
        var numbers = services.Fonts.Numbers(services.Style);

        UiDraw.Text(batch, small, "MOSSA", new Vector2(panel.X + 12, panel.Y + 8), Dim);
        UiDraw.Text(batch, small, "LV", new Vector2(panel.X + 200, panel.Y + 8), Dim);
        RightText(batch, small, "DANNI", panel.X + 300, panel.Y + 8, Dim);
        RightText(batch, small, "DPS", panel.Right - 12, panel.Y + 8, Dim);

        for (var i = 0; i < run.Player.Moves.Count && i < Hud.Slots; i++)
        {
            var move = run.Player.Moves[i];
            var y = panel.Y + 20 + i * MoveRowPitch;
            var slot = new Rectangle(panel.X + 10, y, 20, 20);
            var evolved = move.Definition.IsEvolution;
            UiDraw.Frame(batch, p, slot, evolved ? hud.EvolvedTheme : services.Style.Theme);
            services.MoveIcons.Draw(batch, p, new Rectangle(slot.X + 2, slot.Y + 2, 16, 16), move.Definition.Id, move.Definition.Type);

            // Evolved moves have a single level: "EVO" in gold instead of the number.
            UiDraw.Text(batch, body, move.Definition.NameIt, new Vector2(panel.X + 38, y + 1), evolved ? Gold : Paper);
            UiDraw.Text(batch, numbers, evolved ? "EVO" : move.Level.ToString(), new Vector2(panel.X + 202, y - 1), evolved ? Gold : Paper);

            var seconds = MathF.Max(1f, (run.Ticks - move.AcquiredTick) / (float)RunClock.TicksPerSecond);
            RightText(batch, numbers, UiFormat.Number((long)move.DamageDealt), panel.X + 300, y - 1, Paper);
            RightText(batch, numbers, UiFormat.Decimal(move.DamageDealt / seconds), panel.Right - 12, y - 1, Paper);
        }

        // Held items on one line, the label beside the slots.
        var itemsY = panel.Bottom - 32;
        p.Rect(batch, new Rectangle(panel.X + 8, itemsY - 6, panel.Width - 16, 1), Dim * 0.4f);
        UiDraw.Text(batch, small, "STRUMENTI", new Vector2(panel.X + 12, itemsY + 8), Dim);
        var slotsX = panel.X + 24 + (int)UiDraw.Measure(small, "STRUMENTI").X;
        for (var i = 0; i < Hud.Slots; i++)
        {
            var slot = new Rectangle(slotsX + i * 30, itemsY, 24, 24);
            UiDraw.Frame(batch, p, slot, services.Style.Theme);
            if (i < run.Player.Items.Count)
            {
                var item = run.Player.Items[i];
                hud.DrawItemIcon(batch, item.Definition.IconKey, slot);
                hud.Pips(batch, slot, item.Level, item.Definition.MaxLevel);
            }
        }
    }

    private void DrawDefeated(SpriteBatch batch, RunState run, Rectangle panel)
    {
        var small = services.Fonts.Small(services.Style);
        UiDraw.Text(batch, small, "POKÉMON SCONFITTI", new Vector2(panel.X + 12, panel.Y + 8), Dim);

        var top = run.Stats.TopKills(MaxSpeciesShown);
        if (top.Count == 0)
        {
            UiDraw.Text(batch, small, "NESSUNO", new Vector2(panel.X + 12, panel.Y + 40), Dim);
            return;
        }

        for (var i = 0; i < top.Count; i++)
        {
            var (speciesId, count) = top[i];
            var x = panel.X + 12 + i * 53;
            var frame = new Rectangle(x, panel.Y + 20, 44, 44);
            UiDraw.Frame(batch, services.Primitives, frame, services.Style.Theme);
            if (services.Sprites.Portrait(speciesId) is { } portrait)
                batch.Draw(portrait, new Rectangle(frame.X + 2, frame.Y + 2, 40, 40), Color.White);
            UiDraw.TextCentered(batch, small, $"X{UiFormat.Number(count)}", frame.Center.X, frame.Bottom + 4, Paper);
        }
    }

    private static void RightText(SpriteBatch batch, FontStashSharp.SpriteFontBase font, string text, float right, float y, Color color) =>
        UiDraw.Text(batch, font, text, new Vector2(right - UiDraw.Measure(font, text).X, y), color);
}
