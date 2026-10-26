using FontStashSharp;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PokeRogue.Core.Data;
using PokeRogue.Core.Run;
using PokeRogue.Core.Run.Specials;
using PokeRogue.Game.Graphics;
using PokeRogue.Game.Vfx;

namespace PokeRogue.Game.Ui;

/// <summary>
/// The special move (phase 4.4), played while the game is frozen: a cut-in band slides across with the Pokémon's
/// portrait and the move's name, then the move fills the screen with its type's picture, a flash marks the impact (the
/// run applies the damage there), and the field comes back. One picture per type, drawn procedurally in screen space.
/// </summary>
public sealed partial class SpecialWindow(GameServices services)
{
    private const int CutInEnd = 44;
    private const int EffectStart = 40;
    private const int EffectTicks = 60;
    private const int ImpactTick = EffectStart + 44;
    private const int FlashTicks = 12;
    private const int EndTick = EffectStart + EffectTicks + 12;

    private static readonly Color Ink = new(16, 12, 28);
    private static readonly Color Paper = new(244, 240, 228);

    private MoveShapes? _shapes;
    private RunState? _run;
    private SpecialMove? _special;
    private int _seed;

    public bool IsOpen => _run is not null;

    public int Age { get; private set; }

    /// <summary>Previews: show only the full-screen picture, without the cut-in band.</summary>
    public bool SkipCutIn { get; set; }

    private int W => services.Screen.Width;

    private int H => services.Screen.Height;

    private MoveShapes Shapes => _shapes ??= new MoveShapes(services.Device);

    public void Open(RunState run)
    {
        _run = run;
        _special = run.SpecialMove;
        _seed = run.Special.Uses * 7919 + 13;
        Age = SkipCutIn ? EffectStart - 6 : 0;
    }

    public void Close()
    {
        _run = null;
        _special = null;
    }

    /// <summary>Advances the animation; true on the tick of the impact (the caller shakes the camera).</summary>
    public bool Update()
    {
        if (_run is not { } run)
            return false;

        Age++;
        if (Age == ImpactTick)
        {
            run.UnleashSpecial();
            return true;
        }

        if (Age >= EndTick)
            Close();
        return false;
    }

    public void Draw(SpriteBatch batch)
    {
        if (_run is not { } run || _special is not { } special)
            return;

        var p = services.Primitives;
        batch.Begin(samplerState: SamplerState.PointClamp);

        // The field darkens while the special plays, more for the dark types.
        var dim = special.Type is PokemonType.Dark or PokemonType.Ghost ? 0.75f : 0.45f;
        var fade = Age < 10 ? Age / 10f : Age > EndTick - 12 ? (EndTick - Age) / 12f : 1f;
        p.Rect(batch, new Rectangle(0, 0, W, H), Color.Black * (dim * MathHelper.Clamp(fade, 0f, 1f)));

        if (Age >= EffectStart)
        {
            var t = MathHelper.Clamp((Age - EffectStart) / (float)EffectTicks, 0f, 1f);
            DrawPicture(batch, special.Type, t, Age - EffectStart);
        }

        if (!SkipCutIn && Age < CutInEnd)
            DrawCutIn(batch, run, special);

        // The impact: a white flash, quick to come and slower to go.
        var flash = Age - ImpactTick;
        if (flash is >= -3 and < FlashTicks)
        {
            var alpha = flash < 0 ? (flash + 4) / 4f : 1f - flash / (float)FlashTicks;
            p.Rect(batch, new Rectangle(0, 0, W, H), Color.White * (0.85f * alpha));
        }

        batch.End();
    }

    /// <summary>The band: it slides in from the left with the portrait and the name, holds, and leaves to the right.</summary>
    private void DrawCutIn(SpriteBatch batch, RunState run, SpecialMove special)
    {
        var p = services.Primitives;
        var pal = TypeFx.For(special.Type);
        var enter = MathHelper.Clamp(Age / 10f, 0f, 1f);
        var leave = MathHelper.Clamp((Age - 34) / 10f, 0f, 1f);
        var offset = -W * (1f - Ease(enter)) + W * Ease(leave) * Ease(leave);
        var band = new Rectangle((int)offset, 116, W, 104);

        p.Rect(batch, band, pal.Dark * 0.94f);
        for (var x = -120; x < W + 120; x += 22)
        {
            var sx = band.X + x + (Age * 3) % 22;
            UiDraw.Line(batch, p, new Vector2(sx, band.Bottom), new Vector2(sx + 60, band.Y), pal.Main * 0.22f, 6f);
        }

        p.Rect(batch, new Rectangle(band.X, band.Y - 3, band.Width, 3), pal.Hot);
        p.Rect(batch, new Rectangle(band.X, band.Bottom, band.Width, 3), pal.Hot);

        var frame = new Rectangle(band.X + 84, band.Y + 8, 88, 88);
        UiDraw.Frame(batch, p, frame, UiTheme.Dark with { Border = pal.Hot, Highlight = pal.Main });
        if (services.Sprites.Portrait(run.Player.Form.Id) is { } portrait)
            batch.Draw(portrait, new Rectangle(frame.X + 4, frame.Y + 4, 80, 80), Color.White);

        var small = services.Fonts.Small(services.Style);
        var x0 = band.X + 196;
        UiDraw.TextOutlined(batch, small, $"{run.Player.Form.NameIt.ToUpperInvariant()} USA LA MOSSA SPECIALE", new Vector2(x0, band.Y + 16), Paper, Ink);
        UiDraw.TextOutlined(batch, services.Fonts.Title(services.Style), special.NameIt.ToUpperInvariant(), new Vector2(x0, band.Y + 32), pal.Hot, Ink);
        UiDraw.Pill(batch, p, small, new Rectangle(x0, band.Y + 78, 60, 12), TypePalette.Of(special.Type), TypePalette.NameIt(special.Type).ToUpperInvariant());
    }

    private void DrawPicture(SpriteBatch batch, PokemonType type, float t, int age)
    {
        switch (type)
        {
            case PokemonType.Fire: Fire(batch, t, age); break;
            case PokemonType.Water: Water(batch, t, age); break;
            case PokemonType.Grass: Grass(batch, t, age); break;
            case PokemonType.Electric: Electric(batch, t, age); break;
            case PokemonType.Ice: Ice(batch, t, age); break;
            case PokemonType.Fighting: Fighting(batch, t, age); break;
            case PokemonType.Poison: Poison(batch, t, age); break;
            case PokemonType.Ground: Ground(batch, t, age); break;
            case PokemonType.Flying: Flying(batch, t, age); break;
            case PokemonType.Psychic: Psychic(batch, t, age); break;
            case PokemonType.Bug: Bug(batch, t, age); break;
            case PokemonType.Rock: Rock(batch, t, age); break;
            case PokemonType.Ghost: Ghost(batch, t, age); break;
            case PokemonType.Dragon: Dragon(batch, t, age); break;
            case PokemonType.Dark: Dark(batch, t, age); break;
            case PokemonType.Steel: Steel(batch, t, age); break;
            default: Normal(batch, t, age); break;
        }
    }

    /// <summary>A repeatable pseudo-random number in [0, 1) for element <paramref name="i"/> of this use.</summary>
    private float R(int i, int salt = 0)
    {
        var h = (uint)(i * 374761393 + salt * 668265263 + _seed * 2246822519u);
        h = (h ^ (h >> 13)) * 1274126177u;
        return (h ^ (h >> 16)) % 10000 / 10000f;
    }

    private static float Ease(float t) => 1f - (1f - t) * (1f - t);

    private Vector2 Center => new(W / 2f, H / 2f);

    private SpriteFontBase Small => services.Fonts.Small(services.Style);
}
