using System.Numerics;
using PokeRogue.Core.Combat;
using PokeRogue.Core.Data;
using PokeRogue.Core.Geometry;
using PokeRogue.Core.Run.Abilities;
using PokeRogue.Core.Run.Characters;
using PokeRogue.Core.Run.Enemies;
using PokeRogue.Core.Run.Items;
using PokeRogue.Core.Run.Moves;
using PokeRogue.Core.Run.Pickups;
using PokeRogue.Core.Run.Progression;
using PokeRogue.Core.Run.Specials;
using PokeRogue.Core.Run.Statuses;
using PokeRogue.Core.Run.Trainers;
using PokeRogue.Core.Run.Waves;

namespace PokeRogue.Core.Run;

/// <summary>The whole simulation of one run, advanced at a fixed 60 Hz by <see cref="Tick"/>.</summary>
public sealed class RunState
{
    /// <summary>How hard overlapping enemies push each other apart (fraction of overlap per tick).</summary>
    private const float SeparationStrength = 0.35f;

    /// <summary>Options shown per level-up; the shop's Fortuna adds a fourth (<see cref="ChoiceCount"/>).</summary>
    public const int LevelUpChoiceCount = 3;

    /// <summary>Rilancio, Salta and Bandisci charges per run; the shop's upgrades add more (they need medals).</summary>
    public const int StartingLevelUpCharges = 2;

    private readonly List<RunEvent> _events = [];
    private readonly List<RunEvent> _resumeEvents = [];
    private int _chestSerial;

    public RunState(IReadOnlyList<SpeciesData> pokedex, TypeChart typeChart, int playerSpeciesId, WaveTimeline waves, Vector2 viewSize, int seed = 1)
    {
        Pokedex = pokedex;
        TypeChart = typeChart;
        Random = new Random(seed);
        Player = new Player(pokedex[playerSpeciesId - 1]);
        Enemies = new EnemyPool(4096);
        Grid = new SpatialGrid(32f, 64);
        Spawner = new Spawner(waves, viewSize);
        ViewSize = viewSize;
    }

    /// <summary>Tests and previews: the same species forever (<see cref="WaveTimeline.Constant"/>).</summary>
    public RunState(IReadOnlyList<SpeciesData> pokedex, TypeChart typeChart, int playerSpeciesId, IReadOnlyList<SpawnEntry> spawnTable, Vector2 viewSize, int seed = 1)
        : this(pokedex, typeChart, playerSpeciesId, WaveTimeline.Constant(spawnTable), viewSize, seed)
    {
    }

    public IReadOnlyList<SpeciesData> Pokedex { get; }

    public TypeChart TypeChart { get; }

    /// <summary>Moves that level-ups may offer (the enabled moves of <see cref="MoveLibrary"/>).</summary>
    public IReadOnlyList<MoveDefinition> MovePool { get; init; } = [];

    /// <summary>Evolutions a Dominante's chest gives once their move is maxed and their item held (phases 2.6 and 3.3).</summary>
    public IReadOnlyList<MoveDefinition> Evolutions { get; init; } = [];

    /// <summary>
    /// The gold EVOLUZIONE level-up card of phase 2.6. Since phase 3.3 evolutions come only from chests, as in Vampire
    /// Survivors; previews of the card switch it back on.
    /// </summary>
    public bool EvolutionsAtLevelUp { get; init; }

    /// <summary>Held items that level-ups may offer (the enabled items of <see cref="ItemLibrary"/>).</summary>
    public IReadOnlyList<ItemDefinition> ItemPool { get; init; } = [];

    /// <summary>How level-up cards are drawn (content/data/levelup.json); null draws every card alike.</summary>
    public LevelUpRules? LevelUpRules { get; init; }

    /// <summary>Status condition rules (content/data/statuses.json); null = no statuses at all. Previews may swap them.</summary>
    public StatusLibrary? StatusRules { get; set; }

    /// <summary>How enemy species fight (content/data/enemies.json); null = everyone just chases. Previews may swap it.</summary>
    public EnemyLibrary? EnemyRules { get; set; }

    /// <summary>Shots fired by ranged enemies.</summary>
    public EnemyShotPool EnemyShots { get; } = new(512);

    /// <summary>Rocks a boss has called down, waiting to land.</summary>
    public List<Hazard> Hazards { get; } = [];

    /// <summary>The boss fight, once the time has run out on a level with a boss (phase 3.4).</summary>
    public BossFight? Boss { get; private set; }

    public MoveSystem MoveSystem { get; } = new();

    public PickupSystem Pickups { get; } = new();

    /// <summary>Breakables, drops and shinies (content/data/loot.json, phase 3.5); null = none of them.</summary>
    public LootRules? LootRules { get; set; }

    /// <summary>Tall grass and boulders on the map, and the items lying on the ground.</summary>
    public LootSystem Loot { get; } = new();

    /// <summary>Level-ups earned but not chosen yet.</summary>
    public int PendingLevelUps { get; private set; }

    /// <summary>The options currently offered; while not null the run is paused.</summary>
    public IReadOnlyList<LevelUpOption>? LevelUpChoices { get; private set; }

    /// <summary>Rilancio: draws new options in place of the ones on offer.</summary>
    public int RerollsLeft { get; set; } = StartingLevelUpCharges;

    /// <summary>Salta: gives up a level-up's choice.</summary>
    public int SkipsLeft { get; set; } = StartingLevelUpCharges;

    /// <summary>Bandisci: removes an offered move or item from the run for good.</summary>
    public int BanishesLeft { get; set; } = StartingLevelUpCharges;

    /// <summary>Ids of the moves and items banished this run: level-ups never offer them again.</summary>
    public HashSet<string> Banished { get; } = new(StringComparer.Ordinal);

    public bool IsPaused => LevelUpChoices is not null || SpecialPending || PlateChoices is not null || ShopOpen;

    // ---- Arceus' Plates (phase 9.1) ----

    /// <summary>The Plates' rules (content/data/plates.json); null = no Plates on the map nor from chests.</summary>
    public Run.Plates.PlateLibrary? PlateRules { get; init; }

    /// <summary>The Plates found so far: a chest may offer them during the run.</summary>
    public IReadOnlyList<Run.Plates.PlateDefinition> PlatePool { get; init; } = [];

    /// <summary>Plates already found: theirs are not laid on the map again.</summary>
    public IReadOnlySet<string> SkipPlates { get; init; } = new HashSet<string>();

    /// <summary>The Plates held in this run, in the order they came.</summary>
    public List<Run.Plates.PlateDefinition> Plates { get; } = [];

    public Run.Plates.PlateState PlateState { get; } = new();

    /// <summary>The Plates a chest offers; while not null the run is paused.</summary>
    public IReadOnlyList<Run.Plates.PlateDefinition>? PlateChoices { get; private set; }

    /// <summary>Holds <paramref name="plate"/> from now on, unless it is already held or the run has no room left. Call before the run starts for the chosen one.</summary>
    public bool UsePlate(Run.Plates.PlateDefinition plate)
    {
        var max = PlateRules?.File.MaxPerRun ?? 2;
        if (Plates.Any(p => p.Id == plate.Id) || Plates.Count >= max)
            return false;

        Plates.Add(plate);
        Run.Plates.PlateSystem.OnGained(this, plate);
        return true;
    }

    /// <summary>Takes the offered Plate at <paramref name="index"/>; any other index declines the offer.</summary>
    public void ChoosePlate(int index)
    {
        if (PlateChoices is { } choices && (uint)index < (uint)choices.Count)
        {
            UsePlate(choices[index]);
            AddEventOnResume(new RunEvent(RunEventType.PlateEffect, Player.Position, -2f, MoveId: choices[index].Id));
        }

        PlateChoices = null;
    }

    /// <summary>Previews and cheats: offers these Plates now, as a chest would.</summary>
    public void OfferPlates(IReadOnlyList<Run.Plates.PlateDefinition> options) => PlateChoices = options.Count > 0 ? options : null;

    /// <summary>The first chest opened from the Plates' time on offers some of the Plates found, not held yet.</summary>
    private void OfferPlateFromChest()
    {
        if (PlateState.Offered || PlateRules is not { } rules || Ticks < rules.OfferFromTicks || Plates.Count >= rules.File.MaxPerRun)
            return;

        PlateState.Offered = true;
        var options = PlatePool.Where(p => Plates.All(h => h.Id != p.Id)).OrderBy(_ => Random.Next()).Take(rules.File.OfferCount).ToList();
        if (options.Count > 0)
            PlateChoices = options;
    }

    // ---- Kecleon (phase 9.2) ----

    /// <summary>Kecleon's rules (content/data/merchant.json); null = it never comes.</summary>
    public Run.Merchant.MerchantRules? MerchantRules { get; init; }

    public Run.Merchant.MerchantState Merchant { get; } = new();

    /// <summary>The player walked into Kecleon: its shop is open and the run waits.</summary>
    public bool ShopOpen { get; private set; }

    /// <summary>Opens Kecleon's shop (the merchant when touched, previews).</summary>
    public void OpenShop() => ShopOpen = Merchant.Present;

    public void CloseShop() => ShopOpen = false;

    /// <summary>Buys the ware at <paramref name="index"/> with the run's coins.</summary>
    public bool BuyWare(int index) => Run.Merchant.MerchantSystem.Buy(this, index);

    /// <summary>Survive this long to win (the Block 1 prototype uses 10 minutes); null for endless.</summary>
    public int? TimeLimitTicks { get; init; }

    public RunOutcome Outcome { get; private set; }

    public bool IsOver => Outcome != RunOutcome.None;

    public RunStats Stats { get; } = new();

    public Random Random { get; }

    public Player Player { get; }

    /// <summary>The character being played (phase 4.1); null in tests and previews that use a bare species.</summary>
    public CharacterDefinition? Character { get; private set; }

    /// <summary>The stage of <see cref="Character"/>'s line the player is now (phase 4.2).</summary>
    public CharacterStage? Stage { get; internal set; }

    /// <summary>Evolution items found (stones, Metalcoperta, …) and not used yet.</summary>
    public HashSet<string> EvolutionItems { get; } = new(StringComparer.Ordinal);

    /// <summary>The level at which the player last stopped an evolution: it is offered again from the next level.</summary>
    public int EvolutionDeclinedAtLevel { get; internal set; }

    /// <summary>Timers and stacks of the player's ability (phase 4.3).</summary>
    public AbilityState Ability { get; } = new();

    /// <summary>The trainer of the run (phase 5.1); null in tests and previews without one.</summary>
    public TrainerDefinition? Trainer { get; private set; }

    /// <summary>The trainer's active ability: its cooldown and the effect running.</summary>
    public TrainerState TrainerState { get; } = new();

    /// <summary>The trainer challenged on this run (phase 5.3): they replace the level's boss when the time runs out.</summary>
    public TrainerChallenge? Challenge { get; private set; }

    /// <summary>Makes this run a challenge of <paramref name="trainer"/>, who needs a team.</summary>
    public void ChallengeTrainer(TrainerDefinition trainer, ChallengeRules rules)
    {
        if (trainer.Team.Count == 0)
            throw new ArgumentException($"{trainer.Id} has no team to challenge.", nameof(trainer));
        Challenge = new TrainerChallenge(trainer, rules);
    }

    /// <summary>Plays with <paramref name="trainer"/>: its passive bonuses are on from now, its key ready.</summary>
    public void UseTrainer(TrainerDefinition trainer)
    {
        Trainer = trainer;
        Player.TrainerBonus = trainer.Passive.Modifiers;
        Player.RecalculateStats();
    }

    /// <summary>The special moves (content/data/specials.json, phase 4.4); null = no special gauge.</summary>
    public SpecialMoves? SpecialRules { get; init; }

    /// <summary>The special gauge.</summary>
    public SpecialState Special { get; } = new();

    /// <summary>The character's special move; null without a character or special rules.</summary>
    public SpecialMove? SpecialMove => Character is { } character && SpecialRules is { } rules && Loadout.SpecialMove ? rules[character.Special] : null;

    /// <summary>The key was pressed with a full gauge: the run waits until <see cref="UnleashSpecial"/>.</summary>
    public bool SpecialPending { get; private set; }

    /// <summary>The screen size around the player, for what the special reaches.</summary>
    public Vector2 ViewSize { get; }

    /// <summary>The special lands (called by the game at the impact of its animation, or straight away when scripted).</summary>
    public void UnleashSpecial()
    {
        if (!SpecialPending || SpecialMove is not { } special)
            return;

        SpecialPending = false;
        SpecialMoves.Unleash(this, special, ViewSize);
    }

    /// <summary>
    /// Plays <paramref name="character"/>, whose first species the run was created with: its modifiers and its main move,
    /// plus what the meta-progression bought for it (Block 6): the secondary move, the special, stat grades and the shop's
    /// upgrades. Without a loadout the run is as before Block 6 (<see cref="RunLoadout.Default"/>).
    /// </summary>
    public void UseCharacter(CharacterDefinition character, RunLoadout? loadout = null)
    {
        if (Player.Species.Id != character.First.Id)
            throw new ArgumentException($"The run was created as {Player.Species.Key}, not as {character.First.Key}.", nameof(character));

        Character = character;
        Stage = character.Stages[0];
        Player.CharacterBonus = character.Modifiers;
        Player.Moves.Add(new MoveInstance(character.MainMove) { AcquiredTick = Ticks });
        UseLoadout(loadout ?? RunLoadout.Default);
        if (Loadout.SecondaryMove)
            Player.Moves.Add(new MoveInstance(character.SecondaryMove) { AcquiredTick = Ticks });
    }

    /// <summary>What the meta-progression brings into the run (Block 6); <see cref="RunLoadout.Default"/> until set.</summary>
    public RunLoadout Loadout { get; private set; } = RunLoadout.Default;

    /// <summary>Move slots of this run: five, six with the shop's upgrade.</summary>
    public int MaxMoves => Loadout.MaxMoves;

    /// <summary>Cards per level-up: three, four with Fortuna.</summary>
    public int ChoiceCount => Loadout.Choices + Run.Plates.PlateSystem.ExtraChoices(this);

    /// <summary>Sets the run's loadout: its bonus on the player and the level-up charges it adds. Call before the run starts.</summary>
    public void UseLoadout(RunLoadout loadout)
    {
        Loadout = loadout;
        Player.MetaBonus = loadout.Bonus;
        Player.RecalculateStats();
        Player.Hp = Player.MaxHp;
        RerollsLeft = StartingLevelUpCharges + loadout.ExtraRerolls;
        SkipsLeft = StartingLevelUpCharges + loadout.ExtraSkips;
        BanishesLeft = StartingLevelUpCharges + loadout.ExtraBanishes;
    }

    public EnemyPool Enemies { get; }

    public SpatialGrid Grid { get; }

    public Spawner Spawner { get; }

    /// <summary>The level's walls and obstacles (phase 7.2).</summary>
    public Worlds.LevelMap Map => Spawner.Timeline.Map;

    /// <summary>The weather now (phase 7.2), set every tick by <see cref="Worlds.WeatherSystem"/>.</summary>
    public Worlds.WeatherKind Weather { get; internal set; }

    /// <summary>Night has fallen on the level: the waves send their night species.</summary>
    public bool IsNight { get; internal set; }

    /// <summary>The difficulty played (phase 7.3), for the records and the missions.</summary>
    public Meta.Difficulty Difficulty { get; init; }

    /// <summary>Characters whose fossil or egg the player already has: theirs are not laid on the map (phase 7.4).</summary>
    public IReadOnlySet<string> SkipFinds { get; init; } = new HashSet<string>();

    private readonly Queue<BossDefinition> _legendaries = new();

    /// <summary>
    /// At Ultra the level's legendaries come instead of its boss when the time runs out (phase 7.3), one after another;
    /// beating each unlocks it. Call before the run starts.
    /// </summary>
    public void UseLegendaries(IEnumerable<BossDefinition> legendaries)
    {
        _legendaries.Clear();
        foreach (var legendary in legendaries)
            _legendaries.Enqueue(legendary);
        LegendaryEncounter = _legendaries.Count > 0;
    }

    /// <summary>This run ends with the level's legendaries rather than its boss.</summary>
    public bool LegendaryEncounter { get; private set; }

    /// <summary>Legendaries still to come after the one fighting.</summary>
    public int LegendariesLeft => _legendaries.Count;

    private BossFight SpawnLegendary(BossDefinition legendary)
    {
        var fight = BossAi.Spawn(this, legendary, Spawner.Timeline.HpMultiplier(Ticks));
        fight.IsLegendary = true;
        return fight;
    }

    public int Ticks { get; private set; }

    public TimeSpan Elapsed => TimeSpan.FromSeconds(Ticks / (double)RunClock.TicksPerSecond);

    public IReadOnlyList<RunEvent> Events => _events;

    public void Tick(PlayerInput input)
    {
        _events.Clear();
        if (IsOver)
        {
            // Aftermath: let fainting enemies finish their animation; nothing else moves.
            UpdateFainting();
            return;
        }

        if (IsPaused)
            return;

        _events.AddRange(_resumeEvents);
        _resumeEvents.Clear();
        Ticks++;

        Player.Move(input.Move);
        if (Map.IsConstrained)
        {
            var position = Player.Position;
            Map.Resolve(ref position, Player.Radius * 0.6f);
            Player.Position = position;
        }

        Player.Tick();
        Worlds.WeatherSystem.Tick(this);
        AbilitySystem.Tick(this, input.UseAbility);
        TrainerSystem.Tick(this, input.UseTrainer);

        if (Special.LockTicks > 0)
            Special.LockTicks--;
        if (input.UseSpecial && Special.IsFull && SpecialMove is { } special)
        {
            SpecialPending = true;
            _events.Add(new RunEvent(RunEventType.SpecialStarted, Player.Position, (int)special.Type, MoveId: special.Id));
            return;
        }

        Grid.Rebuild(Enemies, Player.Position);
        StatusSystem.Tick(this);
        UpdateEnemies();
        EnemyShots.Tick(this);
        BossAi.TickHazards(this);
        MoveSystem.Tick(this);
        Run.Plates.PlateSystem.Tick(this);
        Pickups.Tick(this);
        Loot.Tick(this);
        Run.Merchant.MerchantSystem.Tick(this);
        Spawner.Tick(this);
        CheckOutcome();
    }

    public void AddEvent(in RunEvent e) => _events.Add(e);

    /// <summary>Spawns an enemy with its species' fighting profile from <see cref="EnemyRules"/>, if it has one. -1 when the pool is full.</summary>
    public int SpawnEnemy(int speciesId, Vector2 position, float hpMultiplier = 1f, float xpMultiplier = 1f)
    {
        var index = Enemies.Spawn(Pokedex[speciesId - 1], position, hpMultiplier, xpMultiplier);
        if (index >= 0)
            Stats.RecordSeen(speciesId);
        if (index >= 0 && EnemyRules?.For(speciesId) is { } profile)
            EnemyAi.Apply(this, ref Enemies[index], profile);
        return index;
    }

    /// <summary>For changes made while the game is frozen (level-up choices): the event comes out with the first tick after it resumes.</summary>
    public void AddEventOnResume(in RunEvent e) => _resumeEvents.Add(e);

    /// <summary>Gives coins, more with Monetamuleto.</summary>
    public void AddCoins(int amount)
    {
        var gained = (int)MathF.Round(amount * Player.CoinMultiplier * TrainerSystem.GainMultiplier(this) * Spawner.Timeline.Difficulty.Coins);
        Player.Coins += gained;
        Stats.RecordCoins(gained);
    }

    /// <summary>Previews and cheats: ends the run immediately.</summary>
    public void End(RunOutcome outcome)
    {
        if (IsOver || outcome == RunOutcome.None)
            return;

        Outcome = outcome;
        LevelUpChoices = null;
        PendingLevelUps = 0;
        EnemyShots.Clear();
        Hazards.Clear();
        if (outcome == RunOutcome.Defeat)
            Player.Hp = 0f;
        if (outcome == RunOutcome.Victory)
        {
            // The horde scatters: every enemy faints at once, without drops or KO credit.
            for (var i = 0; i < Enemies.HighWater; i++)
            {
                if (Enemies[i].IsAlive && Enemies[i].Rank != EnemyRank.Prop)
                    Enemies.Defeat(i);
            }
        }

        _events.Add(new RunEvent(RunEventType.RunEnded, Player.Position, (int)outcome));
    }

    private void CheckOutcome()
    {
        if (Player.IsDown && Player.TryRevive())
            _events.Add(new RunEvent(RunEventType.PlayerRevived, Player.Position, Player.RevivesLeft));
        else if (Player.IsDown)
            End(RunOutcome.Defeat);
        else if (Challenge is { Started: true } challenge)
            CheckChallenge(challenge);
        else if (Boss?.DefeatedTick is { } defeated && Ticks >= defeated + BossAi.VictoryDelayTicks)
        {
            // The next legendary steps in (phase 7.3), or the level is won.
            if (_legendaries.TryDequeue(out var next))
                Boss = SpawnLegendary(next);
            else
                End(RunOutcome.Victory);
        }
        else if (TimeLimitTicks is { } limit && Ticks >= limit && Boss is null)
        {
            // Time is up: the challenged trainer steps in, or the legendaries of Ultra, or the level's boss arrives, or,
            // without one, the level is won.
            if (Challenge is { } trainer)
                trainer.Begin(this);
            else if (_legendaries.TryDequeue(out var legendary))
                Boss = SpawnLegendary(legendary);
            else if (Spawner.Timeline.Boss is { } boss)
                Boss = BossAi.Spawn(this, boss, Spawner.Timeline.HpMultiplier(Ticks));
            else
                End(RunOutcome.Victory);
        }
    }

    /// <summary>
    /// The challenge (phase 5.3): the trainer's Pokémon come out one at a time, each when the one before has fainted and a
    /// moment has passed; when the last one has fallen the trainer is beaten and the run won.
    /// </summary>
    private void CheckChallenge(TrainerChallenge challenge)
    {
        if (Boss is { DefeatedTick: { } defeated } && challenge.Fainted < challenge.SentOut)
            challenge.OnFainted(defeated);

        if (Boss is { DefeatedTick: null })
            return;

        if (challenge.HasMore)
        {
            if (Ticks >= challenge.NextTick)
                Boss = challenge.SendOut(this);
        }
        else if (Ticks >= challenge.NextTick - challenge.NextDelayTicks + BossAi.VictoryDelayTicks)
        {
            challenge.Won = true;
            _events.Add(new RunEvent(RunEventType.TrainerDefeated, Player.Position, 0f, MoveId: challenge.Trainer.Id));
            End(RunOutcome.Victory);
        }
    }

    /// <summary>Previews and cheats: the boss (or the challenged trainer) arrives now, whatever the time.</summary>
    public void SummonBoss()
    {
        if (Challenge is { Started: false } challenge)
            challenge.Begin(this);
        else if (Boss is null && Challenge is null && _legendaries.TryDequeue(out var legendary))
            Boss = SpawnLegendary(legendary);
        else if (Boss is null && Challenge is null && Spawner.Timeline.Boss is { } boss)
            Boss = BossAi.Spawn(this, boss, Spawner.Timeline.HpMultiplier(Ticks));
    }

    /// <summary>Called by the pickup system when a gem reaches the player.</summary>
    public void CollectExperience(int value, Vector2 position)
    {
        _events.Add(new RunEvent(RunEventType.GemCollected, position, value));
        var levels = Player.AddXp(value * TrainerSystem.GainMultiplier(this));
        if (levels == 0)
            return;

        PendingLevelUps += levels;
        _events.Add(new RunEvent(RunEventType.LevelUp, Player.Position, Player.Level));
        AbilitySystem.OnLevelUp(this, levels);
        LevelUpChoices ??= LevelUp.Generate(this, ChoiceCount);
    }

    /// <summary>Applies one of <see cref="LevelUpChoices"/>; offers the next set if more level-ups are pending.</summary>
    public void ChooseLevelUp(int index)
    {
        if (LevelUpChoices is not { } choices)
            return;

        LevelUp.Apply(this, choices[Math.Clamp(index, 0, choices.Count - 1)]);
        PendingLevelUps--;
        LevelUpChoices = PendingLevelUps > 0 ? LevelUp.Generate(this, ChoiceCount) : null;
    }

    /// <summary>Rilancio: replaces the options on offer with a fresh draw. False when there is nothing to reroll or no charge left.</summary>
    public bool RerollLevelUp()
    {
        if (LevelUpChoices is null || RerollsLeft <= 0)
            return false;

        RerollsLeft--;
        LevelUpChoices = LevelUp.Generate(this, ChoiceCount);
        return true;
    }

    /// <summary>Salta: gives up this level-up without taking anything, then offers the next pending one.</summary>
    public bool SkipLevelUp()
    {
        if (LevelUpChoices is null || SkipsLeft <= 0)
            return false;

        SkipsLeft--;
        PendingLevelUps--;
        LevelUpChoices = PendingLevelUps > 0 ? LevelUp.Generate(this, ChoiceCount) : null;
        return true;
    }

    /// <summary>
    /// Bandisci: the move or item at <paramref name="index"/> is never offered again this run. Its card is replaced
    /// by something not already on offer, or dropped when nothing is left. Rewards (heal, coins) cannot be banished.
    /// </summary>
    public bool BanishLevelUp(int index)
    {
        if (LevelUpChoices is not { } choices || BanishesLeft <= 0 || (uint)index >= (uint)choices.Count)
            return false;
        if (choices[index].Kind is LevelUpKind.Heal or LevelUpKind.Coins)
            return false;

        BanishesLeft--;
        Banished.Add(choices[index].Id);

        var remaining = choices.Where((_, i) => i != index).ToList();
        var replacement = LevelUp.Generate(this, int.MaxValue)
            .FirstOrDefault(o => o.Kind is not (LevelUpKind.Heal or LevelUpKind.Coins) && remaining.All(c => c.Id != o.Id));
        if (replacement is not null)
            remaining.Insert(index, replacement);

        LevelUpChoices = remaining.Count > 0 ? remaining : LevelUp.Generate(this, ChoiceCount);
        return true;
    }

    /// <summary>Previews and cheats: offers a specific set of options as if a level had just been reached.</summary>
    public void OfferLevelUp(IReadOnlyList<LevelUpOption> options)
    {
        PendingLevelUps = Math.Max(PendingLevelUps, 1);
        LevelUpChoices = options;
    }

    /// <summary>Debug/cheat: grants exactly enough experience for the next level.</summary>
    public void GrantLevel() => CollectExperience((int)MathF.Ceiling((Player.XpToNext - Player.Xp) / Player.Growth), Player.Position);

    /// <summary>Applies a move hit: type-aware damage, knockback along <paramref name="direction"/>, and an EnemyHit event.</summary>
    public void HitEnemy(int index, MoveInstance move, Vector2 direction)
    {
        ref var e = ref Enemies[index];
        if (!e.IsAlive || e.IsShielded)
            return;

        var definition = move.Definition;
        var defender = new DefenderProfile(e.Type1, e.Type2, e.Defense, e.SpDefense);
        var power = move.Stats.Power * Player.PowerMultiplier(definition.Category) * AbilitySystem.DamageMultiplier(this, move)
            * TrainerSystem.DamageMultiplier(this, move) * Worlds.WeatherSystem.DamageMultiplier(this, definition.Type)
            * Run.Plates.PlateSystem.DamageMultiplier(this, move, e);
        var attacker = Run.Plates.PlateSystem.Attacker(this, AbilitySystem.Attacker(this, Player.Attacker));
        if (definition.CritBonus > 0f)
            attacker = attacker with { CritChance = attacker.CritChance + definition.CritBonus };
        var result = DamageCalculator.Compute(power, definition.Category, definition.Type, attacker, defender, TypeChart, Random);

        // Abisso: now and then the ground swallows the enemy whole; Dominanti and bosses are immune.
        var ohko = definition.OhkoChance > 0f && e.Rank == EnemyRank.Normal && Random.NextSingle() < definition.OhkoChance;
        if (ohko)
            result = result with { Amount = MathF.Max(result.Amount, e.Hp) };

        var dealt = MathF.Min(result.Amount, e.Hp);
        move.DamageDealt += dealt;
        if (e.Rank != EnemyRank.Prop)
            SpecialMoves.Charge(this, dealt, e.MaxHp);

        // Sanguisuga's drain and Conchinella's lifesteal both give back part of the damage (not from grass and rocks).
        var heal = definition.Drain + Player.Lifesteal;
        if (heal > 0f && e.Rank != EnemyRank.Prop)
            Player.HealFromDamage(dealt * heal);

        _events.Add(new RunEvent(RunEventType.EnemyHit, e.Position, result.Amount, e.SpeciesId,
            Effectiveness: result.Effectiveness, Critical: result.Critical, MoveId: definition.Id, Ohko: ohko));
        var target = e.Rank;
        DamageEnemy(index, result.Amount, direction * move.Stats.Knockback);
        AbilitySystem.OnMoveHit(this, move, knockedOut: target != EnemyRank.Prop && !Enemies[index].IsAlive);
        StatusSystem.OnHit(this, index, move, result.Amount);
    }

    /// <summary>
    /// Every hit on the player goes through here (phase 4.3): its ability may stop or soften it, the defence stats, Ferro
    /// and Zinco reduce it, and then the ability may strike back. Returns the damage taken.
    /// </summary>
    public float HurtPlayer(in Hurt hurt)
    {
        if (!Player.CanBeHurt || TrainerSystem.Blocks(this, hurt))
            return 0f;

        var multiplier = AbilitySystem.HurtMultiplier(this, hurt);
        if (multiplier > 0f)
            multiplier *= Run.Plates.PlateSystem.HurtMultiplier(this);
        if (multiplier <= 0f)
            return 0f;

        var taken = Player.TakeContactDamage(hurt.Damage * multiplier, hurt.Special);
        if (taken <= 0f)
            return 0f;

        Stats.RecordDamageTaken(taken);
        _events.Add(new RunEvent(RunEventType.PlayerHurt, Player.Position, taken, hurt.SpeciesId));
        AbilitySystem.OnHurt(this, hurt, taken);
        return taken;
    }

    /// <summary>Damage from a status condition: no flash or knockback, credited to the move that inflicted the status.</summary>
    public void DamageFromStatus(int index, float damage)
    {
        ref var e = ref Enemies[index];
        if (!e.IsAlive || damage <= 0f)
            return;

        if (e.StatusSource >= 0 && e.StatusSource < Player.Moves.Count)
            Player.Moves[e.StatusSource].DamageDealt += MathF.Min(damage, e.Hp);

        _events.Add(new RunEvent(RunEventType.StatusDamage, e.Position, damage, e.SpeciesId, Status: e.Status));
        DamageEnemy(index, damage, flash: false);
    }

    /// <summary>Deals direct damage to an enemy (weapons arrive in phase 1.4; also used by cheats).</summary>
    public void DamageEnemy(int index, float damage, Vector2 knockback = default, bool flash = true)
    {
        ref var e = ref Enemies[index];
        if (!e.IsAlive || e.IsShielded)
            return;

        e.Hp -= damage;
        if (flash)
            e.FlashTicks = Enemy.HitFlashTicks;
        e.Knockback += knockback * e.KnockbackScale;
        if (e.Hp <= 0f && e.Rank == EnemyRank.Prop)
        {
            // Grass is cut and boulders shatter: no KO, no gem, maybe something left behind.
            _events.Add(new RunEvent(RunEventType.PropBroken, e.Position, (int)e.Breakable));
            Loot.OnBroken(this, e);
            Enemies.Release(index);
            return;
        }

        if (e.Hp <= 0f)
        {
            // The Plates look at its status before fainting clears it (phase 9.1).
            Run.Plates.PlateSystem.OnDefeated(this, e);
            Enemies.Defeat(index);
            Stats.RecordKill(e.SpeciesId);
            _events.Add(new RunEvent(RunEventType.EnemyDefeated, e.Position, 0f, e.SpeciesId));
            Pickups.Gems.Spawn(e.Position, e.XpValue);

            // A shiny leaves a big nugget and a held item; now and then a regular Pokémon drops a coin or a Baccarancia.
            if (e.Shiny)
            {
                Stats.RecordShiny();
                Loot.Drop(e.Position + new Vector2(6, 0), LootKind.BigNugget);
                if (LootSystem.ShinyItem(this) is { } item)
                    Loot.Drop(e.Position + new Vector2(-6, 2), LootKind.HeldItem, item);
            }
            else if (e.Rank == EnemyRank.Normal && LootRules is { } loot)
            {
                var roll = Random.NextSingle() / (AbilitySystem.DropMultiplier(this) * Run.Plates.PlateSystem.DropMultiplier(this));
                if (roll < loot.File.EnemyFoodChance)
                    Loot.Drop(e.Position + new Vector2(-4, 3), LootKind.OranBerry);
                else if (roll < loot.File.EnemyFoodChance + loot.File.EnemyCoinChance)
                    Loot.Drop(e.Position + new Vector2(5, 2), LootKind.Coin);
            }

            if (e.Rank == EnemyRank.Boss && Boss is { DefeatedTick: null } fight)
            {
                fight.DefeatedTick = Ticks;
                if (fight.IsLegendary)
                    Stats.RecordLegendary(e.SpeciesId);
                Hazards.Clear();
                _events.Add(new RunEvent(RunEventType.BossDefeated, e.Position, 0f, e.SpeciesId));
            }

            if (e.Rank == EnemyRank.Dominant)
            {
                Stats.RecordDominant();
                Pickups.Chests.Add(new Chest(++_chestSerial, e.Position, e.Chest));
                _events.Add(new RunEvent(RunEventType.ChestDropped, e.Position, (int)e.Chest, e.SpeciesId));
            }
        }
    }

    /// <summary>The last chest opened: its rewards are already applied; the game shows them (phase 3.3).</summary>
    public ChestOpening? LastChest { get; private set; }

    /// <summary>
    /// Opens a chest: 1, 3 or 5 rewards are applied at once (a ready evolution first, then upgrades of what the player
    /// has), and a ChestOpened event tells the game to show them. Also used by previews and cheats.
    /// </summary>
    public ChestOpening OpenChest(ChestTier tier, Vector2 position)
    {
        var rewards = ChestRewards.Open(this, ChestRewards.CountFor(tier));
        LastChest = new ChestOpening(tier, rewards, Ticks);
        Stats.RecordChest();
        OfferPlateFromChest();
        _events.Add(new RunEvent(RunEventType.ChestOpened, position, (int)tier));
        return LastChest;
    }

    public void AdvanceTime(int ticks) => Ticks += ticks;

    private void UpdateFainting()
    {
        var items = Enemies.Items;
        for (var i = 0; i < items.Length; i++)
        {
            if (items[i].State == EnemyState.Fainting && --items[i].FaintTicksLeft <= 0)
                Enemies.Release(i);
        }
    }

    private void UpdateEnemies()
    {
        UpdateFainting();

        var items = Enemies.Items;
        var playerPosition = Player.Position;
        var slow = TrainerSystem.EnemySpeed(this);
        var map = Map;

        for (var i = 0; i < items.Length; i++)
        {
            ref var e = ref items[i];
            if (!e.IsAlive)
                continue;

            if (e.FlashTicks > 0)
                e.FlashTicks--;

            // Grass and boulders just stand there.
            if (e.Rank == EnemyRank.Prop)
                continue;

            // Chasers aim at the player; swarms and stampedes keep their course; fighters follow their profile.
            var contactDamage = e.ContactDamage;
            Vector2 aim, desired;
            if (e.Rank == EnemyRank.Boss && Boss is { } fight && fight.Index == i)
            {
                BossAi.Think(this, fight, ref e, playerPosition - e.Position, out desired, out aim, ref contactDamage);
            }
            else if (e.Profile is null)
            {
                aim = e.Behavior == EnemyBehavior.Straight ? e.Heading : playerPosition - e.Position;
                desired = aim.NormalizedOrZero() * e.Speed;
            }
            else
            {
                EnemyAi.Think(this, i, ref e, playerPosition - e.Position, out desired, out aim, ref contactDamage);
                if (!e.IsAlive)
                    continue;
            }

            StatusSystem.Steer(this, e, ref desired, ref contactDamage);
            AbilitySystem.Steer(this, ref e, ref desired);
            Run.Plates.PlateSystem.Steer(this, e, ref desired);
            desired *= slow;
            e.Velocity = desired;
            e.Position += (desired + e.Knockback) * RunClock.Dt;
            e.Knockback *= 0.82f;

            // Face where it walks; frozen, sleeping or locked-up Pokémon keep their pose.
            if (!e.IsImmobilized)
                e.Facing = Direction8Extensions.FromVector(e.Status == StatusKind.Confusion ? desired : aim, e.Facing);

            var separation = new SeparationState(i, e.Position, e.Radius, items);
            Grid.Query(e.Position, e.Radius * 2f, ref separation, AccumulateSeparation);
            e.Position += separation.Push.ClampLength(e.Radius * 0.5f) * SeparationStrength;
            if (map.IsConstrained)
                KeepOnMap(map, ref e, desired, i);

            var reach = e.Radius + Player.Radius;
            if (contactDamage > 0f && Vector2.DistanceSquared(e.Position, playerPosition) < reach * reach)
                HurtPlayer(new Hurt(HurtKind.Contact, contactDamage, e.HitsSpecially, e.SpeciesId, e.Type1, i));
        }
    }

    /// <summary>
    /// Walls and obstacles (phase 7.2). A walker pressed against an obstacle slides around it, so a horde flows past a tree
    /// instead of piling up behind it. Travellers (swarms, stampedes) fly over obstacles; at a wall on their way out they leave.
    /// </summary>
    private void KeepOnMap(Worlds.LevelMap map, ref Enemy e, Vector2 desired, int index)
    {
        if (e.Behavior == EnemyBehavior.Straight)
        {
            if (!map.IsBounded)
                return;

            var inside = map.Clamp(e.Position, e.Radius * 0.6f);
            if (inside != e.Position && Vector2.Dot(inside - e.Position, e.Heading) < 0f)
                e.ExpiresAtTick = Ticks;
            e.Position = inside;
            return;
        }

        var push = map.Resolve(ref e.Position, e.Radius * 0.6f);
        if (push == Vector2.Zero || desired == Vector2.Zero)
            return;

        var normal = Vector2.Normalize(push);
        if (Vector2.Dot(desired, normal) >= 0f)
            return;

        // Sideways along the obstacle, on the side it was already heading (a fixed side when it walks straight into it).
        var tangent = new Vector2(-normal.Y, normal.X);
        var along = Vector2.Dot(tangent, desired);
        var speed = desired.Length();
        var side = MathF.Abs(along) > speed * 0.15f ? MathF.Sign(along) : (index & 1) == 0 ? 1f : -1f;
        e.Position += tangent * (side * speed * 0.7f * RunClock.Dt);
    }

    private ref struct SeparationState(int self, Vector2 position, float radius, Span<Enemy> items)
    {
        public readonly int Self = self;
        public readonly Vector2 Position = position;
        public readonly float Radius = radius;
        public readonly Span<Enemy> Items = items;
        public Vector2 Push;
    }

    private static void AccumulateSeparation(int index, ref SeparationState state)
    {
        if (index == state.Self)
            return;

        // Pokémon walk through grass and over boulders.
        ref var other = ref state.Items[index];
        if (other.Rank == EnemyRank.Prop)
            return;

        var delta = state.Position - other.Position;
        var minDistance = (state.Radius + other.Radius) * 0.8f;
        var distanceSquared = delta.LengthSquared();
        if (distanceSquared >= minDistance * minDistance)
            return;

        if (distanceSquared < 1e-6f)
        {
            // Perfectly stacked: nudge apart deterministically by slot index.
            delta = new Vector2(MathF.Cos(state.Self), MathF.Sin(state.Self));
            distanceSquared = 1f;
        }

        var distance = MathF.Sqrt(distanceSquared);
        state.Push += delta / distance * (minDistance - distance);
    }
}
