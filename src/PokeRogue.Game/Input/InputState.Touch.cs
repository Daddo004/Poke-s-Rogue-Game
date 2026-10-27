using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Input.Touch;

namespace PokeRogue.Game.Input;

/// <summary>
/// The touch screen (Android). Outside a run, and in every window during one, a finger is the mouse: it points where it
/// lies, a tap clicks where it lifts, and dragging up or down scrolls like the wheel. While playing
/// (<see cref="GameControls"/>, set by the scene every tick) a finger on the left half is a floating joystick and the
/// round buttons on the right press the ability, the special move, the trainer and the pause. The phone's back
/// button is Esc.
/// </summary>
public sealed partial class InputState
{
    /// <summary>How far the joystick's knob goes from its base, in virtual pixels.</summary>
    public const float StickRadius = 30f;

    /// <summary>A finger that travels this far is a drag, not a tap.</summary>
    private const float TapSlop = 10f;

    /// <summary>Virtual pixels of drag per notch of the wheel.</summary>
    private const float DragNotch = 22f;

    /// <summary>The buttons drawn while playing by touch, in virtual pixels (640×360).</summary>
    public enum TouchButton
    {
        Ability,
        Special,
        Trainer,
        Pause,
    }

    public static readonly TouchButton[] TouchButtons = Enum.GetValues<TouchButton>();

    public static (Vector2 Center, float Radius) ButtonPlace(TouchButton button) => button switch
    {
        TouchButton.Ability => (new Vector2(586, 306), 24),
        TouchButton.Special => (new Vector2(526, 322), 20),
        TouchButton.Trainer => (new Vector2(600, 248), 18),
        _ => (new Vector2(620, 70), 13),
    };

    public static Keys ButtonKey(TouchButton button) => button switch
    {
        TouchButton.Ability => Keys.Space,
        TouchButton.Special => Keys.E,
        TouchButton.Trainer => Keys.Q,
        _ => Keys.Escape,
    };

    /// <summary>A run is being played: fingers move and press the round buttons. Set by the scene each tick, cleared after it is read.</summary>
    public bool GameControls { get; set; }

    /// <summary>Which round buttons the run has (no ability key, no trainer: no button); set with <see cref="GameControls"/>.</summary>
    public HashSet<TouchButton> ShownButtons { get; } = [TouchButton.Pause];

    /// <summary>A finger has touched the screen since the game started: the touch controls are drawn.</summary>
    public bool TouchEverUsed { get; private set; }

    /// <summary>The last thing used to point was a finger (no mouse pointer is drawn then).</summary>
    public bool LastWasTouch { get; private set; }

    /// <summary>The joystick, length ≤ 1, screen space (+Y down); zero without a finger on it.</summary>
    public Vector2 TouchStick { get; private set; }

    /// <summary>Where the joystick's finger came down and where it is now, in virtual pixels; null without one.</summary>
    public (Vector2 Base, Vector2 Knob)? StickDrawn => _stickId is null ? null : (_stickBase, _stickBase + TouchStick * StickRadius);

    /// <summary>The round buttons held down now, for their pressed look.</summary>
    public HashSet<TouchButton> HeldButtons { get; } = [];

    private bool _gameControls;
    private int? _stickId;
    private Vector2 _stickBase;
    private readonly Dictionary<int, TouchButton> _buttonTouches = [];

    /// <summary>The finger acting as the mouse: where it came down, how far it was dragged, the drag not yet turned into notches.</summary>
    private int? _pointerId;
    private Vector2 _pointerStart;
    private bool _pointerDragged;
    private float _dragLeft;

    private void UpdateTouch()
    {
        if (OperatingSystem.IsAndroid() && (Keyboard.GetState().IsKeyDown(Keys.Back) && !_previousKeys.IsKeyDown(Keys.Back)
                                            || _pad.IsButtonDown(Buttons.Back) && !_previousPad.IsButtonDown(Buttons.Back)))
            _pressedByPointer.Add(Keys.Escape);

        // The scene said last tick whether a run is being played; a change of mode lets go of everything held.
        if (GameControls != _gameControls)
        {
            _gameControls = GameControls;
            _stickId = null;
            TouchStick = Vector2.Zero;
            _buttonTouches.Clear();
            _pointerId = null;
        }

        GameControls = false;
        if (!TouchPanel.GetCapabilities().IsConnected)
            return;

        foreach (var touch in TouchPanel.GetState())
        {
            TouchEverUsed = true;
            LastWasTouch = true;
            var at = ToVirtual?.Invoke(touch.Position.ToPoint()) ?? touch.Position;
            if (_gameControls)
                GameTouch(touch, at);
            else
                PointerTouch(touch, at);
        }

        HeldButtons.Clear();
        foreach (var button in _buttonTouches.Values)
            HeldButtons.Add(button);
    }

    private void GameTouch(TouchLocation touch, Vector2 at)
    {
        switch (touch.State)
        {
            case TouchLocationState.Pressed:
                foreach (var button in TouchButtons)
                {
                    var (center, radius) = ButtonPlace(button);
                    if (ShownButtons.Contains(button) && Vector2.Distance(at, center) <= radius * 1.35f)
                    {
                        _buttonTouches[touch.Id] = button;
                        _pressedByPointer.Add(ButtonKey(button));
                        return;
                    }
                }

                if (_stickId is null && at.X < 320)
                {
                    _stickId = touch.Id;
                    _stickBase = at;
                    TouchStick = Vector2.Zero;
                }

                break;
            case TouchLocationState.Moved when touch.Id == _stickId:
                // A finger pulled past the rim drags the base along: the joystick follows the thumb.
                var offset = at - _stickBase;
                if (offset.Length() > StickRadius)
                {
                    _stickBase = at - Vector2.Normalize(offset) * StickRadius;
                    offset = at - _stickBase;
                }

                var length = offset.Length();
                TouchStick = length < 3f ? Vector2.Zero : offset / StickRadius;
                break;
            case TouchLocationState.Released or TouchLocationState.Invalid:
                if (touch.Id == _stickId)
                {
                    _stickId = null;
                    TouchStick = Vector2.Zero;
                }

                _buttonTouches.Remove(touch.Id);
                break;
        }
    }

    private void PointerTouch(TouchLocation touch, Vector2 at)
    {
        switch (touch.State)
        {
            case TouchLocationState.Pressed when _pointerId is null:
                _pointerId = touch.Id;
                _pointerStart = at;
                _pointerDragged = false;
                _dragLeft = 0f;
                MovePointer(at);
                break;
            case TouchLocationState.Moved when touch.Id == _pointerId:
                if (Vector2.Distance(at, _pointerStart) > TapSlop)
                    _pointerDragged = true;
                if (_pointerDragged)
                {
                    // The finger going up pulls the list down, as on any phone: the wheel turned towards the player.
                    _dragLeft += at.Y - Pointer.Y;
                    while (Math.Abs(_dragLeft) >= DragNotch)
                    {
                        var notch = Math.Sign(_dragLeft);
                        _dragLeft -= notch * DragNotch;
                        Wheel = notch;
                    }
                }

                MovePointer(at);
                break;
            case TouchLocationState.Released when touch.Id == _pointerId:
                _pointerId = null;
                MovePointer(at);
                // Update presses the key of a hint tapped this way, as for a click.
                Clicked |= !_pointerDragged;
                break;
            case TouchLocationState.Invalid when touch.Id == _pointerId:
                _pointerId = null;
                break;
        }
    }

    private void MovePointer(Vector2 at)
    {
        PointerMoved = true;
        Pointer = at;
        PointerIdleTicks = 0;
        PointerEverUsed = true;
    }
}
