using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PokeRogue.Core.Data;
using PokeRogue.Core.Geometry;
using PokeRogue.Game.Graphics;
using PokeRogue.Game.Graphics.Floor;
using PokeRogue.Game.Scenes;

namespace PokeRogue.Game.Preview;

/// <summary>Phase 1.1 preview: infinite procedural floor with a camera following Pikachu along a scripted path.</summary>
public sealed class FloorPreviewScene(GameServices services, string? variant) : Scene(services)
{
    private readonly FloorRenderer _floor = new(services.Device, FloorStyle.ById(variant));
    private readonly Camera2D _camera = new(services.Screen.Width, services.Screen.Height);
    private SpriteAnimator _animator;
    private Vector2 _position;
    private Direction8 _direction;
    private int _tick;

    public override void Enter()
    {
        _animator.Play(Services.Sprites.Get(25)!.Resolve(PmdAnimations.Walk));
        _position = PathAt(0);
        _camera.Position = _position;
    }

    public override void Update()
    {
        _tick++;
        var next = PathAt(_tick);
        var velocity = next - _position;
        _direction = Direction8Extensions.FromVector(new System.Numerics.Vector2(velocity.X, velocity.Y), _direction);
        _position = next;
        _camera.Position = _position;
        _animator.Update();
    }

    public override void Draw(SpriteBatch batch)
    {
        var p = Services.Primitives;
        var view = _camera.VisibleWorld;

        batch.BeginVirtual(samplerState: SamplerState.PointClamp, transformMatrix: _camera.Transform);
        _floor.Draw(batch, view);

        var props = _floor.Props(new Rectangle(view.X - 16, view.Y - 16, view.Width + 32, view.Height + 32)).ToList();
        foreach (var prop in props)
            p.Ellipse(batch, prop.Feet, prop.ShadowWidth, 4, Color.Black * 0.3f);
        p.Ellipse(batch, _position + new Vector2(0, 1), 16, 6, Color.Black * 0.32f);

        var pikachu = Services.Sprites.Get(25)!;
        var drawables = props.Select(prop => (prop.Feet.Y, Draw: (Action)(() =>
                batch.Draw(prop.Texture, prop.Feet - new Vector2(prop.Texture.Width / 2f, prop.Texture.Height - 1), Color.White))))
            .Append((_position.Y, Draw: () => pikachu.Draw(batch, _animator.Anim!, _animator.Frame, _direction, _position, Color.White)))
            .OrderBy(d => d.Item1);
        foreach (var d in drawables)
            d.Draw();
        batch.End();

        batch.BeginVirtual(samplerState: SamplerState.PointClamp);
        var label = $"Anteprima 1.1  -  pavimento: {_floor.Style.Id}   (chunk in cache: {_floor.CachedChunks})";
        UiDraw.TextOutlined(batch, Services.Fonts.Small(Services.Style), label, new Vector2(8, 8), new Color(244, 240, 228), new Color(16, 12, 28));
        batch.End();
    }

    /// <summary>A long, gently curving walk at ~75 px/s so the preview crosses many chunks.</summary>
    private static Vector2 PathAt(int tick)
    {
        var t = tick / 60f;
        return new Vector2(t * 62f + MathF.Sin(t * 0.9f) * 60f, MathF.Sin(t * 0.55f) * 90f + t * 18f);
    }
}
