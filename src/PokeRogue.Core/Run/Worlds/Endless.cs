using PokeRogue.Core.Data;
using PokeRogue.Core.Run.Enemies;
using PokeRogue.Core.Run.Waves;

namespace PokeRogue.Core.Run.Worlds;

/// <summary>The endless mode as written in worlds.json (phase 9.3).</summary>
public sealed record EndlessSpec
{
    /// <summary>Past the level's end, its waves and events from this time ("m:ss") to the end play again and again.</summary>
    public string LoopFrom { get; set; } = "10:00";

    /// <summary>When the Reaper comes.</summary>
    public string ReaperAt { get; set; } = "30:00";

    /// <summary>The Reaper's contact, shots and circles hit this much harder for every minute it has been on the field.</summary>
    public float EnragePerMinute { get; set; } = 0.15f;

    /// <summary>And it walks this much faster for every minute.</summary>
    public float SpeedPerMinute { get; set; } = 0.03f;

    /// <summary>The Reaper: Darkrai, with its own way of fighting.</summary>
    public BossSpec? Reaper { get; set; }
}

/// <summary>
/// The endless mode (phase 9.3), as in Vampire Survivors: the level's boss no longer ends the run, the last minutes of its
/// waves come round again with the enemies growing as the minutes pass, and at <see cref="ReaperAtTicks"/> Darkrai the
/// Reaper arrives. Beating it wins the run and unlocks Darkrai; otherwise the run goes on until the player falls.
/// </summary>
public sealed class EndlessRules
{
    private EndlessRules(EndlessSpec spec, int loopFromTicks, int reaperAtTicks, BossDefinition reaper)
    {
        Spec = spec;
        LoopFromTicks = loopFromTicks;
        ReaperAtTicks = reaperAtTicks;
        Reaper = reaper;
    }

    public EndlessSpec Spec { get; }

    public int LoopFromTicks { get; }

    public int ReaperAtTicks { get; }

    public BossDefinition Reaper { get; }

    /// <summary>The time the waves play at <paramref name="tick"/> of a level lasting <paramref name="duration"/>: past the end, a loop of its last stretch.</summary>
    public int Looped(int tick, int duration)
    {
        if (tick < duration || duration <= LoopFromTicks)
            return tick;
        return LoopFromTicks + (tick - duration) % (duration - LoopFromTicks);
    }

    /// <summary>How many times the loop has started again at <paramref name="tick"/> (0 before the end).</summary>
    public int Cycle(int tick, int duration) =>
        tick < duration || duration <= LoopFromTicks ? 0 : (tick - duration) / (duration - LoopFromTicks) + 1;

    public static EndlessRules From(EndlessSpec spec, IReadOnlyList<SpeciesData> pokedex, Moves.MoveLibrary moves)
    {
        const string where = WorldLibrary.FileName + ", \"endless\"";
        if (spec.Reaper is not { } reaper)
            throw new InvalidDataException($"{where}: it needs its \"reaper\".");
        var species = pokedex.FirstOrDefault(s => s.Key == reaper.Pokemon)
            ?? throw new InvalidDataException($"{where}: '{reaper.Pokemon}' is not a Pokémon key.");
        var loopFrom = WaveTimeline.ParseTime(spec.LoopFrom, where);
        var reaperAt = WaveTimeline.ParseTime(spec.ReaperAt, where);
        if (reaperAt <= loopFrom || spec.EnragePerMinute < 0f || spec.SpeedPerMinute < 0f)
            throw new InvalidDataException($"{where}: the Reaper comes after the loop's start, and grows (or stays), never weaker.");

        return new EndlessRules(spec, loopFrom, reaperAt, BossDefinition.From(reaper, species.Id, moves, WorldLibrary.FileName));
    }
}
