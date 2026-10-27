using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PokeRogue.Game.Graphics;
using PokeRogue.Game.Input;

namespace PokeRogue.Game.Ui;

/// <summary>
/// The touch controls drawn over a run (Android): the floating joystick under the left thumb and the round buttons on
/// the right, gold-rimmed when what they press is ready. Drawn only once a finger has touched the screen.
/// </summary>
public static class TouchControls
{
    private static readonly Color Ink = new(16, 12, 28);
    private static readonly Color Paper = new(244, 240, 228);
    private static readonly Color Gold = new(248, 208, 72);
    private static readonly Color Dim = new(160, 152, 190);

    public static void Draw(SpriteBatch batch, GameServices services, IReadOnlySet<InputState.TouchButton> ready)
    {
        var input = services.Input;
        if (!input.TouchEverUsed)
            return;

        var p = services.Primitives;
        var small = services.Fonts.Small(services.Style);
        batch.BeginVirtual(samplerState: SamplerState.PointClamp);

        if (input.StickDrawn is var (stickBase, knob))
        {
            var size = (int)(InputState.StickRadius * 2);
            p.Ellipse(batch, stickBase, size + 6, size + 6, Ink * 0.35f);
            p.Ring(batch, stickBase, InputState.StickRadius + 3, InputState.StickRadius + 3, Paper * 0.5f);
            p.Ellipse(batch, knob, 22, 22, Paper * 0.55f);
            p.Ring(batch, knob, 11, 11, Ink * 0.6f);
        }

        foreach (var button in InputState.TouchButtons)
        {
            if (!input.ShownButtons.Contains(button))
                continue;

            var (center, radius) = InputState.ButtonPlace(button);
            var held = input.HeldButtons.Contains(button);
            var on = ready.Contains(button);
            var size = (int)(radius * 2);
            p.Ellipse(batch, center, size, size, Ink * (held ? 0.8f : 0.5f));
            p.Ring(batch, center, radius, radius, (on ? Gold : Dim) * (held ? 1f : 0.8f), on ? 2 : 1);

            if (button == InputState.TouchButton.Pause)
            {
                p.Rect(batch, new Rectangle((int)center.X - 4, (int)center.Y - 5, 3, 10), Paper * 0.9f);
                p.Rect(batch, new Rectangle((int)center.X + 1, (int)center.Y - 5, 3, 10), Paper * 0.9f);
                continue;
            }

            var label = button switch
            {
                InputState.TouchButton.Ability => "ABILITÀ",
                InputState.TouchButton.Special => "SPECIALE",
                _ => "ALLEN.",
            };
            var measure = UiDraw.Measure(small, label);
            UiDraw.Text(batch, small, label, new Vector2(MathF.Round(center.X - measure.X / 2), MathF.Round(center.Y - measure.Y / 2)),
                on ? Paper : Dim * 0.9f);
        }

        batch.End();
    }
}
