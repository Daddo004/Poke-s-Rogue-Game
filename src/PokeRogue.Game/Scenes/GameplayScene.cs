using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using PokeRogue.Core.Run;
using PokeRogue.Core.Run.Moves;
using PokeRogue.Game.Graphics;
using PokeRogue.Game.Graphics.Floor;
using PokeRogue.Game.Rendering;
using PokeRogue.Game.Ui;

namespace PokeRogue.Game.Scenes;

/// <summary>A run in progress: the Core simulation, renderer, HUD, pause, level-ups, end of run and results.</summary>
public sealed class GameplayScene : Scene
{
    /// <summary>Milestone A: survive 10 minutes. Real levels end with a boss (phase 3.4).</summary>
    public const int PrototypeMinutes = 10;

    /// <summary>Pikachu's main move until characters arrive in Block 4.</summary>
    public const string StartingMoveId = "thundershock";

    /// <summary>Prototype spawn table (Bosco Smeraldo-like), until worlds and waves arrive in Blocks 3 and 7.</summary>
    private static readonly SpawnEntry[] PrototypeSpawns =
    [
        new(10, 3), new(13, 3), new(16, 4), new(19, 5), new(21, 2), new(23, 2),
    ];

    private readonly RunRenderer _renderer;
    private readonly Hud _hud;
    private readonly LevelUpWindow _levelUpWindow;
    private readonly PauseMenu _pause;
    private readonly RunEndBanner _banner;
    private readonly ResultsScreen _results;
    private readonly int _playerSpeciesId;
    private readonly int _initialSpawn;
    private readonly Action<RunState>? _setup;

    private RunState _run = null!;
    private LevelUpFlow _levelUp = null!;
    private Phase _phase;

    /// <param name="initialSpawn">Enemies spawned immediately (stress tests).</param>
    /// <param name="autopilot">Player picks up gems and walks in circles, invincible (previews and benchmarks).</param>
    /// <param name="setup">Previews: prepares the run right after it is created.</param>
    public GameplayScene(GameServices services, int playerSpeciesId = 25, int initialSpawn = 0, bool autopilot = false, Action<RunState>? setup = null)
        : base(services)
    {
        _playerSpeciesId = playerSpeciesId;
        _initialSpawn = initialSpawn;
        _setup = setup;
        Autopilot = autopilot;
        _renderer = new RunRenderer(services, FloorStyle.MeadowDecorated);
        _hud = new Hud(services);
        _levelUpWindow = new LevelUpWindow(services);
        _pause = new PauseMenu(services, _hud);
        _banner = new RunEndBanner(services);
        _results = new ResultsScreen(services, _hud);
    }

    private enum Phase
    {
        Playing,
        Paused,
        Ending,
        Results,
    }

    public RunState Run => _run;

    /// <summary>Previews: e.g. the status icon style.</summary>
    public RunRenderer Renderer => _renderer;

    /// <summary>Previews: drive the player automatically and auto-confirm level-ups.</summary>
    public bool Autopilot { get; set; }

    /// <summary>Simulation ticks per frame (fast-forward for playtests: F11, or --speed N).</summary>
    public int Speed { get; set; } = 1;

    /// <summary>Playtests (--evoluzioni): level-ups also offer the evolved moves still waiting for approval.</summary>
    public bool PendingEvolutions { get; init; }

    public override bool HandlesEscape => true;

    public bool IsPauseOpen => _phase == Phase.Paused;

    public override void Enter() => Restart();

    public override IReadOnlyList<string> DebugLines =>
    [
        $"tempo {_run.Elapsed:mm\\:ss}   nemici {_run.Enemies.AliveCount} (slot {_run.Enemies.HighWater})   proiettili {_run.MoveSystem.Projectiles.ActiveCount}   particelle {_renderer.ParticleCount}   chunk {_renderer.Floor.CachedChunks}",
        $"PS {_run.Player.Hp:0}/{_run.Player.MaxHp:0}   pos {_run.Player.Position.X:0},{_run.Player.Position.Y:0}   vel {_run.Player.MoveSpeed:0} px/s" +
            $"{(_run.Player.Invincible ? "   [INVINCIBILE]" : "")}{(_run.Spawner.Enabled ? "" : "   [SPAWN OFF]")}",
        $"Lv {_run.Player.Level}   Esp {_run.Player.Xp:0}/{_run.Player.XpToNext}   gemme {_run.Pickups.Gems.ActiveCount}   raggio {_run.Player.PickupRadius:0}   " +
            string.Join(" ", _run.Player.Moves.Select(m => $"{m.Definition.NameIt} {m.Level}").Concat(_run.Player.Items.Select(i => $"{i.Definition.NameIt} {i.Level}"))),
        $"F1 invincibile  F2 +1 livello  F4 +100  F9 +500  F6 +1 min  F7 KO  F8 spawn  F10 fine  F11 velocita x{Speed}  F12 hitbox  F3 overlay",
    ];

    public override void Update()
    {
        var input = Services.Input;
        switch (_phase)
        {
            case Phase.Paused:
                switch (_pause.Update(input))
                {
                    case PauseChoice.Resume:
                        _pause.Close();
                        _phase = Phase.Playing;
                        break;
                    case PauseChoice.Restart:
                        Restart();
                        break;
                    case PauseChoice.Quit:
                        Services.Exit();
                        break;
                }

                return;

            case Phase.Ending:
                _run.Tick(default);
                _renderer.Update(_run);
                if (_banner.Update(input, autoContinue: Autopilot))
                {
                    _banner.Stop();
                    _results.Open(_run);
                    _phase = Phase.Results;
                }

                return;

            case Phase.Results:
                switch (_results.Update(Autopilot ? null : input))
                {
                    case ResultsChoice.Restart:
                        Restart();
                        break;
                    case ResultsChoice.Quit:
                        Services.Exit();
                        break;
                }

                return;
        }

        HandleCheats();
        if (input.Pressed(Keys.Escape) || input.Pressed(Buttons.Start))
        {
            OpenPause();
            return;
        }

        // Level-up: the game freezes (effects keep playing) until a card is chosen.
        if (_levelUp.Active)
        {
            _levelUp.Update(Autopilot ? null : input);
            if (Autopilot && _levelUpWindow.Interactive)
                _levelUpWindow.ConfirmAt(AutoPilot.ChooseLevelUp(_run));
            _renderer.Update(_run, paused: true);
            _hud.Update(_run);
            return;
        }

        for (var step = 0; step < Speed; step++)
        {
            var move = Autopilot ? AutoPilot.Input(_run) : input.Move.ToNumerics();
            _run.Tick(new PlayerInput(move));
            _renderer.Update(_run);
            _hud.Update(_run);

            if (_run.IsOver)
            {
                BeginEnding();
                break;
            }

            if (_levelUp.Active)
                break;
        }
    }

    public override void Draw(SpriteBatch batch)
    {
        _renderer.Draw(batch, _run);
        if (_phase is Phase.Playing or Phase.Paused)
            _hud.Draw(batch, _run);

        _levelUpWindow.Draw(batch);
        _pause.Draw(batch, _run);
        _banner.Draw(batch);
        _results.Draw(batch);
    }

    /// <summary>Previews and the Esc key.</summary>
    public void OpenPause()
    {
        if (_phase != Phase.Playing || _levelUp.Active)
            return;

        _pause.Open();
        _phase = Phase.Paused;
    }

    /// <summary>Previews and cheats: ends the run now.</summary>
    public void ForceEnd(RunOutcome outcome)
    {
        _run.End(outcome);
        BeginEnding();
    }

    private void BeginEnding()
    {
        if (_phase == Phase.Ending)
            return;

        // No digits here: the body font's 5 reads as an S; the time is on the results screen.
        var subtitle = _run.Outcome == RunOutcome.Victory
            ? "Hai resistito fino alla fine!"
            : $"{_run.Player.Species.NameIt} non può più lottare!";
        _banner.Start(_run.Outcome, subtitle);
        _phase = Phase.Ending;
    }

    private void Restart()
    {
        var pokedex = Services.Pokedex.Value.Species;
        var view = new System.Numerics.Vector2(Services.Screen.Width, Services.Screen.Height);
        _run = new RunState(pokedex, Services.TypeChart.Value, _playerSpeciesId, PrototypeSpawns, view, seed: Environment.TickCount)
        {
            TimeLimitTicks = RunClock.TicksPerSecond * 60 * PrototypeMinutes,
            MovePool = Services.Moves.Value.Enabled,
            Evolutions = PendingEvolutions
                ? Services.Moves.Value.All.Where(m => m.IsEvolution).Select(m => m with { Enabled = true }).ToList()
                : Services.Moves.Value.Evolutions,
            ItemPool = Services.Items.Value.Enabled,
            LevelUpRules = Services.LevelUpRules.Value,
            StatusRules = Services.Statuses.Value,
        };
        _run.Player.Moves.Add(new MoveInstance(Services.Moves.Value[StartingMoveId]));
        _setup?.Invoke(_run);

        _renderer.Reset(_run);
        _levelUp = new LevelUpFlow(_run, _levelUpWindow);
        _pause.Close();
        _banner.Stop();
        _results.Close();
        _phase = Phase.Playing;

        // Load sprites up front so the first spawn of a species does not hitch.
        Services.Sprites.Get(_playerSpeciesId);
        foreach (var entry in PrototypeSpawns)
            Services.Sprites.Get(entry.SpeciesId);

        if (Autopilot)
            _run.Player.Invincible = true;
        if (_initialSpawn > 0)
        {
            _run.Spawner.MaxAlive = System.Math.Max(_run.Spawner.MaxAlive, _initialSpawn);
            SpawnBurst(_initialSpawn);
        }
    }

    private void HandleCheats()
    {
        var input = Services.Input;
        if (input.Pressed(Keys.F1))
            _run.Player.Invincible = !_run.Player.Invincible;
        if (input.Pressed(Keys.F2))
            _run.GrantLevel();
        if (input.Pressed(Keys.F4))
            SpawnBurst(100);
        if (input.Pressed(Keys.F9))
            SpawnBurst(500);
        if (input.Pressed(Keys.F6))
            _run.AdvanceTime(RunClock.TicksPerSecond * 60);
        if (input.Pressed(Keys.F7))
            KillVisible();
        if (input.Pressed(Keys.F8))
            _run.Spawner.Enabled = !_run.Spawner.Enabled;
        if (input.Pressed(Keys.F10))
            ForceEnd(input.IsDown(Keys.LeftShift) ? RunOutcome.Defeat : RunOutcome.Victory);
        if (input.Pressed(Keys.F11))
            Speed = Speed == 1 ? 4 : 1;
        if (input.Pressed(Keys.F12))
            _renderer.ShowHitboxes = !_renderer.ShowHitboxes;
    }

    private void SpawnBurst(int count)
    {
        for (var i = 0; i < count; i++)
            _run.Spawner.SpawnOne(_run);
    }

    private void KillVisible()
    {
        var view = _renderer.Camera.VisibleWorld;
        var items = _run.Enemies.Items;
        for (var i = 0; i < items.Length; i++)
        {
            if (items[i].IsAlive && view.Contains(items[i].Position.ToXna()))
                _run.DamageEnemy(i, float.MaxValue);
        }
    }
}
