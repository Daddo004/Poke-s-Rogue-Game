using Microsoft.Xna.Framework.Graphics;

namespace PokeRogue.Game.Scenes;

/// <summary>A screen of the game. Updates at the fixed 60 Hz tick and draws into the virtual screen.</summary>
public abstract class Scene(GameServices services)
{
    protected GameServices Services { get; } = services;

    public virtual void Enter() { }

    public abstract void Update();

    public abstract void Draw(SpriteBatch batch);

    /// <summary>Extra lines for the debug overlay.</summary>
    public virtual IReadOnlyList<string> DebugLines => [];

    /// <summary>When true the scene uses Esc itself (pause); otherwise Esc closes the game.</summary>
    public virtual bool HandlesEscape => false;
}
