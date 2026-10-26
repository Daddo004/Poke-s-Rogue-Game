using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PokeRogue.Core.Data;
using PokeRogue.Core.Geometry;
using PokeRogue.Game.Graphics;

namespace PokeRogue.Game.Scenes.Menus;

/// <summary>A Pokémon standing in a menu, playing its animation (the character select's showcase, the main menu).</summary>
public sealed class MenuPokemon(GameServices services)
{
    private SpriteAnimator _animator;
    private string _anim = PmdAnimations.Idle;

    public int SpeciesId { get; private set; }

    /// <summary>Shows <paramref name="speciesId"/>; the animation restarts only when the species changes.</summary>
    public void Show(int speciesId, string anim = PmdAnimations.Idle)
    {
        if (speciesId == SpeciesId && anim == _anim)
            return;

        SpeciesId = speciesId;
        _anim = anim;
        _animator = new SpriteAnimator();
        if (services.Sprites.Get(speciesId) is { } set)
            _animator.Play(set.Resolve(anim));
    }

    public void Update() => _animator.Update();

    /// <summary>The Pokémon with its feet on <paramref name="feet"/>, over a soft shadow; <paramref name="tint"/> darkens a locked one.</summary>
    public void Draw(SpriteBatch batch, Vector2 feet, float scale, Color tint, Direction8 facing = Direction8.Down)
    {
        if (services.Sprites.Get(SpeciesId) is not { } set || _animator.Anim is not { } anim)
            return;

        var (w, h) = set.Data.ShadowSize switch { 0 => (10, 4), 2 => (22, 8), _ => (16, 6) };
        services.Primitives.Ellipse(batch, feet + new Vector2(0, 1), (int)(w * scale), (int)(h * scale), new Color(0, 0, 0, 90));
        set.Draw(batch, anim, _animator.Frame, facing, feet, tint, scale);
    }
}
