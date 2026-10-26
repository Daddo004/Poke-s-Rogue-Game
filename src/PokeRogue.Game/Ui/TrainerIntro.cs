using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PokeRogue.Core.Run.Trainers;
using PokeRogue.Game.Graphics;

namespace PokeRogue.Game.Ui;

/// <summary>
/// The challenged trainer steps in (phase 5.3), like the battle intro of the games: the game freezes, black bars close
/// in, the trainer's figure slides in from the right on a band of their type's colour, their title and name slide in
/// from the left with "TI SFIDA!" and a Poké Ball for each Pokémon of the team. Then the bars open and the first Pokémon
/// is sent out.
/// </summary>
public sealed class TrainerIntro(GameServices services)
{
    public const int Ticks = 160;

    private const int BarHeight = 34;
    private const int BarTicks = 14;
    private const int SlideTicks = 18;
    private const int TextTick = 24;

    private static readonly Color Ink = new(16, 12, 28);
    private static readonly Color Gold = new(248, 208, 72);
    private static readonly Color Paper = new(244, 240, 228);

    private TrainerDefinition? _trainer;

    public int Age { get; private set; } = -1;

    public bool IsPlaying => Age >= 0;

    public void Start(TrainerDefinition trainer)
    {
        _trainer = trainer;
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
        if (Age < 0 || _trainer is not { } trainer)
            return;

        var p = services.Primitives;
        var width = services.Screen.Width;
        var height = services.Screen.Height;
        var open = Ease(Age / (float)BarTicks) * Ease((Ticks - Age) / (float)BarTicks);
        var bar = (int)(BarHeight * open);

        batch.Begin(samplerState: SamplerState.PointClamp);
        p.Rect(batch, new Rectangle(0, 0, width, bar), Color.Black);
        p.Rect(batch, new Rectangle(0, height - bar, width, bar), Color.Black);

        // A band of the type's colour across the middle, darker at its edges, opening with the bars.
        var color = Vfx.TrainerVfx.ColorOf(trainer);
        var bandHeight = (int)(120 * open);
        var band = new Rectangle(0, height / 2 - bandHeight / 2 + 10, width, bandHeight);
        p.Rect(batch, band, Color.Lerp(color, Ink, 0.72f) * 0.9f);
        p.Rect(batch, new Rectangle(0, band.Y, width, 2), Color.Lerp(color, Color.White, 0.3f) * open);
        p.Rect(batch, new Rectangle(0, band.Bottom - 2, width, 2), Color.Lerp(color, Color.White, 0.3f) * open);

        // The figure, twice its size, slides in from the right and stands on the band's lower edge.
        var fade = Ease((Ticks - Age) / (float)BarTicks);
        if (services.TrainerPortraits.Sprite(trainer) is { } sprite)
        {
            var slide = Ease(Age / (float)SlideTicks);
            var x = MathHelper.Lerp(width + 20, width - 200, slide);
            var y = band.Bottom - sprite.Height * 2 + 6;
            batch.Draw(sprite, new Rectangle((int)x, y, sprite.Width * 2, sprite.Height * 2), Color.White * fade);
        }

        if (Age >= TextTick)
        {
            var t = Ease((Age - TextTick) / 16f);
            var left = 70 - (1f - t) * 80f;
            var title = services.Fonts.Body(services.Style);
            var top = band.Y + 16;
            UiDraw.TextOutlined(batch, title, trainer.TitleIt.ToUpperInvariant(), new Vector2(left, top), Paper * (t * fade), Ink * (t * fade));
            UiDraw.TextOutlined(batch, services.Fonts.Title(services.Style), trainer.NameIt.ToUpperInvariant(), new Vector2(left, top + 16), Gold * (t * fade), Ink * (t * fade));
            UiDraw.TextOutlined(batch, title, "TI SFIDA!", new Vector2(left, top + 50), Paper * (t * fade), Ink * (t * fade));

            // One Poké Ball per Pokémon, popping in one after the other.
            for (var i = 0; i < trainer.Team.Count; i++)
            {
                var appear = Age - TextTick - 12 - i * 5;
                if (appear < 0)
                    continue;

                var pop = appear < 4 ? 1.4f - appear * 0.1f : 1f;
                var size = (int)(ItemIcons.Crop.Width * 0.75f * pop);
                var center = new Vector2(left + 8 + i * 22, top + 84);
                services.ItemIcons.Draw(batch, "poke-ball", new Rectangle((int)(center.X - size / 2f), (int)(center.Y - size / 2f), size, size), alpha: fade);
            }
        }

        batch.End();
    }

    private static float Ease(float t)
    {
        t = MathHelper.Clamp(t, 0f, 1f);
        return 1f - (1f - t) * (1f - t) * (1f - t);
    }
}
