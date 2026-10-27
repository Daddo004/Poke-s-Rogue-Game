using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using PokeRogue.Core.Data;
using PokeRogue.Game.Debugging;
using PokeRogue.Game.Graphics;
using PokeRogue.Game.Preview;
using PokeRogue.Game.Scenes;

namespace PokeRogue.Game;

public sealed class PokeRogueGame : Microsoft.Xna.Framework.Game
{
    public const int TargetFps = 60;

    private static readonly Color Letterbox = new(10, 8, 16);

    private readonly GraphicsDeviceManager _graphics;
    private readonly LaunchOptions _options;
    private readonly PreviewCapture _capture;
    private readonly SceneManager _scenes = new();
    private readonly FrameStats _stats = new();
    private readonly bool _blendFrames;

    /// <summary>Time not yet ticked away, in seconds (frame blending).</summary>
    private double _pending;

    private SpriteBatch _batch = null!;
    private GameServices _services = null!;
    private DebugOverlay _overlay = null!;
    private CursorArt _cursor = null!;

    public PokeRogueGame(string[] args)
    {
        _options = LaunchOptions.Parse(args);
        _capture = new PreviewCapture(_options);
        _graphics = new GraphicsDeviceManager(this)
        {
            PreferredBackBufferWidth = VirtualScreen.DefaultWidth * 2,
            PreferredBackBufferHeight = VirtualScreen.DefaultHeight * 2,
            SynchronizeWithVerticalRetrace = !_options.Values.ContainsKey("novsync"),
        };

        // Captures need exactly one simulation tick per drawn frame: a fixed timestep would catch up after slow
        // loading frames with several updates per draw, so "--wait N" would no longer mean N ticks.
        // Playing with vsync, a frame is drawn at every refresh of the monitor (144 Hz and up) while the game still
        // ticks at 60 Hz on its own clock, and scenes blend the motion between ticks (phase 8.6).
        _blendFrames = !_capture.Active && _graphics.SynchronizeWithVerticalRetrace;
        IsFixedTimeStep = !_capture.Active && !_blendFrames;
        TargetElapsedTime = TimeSpan.FromSeconds(1.0 / TargetFps);
        IsMouseVisible = true;
        Window.AllowUserResizing = true;
        Window.Title = "PokeRogue";
        Window.ClientSizeChanged += (_, _) => FollowWindowSize();
    }

    /// <summary>
    /// Alt+Invio (or --schermo-intero at launch): full screen, borderless at the monitor's own resolution unless Opzioni
    /// chose another (then the monitor switches mode). The game is drawn at 640×360 and scaled by a whole number, so a
    /// 1080p screen shows it pixel-perfect at 3×, 1440p at 4×, 4K at 6×.
    /// </summary>
    public void ToggleFullScreen()
    {
        if (_graphics.IsFullScreen)
        {
            _graphics.IsFullScreen = false;
            var window = WindowSize();
            _graphics.PreferredBackBufferWidth = window.X;
            _graphics.PreferredBackBufferHeight = window.Y;
        }
        else
        {
            var size = FullScreenSize();
            _graphics.HardwareModeSwitch = size != _monitor;
            _graphics.IsFullScreen = true;
            _graphics.PreferredBackBufferWidth = size.X;
            _graphics.PreferredBackBufferHeight = size.Y;
        }

        _graphics.ApplyChanges();
        if (!_graphics.IsFullScreen)
            CenterWindow();
        if (_services is not null)
        {
            _services.Settings.FullScreen = _graphics.IsFullScreen;
            _services.Settings.Save();
        }
    }

    /// <summary>The monitor's own resolution, read before any change of mode.</summary>
    private Point _monitor;

    /// <summary>Window sizes Opzioni offers: the game's 16:9 at common sizes, smaller than the monitor (room for the title bar).</summary>
    private static readonly Point[] WindowSizes =
        [new(640, 360), new(960, 540), new(1280, 720), new(1600, 900), new(1920, 1080), new(2560, 1440), new(3200, 1800), new(3840, 2160)];

    private IReadOnlyList<Point> Resolutions()
    {
        if (!_graphics.IsFullScreen)
            return WindowSizes.Where(s => s.X < _monitor.X && s.Y < _monitor.Y).ToList();

        // Full screen: the monitor's own first, then its other modes from the largest, none smaller than the game.
        var modes = GraphicsAdapter.DefaultAdapter.SupportedDisplayModes
            .Select(m => new Point(m.Width, m.Height))
            .Where(s => s != _monitor && s.X >= VirtualScreen.DefaultWidth && s.Y >= VirtualScreen.DefaultHeight)
            .Distinct()
            .OrderByDescending(s => s.X * s.Y);
        return [_monitor, .. modes];
    }

    private Point WindowSize()
    {
        var saved = _services is null ? Point.Zero : new Point(_services.Settings.WindowWidth, _services.Settings.WindowHeight);
        return saved.X > 0 && saved.Y > 0 && saved.X <= _monitor.X && saved.Y <= _monitor.Y
            ? saved
            : new Point(VirtualScreen.DefaultWidth * 2, VirtualScreen.DefaultHeight * 2);
    }

    private Point FullScreenSize()
    {
        var saved = _services is null ? Point.Zero : new Point(_services.Settings.FullScreenWidth, _services.Settings.FullScreenHeight);
        return saved.X > 0 && saved.Y > 0 && GraphicsAdapter.DefaultAdapter.SupportedDisplayModes.Any(m => m.Width == saved.X && m.Height == saved.Y)
            ? saved
            : _monitor;
    }

    /// <summary>Opzioni "Risoluzione": the window's size, or full screen's (leaving and entering it again to switch mode).</summary>
    private void SetResolution(Point size)
    {
        var settings = _services.Settings;
        if (_graphics.IsFullScreen)
        {
            (settings.FullScreenWidth, settings.FullScreenHeight) = size == _monitor ? (0, 0) : (size.X, size.Y);
            settings.Save();
            if (size != new Point(_graphics.PreferredBackBufferWidth, _graphics.PreferredBackBufferHeight))
            {
                ToggleFullScreen();
                ToggleFullScreen();
            }

            return;
        }

        (settings.WindowWidth, settings.WindowHeight) = (size.X, size.Y);
        settings.Save();
        _graphics.PreferredBackBufferWidth = size.X;
        _graphics.PreferredBackBufferHeight = size.Y;
        _graphics.ApplyChanges();
        CenterWindow();
    }

    private void CenterWindow()
    {
        var bounds = Window.ClientBounds;
        Window.Position = new Point(Math.Max(0, (_monitor.X - bounds.Width) / 2), Math.Max(32, (_monitor.Y - bounds.Height) / 2));
    }

    /// <summary>A resized window gets a back buffer of its new size, so the scaling can grow with it; the size is kept.</summary>
    private void FollowWindowSize()
    {
        if (_graphics.IsFullScreen || Window.ClientBounds.Width <= 0 || Window.ClientBounds.Height <= 0)
            return;
        if (_graphics.PreferredBackBufferWidth == Window.ClientBounds.Width && _graphics.PreferredBackBufferHeight == Window.ClientBounds.Height)
            return;

        _graphics.PreferredBackBufferWidth = Window.ClientBounds.Width;
        _graphics.PreferredBackBufferHeight = Window.ClientBounds.Height;
        _graphics.ApplyChanges();
        if (_services is not null && !_capture.Active)
        {
            (_services.Settings.WindowWidth, _services.Settings.WindowHeight) = (Window.ClientBounds.Width, Window.ClientBounds.Height);
            _services.Settings.Save();
        }
    }

    protected override void Initialize()
    {
        var display = GraphicsAdapter.DefaultAdapter.CurrentDisplayMode;
        _monitor = new Point(display.Width, display.Height);
        base.Initialize();
        if (_options.Values.ContainsKey("schermo-intero") && !_capture.Active && !_graphics.IsFullScreen)
            ToggleFullScreen();
    }

    protected override void LoadContent()
    {
        _batch = new SpriteBatch(GraphicsDevice);
        var screen = new VirtualScreen(GraphicsDevice, VirtualScreen.DefaultWidth, VirtualScreen.DefaultHeight);
        _services = new GameServices(GraphicsDevice, ContentPaths.FindContentRoot(), screen)
        {
            Exit = Exit,
            Settings = _options.Preview is null && !_capture.Active ? Settings.Load() : new Settings(),
            // Previews and captures play on an empty save in memory; "--salvataggio path" uses another file (playtests).
            SaveStore = _options.Preview is null && !_capture.Active ? new Core.Meta.SaveStore(_options.Str("salvataggio") ?? Core.Meta.SaveStore.DefaultPath) : null,
            Switch = _scenes.Switch,
            SetFullScreen = on =>
            {
                if (on != _graphics.IsFullScreen)
                    ToggleFullScreen();
            },
        };
        _services.Resolutions = Resolutions;
        _services.Resolution = () => _graphics.IsFullScreen
            ? new Point(_graphics.PreferredBackBufferWidth, _graphics.PreferredBackBufferHeight)
            : new Point(Window.ClientBounds.Width, Window.ClientBounds.Height);
        _services.SetResolution = SetResolution;
        if (_services.Settings.FullScreen && !_graphics.IsFullScreen && !_capture.Active)
            ToggleFullScreen();
        else if (!_graphics.IsFullScreen && !_capture.Active && WindowSize() is var window
                 && window != new Point(_graphics.PreferredBackBufferWidth, _graphics.PreferredBackBufferHeight))
        {
            _graphics.PreferredBackBufferWidth = window.X;
            _graphics.PreferredBackBufferHeight = window.Y;
            _graphics.ApplyChanges();
            CenterWindow();
        }
        var muted = _options.CapturePng is not null || _options.CaptureGif is not null || _options.Values.ContainsKey("muto");
        _services.Cries.Muted = _services.Sounds.Muted = _services.Music.Muted = muted;
        _services.ApplyVolumes();
        _services.Input.ToVirtual = screen.ToVirtual;
        _services.SetFillScreen = on =>
        {
            screen.Fill = on;
            _services.Settings.FillScreen = on;
            _services.Settings.Save();
        };
        screen.Fill = _services.Settings.FillScreen && !_capture.Active;
        if (_capture.Active)
            screen.FixedZoom = _options.Scale;
        _scenes.Switched += _services.Input.ClearRegions;
        _cursor = new CursorArt(GraphicsDevice);
        // The developer panel (frame timings) only with --debug, since the playtest of 2026-10-27.
        _overlay = new DebugOverlay(_services) { Visible = _options.Values.ContainsKey("debug") };

        // The title screen (phase 8.2), unless a run is asked for straight away (playtests: --pokemon, --allenatore,
        // --sfida, --da): that run still goes into the save and ends back in the menu.
        var direct = new[] { "pokemon", "allenatore", "sfida", "da", "evoluzioni", "nemici", "oggetti", "livello", "difficolta", "lastra", "infinita", "invincibile", "livelli" }.Any(_options.Values.ContainsKey);
        Scene scene = _options.Preview is { } preview
            ? PreviewRegistry.Create(preview, _services, _options)
                ?? throw new ArgumentException($"Unknown preview '{preview}'. Available: {string.Join(", ", PreviewRegistry.List.Select(p => p.Id))}")
            : !direct
                ? new Scenes.Menus.TitleScene(_services)
                : new GameplayScene(_services, _options.Str("pokemon"), trainerId: _options.Str("allenatore"))
                {
                    PendingEvolutions = _options.Values.ContainsKey("evoluzioni"),
                    PendingEnemies = _options.Values.ContainsKey("nemici"),
                    PendingLoot = _options.Values.ContainsKey("oggetti"),
                    ChallengeId = _options.Str("sfida"),
                    PlateId = _options.Str("lastra"),
                    Endless = _options.Values.ContainsKey("infinita"),
                    StartInvincible = _options.Values.ContainsKey("invincibile"),
                    StartLevels = _options.Int("livelli", 0),
                    StartTicks = _options.Str("da") is { } start ? Core.Run.Waves.WaveTimeline.ParseTime(start) : 0,
                    LevelId = _options.Str("livello") ?? GameplayScene.DefaultLevelId,
                    Difficulty = GameplayScene.ParseDifficulty(_options.Str("difficolta")),
                    Meta = _services.Meta,
                    BackToMenu = () => new Scenes.Menus.MainMenuScene(_services),
                };
        _scenes.Switch(scene);
    }

    protected override void Update(GameTime gameTime)
    {
        if (!_blendFrames)
        {
            Step();
            base.Update(gameTime);
            return;
        }

        // As many 60 Hz ticks as the time since the last frame holds (none on most frames of a 144 Hz monitor); the
        // remainder says how far the frame lies towards the next tick. A long stall (window dragged) is not caught up.
        const double step = 1.0 / TargetFps;
        _pending = Math.Min(_pending + gameTime.ElapsedGameTime.TotalSeconds, 0.25);
        while (_pending >= step)
        {
            _pending -= step;
            Step();
        }

        _services.FrameBlend = (float)(_pending / step);
        base.Update(gameTime);
    }

    /// <summary>One 60 Hz tick of input, audio and the current scene.</summary>
    private void Step()
    {
        _stats.BeginUpdate();
        _services.Input.WindowActive = IsActive;
        _services.Input.Update();
        _services.Sounds.Update();
        _services.ApplyVolumes();
        _services.Music.Update();
        // The right button is "back" in the menus: it never closes the game.
        if (_services.Input.Pressed(Keys.Escape) && !_services.Input.RightClicked && _scenes.Current is not { HandlesEscape: true })
            Exit();
        if (_services.Input.Pressed(Keys.F3) && _options.Values.ContainsKey("debug"))
            _overlay.Visible = !_overlay.Visible;
        if (_services.Input.Pressed(Keys.Enter) && (_services.Input.IsDown(Keys.LeftAlt) || _services.Input.IsDown(Keys.RightAlt)))
            ToggleFullScreen();

        _scenes.Update();
        _stats.EndUpdate();
    }

    protected override void Draw(GameTime gameTime)
    {
        _stats.BeginDraw();
        _services.Screen.Begin();
        _services.Fonts.Zoom = _services.Screen.Zoom;
        _services.Input.BeginRegions();
        _scenes.Draw(_batch);
        _services.Input.EndRegions();
        _overlay.Draw(_batch, _stats, _scenes.Current?.DebugLines ?? []);
        DrawPointer();
        _services.Screen.End();

        if (_capture.AfterDraw(GraphicsDevice, _services.Screen.Target, _services.Screen.Zoom))
            Exit();

        _services.Screen.Present(_batch, Letterbox);
        _stats.EndDraw();

        // "--bench": frame timings and this frame's GPU work on the console, readable where the overlay is not (stress tests).
        if (_stats.WindowClosed && _options.Values.ContainsKey("bench"))
        {
            var metrics = GraphicsDevice.Metrics;
            Console.WriteLine($"FPS {_stats.Fps}  update {_stats.UpdateMs:0.00} ms  draw {_stats.DrawMs:0.00} ms  frame max {_stats.WorstFrameMs:0} ms  " +
                $"draw calls {metrics.DrawCount}  sprites {metrics.SpriteCount}  GC {GC.CollectionCount(0)}/{GC.CollectionCount(1)}/{GC.CollectionCount(2)}  " +
                $"| {_scenes.Current?.DebugLines.FirstOrDefault()}");
            _stats.RestartWindow();
        }

        base.Draw(gameTime);
    }

    /// <summary>
    /// The game's own pointer (phase 8.1) over everything, once the mouse has been used: always in the menus, and while
    /// playing only for a few seconds after the mouse moves. The system's arrow hides over the picture.
    /// </summary>
    private void DrawPointer()
    {
        var input = _services.Input;
        var window = Mouse.GetState().Position;
        IsMouseVisible = !_services.Screen.Shows(window) || !IsActive;
        if (input.SimulatedPointer is null && (_capture.Active || !IsActive || !input.PointerEverUsed || !_services.Screen.Shows(window)))
            return;
        if (input.SimulatedPointer is null && !input.PointerAwake && _scenes.Current is not { ShowsPointer: true })
            return;

        _batch.BeginVirtual(samplerState: SamplerState.PointClamp);
        _cursor.Draw(_batch, input.Pointer, input.OverClickable);
        _batch.End();
    }

    protected override void UnloadContent()
    {
        _services.Screen.Dispose();
        base.UnloadContent();
    }
}
