using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PokeRogue.Core.Run.Waves;
using PokeRogue.Core.Run.Worlds;

namespace PokeRogue.Game.Graphics.Floor;

/// <summary>
/// A small picture of a level for the level select and the album (phase 7.1): a stretch of its floor at full scale with
/// its obstacles, a wall when it has one, and the tint of its dark and of its first weather.
/// </summary>
public static class LevelPicture
{
    /// <summary>Renders the level into <paramref name="target"/>, then puts back <paramref name="restore"/> as the render target.</summary>
    public static void Render(GameServices services, SpriteBatch batch, WaveFile level, RenderTarget2D target, RenderTarget2D? restore)
    {
        var device = services.Device;
        var tileset = Tileset.ById(level.Map.Tileset);
        var map = new LevelMap(level.Map);
        var w = target.Width;
        var h = target.Height;

        // Where to look: past the clearing at the start, against a wall when there is one, where most obstacles stand.
        var belowWall = h / 2f - 16f;
        Vector2 Candidate(int k) => level.Map.Shape switch
        {
            MapShape.Corridor when level.Map.Axis == CorridorAxis.Horizontal => new Vector2(200 + k * 70, map.Min.Y + belowWall),
            MapShape.Corridor => new Vector2(map.Min.X + w / 2f - 30, 200 + k * 70),
            MapShape.Arena => new Vector2(map.Min.X + w / 2f - 30 + k % 10 * 40, map.Min.Y + belowWall + k / 10 * 20),
            _ => new Vector2(200 + k % 8 * 110, -300 + k / 8 * 130),
        };
        int Obstacles(Vector2 c) => map.ObstaclesIn(c.X - w / 2f + 12, c.Y - h / 2f + 20, c.X + w / 2f - 12, c.Y + h / 2f - 4).Count();
        var centre = Enumerable.Range(0, 40).Select(Candidate).Where(c => map.Contains(c.ToNumerics()) || !map.IsBounded)
            .OrderByDescending(Obstacles).ThenBy(c => c.X).FirstOrDefault(Candidate(0));
        var view = new Rectangle((int)(centre.X - w / 2f), (int)(centre.Y - h / 2f), w, h);

        using var floor = new FloorRenderer(device, tileset.Floor);
        using var obstacles = new ObstacleArt(device, tileset);
        var edges = new EdgeRenderer(services.Primitives, tileset);

        device.SetRenderTarget(target);
        device.Clear(tileset.Beyond);
        batch.Begin(samplerState: SamplerState.PointClamp, transformMatrix: Matrix.CreateTranslation(-view.X, -view.Y, 0));
        floor.Draw(batch, view);
        edges.Draw(batch, map, view, 0);
        var standing = map.ObstaclesIn(view.Left - 20, view.Top - 10, view.Right + 20, view.Bottom + 50).OrderBy(o => o.Position.Y).ToList();
        foreach (var o in standing)
            obstacles.DrawShadow(batch, services.Primitives, o);
        foreach (var o in standing)
            obstacles.Draw(batch, o);
        batch.End();

        // The dark (cave, tower, night) and the first weather, as a tint.
        batch.Begin(samplerState: SamplerState.PointClamp);
        var dark = MathF.Max(level.Darkness, level.Night is null ? 0f : 0.25f);
        if (dark > 0f)
            services.Primitives.Rect(batch, new Rectangle(0, 0, w, h), tileset.Shade * (dark * 0.7f));
        var weather = level.Weather.FirstOrDefault()?.Kind ?? WeatherKind.Clear;
        var tint = weather switch
        {
            WeatherKind.Rain => new Color(30, 50, 100) * 0.18f,
            WeatherKind.Sun => new Color(255, 214, 130) * 0.12f,
            WeatherKind.Sandstorm => new Color(196, 156, 96) * 0.22f,
            WeatherKind.Hail => new Color(190, 214, 240) * 0.14f,
            _ => Color.Transparent,
        };
        services.Primitives.Rect(batch, new Rectangle(0, 0, w, h), tint);
        batch.End();

        device.SetRenderTarget(restore);
    }
}
