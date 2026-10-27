using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PokeRogue.Core.Run;
using PokeRogue.Core.Run.Plates;
using PokeRogue.Game.Graphics;

namespace PokeRogue.Game.Vfx;

/// <summary>
/// What Arceus' Plates show in the field (phase 9.1): Lastraidro's wave, Lastrasaetta's bolt from the sky, Lastrarogo's
/// blasts, the poison passing from one Pokémon to the next, Lastratetra's wisps when a hit goes through, Lastragelo's
/// cold ring around the feet, and rings of the Plate's colour when one is found or chosen. Coloured light is
/// alpha-blended, never over the player's face.
/// </summary>
public sealed class PlateVfx(GameServices services)
{
    private const int RingTicks = 28;
    private const int BoltTicks = 12;
    private const int BlastTicks = 22;
    private const int SpreadTicks = 16;
    private const int FoundTicks = 50;

    private static readonly Color WaterLight = new(120, 200, 255);
    private static readonly Color WaterDeep = new(40, 110, 220);
    private static readonly Color BoltCore = new(255, 252, 210);
    private static readonly Color BoltGlow = new(250, 220, 60);
    private static readonly Color FireHot = new(255, 226, 120);
    private static readonly Color Fire = new(240, 110, 40);
    private static readonly Color Smoke = new(70, 50, 50);
    private static readonly Color Poison = new(170, 90, 210);
    private static readonly Color Ghost = new(170, 140, 230);
    private static readonly Color Ice = new(190, 235, 255);

    private readonly Particles _particles = new(512);
    private readonly Random _random = new(19);
    private readonly List<Effect> _effects = [];
    private int _tick;

    public int ParticleCount => _particles.Active;

    public void Reset()
    {
        _effects.Clear();
        _tick = 0;
    }

    public void OnEvent(in RunEvent e)
    {
        if (e.Type != RunEventType.PlateEffect || services.Plates.Value.Find(e.MoveId) is not { } plate)
            return;

        var at = e.Position.ToXna();
        var color = TypePalette.Of(plate.Type);
        if (e.Value < 0f)
        {
            // Found on the map (-1) or chosen from a chest (-2): rings of its colour from the player's feet.
            _effects.Add(new Effect(EffectKind.Found, at, at, 0f, color));
            _particles.Burst(at - new Vector2(0, 10), 26, 120f, Color.White, color, 34, _random, size: 2f);
            return;
        }

        switch ((PlateEffectKind)(int)e.Value)
        {
            case PlateEffectKind.Tide:
                _effects.Add(new Effect(EffectKind.Tide, at, at, e.Target.X, color));
                for (var i = 0; i < 24; i++)
                {
                    var angle = i * MathF.Tau / 24f;
                    var direction = new Vector2(MathF.Cos(angle), MathF.Sin(angle) * 0.55f);
                    _particles.Emit(at + direction * 10f, direction * 260f, WaterLight, WaterDeep * 0.6f, 22, 2f, 0.9f);
                }

                break;
            case PlateEffectKind.Bolt:
                _effects.Add(new Effect(EffectKind.Bolt, e.Target.ToXna(), at, _random.Next(1000), color));
                _particles.Burst(at, 12, 120f, BoltCore, BoltGlow, 18, _random, size: 2f);
                break;
            case PlateEffectKind.BurnBlast:
                _effects.Add(new Effect(EffectKind.Blast, at, at, e.Target.X, color));
                for (var i = 0; i < 14; i++)
                {
                    var angle = _random.NextSingle() * MathF.Tau;
                    var speed = 40f + _random.NextSingle() * 90f;
                    _particles.Emit(at, new Vector2(MathF.Cos(angle), MathF.Sin(angle) * 0.6f) * speed - new Vector2(0, 30), FireHot, Fire * 0.5f,
                        16 + _random.Next(10), 3f, 0.86f, gravity: -2f, stretch: 1.6f);
                }

                for (var i = 0; i < 6; i++)
                    _particles.Emit(at + new Vector2(_random.Next(-8, 9), -4), new Vector2(_random.Next(-20, 21), -40), Smoke * 0.6f, Smoke * 0f, 30, 4f, 0.92f);
                break;
            case PlateEffectKind.Contagion:
                _effects.Add(new Effect(EffectKind.Spread, e.Target.ToXna(), at, 0f, color));
                break;
            case PlateEffectKind.Phantom:
                for (var i = 0; i < 10; i++)
                {
                    var side = i % 2 == 0 ? -1f : 1f;
                    _particles.Emit(at + new Vector2(side * (6 + _random.Next(6)), -6 - _random.Next(14)), new Vector2(side * 30f, -30f - _random.Next(20)),
                        Ghost, Ghost * 0f, 26, 2f, 0.9f);
                }

                break;
        }
    }

    public void Update()
    {
        _tick++;
        _particles.Update();
        for (var i = _effects.Count - 1; i >= 0; i--)
        {
            var effect = _effects[i] with { Age = _effects[i].Age + 1 };
            var life = effect.Kind switch
            {
                EffectKind.Tide => RingTicks,
                EffectKind.Bolt => BoltTicks,
                EffectKind.Blast => BlastTicks,
                EffectKind.Spread => SpreadTicks,
                _ => FoundTicks,
            };
            if (effect.Age >= life)
                _effects.RemoveAt(i);
            else
                _effects[i] = effect;
        }
    }

    /// <summary>Lastragelo's cold ring around the feet and the scorch of the blasts. World space, alpha batch, under everyone.</summary>
    public void DrawGround(SpriteBatch batch, RunState run)
    {
        var p = services.Primitives;
        if (PlateSystem.With(run, PlateEffectKind.Chill) is { } ice && !run.IsOver)
        {
            var feet = run.Player.Position.ToXna();
            var radius = ice.Effect.Radius;
            var beat = 0.75f + 0.25f * MathF.Sin(_tick * 0.06f);
            p.Ring(batch, feet, radius, radius * 0.5f, Ice * (0.45f * beat), 2);
            p.Ring(batch, feet, radius - 5f, (radius - 5f) * 0.5f, Ice * (0.2f * beat));

            // A few frost glints drifting on the ring.
            if (_tick % 6 == 0)
            {
                var angle = _random.NextSingle() * MathF.Tau;
                _particles.Emit(feet + new Vector2(MathF.Cos(angle) * radius, MathF.Sin(angle) * radius * 0.5f), new Vector2(0, -8f), Color.White, Ice * 0f, 30, 1f, 0.98f);
            }
        }

        foreach (var effect in _effects)
        {
            if (effect.Kind != EffectKind.Blast)
                continue;

            var t = effect.Age / (float)BlastTicks;
            var size = (int)(effect.Radius * 2f * MathF.Min(1f, 0.4f + t * 2f));
            p.Ellipse(batch, effect.To, size, size / 2, Smoke * (0.35f * (1f - t)));
        }
    }

    /// <summary>Waves, bolts, blasts and rings. World space, alpha batch, over the Pokémon.</summary>
    public void DrawObjects(SpriteBatch batch)
    {
        var p = services.Primitives;
        foreach (var effect in _effects)
        {
            switch (effect.Kind)
            {
                case EffectKind.Tide:
                {
                    // Two flattened rings racing out to the wave's reach, light then deep blue.
                    for (var ring = 0; ring < 2; ring++)
                    {
                        var t = MathHelper.Clamp((effect.Age - ring * 5) / (float)(RingTicks - 6), 0f, 1f);
                        if (t <= 0f || t >= 1f)
                            continue;
                        var radius = 10f + (effect.Radius - 10f) * (1f - (1f - t) * (1f - t));
                        p.Ring(batch, effect.To, radius, radius * 0.55f, (ring == 0 ? WaterLight : WaterDeep) * (0.8f * (1f - t)), 3);
                    }

                    break;
                }
                case EffectKind.Bolt:
                    DrawBolt(batch, effect);
                    break;
                case EffectKind.Blast:
                {
                    var t = effect.Age / (float)BlastTicks;
                    var radius = effect.Radius * MathF.Min(1f, 0.3f + t * 2.2f);
                    if (effect.Age < 5)
                        p.Ellipse(batch, effect.To - new Vector2(0, 4), (int)(radius * 1.2f), (int)(radius * 0.8f), FireHot * (0.7f * (1f - effect.Age / 5f)));
                    p.Ring(batch, effect.To, radius, radius * 0.55f, Fire * (0.75f * (1f - t)), 2);
                    break;
                }
                case EffectKind.Spread:
                {
                    // A purple drop arcing from the fallen Pokémon to the next one.
                    var t = effect.Age / (float)SpreadTicks;
                    var at = Vector2.Lerp(effect.From, effect.To, t) - new Vector2(0, MathF.Sin(t * MathF.PI) * 14f + 8f);
                    p.Ellipse(batch, at, 4, 4, Poison);
                    p.Rect(batch, at - new Vector2(1, 1), Vector2.One, Color.White * 0.8f);
                    if (effect.Age == SpreadTicks - 1)
                        _particles.Burst(effect.To - new Vector2(0, 8), 6, 50f, Poison, Poison * 0f, 16, _random, size: 2f);
                    break;
                }
                case EffectKind.Found:
                {
                    for (var ring = 0; ring < 3; ring++)
                    {
                        var a = effect.Age - ring * 7;
                        var t = a / (FoundTicks * 0.6f);
                        if (a < 0 || t > 1f)
                            continue;
                        var radius = 8f + a * 2.8f;
                        p.Ring(batch, effect.To, radius, radius * 0.5f, Color.Lerp(Color.White, effect.Color, t) * (1f - t), 2);
                    }

                    break;
                }
            }
        }

        _particles.Draw(batch, p);
    }

    /// <summary>A jagged bolt from above the screen down to the enemy, white inside a yellow glow, flickering out.</summary>
    private void DrawBolt(SpriteBatch batch, Effect effect)
    {
        var p = services.Primitives;
        var fade = 1f - effect.Age / (float)BoltTicks;
        if (effect.Age % 4 == 3)
            fade *= 0.4f;

        var random = new Random((int)effect.Radius + effect.Age / 3);
        var from = effect.To - new Vector2(random.Next(-20, 21), 150f);
        var previous = from;
        const int segments = 7;
        for (var i = 1; i <= segments; i++)
        {
            var t = i / (float)segments;
            var point = Vector2.Lerp(from, effect.To, t) + (i < segments ? new Vector2(random.Next(-7, 8), 0) : Vector2.Zero);
            UiDraw.Line(batch, p, previous, point, BoltGlow * (0.5f * fade), 4f);
            UiDraw.Line(batch, p, previous, point, BoltCore * fade, 2f);
            previous = point;
        }

        if (effect.Age < 4)
            p.Ellipse(batch, effect.To, 18, 9, BoltCore * (0.6f * fade));
    }

    private enum EffectKind
    {
        Tide,
        Bolt,
        Blast,
        Spread,
        Found,
    }

    /// <summary>One effect: From → To for the bolt and the poison; To is the centre of the others.</summary>
    private readonly record struct Effect(EffectKind Kind, Vector2 From, Vector2 To, float Radius, Color Color, int Age = 0);
}
