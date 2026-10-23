using FontStashSharp;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PokeRogue.Core.Data;
using PokeRogue.Core.Geometry;
using PokeRogue.Game.Graphics;
using PokeRogue.Game.Scenes;

namespace PokeRogue.Game.Preview.Artboard;

/// <summary>
/// Art-direction mock-ups for approval (phase 0.6). Nothing here is gameplay code.
/// Variants: hud-a (Vampire Survivors-like HUD), hud-b (Pokémon battle-box HUD), menu (character select), style (fonts, colours, frames, FX).
/// </summary>
public sealed partial class ArtboardScene : Scene
{
    private static readonly Color Ink = new(16, 12, 28);
    private static readonly Color Paper = new(244, 240, 228);
    private static readonly Color Gold = new(248, 208, 72);

    private readonly string _variant;
    private readonly string _fontName;
    private readonly ArtboardAssets _art;
    private readonly List<MockMon> _mons = [];
    private readonly Vector2 _player = new(320, 200);
    private readonly Random _random = new(3);
    private MockMon _hero;
    private int _tick;

    public ArtboardScene(GameServices services, string variant, string? font) : base(services)
    {
        _variant = variant;
        _fontName = font ?? "PixelifySans";
        _art = new ArtboardAssets(services.Device);
    }

    private SpriteFontBase Font(float size) => Services.Fonts.Get(_fontName, size);

    /// <summary>Tiny labels always use Silkscreen at its 8 px design size; other fonts blur below 16 px.</summary>
    private SpriteFontBase Small => Services.Fonts.Get("Silkscreen-Regular", 8);

    private SpriteFontBase Body => Font(16);

    public override void Enter()
    {
        if (_variant is "hud-a" or "hud-b")
            BuildHorde();
        else if (_variant == "menu")
            _hero = Mon(25, new Vector2(470, 150), Direction8.Down, PmdAnimations.Walk, 2f);
    }

    public override void Update()
    {
        _tick++;
        _hero.Animator.Update();
        for (var i = 0; i < _mons.Count; i++)
        {
            var m = _mons[i];
            m.Animator.Update();
            _mons[i] = m;
        }
    }

    public override void Draw(SpriteBatch batch)
    {
        switch (_variant)
        {
            case "menu":
                DrawMenu(batch);
                break;
            case "style":
                DrawStyleBoard(batch);
                break;
            default:
                DrawGameplay(batch, _variant == "hud-b");
                break;
        }
    }

    private MockMon Mon(int species, Vector2 position, Direction8 direction, string anim, float scale = 1f, bool elite = false)
    {
        var set = Services.Sprites.Get(species)!;
        var animator = new SpriteAnimator();
        animator.Play(set.Resolve(anim));
        animator.Update(_random.Next(0, 40));
        return new MockMon(species, position, direction, animator, scale, elite);
    }

    private struct MockMon(int species, Vector2 position, Direction8 direction, SpriteAnimator animator, float scale, bool elite)
    {
        public int Species = species;
        public Vector2 Position = position;
        public Direction8 Direction = direction;
        public SpriteAnimator Animator = animator;
        public float Scale = scale;
        public bool Elite = elite;
        public bool Flash;
    }

    private void DrawMon(SpriteBatch batch, MockMon m, Color? tint = null)
    {
        var set = Services.Sprites.Get(m.Species)!;
        set.Draw(batch, m.Animator.Anim!, m.Animator.Frame, m.Direction, m.Position, tint ?? Color.White, m.Scale, m.Flash);
    }

    private void DrawShadow(SpriteBatch batch, MockMon m)
    {
        var set = Services.Sprites.Get(m.Species)!;
        var (w, h) = set.Data.ShadowSize switch { 0 => (10, 4), 2 => (22, 8), _ => (16, 6) };
        Services.Primitives.Ellipse(batch, m.Position + new Vector2(0, 1), (int)(w * m.Scale), (int)(h * m.Scale), new Color(0, 0, 0, 80));
    }
}
