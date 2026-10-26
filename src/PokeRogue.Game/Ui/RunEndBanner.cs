using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using PokeRogue.Core.Run;
using PokeRogue.Game.Graphics;
using PokeRogue.Game.Input;

namespace PokeRogue.Game.Ui;

/// <summary>End-of-run beat: "ESAUSTO!" on defeat, "SOPRAVVISSUTO!" on victory, then a prompt to see the results.</summary>
public sealed class RunEndBanner(GameServices services)
{
    /// <summary>Ticks before the prompt appears and input is accepted.</summary>
    public const int PromptDelay = 90;

    private const int TitleAt = 24;
    private const int SubtitleAt = 48;

    private static readonly Color Ink = new(16, 12, 28);
    private static readonly Color Paper = new(244, 240, 228);
    private static readonly Color Gold = new(248, 208, 72);
    private static readonly Color Red = new(232, 72, 56);
    private static readonly Color Dim = new(160, 152, 190);

    private string _subtitle = "";

    public RunOutcome Outcome { get; private set; }

    public int Age { get; private set; }

    public bool IsActive { get; private set; }

    private bool _bossBeaten;

    /// <param name="bossBeaten">A won level whose boss fell (phase 3.4) says "VITTORIA!" rather than "SOPRAVVISSUTO!".</param>
    public void Start(RunOutcome outcome, string subtitle, bool bossBeaten = false)
    {
        Outcome = outcome;
        _subtitle = subtitle;
        _bossBeaten = bossBeaten;
        Age = 0;
        IsActive = true;
    }

    public void Stop() => IsActive = false;

    /// <summary>Returns true once the player asks to continue (or <paramref name="autoContinue"/> after the prompt shows).</summary>
    public bool Update(InputState? input, bool autoContinue = false)
    {
        if (!IsActive)
            return false;

        Age++;
        if (Age < PromptDelay)
            return false;

        return autoContinue || input is not null &&
            (input.Pressed(Keys.Enter) || input.Pressed(Keys.Space) || input.Pressed(Buttons.A) || input.Pressed(Buttons.Start));
    }

    public void Draw(SpriteBatch batch)
    {
        if (!IsActive)
            return;

        var p = services.Primitives;
        var width = services.Screen.Width;
        var height = services.Screen.Height;
        var victory = Outcome == RunOutcome.Victory;

        batch.Begin(samplerState: SamplerState.PointClamp);

        // Defeat fades to a dark red; victory flashes white, then settles on a light veil.
        if (victory)
        {
            var flash = MathHelper.Clamp(1f - Age / 12f, 0f, 1f);
            p.Rect(batch, new Rectangle(0, 0, width, height), Color.White * (0.6f * flash));
            p.Rect(batch, new Rectangle(0, 0, width, height), new Color(16, 12, 28) * MathHelper.Clamp(Age / 40f * 0.35f, 0f, 0.35f));
        }
        else
        {
            p.Rect(batch, new Rectangle(0, 0, width, height), new Color(40, 4, 16) * MathHelper.Clamp(Age / 60f * 0.6f, 0f, 0.6f));
        }

        if (Age >= TitleAt)
        {
            var t = MathHelper.Clamp((Age - TitleAt) / 14f, 0f, 1f);
            var ease = 1f - (1f - t) * (1f - t);

            // Dark band behind title and subtitle, so they stay readable over a crowded horde.
            p.Rect(batch, new Rectangle(0, 62, width, 78), Color.Black * (0.4f * ease));
            p.Rect(batch, new Rectangle(0, 61, width, 1), (victory ? Gold : Red) * (0.6f * ease));
            p.Rect(batch, new Rectangle(0, 140, width, 1), (victory ? Gold : Red) * (0.6f * ease));
            // Above the player (screen centre) so the faint or victory pose stays visible.
            var title = victory ? _bossBeaten ? "VITTORIA!" : "SOPRAVVISSUTO!" : "ESAUSTO!";
            var color = victory ? Gold : Red;
            var y = 72 - (1f - ease) * 14f;
            UiDraw.TextCentered(batch, services.Fonts.Title(services.Style), title, width / 2f, y, color * ease, Ink * ease);

            if (victory)
                DrawSparkles(batch, width / 2f, y + 16, ease);
        }

        if (Age >= SubtitleAt)
        {
            var a = MathHelper.Clamp((Age - SubtitleAt) / 12f, 0f, 1f);
            UiDraw.TextCentered(batch, services.Fonts.Body(services.Style), _subtitle, width / 2f, 114, Paper * a, Ink * a);
        }

        if (Age >= PromptDelay && Age / 20 % 2 == 0)
            UiDraw.TextCentered(batch, services.Fonts.Small(services.Style), "[INVIO] RISULTATI", width / 2f, 250, Dim, Ink);

        batch.End();
    }

    /// <summary>Twinkling four-point stars scattered around the victory title.</summary>
    private void DrawSparkles(SpriteBatch batch, float centerX, float centerY, float alpha)
    {
        var p = services.Primitives;
        for (var i = 0; i < 10; i++)
        {
            var angle = i * MathF.Tau / 10f + Age * 0.01f;
            var radius = 120f + (i % 3) * 18f;
            var at = new Vector2(centerX + MathF.Cos(angle) * radius, centerY + MathF.Sin(angle) * radius * 0.35f);
            var phase = (Age + i * 7) % 30;
            var size = phase < 15 ? phase / 5 : (29 - phase) / 5;
            var color = (i % 2 == 0 ? Gold : Color.White) * alpha;
            p.Rect(batch, new Rectangle((int)at.X - size, (int)at.Y, size * 2 + 1, 1), color);
            p.Rect(batch, new Rectangle((int)at.X, (int)at.Y - size, 1, size * 2 + 1), color);
        }
    }
}
