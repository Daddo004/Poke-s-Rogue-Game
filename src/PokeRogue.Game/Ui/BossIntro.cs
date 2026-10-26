using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PokeRogue.Game.Graphics;

namespace PokeRogue.Game.Ui;

/// <summary>
/// The boss' entrance (phase 3.4): the game freezes, black bars close in from top and bottom like a cut scene, the boss
/// drops from the sky (drawn by the renderer) and roars, then its name and title slide in. The bars open again and the
/// fight starts.
/// </summary>
public sealed class BossIntro(GameServices services)
{
    public const int Ticks = 150;

    private const int BarHeight = 34;
    private const int BarTicks = 14;
    private const int NameTick = 40;

    private static readonly Color Ink = new(16, 12, 28);
    private static readonly Color Gold = new(248, 208, 72);
    private static readonly Color Paper = new(244, 240, 228);

    private string _name = "";
    private string _title = "";

    public int Age { get; private set; } = -1;

    public bool IsPlaying => Age >= 0;

    public void Start(string name, string title)
    {
        _name = name.ToUpperInvariant();
        _title = title.ToUpperInvariant();
        Age = 0;
    }

    public void Stop() => Age = -1;

    public void Update()
    {
        if (Age >= 0 && ++Age >= Ticks)
            Age = -1;
    }

    public void Draw(SpriteBatch batch)
    {
        if (Age < 0)
            return;

        var p = services.Primitives;
        var width = services.Screen.Width;
        var height = services.Screen.Height;
        var close = Ease(Age / (float)BarTicks) * Ease((Ticks - Age) / (float)BarTicks);
        var bar = (int)(BarHeight * close);

        batch.Begin(samplerState: SamplerState.PointClamp);
        p.Rect(batch, new Rectangle(0, 0, width, bar), Color.Black);
        p.Rect(batch, new Rectangle(0, height - bar, width, bar), Color.Black);

        if (Age >= NameTick)
        {
            // The name slides in from the left, the title from the right; both fade with the bars.
            var t = Ease((Age - NameTick) / 16f);
            var fade = Ease((Ticks - Age) / (float)BarTicks);
            var nameFont = services.Fonts.Numbers(services.Style);
            var titleFont = services.Fonts.Body(services.Style);
            var y = height - 92;
            UiDraw.TextCentered(batch, nameFont, _name, width / 2f - (1f - t) * 80f, y, Gold * fade, Ink * fade);
            UiDraw.TextCentered(batch, titleFont, _title, width / 2f + (1f - t) * 80f, y + 24, Paper * fade, Ink * fade);
            var line = (int)(160 * t);
            p.Rect(batch, new Rectangle(width / 2 - line / 2, y + 20, line, 1), Gold * (0.8f * fade));
        }

        batch.End();
    }

    private static float Ease(float t)
    {
        t = MathHelper.Clamp(t, 0f, 1f);
        return 1f - (1f - t) * (1f - t) * (1f - t);
    }
}
