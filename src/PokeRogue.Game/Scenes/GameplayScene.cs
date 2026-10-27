using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using PokeRogue.Core.Run;
using PokeRogue.Core.Run.Characters;
using PokeRogue.Core.Run.Enemies;
using PokeRogue.Core.Run.Moves;
using PokeRogue.Core.Run.Waves;
using PokeRogue.Game.Graphics;
using PokeRogue.Game.Graphics.Floor;
using PokeRogue.Game.Rendering;
using PokeRogue.Game.Ui;

namespace PokeRogue.Game.Scenes;

/// <summary>A run in progress: the Core simulation, renderer, HUD, pause, level-ups, end of run and results.</summary>
public sealed class GameplayScene : Scene
{
    /// <summary>The level played when none is chosen (previews, tests): content/data/waves/bosco-smeraldo.json, the first one.</summary>
    public const string DefaultLevelId = "bosco-smeraldo";

    /// <summary>The level played (Block 7): a waves file, content/data/waves/&lt;id&gt;.json.</summary>
    public string LevelId { get; init; } = DefaultLevelId;

    /// <summary>The difficulty played (phase 7.3).</summary>
    public Core.Meta.Difficulty Difficulty { get; init; }

    /// <summary>"normale", "difficile" or "ultra" (also the enum's names) to a difficulty; Normale when unknown.</summary>
    public static Core.Meta.Difficulty ParseDifficulty(string? text) => text?.ToLowerInvariant() switch
    {
        "difficile" or "hard" => Core.Meta.Difficulty.Hard,
        "ultra" => Core.Meta.Difficulty.Ultra,
        _ => Core.Meta.Difficulty.Normal,
    };

    /// <summary>Pikachu's main move (characters.json): previews that build a run by hand give it more levels.</summary>
    public const string StartingMoveId = "thundershock";

    private readonly RunRenderer _renderer;
    private readonly MotionBlend _motion = new();
    private readonly Hud _hud;
    private readonly LevelUpWindow _levelUpWindow;
    private readonly PauseMenu _pause;
    private readonly RunEndBanner _banner;
    private readonly ResultsScreen _results;
    private readonly CharacterDefinition _character;

    /// <summary>Previews: play this character as given (e.g. with another special move) instead of the one named.</summary>
    public CharacterDefinition Character
    {
        get => _character;
        init => _character = value;
    }
    private readonly int _initialSpawn;
    private readonly Action<RunState>? _setup;

    private RunState _run = null!;
    private LevelUpFlow _levelUp = null!;
    private Phase _phase;

    private readonly Core.Run.Trainers.TrainerDefinition _trainer;

    /// <param name="characterId">The character played (characters.json); Pikachu when null.</param>
    /// <param name="initialSpawn">Enemies spawned immediately (stress tests).</param>
    /// <param name="autopilot">Player picks up gems and walks in circles, invincible (previews and benchmarks).</param>
    /// <param name="setup">Previews: prepares the run right after it is created.</param>
    /// <param name="trainerId">The trainer (trainers.json); Ash when null.</param>
    public GameplayScene(GameServices services, string? characterId = null, int initialSpawn = 0, bool autopilot = false, Action<RunState>? setup = null,
        string? trainerId = null)
        : base(services)
    {
        _character = services.Characters.Find(characterId ?? CharacterLibrary.DefaultId)
            ?? throw new ArgumentException($"Unknown character '{characterId}'. Available: {string.Join(", ", services.Characters.All.Select(c => c.Id))}");
        _trainer = services.Trainers.Value.Find(trainerId ?? Core.Run.Trainers.TrainerLibrary.DefaultId)
            ?? throw new ArgumentException($"Unknown trainer '{trainerId}'. Available: {string.Join(", ", services.Trainers.Value.All.Select(t => t.Id))}");
        _initialSpawn = initialSpawn;
        _setup = setup;
        Autopilot = autopilot;
        _renderer = new RunRenderer(services, Tileset.Forest);
        _hud = new Hud(services);
        _levelUpWindow = new LevelUpWindow(services);
        _pause = new PauseMenu(services, _hud);
        _banner = new RunEndBanner(services);
        _results = new ResultsScreen(services, _hud);
        _chestWindow = new ChestWindow(services);
        _evolution = new EvolutionWindow(services);
        _special = new SpecialWindow(services);
        _bossIntro = new BossIntro(services);
        _trainerIntro = new TrainerIntro(services);
        _plateWindow = new PlateWindow(services);
        _shop = new MerchantWindow(services);
    }

    private readonly PlateWindow _plateWindow;
    private readonly MerchantWindow _shop;

    /// <summary>Previews: Kecleon's shop.</summary>
    public MerchantWindow Shop => _shop;

    private readonly BossIntro _bossIntro;

    /// <summary>The challenged trainer's entrance (phase 5.3).</summary>
    private readonly TrainerIntro _trainerIntro;

    /// <summary>Ticks the game stays frozen while a trainer's Pokémon comes out of its Ball.</summary>
    private int _sendOutFreeze;

    /// <summary>A challenge run (phase 5.3, --sfida id): this trainer replaces the level's boss when the time runs out.</summary>
    public string? ChallengeId { get; init; }

    /// <summary>The Plate chosen for the run (phase 9.1, --lastra id); null for none.</summary>
    public string? PlateId { get; init; }

    /// <summary>
    /// The meta-progression (Block 6): the run gets the character's loadout and the trainer's grades from it, the missions
    /// it completes are announced, and its end is recorded (coins to the bank, Pokédex, rewards). Null for previews.
    /// </summary>
    public Core.Meta.MetaProgress? Meta { get; init; }

    /// <summary>Previews: the loadout to play with, instead of the meta-progression's (or the default one).</summary>
    public RunLoadout? Loadout { get; init; }

    /// <summary>Where "Esci" leads: the menu it came from; null closes the game (previews, launches with --pokemon).</summary>
    public Func<Scene>? BackToMenu { get; init; }

    /// <summary>What the end of the run brought, once recorded.</summary>
    public Core.Meta.RunReport? Report => _report;

    private Core.Meta.RunReport? _report;
    private bool _recorded;

    /// <summary>Missions already announced during this run.</summary>
    private readonly HashSet<string> _noticed = new(StringComparer.Ordinal);

    /// <summary>A run left from the pause before this many ticks is not recorded (an immediate restart is not a run).</summary>
    private const int MinRecordedTicks = RunClock.TicksPerSecond * 10;

    /// <summary>The ground walked this run, for the map in the pause (phase 3.6).</summary>
    private readonly ExploredMap _explored = new();

    private readonly ChestWindow _chestWindow;

    /// <summary>Previews: the chest window, e.g. to open one by hand.</summary>
    public ChestWindow ChestWindow => _chestWindow;

    private readonly EvolutionWindow _evolution;

    /// <summary>Previews: the player's evolution, e.g. to script a choice.</summary>
    public EvolutionWindow Evolution => _evolution;

    private readonly SpecialWindow _special;

    /// <summary>Previews: the special move's window.</summary>
    public SpecialWindow Special => _special;

    private enum Phase
    {
        Playing,
        Paused,
        Ending,
        Results,
    }

    public RunState Run => _run;

    /// <summary>Previews: e.g. a mission's notice.</summary>
    public Hud Hud => _hud;

    /// <summary>Previews: e.g. the status icon style.</summary>
    public RunRenderer Renderer => _renderer;

    /// <summary>Previews: drive the player automatically and auto-confirm level-ups.</summary>
    public bool Autopilot { get; set; }

    /// <summary>Previews: the autopilot also presses the trainer's key; off when a preview presses it by hand.</summary>
    public bool AutoPilotTrainer { get; set; } = true;

    private bool _pressTrainer;

    /// <summary>Previews: the trainer's key is pressed on the next tick.</summary>
    public void PressTrainer() => _pressTrainer = true;

    /// <summary>Simulation ticks per frame (fast-forward for playtests: F11, or --speed N).</summary>
    public int Speed { get; set; } = 1;

    /// <summary>Playtests (--evoluzioni): level-ups also offer the evolved moves still waiting for approval.</summary>
    public bool PendingEvolutions { get; init; }

    /// <summary>Playtests (--nemici): enemies also use the fighting profiles still waiting for approval.</summary>
    public bool PendingEnemies { get; init; }

    /// <summary>Playtests and previews (--oggetti): breakables, items and shinies even before they are approved.</summary>
    public bool PendingLoot { get; init; }

    /// <summary>Playtests (--da m:ss): the run starts this far into the level, skipping the special events already past.</summary>
    public int StartTicks { get; init; }

    public override bool HandlesEscape => true;

    /// <summary>While playing the pointer hides when the mouse stops; any window (pause, level-up, chest…) keeps it.</summary>
    public override bool ShowsPointer =>
        _phase != Phase.Playing || _levelUp.Active || _chestWindow.IsOpen || _evolution.IsOpen || _plateWindow.IsOpen || _shop.IsOpen;

    public bool IsPauseOpen => _phase == Phase.Paused;

    /// <summary>Previews: the pause, e.g. with its options open.</summary>
    public PauseMenu Pause => _pause;

    /// <summary>The level's own music (phase 8.3): its track in audio.json, else the dark one in caves and at night.</summary>
    private string LevelMusic
    {
        get
        {
            var map = Services.Music.Map;
            if (map.Levels.TryGetValue(LevelId, out var track))
                return track;
            return _run.Spawner.Timeline.Darkness >= 0.4f || _run.IsNight ? map.LevelDark : map.Level;
        }
    }

    /// <summary>The music for what is happening: the boss' theme once a boss, legendaries or a trainer are on the field.</summary>
    private void FollowMusic()
    {
        var music = Services.Music;
        if (_run.Boss is not null || _run.Challenge is not null && _run.Events.Any(e => e.Type == RunEventType.TrainerChallenged))
            music.Play(music.Map.Boss);
        else if (music.Wanted != music.Map.Boss)
            music.Play(LevelMusic);
    }

    public override void Enter() => Restart();

    public override IReadOnlyList<string> DebugLines =>
    [
        $"tempo {_run.Elapsed:mm\\:ss}   nemici {_run.Enemies.AliveCount} (slot {_run.Enemies.HighWater})   proiettili {_run.MoveSystem.Projectiles.ActiveCount}   particelle {_renderer.ParticleCount}   chunk {_renderer.Floor.CachedChunks}" +
            $"   {LevelId} {Difficulty} {_run.Weather}{(_run.IsNight ? " notte" : "")}",
        $"PS {_run.Player.Hp:0}/{_run.Player.MaxHp:0}   pos {_run.Player.Position.X:0},{_run.Player.Position.Y:0}   vel {_run.Player.MoveSpeed:0} px/s" +
            $"{(_run.Player.Invincible ? "   [INVINCIBILE]" : "")}{(_run.Spawner.Enabled ? "" : "   [SPAWN OFF]")}" +
            (_run.Spawner.Timeline.WaveAt(_run.Ticks) is { } wave ? $"   ondata {wave.FromTick / (RunClock.TicksPerSecond * 60)}' min {wave.MinAlive} max {wave.MaxAlive}" : ""),
        $"Lv {_run.Player.Level}   Esp {_run.Player.Xp:0}/{_run.Player.XpToNext}   gemme {_run.Pickups.Gems.ActiveCount}   raggio {_run.Player.PickupRadius:0}   " +
            string.Join(" ", _run.Player.Moves.Select(m => $"{m.Definition.NameIt} {m.Level}").Concat(_run.Player.Items.Select(i => $"{i.Definition.NameIt} {i.Level}"))),
        $"F1 invincibile  F2 +1 livello  F4 +100  F9 +500  F5 dominante (Maiusc: forziere)  F6 +1 min (Maiusc: boss)  F7 KO  F8 spawn  F10 fine  F11 velocita x{Speed}  F12 hitbox  F3 overlay",
    ];

    public override void Update()
    {
        var input = Services.Input;
        _motion.Invalidate();
        switch (_phase)
        {
            case Phase.Paused:
                switch (_pause.Update(input))
                {
                    case PauseChoice.Resume:
                        _pause.Close();
                        Services.Music.Duck = 1f;
                        _phase = Phase.Playing;
                        break;
                    case PauseChoice.Restart:
                        RecordRun(abandoned: true);
                        Restart();
                        break;
                    case PauseChoice.Quit:
                        RecordRun(abandoned: true);
                        Leave();
                        break;
                }

                return;

            case Phase.Ending:
                _run.Tick(default);
                _renderer.Update(_run);
                if (_banner.Update(input, autoContinue: Autopilot))
                {
                    _banner.Stop();
                    _results.Open(_run, _report);
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
                        Leave();
                        break;
                }

                return;
        }

        // The player's evolution (phase 4.2): the game freezes until it is over, and Esc stops it instead of pausing.
        if (_evolution.IsOpen)
        {
            _evolution.Update(Autopilot ? null : input);
            _renderer.Update(_run, paused: true);
            _hud.Update(_run);
            return;
        }

        // The special move (phase 4.4): the game freezes while it plays; the run takes the damage at its impact.
        if (_special.IsOpen)
        {
            if (_special.Update())
            {
                _renderer.Shake.Add(0.45f);
                Services.Sounds.Play("speciale-impatto");
            }
            _renderer.Update(_run, paused: true);
            _hud.Update(_run);
            return;
        }

        // Kecleon's shop (phase 9.2) and a chest's Plate (phase 9.1): the game freezes; Esc leaves them instead of pausing.
        if (_shop.IsOpen)
        {
            _shop.Update(_run, Autopilot ? null : input);
            _renderer.Update(_run, paused: true);
            _hud.Update(_run);
            return;
        }

        if (_plateWindow.IsOpen)
        {
            _plateWindow.Update(_run, Autopilot ? null : input);
            _renderer.Update(_run, paused: true);
            _hud.Update(_run);
            return;
        }

        if (_run.ShopOpen)
        {
            _shop.Open(_run);
            return;
        }

        if (_run.PlateChoices is { } plates && !_chestWindow.IsOpen)
        {
            _plateWindow.Open(plates);
            return;
        }

        HandleCheats();
        if (input.Pressed(Keys.Escape) || input.Pressed(Buttons.Start))
        {
            OpenPause();
            return;
        }

        // The boss' entrance: the game freezes while it lands and its name is shown.
        if (_bossIntro.IsPlaying)
        {
            _bossIntro.Update();
            _renderer.Update(_run, paused: true);
            _hud.Update(_run);
            return;
        }

        // A challenge (phase 5.3): the game freezes while the trainer steps in, and while each Pokémon comes out of its Ball.
        if (_trainerIntro.IsPlaying || _sendOutFreeze > 0)
        {
            if (_trainerIntro.IsPlaying)
                _trainerIntro.Update();
            else
                _sendOutFreeze--;
            _renderer.Update(_run, paused: true);
            _hud.Update(_run);
            return;
        }

        // A chest is being opened: the game freezes until its rewards have been shown.
        if (_chestWindow.IsOpen)
        {
            _chestWindow.Update(Autopilot ? null : input);
            _renderer.Update(_run, paused: true);
            _hud.Update(_run);
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

        // Every level-up chosen and no chest open: an evolution that is ready starts now.
        if (CharacterEvolution.Ready(_run) is { Count: > 0 } evolutions)
        {
            _evolution.Open(_run, evolutions);
            return;
        }

        for (var step = 0; step < Speed; step++)
        {
            var move = Autopilot ? AutoPilot.Input(_run) : input.Move.ToNumerics();
            // The active ability (phase 4.3): Space or X; the autopilot uses it as soon as it is ready.
            var useAbility = Autopilot
                ? Core.Run.Abilities.AbilitySystem.CanUse(_run)
                : step == 0 && (input.Pressed(Keys.Space) || input.Pressed(Buttons.X));
            // The special move (phase 4.4): E or Y with a full gauge; the autopilot unleashes it as soon as it is full.
            var useSpecial = Autopilot ? _run.Special.IsFull : step == 0 && (input.Pressed(Keys.E) || input.Pressed(Buttons.Y));
            // The trainer's ability (phase 5.2): Q or a shoulder button; the autopilot uses it when it would help (AutoPilot.UseTrainer).
            var useTrainer = Autopilot
                ? AutoPilotTrainer && AutoPilot.UseTrainer(_run)
                : step == 0 && (input.Pressed(Keys.Q) || input.Pressed(Buttons.LeftShoulder) || input.Pressed(Buttons.RightShoulder));
            useTrainer |= _pressTrainer;
            _pressTrainer = false;
            _motion.Capture(_run);
            _run.Tick(new PlayerInput(move, useAbility, useSpecial, useTrainer));
            _explored.Visit(_run.Player.Position);
            if (_run.Ticks % 30 == 0)
                NoticeMissions();
            _renderer.Update(_run);
            _hud.Update(_run);
            if (_run.Events.Any(e => e.Type is RunEventType.BossAppeared or RunEventType.TrainerChallenged or RunEventType.NightChanged))
                FollowMusic();

            if (_run.SpecialPending)
            {
                _special.Open(_run);
                Services.Sounds.Play("speciale");
                break;
            }

            if (_run.IsOver)
            {
                BeginEnding();
                break;
            }

            if (_run.Boss is { } fight && _run.Events.Any(e => e.Type == RunEventType.BossAppeared))
            {
                _bossIntro.Start(Services.Species(fight.Definition.SpeciesId)?.NameIt ?? "", fight.Definition.Spec.TitleIt);
                break;
            }

            if (_run.Challenge is { } challenge && _run.Events.Any(e => e.Type == RunEventType.TrainerChallenged))
            {
                _trainerIntro.Start(challenge.Trainer);
                break;
            }

            if (_run.Events.Any(e => e.Type == RunEventType.TrainerSentOut))
            {
                _sendOutFreeze = RunRenderer.SendOutTicks;
                break;
            }

            if (_run.LastChest is { } chest && _run.Events.Any(e => e.Type == RunEventType.ChestOpened))
            {
                _chestWindow.Open(chest);
                break;
            }

            if (_levelUp.Active)
                break;
        }
    }

    public override void Draw(SpriteBatch batch)
    {
        // Between two ticks, things are drawn part of the way along their last step (phase 8.6).
        _motion.Apply(_run, Services.FrameBlend);
        try
        {
            DrawScene(batch);
        }
        finally
        {
            _motion.Restore(_run);
        }
    }

    private void DrawScene(SpriteBatch batch)
    {
        _renderer.Draw(batch, _run);
        if (_phase is Phase.Playing or Phase.Paused)
            _hud.Draw(batch, _run);

        _bossIntro.Draw(batch);
        _trainerIntro.Draw(batch);

        _levelUpWindow.Draw(batch);
        _chestWindow.Draw(batch);
        _plateWindow.Draw(batch);
        _shop.Draw(batch, _run);
        _evolution.Draw(batch);
        _special.Draw(batch);
        _pause.Draw(batch, _run, _explored);
        _banner.Draw(batch);
        _results.Draw(batch);
    }

    /// <summary>Previews and the Esc key.</summary>
    public void OpenPause()
    {
        if (_phase != Phase.Playing || _levelUp.Active)
            return;

        _pause.Open();
        Services.Music.Duck = 0.4f;
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
        var legendaries = _run.Stats.LegendariesDefeated.Select(id => Services.Species(id)?.NameIt ?? "").ToList();
        var subtitle = _run.Outcome != RunOutcome.Victory
            ? $"{_run.Player.Species.NameIt} non può più lottare!"
            : _run.Challenge is { Won: true } challenge
                ? $"Hai sconfitto {challenge.Trainer.NameIt}! Ora può essere il tuo allenatore."
            : legendaries.Count > 0
                ? $"Hai sconfitto {UiFormat.List(legendaries)}!"
            : _run.Boss is { DefeatedTick: not null } fight && Services.Species(fight.Definition.SpeciesId) is { } boss
                ? $"Hai sconfitto {boss.NameIt}!"
                : "Hai resistito fino alla fine!";
        _banner.Start(_run.Outcome, subtitle, bossBeaten: _run.Boss?.DefeatedTick is not null);
        _phase = Phase.Ending;
        var music = Services.Music;
        music.Play(_run.Outcome == RunOutcome.Victory ? music.Map.Victory : music.Map.Defeat, restart: true);
        RecordRun(abandoned: false);
    }

    /// <summary>
    /// The end of the run goes into the save (Block 6): coins, Pokédex, records, missions and their rewards. A run left
    /// from the pause counts too, as in Vampire Survivors, unless it had barely begun.
    /// </summary>
    private void RecordRun(bool abandoned)
    {
        if (Meta is null || _recorded || abandoned && _run.Ticks < MinRecordedTicks)
            return;

        _recorded = true;
        _report = Meta.RecordRun(Core.Meta.RunRecord.From(_run, LevelId));
    }

    /// <summary>Missions the run has just completed are announced in the HUD; their rewards come at the end.</summary>
    private void NoticeMissions()
    {
        if (Meta is null)
            return;

        foreach (var mission in Meta.ReachedDuring(Core.Meta.RunRecord.From(_run, LevelId)))
        {
            if (_noticed.Add(mission.Id))
                _hud.ShowMission(mission);
        }
    }

    /// <summary>"Esci": back to the menu, or out of the game when there is none.</summary>
    private void Leave()
    {
        if (BackToMenu is { } menu)
            Services.Switch(menu());
        else
            Services.Exit();
    }

    private void Restart()
    {
        var pokedex = Services.Pokedex.Value.Species;
        var view = new System.Numerics.Vector2(Services.Screen.Width, Services.Screen.Height);

        // Read again at every restart, so waves and enemies can be retuned while the game is open.
        // Block 7: the level as the difficulty makes it, with worlds.json's pace and patterns for what its file leaves out.
        var worlds = Services.Worlds.Value;
        var waves = WaveTimeline.Load(Services.ContentRoot, LevelId, pokedex, Services.Moves.Value, worlds.Defaults, worlds.Difficulty(Difficulty));
        var enemies = EnemyLibrary.Load(Services.ContentRoot, pokedex, Services.Moves.Value);
        var loot = Core.Run.Pickups.LootRules.Load(Services.ContentRoot);
        var plates = Core.Run.Plates.PlateLibrary.Load(Services.ContentRoot);
        var merchant = Core.Run.Merchant.MerchantRules.Load(Services.ContentRoot);
        var platesFound = Meta?.UnlockedPlates() ?? [];
        _renderer.UseTileset(Tileset.ById(waves.Map.Spec.Tileset));
        _run = new RunState(pokedex, Services.TypeChart.Value, _character.First.Id, waves, view, seed: Environment.TickCount)
        {
            Difficulty = Difficulty,
            SkipFinds = Meta?.FoundFinds() ?? new HashSet<string>(),
            // Arceus' Plates and Kecleon (Block 9).
            PlateRules = plates.File.Enabled || PlateId is not null ? plates : null,
            PlatePool = platesFound,
            SkipPlates = platesFound.Select(p => p.Id).ToHashSet(),
            MerchantRules = merchant.File.Enabled ? merchant : null,
            TimeLimitTicks = waves.DurationTicks,
            MovePool = Services.Moves.Value.Enabled,
            Evolutions = PendingEvolutions
                ? Services.Moves.Value.All.Where(m => m.IsEvolution).Select(m => m with { Enabled = true }).ToList()
                : Services.Moves.Value.Evolutions,
            ItemPool = Services.Items.Value.Enabled,
            LevelUpRules = Services.LevelUpRules.Value,
            StatusRules = Services.Statuses.Value,
            EnemyRules = PendingEnemies ? enemies.WithDisabled() : enemies,
            LootRules = loot.File.Enabled || PendingLoot ? loot : null,
            SpecialRules = Services.Specials.Value,
        };
        _run.UseCharacter(_character, Loadout ?? Meta?.Loadout(_character));
        _run.UseTrainer(Meta?.Upgraded(_trainer) ?? _trainer);
        if (plates.Find(PlateId) is { } plate)
            _run.UsePlate(plate);
        if (ChallengeId is { } challengeId)
        {
            var challenged = Services.Trainers.Value.Find(challengeId)
                ?? throw new ArgumentException($"Unknown trainer '{challengeId}' to challenge.");
            _run.ChallengeTrainer(challenged, Services.Trainers.Value.Challenge!);
            foreach (var member in challenged.Team)
                Services.Sprites.Get(member.SpeciesId);
        }
        else if (Difficulty == Core.Meta.Difficulty.Ultra)
        {
            // At Ultra the level's legendaries come instead of its boss (phase 7.3), those whose requirements are met.
            var legendaries = Meta?.Legendaries(waves) ?? waves.Legendaries.Where(l => l.Requires.Count == 0).ToList();
            _run.UseLegendaries(legendaries.Select(l => l.Boss));
        }

        if (StartTicks > 0)
        {
            _run.AdvanceTime(StartTicks);
            _run.Spawner.SkipEventsBefore(StartTicks);
        }

        _setup?.Invoke(_run);

        _renderer.Reset(_run);
        _levelUp = new LevelUpFlow(_run, _levelUpWindow);
        _pause.Close();
        _banner.Stop();
        _results.Close();
        _chestWindow.Close();
        _plateWindow.Close();
        _shop.Close();
        _evolution.Close();
        _special.Close();
        _bossIntro.Stop();
        _trainerIntro.Stop();
        _sendOutFreeze = 0;
        _explored.Clear();
        _recorded = false;
        _report = null;
        _noticed.Clear();
        _hud.ClearMissions();
        _hud.ShowLevel(waves.NameIt, Difficulty == Core.Meta.Difficulty.Normal ? null : worlds.Difficulty(Difficulty).NameIt);
        _pause.QuitLabel = BackToMenu is null ? "Esci dal gioco" : "Torna al menu";
        _results.QuitLabel = BackToMenu is null ? "ESCI" : "MENU";
        _phase = Phase.Playing;

        // The level's music from its start, and the boss' theme made ready for when it comes (phase 8.3).
        Services.Music.Duck = 1f;
        Services.Music.Play(LevelMusic, restart: true);
        Services.Music.Prepare(Services.Music.Map.Boss);

        // Load sprites up front so the first spawn of a species does not hitch.
        foreach (var stage in _character.Stages)
            Services.Sprites.Get(stage.Species.Id);
        foreach (var speciesId in waves.SpeciesIds)
            Services.Sprites.Get(speciesId);

        if (Autopilot)
            _run.Player.Invincible = true;
        if (_initialSpawn > 0)
        {
            _run.Spawner.StressMinAlive = _initialSpawn;
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
        if (input.Pressed(Keys.F5))
            SpawnDominant(input.IsDown(Keys.LeftShift));
        if (input.Pressed(Keys.F6) && input.IsDown(Keys.LeftShift))
            _run.SummonBoss();
        else if (input.Pressed(Keys.F6))
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

    /// <summary>
    /// Cheat F5: a Dominante of the current wave walks in, with a Poké, Mega or Ultra Ball by the minute.
    /// Shift+F5 opens an Ultra Ball at once.
    /// </summary>
    private void SpawnDominant(bool openChest)
    {
        if (openChest)
        {
            _run.OpenChest(ChestTier.Ultra, _run.Player.Position);
            _chestWindow.Open(_run.LastChest!);
            return;
        }

        var minute = _run.Ticks / (RunClock.TicksPerSecond * 60);
        var tier = minute < 5 ? ChestTier.Poke : minute < 10 ? ChestTier.Great : ChestTier.Ultra;
        var index = _run.Spawner.SpawnOne(_run);
        if (index < 0)
            return;

        ref var e = ref _run.Enemies[index];
        e.MaxHp *= 40f;
        e.Hp = e.MaxHp;
        Spawner.MakeDominant(_run, index, tier);
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
