using Microsoft.Xna.Framework.Graphics;

namespace PokeRogue.Game.Scenes;

/// <summary>Holds the active scene; a switch requested during Update takes effect before the next one.</summary>
public sealed class SceneManager
{
    private Scene? _pending;

    public Scene? Current { get; private set; }

    /// <summary>Another scene has just taken over (the mouse forgets what the last one drew).</summary>
    public event Action? Switched;

    public void Switch(Scene scene) => _pending = scene;

    public void Update()
    {
        if (_pending is not null)
        {
            Current?.OnLeave();
            Current = _pending;
            _pending = null;
            Switched?.Invoke();
            Current.Enter();
        }

        Current?.Update();
    }

    public void Draw(SpriteBatch batch) => Current?.Draw(batch);
}
