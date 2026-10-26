using System.Numerics;
using System.Text.Json;
using PokeRogue.Core.Combat;
using PokeRogue.Core.Data;

namespace PokeRogue.Core.Run.Specials;

/// <summary>One type's special move as written in content/data/specials.json.</summary>
public sealed record SpecialSpec
{
    public required PokemonType Type { get; set; }

    public required string CatalogKey { get; set; }

    /// <summary>The status survivors may catch; None for none.</summary>
    public StatusKind Status { get; set; }

    public float StatusChance { get; set; }
}

public sealed record SpecialFile
{
    public int Version { get; set; }

    /// <summary>The gauge is full after dealing this many enemies' worth of PS.</summary>
    public float ChargeEnemies { get; set; } = 60f;

    /// <summary>Seconds the gauge stays empty after a use.</summary>
    public float LockSeconds { get; set; } = 30f;

    /// <summary>Share of their max PS that regular enemies lose, before the type matchup.</summary>
    public float RegularDamage { get; set; } = 0.75f;

    /// <summary>Share of their max PS that Dominanti and bosses lose, before the type matchup.</summary>
    public float EliteDamage { get; set; } = 0.06f;

    /// <summary>Pixels per second of push away from the player.</summary>
    public float Knockback { get; set; } = 140f;

    public IReadOnlyList<SpecialSpec> Specials { get; set; } = [];
}

/// <summary>A type's special move, resolved against the move catalogue.</summary>
public sealed record SpecialMove(PokemonType Type, string Id, string NameIt, StatusKind Status, float StatusChance);

/// <summary>The special gauge of a run.</summary>
public sealed class SpecialState
{
    /// <summary>0 to 1; full means the key works.</summary>
    public float Charge;

    /// <summary>Ticks left before the gauge starts filling again after a use.</summary>
    public int LockTicks;

    public int Uses;

    /// <summary>Damage dealt by the special this run, for the results.</summary>
    public float DamageDealt;

    public bool IsFull => Charge >= 1f;
}

/// <summary>
/// The special moves (phase 4.4), from content/data/specials.json: one per type, charged by the damage the moves deal,
/// unleashed with a key on everything on screen.
/// </summary>
public sealed class SpecialMoves
{
    public const string FileName = "specials.json";

    private readonly Dictionary<PokemonType, SpecialMove> _byType;

    private SpecialMoves(SpecialFile file, IReadOnlyList<SpecialMove> all)
    {
        File = file;
        All = all;
        _byType = all.ToDictionary(s => s.Type);
    }

    public SpecialFile File { get; }

    public IReadOnlyList<SpecialMove> All { get; }

    public SpecialMove this[PokemonType type] =>
        _byType.TryGetValue(type, out var special) ? special : throw new KeyNotFoundException($"No special move of type {type} in {FileName}.");

    public static SpecialMoves Load(string contentRoot)
    {
        var path = Path.Combine(contentRoot, "data", FileName);
        using var stream = System.IO.File.OpenRead(path);
        var file = JsonSerializer.Deserialize(stream, DataJsonContext.Default.SpecialFile)
            ?? throw new InvalidDataException($"{path} is empty.");
        return From(file, GameDataFiles.LoadMoveCatalog(contentRoot));
    }

    public static SpecialMoves From(SpecialFile file, MoveCatalogFile catalog)
    {
        if (file.ChargeEnemies <= 0f || file.LockSeconds < 0f)
            throw new InvalidDataException($"{FileName}: \"chargeEnemies\" must be positive and \"lockSeconds\" not negative.");

        var byKey = catalog.Moves.ToDictionary(m => m.Key, StringComparer.Ordinal);
        var all = new List<SpecialMove>();
        foreach (var spec in file.Specials)
        {
            if (!byKey.TryGetValue(spec.CatalogKey, out var entry))
                throw new InvalidDataException($"{FileName}: '{spec.CatalogKey}' is not a Gen 1–4 move.");
            if (entry.Type != spec.Type)
                throw new InvalidDataException($"{FileName}: {entry.NameIt} is a {entry.Type} move, not {spec.Type}.");
            all.Add(new SpecialMove(spec.Type, spec.CatalogKey, entry.NameIt, spec.Status, spec.StatusChance));
        }

        var duplicate = all.GroupBy(s => s.Type).FirstOrDefault(g => g.Count() > 1);
        if (duplicate is not null)
            throw new InvalidDataException($"{FileName}: two special moves of type {duplicate.Key}.");

        return new SpecialMoves(file, all);
    }

    /// <summary>
    /// The special's hit (phase 4.4): every enemy within the view around the player loses a share of its max PS times the
    /// type matchup, is pushed away, and may catch the status; grass and boulders break. The gauge empties and locks.
    /// </summary>
    public static void Unleash(RunState run, SpecialMove special, Vector2 viewSize)
    {
        var rules = run.SpecialRules!.File;
        var center = run.Player.Position;
        var half = viewSize / 2f + new Vector2(24f);
        var items = run.Enemies.Items;
        for (var i = 0; i < items.Length; i++)
        {
            ref var e = ref items[i];
            var offset = e.Position - center;
            if (!e.IsAlive || MathF.Abs(offset.X) > half.X || MathF.Abs(offset.Y) > half.Y)
                continue;

            if (e.Rank == EnemyRank.Prop)
            {
                run.DamageEnemy(i, e.Hp, flash: false);
                continue;
            }

            if (e.IsShielded)
                continue;

            var effectiveness = DamageCalculator.Effectiveness(run.TypeChart, special.Type, e.Type1, e.Type2);
            var share = e.Rank == EnemyRank.Normal ? rules.RegularDamage : rules.EliteDamage;
            var damage = MathF.Max(1f, MathF.Round(e.MaxHp * share * effectiveness));
            run.Special.DamageDealt += MathF.Min(damage, e.Hp);
            run.AddEventOnResume(new RunEvent(RunEventType.EnemyHit, e.Position, damage, e.SpeciesId, Effectiveness: effectiveness, MoveId: special.Id));
            run.DamageEnemy(i, damage, offset.NormalizedOrZero() * rules.Knockback);

            if (e.IsAlive && special.Status != StatusKind.None && run.Random.NextSingle() < special.StatusChance)
                Statuses.StatusSystem.Apply(run, i, special.Status, damage);
        }

        run.Special.Charge = 0f;
        run.Special.LockTicks = RunClock.Seconds(rules.LockSeconds);
        run.Special.Uses++;
        run.AddEventOnResume(new RunEvent(RunEventType.SpecialUnleashed, center, (int)special.Type, MoveId: special.Id));
    }

    /// <summary>A move hit fills the gauge by the share of the enemy's PS it took (grass and boulders do not count).</summary>
    public static void Charge(RunState run, float dealt, float enemyMaxHp)
    {
        if (run.SpecialRules is not { } rules || run.Special.LockTicks > 0 || enemyMaxHp <= 0f)
            return;

        run.Special.Charge = MathF.Min(1f, run.Special.Charge + dealt / enemyMaxHp / rules.File.ChargeEnemies);
    }
}
