using PokeRogue.Core.Data;
using PokeRogue.Core.Run.Abilities;

namespace PokeRogue.Core.Run.Worlds;

/// <summary>
/// The weather and the night of a level (phase 7.2). The level's file says how the sky changes; some abilities override
/// it for the whole run (Piovischio, Siccità, Scendineve bring their weather, Riparo clears it). Rain and sun make Water
/// and Fire moves stronger or weaker; a sandstorm and hail boost their own types, wear down the other enemies every second
/// and slow a player that does not stand them. At night the waves send their night species.
/// </summary>
public static class WeatherSystem
{
    /// <summary>Rain and sun: the favoured type's moves deal this much more, the other this much less.</summary>
    public const float StrongShare = 0.3f;

    /// <summary>Sandstorm and hail: their own types' moves deal this much more.</summary>
    public const float BoostShare = 0.2f;

    /// <summary>Sandstorm and hail, every second: regular enemies that do not stand them lose this share of their max PS.</summary>
    public const float EnemyChip = 0.03f;

    /// <summary>
    /// Sandstorm and hail slow a player that does not stand them. They used to take 1% of its PS every second down to
    /// the last one, and the simulations' players were left at 1 PS for the whole level: any touch was a knock-out.
    /// </summary>
    public const float PlayerSlow = 0.9f;

    /// <summary>The weather now: the ability's, if it brings one, otherwise the level's.</summary>
    public static WeatherKind Current(RunState run) =>
        AbilitySystem.WeatherOverride(run) ?? run.Spawner.Timeline.WeatherAt(run.Ticks);

    /// <summary>Every tick: notices a change of weather or of the time of day, and lets a sandstorm or hail bite once a second.</summary>
    public static void Tick(RunState run)
    {
        var weather = Current(run);
        if (weather != run.Weather)
        {
            var before = run.Weather;
            run.Weather = weather;
            run.AddEvent(new RunEvent(RunEventType.WeatherChanged, run.Player.Position, (int)weather, Target: new System.Numerics.Vector2((int)before, 0)));
        }

        var night = run.Spawner.Timeline.IsNight(run.Ticks);
        if (night != run.IsNight)
        {
            run.IsNight = night;
            run.AddEvent(new RunEvent(RunEventType.NightChanged, run.Player.Position, night ? 1 : 0));
        }

        var types = run.Player.Types;
        run.Player.WeatherSpeedMultiplier = Withstands(weather, types[0], types.Count > 1 ? types[1] : null) ? 1f : PlayerSlow;
        if (weather is WeatherKind.Sandstorm or WeatherKind.Hail && run.Ticks % RunClock.TicksPerSecond == 0)
            Chip(run, weather);
    }

    /// <summary>A move's damage under the weather.</summary>
    public static float DamageMultiplier(RunState run, PokemonType type) => run.Weather switch
    {
        WeatherKind.Rain when type == PokemonType.Water => 1f + StrongShare,
        WeatherKind.Rain when type == PokemonType.Fire => 1f - StrongShare,
        WeatherKind.Sun when type == PokemonType.Fire => 1f + StrongShare,
        WeatherKind.Sun when type == PokemonType.Water => 1f - StrongShare,
        WeatherKind.Sandstorm when type is PokemonType.Rock or PokemonType.Ground or PokemonType.Steel => 1f + BoostShare,
        WeatherKind.Hail when type == PokemonType.Ice => 1f + BoostShare,
        _ => 1f,
    };

    /// <summary>Whether a Pokémon of these types stands the weather unharmed.</summary>
    public static bool Withstands(WeatherKind weather, PokemonType type1, PokemonType? type2) => weather switch
    {
        WeatherKind.Sandstorm => IsSandproof(type1) || type2 is { } t && IsSandproof(t),
        WeatherKind.Hail => type1 == PokemonType.Ice || type2 == PokemonType.Ice,
        _ => true,
    };

    private static bool IsSandproof(PokemonType type) => type is PokemonType.Rock or PokemonType.Ground or PokemonType.Steel;

    /// <summary>Sand or hail bites: regular enemies that do not stand it lose a share of their PS.</summary>
    private static void Chip(RunState run, WeatherKind weather)
    {
        for (var i = 0; i < run.Enemies.HighWater; i++)
        {
            ref var e = ref run.Enemies[i];
            if (!e.IsAlive || e.Rank != EnemyRank.Normal || Withstands(weather, e.Type1, e.Type2))
                continue;
            run.DamageEnemy(i, e.MaxHp * EnemyChip, flash: false);
        }
    }
}
