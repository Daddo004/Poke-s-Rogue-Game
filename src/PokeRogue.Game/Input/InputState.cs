using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace PokeRogue.Game.Input;

/// <summary>
/// Keyboard, first gamepad and mouse, sampled once per update, with edge detection.
/// The mouse (phase 8.1) works in every menu without each screen knowing much about it: what is drawn registers its
/// clickable <see cref="Region"/>s, and the next update finds what the pointer is over. A key hint drawn as a
/// <see cref="KeyRegion"/> ("[INVIO] GIOCA") presses its key when clicked; the right button presses Esc and the wheel
/// presses the arrows up and down.
/// </summary>
public sealed class InputState
{
    /// <summary>The pointer counts as in use for this long after it last moved or clicked (3 s).</summary>
    public const int PointerAwakeTicks = 180;

    private readonly record struct Hit(Rectangle Area, string? Tag, int Index, Keys? Key);

    private KeyboardState _keys;
    private KeyboardState _previousKeys;
    private GamePadState _pad;
    private GamePadState _previousPad;
    private MouseState _mouse;
    private MouseState _previousMouse;
    private readonly HashSet<Keys> _pressedByPointer = [];
    private List<Hit> _regions = [];
    private List<Hit> _drawing = [];

    /// <summary>Window pixels to virtual-screen pixels (set by the game once the screen exists).</summary>
    public Func<Point, Vector2>? ToVirtual { get; set; }

    /// <summary>False while the window is in the background: its clicks are not the game's.</summary>
    public bool WindowActive { get; set; } = true;

    /// <summary>Previews: a pointer held here instead of the real mouse (its hover shows in captures).</summary>
    public Vector2? SimulatedPointer { get; set; }

    /// <summary>Where the pointer is, in virtual-screen pixels.</summary>
    public Vector2 Pointer { get; private set; }

    public bool PointerMoved { get; private set; }

    /// <summary>Ticks since the pointer last moved, clicked or scrolled.</summary>
    public int PointerIdleTicks { get; private set; } = int.MaxValue / 2;

    /// <summary>The mouse has been used at all since the game started.</summary>
    public bool PointerEverUsed { get; private set; }

    public bool PointerAwake => PointerIdleTicks < PointerAwakeTicks || SimulatedPointer is not null;

    /// <summary>The left button went down this tick.</summary>
    public bool Clicked { get; private set; }

    public bool RightClicked { get; private set; }

    /// <summary>+1 for a notch of the wheel up, -1 down, 0 none.</summary>
    public int Wheel { get; private set; }

    public void Update()
    {
        _previousKeys = _keys;
        _previousPad = _pad;
        _previousMouse = _mouse;
        _keys = Keyboard.GetState();
        _pad = GamePad.GetState(PlayerIndex.One);
        _mouse = Mouse.GetState();
        _pressedByPointer.Clear();

        var at = SimulatedPointer ?? ToVirtual?.Invoke(_mouse.Position) ?? _mouse.Position.ToVector2();
        PointerMoved = SimulatedPointer is null ? _mouse.Position != _previousMouse.Position && WindowActive : at != Pointer;
        Pointer = at;
        Clicked = WindowActive && _mouse.LeftButton == ButtonState.Pressed && _previousMouse.LeftButton == ButtonState.Released;
        RightClicked = WindowActive && _mouse.RightButton == ButtonState.Pressed && _previousMouse.RightButton == ButtonState.Released;
        Wheel = WindowActive ? Math.Sign(_mouse.ScrollWheelValue - _previousMouse.ScrollWheelValue) : 0;

        if (PointerMoved || Clicked || RightClicked || Wheel != 0)
        {
            PointerIdleTicks = 0;
            PointerEverUsed = true;
        }
        else if (PointerIdleTicks < int.MaxValue / 2)
        {
            PointerIdleTicks++;
        }

        if (Clicked && Top() is { Key: { } key })
            _pressedByPointer.Add(key);
        if (RightClicked)
            _pressedByPointer.Add(Keys.Escape);
        if (Wheel > 0)
            _pressedByPointer.Add(Keys.Up);
        if (Wheel < 0)
            _pressedByPointer.Add(Keys.Down);
    }

    public bool IsDown(Keys key) => _keys.IsKeyDown(key);

    /// <summary>The key went down this tick, or the pointer pressed it (a key hint clicked, the right button, the wheel).</summary>
    public bool Pressed(Keys key) => _keys.IsKeyDown(key) && !_previousKeys.IsKeyDown(key) || _pressedByPointer.Contains(key);

    public bool Pressed(Buttons button) => _pad.IsButtonDown(button) && !_previousPad.IsButtonDown(button);

    /// <summary>Presses a key for this tick, as if typed (a button drawn on screen and clicked).</summary>
    public void Press(Keys key) => _pressedByPointer.Add(key);

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

    // ---- Clickable regions (phase 8.1) ----

    /// <summary>A frame starts drawing: its regions are collected anew.</summary>
    public void BeginRegions() => _drawing.Clear();

    /// <summary>The frame is drawn: its regions are what the next updates test the pointer against.</summary>
    public void EndRegions() => (_regions, _drawing) = (_drawing, _regions);

    /// <summary>Another screen took over: what the last one drew is no longer there to click.</summary>
    public void ClearRegions() => _regions.Clear();

    /// <summary>Something drawn that the pointer can point at: a cell, a row, a card. Later regions lie on top.</summary>
    public void Region(Rectangle area, string tag, int index = 0) => _drawing.Add(new Hit(area, tag, index, null));

    /// <summary>A button that presses <paramref name="key"/> when clicked.</summary>
    public void KeyRegion(Rectangle area, Keys key) => _drawing.Add(new Hit(area, null, 0, key));

    /// <summary>A window over everything: what lies under it cannot be pointed at.</summary>
    public void Blocker(Rectangle area) => _drawing.Add(new Hit(area, "", 0, null));

    /// <summary>The index of the region tagged <paramref name="tag"/> under the pointer, if it is the topmost there.</summary>
    public int? Hover(string tag) => PointerAwake && Top() is { Tag: { } t } hit && t == tag ? hit.Index : null;

    /// <summary>As <see cref="Hover"/>, only on a tick the pointer moved (a still mouse does not steal the keyboard's cursor).</summary>
    public int? HoverMoved(string tag) => PointerMoved ? Hover(tag) : null;

    /// <summary>The region tagged <paramref name="tag"/> clicked this tick.</summary>
    public int? Click(string tag) => Clicked ? Hover(tag) : null;

    /// <summary>The pointer is over something that can be clicked (the cursor turns into a hand).</summary>
    public bool OverClickable => PointerAwake && Top() is { } hit && (hit.Key is not null || hit.Tag is { Length: > 0 });

    private Hit? Top()
    {
        for (var i = _regions.Count - 1; i >= 0; i--)
        {
            if (_regions[i].Area.Contains(Pointer))
                return _regions[i];
        }

        return null;
    }
}
