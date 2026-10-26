using FontStashSharp;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using PokeRogue.Core.Run;
using PokeRogue.Core.Run.Progression;
using PokeRogue.Game.Graphics;
using PokeRogue.Game.Input;
using PokeRogue.Game.Rendering;

namespace PokeRogue.Game.Ui;

/// <summary>
/// Opening a Dominante's chest (phase 3.3). The Ball drops into the middle of the screen, wobbles three times as in a
/// capture, bursts open in a flash, and its rewards fly out one by one to their places in a row. The rewards were
/// already applied by the run; this only shows them.
/// </summary>
public sealed class ChestWindow(GameServices services)
{
    private const int DropTicks = 16;
    private const int WobbleTicks = 14;
    private const int Wobbles = 3;
    private const int OpenTick = DropTicks + WobbleTicks * Wobbles + 6;
    private const int FlashTicks = 10;
    private const int FirstRewardTick = OpenTick + 8;
    private const int RewardStagger = 14;
    private const int FlyTicks = 12;

    /// <summary>Scripted players close the window this long after the last reward lands.</summary>
    private const int AutoCloseTicks = 70;

    private const int BallScale = 3;

    private static readonly Color Ink = new(16, 12, 28);
    private static readonly Color Paper = new(244, 240, 228);
    private static readonly Color Gold = new(248, 208, 72);
    private static readonly Color Dim = new(160, 152, 190);
    private static readonly UiTheme EvolutionTheme = UiTheme.Dark with { Fill = new Color(52, 40, 34, 240), Border = Gold, Highlight = new Color(150, 116, 48) };

    private ChestOpening? _chest;

    public bool IsOpen => _chest is not null;

    public int Age { get; private set; }

    private int DoneTick => FirstRewardTick + (_chest?.Rewards.Count ?? 0) * RewardStagger + FlyTicks;

    public void Open(ChestOpening chest)
    {
        _chest = chest;
        Age = 0;
    }

    public void Close() => _chest = null;

    /// <summary>Advances the animation; once every reward has landed, a confirmation (or, scripted, a short wait) closes it.</summary>
    public void Update(InputState? input)
    {
        if (_chest is null)
            return;

        Age++;
        if (Age < DoneTick)
            return;

        var confirmed = input is null
            ? Age >= DoneTick + AutoCloseTicks
            : input.Pressed(Keys.Enter) || input.Pressed(Keys.Space) || input.Pressed(Buttons.A);
        if (confirmed)
            Close();
    }

    public void Draw(SpriteBatch batch)
    {
        if (_chest is not { } chest)
            return;

        var p = services.Primitives;
        var centerX = services.Screen.Width / 2;
        var ballCenter = new Vector2(centerX, 128);
        var color = RunRenderer.ChestColor(chest.Tier);

        batch.Begin(samplerState: SamplerState.PointClamp);
        p.Rect(batch, new Rectangle(0, 0, services.Screen.Width, services.Screen.Height), Color.Black * (0.6f * MathHelper.Clamp(Age / 10f, 0f, 1f)));

        var (title, subtitle) = chest.Tier switch
        {
            ChestTier.Great => ("MEGA BALL", "TRE PREMI"),
            ChestTier.Ultra => ("ULTRA BALL", "CINQUE PREMI"),
            _ => ("POKÉ BALL", "UN PREMIO"),
        };
        UiDraw.TextCentered(batch, Body, title, centerX, 30, color, Ink);
        UiDraw.TextCentered(batch, Small, subtitle, centerX, 48, Dim, Ink);

        if (Age >= OpenTick)
            DrawBurst(batch, ballCenter, color);
        else
            DrawBall(batch, chest.Tier, ballCenter);

        for (var i = 0; i < chest.Rewards.Count; i++)
            DrawReward(batch, chest.Rewards[i], i, chest.Rewards.Count, ballCenter);

        if (Age >= DoneTick && Age / 15 % 2 == 0)
            UiDraw.TextCentered(batch, Small, "[INVIO] CONTINUA", centerX, 320, Paper, Ink);
        batch.End();
    }

    private SpriteFontBase Body => services.Fonts.Body(services.Style);

    private SpriteFontBase Small => services.Fonts.Small(services.Style);

    /// <summary>The Ball falling into place with a small bounce, then rocking on its base three times.</summary>
    private void DrawBall(SpriteBatch batch, ChestTier tier, Vector2 center)
    {
        if (services.ItemIcons.Get(RunRenderer.ChestIcon(tier)) is not { } icon)
            return;

        var y = center.Y;
        var angle = 0f;
        if (Age < DropTicks)
        {
            var t = Age / (float)DropTicks;
            y = MathHelper.Lerp(-40f, center.Y, 1f - (1f - t) * (1f - t)) - MathF.Sin(t * MathF.PI) * 6f;
        }
        else
        {
            var wobble = Age - DropTicks;
            var phase = wobble % WobbleTicks;
            if (wobble < WobbleTicks * Wobbles && phase < 9)
                angle = MathF.Sin(phase / 9f * MathF.Tau) * 0.35f;
        }

        // Rocks on its base: the pivot is the bottom of the ball.
        var crop = ItemIcons.Crop;
        var origin = new Vector2(crop.X + crop.Width / 2f, crop.Bottom);
        var foot = new Vector2(center.X, y + crop.Height * BallScale / 2f);
        services.Primitives.Ellipse(batch, new Vector2(center.X, center.Y + crop.Height * BallScale / 2f + 2), 44, 8, Color.Black * 0.35f);
        batch.Draw(icon, foot, null, Color.White, angle, origin, BallScale, SpriteEffects.None, 0f);
    }

    /// <summary>Where the Ball was: a white flash, then slow rays of the Ball's colour behind the rewards.</summary>
    private void DrawBurst(SpriteBatch batch, Vector2 center, Color color)
    {
        var p = services.Primitives;
        var age = Age - OpenTick;
        for (var k = 0; k < 12; k++)
        {
            var angle = k * MathF.Tau / 12f + Age * 0.01f;
            var length = 54f + (k % 2) * 18f;
            var grow = MathHelper.Clamp(age / 8f, 0f, 1f);
            var direction = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
            UiDraw.Line(batch, p, center + direction * 12f, center + direction * (12f + length * grow), color * 0.25f, 2f);
        }

        p.Ellipse(batch, center, 30, 30, color * 0.35f);
        if (age < FlashTicks)
        {
            var size = 20 + age * 9;
            p.Ellipse(batch, center, size, size, Color.White * (0.9f * (1f - age / (float)FlashTicks)));
        }
    }

    /// <summary>One reward: its icon flies from the Ball to its place in the row, then its tile shows name and level.</summary>
    private void DrawReward(SpriteBatch batch, LevelUpOption reward, int index, int count, Vector2 from)
    {
        var age = Age - (FirstRewardTick + index * RewardStagger);
        if (age < 0)
            return;

        var p = services.Primitives;
        const int width = 100, height = 92, gap = 8;
        var left = services.Screen.Width / 2 - (count * width + (count - 1) * gap) / 2;
        var tile = new Rectangle(left + index * (width + gap), 196, width, height);
        var iconBox = new Rectangle(tile.Center.X - 20, tile.Y + 8, 40, 40);
        var evolution = reward.Kind is LevelUpKind.Evolution or LevelUpKind.EvolutionItem;

        var t = MathHelper.Clamp(age / (float)FlyTicks, 0f, 1f);
        if (t < 1f)
        {
            // In flight: an arc out of the Ball, growing to full size.
            var ease = 1f - (1f - t) * (1f - t);
            var at = Vector2.Lerp(from, iconBox.Center.ToVector2(), ease) - new Vector2(0, MathF.Sin(t * MathF.PI) * 30f);
            var size = (int)MathHelper.Lerp(12f, 32f, ease);
            DrawIcon(batch, reward, new Rectangle((int)at.X - size / 2, (int)at.Y - size / 2, size, size));
            return;
        }

        UiDraw.Frame(batch, p, tile, evolution ? EvolutionTheme : UiTheme.Dark);
        UiDraw.Frame(batch, p, iconBox, evolution ? EvolutionTheme : UiTheme.Dark);
        DrawIcon(batch, reward, new Rectangle(iconBox.X + 4, iconBox.Y + 4, 32, 32));

        var lines = UiDraw.Wrap(Body, reward.NameIt, width - 8);
        for (var l = 0; l < Math.Min(2, lines.Count); l++)
            UiDraw.TextCentered(batch, Body, lines[l], tile.Center.X, tile.Y + 50 + l * 14, evolution ? Gold : Paper);

        var (badge, color) = reward.Kind switch
        {
            LevelUpKind.Evolution => ("EVOLUZIONE!", Gold),
            LevelUpKind.EvolutionItem => ("SI EVOLVE!", Gold),
            LevelUpKind.MoveUpgrade or LevelUpKind.ItemUpgrade => ($"LV {reward.ToLevel}", Gold),
            _ => ("MONETE", new Color(120, 220, 120)),
        };
        UiDraw.TextCentered(batch, Small, badge, tile.Center.X, tile.Bottom - 14, color, Ink);

        // A white pop as it lands.
        var pop = age - FlyTicks;
        if (pop < 6)
            p.Rect(batch, tile, Color.White * (0.6f * (1f - pop / 6f)));
    }

    private void DrawIcon(SpriteBatch batch, LevelUpOption option, Rectangle area)
    {
        if (option.MoveType is { } type)
        {
            services.MoveIcons.Draw(batch, services.Primitives, area, option.Id, type, glyphScale: area.Width >= 28 ? 2 : 1);
            return;
        }

        services.ItemIcons.Draw(batch, option.IconKey, area);
    }
}
