using System.Numerics;
using PokeRogue.Core.Combat;
using PokeRogue.Core.Data;
using PokeRogue.Core.Geometry;
using PokeRogue.Core.Run.Moves;
using PokeRogue.Core.Run.Pickups;

namespace PokeRogue.Core.Run.Abilities;

/// <summary>How the player was hurt: an enemy's touch, a shot, a kamikaze's blast, or a boss' rock.</summary>
public enum HurtKind
{
    Contact,
    Shot,
    Blast,
    Hazard,
}

/// <summary>A hit about to land on the player. <see cref="Type"/> is the attack's type; <see cref="EnemyIndex"/> is -1 when no enemy touched it.</summary>
public readonly record struct Hurt(HurtKind Kind, float Damage, bool Special, int SpeciesId, PokemonType? Type, int EnemyIndex = -1);

/// <summary>What an <see cref="RunEventType.AbilityTriggered"/> event shows.</summary>
public enum AbilityEffect
{
    Teleported,
    Transformed,
    TransformEnded,
    Blocked,
    Dodged,
    Endured,
    Immune,
    Boosted,
    Healed,
    Switched,
    Awakened,
    Ready,
}

/// <summary>The player's ability timers and stacks during a run.</summary>
public sealed class AbilityState
{
    /// <summary>Active abilities: ticks before the key works again.</summary>
    public int CooldownTicks;

    /// <summary>Trasformazione: ticks left as the copied Pokémon.</summary>
    public int TransformTicks;

    /// <summary>Muta: ticks before the next shed skin, and whether one is waiting for a hit.</summary>
    public int ShieldTicks = RunClock.Seconds(AbilitySystem.ShedSkinSeconds);

    public bool ShieldReady;

    /// <summary>Vigore: already used this run.</summary>
    public bool SturdyUsed;

    /// <summary>Fuocardore: +1% Fire damage per stack.</summary>
    public int FireStacks;

    /// <summary>Acceleratore: +3% speed and cooldown per stack.</summary>
    public int SpeedStacks;

    /// <summary>Agiltecnica: ticks left running faster.</summary>
    public int BoostTicks;

    /// <summary>Elettrorapid and Cuordeciso: stacks of speed and the ticks they last.</summary>
    public int ChargeStacks;

    public int ChargeTicks;

    /// <summary>Download: the category it boosts now.</summary>
    public MoveCategory? Downloaded;

    /// <summary>Lentoinizio: the slow start is over.</summary>
    public bool Awake;
}

/// <summary>
/// The player's ability (phase 4.3): a trigger system hooked into the run. Passive abilities react to hits dealt and
/// taken, knock-outs, contacts, low PS, level-ups and pickups, or work over time; active ones (Teletrasporto,
/// Trasformazione) fire on their key and then recharge. The ability is the current stage's (<see cref="RunState.Stage"/>).
/// </summary>
public static class AbilitySystem
{
    public const float TeleportDistance = 110f;
    public const float TeleportCooldownSeconds = 8f;
    public const float TransformSeconds = 15f;
    public const float TransformCooldownSeconds = 40f;
    public const float TransformRange = 320f;
    public const float ShedSkinSeconds = 15f;
    public const float SpeedBoostSeconds = 30f;
    public const int MaxSpeedStacks = 10;
    public const float SpeedBoostStep = 0.02f;

    /// <summary>Alternacura: share of the max PS healed per level gained (phase 4.5: was 10%, about half the PS a minute).</summary>
    public const float NaturalCureShare = 0.04f;
    public const float SlowStartSeconds = 180f;
    public const float PressureRange = 80f;
    public const float ContactStatusChance = 0.3f;
    public const float SynchronizeRange = 80f;

    /// <summary>Every ability id this system knows; characters.json may use only these.</summary>
    public static readonly IReadOnlySet<string> Known = new HashSet<string>(StringComparer.Ordinal)
    {
        "overgrow", "blaze", "torrent", "leaf-guard", "flash-fire", "unburden", "speed-boost", "damp", "iron-fist",
        "shell-armor", "battle-armor", "swift-swim", "chlorophyll", "pressure", "suction-cups", "mold-breaker", "sturdy",
        "static", "teleport", "keen-eye", "levitate", "technician", "motor-drive", "intimidate", "transform", "adaptability",
        "gluttony", "shed-skin", "serene-grace", "download", "guts", "synchronize", "pure-power", "water-veil",
        "natural-cure", "cute-charm", "frisk", "rough-skin", "steadfast", "snow-warning", "snow-cloak", "flame-body",
        "ice-body", "clear-body", "drizzle", "drought", "air-lock", "slow-start", "ancestor", "hydration", "bad-dreams",
        "multitype",
    };

    public static string? Id(RunState run) => run.Stage?.Ability.Id;

    /// <summary>Whether the ability is used with a key (and so has a cooldown in the HUD).</summary>
    public static bool IsActive(RunState run) => run.Stage?.Ability.Active == true;

    /// <summary>The key of the active ability would work now (scripted players press it then).</summary>
    public static bool CanUse(RunState run) => IsActive(run) && run.Ability.CooldownTicks == 0 && run.Ability.TransformTicks == 0;

    /// <summary>Seconds between uses of the active ability.</summary>
    public static float CooldownSeconds(string? id) => id switch
    {
        "teleport" => TeleportCooldownSeconds,
        "transform" => TransformCooldownSeconds,
        _ => 0f,
    };

    /// <summary>
    /// What the HUD's gauge shows, 0 to 1: the recharge of an active ability or of Muta, Vigore still unused, the stacks of
    /// Acceleratore, Fuocardore, Elettrorapid and Cuordeciso, Agiltecnica's sprint, Lentoinizio waking up. Null: no gauge.
    /// </summary>
    public static float? Gauge(RunState run)
    {
        var state = run.Ability;
        return Id(run) switch
        {
            "teleport" or "transform" => 1f - state.CooldownTicks / (float)RunClock.Seconds(CooldownSeconds(Id(run)) * run.Player.CooldownMultiplier),
            "shed-skin" => state.ShieldReady ? 1f : 1f - state.ShieldTicks / (float)RunClock.Seconds(ShedSkinSeconds),
            "sturdy" => state.SturdyUsed ? 0f : 1f,
            "speed-boost" => state.SpeedStacks / (float)MaxSpeedStacks,
            "flash-fire" => state.FireStacks / 25f,
            "motor-drive" => state.ChargeStacks / 10f,
            "steadfast" => state.ChargeStacks / 5f,
            "unburden" => state.BoostTicks / (float)RunClock.Seconds(10f),
            "slow-start" => state.Awake ? 1f : run.Ticks / (float)RunClock.Seconds(SlowStartSeconds),
            _ => null,
        };
    }

    /// <summary>Every tick, after the player has moved: timers, regeneration, speed, and the active ability's key.</summary>
    public static void Tick(RunState run, bool usePressed)
    {
        if (Id(run) is not { } id)
            return;

        var state = run.Ability;
        var player = run.Player;
        if (state.CooldownTicks > 0)
            state.CooldownTicks--;

        switch (id)
        {
            case "teleport" when usePressed && state.CooldownTicks == 0:
                Teleport(run);
                break;
            case "transform":
                TickTransform(run, usePressed);
                break;
            case "shed-skin" when !state.ShieldReady && --state.ShieldTicks <= 0:
                state.ShieldReady = true;
                Trigger(run, AbilityEffect.Ready);
                break;
            case "speed-boost":
                var stacks = Math.Min(MaxSpeedStacks, run.Ticks / RunClock.Seconds(SpeedBoostSeconds));
                if (stacks > state.SpeedStacks)
                {
                    state.SpeedStacks = stacks;
                    Trigger(run, AbilityEffect.Boosted);
                }

                break;
            case "ice-body":
                player.Heal(1f * RunClock.Dt);
                break;
            case "hydration":
                player.Heal(0.3f * RunClock.Dt);
                break;
            case "bad-dreams" when run.Ticks % RunClock.TicksPerSecond == 0:
                Nightmares(run);
                break;
            case "download" when run.Ticks % RunClock.Seconds(60f) == 1:
                Download(run);
                break;
            case "multitype" when run.Ticks % RunClock.TicksPerSecond == 0:
                Multitype(run);
                break;
            case "slow-start" when !state.Awake && run.Ticks >= RunClock.Seconds(SlowStartSeconds):
                state.Awake = true;
                Trigger(run, AbilityEffect.Awakened);
                break;
        }

        if (state.BoostTicks > 0)
            state.BoostTicks--;
        if (state.ChargeTicks > 0 && --state.ChargeTicks == 0)
            state.ChargeStacks = 0;

        player.AbilitySpeedMultiplier = SpeedMultiplier(run, id);
        player.AbilityCooldownMultiplier = id == "speed-boost" ? 1f - SpeedBoostStep * state.SpeedStacks : 1f;
    }

    private static float SpeedMultiplier(RunState run, string id)
    {
        var state = run.Ability;
        return id switch
        {
            // Phase 4.5: Acceleratore was 3% a stack and Elettrorapid 2%; with them the pilot outran every wave.
            "speed-boost" => 1f + SpeedBoostStep * state.SpeedStacks,
            "unburden" when state.BoostTicks > 0 => 1.4f,
            "motor-drive" => 1f + 0.01f * state.ChargeStacks,
            "steadfast" => 1f + 0.05f * state.ChargeStacks,
            "swift-swim" or "chlorophyll" => 1.05f,
            // Phase 4.5: was half speed; Regigigas fell in the second minute.
            "slow-start" when !state.Awake => 0.8f,
            _ => 1f,
        };
    }

    /// <summary>The player's attack as this hit sees it: Adattabilità, Sguardofermo, Rompiforma.</summary>
    public static AttackerProfile Attacker(RunState run, AttackerProfile attacker) => Id(run) switch
    {
        "adaptability" => attacker with { Stab = 2f },
        "keen-eye" => attacker with { CritChance = attacker.CritChance + 0.1f, CritDamage = 2f },
        "mold-breaker" => attacker with { IgnoresResistances = true },
        _ => attacker,
    };

    /// <summary>Damage multiplier of a move hit from the ability (Erbaiuto, Tecnico, Forzapura, Fuocardore…).</summary>
    public static float DamageMultiplier(RunState run, MoveInstance move)
    {
        var player = run.Player;
        var definition = move.Definition;
        var physical = definition.Category == MoveCategory.Physical;
        var low = player.Hp < player.MaxHp / 3f;
        return Id(run) switch
        {
            "overgrow" when low && definition.Type == PokemonType.Grass => 1.5f,
            "blaze" when low && definition.Type == PokemonType.Fire => 1.5f,
            "torrent" when low && definition.Type == PokemonType.Water => 1.5f,
            "iron-fist" when definition.Type == PokemonType.Fighting => 1.2f,
            "technician" when move.Stats.Power <= 12f => 1.5f,
            "guts" when physical && player.Hp < player.MaxHp / 2f => 1.3f,
            "pure-power" when physical => 1.4f,
            "flash-fire" when definition.Type == PokemonType.Fire => 1f + 0.01f * run.Ability.FireStacks,
            "drizzle" when definition.Type == PokemonType.Water => 1.2f,
            "drought" when definition.Type is PokemonType.Fire or PokemonType.Ground => 1.1f,
            "slow-start" => run.Ability.Awake ? 1.2f : 0.5f,
            "download" when run.Ability.Downloaded == definition.Category => 1.2f,
            _ => 1f,
        };
    }

    /// <summary>After a move hit: Elettrorapid charges up, Fuocardore grows with every Fire knock-out.</summary>
    public static void OnMoveHit(RunState run, MoveInstance move, bool knockedOut)
    {
        var state = run.Ability;
        switch (Id(run))
        {
            case "motor-drive" when move.Definition.Type == PokemonType.Electric:
                state.ChargeStacks = Math.Min(10, state.ChargeStacks + 1);
                state.ChargeTicks = RunClock.Seconds(3f);
                break;
            case "flash-fire" when knockedOut && move.Definition.Type == PokemonType.Fire:
                state.FireStacks = Math.Min(25, state.FireStacks + 1);
                break;
        }
    }

    /// <summary>Multiplier on a move's chance to inflict its status: Leggiadro doubles every chance, Scendineve the freezes.</summary>
    public static float StatusChanceMultiplier(RunState run, MoveDefinition move) => Id(run) switch
    {
        "serene-grace" => 2f,
        "snow-warning" when move.Status == StatusKind.Freeze => 2f,
        _ => 1f,
    };

    /// <summary>Sincronismo: a status the player inflicts also hits the two enemies nearest to the first.</summary>
    public static void OnStatusInflicted(RunState run, int index, StatusKind kind, float damage, int sourceSlot)
    {
        if (Id(run) != "synchronize")
            return;

        var origin = run.Enemies[index].Position;
        Span<int> nearest = [-1, -1];
        Span<float> distances = [float.MaxValue, float.MaxValue];
        var items = run.Enemies.Items;
        for (var i = 0; i < items.Length; i++)
        {
            ref var e = ref items[i];
            if (i == index || !e.IsAlive || e.Rank == EnemyRank.Prop || e.Status != StatusKind.None)
                continue;

            var d = Vector2.DistanceSquared(e.Position, origin);
            if (d > SynchronizeRange * SynchronizeRange)
                continue;
            if (d < distances[0])
            {
                (nearest[1], distances[1]) = (nearest[0], distances[0]);
                (nearest[0], distances[0]) = (i, d);
            }
            else if (d < distances[1])
            {
                (nearest[1], distances[1]) = (i, d);
            }
        }

        foreach (var other in nearest)
        {
            if (other >= 0)
                Statuses.StatusSystem.Apply(run, other, kind, damage, sourceSlot);
        }
    }

    /// <summary>
    /// What share of a hit reaches the player: 0 when an ability stops it (Levitazione, Umidità, Mantelneve, Muta), less
    /// with the defensive ones (Guscioscudo, Fogliamanto, Prepotenza, Idrovelo, Riparo, Corpochiaro).
    /// </summary>
    public static float HurtMultiplier(RunState run, in Hurt hurt)
    {
        var player = run.Player;
        var state = run.Ability;
        var enemyTypes = hurt.SpeciesId > 0 ? run.Pokedex[hurt.SpeciesId - 1].Types : [];
        switch (Id(run))
        {
            case "levitate" when hurt.Kind == HurtKind.Hazard || hurt.Type == PokemonType.Ground:
            case "damp" when hurt.Kind == HurtKind.Blast:
                Trigger(run, AbilityEffect.Immune);
                return 0f;
            case "snow-cloak" when run.Random.NextSingle() < 0.15f:
                player.Shrug();
                Trigger(run, AbilityEffect.Dodged);
                return 0f;
            case "shed-skin" when state.ShieldReady:
                state.ShieldReady = false;
                state.ShieldTicks = RunClock.Seconds(ShedSkinSeconds);
                player.Shrug();
                Trigger(run, AbilityEffect.Blocked);
                return 0f;
            case "shell-armor" or "battle-armor" when hurt.Kind == HurtKind.Contact:
                return 0.85f;
            case "leaf-guard" when player.Hp > player.MaxHp * 0.8f:
                return 0.75f;
            case "intimidate" when hurt.Kind is HurtKind.Contact or HurtKind.Blast:
                return 0.85f;
            case "water-veil" when enemyTypes.Contains(PokemonType.Fire):
                return 0.5f;
            case "air-lock" when enemyTypes.Contains(PokemonType.Flying):
                return 0.5f;
            case "clear-body" when hurt.Kind == HurtKind.Shot:
                return 0.7f;
            default:
                return 1f;
        }
    }

    /// <summary>
    /// After a hit landed: the toucher may be paralysed, burned, confused, cut or held back; Cuordeciso speeds up;
    /// Fuocardore's flame goes out; Vigore keeps the player standing once per run.
    /// </summary>
    public static void OnHurt(RunState run, in Hurt hurt, float taken)
    {
        var state = run.Ability;
        var player = run.Player;
        var id = Id(run);
        if (id == "sturdy" && player.IsDown && !state.SturdyUsed)
        {
            state.SturdyUsed = true;
            player.Hp = 1f;
            Trigger(run, AbilityEffect.Endured);
        }

        switch (id)
        {
            case "steadfast":
                state.ChargeStacks = Math.Min(5, state.ChargeStacks + 1);
                state.ChargeTicks = RunClock.Seconds(5f);
                break;
            case "flash-fire":
                state.FireStacks = 0;
                break;
        }

        if (hurt.Kind != HurtKind.Contact || hurt.EnemyIndex < 0 || !run.Enemies[hurt.EnemyIndex].IsAlive)
            return;

        var index = hurt.EnemyIndex;
        switch (id)
        {
            case "static" when run.Random.NextSingle() < ContactStatusChance:
                Statuses.StatusSystem.Apply(run, index, StatusKind.Paralysis, taken);
                break;
            case "flame-body" when run.Random.NextSingle() < ContactStatusChance:
                Statuses.StatusSystem.Apply(run, index, StatusKind.Burn, taken);
                break;
            case "cute-charm" when run.Random.NextSingle() < ContactStatusChance:
                Statuses.StatusSystem.Apply(run, index, StatusKind.Confusion, taken);
                break;
            case "rough-skin":
                run.DamageEnemy(index, MathF.Max(1f, taken * 0.25f), flash: true);
                break;
            case "suction-cups":
                run.Enemies[index].SlowTicks = RunClock.Seconds(2f);
                break;
        }
    }

    /// <summary>Bends an enemy's walk: Pressione slows those close to the player, Ventose those that touched it.</summary>
    public static void Steer(RunState run, ref Enemy e, ref Vector2 velocity)
    {
        if (e.SlowTicks > 0)
        {
            e.SlowTicks--;
            velocity *= 0.5f;
        }

        if (Id(run) == "pressure" && Vector2.DistanceSquared(e.Position, run.Player.Position) < PressureRange * PressureRange)
            velocity *= 0.85f;
    }

    /// <summary>Alternacura: every level gained heals a tenth of the max PS.</summary>
    public static void OnLevelUp(RunState run, int levels)
    {
        if (Id(run) != "natural-cure" || levels <= 0)
            return;

        run.Player.Heal(run.Player.MaxHp * NaturalCureShare * levels);
        Trigger(run, AbilityEffect.Healed);
    }

    /// <summary>Indagine: regular enemies drop coins and berries twice as often.</summary>
    public static float DropMultiplier(RunState run) => Id(run) == "frisk" ? 2f : 1f;

    /// <summary>Voracità: food and potions heal twice as much.</summary>
    public static float HealMultiplier(RunState run) => Id(run) == "gluttony" ? 2f : 1f;

    /// <summary>Agiltecnica: something picked up from the ground (not money) makes the player run faster for a while.</summary>
    public static void OnLootCollected(RunState run, LootKind kind)
    {
        if (Id(run) != "unburden" || kind is LootKind.Coin or LootKind.Nugget or LootKind.BigNugget)
            return;

        run.Ability.BoostTicks = RunClock.Seconds(10f);
        Trigger(run, AbilityEffect.Boosted);
    }

    /// <summary>Antenato (Mew): new moves from level-ups start at level 2.</summary>
    public static int NewMoveLevel(RunState run, MoveDefinition move) => Id(run) == "ancestor" ? Math.Min(2, move.MaxLevel) : 1;

    private static void Teleport(RunState run)
    {
        var player = run.Player;
        var direction = player.IsMoving ? Vector2.Normalize(player.Velocity) : player.Facing.ToVector();
        var from = player.Position;
        player.Position += direction * TeleportDistance;
        player.Blink(20);
        run.Ability.CooldownTicks = RunClock.Seconds(TeleportCooldownSeconds * player.CooldownMultiplier);
        run.AddEvent(new RunEvent(RunEventType.AbilityTriggered, from, (int)AbilityEffect.Teleported, Target: player.Position, MoveId: "teleport"));
    }

    private static void TickTransform(RunState run, bool usePressed)
    {
        var state = run.Ability;
        var player = run.Player;
        if (state.TransformTicks > 0 && --state.TransformTicks == 0)
        {
            player.SetDisguise(null);
            Trigger(run, AbilityEffect.TransformEnded);
            return;
        }

        if (!usePressed || state.CooldownTicks > 0 || state.TransformTicks > 0)
            return;

        // The strongest Pokémon around: the highest base stat total, bosses and Dominanti included.
        SpeciesData? strongest = null;
        foreach (ref var e in run.Enemies.Items)
        {
            if (!e.IsAlive || e.Rank == EnemyRank.Prop || Vector2.DistanceSquared(e.Position, player.Position) > TransformRange * TransformRange)
                continue;

            var species = run.Pokedex[e.SpeciesId - 1];
            if (strongest is null || species.BaseStats.Total > strongest.BaseStats.Total)
                strongest = species;
        }

        if (strongest is null)
            return;

        player.SetDisguise(strongest);
        state.TransformTicks = RunClock.Seconds(TransformSeconds);
        state.CooldownTicks = RunClock.Seconds(TransformCooldownSeconds * player.CooldownMultiplier);
        run.AddEvent(new RunEvent(RunEventType.AbilityTriggered, player.Position, (int)AbilityEffect.Transformed, strongest.Id, MoveId: "transform"));
    }

    /// <summary>Sogniamari: every second, sleeping enemies lose a tenth of their PS.</summary>
    private static void Nightmares(RunState run)
    {
        var items = run.Enemies.Items;
        for (var i = 0; i < items.Length; i++)
        {
            if (items[i].IsAlive && items[i].Status == StatusKind.Sleep)
                run.DamageFromStatus(i, MathF.Max(1f, items[i].MaxHp * 0.1f));
        }
    }

    /// <summary>Download: boosts the category the enemies on the field defend worse against.</summary>
    private static void Download(RunState run)
    {
        float defense = 0f, spDefense = 0f;
        foreach (ref var e in run.Enemies.Items)
        {
            if (!e.IsAlive || e.Rank == EnemyRank.Prop)
                continue;
            defense += e.Defense;
            spDefense += e.SpDefense;
        }

        var category = defense <= spDefense ? MoveCategory.Physical : MoveCategory.Special;
        if (run.Ability.Downloaded == category)
            return;

        run.Ability.Downloaded = category;
        Trigger(run, AbilityEffect.Switched);
    }

    /// <summary>Multitipo: the player takes the type of the move that has dealt the most damage so far.</summary>
    private static void Multitype(RunState run)
    {
        var best = run.Player.Moves.Where(m => m.DamageDealt > 0f).MaxBy(m => m.DamageDealt);
        if (best is null || run.Player.TypeOverride == best.Definition.Type)
            return;

        run.Player.TypeOverride = best.Definition.Type;
        Trigger(run, AbilityEffect.Switched);
    }

    private static void Trigger(RunState run, AbilityEffect effect) =>
        run.AddEvent(new RunEvent(RunEventType.AbilityTriggered, run.Player.Position, (int)effect, MoveId: Id(run)));
}
