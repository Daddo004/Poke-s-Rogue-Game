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
    /// Alt+Invio (or --schermo-intero at launch): borderless full screen at the monitor's own resolution. The game is
    /// drawn at 640×360 and scaled by a whole number, so a 1080p screen shows it pixel-perfect at 3×, 1440p at 4×, 4K at 6×.
    /// </summary>
    public void ToggleFullScreen()
    {
        _graphics.HardwareModeSwitch = false;
        if (_graphics.IsFullScreen)
        {
            _graphics.IsFullScreen = false;
            _graphics.PreferredBackBufferWidth = VirtualScreen.DefaultWidth * 2;
            _graphics.PreferredBackBufferHeight = VirtualScreen.DefaultHeight * 2;
        }
        else
        {
            var display = GraphicsAdapter.DefaultAdapter.CurrentDisplayMode;
            _graphics.IsFullScreen = true;
            _graphics.PreferredBackBufferWidth = display.Width;
            _graphics.PreferredBackBufferHeight = display.Height;
        }

        _graphics.ApplyChanges();
        if (_services is not null)
        {
            _services.Settings.FullScreen = _graphics.IsFullScreen;
            _services.Settings.Save();
        }
    }

    /// <summary>A resized window gets a back buffer of its new size, so the scaling can grow with it.</summary>
    private void FollowWindowSize()
    {
        if (_graphics.IsFullScreen || Window.ClientBounds.Width <= 0 || Window.ClientBounds.Height <= 0)
            return;
        if (_graphics.PreferredBackBufferWidth == Window.ClientBounds.Width && _graphics.PreferredBackBufferHeight == Window.ClientBounds.Height)
            return;

        _graphics.PreferredBackBufferWidth = Window.ClientBounds.Width;
        _graphics.PreferredBackBufferHeight = Window.ClientBounds.Height;
        _graphics.ApplyChanges();
    }

    protected override void Initialize()
    {
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
        if (_services.Settings.FullScreen && !_graphics.IsFullScreen && !_capture.Active)
            ToggleFullScreen();
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
        _overlay = new DebugOverlay(_services) { Visible = _options.Preview is null || _options.Values.ContainsKey("debug") };

        // The title screen (phase 8.2), unless a run is asked for straight away (playtests: --pokemon, --allenatore,
        // --sfida, --da): that run still goes into the save and ends back in the menu.
        var direct = new[] { "pokemon", "allenatore", "sfida", "da", "evoluzioni", "nemici", "oggetti", "livello", "difficolta", "lastra", "infinita" }.Any(_options.Values.ContainsKey);
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
        if (_services.Input.Pressed(Keys.F3))
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
        _services.Fonts.Smooth = _services.Settings.SmoothText;
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
