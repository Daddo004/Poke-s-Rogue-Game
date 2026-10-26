using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PokeRogue.Core.Combat;
using PokeRogue.Core.Data;
using PokeRogue.Core.Geometry;
using PokeRogue.Core.Run;
using PokeRogue.Core.Run.Trainers;
using PokeRogue.Game.Graphics;

namespace PokeRogue.Game.Vfx;

/// <summary>
/// What the trainer's active ability shows in the field (phase 5.2), one simple look per archetype, in the colour of the
/// trainer's type (gold without one) or of the status spread:
/// - Shield: a bubble around the player, one rim per charge left, flashing when it stops a hit and bursting at the end;
/// - Heal: a green ring at the feet and rising sparks;
/// - Wave: a thick ring spreading to its reach, with dust along it;
/// - SlowTime: violet edges on the screen while time is bent;
/// - Intangible: the player see-through, with a pale outline;
/// - TypeBoost: a glow under the feet and streaks rising around the body;
/// - Fortune: gold glints around the player;
/// - Frenzy: two arcs turning around the player like clock hands;
/// - StatusStorm: a ring sweeping the screen and a brief tint of the status colour;
/// - Haste: afterimages left behind while running.
/// Coloured light is alpha-blended, so it keeps its colour on the grass, and nothing covers the face.
/// </summary>
public sealed class TrainerVfx(GameServices services)
{
    public static readonly Color NoTypeGold = new(248, 208, 72);

    private const int RingTicks = 22;
    private const int StormTicks = 30;
    private const int BlockFlashTicks = 10;
    private const int AfterimageTicks = 16;

    private static readonly Color Heal = new(120, 230, 120);
    private static readonly Color Ink = new(16, 12, 28);
    private static readonly Color Violet = new(120, 70, 200);

    private readonly Particles _particles = new(512);
    private readonly Random _random = new(13);
    private readonly List<Afterimage> _afterimages = [];
    private int _ringAge = -1;
    private float _ringRadius;
    private Color _ringColor;
    private int _stormAge = -1;
    private Color _stormColor;
    private int _healAge = -1;
    private int _blockFlash;
    private int _tick;

    public int ParticleCount => _particles.Active;

    /// <summary>The colour of a trainer's effects: their type's, gold without one.</summary>
    public static Color ColorOf(TrainerDefinition trainer) => trainer.Type is { } type ? TypeFx.For(type).Main : NoTypeGold;

    public void Reset()
    {
        _afterimages.Clear();
        _ringAge = _stormAge = _healAge = -1;
        _blockFlash = 0;
    }

    public void OnEvent(in RunEvent e, RunState run)
    {
        if (e.Type != RunEventType.TrainerAbility || run.Trainer is not { } trainer)
            return;

        var effect = (TrainerEffect)(int)e.Value;
        var kind = (TrainerActiveKind)(int)e.Target.Y;
        var color = ColorOf(trainer);
        var feet = run.Player.Position.ToXna();
        switch (effect)
        {
            case TrainerEffect.Used:
                switch (kind)
                {
                    case TrainerActiveKind.Wave:
                        _ringAge = 0;
                        _ringRadius = e.Target.X;
                        _ringColor = color;
                        break;
                    case TrainerActiveKind.StatusStorm:
                        _stormAge = 0;
                        _stormColor = StatusIcons.Art(e.Status).Main;
                        break;
                    case TrainerActiveKind.Heal:
                        // Green sparks rising from around the body, like the games' healing sparkle.
                        _healAge = 0;
                        for (var i = 0; i < 24; i++)
                            Rise(feet, Heal, 18f);
                        break;
                    case TrainerActiveKind.Fortune:
                        _particles.Burst(feet - new Vector2(0, 10), 20, 120f, Color.White, NoTypeGold, 30, _random, size: 2f);
                        break;
                    default:
                        _particles.Burst(feet - new Vector2(0, 10), 16, 110f, Color.White, color, 24, _random, size: 2f);
                        break;
                }

                break;
            case TrainerEffect.Blocked:
                _blockFlash = BlockFlashTicks;
                _particles.Burst(feet - new Vector2(0, 10), 10, 120f, Color.White, color, 18, _random, size: 2f);
                break;
            case TrainerEffect.Ended when kind == TrainerActiveKind.Shield:
                // The bubble bursts into specks.
                for (var i = 0; i < 24; i++)
                {
                    var angle = i * MathF.Tau / 24f;
                    var dir = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
                    _particles.Emit(feet - new Vector2(0, 10) + dir * 16f, dir * 70f, Color.White, color, 20, 2f, 0.88f);
                }

                break;
        }
    }

    /// <param name="anim">The player's animation right now, for Scatto's afterimages.</param>
    public void Update(RunState run, PmdAnim? anim, int frame, Direction8 facing, bool paused)
    {
        _tick++;
        _particles.Update();
        if (_blockFlash > 0)
            _blockFlash--;
        if (_ringAge >= 0 && ++_ringAge >= RingTicks)
            _ringAge = -1;
        if (_stormAge >= 0 && ++_stormAge >= StormTicks)
            _stormAge = -1;
        if (_healAge >= 0 && ++_healAge >= RingTicks)
            _healAge = -1;
        for (var i = _afterimages.Count - 1; i >= 0; i--)
        {
            _afterimages[i] = _afterimages[i] with { Age = _afterimages[i].Age + 1 };
            if (_afterimages[i].Age >= AfterimageTicks)
                _afterimages.RemoveAt(i);
        }

        if (paused || run.Trainer is not { } trainer || !run.TrainerState.IsActive)
            return;

        var feet = run.Player.Position.ToXna();
        var color = ColorOf(trainer);
        switch (trainer.Active.Kind)
        {
            case TrainerActiveKind.TypeBoost when _tick % 2 == 0:
                Rise(feet, color, 14f);
                break;
            case TrainerActiveKind.Fortune when _tick % 5 == 0:
                var angle = _random.NextSingle() * MathF.Tau;
                var spot = feet - new Vector2(0, 10) + new Vector2(MathF.Cos(angle) * 14f, MathF.Sin(angle) * 10f);
                _particles.Emit(spot, new Vector2(0, -12f), Color.White, NoTypeGold, 22, 2f, 0.95f);
                break;
            case TrainerActiveKind.Haste when anim is not null && _tick % 4 == 0 && run.Player.IsMoving:
                _afterimages.Add(new Afterimage(feet, run.Player.Form.Id, anim, frame, facing, 0));
                break;
        }
    }

    /// <summary>Under the sprites: the glow of a type boost, the rings of a heal, a wave and a status storm. World space, alpha batch.</summary>
    public void DrawGround(SpriteBatch batch, RunState run)
    {
        var p = services.Primitives;
        var feet = run.Player.Position.ToXna();
        if (run.Trainer is { } trainer && TrainerSystem.Running(run, TrainerActiveKind.TypeBoost))
        {
            var beat = 0.5f + 0.5f * MathF.Sin(_tick * 0.18f);
            var color = ColorOf(trainer);
            p.Ellipse(batch, feet + new Vector2(0, 1), 44, 16, color * (0.3f + 0.2f * beat));
            p.Ellipse(batch, feet + new Vector2(0, 1), 26, 9, Color.Lerp(color, Color.White, 0.4f) * (0.35f + 0.2f * beat));
            p.Ring(batch, feet + new Vector2(0, 1), 22f + beat * 2f, 8f + beat, Color.Lerp(color, Ink, 0.25f) * 0.8f, 1);
        }

        if (_healAge >= 0)
        {
            for (var ring = 0; ring < 2; ring++)
            {
                var t = (_healAge - ring * 6) / (float)RingTicks;
                if (t is < 0f or > 1f)
                    continue;
                var radius = 8f + t * 30f;
                p.Ring(batch, feet, radius, radius * 0.45f, Color.Lerp(Color.White, Heal, t) * (1f - t), 2);
            }
        }

        if (_ringAge >= 0)
        {
            // Out to the wave's reach in its first two thirds, then it fades where it stopped.
            var t = _ringAge / (float)RingTicks;
            var radius = _ringRadius * MathF.Min(1f, t * 1.5f);
            var alpha = t < 0.66f ? 0.9f : (1f - t) / 0.34f * 0.9f;
            p.Ring(batch, feet, radius, radius * 0.5f, _ringColor * alpha, 3);
            p.Ring(batch, feet, radius - 4f, (radius - 4f) * 0.5f, Color.White * (alpha * 0.5f), 1);
            if (_ringAge % 2 == 0 && t < 0.66f)
            {
                for (var i = 0; i < 6; i++)
                {
                    var angle = _random.NextSingle() * MathF.Tau;
                    var spot = feet + new Vector2(MathF.Cos(angle) * radius, MathF.Sin(angle) * radius * 0.5f);
                    _particles.Emit(spot, new Vector2(0, -20f), _ringColor, Color.Lerp(_ringColor, Color.White, 0.5f), 14, 2f);
                }
            }
        }

        if (_stormAge >= 0)
        {
            var t = _stormAge / (float)StormTicks;
            var radius = 20f + t * 360f;
            p.Ring(batch, feet, radius, radius * 0.56f, _stormColor * (0.85f * (1f - t)), 3);
        }
    }

    /// <summary>
    /// Over the sprites: the shield bubble, Frenesia's turning arcs, Scatto's afterimages and the sparks. The bubble is a
    /// ring around the body with a faint fill, so the face shows through. World space, alpha batch.
    /// </summary>
    public void DrawObjects(SpriteBatch batch, RunState run)
    {
        var p = services.Primitives;
        foreach (var image in _afterimages)
        {
            if (services.Sprites.Get(image.SpeciesId) is not { } set || run.Trainer is not { } owner)
                continue;

            var alpha = 0.5f * (1f - image.Age / (float)AfterimageTicks);
            set.Draw(batch, image.Anim, image.Frame, image.Facing, image.Feet, ColorOf(owner) * alpha, silhouette: true);
        }

        if (run.Trainer is { } trainer && run.TrainerState.IsActive)
        {
            var color = ColorOf(trainer);
            var head = services.Sprites.Get(run.Player.Form.Id)?.HeadHeight ?? 20;
            var center = run.Player.Position.ToXna() - new Vector2(0, head / 2f + 1f);
            switch (trainer.Active.Kind)
            {
                case TrainerActiveKind.Shield:
                {
                    var radius = MathF.Max(16f, head * 0.75f + 6f);
                    var flash = _blockFlash / (float)BlockFlashTicks;
                    var left = TrainerSystem.EffectLeft(run);
                    var blink = left < 0.15f && _tick / 4 % 2 == 0 ? 0.4f : 1f;
                    var rimColor = Color.Lerp(Color.Lerp(color, Color.White, 0.45f), Color.White, flash);
                    p.Ellipse(batch, center, (int)(radius * 2f), (int)(radius * 2f), rimColor * ((0.18f + 0.3f * flash) * blink));
                    p.Ring(batch, center, radius, radius, Color.Lerp(color, Ink, 0.3f) * (0.9f * blink), 2);
                    for (var rim = 0; rim < run.TrainerState.ShieldCharges; rim++)
                        p.Ring(batch, center, radius - 1f - rim * 3f, radius - 1f - rim * 3f, rimColor * ((0.95f - rim * 0.25f) * blink), rim == 0 ? 2 : 1);

                    // A glint sliding round the rim.
                    var glint = _tick * 0.08f;
                    p.Rect(batch, new Rectangle((int)(center.X + MathF.Cos(glint) * radius) - 1, (int)(center.Y + MathF.Sin(glint) * radius) - 1, 2, 2), Color.White * (0.9f * blink));
                    break;
                }

                case TrainerActiveKind.Frenzy:
                {
                    // Around the feet, flattened like the ground, so they never cross the face.
                    var feet = run.Player.Position.ToXna();
                    var radius = MathF.Max(18f, head * 0.8f + 6f);
                    for (var hand = 0; hand < 2; hand++)
                    {
                        var start = _tick * 0.22f + hand * MathF.PI;
                        for (var s = 0; s < 14; s++)
                        {
                            var a = start - s * 0.08f;
                            var alpha = 0.95f * (1f - s / 14f);
                            var spot = feet + new Vector2(MathF.Cos(a) * radius, MathF.Sin(a) * radius * 0.45f);
                            var size = s < 5 ? 3 : 2;
                            p.Rect(batch, new Rectangle((int)spot.X - 1, (int)spot.Y - 1, size, size), Color.Lerp(Color.White, color, s / 8f) * alpha);
                        }
                    }

                    break;
                }
            }
        }

        _particles.Draw(batch, p);
    }

    /// <summary>Whether the player is see-through now (Intangible).</summary>
    public static bool Ghost(RunState run) => TrainerSystem.Running(run, TrainerActiveKind.Intangible);

    /// <summary>
    /// Screen space, after the world: violet edges while time is slowed (pulsing in its last second), and the brief tint
    /// of a status storm.
    /// </summary>
    public void DrawScreen(SpriteBatch batch, RunState run, int width, int height)
    {
        var p = services.Primitives;
        if (TrainerSystem.Running(run, TrainerActiveKind.SlowTime))
        {
            var left = TrainerSystem.EffectLeft(run);
            var fade = left < 0.15f ? 0.5f + 0.5f * MathF.Sin(_tick * 0.6f) : 1f;
            const int band = 28;
            for (var i = 0; i < band; i += 2)
            {
                var alpha = 0.32f * (1f - i / (float)band) * fade;
                var c = Violet * alpha;
                p.Rect(batch, new Rectangle(0, i, width, 2), c);
                p.Rect(batch, new Rectangle(0, height - i - 2, width, 2), c);
                p.Rect(batch, new Rectangle(i, 0, 2, height), c);
                p.Rect(batch, new Rectangle(width - i - 2, 0, 2, height), c);
            }
        }

        if (_stormAge is >= 0 and < 12)
            p.Rect(batch, new Rectangle(0, 0, width, height), _stormColor * (0.18f * (1f - _stormAge / 12f)));
    }

    /// <summary>A streak rising beside the body, never in front of the face.</summary>
    private void Rise(Vector2 feet, Color color, float spread)
    {
        var x = (_random.Next(2) == 0 ? -1f : 1f) * spread * (0.65f + 0.35f * _random.NextSingle());
        _particles.Emit(feet + new Vector2(x, -_random.NextSingle() * 6f), new Vector2(0, -40f - _random.NextSingle() * 30f),
            Color.Lerp(color, Color.White, 0.5f), color, 24, 2f, 0.97f, stretch: 2f);
    }

    private readonly record struct Afterimage(Vector2 Feet, int SpeciesId, PmdAnim Anim, int Frame, Direction8 Facing, int Age);
}
