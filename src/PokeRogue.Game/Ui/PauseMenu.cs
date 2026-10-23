using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using PokeRogue.Core.Run;
using PokeRogue.Game.Graphics;
using PokeRogue.Game.Input;

namespace PokeRogue.Game.Ui;

public enum PauseChoice
{
    Resume,
    Restart,
    Quit,
}

/// <summary>Basic pause (Esc): resume, restart or quit, with the current build. The full pause screen comes in 8.4.</summary>
public sealed class PauseMenu(GameServices services, Hud hud)
{
    private static readonly string[] Labels = ["Riprendi", "Ricomincia", "Esci dal gioco"];
    private static readonly Color Ink = new(16, 12, 28);
    private static readonly Color Paper = new(244, 240, 228);
    private static readonly Color Gold = new(248, 208, 72);
    private static readonly Color Dim = new(160, 152, 190);

    private int _age;

    public bool IsOpen { get; private set; }

    public int Cursor { get; private set; }

    public void Open()
    {
        IsOpen = true;
        Cursor = 0;
        _age = 0;
    }

    public void Close() => IsOpen = false;

    public void MoveCursor(int delta) => Cursor = (Cursor + delta + Labels.Length) % Labels.Length;

    public PauseChoice? Update(InputState? input)
    {
        if (!IsOpen)
            return null;

        _age++;
        if (input is null || _age < 2)
            return null;

        if (input.Pressed(Keys.Escape) || input.Pressed(Buttons.Start) || input.Pressed(Buttons.B))
            return PauseChoice.Resume;
        if (input.Pressed(Keys.Up) || input.Pressed(Keys.W) || input.Pressed(Buttons.DPadUp))
            MoveCursor(-1);
        if (input.Pressed(Keys.Down) || input.Pressed(Keys.S) || input.Pressed(Buttons.DPadDown))
            MoveCursor(1);
        if (input.Pressed(Keys.Enter) || input.Pressed(Keys.Space) || input.Pressed(Buttons.A))
            return (PauseChoice)Cursor;
        return null;
    }

    public void Draw(SpriteBatch batch, RunState run)
    {
        if (!IsOpen)
            return;

        var p = services.Primitives;
        var small = services.Fonts.Small(services.Style);
        var body = services.Fonts.Body(services.Style);
        var numbers = services.Fonts.Numbers(services.Style);

        batch.Begin(samplerState: SamplerState.PointClamp);
        p.Rect(batch, new Rectangle(0, 0, services.Screen.Width, services.Screen.Height), Color.Black * 0.6f);

        var panel = new Rectangle(200, 62, 240, 236);
        UiDraw.Frame(batch, p, panel, services.Style.Theme);
        UiDraw.TextCentered(batch, services.Fonts.Title(services.Style), "PAUSA", panel.Center.X, panel.Y + 6, Paper, Ink);
        UiDraw.TextCentered(batch, numbers, $"Lv {run.Player.Level}   {UiFormat.Clock(run.Ticks)}", panel.Center.X, panel.Y + 46, Dim);

        for (var i = 0; i < Labels.Length; i++)
        {
            var row = new Rectangle(panel.X + 24, panel.Y + 76 + i * 24, panel.Width - 48, 20);
            var selected = i == Cursor;
            if (selected)
            {
                p.Rect(batch, row, Color.White * 0.08f);
                p.Outline(batch, row, _age / 8 % 2 == 0 ? Gold : Color.Lerp(Gold, Color.White, 0.6f), 1);
            }

            UiDraw.TextCentered(batch, body, Labels[i], row.Center.X, row.Y + 2, selected ? Paper : Dim);
        }

        // Current build: moves and held items with their level marks.
        UiDraw.Text(batch, small, "SQUADRA", new Vector2(panel.X + 16, panel.Y + 156), Dim);
        for (var i = 0; i < Hud.Slots; i++)
        {
            var moveSlot = new Rectangle(panel.X + 16 + i * 35, panel.Y + 168, 24, 24);
            hud.DrawMoveSlot(batch, moveSlot, i < run.Player.Moves.Count ? run.Player.Moves[i] : null);

            var itemSlot = new Rectangle(panel.X + 16 + i * 35, panel.Y + 198, 24, 24);
            UiDraw.Frame(batch, p, itemSlot, services.Style.Theme);
            if (i < run.Player.Items.Count)
            {
                var item = run.Player.Items[i];
                hud.DrawItemIcon(batch, item.Definition.IconKey, itemSlot);
                hud.Pips(batch, itemSlot, item.Level, item.Definition.MaxLevel);
            }
        }

        UiDraw.TextCentered(batch, small, "[ESC] RIPRENDI     [SU/GIU] SCEGLI     [INVIO] CONFERMA", panel.Center.X, panel.Bottom + 8, Dim, Ink);
        batch.End();
    }
}
