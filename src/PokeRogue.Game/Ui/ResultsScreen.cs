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
    private int _age;

    public bool IsOpen => _run is not null;

    public void Open(RunState run)
    {
        _run = run;
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

        if (input.Pressed(Keys.Enter) || input.Pressed(Keys.Space) || input.Pressed(Buttons.A) || input.Pressed(Buttons.Start))
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
        UiDraw.TextCentered(batch, services.Fonts.Small(services.Style), "PROTOTIPO  -  PRATO", services.Screen.Width / 2f, 42, Dim);

        var left = new Rectangle(16, 56, 208, 280);
        UiDraw.Frame(batch, p, left, theme);
        DrawPokemon(batch, run, left);

        var moves = new Rectangle(236, 56, 388, 186);
        UiDraw.Frame(batch, p, moves, theme);
        DrawBuild(batch, run, moves);

        var defeated = new Rectangle(236, 250, 388, 86);
        UiDraw.Frame(batch, p, defeated, theme);
        DrawDefeated(batch, run, defeated);

        UiDraw.TextCentered(batch, services.Fonts.Small(services.Style), "[INVIO] RICOMINCIA          [ESC] ESCI", services.Screen.Width / 2f, 344, Dim, Ink);
        batch.End();
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
