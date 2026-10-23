using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PokeRogue.Core.Data;
using PokeRogue.Core.Run;
using PokeRogue.Core.Run.Moves;
using PokeRogue.Game.Graphics;

namespace PokeRogue.Game.Vfx;

/// <summary>Look of a move's effects. Defaults come from the archetype; "vfx" in moves.json can pick another.</summary>
public enum FxStyle
{
    Electric,
    Orb,
    Blade,
    Bone,
    Leaf,
    Cloud,
    Vine,
    Aurora,
    Rock,
    Puddle,
    Flames,
    Quake,

    // Phase 2.4, group 1.
    Star,
    Sonic,
    Jet,
    Bubble,
    Static,
    Shard,

    // Phase 2.4, group 2.
    Chop,
    Sphere,
    Needle,
    Wind,
    Psywave,
    Hypno,

    // Phase 2.4, group 3.
    Scissor,
    Drain,
    Pillars,
    Shadow,
    Shade,
    Twister,
    Meteor,

    // Phase 2.4, group 4.
    Bite,
    DarkPulse,
    Gyro,
    Flash,

    // Phase 2.6, evolved moves.
    Thunder,
    Flamethrower,
    AirBlade,
    BoneRush,
    LeafStorm,
    Toxic,
    PowerWhip,
    IceBeam,
    StoneEdge,
    Eruption,
    FlareBlitz,
    Fissure,
}

/// <summary>
/// Procedural pixel-art effects for every move archetype (approved style A, phase 0.6), coloured by the move's type.
/// Three layers: <see cref="DrawGround"/> under the Pokémon, <see cref="DrawObjects"/> over them,
/// <see cref="DrawGlow"/> additive on top. The styles added in phase 2.4 live in one partial file per type.
/// </summary>
public sealed partial class MoveVfx
{
    /// <summary>Flying effects are drawn this far above the feet, roughly at body height.</summary>
    public const float BodyHeight = 12f;

    private static readonly Color MudDark = new(66, 46, 28);
    private static readonly Color MudMain = new(108, 78, 44);
    private static readonly Color MudLight = new(158, 122, 78);
    private static readonly Color Smoke = new(92, 88, 96);

    private readonly GameServices _services;
    private readonly MoveShapes _shapes;
    private readonly Random _random = new(21);
    private readonly Particles _glow = new(3072);
    private readonly Particles _dust = new(2048);

    /// <summary>Fire and other coloured matter, alpha-blended so oranges stay orange on green grass (additive washes them to yellow).</summary>
    private readonly Particles _flames = new(3072);
    private readonly List<SlashFx> _slashes = [];
    private readonly List<TimedFx> _timed = [];
    private readonly List<ArcFx> _arcs = [];
    private readonly List<Bubble> _bubbles = [];
    private readonly HashSet<int> _seenZones = [];
    private readonly HashSet<int> _seenRings = [];
    private readonly Dictionary<string, FxStyle> _styles = new(StringComparer.Ordinal);
    private int _tick;

    public MoveVfx(GameServices services)
    {
        _services = services;
        _shapes = new MoveShapes(services.Device);
    }

    public ScreenShake Shake { get; } = new();

    public int ParticleCount => _glow.Active + _dust.Active + _flames.Active;

    public static FxStyle DefaultStyle(MoveDefinition move)
    {
        if (move.Vfx is { } id && Enum.TryParse<FxStyle>(id, ignoreCase: true, out var chosen))
            return chosen;

        return move.Archetype switch
        {
            _ when move.Type == PokemonType.Electric && move.Archetype is MoveArchetype.Arc or MoveArchetype.Projectile => FxStyle.Electric,
            MoveArchetype.Homing => FxStyle.Blade,
            MoveArchetype.Boomerang => FxStyle.Bone,
            MoveArchetype.Orbit => FxStyle.Leaf,
            MoveArchetype.Aura => FxStyle.Cloud,
            MoveArchetype.Slash => FxStyle.Vine,
            MoveArchetype.Beam => FxStyle.Aurora,
            MoveArchetype.Strike => FxStyle.Rock,
            MoveArchetype.Zone => move.SpawnAtPlayer ? FxStyle.Flames : FxStyle.Puddle,
            MoveArchetype.Ring => FxStyle.Quake,
            MoveArchetype.Arc => FxStyle.Electric,
            _ => FxStyle.Orb,
        };
    }

    public void Reset()
    {
        _slashes.Clear();
        _timed.Clear();
        _arcs.Clear();
        _bubbles.Clear();
        _seenZones.Clear();
        _seenRings.Clear();
        Shake.Reset();
    }

    private FxStyle Style(MoveDefinition move)
    {
        if (!_styles.TryGetValue(move.Id, out var style))
            _styles[move.Id] = style = DefaultStyle(move);
        return style;
    }

    private (FxStyle Style, TypeFxPalette Palette, PokemonType Type)? Lookup(string? moveId)
    {
        if (moveId is null || _services.Moves.Value.All.FirstOrDefault(m => m.Id == moveId) is not { } move)
            return null;
        return (Style(move), TypeFx.For(move.Type), move.Type);
    }

    // ---------------------------------------------------------------- events

    public void OnEvent(in RunEvent e, RunState run)
    {
        switch (e.Type)
        {
            case RunEventType.EnemyHit:
                Impact(e, run);
                break;
            case RunEventType.ArcSegment:
            {
                var style = Lookup(e.MoveId)?.Style ?? FxStyle.Electric;
                _arcs.Add(new ArcFx(e.Position.ToXna() - new Vector2(0, BodyHeight), e.Target.ToXna() - new Vector2(0, BodyHeight), _random.Next(), Style: style));
                if (style == FxStyle.Thunder)
                    ThunderStrike(e.Target.ToXna());
                break;
            }
            case RunEventType.Slash:
                _slashes.Add(new SlashFx(e.Position.ToXna() - new Vector2(0, BodyHeight * 0.5f), e.Target.ToXna(), e.Value, MoveId: e.MoveId));
                break;
            case RunEventType.AuraPulse:
                AuraPulse(e);
                break;
            case RunEventType.StrikeImpact:
                StrikeImpact(e);
                break;
            case RunEventType.PlayerHurt:
                Shake.Add(0.12f);
                break;
        }
    }

    private void Impact(in RunEvent e, RunState run)
    {
        var at = e.Position.ToXna() - new Vector2(0, BodyHeight);
        if (Lookup(e.MoveId) is not { } info)
        {
            _glow.Burst(at, 5, 110f, Color.White, new Color(170, 170, 190), 12, _random);
            return;
        }

        var (style, pal, _) = info;
        var big = e.Critical ? 2 : 1;
        switch (style)
        {
            case FxStyle.Electric:
            case FxStyle.Thunder:
                ElectricFx.ImpactSparks(_glow, at, _random, 7 * big);
                break;
            case FxStyle.Orb:
            case FxStyle.Flames:
            case FxStyle.Flamethrower:
            case FxStyle.FlareBlitz:
                _flames.Burst(at, 8 * big, 110f, pal.Main, pal.Dark, 14, _random, size: 3f);
                _glow.Burst(at, 4 * big, 90f, pal.Hot * 0.6f, pal.Main * 0.2f, 8, _random, size: 1f);
                for (var i = 0; i < 2 * big; i++)
                    _dust.Emit(at + Jitter(3), new Vector2(Rand(-8, 8), Rand(-28, -14)), Smoke * 0.8f, Smoke * 0.2f, 26, 2f, drag: 0.95f);
                break;
            case FxStyle.Blade:
            case FxStyle.Aurora:
            case FxStyle.AirBlade:
                _timed.Add(new TimedFx(FxKind.Star, at, Vector2.Zero, 1f, style == FxStyle.Aurora ? pal.Hot : Color.White, Life: 8));
                _glow.Burst(at, 6 * big, 130f, pal.Hot, pal.Main, 12, _random, size: style == FxStyle.Aurora ? 2f : 1f);
                break;
            case FxStyle.Leaf:
            case FxStyle.Vine:
            case FxStyle.LeafStorm:
            case FxStyle.PowerWhip:
                _timed.Add(new TimedFx(FxKind.CutMark, at, Vector2.Zero, Rand(0f, MathF.PI), pal.Hot, Life: 9));
                _dust.Burst(at, 3 * big, 70f, pal.Main, pal.Dark, 14, _random, size: 2f);
                break;
            case FxStyle.Cloud:
            case FxStyle.Toxic:
                for (var i = 0; i < 3 * big; i++)
                    _dust.Emit(at + Jitter(4), new Vector2(Rand(-6, 6), Rand(-24, -10)), pal.Main, pal.Dark * 0.4f, 22, 2f, drag: 0.94f);
                break;
            case FxStyle.Bone:
            case FxStyle.Rock:
            case FxStyle.Quake:
            case FxStyle.BoneRush:
            case FxStyle.StoneEdge:
            case FxStyle.Eruption:
            case FxStyle.Fissure:
                _dust.Burst(at + new Vector2(0, BodyHeight * 0.6f), 6 * big, 80f, pal.Spark, pal.Dark * 0.5f, 18, _random, size: 2f);
                break;
            case FxStyle.IceBeam:
                ShardImpact(at, big);
                break;
            case FxStyle.Puddle:
                for (var i = 0; i < 4 * big; i++)
                    _dust.Emit(at, new Vector2(Rand(-60, 60), Rand(-80, -30)), MudLight, MudDark, 16, 2f, drag: 0.92f, gravity: 6f);
                break;
            case FxStyle.Star:
                StarImpact(at, big);
                break;
            case FxStyle.Sonic:
                _glow.Burst(at, 4 * big, 90f, Color.White, SoundLilac, 10, _random);
                break;
            case FxStyle.Jet:
            case FxStyle.Bubble:
                Splash(at, big, style == FxStyle.Bubble);
                break;
            case FxStyle.Static:
                ElectricFx.ImpactSparks(_glow, at, _random, 3 * big);
                break;
            case FxStyle.Shard:
                ShardImpact(at, big);
                break;
            case FxStyle.Chop:
                ChopImpact(at, big);
                break;
            case FxStyle.Sphere:
                SphereImpact(at, big);
                break;
            case FxStyle.Needle:
                _dust.Burst(at, 4 * big, 70f, PoisonPalette.Main, PoisonPalette.Dark, 12, _random);
                break;
            case FxStyle.Wind:
                _dust.Burst(at, 3 * big, 80f, Color.White * 0.8f, new Color(150, 200, 110) * 0.3f, 10, _random);
                break;
            case FxStyle.Psywave:
                _glow.Burst(at, 3 * big, 70f, PsychicPalette.Hot, PsychicPalette.Main, 10, _random);
                break;
            case FxStyle.Hypno:
                _glow.Burst(at, 3, 50f, HypnoViolet, PsychicPalette.Main * 0.3f, 12, _random);
                break;
            case FxStyle.Scissor:
            case FxStyle.Twister:
                _glow.Burst(at, 4 * big, 100f, pal.Hot, pal.Main, 10, _random);
                break;
            case FxStyle.Drain:
                DrainImpact(at, run.Player.Position.ToXna() - new Vector2(0, BodyHeight), big);
                break;
            case FxStyle.Pillars:
                _dust.Burst(at + new Vector2(0, BodyHeight * 0.6f), 4 * big, 70f, pal.Spark, pal.Dark * 0.5f, 14, _random, size: 2f);
                break;
            case FxStyle.Shadow:
                ShadowImpact(at, big);
                break;
            case FxStyle.Shade:
                _dust.Burst(at, 3 * big, 50f, GhostPalette.Dark, Night * 0.3f, 12, _random, size: 2f);
                break;
            case FxStyle.Meteor:
                _flames.Burst(at, 4 * big, 90f, pal.Main, pal.Dark, 12, _random, size: 2f);
                break;
            case FxStyle.Bite:
                _timed.Add(new TimedFx(FxKind.Star, at, Vector2.Zero, 1f, Color.White, Life: 6));
                _dust.Burst(at, 4 * big, 70f, Night, Night * 0.3f, 12, _random, size: 2f);
                break;
            case FxStyle.DarkPulse:
                _dust.Burst(at, 3 * big, 60f, DarkViolet, Night * 0.3f, 12, _random);
                break;
            case FxStyle.Gyro:
                GyroClang(at, big);
                break;
            case FxStyle.Flash:
                _glow.Burst(at, 5 * big, 110f, Color.White, SteelPalette.Main, 10, _random);
                break;
        }
    }

    private void AuraPulse(in RunEvent e)
    {
        switch (Lookup(e.MoveId)?.Style)
        {
            case FxStyle.Static:
                StaticPulse(e.Position.ToXna(), e.Value);
                return;
            case FxStyle.DarkPulse:
                _timed.Add(new TimedFx(FxKind.DarkRing, e.Position.ToXna(), Vector2.Zero, e.Value, Night, Life: 20));
                return;
            case FxStyle.Toxic:
                ToxicPulse(e.Position.ToXna(), e.Value);
                return;
        }

        var pal = Lookup(e.MoveId)?.Palette ?? TypeFx.For(PokemonType.Poison);
        var center = e.Position.ToXna();
        _timed.Add(new TimedFx(FxKind.AuraPulse, center, Vector2.Zero, e.Value, pal.Main, Life: 14));
        for (var i = 0; i < 12; i++)
        {
            var angle = i * MathF.Tau / 12f + Rand(-0.2f, 0.2f);
            var dir = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
            _dust.Emit(center + dir * e.Value * 0.6f, dir * Rand(30, 50), pal.Main * 0.9f, pal.Dark * 0.3f, 18, 3f, drag: 0.9f);
        }
    }

    private void StrikeImpact(in RunEvent e)
    {
        switch (Lookup(e.MoveId)?.Style)
        {
            case FxStyle.Psywave:
                PsyBurst(e.Position.ToXna(), e.Value);
                return;
            case FxStyle.Meteor:
                MeteorImpact(e.Position.ToXna(), e.Value);
                return;
            case FxStyle.StoneEdge:
                StoneEdgeImpact(e.Position.ToXna(), e.Value);
                return;
        }

        var pal = Lookup(e.MoveId)?.Palette ?? TypeFx.For(PokemonType.Rock);
        var at = e.Position.ToXna();
        _timed.Add(new TimedFx(FxKind.DustRing, at, Vector2.Zero, e.Value, pal.Spark, Life: 14));
        for (var i = 0; i < 12; i++)
        {
            var angle = Rand(0f, MathF.Tau);
            var dir = new Vector2(MathF.Cos(angle), MathF.Sin(angle) * 0.6f);
            var color = i % 3 == 0 ? pal.Dark : pal.Main;
            _dust.Emit(at + dir * 4, dir * Rand(50, 110) + new Vector2(0, -60), color, color * 0.6f, 20, i % 4 == 0 ? 3f : 2f, drag: 0.93f, gravity: 7f);
        }

        Shake.Add(0.22f);
    }

    // ---------------------------------------------------------------- per tick

    public void Update(RunState run, bool paused)
    {
        _tick++;
        Shake.Update();
        _glow.Update();
        _dust.Update();
        _flames.Update();
        Age(_slashes, s => s with { Age = s.Age + 1 }, s => s.Age >= SlashFx.Life);
        Age(_timed, t => t with { Age = t.Age + 1 }, t => t.Age >= t.Life);
        Age(_arcs, a => a with { Age = a.Age + 1 }, a => a.Age >= ArcFx.Life);
        UpdateBubbles();
        if (paused)
            return;

        var moves = run.MoveSystem;
        foreach (ref readonly var p in moves.Projectiles.Items)
        {
            if (!p.Active || p.Move is null)
                continue;

            var pal = TypeFx.For(p.Move.Definition.Type);
            var at = p.Position.ToXna() - new Vector2(0, BodyHeight);
            switch (Style(p.Move.Definition))
            {
                case FxStyle.Electric when _tick % 2 == 0:
                    ElectricFx.Trail(_glow, at, _random);
                    break;
                case FxStyle.Orb:
                {
                    // Flame tail: chunky particles that start at the body colour and cool down to the dark tone.
                    var back = -Vector2.Normalize(p.Velocity.ToXna() + new Vector2(0.001f, 0));
                    _flames.Emit(at + back * 5 + Jitter(2), back * 20f + new Vector2(Rand(-8, 8), Rand(-22, -8)), pal.Main, pal.Dark, 12 + _random.Next(6), 4f, drag: 0.9f);
                    _flames.Emit(at + back * 3 + Jitter(1), back * 10f, pal.Hot, pal.Main, 6, 2f, drag: 0.9f);
                    break;
                }
                case FxStyle.Blade when _tick % 2 == 0:
                    _glow.Emit(at + Jitter(2), Vector2.Zero, pal.Spark, pal.Dark, 8, 1f);
                    break;
                case FxStyle.Leaf when _tick % 7 == 0:
                    _dust.Emit(at + Jitter(3), new Vector2(Rand(-10, 10), Rand(-6, 6)), pal.Main, pal.Dark * 0.4f, 16, 1f);
                    break;
                case FxStyle.Star:
                    StarTrail(at);
                    break;
                case FxStyle.Shard:
                    ShardTrail(OrbitAt(p));
                    break;
                case FxStyle.Sphere:
                    SphereTrail(at);
                    break;
                case FxStyle.Needle:
                    NeedleTrail(at);
                    break;
                case FxStyle.Shadow:
                    ShadowTrail(at);
                    break;
                case FxStyle.Twister when _tick % 3 == 0:
                    TwisterDust(p.Position.ToXna());
                    break;
                case FxStyle.Gyro when _tick % 2 == 0:
                    GyroSparks(at, p.Velocity.ToXna());
                    break;
                case FxStyle.AirBlade when _tick % 2 == 0:
                    AirBladeTrail(at, p.Velocity.ToXna());
                    break;
                case FxStyle.BoneRush when _tick % 3 == 0:
                    BoneRushDust(p.Position.ToXna());
                    break;
                case FxStyle.LeafStorm when (_tick + p.Serial) % 5 == 0:
                    LeafStormBits(p);
                    break;
            }
        }

        foreach (var zone in moves.Zones)
            UpdateZone(zone);

        foreach (var pending in moves.Pending)
        {
            if (pending.Kind == PendingKind.Strike && Style(pending.Move.Definition) == FxStyle.Meteor)
                MeteorTrail(MeteorAt(pending, Progress(run, pending)));
        }

        foreach (var ring in moves.Rings)
        {
            var pal = TypeFx.For(ring.Move.Definition.Type);
            var fresh = _seenRings.Add(ring.Serial);
            switch (Style(ring.Move.Definition))
            {
                case FxStyle.Sonic:
                    // A shout, not a quake: only a nudge of the camera.
                    if (fresh)
                        Shake.Add(0.1f);
                    continue;
                case FxStyle.Wind:
                    UpdateWind(ring);
                    continue;
                case FxStyle.Hypno:
                    UpdateHypno(ring);
                    continue;
                case FxStyle.Fissure:
                    UpdateFissure(ring, fresh);
                    continue;
            }

            if (fresh)
                Shake.Add(0.32f);

            for (var i = 0; i < 3; i++)
            {
                var angle = Rand(0f, MathF.Tau);
                var dir = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
                var color = i == 0 ? pal.Dark : pal.Main;
                _dust.Emit(ring.Center.ToXna() + dir * ring.Radius, dir * 25f + new Vector2(0, -25), color, color * 0.4f, 14, 2f, drag: 0.92f, gravity: 4f);
            }
        }

        foreach (var beam in moves.Beams)
        {
            switch (Style(beam.Move.Definition))
            {
                case FxStyle.Jet:
                    UpdateJet(beam);
                    continue;
                case FxStyle.Shade:
                    continue;
                case FxStyle.Flash:
                    UpdateFlash(beam);
                    continue;
                case FxStyle.Flamethrower:
                    UpdateFlamethrower(beam);
                    continue;
                case FxStyle.IceBeam:
                    UpdateIceBeam(beam);
                    continue;
            }

            var pal = TypeFx.For(beam.Move.Definition.Type);
            var origin = beam.Origin.ToXna() - new Vector2(0, BodyHeight);
            for (var i = 0; i < 2; i++)
            {
                var along = Rand(0f, beam.Length);
                _glow.Emit(origin + beam.Direction.ToXna() * along + Jitter(beam.HalfWidth + 2), Jitter(10), pal.Hot, pal.Main, 10, 1f);
            }
        }

        // A Fire trail move wraps the player in flames ("Ruotafuoco"); Toxic keeps its swamp bubbling.
        foreach (var move in run.Player.Moves)
        {
            switch (Style(move.Definition))
            {
                case FxStyle.FlareBlitz:
                    FlareCloak(run.Player.Position.ToXna());
                    continue;
                case FxStyle.Toxic:
                    ToxicBubbles(run.Player.Position.ToXna(), move.Stats.Radius * run.Player.AreaMultiplier);
                    continue;
            }

            if (move.Definition.Archetype == MoveArchetype.Zone && Style(move.Definition) == FxStyle.Flames)
            {
                // A wheel of fire spinning around the feet.
                var pal = TypeFx.For(move.Definition.Type);
                var angle = _tick * 0.35f + (_tick % 2) * MathF.PI;
                var feet = run.Player.Position.ToXna();
                _flames.Emit(feet + new Vector2(MathF.Cos(angle) * 11f, MathF.Sin(angle) * 5f - 3f), new Vector2(Rand(-4, 4), Rand(-24, -12)), pal.Main, pal.Dark, 12, 3f, drag: 0.92f, stretch: 2f);
                if (_tick % 3 == 0)
                    _flames.Emit(feet + new Vector2(MathF.Cos(angle) * 11f, MathF.Sin(angle) * 5f - 4f), new Vector2(0, -16), pal.Hot, pal.Main, 7, 2f, drag: 0.92f, stretch: 2f);
            }
        }
    }

    private void UpdateZone(MoveZone zone)
    {
        var center = zone.Position.ToXna();
        var style = Style(zone.Move.Definition);
        var pal = TypeFx.For(zone.Move.Definition.Type);
        var fresh = _seenZones.Add(zone.Serial);

        switch (style)
        {
            case FxStyle.Pillars:
                UpdatePillars(zone, fresh);
                return;
            case FxStyle.Eruption:
                UpdateEruption(zone, fresh);
                return;
            case FxStyle.FlareBlitz:
                UpdateFlareTrail(zone);
                return;
        }

        if (style == FxStyle.Puddle)
        {
            if (fresh)
            {
                // Splash as the mud lands.
                for (var i = 0; i < 10; i++)
                {
                    var angle = Rand(0f, MathF.Tau);
                    _dust.Emit(center, new Vector2(MathF.Cos(angle), MathF.Sin(angle) * 0.6f) * Rand(40, 90) + new Vector2(0, -50), MudLight, MudDark, 16, 2f, drag: 0.9f, gravity: 6f);
                }
            }

            if (_random.NextSingle() < 0.08f && zone.TicksLeft > 10)
                _bubbles.Add(new Bubble(center + Jitter(zone.Radius * 0.6f) * new Vector2(1f, 0.7f), 0, 18 + _random.Next(10)));
            return;
        }

        // Flames: tongues rising from the patch (body colour cooling to dark, so overlaps stay orange), plus a few embers.
        var strength = MathHelper.Clamp(zone.TicksLeft / 20f, 0f, 1f);
        if (_random.NextSingle() < 0.75f * strength + 0.05f)
        {
            // Upright tongues: orange cooling to red, with a smaller yellow core inside some of them.
            var spot = center + Jitter(zone.Radius * 0.6f) * new Vector2(1f, 0.5f);
            _flames.Emit(spot, new Vector2(Rand(-4, 4), Rand(-30, -16)), pal.Main, pal.Dark, 14 + _random.Next(8), 4f, drag: 0.93f, stretch: 2.2f);
            if (_random.NextSingle() < 0.5f)
                _flames.Emit(spot + new Vector2(0, 1), new Vector2(Rand(-3, 3), Rand(-24, -12)), pal.Hot, pal.Main, 9, 2f, drag: 0.93f, stretch: 2f);
        }

        if (_random.NextSingle() < 0.05f)
            _glow.Emit(center + Jitter(zone.Radius * 0.5f), new Vector2(Rand(-12, 12), Rand(-50, -30)), pal.Spark, pal.Dark, 24, 1f, drag: 0.97f);
    }

    private void UpdateBubbles()
    {
        for (var i = _bubbles.Count - 1; i >= 0; i--)
        {
            var b = _bubbles[i] with { Age = _bubbles[i].Age + 1 };
            if (b.Age < b.Life)
            {
                _bubbles[i] = b;
                continue;
            }

            // Pop.
            var (light, dark) = b.Toxic ? (PoisonPalette.Spark, PoisonPalette.Dark) : (MudLight, MudDark);
            for (var k = 0; k < 3; k++)
                _dust.Emit(b.Position, new Vector2(Rand(-25, 25), Rand(-35, -15)), light, dark, 10, 1f, drag: 0.9f, gravity: 5f);
            _bubbles.RemoveAt(i);
        }
    }

    // ---------------------------------------------------------------- drawing

    /// <summary>Flat effects on the floor, under the Pokémon. Alpha batch with the camera transform.</summary>
    public void DrawGround(SpriteBatch batch, RunState run)
    {
        var p = _services.Primitives;
        var moves = run.MoveSystem;

        // Lingering aura (Smog): a faint area and a slow ring of toxic puffs around the player.
        foreach (var move in run.Player.Moves)
        {
            if (move.Definition.Archetype != MoveArchetype.Aura)
                continue;

            var pal = TypeFx.For(move.Definition.Type);
            var radius = move.Stats.Radius * run.Player.AreaMultiplier;
            var center = run.Player.Position.ToXna();
            if (Style(move.Definition) == FxStyle.Static)
            {
                DrawStaticField(batch, center, radius);
                continue;
            }

            if (Style(move.Definition) == FxStyle.DarkPulse)
            {
                p.Ellipse(batch, center, (int)(radius * 2), (int)(radius * 2), Night * 0.12f);
                continue;
            }

            if (Style(move.Definition) == FxStyle.Toxic)
            {
                DrawToxicSwamp(batch, center, radius);
                continue;
            }
            p.Ellipse(batch, center, (int)(radius * 2), (int)(radius * 2), pal.Dark * 0.12f);
            // Each puff is a small cluster of blobs, so the ring reads as clouds rather than balls.
            var puffs = Math.Max(7, (int)(radius / 6f));
            for (var k = 0; k < puffs; k++)
            {
                var angle = k * MathF.Tau / puffs + _tick * 0.012f;
                var bob = MathF.Sin(_tick * 0.08f + k * 1.7f) * 1.5f;
                var puff = center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * radius * 0.92f + new Vector2(0, bob);
                var tangent = new Vector2(-MathF.Sin(angle), MathF.Cos(angle));
                var size = 8 + Hash(k, 77) % 4;
                p.Ellipse(batch, puff - tangent * 4f, size, size - 2, pal.Dark * 0.45f);
                p.Ellipse(batch, puff + tangent * 4f + new Vector2(0, 1), size - 2, size - 3, pal.Dark * 0.45f);
                p.Ellipse(batch, puff + new Vector2(0, -2), size + 2, size, pal.Main * 0.5f);
                p.Ellipse(batch, puff + new Vector2(-2, -4), size / 2, size / 3, pal.Hot * 0.4f);
            }
        }

        foreach (var zone in moves.Zones)
        {
            var center = zone.Position.ToXna();
            var age = zone.Lifetime - zone.TicksLeft;
            var alpha = MathHelper.Clamp(age / 5f, 0f, 1f) * MathHelper.Clamp(zone.TicksLeft / 15f, 0f, 1f);
            var r = zone.Radius;

            if (Style(zone.Move.Definition) == FxStyle.Pillars)
            {
                DrawCrater(batch, zone, alpha);
            }
            else if (Style(zone.Move.Definition) == FxStyle.Eruption)
            {
                DrawEruptionGround(batch, zone, alpha);
            }
            else if (Style(zone.Move.Definition) == FxStyle.Puddle)
            {
                // Irregular mud blob: lobes around a body, a lighter middle and a wet highlight.
                p.Ellipse(batch, center, (int)(r * 2), (int)(r * 1.5f), MudDark * (0.9f * alpha));
                for (var k = 0; k < 6; k++)
                {
                    var angle = k * MathF.Tau / 6f + zone.Serial;
                    var lobe = center + new Vector2(MathF.Cos(angle), MathF.Sin(angle) * 0.75f) * r * 0.8f;
                    p.Ellipse(batch, lobe, (int)(r * 0.7f), (int)(r * 0.5f), MudDark * (0.9f * alpha));
                }

                p.Ellipse(batch, center + new Vector2(-r * 0.1f, -r * 0.08f), (int)(r * 1.4f), (int)(r * 0.95f), MudMain * (0.9f * alpha));
                p.Ellipse(batch, center + new Vector2(-r * 0.35f, -r * 0.3f), (int)(r * 0.5f), (int)(r * 0.25f), MudLight * (0.7f * alpha));
            }
            else
            {
                var pal = TypeFx.For(zone.Move.Definition.Type);
                p.Ellipse(batch, center, (int)(r * 2), (int)(r * 1.1f), pal.Dark * (0.35f * alpha));
            }
        }

        foreach (var bubble in _bubbles)
        {
            var size = 1 + bubble.Age * 3 / bubble.Life;
            p.Ring(batch, bubble.Position, size, size, (bubble.Toxic ? PoisonPalette.Spark : MudLight) * 0.9f);
        }

        // Flare Blitz: the ground glows under Pikachu while it burns.
        foreach (var move in run.Player.Moves)
        {
            if (Style(move.Definition) == FxStyle.FlareBlitz)
                DrawFlareGround(batch, run.Player.Position.ToXna());
        }

        foreach (var beam in moves.Beams)
        {
            if (Style(beam.Move.Definition) == FxStyle.Shade)
                DrawShade(batch, beam);
        }

        // Strike warning: a shadow growing where the rock (or the meteor, with its glowing mark) will land.
        foreach (var pending in moves.Pending)
        {
            var style = Style(pending.Move.Definition);
            if (pending.Kind == PendingKind.Strike && style == FxStyle.StoneEdge)
            {
                DrawStoneEdgeWarning(batch, pending, Progress(run, pending));
                continue;
            }

            if (pending.Kind != PendingKind.Strike || style is not (FxStyle.Rock or FxStyle.Meteor))
                continue;

            var progress = Progress(run, pending);
            var size = pending.Radius * (0.4f + 0.6f * progress);
            p.Ellipse(batch, pending.Position.ToXna(), (int)(size * 2), (int)size, Color.Black * (0.15f + 0.35f * progress));
            if (style == FxStyle.Meteor)
                DrawMeteorMark(batch, pending, progress);
        }

        // Shock wave: a solid band of churned earth at the ring's edge, rough dust on both sides, a faint wake inside.
        foreach (var ring in moves.Rings)
        {
            if (Style(ring.Move.Definition) == FxStyle.Fissure)
            {
                DrawFissure(batch, ring);
                continue;
            }

            if (Style(ring.Move.Definition) != FxStyle.Quake)
                continue;

            var pal = TypeFx.For(ring.Move.Definition.Type);
            var center = ring.Center.ToXna();
            var fade = MathF.Sqrt(MathHelper.Clamp(1f - ring.Radius / ring.MaxRadius, 0f, 1f));
            p.Ring(batch, center, ring.Radius + 2f, ring.Radius + 2f, pal.Dark * (0.8f * fade), 2);
            p.Ring(batch, center, ring.Radius, ring.Radius, pal.Main * (0.9f * fade), 3);
            p.Ring(batch, center, MathF.Max(1f, ring.Radius - 2f), MathF.Max(1f, ring.Radius - 2f), pal.Spark * (0.7f * fade));

            var steps = Math.Max(24, (int)(MathF.Tau * ring.Radius / 2f));
            for (var i = 0; i < steps; i++)
            {
                var angle = i * MathF.Tau / steps;
                var h = Hash(i, ring.Serial);
                var r = ring.Radius + (h % 100 / 100f * 2f - 1f) * (ring.HalfThickness + 2f);
                var color = ((h >> 8) % 3) switch { 0 => pal.Dark, 1 => pal.Main, _ => pal.Spark };
                var size = (h >> 12) % 4 == 0 ? 3 : 2;
                p.Rect(batch, new Rectangle((int)(center.X + MathF.Cos(angle) * r), (int)(center.Y + MathF.Sin(angle) * r), size, size), color * fade);
            }

            var wake = MathF.Max(1f, ring.Radius - ring.HalfThickness * 2.5f);
            p.Ring(batch, center, wake, wake, pal.Dark * (0.3f * fade), 2);
        }

        foreach (var fx in _timed)
        {
            var t = fx.Age / (float)fx.Life;
            if (fx.Kind == FxKind.ToxicRing)
            {
                // Coloured, so alpha-blended: a purple wave washing out to the edge of the swamp.
                var reach = fx.Value * (0.35f + 0.65f * t);
                p.Ring(batch, fx.Position, reach, reach, fx.Color * (0.8f * (1f - t)), 2);
                continue;
            }

            if (fx.Kind != FxKind.DustRing)
                continue;

            var radius = fx.Value * (0.4f + 0.8f * t);
            p.Ring(batch, fx.Position, radius, radius * 0.55f, fx.Color * (1f - t), 2);
        }
    }

    /// <summary>Solid objects over the Pokémon: bones, leaves, falling rocks, whips, dust. Alpha batch.</summary>
    public void DrawObjects(SpriteBatch batch, RunState run)
    {
        var p = _services.Primitives;
        var moves = run.MoveSystem;

        foreach (ref readonly var projectile in moves.Projectiles.Items)
        {
            if (!projectile.Active || projectile.Move is null)
                continue;

            var at = projectile.Position.ToXna() - new Vector2(0, BodyHeight);
            switch (Style(projectile.Move.Definition))
            {
                case FxStyle.Orb:
                {
                    // Solid fireball: dark rim, body, hot core, white heart (the soft halo is in the glow layer).
                    var pal = TypeFx.For(projectile.Move.Definition.Type);
                    var flicker = (_tick + projectile.Serial) % 4 < 2 ? 1 : 0;
                    p.Ellipse(batch, at, 11 + flicker, 11, pal.Dark);
                    p.Ellipse(batch, at, 9 + flicker, 9, pal.Main);
                    p.Ellipse(batch, at + new Vector2(-1, -1), 5, 5, pal.Hot);
                    p.Ellipse(batch, at + new Vector2(-1, -1), 2, 2, Color.White);
                    break;
                }

                case FxStyle.Bone:
                {
                    // Spinning bone with two fading ghosts behind it.
                    var spin = (_tick + projectile.Serial * 7) * 0.45f;
                    var step = projectile.Velocity.ToXna() / RunClock.TicksPerSecond * 2f;
                    MoveShapes.DrawRotated(batch, _shapes.Bone, at - step * 2, spin - 0.9f, Color.White * 0.18f);
                    MoveShapes.DrawRotated(batch, _shapes.Bone, at - step, spin - 0.45f, Color.White * 0.35f);
                    MoveShapes.DrawRotated(batch, _shapes.Bone, at, spin, Color.White);
                    break;
                }

                case FxStyle.Leaf:
                {
                    var tangent = projectile.Angle + MathF.PI / 2f;
                    var flutter = MathF.Sin((_tick + projectile.Serial * 5) * 0.4f) * 0.4f;
                    MoveShapes.DrawRotated(batch, _shapes.Leaf, OrbitAt(projectile), tangent + flutter, Color.White, 2f);
                    break;
                }

                case FxStyle.Star:
                    MoveShapes.DrawRotated(batch, _shapes.Star, at, (_tick + projectile.Serial * 5) * 0.2f, Color.White);
                    break;

                case FxStyle.Bubble:
                    DrawBubble(batch, projectile.Radius, at, projectile.Serial);
                    break;

                case FxStyle.Shard:
                    MoveShapes.DrawRotated(batch, _shapes.Shard, OrbitAt(projectile), projectile.Angle + MathF.PI / 2f, Color.White);
                    break;

                case FxStyle.Needle:
                    DrawNeedle(batch, at, projectile.Velocity.ToXna());
                    break;

                case FxStyle.Scissor:
                {
                    // Two blades crossed in an X, spinning, with a fading ghost behind.
                    var spin = (_tick + projectile.Serial * 7) * 0.4f;
                    var step = projectile.Velocity.ToXna() / RunClock.TicksPerSecond * 2f;
                    DrawScissor(batch, at - step, spin - 0.5f, 0.3f);
                    DrawScissor(batch, at, spin, 1f);
                    break;
                }

                case FxStyle.Shadow:
                    DrawShadowBall(batch, at, projectile.Radius, projectile.Serial);
                    break;

                case FxStyle.Twister:
                    DrawTwister(batch, projectile.Position.ToXna(), projectile.Serial);
                    break;

                case FxStyle.Gyro:
                {
                    var step = projectile.Velocity.ToXna() / RunClock.TicksPerSecond * 2f;
                    DrawGyro(batch, at - step, projectile.Radius, projectile.Serial, 0.3f);
                    DrawGyro(batch, at, projectile.Radius, projectile.Serial, 1f);
                    break;
                }

                case FxStyle.AirBlade:
                    DrawAirBlade(batch, at, projectile.Velocity.ToXna());
                    break;

                case FxStyle.BoneRush:
                {
                    // Big spinning bones with two fading ghosts behind them.
                    var spin = (_tick + projectile.Serial * 7) * 0.5f;
                    var step = projectile.Velocity.ToXna() / RunClock.TicksPerSecond * 2f;
                    MoveShapes.DrawRotated(batch, _shapes.Bone, at - step * 2, spin - 1f, Color.White * 0.18f, 2f);
                    MoveShapes.DrawRotated(batch, _shapes.Bone, at - step, spin - 0.5f, Color.White * 0.35f, 2f);
                    MoveShapes.DrawRotated(batch, _shapes.Bone, at, spin, Color.White, 2f);
                    break;
                }

                case FxStyle.LeafStorm:
                    DrawLeafStorm(batch, projectile);
                    break;
            }
        }

        foreach (var zone in moves.Zones)
        {
            if (Style(zone.Move.Definition) == FxStyle.Pillars)
                DrawPillars(batch, zone);
        }

        foreach (var pending in moves.Pending)
        {
            if (pending.Kind == PendingKind.Strike && Style(pending.Move.Definition) == FxStyle.Meteor)
                DrawMeteor(batch, MeteorAt(pending, Progress(run, pending)), pending.Radius);
        }

        foreach (var arc in _arcs)
        {
            if (arc.Style == FxStyle.Drain)
                DrawDrainArc(batch, arc);
        }

        foreach (var beam in moves.Beams)
        {
            switch (Style(beam.Move.Definition))
            {
                case FxStyle.Jet:
                    DrawJet(batch, beam);
                    break;
                case FxStyle.Flamethrower:
                    DrawFlamethrower(batch, beam);
                    break;
                case FxStyle.IceBeam:
                    DrawIceBeam(batch, beam);
                    break;
            }
        }

        // Falling rocks above their warning shadows.
        foreach (var pending in moves.Pending)
        {
            if (pending.Kind != PendingKind.Strike || Style(pending.Move.Definition) != FxStyle.Rock)
                continue;

            var progress = Progress(run, pending);
            var height = (1f - progress) * (1f - progress) * 150f;
            var rockAt = pending.Position.ToXna() - new Vector2(0, height + 4f);
            var scale = pending.Radius >= 20f ? 2f : 1f;
            for (var k = 1; k <= 3; k++)
                UiDraw.Line(batch, p, rockAt + new Vector2((k - 2) * 3, -6 * scale), rockAt + new Vector2((k - 2) * 3, -6 * scale - 10 - k * 3), Color.White * (0.35f * (1f - progress * 0.5f)));
            MoveShapes.DrawRotated(batch, _shapes.Rock, rockAt, (pending.StartTick % 4) * MathF.PI / 2f, Color.White, scale);
        }

        foreach (var slash in _slashes)
        {
            switch (Lookup(slash.MoveId)?.Style)
            {
                case FxStyle.Chop:
                    DrawChop(batch, slash);
                    break;
                case FxStyle.Bite:
                    DrawBite(batch, slash);
                    break;
                case FxStyle.PowerWhip:
                    DrawPowerWhip(batch, slash);
                    break;
                default:
                    DrawVine(batch, slash);
                    break;
            }
        }

        // Psychic light is coloured: additive blending would wash these pinks and violets to white on the grass.
        foreach (var ring in moves.Rings)
        {
            if (Style(ring.Move.Definition) == FxStyle.Hypno)
                DrawHypnoRing(batch, ring);
        }

        foreach (var pending in moves.Pending)
        {
            if (pending.Kind == PendingKind.Strike && Style(pending.Move.Definition) == FxStyle.Psywave)
                DrawPsyWarning(batch, pending, Progress(run, pending));
        }

        foreach (var fx in _timed)
        {
            if (fx.Kind == FxKind.PsyRing)
            {
                var t = fx.Age / (float)fx.Life;
                var radius = fx.Value * (0.3f + 0.9f * t);
                p.Ring(batch, fx.Position, radius, radius * 0.6f, fx.Color * (1f - t), 2);
                continue;
            }

            if (fx.Kind == FxKind.DarkRing)
            {
                DrawDarkRings(batch, fx);
                continue;
            }

            if (fx.Kind == FxKind.StoneSpikes)
            {
                DrawStoneSpikes(batch, fx);
                continue;
            }

            if (fx.Kind != FxKind.CutMark)
                continue;

            var fade = 1f - fx.Age / (float)fx.Life;
            var length = 4f + fx.Age * 0.6f;
            var a = new Vector2(MathF.Cos(fx.Value), MathF.Sin(fx.Value)) * length;
            var b = new Vector2(MathF.Cos(fx.Value + 1.2f), MathF.Sin(fx.Value + 1.2f)) * length * 0.8f;
            UiDraw.Line(batch, p, fx.Position - a, fx.Position + a, fx.Color * fade, 2f);
            UiDraw.Line(batch, p, fx.Position - b, fx.Position + b, Color.White * (0.8f * fade));
        }

        _dust.Draw(batch, p);
        _flames.Draw(batch, p);
    }

    /// <summary>Light-emitting effects: fire, wind, electricity, beams, sparkles. Additive batch.</summary>
    public void DrawGlow(SpriteBatch batch, RunState run)
    {
        var p = _services.Primitives;
        var moves = run.MoveSystem;

        foreach (ref readonly var projectile in moves.Projectiles.Items)
        {
            if (!projectile.Active || projectile.Move is null)
                continue;

            var pal = TypeFx.For(projectile.Move.Definition.Type);
            var at = projectile.Position.ToXna() - new Vector2(0, BodyHeight);
            switch (Style(projectile.Move.Definition))
            {
                case FxStyle.Electric:
                    ElectricFx.Orb(batch, p, at, _tick, projectile.Serial);
                    break;

                case FxStyle.Orb:
                {
                    // Only a soft halo here; the coloured body is drawn opaque in DrawObjects.
                    var flicker = (_tick + projectile.Serial) % 4 < 2 ? 1 : 0;
                    p.Ellipse(batch, at, 19 + flicker, 19 + flicker, pal.Main * 0.18f);
                    break;
                }

                case FxStyle.Blade:
                {
                    // Crescent of air, convex side forward, with speed lines behind.
                    var dir = Vector2.Normalize(projectile.Velocity.ToXna() + new Vector2(0.001f, 0));
                    var side = new Vector2(-dir.Y, dir.X);
                    for (var s = -1f; s <= 1.001f; s += 0.2f)
                    {
                        var point = at + side * s * 6f + dir * (1f - s * s) * 3f;
                        p.Rect(batch, new Rectangle((int)point.X - 1, (int)point.Y - 1, 2, 2), pal.Main * 0.6f);
                        p.Rect(batch, new Rectangle((int)point.X, (int)point.Y, 1, 1), pal.Hot);
                    }

                    for (var k = -1; k <= 1; k++)
                        UiDraw.Line(batch, p, at - dir * 3 + side * k * 3, at - dir * (10 + Math.Abs(k) * 3) + side * k * 3, pal.Spark * 0.5f);
                    break;
                }

                case FxStyle.Star:
                {
                    // Soft gold halo that twinkles.
                    var twinkle = (_tick + projectile.Serial) % 6 < 3 ? 2 : 0;
                    p.Ellipse(batch, at, 15 + twinkle, 15 + twinkle, StarGold * 0.25f);
                    break;
                }

                case FxStyle.Shard:
                    // A cold glow, so the small crystals read against the grass.
                    p.Ellipse(batch, OrbitAt(projectile), 11, 9, IcePalette.Main * 0.45f);
                    break;

                case FxStyle.Sphere:
                    DrawSphere(batch, at, projectile.Serial);
                    break;

                case FxStyle.Shadow:
                    // A faint violet halo: the body itself is dark, so it is drawn in the alpha layer.
                    p.Ellipse(batch, at, (int)(projectile.Radius * 3f), (int)(projectile.Radius * 3f), GhostPalette.Main * 0.3f);
                    break;

                case FxStyle.Scissor:
                    // A pale glint around the blades, so green on green still reads.
                    p.Ellipse(batch, at, 21, 21, new Color(230, 255, 150) * 0.3f);
                    break;

                case FxStyle.AirBlade:
                    p.Ellipse(batch, at, 23, 19, Color.White * 0.16f);
                    break;
            }
        }

        foreach (var arc in _arcs)
        {
            if (arc.Style == FxStyle.Electric)
                ElectricFx.Bolt(batch, p, arc.From, arc.To, arc.Seed + arc.Age / 2, 1f - arc.Age / (float)ArcFx.Life);
            else if (arc.Style == FxStyle.Thunder)
                DrawThunder(batch, arc);
        }

        foreach (var pending in moves.Pending)
        {
            // A hot glow around each falling meteor.
            if (pending.Kind == PendingKind.Strike && Style(pending.Move.Definition) == FxStyle.Meteor)
                p.Ellipse(batch, MeteorAt(pending, Progress(run, pending)), 22, 22, MeteorOrange * 0.25f);
        }

        foreach (var beam in moves.Beams)
        {
            switch (Style(beam.Move.Definition))
            {
                case FxStyle.Aurora:
                    DrawAurora(batch, beam);
                    break;
                case FxStyle.Flash:
                    DrawFlash(batch, beam);
                    break;
                case FxStyle.IceBeam:
                    DrawIceGlow(batch, beam);
                    break;
            }
        }

        foreach (var ring in moves.Rings)
        {
            switch (Style(ring.Move.Definition))
            {
                case FxStyle.Sonic:
                    DrawSonicRing(batch, ring);
                    break;
                case FxStyle.Wind:
                    DrawWindRing(batch, ring);
                    break;
            }
        }

        foreach (var move in run.Player.Moves)
        {
            if (move.Definition.Archetype == MoveArchetype.Aura && Style(move.Definition) == FxStyle.Static)
                DrawStaticSparks(batch, run.Player.Position.ToXna(), move.Stats.Radius * run.Player.AreaMultiplier);
        }

        foreach (var fx in _timed)
        {
            var t = fx.Age / (float)fx.Life;
            switch (fx.Kind)
            {
                case FxKind.Star:
                {
                    var length = 2f + fx.Age * 1.5f;
                    var color = fx.Color * (1f - t);
                    UiDraw.Line(batch, p, fx.Position - new Vector2(length, 0), fx.Position + new Vector2(length, 0), color);
                    UiDraw.Line(batch, p, fx.Position - new Vector2(0, length), fx.Position + new Vector2(0, length), color);
                    var diagonal = length * 0.5f;
                    UiDraw.Line(batch, p, fx.Position - new Vector2(diagonal, diagonal), fx.Position + new Vector2(diagonal, diagonal), color * 0.6f);
                    UiDraw.Line(batch, p, fx.Position - new Vector2(diagonal, -diagonal), fx.Position + new Vector2(diagonal, -diagonal), color * 0.6f);
                    break;
                }

                case FxKind.AuraPulse:
                {
                    var radius = fx.Value * (0.6f + 0.5f * t);
                    p.Ring(batch, fx.Position, radius, radius, fx.Color * (0.6f * (1f - t)), 2);
                    break;
                }

                case FxKind.Pop:
                {
                    var radius = 2f + fx.Age * 1.3f;
                    p.Ring(batch, fx.Position, radius, radius, fx.Color * (1f - t));
                    break;
                }

                case FxKind.StaticRing:
                    DrawStaticRing(batch, fx.Position, fx.Value * (0.55f + 0.55f * t), fx.Age, 1f - t);
                    break;
            }
        }

        _glow.Draw(batch, p);
    }

    private void DrawVine(SpriteBatch batch, SlashFx slash)
    {
        var p = _services.Primitives;
        var pal = TypeFx.For(Lookup(slash.MoveId)?.Type ?? PokemonType.Grass);
        var reach = slash.Reach.Length();
        var facing = MathF.Atan2(slash.Reach.Y, slash.Reach.X);
        var half = slash.SpreadDegrees * MathF.PI / 360f;
        var t = 1f - (1f - slash.Age / (float)SlashFx.Life) * (1f - slash.Age / (float)SlashFx.Life);
        var fade = slash.Age < SlashFx.Life - 3 ? 1f : (SlashFx.Life - slash.Age) / 3f;

        // Smear of the sweep so far: a fan of fading strokes behind the vine.
        for (var k = 6; k >= 1; k--)
        {
            var past = facing - half + 2f * half * MathF.Max(0f, t - k * 0.06f);
            UiDraw.Line(batch, p, slash.Origin, slash.Origin + new Vector2(MathF.Cos(past), MathF.Sin(past)) * reach, pal.Main * ((0.45f - k * 0.06f) * fade), 3f);
        }

        // The vine itself: a thick curling quadratic curve with a light stripe and leaves along it.
        var angle = facing - half + 2f * half * t;
        var tip = slash.Origin + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * reach;
        var normal = new Vector2(-MathF.Sin(angle), MathF.Cos(angle));
        var control = (slash.Origin + tip) / 2f + normal * reach * 0.25f * (1f - t);
        Vector2 Curve(float u) => (1 - u) * (1 - u) * slash.Origin + 2 * (1 - u) * u * control + u * u * tip;

        var previous = slash.Origin;
        for (var i = 1; i <= 12; i++)
        {
            var point = Curve(i / 12f);
            UiDraw.Line(batch, p, previous, point, pal.Dark * fade, 5f);
            UiDraw.Line(batch, p, previous, point, pal.Main * fade, 3f);
            UiDraw.Line(batch, p, previous, point, pal.Hot * (0.8f * fade), 1f);
            previous = point;
        }

        foreach (var u in (ReadOnlySpan<float>)[0.35f, 0.65f])
            MoveShapes.DrawRotated(batch, _shapes.Leaf, Curve(u) + normal * 3f, angle + MathF.PI / 3f, Color.White * fade);
        MoveShapes.DrawRotated(batch, _shapes.Leaf, tip, angle, Color.White * fade, 2f);
    }

    private void DrawAurora(SpriteBatch batch, MoveBeam beam)
    {
        var p = _services.Primitives;
        var pal = TypeFx.For(beam.Move.Definition.Type);
        var origin = beam.Origin.ToXna() - new Vector2(0, BodyHeight);
        var direction = beam.Direction.ToXna();
        var end = origin + direction * beam.Length;
        var age = beam.Lifetime - beam.TicksLeft;
        var alpha = MathHelper.Clamp(age / 4f, 0f, 1f) * MathHelper.Clamp(beam.TicksLeft / 6f, 0f, 1f);
        var width = beam.HalfWidth * 2f;

        UiDraw.Line(batch, p, origin, end, pal.Main * (0.25f * alpha), width + 6f);

        // Aurora bands: pink, gold and ice blue scrolling along the beam.
        Color[] bands = [new(255, 160, 220), new(255, 236, 140), new(140, 226, 255)];
        for (var d = 0f; d < beam.Length; d += 6f)
        {
            var band = bands[((int)(d / 6f) + _tick / 2) % bands.Length];
            UiDraw.Line(batch, p, origin + direction * d, origin + direction * MathF.Min(beam.Length, d + 6f), band * (0.8f * alpha), width);
        }

        UiDraw.Line(batch, p, origin, end, Color.White * alpha, MathF.Max(1f, width / 3f));
        p.Ellipse(batch, origin, 10, 10, Color.White * (0.5f * alpha));
        p.Ellipse(batch, end, 8, 8, pal.Hot * (0.6f * alpha));
    }

    // ---------------------------------------------------------------- helpers

    private static float Progress(RunState run, PendingHit pending) =>
        MathHelper.Clamp((run.Ticks - pending.StartTick) / (float)Math.Max(1, pending.DueTick - pending.StartTick), 0f, 1f);

    /// <summary>Orbiting objects ride at mid-body height, lower than flying shots.</summary>
    private static Vector2 OrbitAt(in Projectile projectile) => projectile.Position.ToXna() - new Vector2(0, BodyHeight * 0.6f);

    private float Rand(float min, float max) => min + _random.NextSingle() * (max - min);

    private Vector2 Jitter(float amount) => new(Rand(-amount, amount), Rand(-amount, amount));

    private static int Hash(int a, int b)
    {
        unchecked
        {
            var h = a * 374761393 + b * 668265263;
            h = (h ^ (h >> 13)) * 1274126177;
            return (h ^ (h >> 16)) & 0x7FFFFFFF;
        }
    }

    private static void Age<T>(List<T> list, Func<T, T> grow, Func<T, bool> dead)
    {
        for (var i = list.Count - 1; i >= 0; i--)
        {
            list[i] = grow(list[i]);
            if (dead(list[i]))
                list.RemoveAt(i);
        }
    }

    private enum FxKind : byte
    {
        Star,
        CutMark,
        AuraPulse,
        DustRing,
        Pop,
        StaticRing,
        PsyRing,
        DarkRing,
        ToxicRing,
        StoneSpikes,
    }

    private readonly record struct TimedFx(FxKind Kind, Vector2 Position, Vector2 Vector, float Value, Color Color, int Age = 0, int Life = 10);

    private readonly record struct SlashFx(Vector2 Origin, Vector2 Reach, float SpreadDegrees, int Age = 0, string? MoveId = null)
    {
        public const int Life = 10;
    }

    private readonly record struct ArcFx(Vector2 From, Vector2 To, int Seed, int Age = 0, FxStyle Style = FxStyle.Electric)
    {
        public const int Life = 9;
    }

    /// <summary>A bubble rising in Fangobomba's mud, or in Tossina's poison when <paramref name="Toxic"/>.</summary>
    private readonly record struct Bubble(Vector2 Position, int Age, int Life, bool Toxic = false);
}
