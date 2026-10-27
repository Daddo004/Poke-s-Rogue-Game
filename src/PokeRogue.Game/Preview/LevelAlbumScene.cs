using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PokeRogue.Game.Graphics;
using PokeRogue.Game.Graphics.Floor;
using PokeRogue.Game.Scenes;

namespace PokeRogue.Game.Preview;

/// <summary>
/// Album of the levels' maps (phase 7.2): six to a page, each with the picture of the level select (floor, obstacles,
/// walls, the tint of its dark and weather), its name, its tileset and its outline.
/// </summary>
public sealed class LevelAlbumScene(GameServices services, int page) : Scene(services)
{
    private const int PerPage = 6;
    private readonly List<RenderTarget2D> _pictures = [];

    private IReadOnlyList<Core.Run.Worlds.LevelInfo> Levels =>
        Services.Worlds.Value.Levels.Skip((Math.Max(1, page) - 1) * PerPage).Take(PerPage).ToList();

    public override void Update()
    {
    }

    public override void OnLeave()
    {
        foreach (var picture in _pictures)
            picture.Dispose();
    }

    public override void Draw(SpriteBatch batch)
    {
        var levels = Levels;
        if (_pictures.Count == 0)
        {
            foreach (var level in levels)
            {
                var target = new RenderTarget2D(Services.Device, 196, 110);
                LevelPicture.Render(Services, batch, level.File, target, Services.Screen.Target);
                _pictures.Add(target);
            }
        }

        Services.Device.Clear(new Color(22, 18, 36));
        var small = Services.Fonts.Small(Services.Style);
        var body = Services.Fonts.Body(Services.Style);
        batch.Begin(samplerState: SamplerState.PointClamp);
        var pages = (Services.Worlds.Value.Levels.Count + PerPage - 1) / PerPage;
        UiDraw.TextOutlined(batch, body, $"MAPPE DEI LIVELLI  {Math.Max(1, page)}/{pages}", new Vector2(16, 8), new Color(244, 240, 228), new Color(16, 12, 28));
        for (var i = 0; i < levels.Count; i++)
        {
            var level = levels[i];
            var at = new Point(16 + i % 3 * 208, 34 + i / 3 * 160);
            var frame = new Rectangle(at.X, at.Y, 196, 110);
            Services.Primitives.Rect(batch, new Rectangle(frame.X - 1, frame.Y - 1, frame.Width + 2, frame.Height + 2), new Color(16, 12, 28));
            batch.Draw(_pictures[i], frame, Color.White);
            UiDraw.Text(batch, body, level.NameIt, new Vector2(at.X, frame.Bottom + 4), new Color(244, 240, 228));
            var map = level.File.Map;
            var shape = map.Shape switch
            {
                Core.Run.Worlds.MapShape.Corridor => $"CORRIDOIO {(map.Axis == Core.Run.Worlds.CorridorAxis.Horizontal ? "ORIZZONTALE" : "VERTICALE")}",
                Core.Run.Worlds.MapShape.Arena => "ARENA",
                _ => "APERTA",
            };
            UiDraw.Text(batch, small, $"{level.World.NameIt.ToUpperInvariant()}  {map.Tileset.ToUpperInvariant()}  {shape}", new Vector2(at.X, frame.Bottom + 22), new Color(160, 152, 190));
        }

        batch.End();
    }
}
