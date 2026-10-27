using Microsoft.Xna.Framework.Graphics;

namespace PokeRogue.Game.Scenes;

/// <summary>Holds the active scene; a switch requested during Update takes effect before the next one.</summary>
public sealed class SceneManager
{
    private Scene? _pending;

    public Scene? Current { get; private set; }

    public void Switch(Scene scene) => _pending = scene;

    public void Update()
    {
        if (_pending is not null)
        {
            Current?.OnLeave();
            Current = _pending;
            _pending = null;
            Current.Enter();
        }

        Current?.Update();
    }

    public void Draw(SpriteBatch batch) => Current?.Draw(batch);
}
