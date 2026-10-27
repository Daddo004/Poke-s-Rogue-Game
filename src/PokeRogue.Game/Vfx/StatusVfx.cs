using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PokeRogue.Core.Combat;
using PokeRogue.Core.Data;
using PokeRogue.Core.Run;
using PokeRogue.Game.Graphics;

namespace PokeRogue.Game.Vfx;

/// <summary>
/// Status conditions on enemies (phase 2.3): sprite tints, particles and the icon over the head. Mostly state-driven:
/// every frame reads each enemy's status, so nothing needs cleaning up when one wears off.
/// Layers: <see cref="DrawObjects"/> (flames, bubbles, ice shards; alpha), <see cref="DrawGlow"/> (sparks, embers, glints;
/// additive) and <see cref="DrawOverhead"/> (icons, z's, stars; over everything).
/// </summary>
public sealed class StatusVfx
{
    private static readonly TypeFxPalette Fire = TypeFx.For(PokemonType.Fire);
    private static readonly TypeFxPalette Ice = TypeFx.For(PokemonType.Ice);
    private static readonly StatusArt PoisonArt = StatusIcons.Art(StatusKind.Poison);
    private static readonly StatusArt ConfusionArt = StatusIcons.Art(StatusKind.Confusion);

    /// <summary>The icon blinks during the status' last second.</summary>
    private const int EndingTicks = 60;

    private readonly GameServices _services;
    private readonly Random _random = new(33);

    /// <summary>Alpha-blended: flame tongues, ice shards, bubble pops.</summary>
    private readonly Particles _matter = new(1536);

    /// <summary>Additive: embers, sparks, puffs.</summary>
    private readonly Particles _glow = new(1024);

    private readonly List<Bubble> _bubbles = [];
    private readonly List<Snooze> _snoozes = [];
    private readonly List<Spark> _sparks = [];
    private readonly List<Glint> _glints = [];
    private int _tick;

    public StatusVfx(GameServices services) => _services = services;

    /// <summary>The symbol was approved in phase 2.3; the badge and no icon stay for comparisons.</summary>
    public StatusIconStyle IconStyle { get; set; } = StatusIconStyle.Symbol;

    /// <summary>Previews: a different icon style per enemy slot.</summary>
    public Func<int, StatusIconStyle>? IconStyleForSlot { get; set; }

    public int ParticleCount => _matter.Active + _glow.Active;

    public void Reset()
    {
        _bubbles.Clear();
        _snoozes.Clear();
        _sparks.Clear();
        _glints.Clear();
    }

    public void OnEvent(in RunEvent e)
    {
        switch (e.Type)
        {
            case RunEventType.StatusApplied:
            {
                // A puff in the status colour as it takes hold.
                var art = StatusIcons.Art(e.Status);
                _glow.Burst(Body(e.Position.ToXna(), e.SpeciesId), 7, 70f, art.Light, art.Main, 14, _random);
                break;
            }

            case RunEventType.StatusDamage when e.Status == StatusKind.Confusion:
                // It hurt itself: a little bump of stars.
                _glow.Burst(Body(e.Position.ToXna(), e.SpeciesId), 5, 90f, Color.White, ConfusionArt.Main, 12, _random);
                break;

            case RunEventType.StatusEnded when e.Status == StatusKind.Freeze:
                Shatter(e.Position.ToXna(), e.SpeciesId);
                break;
        }
    }

    // ---------------------------------------------------------------- per tick

    public void Update(RunState run, bool paused, Rectangle cull)
    {
        _tick++;
        _matter.Update();
        _glow.Update();
        AgeEffects();
        if (paused)
            return;

        var items = run.Enemies.Items;
        for (var i = 0; i < items.Length; i++)
        {
            ref readonly var e = ref items[i];
            if (!e.IsAlive || e.Status == StatusKind.None || !cull.Contains(e.Position.ToXna()))
                continue;

            var feet = e.Position.ToXna();
            var head = HeadHeight(e.SpeciesId);
            var phase = _tick + i * 7;
            switch (e.Status)
            {
                case StatusKind.Burn:
                    // Tongues of fire licking up the body (orange cooling to red), now and then an ember.
                    if (phase % 3 == 0)
                    {
                        var spot = feet + new Vector2(Rand(-0.6f, 0.6f) * e.Radius, -Rand(2f, head * 0.75f));
                        _matter.Emit(spot, new Vector2(Rand(-3, 3), Rand(-28, -16)), Fire.Main, Fire.Dark, 12 + _random.Next(6), 3f, drag: 0.93f, stretch: 2.2f);
                        if (phase % 8 == 0)
                            _matter.Emit(spot + new Vector2(0, 1), new Vector2(Rand(-2, 2), Rand(-22, -12)), Fire.Hot, Fire.Main, 8, 2f, drag: 0.93f, stretch: 2f);
                    }

                    if (phase % 26 == 0)
                        _glow.Emit(feet - new Vector2(Rand(-4, 4), head * 0.6f), new Vector2(Rand(-10, 10), Rand(-45, -30)), Fire.Spark, Fire.Dark, 26, 1f, drag: 0.97f);
                    break;

                case StatusKind.Poison:
                    // Toxic bubbles rising off the body and popping.
                    if (phase % 12 == 0)
                        _bubbles.Add(new Bubble(feet + new Vector2(Rand(-0.5f, 0.5f) * e.Radius, -Rand(head * 0.25f, head * 0.7f)), _random.Next(100), 24 + _random.Next(12)));
                    break;

                case StatusKind.Paralysis:
                    // Static crackling over the body; much more of it during a lock-up.
                    if (phase % (e.StuckTicks > 0 ? 3 : 7) == 0)
                        _sparks.Add(new Spark(feet + new Vector2(Rand(-1f, 1f) * e.Radius, -Rand(head * 0.2f, head * 0.9f)), Rand(0f, MathF.Tau), _random.Next()));
                    break;

                case StatusKind.Freeze:
                    // Light glinting on the ice.
                    if (phase % 16 == 0)
                        _glints.Add(new Glint(feet + new Vector2(Rand(-0.8f, 0.8f) * e.Radius, -Rand(3f, head))));
                    break;

                case StatusKind.Sleep:
                    if (phase % 50 == 0)
                        _snoozes.Add(new Snooze(feet + new Vector2(3, -head - 2)));
                    break;
            }
        }
    }

    private void AgeEffects()
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
            for (var k = 0; k < 3; k++)
                _matter.Emit(BubbleAt(b), new Vector2(Rand(-20, 20), Rand(-25, -5)), PoisonArt.Light, PoisonArt.Main, 9, 1f, drag: 0.9f, gravity: 3f);
            _bubbles.RemoveAt(i);
        }

        Age(_snoozes, s => s with { Age = s.Age + 1 }, s => s.Age >= Snooze.Life);
        Age(_sparks, s => s with { Age = s.Age + 1 }, s => s.Age >= Spark.Life);
        Age(_glints, g => g with { Age = g.Age + 1 }, g => g.Age >= Glint.Life);
    }

    private void Shatter(Vector2 feet, int speciesId)
    {
        var head = HeadHeight(speciesId);
        for (var k = 0; k < 12; k++)
        {
            var at = feet - new Vector2(Rand(-6, 6), Rand(2, head * 0.8f));
            var dir = new Vector2(Rand(-1f, 1f), Rand(-1.2f, -0.2f));
            var color = k % 3 == 0 ? Color.White : Ice.Main;
            _matter.Emit(at, dir * Rand(40, 90), color, Ice.Dark * 0.6f, 18 + _random.Next(8), k % 4 == 0 ? 3f : 2f, drag: 0.94f, gravity: 7f);
        }

        _glow.Burst(feet - new Vector2(0, head * 0.5f), 6, 90f, Color.White, Ice.Main, 12, _random);
    }

    // ---------------------------------------------------------------- sprite tints

    /// <summary>Multiplied into the enemy's sprite.</summary>
    public Color Tint(in Enemy e, int slot)
    {
        var pulse = 0.5f + 0.5f * MathF.Sin((_tick + slot * 11) * 0.15f);
        return e.Status switch
        {
            StatusKind.Burn => Color.Lerp(Color.White, new Color(255, 170, 130), 0.4f + 0.4f * pulse),
            StatusKind.Poison => Color.Lerp(Color.White, new Color(214, 150, 255), 0.4f + 0.4f * pulse),
            StatusKind.Paralysis when ParalysisFlash(e, slot) => new Color(255, 244, 140),
            StatusKind.Freeze => new Color(150, 210, 255),
            StatusKind.Sleep => new Color(200, 200, 232),
            _ => Color.White,
        };
    }

    /// <summary>
    /// Light added over the sprite's silhouette in the glow pass; transparent for none. The additive batch weighs a
    /// premultiplied colour by its alpha again, so 0.5 here adds about a quarter of the colour.
    /// </summary>
    public Color Glow(in Enemy e, int slot)
    {
        var pulse = 0.5f + 0.5f * MathF.Sin((_tick + slot * 11) * 0.15f);
        return e.Status switch
        {
            StatusKind.Burn => new Color(255, 96, 24) * (0.35f + 0.25f * pulse),
            StatusKind.Poison => new Color(150, 50, 220) * (0.32f + 0.22f * pulse),
            StatusKind.Paralysis when ParalysisFlash(e, slot) => new Color(255, 230, 80) * 0.6f,
            StatusKind.Freeze => new Color(110, 190, 255) * 0.38f,
            _ => Color.Transparent,
        };
    }

    /// <summary>Paralysed sprites shake on the spot while locked up.</summary>
    public Vector2 Jitter(in Enemy e) => e.IsAlive && e.StuckTicks > 0 ? new Vector2(_tick / 2 % 2 * 2 - 1, 0) : Vector2.Zero;

    private bool ParalysisFlash(in Enemy e, int slot) => e.StuckTicks > 0 ? _tick / 3 % 2 == 0 : (_tick + slot * 5) % 16 < 3;

    /// <summary>The block of ice around a frozen Pokémon, drawn right after its sprite.</summary>
    public void DrawIce(SpriteBatch batch, in Enemy e, Vector2 feet)
    {
        var p = _services.Primitives;
        var head = HeadHeight(e.SpeciesId);
        var width = (int)MathF.Max(14f, e.Radius * 2f + 6f);
        var height = head + 5;
        var x = (int)MathF.Round(feet.X - width / 2f);
        var y = (int)MathF.Round(feet.Y) - height + 3;
        Color light = new(236, 250, 255), deep = new(70, 140, 210);
        p.Follow(feet, new Vector2(MathF.Round(feet.X), MathF.Round(feet.Y)));

        // Translucent body, then edges lit from the top-left; the corners are chipped.
        p.Rect(batch, new Rectangle(x, y, width, height), new Color(170, 225, 255) * 0.36f);
        p.Rect(batch, new Rectangle(x + 1, y, width - 2, 1), light * 0.9f);
        p.Rect(batch, new Rectangle(x, y + 1, 1, height - 2), light * 0.75f);
        p.Rect(batch, new Rectangle(x + width - 1, y + 1, 1, height - 2), deep * 0.7f);
        p.Rect(batch, new Rectangle(x + 1, y + height - 1, width - 2, 1), deep * 0.7f);

        // Facets: two diagonal glints across the front.
        UiDraw.Line(batch, p, new Vector2(x + 2, y + height * 0.5f), new Vector2(x + width * 0.45f, y + 2), Color.White * 0.5f);
        UiDraw.Line(batch, p, new Vector2(x + 3, y + height * 0.8f), new Vector2(x + width * 0.3f, y + height * 0.55f), Color.White * 0.3f);
        p.Nudge = Vector2.Zero;
    }

    // ---------------------------------------------------------------- drawing

    /// <summary>Bubbles, flame tongues and shards over the Pokémon. Alpha batch with the camera transform.</summary>
    public void DrawObjects(SpriteBatch batch)
    {
        var p = _services.Primitives;
        foreach (var b in _bubbles)
        {
            // A purple bubble with a light rim and a white glint, swelling as it rises.
            var t = b.Age / (float)b.Life;
            var radius = 1.5f + t * 1.5f;
            var at = BubbleAt(b);
            p.Ellipse(batch, at, (int)(radius * 2f), (int)(radius * 2f), PoisonArt.Main * 0.75f);
            p.Ring(batch, at, radius, radius, PoisonArt.Light * 0.9f);
            p.Rect(batch, new Rectangle((int)MathF.Round(at.X - radius * 0.5f), (int)MathF.Round(at.Y - radius * 0.5f), 1, 1), Color.White);
        }

        _matter.Draw(batch, p);
    }

    /// <summary>Sparks, glints and embers. Additive batch with the camera transform.</summary>
    public void DrawGlow(SpriteBatch batch)
    {
        var p = _services.Primitives;
        foreach (var spark in _sparks)
        {
            // A three-point zig-zag, re-rolled every other tick so it crackles.
            var random = new Random(spark.Seed + spark.Age / 2);
            var dir = new Vector2(MathF.Cos(spark.Angle), MathF.Sin(spark.Angle));
            var side = new Vector2(-dir.Y, dir.X);
            var length = 4f + random.Next(4);
            var a = spark.Position - dir * length * 0.5f;
            var mid = spark.Position + side * random.Next(-2, 3);
            var b = spark.Position + dir * length * 0.5f;
            var fade = 1f - spark.Age / (float)Spark.Life;
            UiDraw.Line(batch, p, a, mid, ElectricFx.Glow * (0.5f * fade), 3f);
            UiDraw.Line(batch, p, mid, b, ElectricFx.Glow * (0.5f * fade), 3f);
            UiDraw.Line(batch, p, a, mid, ElectricFx.Core * fade);
            UiDraw.Line(batch, p, mid, b, ElectricFx.Core * fade);
            p.Rect(batch, new Rectangle((int)mid.X, (int)mid.Y, 1, 1), ElectricFx.Hot * fade);
        }

        foreach (var glint in _glints)
        {
            // A small four-pointed twinkle that grows and fades.
            var t = glint.Age / (float)Glint.Life;
            var arm = t < 0.5f ? 1 + (int)(t * 4f) : 3 - (int)((t - 0.5f) * 4f);
            var color = Color.White * (1f - t * 0.6f);
            var at = new Point((int)glint.Position.X, (int)glint.Position.Y);
            p.Rect(batch, new Rectangle(at.X - arm, at.Y, arm * 2 + 1, 1), color * 0.7f);
            p.Rect(batch, new Rectangle(at.X, at.Y - arm, 1, arm * 2 + 1), color * 0.7f);
            p.Rect(batch, new Rectangle(at.X, at.Y, 1, 1), color);
        }

        _glow.Draw(batch, p);
    }

    /// <summary>Icons over the heads, confusion stars and sleepy z's, on top of everything. Alpha batch with the camera transform.</summary>
    public void DrawOverhead(SpriteBatch batch, RunState run, Rectangle cull)
    {
        var icons = _services.StatusIcons;
        var items = run.Enemies.Items;
        for (var i = 0; i < items.Length; i++)
        {
            ref readonly var e = ref items[i];
            if (!e.IsAlive || e.Status == StatusKind.None || !cull.Contains(e.Position.ToXna()))
                continue;

            var feet = e.Position.ToXna() + Jitter(e);
            var top = VirtualScreen.Snap(new Vector2(feet.X, feet.Y - HeadHeight(e.SpeciesId)));
            if (e.Status == StatusKind.Confusion)
                DrawStars(batch, icons.Star, top, i);

            // A gentle bob; during the last second the icon blinks, so it shows the status is about to wear off.
            var style = IconStyleForSlot?.Invoke(i) ?? IconStyle;
            if (icons.Icon(e.Status, style) is { } icon && (e.StatusTicks > EndingTicks || _tick / 5 % 2 == 0))
            {
                var bob = (_tick + i * 13) / 24 % 2;
                batch.Draw(icon, new Vector2(top.X - StatusIcons.Size / 2, top.Y - StatusIcons.Size - 3 - bob), Color.White);
            }
        }

        foreach (var s in _snoozes)
        {
            var t = s.Age / (float)Snooze.Life;
            var texture = t < 0.35f ? icons.SmallZ : icons.BigZ;
            var at = s.Origin + new Vector2(s.Age * 0.14f + MathF.Sin(s.Age * 0.12f) * 2f, -s.Age * 0.26f);
            var alpha = t < 0.75f ? 1f : (1f - t) / 0.25f;
            batch.Draw(texture, new Vector2(MathF.Round(at.X), MathF.Round(at.Y - texture.Height)), Color.White * alpha);
        }
    }

    /// <summary>Three stars circling the head; the ones passing behind are dimmer.</summary>
    private void DrawStars(SpriteBatch batch, Texture2D star, Vector2 top, int slot)
    {
        for (var k = 0; k < 3; k++)
        {
            var angle = (_tick + slot * 9) * 0.11f + k * MathF.Tau / 3f;
            var depth = MathF.Sin(angle);
            var at = top + new Vector2(MathF.Cos(angle) * 9f, depth * 3f + 1f);
            batch.Draw(star, new Vector2(MathF.Round(at.X - star.Width / 2f), MathF.Round(at.Y - star.Height / 2f)), Color.White * (depth > 0f ? 1f : 0.55f));
        }
    }

    // ---------------------------------------------------------------- helpers

    private int HeadHeight(int speciesId) => _services.Sprites.Get(speciesId)?.HeadHeight ?? 16;

    private Vector2 Body(Vector2 feet, int speciesId) => feet - new Vector2(0, HeadHeight(speciesId) * 0.5f);

    private static Vector2 BubbleAt(Bubble b) => b.Origin + new Vector2(MathF.Sin((b.Age + b.Seed) * 0.25f), -b.Age * 0.3f);

    private float Rand(float min, float max) => min + _random.NextSingle() * (max - min);

    private static void Age<T>(List<T> list, Func<T, T> grow, Func<T, bool> dead)
    {
        for (var i = list.Count - 1; i >= 0; i--)
        {
            list[i] = grow(list[i]);
            if (dead(list[i]))
                list.RemoveAt(i);
        }
    }

    private readonly record struct Bubble(Vector2 Origin, int Seed, int Life, int Age = 0);

    private readonly record struct Snooze(Vector2 Origin, int Age = 0)
    {
        public const int Life = 72;
    }

    private readonly record struct Spark(Vector2 Position, float Angle, int Seed, int Age = 0)
    {
        public const int Life = 5;
    }

    private readonly record struct Glint(Vector2 Position, int Age = 0)
    {
        public const int Life = 14;
    }
}
