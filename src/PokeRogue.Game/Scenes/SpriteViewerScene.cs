using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using PokeRogue.Core.Data;
using PokeRogue.Core.Geometry;
using PokeRogue.Game.Graphics;

namespace PokeRogue.Game.Scenes;

/// <summary>Debug browser for every Pokémon's PMD animations in all 8 directions.</summary>
public sealed class SpriteViewerScene : Scene
{
    private const int MaxSpecies = 493;
    private static readonly Color Background = new(28, 24, 40);
    private static readonly Color Panel = new(44, 38, 62);
    private static readonly Color TextColor = new(240, 236, 220);
    private static readonly Color Dim = new(150, 144, 170);

    private readonly SpriteAnimator[] _animators = new SpriteAnimator[9];
    private int _speciesId;
    private int _animIndex;
    private Direction8 _direction = Direction8.Down;
    private bool _paused;
    private bool _silhouette;
    private bool _autoRotate;
    private int _rotateTimer;

    public SpriteViewerScene(GameServices services, int speciesId = 25, string? anim = null) : base(services)
    {
        _speciesId = System.Math.Clamp(speciesId, 1, MaxSpecies);
        _animIndex = anim is null ? 1 : System.Math.Max(0, PmdAnimations.Required.ToList().IndexOf(anim));
    }

    private PokemonSpriteSet? Set => Services.Sprites.Get(_speciesId);

    private string AnimName => PmdAnimations.Required[_animIndex];

    public override void Enter() => Restart();

    public override void Update()
    {
        var input = Services.Input;
        var step = input.IsDown(Keys.LeftShift) ? 10 : 1;
        if (input.Pressed(Keys.Right) || input.Pressed(Keys.D)) ChangeSpecies(step);
        if (input.Pressed(Keys.Left) || input.Pressed(Keys.A)) ChangeSpecies(-step);
        if (input.Pressed(Keys.PageDown)) ChangeSpecies(50);
        if (input.Pressed(Keys.PageUp)) ChangeSpecies(-50);
        if (input.Pressed(Keys.Down) || input.Pressed(Keys.S)) ChangeAnim(1);
        if (input.Pressed(Keys.Up) || input.Pressed(Keys.W)) ChangeAnim(-1);
        if (input.Pressed(Keys.E)) _direction = (Direction8)(((int)_direction + 1) % 8);
        if (input.Pressed(Keys.Q)) _direction = (Direction8)(((int)_direction + 7) % 8);
        if (input.Pressed(Keys.Space)) _paused = !_paused;
        if (input.Pressed(Keys.F)) _silhouette = !_silhouette;
        if (input.Pressed(Keys.R)) _autoRotate = !_autoRotate;

        if (_autoRotate && ++_rotateTimer >= 45)
        {
            _rotateTimer = 0;
            _direction = (Direction8)(((int)_direction + 1) % 8);
        }

        if (_paused)
            return;

        for (var i = 0; i < _animators.Length; i++)
        {
            _animators[i].Update();
            if (_animators[i].Finished)
                _animators[i].Play(_animators[i].Anim!, loop: false, restart: true);
        }
    }

    public override void Draw(SpriteBatch batch)
    {
        var screen = Services.Screen;
        var prim = Services.Primitives;
        Services.Device.Clear(Background);
        batch.BeginVirtual(samplerState: SamplerState.PointClamp);

        var set = Set;
        var species = Services.Species(_speciesId);

        // Big preview
        var stage = new Rectangle(16, 40, 300, 220);
        prim.Rect(batch, stage, Panel);
        if (set is not null)
        {
            var anim = set.Resolve(AnimName);
            var feet = new Vector2(stage.Center.X, stage.Bottom - 50);
            DrawShadow(batch, set, feet, 3);
            set.Draw(batch, anim, _animators[8].Frame, _direction, feet, Color.White, 3f, _silhouette);
        }

        // Eight directions
        var cell = new Rectangle(332, 40, 70, 70);
        for (var d = 0; d < 8; d++)
        {
            var r = new Rectangle(cell.X + d % 4 * (cell.Width + 4), cell.Y + d / 4 * (cell.Height + 4), cell.Width, cell.Height);
            prim.Rect(batch, r, Panel);
            if (set is null)
                continue;

            var feet = new Vector2(r.Center.X, r.Bottom - 14);
            DrawShadow(batch, set, feet, 1);
            set.Draw(batch, set.Resolve(AnimName), _animators[d].Frame, (Direction8)d, feet, Color.White, 1f, _silhouette);
        }

        // Portrait
        if (Services.Sprites.Portrait(_speciesId) is { } portrait)
        {
            prim.Rect(batch, new Rectangle(330, 194, 84, 84), Panel);
            batch.Draw(portrait, new Rectangle(332, 196, 80, 80), Color.White);
        }

        DrawText(batch, species, set);
        batch.End();
    }

    private void DrawText(SpriteBatch batch, SpeciesData? species, PokemonSpriteSet? set)
    {
        var fonts = Services.Fonts.Available;
        if (fonts.Count == 0)
            return;

        var font = Services.Fonts.Get(fonts.Contains("Silkscreen-Regular") ? "Silkscreen-Regular" : fonts[0], 8);
        var big = Services.Fonts.Get(fonts.Contains("Silkscreen-Regular") ? "Silkscreen-Regular" : fonts[0], 16);

        var title = species is null ? $"#{_speciesId:D3}" : $"#{species.Id:D3} {species.NameIt}";
        UiDraw.Text(batch, big, title, new Vector2(16, 12), TextColor);

        var anim = set?.Resolve(AnimName);
        var lines = new List<string>
        {
            species is null ? "" : $"{species.GenusIt}  ·  Gen {species.Generation}  ·  {string.Join("/", species.Types)}",
            set is null ? "Nessuno sprite" : set.IsFallback ? "Sprite di riserva (HGSS)" : $"{AnimName}{(anim?.Name != AnimName ? $" -> {anim?.Name}" : "")}  {anim?.FrameWidth}x{anim?.FrameHeight}  {anim?.Durations.Count} frame  {anim?.TotalTicks} tick",
            $"Direzione: {_direction}{(_paused ? "  [PAUSA]" : "")}{(_silhouette ? "  [SAGOMA]" : "")}",
        };

        for (var i = 0; i < lines.Count; i++)
            UiDraw.Text(batch, font, lines[i], new Vector2(16, 268 + i * 12), i == 0 ? TextColor : Dim);

        UiDraw.Text(batch, font, "Frecce/AD: Pokemon (Shift x10, PagSu/Giu x50)   WS: animazione   QE: direzione   R: ruota   F: sagoma   Spazio: pausa", new Vector2(16, 340), Dim);
    }

    private void DrawShadow(SpriteBatch batch, PokemonSpriteSet set, Vector2 feet, int scale)
    {
        var (w, h) = set.Data.ShadowSize switch
        {
            0 => (10, 4),
            2 => (22, 8),
            _ => (16, 6),
        };
        Services.Primitives.Ellipse(batch, feet, w * scale, h * scale, new Color(0, 0, 0, 90));
    }

    private void ChangeSpecies(int delta)
    {
        _speciesId = ((_speciesId - 1 + delta) % MaxSpecies + MaxSpecies) % MaxSpecies + 1;
        Restart();
    }

    private void ChangeAnim(int delta)
    {
        _animIndex = (_animIndex + delta + PmdAnimations.Required.Count) % PmdAnimations.Required.Count;
        Restart();
    }

    private void Restart()
    {
        if (Set is not { } set)
            return;

        var anim = set.Resolve(AnimName);
        var loop = AnimName is PmdAnimations.Idle or PmdAnimations.Walk;
        for (var i = 0; i < _animators.Length; i++)
            _animators[i].Play(anim, loop, restart: true);
    }
}
