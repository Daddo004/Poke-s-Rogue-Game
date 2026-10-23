using System.Numerics;
using PokeRogue.Core.Combat;
using PokeRogue.Core.Data;
using PokeRogue.Core.Geometry;
using PokeRogue.Core.Run.Items;
using PokeRogue.Core.Run.Moves;
using PokeRogue.Core.Run.Pickups;
using PokeRogue.Core.Run.Progression;
using PokeRogue.Core.Run.Statuses;

namespace PokeRogue.Core.Run;

/// <summary>The whole simulation of one run, advanced at a fixed 60 Hz by <see cref="Tick"/>.</summary>
public sealed class RunState
{
    /// <summary>How hard overlapping enemies push each other apart (fraction of overlap per tick).</summary>
    private const float SeparationStrength = 0.35f;

    /// <summary>Options shown per level-up (Fortuna adds a fourth in Block 6).</summary>
    public const int LevelUpChoiceCount = 3;

    /// <summary>Rilancio, Salta and Bandisci charges per run, until the medals of Block 6 unlock and raise them.</summary>
    public const int StartingLevelUpCharges = 2;

    private readonly List<RunEvent> _events = [];
    private readonly List<RunEvent> _resumeEvents = [];

    public RunState(IReadOnlyList<SpeciesData> pokedex, TypeChart typeChart, int playerSpeciesId, IReadOnlyList<SpawnEntry> spawnTable, Vector2 viewSize, int seed = 1)
    {
        Pokedex = pokedex;
        TypeChart = typeChart;
        Random = new Random(seed);
        Player = new Player(pokedex[playerSpeciesId - 1]);
        Enemies = new EnemyPool(4096);
        Grid = new SpatialGrid(32f, 64);
        Spawner = new Spawner(spawnTable, viewSize);
    }

    public IReadOnlyList<SpeciesData> Pokedex { get; }

    public TypeChart TypeChart { get; }

    /// <summary>Moves that level-ups may offer (the enabled moves of <see cref="MoveLibrary"/>).</summary>
    public IReadOnlyList<MoveDefinition> MovePool { get; init; } = [];

    /// <summary>Evolutions level-ups may offer once their move is maxed and their item held (phase 2.6).</summary>
    public IReadOnlyList<MoveDefinition> Evolutions { get; init; } = [];

    /// <summary>Held items that level-ups may offer (the enabled items of <see cref="ItemLibrary"/>).</summary>
    public IReadOnlyList<ItemDefinition> ItemPool { get; init; } = [];

    /// <summary>How level-up cards are drawn (content/data/levelup.json); null draws every card alike.</summary>
    public LevelUpRules? LevelUpRules { get; init; }

    /// <summary>Status condition rules (content/data/statuses.json); null = no statuses at all. Previews may swap them.</summary>
    public StatusLibrary? StatusRules { get; set; }

    public MoveSystem MoveSystem { get; } = new();

    public PickupSystem Pickups { get; } = new();

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

    public bool IsPaused => LevelUpChoices is not null;

    /// <summary>Survive this long to win (the Block 1 prototype uses 10 minutes); null for endless.</summary>
    public int? TimeLimitTicks { get; init; }

    public RunOutcome Outcome { get; private set; }

    public bool IsOver => Outcome != RunOutcome.None;

    public RunStats Stats { get; } = new();

    public Random Random { get; }

    public Player Player { get; }

    public EnemyPool Enemies { get; }

    public SpatialGrid Grid { get; }

    public Spawner Spawner { get; }

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
        Player.Tick();

        Grid.Rebuild(Enemies, Player.Position);
        StatusSystem.Tick(this);
        UpdateEnemies();
        MoveSystem.Tick(this);
        Pickups.Tick(this);
        Spawner.Tick(this);
        CheckOutcome();
    }

    public void AddEvent(in RunEvent e) => _events.Add(e);

    /// <summary>For changes made while the game is frozen (level-up choices): the event comes out with the first tick after it resumes.</summary>
    public void AddEventOnResume(in RunEvent e) => _resumeEvents.Add(e);

    /// <summary>Gives coins, more with Monetamuleto.</summary>
    public void AddCoins(int amount)
    {
        var gained = (int)MathF.Round(amount * Player.CoinMultiplier);
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
        if (outcome == RunOutcome.Defeat)
            Player.Hp = 0f;
        if (outcome == RunOutcome.Victory)
        {
            // The horde scatters: every enemy faints at once, without drops or KO credit.
            for (var i = 0; i < Enemies.HighWater; i++)
            {
                if (Enemies[i].IsAlive)
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
        else if (TimeLimitTicks is { } limit && Ticks >= limit)
            End(RunOutcome.Victory);
    }

    /// <summary>Called by the pickup system when a gem reaches the player.</summary>
    public void CollectExperience(int value, Vector2 position)
    {
        _events.Add(new RunEvent(RunEventType.GemCollected, position, value));
        var levels = Player.AddXp(value);
        if (levels == 0)
            return;

        PendingLevelUps += levels;
        _events.Add(new RunEvent(RunEventType.LevelUp, Player.Position, Player.Level));
        LevelUpChoices ??= LevelUp.Generate(this, LevelUpChoiceCount);
    }

    /// <summary>Applies one of <see cref="LevelUpChoices"/>; offers the next set if more level-ups are pending.</summary>
    public void ChooseLevelUp(int index)
    {
        if (LevelUpChoices is not { } choices)
            return;

        LevelUp.Apply(this, choices[Math.Clamp(index, 0, choices.Count - 1)]);
        PendingLevelUps--;
        LevelUpChoices = PendingLevelUps > 0 ? LevelUp.Generate(this, LevelUpChoiceCount) : null;
    }

    /// <summary>Rilancio: replaces the options on offer with a fresh draw. False when there is nothing to reroll or no charge left.</summary>
    public bool RerollLevelUp()
    {
        if (LevelUpChoices is null || RerollsLeft <= 0)
            return false;

        RerollsLeft--;
        LevelUpChoices = LevelUp.Generate(this, LevelUpChoiceCount);
        return true;
    }

    /// <summary>Salta: gives up this level-up without taking anything, then offers the next pending one.</summary>
    public bool SkipLevelUp()
    {
        if (LevelUpChoices is null || SkipsLeft <= 0)
            return false;

        SkipsLeft--;
        PendingLevelUps--;
        LevelUpChoices = PendingLevelUps > 0 ? LevelUp.Generate(this, LevelUpChoiceCount) : null;
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

        LevelUpChoices = remaining.Count > 0 ? remaining : LevelUp.Generate(this, LevelUpChoiceCount);
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
        if (!e.IsAlive)
            return;

        var definition = move.Definition;
        var defender = new DefenderProfile(e.Type1, e.Type2, e.Defense, e.SpDefense);
        var power = move.Stats.Power * Player.PowerMultiplier(definition.Category);
        var attacker = Player.Attacker;
        if (definition.CritBonus > 0f)
            attacker = attacker with { CritChance = attacker.CritChance + definition.CritBonus };
        var result = DamageCalculator.Compute(power, definition.Category, definition.Type, attacker, defender, TypeChart, Random);

        // Abisso: now and then the ground swallows the enemy whole (bosses will be immune, Block 3).
        var ohko = definition.OhkoChance > 0f && Random.NextSingle() < definition.OhkoChance;
        if (ohko)
            result = result with { Amount = MathF.Max(result.Amount, e.Hp) };

        var dealt = MathF.Min(result.Amount, e.Hp);
        move.DamageDealt += dealt;

        // Sanguisuga's drain and Conchinella's lifesteal both give back part of the damage.
        var heal = definition.Drain + Player.Lifesteal;
        if (heal > 0f)
            Player.Heal(dealt * heal);

        _events.Add(new RunEvent(RunEventType.EnemyHit, e.Position, result.Amount, e.SpeciesId,
            Effectiveness: result.Effectiveness, Critical: result.Critical, MoveId: definition.Id, Ohko: ohko));
        DamageEnemy(index, result.Amount, direction * move.Stats.Knockback);
        StatusSystem.OnHit(this, index, move, result.Amount);
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
        if (!e.IsAlive)
            return;

        e.Hp -= damage;
        if (flash)
            e.FlashTicks = Enemy.HitFlashTicks;
        e.Knockback += knockback;
        if (e.Hp <= 0f)
        {
            Enemies.Defeat(index);
            Stats.RecordKill(e.SpeciesId);
            _events.Add(new RunEvent(RunEventType.EnemyDefeated, e.Position, 0f, e.SpeciesId));
            Pickups.Gems.Spawn(e.Position, e.XpValue);
        }
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

        for (var i = 0; i < items.Length; i++)
        {
            ref var e = ref items[i];
            if (!e.IsAlive)
                continue;

            if (e.FlashTicks > 0)
                e.FlashTicks--;

            var toPlayer = playerPosition - e.Position;
            var desired = toPlayer.NormalizedOrZero() * e.Speed;
            var contactDamage = e.ContactDamage;
            StatusSystem.Steer(this, e, ref desired, ref contactDamage);
            e.Velocity = desired;
            e.Position += (desired + e.Knockback) * RunClock.Dt;
            e.Knockback *= 0.82f;

            // Face where it walks; frozen, sleeping or locked-up Pokémon keep their pose.
            if (!e.IsImmobilized)
                e.Facing = Direction8Extensions.FromVector(e.Status == StatusKind.Confusion ? desired : toPlayer, e.Facing);

            var separation = new SeparationState(i, e.Position, e.Radius, items);
            Grid.Query(e.Position, e.Radius * 2f, ref separation, AccumulateSeparation);
            e.Position += separation.Push.ClampLength(e.Radius * 0.5f) * SeparationStrength;

            var reach = e.Radius + Player.Radius;
            if (contactDamage > 0f && Vector2.DistanceSquared(e.Position, playerPosition) < reach * reach)
            {
                var taken = Player.TakeContactDamage(contactDamage, e.HitsSpecially);
                if (taken > 0f)
                {
                    Stats.RecordDamageTaken(taken);
                    _events.Add(new RunEvent(RunEventType.PlayerHurt, playerPosition, taken, e.SpeciesId));
                }
            }
        }
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

        ref var other = ref state.Items[index];
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
