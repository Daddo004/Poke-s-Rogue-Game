using System.Diagnostics;

namespace PokeRogue.Game.Debugging;

/// <summary>Rolling frame timings shown by the debug overlay.</summary>
public sealed class FrameStats
{
    private readonly Stopwatch _update = new();
    private readonly Stopwatch _draw = new();
    private readonly Stopwatch _window = Stopwatch.StartNew();
    private double _updateAccum;
    private double _drawAccum;
    private double _worstFrameMs;
    private double _lastFrameEnd;
    private int _updates;
    private int _frames;

    public int Fps { get; private set; }

    public double UpdateMs { get; private set; }

    public double DrawMs { get; private set; }

    /// <summary>Longest wall-clock gap between two presented frames in the last window (hitches).</summary>
    public double WorstFrameMs { get; private set; }

    /// <summary>True right after a frame that closed a measuring window (about twice a second).</summary>
    public bool WindowClosed { get; private set; }

    public void BeginUpdate() => _update.Restart();

    /// <summary>Starts the measuring window afresh, e.g. after printing the stats, so the print is not counted as a hitch.</summary>
    public void RestartWindow()
    {
        _window.Restart();
        _lastFrameEnd = 0;
    }

    public void EndUpdate()
    {
        _updateAccum += _update.Elapsed.TotalMilliseconds;
        _updates++;
    }

    public void BeginDraw() => _draw.Restart();

    /// <summary>Measured in real time: with a fixed timestep, game time would always report 60.</summary>
    public void EndDraw()
    {
        _drawAccum += _draw.Elapsed.TotalMilliseconds;
        _frames++;

        var now = _window.Elapsed.TotalMilliseconds;
        _worstFrameMs = Math.Max(_worstFrameMs, now - _lastFrameEnd);
        _lastFrameEnd = now;
        WindowClosed = now >= 500;
        if (!WindowClosed)
            return;

        Fps = (int)Math.Round(_frames * 1000.0 / now);
        UpdateMs = _updates > 0 ? _updateAccum / _updates : 0;
        DrawMs = _frames > 0 ? _drawAccum / _frames : 0;
        WorstFrameMs = _worstFrameMs;
        _updateAccum = _drawAccum = _worstFrameMs = _lastFrameEnd = 0;
        _updates = _frames = 0;
        _window.Restart();
    }
}
