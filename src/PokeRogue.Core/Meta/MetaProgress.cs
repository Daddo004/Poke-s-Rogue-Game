using PokeRogue.Core.Data;
using PokeRogue.Core.Run;
using PokeRogue.Core.Run.Characters;
using PokeRogue.Core.Run.Items;
using PokeRogue.Core.Run.Trainers;

namespace PokeRogue.Core.Meta;

/// <summary>Why a purchase went through or not.</summary>
public enum Purchase
{
    Done,
    NotEnoughCoins,

    /// <summary>Not on sale: a character won by a mission or a defeat, a fossil not found yet, a move of a locked character.</summary>
    Unavailable,

    /// <summary>Already owned, or every grade bought.</summary>
    Owned,

    /// <summary>The special needs the secondary move first.</summary>
    NeedsSecondary,

    /// <summary>Not enough medals for this grade.</summary>
    NeedsMedals,
}

/// <summary>What the end of a run brought (Block 6): coins banked, missions completed, characters and trainers unlocked, Pokédex news.</summary>
public sealed record RunReport(
    int CoinsBanked,
    int MissionCoins,
    int Bank,
    IReadOnlyList<MissionSpec> Missions,
    IReadOnlyList<CharacterDefinition> NewCharacters,
    IReadOnlyList<TrainerDefinition> NewTrainers,
    int NewSpecies)
{
    /// <summary>Fossils found this run (phase 7.4), ready to be revived in the shop.</summary>
    public IReadOnlyList<CharacterDefinition> Fossils { get; init; } = [];

    /// <summary>Levels and difficulties opened by this run (phase 7.1).</summary>
    public IReadOnlyList<string> Opened { get; init; } = [];

    /// <summary>Plates found for the first time (phase 9.1).</summary>
    public IReadOnlyList<Run.Plates.PlateDefinition> Plates { get; init; } = [];

    public bool HasNews => Missions.Count > 0 || NewCharacters.Count > 0 || NewTrainers.Count > 0 || Fossils.Count > 0 || Opened.Count > 0 || Plates.Count > 0;
}

/// <summary>
/// The meta-progression (Block 6) over a <see cref="SaveData"/>: what is unlocked, the shop (characters, their moves and
/// stat grades, trainer grades, upgrades for every run, with refunds), the missions' progress, and what a finished run
/// leaves behind. Every change is written at once when a <see cref="SaveStore"/> is given.
/// </summary>
public sealed class MetaProgress(
    SaveData data,
    ShopRules shop,
    CharacterLibrary characters,
    TrainerLibrary trainers,
    MissionLibrary missions,
    IReadOnlyList<SpeciesData> pokedex,
    SaveStore? store = null,
    Run.Worlds.WorldLibrary? worlds = null,
    Run.Plates.PlateLibrary? plates = null)
{
    public SaveData Data { get; } = data;

    /// <summary>Arceus' Plates (phase 9.1); null in tests that do not use them.</summary>
    public Run.Plates.PlateLibrary? Plates { get; } = plates;

    /// <summary>A Plate found on its level in some earlier run.</summary>
    public bool IsUnlocked(Run.Plates.PlateDefinition plate) => Data.Plates.Contains(plate.Id);

    /// <summary>The Plates found so far, in the order of the file.</summary>
    public IReadOnlyList<Run.Plates.PlateDefinition> UnlockedPlates() => Plates?.All.Where(IsUnlocked).ToList() ?? [];

    /// <summary>Remembers the Plate chosen for the next runs (null for none).</summary>
    public void ChoosePlate(string? plateId)
    {
        Data.LastPlate = plateId;
        Persist();
    }

    /// <summary>The worlds and their levels (Block 7); null in tests that only look at the shop.</summary>
    public Run.Worlds.WorldLibrary? Worlds { get; } = worlds;

    public ShopRules Shop { get; } = shop;

    public CharacterLibrary Characters { get; } = characters;

    public TrainerLibrary Trainers { get; } = trainers;

    public MissionLibrary Missions { get; } = missions;

    public IReadOnlyList<SpeciesData> Pokedex { get; } = pokedex;

    public int Coins => Data.Coins;

    /// <summary>Writes the save, when there is a store.</summary>
    public void Persist() => store?.Save(Data);

    // ---- Characters ----

    public bool IsUnlocked(CharacterDefinition character) => character.Unlock.Kind == UnlockKind.Start || Data.Characters.Contains(character.Id);

    /// <summary>The coins it costs in the shop; null when it is not for sale (or not yet: a fossil still to find).</summary>
    public int? Price(CharacterDefinition character) => character.Unlock.Kind switch
    {
        UnlockKind.Shop => character.Unlock.Price,
        UnlockKind.Fossil when Data.Fossils.Contains(character.Id) => character.Unlock.Price,
        _ => null,
    };

    public Purchase Buy(CharacterDefinition character)
    {
        if (IsUnlocked(character))
            return Purchase.Owned;
        if (Price(character) is not { } price)
            return Purchase.Unavailable;
        return Spend(price, () => Data.Characters.Add(character.Id));
    }

    public bool HasSecondary(CharacterDefinition character) => Data.CharacterProgress.TryGetValue(character.Id, out var p) && p.Secondary;

    public bool HasSpecial(CharacterDefinition character) => Data.CharacterProgress.TryGetValue(character.Id, out var p) && p.Special;

    /// <summary>The price used for a character's moves: its own, or the reference price when it is not sold.</summary>
    private int BasePrice(CharacterDefinition character) =>
        character.Unlock.Price > 0 ? character.Unlock.Price : Shop.Pokemon.ReferencePrice;

    public int SecondaryPrice(CharacterDefinition character) => Scaled(Shop.Pokemon.Secondary, BasePrice(character));

    public int SpecialPrice(CharacterDefinition character) => Scaled(Shop.Pokemon.Special, BasePrice(character));

    private static int Scaled(ScaledPrice price, int basePrice) => (int)(MathF.Round((price.Base + price.Share * basePrice) / 50f) * 50f);

    public Purchase BuySecondary(CharacterDefinition character)
    {
        if (!IsUnlocked(character))
            return Purchase.Unavailable;
        if (HasSecondary(character))
            return Purchase.Owned;
        return Spend(SecondaryPrice(character), () => Data.ProgressOf(character.Id).Secondary = true);
    }

    public Purchase BuySpecial(CharacterDefinition character)
    {
        if (!IsUnlocked(character))
            return Purchase.Unavailable;
        if (HasSpecial(character))
            return Purchase.Owned;
        if (!HasSecondary(character))
            return Purchase.NeedsSecondary;
        return Spend(SpecialPrice(character), () => Data.ProgressOf(character.Id).Special = true);
    }

    public int Grade(CharacterDefinition character, StatKind stat) =>
        Data.CharacterProgress.TryGetValue(character.Id, out var p) ? p.Grade(stat) : 0;

    /// <summary>The next grade's price; null when every grade is bought.</summary>
    public int? GradePrice(CharacterDefinition character, StatKind stat)
    {
        var grade = Grade(character, stat);
        return grade >= Shop.Pokemon.MaxGrade ? null : Shop.Pokemon.GradePrice * (grade + 1);
    }

    public Purchase BuyGrade(CharacterDefinition character, StatKind stat)
    {
        if (!IsUnlocked(character))
            return Purchase.Unavailable;
        if (GradePrice(character, stat) is not { } price)
            return Purchase.Owned;
        return Spend(price, () =>
        {
            var progress = Data.ProgressOf(character.Id);
            progress.Grades[stat] = progress.Grade(stat) + 1;
            progress.GradeCoins += price;
        });
    }

    /// <summary>Gives back every coin spent on the character's stat grades and clears them. Returns the coins.</summary>
    public int RefundGrades(CharacterDefinition character)
    {
        if (!Data.CharacterProgress.TryGetValue(character.Id, out var progress) || progress.GradeCoins == 0)
            return 0;

        var coins = progress.GradeCoins;
        Data.Coins += coins;
        progress.Grades.Clear();
        progress.GradeCoins = 0;
        Persist();
        return coins;
    }

    /// <summary>The character's stat grades as one bonus.</summary>
    public ItemBonus GradeBonus(CharacterDefinition character) =>
        Shop.Pokemon.Stats.Aggregate(ItemBonus.None, (sum, stat) => sum + stat.PerGrade * Grade(character, stat.Stat));

    // ---- Trainers ----

    public bool IsUnlocked(TrainerDefinition trainer) => trainer.Unlock.Kind == UnlockKind.Start || Data.Trainers.Contains(trainer.Id);

    public bool IsDefeated(TrainerDefinition trainer) => Data.TrainersDefeated.Contains(trainer.Id);

    /// <summary>Medals: the gym leaders beaten in a challenge.</summary>
    public int Medals => Data.TrainersDefeated.Count(id => Trainers.Find(id)?.Role == TrainerRole.GymLeader);

    public int TrainerGrade(TrainerDefinition trainer) => Data.TrainerProgress.TryGetValue(trainer.Id, out var p) ? p.Grade : 0;

    /// <summary>The next grade's price, dearer with every grade owned on any trainer; null when maxed.</summary>
    public int? TrainerGradePrice(TrainerDefinition trainer)
    {
        var grade = TrainerGrade(trainer);
        if (grade >= Shop.Trainers.MaxGrade)
            return null;
        var owned = Data.TrainerProgress.Values.Sum(p => p.Grade);
        return (int)(MathF.Round(Shop.Trainers.GradePrice * (grade + 1) * (1f + Shop.Trainers.MarkupPerGrade * owned) / 10f) * 10f);
    }

    public Purchase BuyTrainerGrade(TrainerDefinition trainer)
    {
        if (!IsUnlocked(trainer))
            return Purchase.Unavailable;
        if (TrainerGradePrice(trainer) is not { } price)
            return Purchase.Owned;
        return Spend(price, () =>
        {
            var progress = Data.TrainerOf(trainer.Id);
            progress.Grade++;
            progress.Coins += price;
        });
    }

    public int RefundTrainer(TrainerDefinition trainer)
    {
        if (!Data.TrainerProgress.TryGetValue(trainer.Id, out var progress) || progress.Coins == 0)
            return 0;

        var coins = progress.Coins;
        Data.Coins += coins;
        Data.TrainerProgress.Remove(trainer.Id);
        Persist();
        return coins;
    }

    /// <summary>Buys an unlocked-in-the-shop trainer (none in trainers.json yet: they are beaten in challenges).</summary>
    public Purchase Buy(TrainerDefinition trainer)
    {
        if (IsUnlocked(trainer))
            return Purchase.Owned;
        if (trainer.Unlock.Kind != UnlockKind.Shop)
            return Purchase.Unavailable;
        return Spend(trainer.Unlock.Price, () => Data.Trainers.Add(trainer.Id));
    }

    /// <summary>
    /// The trainer as their grades make them: passive bonuses (and the boost to their type) stronger, the active
    /// ability's cooldown shorter, never shorter than its effect.
    /// </summary>
    public TrainerDefinition Upgraded(TrainerDefinition trainer) => Upgraded(trainer, TrainerGrade(trainer), Shop.Trainers);

    public static TrainerDefinition Upgraded(TrainerDefinition trainer, int grade, TrainerShopSpec rules)
    {
        if (grade <= 0)
            return trainer;

        var passive = 1f + rules.PassivePerGrade * grade;
        var cooldown = MathF.Max(trainer.Active.Duration + 1f, trainer.Active.Cooldown * (1f - rules.CooldownPerGrade * grade));
        return trainer with
        {
            Passive = trainer.Passive with { Modifiers = trainer.Passive.Modifiers.Scaled(passive), TypePower = trainer.Passive.TypePower * passive },
            Active = trainer.Active with { Cooldown = cooldown },
        };
    }

    // ---- Levels (Block 7) ----

    /// <summary>
    /// Whether a level can be played at a difficulty (phase 7.3). Levels open one after another, world after world, each
    /// once the one before is won; on a level, Difficile opens once it is won at Normale, Ultra once it is won at Difficile.
    /// </summary>
    public bool IsUnlocked(Run.Worlds.LevelInfo level, Difficulty difficulty = Difficulty.Normal) => difficulty switch
    {
        Difficulty.Normal => Worlds?.Previous(level) is not { } before || IsWon(before),
        Difficulty.Hard => IsWon(level, Difficulty.Normal),
        _ => IsWon(level, Difficulty.Hard),
    };

    /// <summary>Won at least once, at <paramref name="difficulty"/> or, when null, at any.</summary>
    public bool IsWon(Run.Worlds.LevelInfo level, Difficulty? difficulty = null) =>
        difficulty is { } d ? Data.WinsOn(level.Id, d) > 0 : Enum.GetValues<Difficulty>().Any(x => Data.WinsOn(level.Id, x) > 0);

    /// <summary>What can be played now, for "levels and difficulties opened" after a run.</summary>
    private HashSet<string> OpenLevels() =>
        Worlds is null ? [] : Worlds.Levels.SelectMany(l => Enum.GetValues<Difficulty>().Where(d => IsUnlocked(l, d)).Select(d => $"{l.Id}/{d}")).ToHashSet();

    /// <summary>
    /// The legendaries of a level that would come at Ultra (phase 7.3): those whose requirements are unlocked. Already
    /// beaten ones come again, for the fight.
    /// </summary>
    public IReadOnlyList<Run.Waves.LegendaryBoss> Legendaries(Run.Waves.WaveTimeline level) =>
        level.Legendaries.Where(l => l.Requires.All(id => Characters.Find(id) is { } c && IsUnlocked(c))).ToList();

    /// <summary>Characters whose fossil or egg is already found: theirs are not laid on the map again.</summary>
    public IReadOnlySet<string> FoundFinds() =>
        Characters.All.Where(c => c.Unlock.Kind is UnlockKind.Fossil or UnlockKind.Egg && (IsUnlocked(c) || Data.Fossils.Contains(c.Id))).Select(c => c.Id).ToHashSet();

    // ---- Upgrades ----

    public int UpgradeGrade(UpgradeSpec upgrade) => Data.Upgrades.GetValueOrDefault(upgrade.Id);

    public int? UpgradePrice(UpgradeSpec upgrade)
    {
        var grade = UpgradeGrade(upgrade);
        return grade >= upgrade.MaxGrade ? null : upgrade.Prices[grade];
    }

    /// <summary>Medals the next grade needs; 0 when none.</summary>
    public int MedalsNeeded(UpgradeSpec upgrade)
    {
        var grade = UpgradeGrade(upgrade);
        return grade < upgrade.Medals.Count ? upgrade.Medals[grade] : 0;
    }

    public Purchase BuyUpgrade(UpgradeSpec upgrade)
    {
        if (UpgradePrice(upgrade) is not { } price)
            return Purchase.Owned;
        if (Medals < MedalsNeeded(upgrade))
            return Purchase.NeedsMedals;
        return Spend(price, () =>
        {
            Data.Upgrades[upgrade.Id] = UpgradeGrade(upgrade) + 1;
            Data.UpgradeCoins += price;
        });
    }

    /// <summary>Gives back every coin spent on the shop's upgrades and clears them, as in Vampire Survivors.</summary>
    public int RefundUpgrades()
    {
        var coins = Data.UpgradeCoins;
        if (coins == 0)
            return 0;

        Data.Coins += coins;
        Data.Upgrades.Clear();
        Data.UpgradeCoins = 0;
        Persist();
        return coins;
    }

    private Purchase Spend(int price, Action apply)
    {
        if (Data.Coins < price)
            return Purchase.NotEnoughCoins;

        Data.Coins -= price;
        apply();
        Persist();
        return Purchase.Done;
    }

    // ---- The run ----

    /// <summary>What a run with <paramref name="character"/> gets from the meta-progression.</summary>
    public RunLoadout Loadout(CharacterDefinition character)
    {
        var bonus = GradeBonus(character);
        int slots = 0, choices = 0, rerolls = 0, skips = 0, banishes = 0;
        foreach (var upgrade in Shop.Upgrades)
        {
            var grade = UpgradeGrade(upgrade);
            switch (upgrade.Kind)
            {
                case UpgradeKind.MoveSlot: slots += grade; break;
                case UpgradeKind.Choices: choices += grade; break;
                case UpgradeKind.Rerolls: rerolls += grade; break;
                case UpgradeKind.Skips: skips += grade; break;
                case UpgradeKind.Banishes: banishes += grade; break;
                case UpgradeKind.Bonus: bonus += upgrade.PerGrade * grade; break;
            }
        }

        return new RunLoadout
        {
            SecondaryMove = HasSecondary(character),
            SpecialMove = HasSpecial(character),
            Bonus = bonus,
            MaxMoves = Run.Progression.LevelUp.MaxMoves + slots,
            Choices = RunState.LevelUpChoiceCount + choices,
            ExtraRerolls = rerolls,
            ExtraSkips = skips,
            ExtraBanishes = banishes,
        };
    }

    /// <summary>
    /// The end of a run: coins to the bank, totals, records and Pokédex, the trainer of a won challenge unlocked, then
    /// every mission now complete with its reward (a reward may complete another mission). Writes the save.
    /// </summary>
    public RunReport RecordRun(RunRecord run)
    {
        var seenBefore = Data.Pokedex.Count(e => e.Value.Seen > 0);
        var openBefore = OpenLevels();
        Data.Coins += run.Coins;

        var totals = Data.Totals;
        totals.Runs++;
        if (run.Won)
            totals.Victories++;
        totals.Kills += run.Kills;
        totals.Shinies += run.Shinies;
        totals.Dominants += run.Dominants;
        totals.Chests += run.Chests;
        totals.CoinsEarned += run.Earned;
        totals.Specials += run.Specials;
        totals.TrainerUses += run.TrainerUses;
        totals.MovesEvolved += run.MovesEvolved;
        totals.PlayTicks += run.Ticks;
        totals.BestLevel = Math.Max(totals.BestLevel, run.Level);
        totals.BestStageDepth = Math.Max(totals.BestStageDepth, run.StageDepth);
        totals.BestTicks = Math.Max(totals.BestTicks, run.Ticks);

        var level = Data.LevelOf(run.LevelId, run.Difficulty);
        level.Runs++;
        if (run.Won)
            level.Wins++;
        level.BestTicks = Math.Max(level.BestTicks, run.Ticks);
        level.BestKills = Math.Max(level.BestKills, run.Kills);
        if (run.Won && run.CharacterId is { } characterId)
            Data.CharacterWins[characterId] = Data.CharacterWins.GetValueOrDefault(characterId) + 1;

        foreach (var species in run.Seen)
            Data.EntryOf(species).Seen++;
        foreach (var (species, count) in run.KillsBySpecies)
            Data.EntryOf(species).Defeated += count;
        foreach (var move in run.Moves.Where(m => !Data.MovesFound.Contains(m)))
            Data.MovesFound.Add(move);
        foreach (var item in run.Items.Where(i => !Data.ItemsFound.Contains(i)))
            Data.ItemsFound.Add(item);
        Data.LastCharacter = run.CharacterId ?? Data.LastCharacter;
        Data.LastTrainer = run.TrainerId ?? Data.LastTrainer;
        if (Worlds?.Find(run.LevelId) is not null)
        {
            Data.LastLevel = run.LevelId;
            Data.LastDifficulty = run.Difficulty;
        }

        // What the levels gave (Block 7): a fossil waits in the shop to be revived, an egg hatches at once, a legendary
        // beaten at Ultra joins.
        var newCharacters = new List<CharacterDefinition>();
        var fossils = new List<CharacterDefinition>();
        foreach (var find in run.Finds.Distinct())
        {
            if (Characters.Find(find) is not { } found || IsUnlocked(found))
                continue;
            if (found.Unlock.Kind == UnlockKind.Fossil && !Data.Fossils.Contains(find))
            {
                Data.Fossils.Add(find);
                fossils.Add(found);
            }
            else if (found.Unlock.Kind == UnlockKind.Egg)
            {
                Data.Characters.Add(find);
                newCharacters.Add(found);
            }
        }

        foreach (var legendary in Characters.All.Where(c => c.Unlock.Kind == UnlockKind.Defeat && run.LegendariesDefeated.Contains(c.First.Id) && !IsUnlocked(c)))
        {
            Data.Characters.Add(legendary.Id);
            newCharacters.Add(legendary);
        }

        // Plates picked up on the map are kept, won or lost (phase 9.1).
        var newPlates = new List<Run.Plates.PlateDefinition>();
        foreach (var plateId in run.Plates.Distinct().Where(id => !Data.Plates.Contains(id)))
        {
            Data.Plates.Add(plateId);
            if (Plates?.Find(plateId) is { } plate)
                newPlates.Add(plate);
        }

        var newTrainers = new List<TrainerDefinition>();
        if (run.ChallengeWon is { } beaten && Trainers.Find(beaten) is { } trainer)
        {
            if (!Data.TrainersDefeated.Contains(beaten))
                Data.TrainersDefeated.Add(beaten);
            if (Unlock(trainer))
                newTrainers.Add(trainer);
        }

        // Missions, until no reward completes another one.
        var completed = new List<MissionSpec>();
        var missionCoins = 0;
        bool more;
        do
        {
            more = false;
            foreach (var mission in Missions.All)
            {
                if (IsDone(mission) || !Reached(mission, run, runSaved: true))
                    continue;

                Data.Missions.Add(mission.Id);
                completed.Add(mission);
                more = true;
                Data.Coins += mission.Reward.Coins;
                missionCoins += mission.Reward.Coins;
                if (mission.Reward.Character is { } c && Characters.Find(c) is { } character && !IsUnlocked(character))
                {
                    Data.Characters.Add(c);
                    newCharacters.Add(character);
                }

                if (mission.Reward.Trainer is { } t && Trainers.Find(t) is { } rewarded && Unlock(rewarded))
                    newTrainers.Add(rewarded);
            }
        }
        while (more);

        Persist();
        var newSpecies = Data.Pokedex.Count(e => e.Value.Seen > 0) - seenBefore;
        return new RunReport(run.Coins, missionCoins, Data.Coins, completed, newCharacters, newTrainers, newSpecies)
        {
            Fossils = fossils,
            Opened = OpenLevels().Except(openBefore).ToList(),
            Plates = newPlates,
        };
    }

    private bool Unlock(TrainerDefinition trainer)
    {
        if (IsUnlocked(trainer))
            return false;
        Data.Trainers.Add(trainer.Id);
        return true;
    }

    // ---- Missions ----

    public bool IsDone(MissionSpec mission) => Data.Missions.Contains(mission.Id);

    /// <summary>
    /// Missions the run in progress has just completed, for the notice during the run; their rewards come when the run
    /// is recorded. Missions of another level or difficulty never count.
    /// </summary>
    public IReadOnlyList<MissionSpec> ReachedDuring(RunRecord run) =>
        Missions.All.Where(m => !IsDone(m) && Reached(m, run, runSaved: false)).ToList();

    private bool Reached(MissionSpec mission, RunRecord run, bool runSaved)
    {
        if (mission.Level is { } levelId && (levelId != run.LevelId || mission.Difficulty != run.Difficulty))
            return false;
        var (current, target) = Progress(mission, run, runSaved);
        return current >= target;
    }

    /// <summary>
    /// How far a mission is: a count towards its target. <paramref name="run"/> is a run not yet in the save (in progress),
    /// counted on top of the totals unless <paramref name="runSaved"/>; one-run missions look only at it (or at the record).
    /// </summary>
    public (int Current, int Target) Progress(MissionSpec mission, RunRecord? run = null, bool runSaved = false)
    {
        var live = run is not null && !runSaved ? run : null;
        var totals = Data.Totals;
        var target = mission.Count;

        int Count(long saved, int inRun) => (int)Math.Min(int.MaxValue, mission.OneRun ? inRun : saved + (live is null ? 0 : inRun));

        switch (mission.Kind)
        {
            case MissionKind.Runs:
                return (totals.Runs + (live is { Over: true } ? 1 : 0), target);

            case MissionKind.Survive:
                var minutes = run is null ? (mission.NoHealing ? 0 : totals.BestTicks / (RunClock.TicksPerSecond * 60))
                    : mission.NoHealing && run.HealingPickups > 0 ? 0 : run.Minutes;
                return (minutes, target);

            case MissionKind.WinLevel:
                var record = Data.Levels.GetValueOrDefault($"{mission.Level}/{mission.Difficulty}");
                var won = (record?.Wins ?? 0) + (live is { Won: true } && live.LevelId == mission.Level && live.Difficulty == mission.Difficulty ? 1 : 0);
                return (Math.Min(won, target), target);

            case MissionKind.WinWith:
                var wins = Data.CharacterWins.GetValueOrDefault(mission.Character!) + (live is { Won: true } && live.CharacterId == mission.Character ? 1 : 0);
                return (Math.Min(wins, target), target);

            case MissionKind.Defeat:
                bool OfType(int species) => mission.Type is not { } type || Pokedex[species - 1].Types.Contains(type);
                var savedKills = mission.Type is null ? totals.Kills : Data.Pokedex.Where(e => OfType(e.Key)).Sum(e => (long)e.Value.Defeated);
                var runKills = run?.KillsBySpecies.Where(kv => OfType(kv.Key)).Sum(kv => kv.Value) ?? 0;
                return (mission.OneRun ? (run is null ? 0 : runKills) : Count(savedKills, runKills), target);

            case MissionKind.DefeatShiny:
                return (Count(totals.Shinies, run?.Shinies ?? 0), target);
            case MissionKind.DefeatDominant:
                return (Count(totals.Dominants, run?.Dominants ?? 0), target);
            case MissionKind.OpenChests:
                return (Count(totals.Chests, run?.Chests ?? 0), target);
            case MissionKind.UseSpecial:
                return (Count(totals.Specials, run?.Specials ?? 0), target);
            case MissionKind.UseTrainer:
                return (Count(totals.TrainerUses, run?.TrainerUses ?? 0), target);
            case MissionKind.EvolveMove:
                return (Count(totals.MovesEvolved, run?.MovesEvolved ?? 0), target);
            case MissionKind.CollectCoins:
                return (Count(totals.CoinsEarned, run?.Earned ?? 0), target);

            case MissionKind.ReachLevel:
                return (Math.Max(totals.BestLevel, run?.Level ?? 0), target);
            case MissionKind.EvolvePokemon:
                return (Math.Max(totals.BestStageDepth, run?.StageDepth ?? 0), target);

            case MissionKind.DefeatTrainer:
                var defeated = Data.TrainersDefeated.ToHashSet();
                if (live?.ChallengeWon is { } beaten)
                    defeated.Add(beaten);
                if (mission.Trainer is { } trainerId)
                    return (defeated.Contains(trainerId) ? 1 : 0, 1);
                var matching = defeated.Count(id => Trainers.Find(id) is { } t && t.Role == mission.Role && (mission.Region is null || t.Region == mission.Region));
                return (matching, target);

            case MissionKind.PokedexSeen:
                var seen = Data.Pokedex.Where(e => e.Value.Seen > 0).Select(e => e.Key).ToHashSet();
                if (live is not null)
                    seen.UnionWith(live.Seen);
                if (mission.Region is { } region)
                {
                    var (from, to) = RegionRange(region);
                    return (seen.Count(id => id >= from && id <= to), mission.Count > 0 ? mission.Count : to - from + 1);
                }

                return (seen.Count, target);

            case MissionKind.PokedexDefeated:
                var species = Data.Pokedex.Where(e => e.Value.Defeated > 0).Select(e => e.Key).ToHashSet();
                if (live is not null)
                    species.UnionWith(live.KillsBySpecies.Keys);
                return (species.Count, target);

            case MissionKind.UnlockCharacters:
                var group = Characters.All.Where(c => mission.Groups.Contains(c.Group) && c.Id != mission.Reward.Character).ToList();
                return (group.Count(IsUnlocked), group.Count);

            case MissionKind.CompleteRegion:
                // Every level of the region won, at any difficulty (Block 7).
                var levels = Worlds?.Worlds.FirstOrDefault(w => w.Region == mission.Region)?.Levels ?? [];
                var done = levels.Count(l => IsWon(l) || live is { Won: true } && live.LevelId == l.Id);
                return (done, Math.Max(1, levels.Count));

            default:
                return (0, 1);
        }
    }

    /// <summary>National Pokédex numbers of a region's species.</summary>
    public static (int From, int To) RegionRange(TrainerRegion region) => region switch
    {
        TrainerRegion.Kanto => (1, 151),
        TrainerRegion.Johto => (152, 251),
        TrainerRegion.Hoenn => (252, 386),
        _ => (387, 493),
    };
}
