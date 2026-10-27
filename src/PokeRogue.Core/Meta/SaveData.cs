using System.Text.Json;
using System.Text.Json.Serialization;
using PokeRogue.Core.Run;

namespace PokeRogue.Core.Meta;

/// <summary>A run's difficulty (phase 7.3): Hard opens once a level is won at Normal, Ultra once it is won at Hard.</summary>
public enum Difficulty
{
    Normal,
    Hard,
    Ultra,
}

/// <summary>What the shop bought for one character (phase 6.2).</summary>
public sealed class CharacterProgress
{
    public bool Secondary { get; set; }

    public bool Special { get; set; }

    /// <summary>Grades bought per stat.</summary>
    public Dictionary<StatKind, int> Grades { get; set; } = [];

    /// <summary>Coins spent on the grades, given back by a refund.</summary>
    public int GradeCoins { get; set; }

    public int Grade(StatKind stat) => Grades.GetValueOrDefault(stat);
}

/// <summary>A trainer's grades (phase 6.2) and the coins spent on them.</summary>
public sealed class TrainerProgress
{
    public int Grade { get; set; }

    public int Coins { get; set; }
}

/// <summary>A line's bond and packs for the collectible cards (phase 10), with its records for the cards' backs.</summary>
public sealed class CardLineProgress
{
    /// <summary>Bond points, from every run played with the line.</summary>
    public int Bond { get; set; }

    /// <summary>Bond levels already turned into packs.</summary>
    public int Level { get; set; }

    /// <summary>Packs waiting to be opened.</summary>
    public int Packs { get; set; }

    /// <summary>Species keys of the line reached in some run.</summary>
    public List<string> Reached { get; set; } = [];

    /// <summary>Difficulties at which a level was won with the line.</summary>
    public List<Difficulty> Won { get; set; } = [];

    public int Runs { get; set; }

    public long Kills { get; set; }

    public int BestLevel { get; set; }

    /// <summary>The longest run with the line, in ticks.</summary>
    public int BestTicks { get; set; }
}

/// <summary>A Pokédex entry (phase 6.4): how many times the species was met and defeated.</summary>
public sealed class PokedexEntry
{
    public int Seen { get; set; }

    public int Defeated { get; set; }
}

/// <summary>Runs, wins and the best result on one level at one difficulty.</summary>
public sealed class LevelRecord
{
    public int Runs { get; set; }

    public int Wins { get; set; }

    /// <summary>Longest run in ticks, won or lost.</summary>
    public int BestTicks { get; set; }

    public int BestKills { get; set; }
}

/// <summary>Totals over every run, for the missions that count across runs.</summary>
public sealed class SaveTotals
{
    public int Runs { get; set; }

    public int Victories { get; set; }

    public long Kills { get; set; }

    public int Shinies { get; set; }

    public int Dominants { get; set; }

    public int Chests { get; set; }

    public long CoinsEarned { get; set; }

    public int Specials { get; set; }

    public int TrainerUses { get; set; }

    public int MovesEvolved { get; set; }

    public long PlayTicks { get; set; }

    /// <summary>The highest run level reached.</summary>
    public int BestLevel { get; set; }

    /// <summary>The furthest stage of a line reached (2 = a third stage).</summary>
    public int BestStageDepth { get; set; }

    /// <summary>The longest run, in ticks.</summary>
    public int BestTicks { get; set; }
}

/// <summary>
/// Everything the meta-progression keeps between runs (phase 6.1), as %APPDATA%\PokeRogue\save.json. <see cref="Version"/>
/// lets a later game read an older file; <see cref="SaveStore"/> keeps a backup of the previous one.
/// </summary>
public sealed class SaveData
{
    /// <summary>
    /// 2: run times count ticks of the level's faster clock (2026-10-27). 3: the collectible cards (phase 10); lines
    /// already won with start with a bond.
    /// </summary>
    public const int CurrentVersion = 3;

    public int Version { get; set; } = CurrentVersion;

    /// <summary>Coins in the bank, to spend in the shop.</summary>
    public int Coins { get; set; }

    /// <summary>Characters unlocked by the shop, missions or defeats; those unlocked from the start are not listed.</summary>
    public List<string> Characters { get; set; } = [];

    public Dictionary<string, CharacterProgress> CharacterProgress { get; set; } = [];

    /// <summary>Runs won with each character.</summary>
    public Dictionary<string, int> CharacterWins { get; set; } = [];

    /// <summary>Fossils found on the levels (Block 7), ready to be revived in the shop.</summary>
    public List<string> Fossils { get; set; } = [];

    /// <summary>Arceus' Plates found on the levels (phase 9.1).</summary>
    public List<string> Plates { get; set; } = [];

    /// <summary>The Plate chosen for the last run; null for none.</summary>
    public string? LastPlate { get; set; }

    /// <summary>Trainers unlocked; Ash is not listed.</summary>
    public List<string> Trainers { get; set; } = [];

    /// <summary>Trainers beaten in a challenge (phase 5.3): gym leaders among them are the medals.</summary>
    public List<string> TrainersDefeated { get; set; } = [];

    public Dictionary<string, TrainerProgress> TrainerProgress { get; set; } = [];

    /// <summary>Shop upgrade grades, by upgrade id.</summary>
    public Dictionary<string, int> Upgrades { get; set; } = [];

    /// <summary>Coins spent on the shop upgrades, given back by a refund.</summary>
    public int UpgradeCoins { get; set; }

    /// <summary>Missions completed, in order.</summary>
    public List<string> Missions { get; set; } = [];

    /// <summary>Pokédex by species id.</summary>
    public Dictionary<int, PokedexEntry> Pokedex { get; set; } = [];

    /// <summary>Moves (base and evolved) and held items used in a run at least once, for the collection.</summary>
    public List<string> MovesFound { get; set; } = [];

    public List<string> ItemsFound { get; set; } = [];

    /// <summary>The longest endless run on each level (phase 9.3), in ticks.</summary>
    public Dictionary<string, int> EndlessBest { get; set; } = [];

    /// <summary>Records by "level/difficulty".</summary>
    public Dictionary<string, LevelRecord> Levels { get; set; } = [];

    public SaveTotals Totals { get; set; } = new();

    /// <summary>Bond and packs of each line, by character id (phase 10).</summary>
    public Dictionary<string, CardLineProgress> CardLines { get; set; } = [];

    /// <summary>Collectible cards owned, in the order they came out of their packs.</summary>
    public List<string> Cards { get; set; } = [];

    /// <summary>Cards owned but not looked at yet in the album (they show "NUOVA").</summary>
    public List<string> NewCards { get; set; } = [];

    /// <summary>The last character and trainer played, chosen again when the menu opens.</summary>
    public string? LastCharacter { get; set; }

    public string? LastTrainer { get; set; }

    /// <summary>The last level and difficulty played (phase 7.1), chosen again in the level select.</summary>
    public string? LastLevel { get; set; }

    public Difficulty LastDifficulty { get; set; }

    /// <summary>Wins on a level at a difficulty.</summary>
    public int WinsOn(string levelId, Difficulty difficulty) => Levels.GetValueOrDefault($"{levelId}/{difficulty}")?.Wins ?? 0;

    public CharacterProgress ProgressOf(string characterId)
    {
        if (!CharacterProgress.TryGetValue(characterId, out var progress))
            CharacterProgress[characterId] = progress = new CharacterProgress();
        return progress;
    }

    public TrainerProgress TrainerOf(string trainerId)
    {
        if (!TrainerProgress.TryGetValue(trainerId, out var progress))
            TrainerProgress[trainerId] = progress = new TrainerProgress();
        return progress;
    }

    public LevelRecord LevelOf(string levelId, Difficulty difficulty)
    {
        var key = $"{levelId}/{difficulty}";
        if (!Levels.TryGetValue(key, out var record))
            Levels[key] = record = new LevelRecord();
        return record;
    }

    public CardLineProgress CardLineOf(string characterId)
    {
        if (!CardLines.TryGetValue(characterId, out var line))
            CardLines[characterId] = line = new CardLineProgress();
        return line;
    }

    public PokedexEntry EntryOf(int speciesId)
    {
        if (!Pokedex.TryGetValue(speciesId, out var entry))
            Pokedex[speciesId] = entry = new PokedexEntry();
        return entry;
    }
}

/// <summary>
/// Reads and writes the save (phase 6.1). A write goes to a temporary file first, then the old save becomes the backup
/// and the new one takes its place, so a crash never leaves half a file. A save that cannot be read falls back to the
/// backup, and without one to a fresh start (the broken file is kept aside, never overwritten silently).
/// </summary>
public sealed class SaveStore(string path)
{
    public string Path { get; } = path;

    public string BackupPath => Path + ".bak";

    /// <summary>%APPDATA%\PokeRogue\save.json, beside the settings.</summary>
    public static string DefaultPath =>
        System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PokeRogue", "save.json");

    /// <summary>Where the last <see cref="Load"/> found its data: the save, the backup, or nothing (a fresh start).</summary>
    public SaveSource LastSource { get; private set; }

    public SaveData Load()
    {
        if (TryRead(Path) is { } data)
        {
            LastSource = SaveSource.Save;
            return data;
        }

        var broken = File.Exists(Path);
        if (broken)
            KeepAside(Path);

        if (TryRead(BackupPath) is { } backup)
        {
            LastSource = SaveSource.Backup;
            return backup;
        }

        LastSource = SaveSource.None;
        return new SaveData();
    }

    /// <summary>Writes the save; false when the folder cannot be written (the game goes on, the progress is kept in memory).</summary>
    public bool Save(SaveData data)
    {
        try
        {
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(Path))!);
            data.Version = SaveData.CurrentVersion;
            var temp = Path + ".tmp";
            File.WriteAllText(temp, JsonSerializer.Serialize(data, SaveJsonContext.Default.SaveData));
            if (File.Exists(Path))
                File.Replace(temp, Path, BackupPath, ignoreMetadataErrors: true);
            else
                File.Move(temp, Path);
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static SaveData? TryRead(string path)
    {
        try
        {
            if (!File.Exists(path))
                return null;

            var data = JsonSerializer.Deserialize(File.ReadAllText(path), SaveJsonContext.Default.SaveData);
            return data is null ? null : Migrate(data);
        }
        catch (Exception exception) when (exception is IOException or JsonException or UnauthorizedAccessException or NotSupportedException)
        {
            return null;
        }
    }

    /// <summary>Brings an older save up to <see cref="SaveData.CurrentVersion"/>; a newer one is read as it is.</summary>
    private static SaveData Migrate(SaveData data)
    {
        // Version 1 ran the clock at real speed: its records keep the same time on the clock, which is now fewer ticks.
        if (data.Version < 2)
        {
            static int Clock(int ticks) => (int)((long)ticks * RunClock.TicksPerClockSecond / RunClock.TicksPerSecond);
            foreach (var record in data.Levels.Values)
                record.BestTicks = Clock(record.BestTicks);
            foreach (var level in data.EndlessBest.Keys.ToList())
                data.EndlessBest[level] = Clock(data.EndlessBest[level]);
            data.Totals.BestTicks = Clock(data.Totals.BestTicks);
            data.Version = 2;
        }

        // Version 3 brought the cards: each win so far is worth 20 bond points and the line's first species is reached.
        // The packs those points give come with the first MetaProgress (SyncCards).
        if (data.Version < 3)
        {
            foreach (var (characterId, wins) in data.CharacterWins.Where(w => w.Value > 0))
            {
                var line = data.CardLineOf(characterId);
                line.Bond += wins * 20;
                line.Runs = Math.Max(line.Runs, wins);
                if (!line.Won.Contains(Difficulty.Normal))
                    line.Won.Add(Difficulty.Normal);
            }

            data.Version = 3;
        }

        return data;
    }

    /// <summary>A save that cannot be read is renamed (save.json.broken-…), so the next write does not destroy it.</summary>
    private static void KeepAside(string path)
    {
        try
        {
            File.Move(path, $"{path}.broken-{DateTime.Now:yyyyMMdd-HHmmss}", overwrite: true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
        }
    }
}

public enum SaveSource
{
    /// <summary>No save yet, or neither the save nor its backup could be read.</summary>
    None,
    Save,
    Backup,
}

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    UseStringEnumConverter = true,
    WriteIndented = true,
    AllowTrailingCommas = true)]
[JsonSerializable(typeof(SaveData))]
internal sealed partial class SaveJsonContext : JsonSerializerContext;
