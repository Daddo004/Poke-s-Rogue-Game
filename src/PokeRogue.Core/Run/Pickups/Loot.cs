using System.Numerics;
using System.Text.Json;
using PokeRogue.Core.Data;

namespace PokeRogue.Core.Run.Pickups;

/// <summary>Things lying on the ground that the player picks up by walking over them (phase 3.5).</summary>
public enum LootKind : byte
{
    /// <summary>A few coins; drawn to the player like gems.</summary>
    Coin,

    /// <summary>"Pepita": more coins.</summary>
    Nugget,

    /// <summary>"Pepita grande": what a shiny Pokémon leaves.</summary>
    BigNugget,

    /// <summary>"Pozione": restores a share of the PS.</summary>
    Potion,

    /// <summary>"Caramella Rara": one level at once.</summary>
    RareCandy,

    /// <summary>"Repellente": every regular enemy on screen faints, leaving its gem.</summary>
    Repel,

    /// <summary>"Condividi Esp.": every gem on the map flies to the player.</summary>
    ExpShare,

    /// <summary>"Baccarancia": what a defeated Pokémon now and then leaves, a small heal (Vampire Survivors' floor chicken).</summary>
    OranBerry,

    /// <summary>A held item lying on the map or left by a shiny: picked up, it is gained or levelled up (<see cref="Loot.ItemId"/>).</summary>
    HeldItem,
}

public sealed record BreakableSpec
{
    /// <summary>Chance that a map cell holds one.</summary>
    public float Chance { get; set; }

    /// <summary>HP before the minute's growth.</summary>
    public float Hp { get; set; } = 1f;

    public float Defense { get; set; } = 20f;

    /// <summary>Chance to leave something when broken.</summary>
    public float DropChance { get; set; }
}

public sealed record LootDropSpec
{
    public required LootKind Kind { get; set; }

    public int Weight { get; set; } = 1;
}

/// <summary>Breakables, drops and shinies as written in content/data/loot.json.</summary>
public sealed record LootFile
{
    public int Version { get; set; } = 1;

    /// <summary>False keeps all of it out of real runs until its look is approved; previews show it anyway.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>The map is cut into square cells this wide; each may hold one breakable.</summary>
    public float CellSize { get; set; } = 160f;

    public BreakableSpec Grass { get; set; } = new();

    public BreakableSpec Rock { get; set; } = new();

    /// <summary>What a broken prop leaves, by weight.</summary>
    public IReadOnlyList<LootDropSpec> Drops { get; set; } = [];

    /// <summary>Chance that a regular enemy leaves a coin.</summary>
    public float EnemyCoinChance { get; set; }

    /// <summary>Chance that a wave spawns its Pokémon shiny.</summary>
    public float ShinyChance { get; set; }

    /// <summary>Chance that a regular Pokémon leaves a Baccarancia (phase 3.6).</summary>
    public float EnemyFoodChance { get; set; }

    /// <summary>PS a Baccarancia restores.</summary>
    public float FoodHeal { get; set; } = 30f;

    public int CoinValue { get; set; } = 2;

    public int NuggetValue { get; set; } = 10;

    public int BigNuggetValue { get; set; } = 25;

    /// <summary>Share of the maximum PS a Pozione restores.</summary>
    public float PotionHeal { get; set; } = 0.3f;
}

/// <summary>An item on the ground. Age counts ticks since it appeared (its pop and bob).</summary>
public record struct Loot(int Serial, Vector2 Position, LootKind Kind, int Age = 0, bool Magnetized = false, float Speed = 0f, string? ItemId = null)
{
    /// <summary>The player picks it up from this close (plus its own radius).</summary>
    public const float Reach = 8f;
}

/// <summary>The rules of content/data/loot.json, checked.</summary>
public sealed class LootRules
{
    public const string FileName = "loot.json";

    private LootRules(LootFile file) => File = file;

    public LootFile File { get; }

    public static LootRules Load(string contentRoot)
    {
        var path = Path.Combine(contentRoot, "data", FileName);
        using var stream = System.IO.File.OpenRead(path);
        var file = JsonSerializer.Deserialize(stream, DataJsonContext.Default.LootFile)
            ?? throw new InvalidDataException($"{path} is empty.");
        return From(file);
    }

    public static LootRules From(LootFile file)
    {
        float[] chances = [file.Grass.Chance, file.Rock.Chance, file.Grass.DropChance, file.Rock.DropChance, file.EnemyCoinChance, file.ShinyChance, file.EnemyFoodChance];
        if (chances.Any(c => c is < 0f or > 1f) || file.Grass.Chance + file.Rock.Chance > 1f)
            throw new InvalidDataException($"{FileName}: chances go from 0 to 1, and grass plus rock at most 1.");
        if (file.CellSize < 48f || file.Grass.Hp <= 0f || file.Rock.Hp <= 0f)
            throw new InvalidDataException($"{FileName}: cells of at least 48 px and positive HP.");
        if (file.Drops.Count == 0 || file.Drops.Any(d => d.Weight <= 0))
            throw new InvalidDataException($"{FileName}: drops need positive weights.");

        return new LootRules(file);
    }

    /// <summary>A random drop by weight.</summary>
    public LootKind RollDrop(Random random)
    {
        var roll = random.Next(File.Drops.Sum(d => d.Weight));
        foreach (var drop in File.Drops)
        {
            if ((roll -= drop.Weight) < 0)
                return drop.Kind;
        }

        return File.Drops[0].Kind;
    }
}

/// <summary>
/// Breakables and the loot on the ground (phase 3.5). The map is cut into cells; each cell may hold tall grass or a
/// boulder, decided by a hash of its coordinates, so the same map comes back when the player returns. Props near the
/// player are spawned in the enemy pool (so every move can hit them); far ones are put away; broken ones are gone.
/// </summary>
public sealed class LootSystem
{
    /// <summary>Props are placed and put away every this many ticks.</summary>
    private const int FieldInterval = 15;

    private const float MagnetSpeed = 260f;

    private readonly Dictionary<long, int> _activeProps = [];
    private readonly HashSet<long> _broken = [];
    private int _serial;
    private int _nextField;

    public List<Loot> Items { get; } = [];

    public int ActivePropCount => _activeProps.Count;

    /// <summary>Drops an item on the ground; a <see cref="LootKind.HeldItem"/> names its item.</summary>
    public void Drop(Vector2 position, LootKind kind, string? itemId = null)
    {
        if (Items.Count < 400)
            Items.Add(new Loot(++_serial, position, kind, ItemId: itemId));
    }

    private bool _mapPlaced;

    /// <summary>
    /// A held item a shiny leaves: one the player has and can still level up, or one it does not have yet while it
    /// has room, never a banished one. Null when nothing fits.
    /// </summary>
    public static string? ShinyItem(RunState run)
    {
        var player = run.Player;
        var choices = run.ItemPool
            .Where(d => !run.Banished.Contains(d.Id))
            .Where(d => player.Items.FirstOrDefault(i => i.Definition.Id == d.Id) is { } owned
                ? owned.Level < d.MaxLevel
                : player.Items.Count < Progression.LevelUp.MaxItems)
            .ToList();
        return choices.Count == 0 ? null : choices[run.Random.Next(choices.Count)].Id;
    }

    /// <summary>A prop was broken: it will not come back, and it may leave something.</summary>
    public void OnBroken(RunState run, in Enemy prop)
    {
        _activeProps.Remove(prop.PropCell);
        _broken.Add(prop.PropCell);
        if (run.LootRules is not { } rules)
            return;

        var spec = prop.Breakable == BreakableKind.Rock ? rules.File.Rock : rules.File.Grass;
        if (run.Random.NextSingle() < spec.DropChance)
            Drop(prop.Position, rules.RollDrop(run.Random));
    }

    public void Tick(RunState run)
    {
        if (run.LootRules is not { } rules)
            return;

        // The level's own items, laid out once at the start (phase 3.6).
        if (!_mapPlaced)
        {
            _mapPlaced = true;
            foreach (var item in run.Spawner.Timeline.MapItems)
                Drop(item.Position, item.Kind, item.ItemId);
        }

        // At once on the first tick (and after a time jump), then every few ticks.
        if (run.Ticks >= _nextField || run.Ticks < _nextField - FieldInterval)
        {
            _nextField = run.Ticks + FieldInterval;
            UpdateField(run, rules);
        }

        CollectLoot(run, rules);
    }

    private void UpdateField(RunState run, LootRules rules)
    {
        var cell = rules.File.CellSize;
        var player = run.Player.Position;
        var half = run.Spawner.ViewSize / 2f + new Vector2(cell);

        // Put away props that are well out of sight.
        var limit = half.Length() * 1.4f;
        foreach (var (key, index) in _activeProps.ToList())
        {
            ref var e = ref run.Enemies[index];
            if (!e.IsAlive || e.Rank != EnemyRank.Prop || e.PropCell != key)
            {
                _activeProps.Remove(key);
                continue;
            }

            if (Vector2.DistanceSquared(e.Position, player) > limit * limit)
            {
                run.Enemies.Release(index);
                _activeProps.Remove(key);
            }
        }

        // Place the props of the cells around the view.
        var x0 = (int)MathF.Floor((player.X - half.X) / cell);
        var x1 = (int)MathF.Floor((player.X + half.X) / cell);
        var y0 = (int)MathF.Floor((player.Y - half.Y) / cell);
        var y1 = (int)MathF.Floor((player.Y + half.Y) / cell);
        for (var cy = y0; cy <= y1; cy++)
        {
            for (var cx = x0; cx <= x1; cx++)
            {
                var key = ((long)cx << 32) | (uint)cy;
                if (_activeProps.ContainsKey(key) || _broken.Contains(key))
                    continue;

                var roll = Hash(cx, cy, 0) / (float)int.MaxValue;
                var kind = roll < rules.File.Grass.Chance ? BreakableKind.Grass
                    : roll < rules.File.Grass.Chance + rules.File.Rock.Chance ? BreakableKind.Rock
                    : BreakableKind.None;
                if (kind == BreakableKind.None)
                    continue;

                // Somewhere inside the cell, away from its edges; never right on top of the player.
                var at = new Vector2(
                    (cx + 0.15f + 0.7f * (Hash(cx, cy, 1) / (float)int.MaxValue)) * cell,
                    (cy + 0.15f + 0.7f * (Hash(cx, cy, 2) / (float)int.MaxValue)) * cell);
                if (Vector2.DistanceSquared(at, player) < 40f * 40f)
                    continue;

                var spec = kind == BreakableKind.Rock ? rules.File.Rock : rules.File.Grass;
                var index = run.Enemies.SpawnProp(kind, at, spec.Hp * run.Spawner.Timeline.HpMultiplier(run.Ticks), spec.Defense,
                    kind == BreakableKind.Rock ? PokemonType.Rock : PokemonType.Grass, key);
                if (index >= 0)
                    _activeProps[key] = index;
            }
        }
    }

    private void CollectLoot(RunState run, LootRules rules)
    {
        var player = run.Player;
        var pickup = player.PickupRadius * player.PickupRadius;
        for (var i = Items.Count - 1; i >= 0; i--)
        {
            var loot = Items[i];
            loot.Age++;
            var toPlayer = player.Position - loot.Position;
            var distanceSquared = toPlayer.LengthSquared();

            // Coins are drawn in like gems; everything else waits to be walked over.
            if (loot.Kind is LootKind.Coin or LootKind.Nugget or LootKind.BigNugget && distanceSquared <= pickup)
                loot.Magnetized = true;
            if (loot.Magnetized && distanceSquared > 1f)
            {
                loot.Speed = MathF.Min(loot.Speed + 900f * RunClock.Dt, 700f) + MagnetSpeed * RunClock.Dt;
                var distance = MathF.Sqrt(distanceSquared);
                loot.Position += toPlayer / distance * MathF.Min(loot.Speed * RunClock.Dt, distance);
            }

            var reach = player.Radius + Loot.Reach;
            if ((player.Position - loot.Position).LengthSquared() <= reach * reach && !player.IsDown)
            {
                Items.RemoveAt(i);
                Collect(run, rules, loot);
                continue;
            }

            Items[i] = loot;
        }
    }

    private static void Collect(RunState run, LootRules rules, Loot loot)
    {
        var file = rules.File;
        var player = run.Player;
        switch (loot.Kind)
        {
            case LootKind.Coin:
                run.AddCoins(file.CoinValue);
                break;
            case LootKind.Nugget:
                run.AddCoins(file.NuggetValue);
                break;
            case LootKind.BigNugget:
                run.AddCoins(file.BigNuggetValue);
                break;
            case LootKind.Potion:
                player.Heal(player.MaxHp * file.PotionHeal);
                break;
            case LootKind.RareCandy:
                run.GrantLevel();
                break;
            case LootKind.ExpShare:
                run.Pickups.Gems.MagnetizeAll();
                break;
            case LootKind.Repel:
                Repel(run);
                break;
            case LootKind.OranBerry:
                player.Heal(file.FoodHeal);
                break;
            case LootKind.HeldItem when loot.ItemId is { } id:
                GiveItem(run, rules, id);
                break;
        }

        run.AddEvent(new RunEvent(RunEventType.LootCollected, loot.Position, (int)loot.Kind));
    }

    /// <summary>
    /// A held item found on the ground: one level more if the player has it, a new item if there is room, otherwise
    /// (maxed, or all six slots taken) a nugget's worth of coins.
    /// </summary>
    private static void GiveItem(RunState run, LootRules rules, string id)
    {
        var player = run.Player;
        var owned = player.Items.FirstOrDefault(i => i.Definition.Id == id);
        var definition = run.ItemPool.FirstOrDefault(d => d.Id == id);
        if (owned is not null && owned.Level < owned.Definition.MaxLevel)
            owned.Level++;
        else if (owned is null && definition is not null && player.Items.Count < Progression.LevelUp.MaxItems)
            player.Items.Add(new Items.ItemInstance(definition) { AcquiredTick = run.Ticks });
        else
        {
            run.AddCoins(rules.File.NuggetValue);
            return;
        }

        player.RecalculateStats();
    }

    /// <summary>Every regular enemy on screen faints and leaves its gem; Dominanti, bosses and props are untouched.</summary>
    private static void Repel(RunState run)
    {
        var half = run.Spawner.ViewSize / 2f;
        var player = run.Player.Position;
        for (var i = 0; i < run.Enemies.HighWater; i++)
        {
            ref var e = ref run.Enemies[i];
            if (!e.IsAlive || e.Rank != EnemyRank.Normal)
                continue;

            var offset = e.Position - player;
            if (MathF.Abs(offset.X) <= half.X && MathF.Abs(offset.Y) <= half.Y)
                run.DamageEnemy(i, float.MaxValue, flash: false);
        }
    }

    private static int Hash(int x, int y, int salt)
    {
        unchecked
        {
            var h = x * 374761393 + y * 668265263 + salt * 1274126177 + 0x5bd1e995;
            h = (h ^ (h >> 13)) * 1274126177;
            return (h ^ (h >> 16)) & 0x7FFFFFFF;
        }
    }
}
