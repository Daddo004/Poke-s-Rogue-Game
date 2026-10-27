using System.Text.Json;
using PokeRogue.Core.Data;
using PokeRogue.Core.Run.Characters;
using PokeRogue.Core.Run.Trainers;

namespace PokeRogue.Core.Meta;

/// <summary>What a mission asks (phase 6.3). <c>count</c> is the target; <c>oneRun</c> counts within a single run instead of across runs.</summary>
public enum MissionKind
{
    /// <summary>Play <c>count</c> runs.</summary>
    Runs,

    /// <summary>Last <c>count</c> minutes in a run (<c>noHealing</c>: without picking up potions or berries).</summary>
    Survive,

    /// <summary>Win the mission's level (at its difficulty).</summary>
    WinLevel,

    /// <summary>Win a run with <c>character</c>.</summary>
    WinWith,

    /// <summary>Defeat <c>count</c> Pokémon (of <c>type</c>, when given).</summary>
    Defeat,

    DefeatShiny,

    DefeatDominant,

    /// <summary>Reach run level <c>count</c>.</summary>
    ReachLevel,

    /// <summary>Evolve <c>count</c> moves.</summary>
    EvolveMove,

    /// <summary>Reach a stage <c>count</c> evolutions down the line (2 = the third stage).</summary>
    EvolvePokemon,

    /// <summary>Beat <c>trainer</c> in a challenge, or <c>count</c> trainers of <c>role</c> (from <c>region</c>, when given).</summary>
    DefeatTrainer,

    /// <summary>Collect <c>count</c> coins.</summary>
    CollectCoins,

    OpenChests,

    UseSpecial,

    UseTrainer,

    /// <summary>See <c>count</c> species in the Pokédex, or every species of <c>region</c> when <c>count</c> is 0.</summary>
    PokedexSeen,

    /// <summary>Defeat <c>count</c> different species.</summary>
    PokedexDefeated,

    /// <summary>Unlock every other character of <c>groups</c>.</summary>
    UnlockCharacters,

    /// <summary>Win every level of <c>region</c> (its levels come with Block 7: not possible yet).</summary>
    CompleteRegion,
}

/// <summary>What completing a mission gives: coins, a character, a trainer.</summary>
public sealed record MissionReward
{
    public int Coins { get; set; }

    public string? Character { get; set; }

    public string? Trainer { get; set; }
}

/// <summary>One mission as written in content/data/missions.json (phase 6.3).</summary>
public sealed record MissionSpec
{
    public required string Id { get; set; }

    public required string NameIt { get; set; }

    public required string DescriptionIt { get; set; }

    public required MissionKind Kind { get; set; }

    public int Count { get; set; } = 1;

    public bool OneRun { get; set; }

    /// <summary>The level the mission belongs to; null for a global mission.</summary>
    public string? Level { get; set; }

    /// <summary>With <see cref="Level"/>: the difficulty to play it at.</summary>
    public Difficulty Difficulty { get; set; } = Difficulty.Normal;

    public string? Character { get; set; }

    public string? Trainer { get; set; }

    public PokemonType? Type { get; set; }

    public TrainerRole? Role { get; set; }

    public TrainerRegion? Region { get; set; }

    public IReadOnlyList<CharacterGroup> Groups { get; set; } = [];

    public bool NoHealing { get; set; }

    public MissionReward Reward { get; set; } = new();
}

public sealed record MissionFile(int Version, IReadOnlyList<MissionSpec> Missions);

/// <summary>Every mission, from content/data/missions.json (phase 6.3).</summary>
public sealed class MissionLibrary
{
    public const string FileName = "missions.json";

    private MissionLibrary(IReadOnlyList<MissionSpec> all) => All = all;

    public IReadOnlyList<MissionSpec> All { get; }

    public MissionSpec? Find(string id) => All.FirstOrDefault(m => m.Id == id);

    /// <summary>The mission whose reward is <paramref name="characterId"/>, if any.</summary>
    public MissionSpec? Unlocking(string characterId) => All.FirstOrDefault(m => m.Reward.Character == characterId);

    /// <param name="worlds">With the worlds, every level gets a "win it" mission at each difficulty the file does not already give (Block 7).</param>
    public static MissionLibrary Load(string contentRoot, CharacterLibrary characters, TrainerLibrary trainers, Run.Worlds.WorldLibrary? worlds = null)
    {
        var path = Path.Combine(contentRoot, "data", FileName);
        using var stream = File.OpenRead(path);
        var file = JsonSerializer.Deserialize(stream, DataJsonContext.Default.MissionFile) ?? throw new InvalidDataException($"{path} is empty.");
        return From(file, characters, trainers, worlds);
    }

    public static MissionLibrary From(MissionFile file, CharacterLibrary characters, TrainerLibrary trainers, Run.Worlds.WorldLibrary? worlds = null)
    {
        if (worlds is not null)
            file = file with { Missions = file.Missions.Concat(LevelMissions(file, worlds)).ToList() };

        foreach (var mission in file.Missions)
        {
            string Where() => $"{FileName}, '{mission.Id}'";
            if (mission.Count < 0 || mission.Count == 0 && !(mission.Kind == MissionKind.PokedexSeen && mission.Region is not null))
                throw new InvalidDataException($"{Where()}: \"count\" must be positive (0 only for a whole region's Pokédex).");
            if (mission.Character is { } c && characters.Find(c) is null)
                throw new InvalidDataException($"{Where()}: '{c}' is not a character.");
            if (mission.Trainer is { } t && trainers.Find(t) is null)
                throw new InvalidDataException($"{Where()}: '{t}' is not a trainer.");
            if (mission.Reward.Character is { } rc && characters.Find(rc) is null)
                throw new InvalidDataException($"{Where()}: the reward '{rc}' is not a character.");
            if (mission.Reward.Trainer is { } rt && trainers.Find(rt) is null)
                throw new InvalidDataException($"{Where()}: the reward '{rt}' is not a trainer.");

            var problem = mission.Kind switch
            {
                MissionKind.WinLevel when mission.Level is null => "a WinLevel mission needs \"level\"",
                MissionKind.WinWith when mission.Character is null => "a WinWith mission needs \"character\"",
                MissionKind.DefeatTrainer when mission.Trainer is null && mission.Role is null => "a DefeatTrainer mission needs \"trainer\" or \"role\"",
                MissionKind.UnlockCharacters when mission.Groups.Count == 0 => "an UnlockCharacters mission needs \"groups\"",
                MissionKind.CompleteRegion when mission.Region is null => "a CompleteRegion mission needs \"region\"",
                _ => null,
            };
            if (problem is not null)
                throw new InvalidDataException($"{Where()}: {problem}.");
        }

        var duplicate = file.Missions.GroupBy(m => m.Id).FirstOrDefault(g => g.Count() > 1);
        if (duplicate is not null)
            throw new InvalidDataException($"{FileName}: '{duplicate.Key}' appears twice.");

        // Every character unlocked by a mission needs exactly one mission that gives it.
        foreach (var character in characters.All.Where(c => c.Unlock.Kind == UnlockKind.Mission))
        {
            var givers = file.Missions.Count(m => m.Reward.Character == character.Id);
            if (givers != 1)
                throw new InvalidDataException($"{FileName}: {character.Id} is unlocked by a mission, and {givers} missions give it.");
        }

        if (worlds is not null && file.Missions.FirstOrDefault(m => m.Level is { } level && worlds.Find(level) is null) is { } stray)
            throw new InvalidDataException($"{FileName}, '{stray.Id}': '{stray.Level}' is not a level of worlds.json.");

        return new MissionLibrary(file.Missions);
    }

    /// <summary>
    /// "Win the level" at each difficulty, for every level whose mission the file does not write by hand (phase 7.3):
    /// "<c>level-normale</c>", "<c>level-difficile</c>", "<c>level-ultra</c>", the coins of worlds.json's difficulty, a
    /// quarter more for each world further on.
    /// </summary>
    private static IEnumerable<MissionSpec> LevelMissions(MissionFile file, Run.Worlds.WorldLibrary worlds)
    {
        foreach (var level in worlds.Levels)
        {
            foreach (var difficulty in Enum.GetValues<Difficulty>())
            {
                if (file.Missions.Any(m => m.Kind == MissionKind.WinLevel && m.Level == level.Id && m.Difficulty == difficulty))
                    continue;

                var spec = worlds.Difficulty(difficulty);
                var coins = (int)(MathF.Round(spec.MissionCoins * (1f + 0.25f * level.World.Index) / 50f) * 50f);
                var suffix = difficulty switch { Difficulty.Normal => "normale", Difficulty.Hard => "difficile", _ => "ultra" };
                yield return new MissionSpec
                {
                    Id = $"{level.Id}-{suffix}",
                    NameIt = difficulty == Difficulty.Normal ? level.NameIt : $"{level.NameIt} {spec.NameIt.ToLowerInvariant()}",
                    DescriptionIt = difficulty == Difficulty.Normal
                        ? $"Vinci {level.NameIt}."
                        : $"Vinci {level.NameIt} a difficoltà {spec.NameIt}.",
                    Kind = MissionKind.WinLevel,
                    Level = level.Id,
                    Difficulty = difficulty,
                    Reward = new MissionReward { Coins = coins },
                };
            }
        }
    }
}
