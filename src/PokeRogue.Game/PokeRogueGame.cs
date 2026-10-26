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

    private SpriteBatch _batch = null!;
    private GameServices _services = null!;
    private DebugOverlay _overlay = null!;

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
        IsFixedTimeStep = !_capture.Active;
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
            SetFullScreen = on =>
            {
                if (on != _graphics.IsFullScreen)
                    ToggleFullScreen();
            },
        };
        if (_services.Settings.FullScreen && !_graphics.IsFullScreen && !_capture.Active)
            ToggleFullScreen();
        _services.Cries.Muted = _options.CapturePng is not null || _options.CaptureGif is not null || _options.Values.ContainsKey("muto");
        _overlay = new DebugOverlay(_services) { Visible = _options.Preview is null || _options.Values.ContainsKey("debug") };

        Scene scene = _options.Preview is { } preview
            ? PreviewRegistry.Create(preview, _services, _options)
                ?? throw new ArgumentException($"Unknown preview '{preview}'. Available: {string.Join(", ", PreviewRegistry.List.Select(p => p.Id))}")
            : new GameplayScene(_services, _options.Str("pokemon"))
            {
                PendingEvolutions = _options.Values.ContainsKey("evoluzioni"),
                PendingEnemies = _options.Values.ContainsKey("nemici"),
                PendingLoot = _options.Values.ContainsKey("oggetti"),
                StartTicks = _options.Str("da") is { } start ? Core.Run.Waves.WaveTimeline.ParseTime(start) : 0,
            };
        _scenes.Switch(scene);
    }

    protected override void Update(GameTime gameTime)
    {
        _stats.BeginUpdate();
        _services.Input.Update();
        if (_services.Input.Pressed(Keys.Escape) && _scenes.Current is not { HandlesEscape: true })
            Exit();
        if (_services.Input.Pressed(Keys.F3))
            _overlay.Visible = !_overlay.Visible;
        if (_services.Input.Pressed(Keys.Enter) && (_services.Input.IsDown(Keys.LeftAlt) || _services.Input.IsDown(Keys.RightAlt)))
            ToggleFullScreen();

        _scenes.Update();
        _stats.EndUpdate();
        base.Update(gameTime);
    }

    protected override void Draw(GameTime gameTime)
    {
        _stats.BeginDraw();
        _services.Screen.Begin();
        _scenes.Draw(_batch);
        _overlay.Draw(_batch, _stats, _scenes.Current?.DebugLines ?? []);
        _services.Screen.End();

        if (_capture.AfterDraw(GraphicsDevice, _services.Screen.Target))
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

    protected override void UnloadContent()
    {
        _services.Screen.Dispose();
        base.UnloadContent();
    }
}
