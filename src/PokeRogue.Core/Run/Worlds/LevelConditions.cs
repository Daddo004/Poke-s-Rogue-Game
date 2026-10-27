namespace PokeRogue.Core.Run.Worlds;

/// <summary>The weather of the games (phase 7.2), each changing what some types do.</summary>
public enum WeatherKind
{
    Clear,

    /// <summary>Pioggia: Water moves stronger, Fire ones weaker.</summary>
    Rain,

    /// <summary>Sole: Fire moves stronger, Water ones weaker.</summary>
    Sun,

    /// <summary>Tempesta di sabbia: chips everyone but Rock, Ground and Steel; boosts their moves.</summary>
    Sandstorm,

    /// <summary>Grandine: chips everyone but Ice; boosts Ice moves.</summary>
    Hail,
}

/// <summary>A change of weather in a level's waves file: from <c>at</c> ("m:ss") on, it is <c>kind</c>.</summary>
public sealed record WeatherSpec
{
    public required string At { get; set; }

    public WeatherKind Kind { get; set; }
}

/// <summary>When night falls on a level (phase 7.2): from <c>from</c> to <c>to</c> ("m:ss"), dusk and dawn a minute long.</summary>
public sealed record NightSpec
{
    public required string From { get; set; }

    public required string To { get; set; }
}

/// <summary>
/// A difficulty's rules (phase 7.3), as written in content/data/worlds.json: multipliers on the level's horde, its
/// Dominanti and its boss, and on the coins it gives.
/// </summary>
public sealed record DifficultySpec
{
    public string NameIt { get; set; } = "";

    /// <summary>Enemy PS (on top of the minute's growth).</summary>
    public float Hp { get; set; } = 1f;

    /// <summary>Enemy contact damage, shots and blasts, the boss' too.</summary>
    public float Damage { get; set; } = 1f;

    /// <summary>The waves' minimum, flow and cap of enemies alive, and the size of swarms, rings and stampedes.</summary>
    public float Crowd { get; set; } = 1f;

    /// <summary>Enemy walking speed.</summary>
    public float Speed { get; set; } = 1f;

    /// <summary>The Dominanti's PS, on top of <see cref="Hp"/>.</summary>
    public float DominantHp { get; set; } = 1f;

    /// <summary>Each Dominante comes back this many more times, 40 seconds apart.</summary>
    public int ExtraDominants { get; set; }

    /// <summary>The boss' PS, on top of <see cref="Hp"/>.</summary>
    public float BossHp { get; set; } = 1f;

    /// <summary>The boss' rest between attacks.</summary>
    public float BossRest { get; set; } = 1f;

    /// <summary>Coins picked up in the run.</summary>
    public float Coins { get; set; } = 1f;

    /// <summary>The chance of a shiny.</summary>
    public float Shiny { get; set; } = 1f;

    /// <summary>The coins of the mission "win this level" at this difficulty.</summary>
    public int MissionCoins { get; set; }

    /// <summary>No change at all (tests, previews, Bosco Smeraldo before Block 7).</summary>
    public static DifficultySpec Normal { get; } = new() { NameIt = "Normale" };
}

/// <summary>
/// The pace of an ordinary level, minute by minute (content/data/worlds.json): a wave that gives no numbers of its own
/// takes those of its minute, so the level files only list who comes.
/// </summary>
public sealed record PacingSpec
{
    public int MinAlive { get; set; }

    public float SpawnsPerSecond { get; set; }

    public int MaxAlive { get; set; } = 300;
}

/// <summary>
/// The first minutes of a level (phase 7.4): enemies start with <c>hp</c> of their PS and reach their full PS after
/// <c>minutes</c>. Bosco Smeraldo opens with the frailest Pokémon of all (Caterpie, Weedle); the other levels open with
/// sturdier ones that a level-one character could not clear in time.
/// </summary>
public sealed record WarmUpSpec
{
    public float Minutes { get; set; }

    public float Hp { get; set; } = 1f;

    /// <summary>The PS multiplier at <paramref name="minutes"/> into the level.</summary>
    public float At(float minutes) => Minutes <= 0f || minutes >= Minutes ? 1f : Hp + (1f - Hp) * minutes / Minutes;
}

/// <summary>A legendary of a level (phase 7.3): at Ultra it comes instead of the boss, and beating it unlocks it.</summary>
public sealed record LegendarySpec
{
    public required string Pokemon { get; set; }

    public string TitleIt { get; set; } = "";

    /// <summary>Multiplies the pattern's PS.</summary>
    public float Hp { get; set; } = 1f;

    public float Speed { get; set; } = 1f;

    /// <summary>The move its volleys look like; by default its type's (worlds.json "shots").</summary>
    public string? Shot { get; set; }

    /// <summary>Characters to unlock first: only then it shows up (Regigigas waits for the three Regi).</summary>
    public IReadOnlyList<string> Requires { get; set; } = [];
}
