using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PokeRogue.Core.Run;
using PokeRogue.Core.Run.Worlds;
using PokeRogue.Game.Graphics;
using PokeRogue.Game.Graphics.Floor;

namespace PokeRogue.Game.Vfx;

/// <summary>
/// A level's conditions on screen (phase 7.2): its darkness (a cave, a haunted tower, the night) as a dark veil with a
/// circle of light around the player, a warm tint at dusk and dawn, and the weather: rain streaks with splashes, hail
/// bouncing, sand blowing across, sunlight with rays and motes. All alpha-blended and simple, drawn under the overhead
/// numbers and bars so those stay readable; nothing falls over the player's face.
/// </summary>
public sealed class ConditionsVfx
{
    private const int Drops = 150;

    /// <summary>The circle of light around the player in the dark.</summary>
    public const float LightRadius = 150f;

    private readonly GameServices _services;
    private readonly Texture2D _light;
    private readonly Particle[] _particles = new Particle[Drops];
    private readonly Random _random = new(7);
    private Vector2 _lastCamera;
    private WeatherKind _weather;
    private float _intensity;
    private int _tick;

    private struct Particle
    {
        public Vector2 Position;
        public Vector2 Velocity;
        public int Life;
        public int Splash;
    }

    public ConditionsVfx(GameServices services)
    {
        _services = services;
        _light = BuildLight(services.Device, 128);
    }

    public void Reset()
    {
        _weather = WeatherKind.Clear;
        _intensity = 0f;
        _tick = 0;
        for (var i = 0; i < Drops; i++)
            _particles[i] = default;
    }

    /// <summary>
    /// Moves the weather's particles; they belong to the world, so they slide back as the camera moves. A new weather
    /// fades in.
    /// </summary>
    public void Update(RunState run, Vector2 camera)
    {
        _tick++;
        if (run.Weather != _weather)
        {
            _weather = run.Weather;
            _intensity = 0f;
            for (var i = 0; i < Drops; i++)
                Respawn(ref _particles[i], scatter: true);
        }

        _intensity = MathF.Min(1f, _intensity + 1f / 60f);
        var shift = camera - _lastCamera;
        _lastCamera = camera;
        if (shift.LengthSquared() > 200f * 200f)
            shift = Vector2.Zero;
        if (_weather == WeatherKind.Clear)
            return;

        var size = new Vector2(_services.Screen.Width, _services.Screen.Height);
        for (var i = 0; i < Drops; i++)
        {
            ref var d = ref _particles[i];
            d.Position -= shift;
            if (d.Splash > 0)
            {
                if (--d.Splash == 0)
                    Respawn(ref d, scatter: false);
                continue;
            }

            d.Position += d.Velocity;
            if (--d.Life <= 0)
            {
                // Rain splashes and hail bounces where it lands; sand and motes just go.
                if (_weather is WeatherKind.Rain or WeatherKind.Hail)
                    d.Splash = 6;
                else
                    Respawn(ref d, scatter: false);
            }

            // Wrap around the screen, so the field never runs out while the player walks.
            d.Position.X = Wrap(d.Position.X, size.X);
            d.Position.Y = Wrap(d.Position.Y, size.Y);
        }
    }

    private static float Wrap(float value, float size) => value < -20f ? value + size + 40f : value > size + 20f ? value - size - 40f : value;

    private void Respawn(ref Particle d, bool scatter)
    {
        var w = _services.Screen.Width;
        var h = _services.Screen.Height;
        var r = _random;
        switch (_weather)
        {
            case WeatherKind.Rain:
                d.Position = new Vector2(r.Next(-20, w + 20), scatter ? r.Next(-20, h) : r.Next(-40, 0));
                d.Velocity = new Vector2(-1.6f, 7.5f + r.NextSingle() * 1.5f);
                d.Life = r.Next(20, 60);
                break;
            case WeatherKind.Hail:
                d.Position = new Vector2(r.Next(-20, w + 20), scatter ? r.Next(-20, h) : r.Next(-40, 0));
                d.Velocity = new Vector2(-0.8f, 5f + r.NextSingle());
                d.Life = r.Next(25, 70);
                break;
            case WeatherKind.Sandstorm:
                d.Position = new Vector2(scatter ? r.Next(-20, w) : r.Next(-60, -10), r.Next(-10, h + 10));
                d.Velocity = new Vector2(8f + r.NextSingle() * 4f, 1.2f + r.NextSingle());
                d.Life = r.Next(40, 90);
                break;
            default:
                d.Position = new Vector2(r.Next(0, w), r.Next(0, h));
                d.Velocity = new Vector2(0.15f + r.NextSingle() * 0.2f, -0.2f - r.NextSingle() * 0.2f);
                d.Life = r.Next(120, 260);
                break;
        }

        d.Splash = 0;
    }

    /// <summary>How dark the level is now: its own gloom, or the night's (dusk and dawn in between).</summary>
    public static float Darkness(RunState run) => MathF.Max(MathF.Max(run.Spawner.Timeline.Darkness, run.Spawner.Timeline.NightAmount(run.Ticks) * 0.62f), ReaperDark(run));

    /// <summary>Darkrai the Reaper (phase 9.3) brings the dark with it, deepening over its first seconds on the field.</summary>
    private static float ReaperDark(RunState run) =>
        run.Boss is { IsReaper: true, DefeatedTick: null } fight ? 0.5f * MathHelper.Clamp((run.Ticks - fight.StartTick) / 180f, 0f, 1f) : 0f;

    /// <summary>The dark veil with its circle of light, and the warm light of dusk and dawn. Screen space.</summary>
    public void DrawDarkness(SpriteBatch batch, RunState run, Vector2 player, Tileset tileset)
    {
        var p = _services.Primitives;
        var screen = new Rectangle(0, 0, _services.Screen.Width, _services.Screen.Height);
        var night = run.Spawner.Timeline.NightAmount(run.Ticks);
        if (night is > 0f and < 1f)
            p.Rect(batch, screen, new Color(255, 150, 80) * (0.12f * MathF.Sin(night * MathF.PI)));

        var dark = Darkness(run);
        if (dark <= 0.01f)
            return;

        // A slow breath in the light's size, like a lantern.
        var radius = LightRadius + MathF.Sin(_tick * 0.05f) * 3f;
        var color = tileset.Shade * dark;
        var light = new Rectangle((int)(player.X - radius), (int)(player.Y - radius), (int)(radius * 2), (int)(radius * 2));
        batch.Draw(_light, light, color);
        p.Rect(batch, new Rectangle(0, 0, screen.Width, Math.Max(0, light.Top)), color);
        p.Rect(batch, new Rectangle(0, light.Bottom, screen.Width, Math.Max(0, screen.Height - light.Bottom)), color);
        p.Rect(batch, new Rectangle(0, light.Top, Math.Max(0, light.Left), light.Height), color);
        p.Rect(batch, new Rectangle(light.Right, light.Top, Math.Max(0, screen.Width - light.Right), light.Height), color);
    }

    /// <summary>The weather's tint and particles. Screen space; <paramref name="player"/> is where the player's feet are.</summary>
    public void DrawWeather(SpriteBatch batch, Vector2 player)
    {
        if (_weather == WeatherKind.Clear)
            return;

        var p = _services.Primitives;
        var screen = new Rectangle(0, 0, _services.Screen.Width, _services.Screen.Height);
        var a = _intensity;
        var face = player + new Vector2(0, -14);
        switch (_weather)
        {
            case WeatherKind.Rain:
                p.Rect(batch, screen, new Color(30, 50, 100) * (0.14f * a));
                break;
            case WeatherKind.Hail:
                p.Rect(batch, screen, new Color(190, 214, 240) * (0.1f * a));
                break;
            case WeatherKind.Sandstorm:
                p.Rect(batch, screen, new Color(170, 120, 60) * (0.24f * a));
                break;
            case WeatherKind.Sun:
                p.Rect(batch, screen, new Color(255, 214, 130) * (0.08f * a));
                for (var ray = 0; ray < 3; ray++)
                {
                    var x = 60 + ray * 170 + MathF.Sin(_tick * 0.01f + ray) * 12f;
                    UiDraw.Line(batch, p, new Vector2(x, -10), new Vector2(x + 220, screen.Height + 10), new Color(255, 244, 210) * (0.05f * a), 26f);
                }

                break;
        }

        for (var i = 0; i < Drops; i++)
        {
            ref readonly var d = ref _particles[i];
            if (Vector2.DistanceSquared(d.Position, face) < 18f * 18f)
                continue;

            var at = new Point((int)d.Position.X, (int)d.Position.Y);
            switch (_weather)
            {
                case WeatherKind.Rain when d.Splash > 0:
                    var spread = 6 - d.Splash;
                    p.Rect(batch, new Rectangle(at.X - spread, at.Y, 1, 1), new Color(200, 220, 255) * (0.6f * a));
                    p.Rect(batch, new Rectangle(at.X + spread, at.Y, 1, 1), new Color(200, 220, 255) * (0.6f * a));
                    break;
                case WeatherKind.Rain:
                    for (var k = 0; k < 4; k++)
                        p.Rect(batch, new Rectangle(at.X - k / 2, at.Y - k * 2, 1, 2), new Color(200, 220, 255) * (0.5f * a));
                    break;
                case WeatherKind.Hail:
                    var hop = d.Splash > 0 ? -(int)MathF.Sin(d.Splash / 6f * MathF.PI) * 3 : 0;
                    p.Rect(batch, new Rectangle(at.X, at.Y + hop, 2, 2), Color.White * (0.85f * a));
                    break;
                case WeatherKind.Sandstorm:
                    // Light and dark streaks, so they read over the sand.
                    p.Rect(batch, new Rectangle(at.X, at.Y, 8 + i % 10, 1), (i % 3 == 0 ? new Color(120, 84, 44) : new Color(255, 236, 190)) * (0.75f * a));
                    break;
                default:
                    p.Rect(batch, new Rectangle(at.X, at.Y, 1, 1), new Color(255, 246, 210) * (0.7f * a));
                    break;
            }
        }
    }

    /// <summary>A soft disc of light: clear in the middle, the full veil at its edge (premultiplied, like every colour here).</summary>
    private static Texture2D BuildLight(GraphicsDevice device, int size)
    {
        var pixels = new Color[size * size];
        var half = size / 2f;
        for (var y = 0; y < size; y++)
        {
            for (var x = 0; x < size; x++)
            {
                var d = MathF.Sqrt((x + 0.5f - half) * (x + 0.5f - half) + (y + 0.5f - half) * (y + 0.5f - half)) / half;
                var t = Math.Clamp((d - 0.35f) / 0.65f, 0f, 1f);
                var alpha = t * t * (3f - 2f * t);
                pixels[y * size + x] = Color.White * alpha;
            }
        }

        var texture = new Texture2D(device, size, size);
        texture.SetData(pixels);
        return texture;
    }
}
