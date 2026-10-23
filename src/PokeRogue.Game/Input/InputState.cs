using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace PokeRogue.Game.Input;

/// <summary>Keyboard and first gamepad, sampled once per update, with edge detection.</summary>
public sealed class InputState
{
    private KeyboardState _keys;
    private KeyboardState _previousKeys;
    private GamePadState _pad;
    private GamePadState _previousPad;

    public void Update()
    {
        _previousKeys = _keys;
        _previousPad = _pad;
        _keys = Keyboard.GetState();
        _pad = GamePad.GetState(PlayerIndex.One);
    }

    public bool IsDown(Keys key) => _keys.IsKeyDown(key);

    public bool Pressed(Keys key) => _keys.IsKeyDown(key) && !_previousKeys.IsKeyDown(key);

    public bool Pressed(Buttons button) => _pad.IsButtonDown(button) && !_previousPad.IsButtonDown(button);

    /// <summary>Movement from WASD / arrows / left stick, length ≤ 1, screen space (+Y down).</summary>
    public Vector2 Move
    {
        get
        {
            var v = Vector2.Zero;
            if (IsDown(Keys.A) || IsDown(Keys.Left)) v.X -= 1;
            if (IsDown(Keys.D) || IsDown(Keys.Right)) v.X += 1;
            if (IsDown(Keys.W) || IsDown(Keys.Up)) v.Y -= 1;
            if (IsDown(Keys.S) || IsDown(Keys.Down)) v.Y += 1;

            var stick = _pad.ThumbSticks.Left;
            if (stick.LengthSquared() > 0.04f)
                v += new Vector2(stick.X, -stick.Y);

            return v.LengthSquared() > 1f ? Vector2.Normalize(v) : v;
        }
    }
}
