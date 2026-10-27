using System.Collections.Concurrent;
using System.Globalization;
using System.Numerics;
using System.Text;
using PokeRogue.Core.Data;
using PokeRogue.Core.Run;
using PokeRogue.Core.Run.Abilities;
using PokeRogue.Core.Run.Characters;
using PokeRogue.Core.Run.Enemies;
using PokeRogue.Core.Run.Items;
using PokeRogue.Core.Run.Moves;
using PokeRogue.Core.Run.Pickups;
using PokeRogue.Core.Run.Progression;
using PokeRogue.Core.Run.Specials;
using PokeRogue.Core.Run.Statuses;
using PokeRogue.Core.Run.Trainers;
using PokeRogue.Core.Run.Waves;

namespace PokeRogue.DataTool.Balance;

/// <summary>
/// Phase 4.5: every playable character plays the level headless, as the game would play it: it evolves when it can
/// (taking the first choice), uses its active ability and its special as soon as they are ready. Each character plays
/// the same seeds twice, with an invincible pilot (how strong its build gets) and with one that can be hurt and flees
/// crowds (how long it lasts). The report compares the characters; it never changes the data.
/// </summary>
public sealed class RosterSimulation
{
    private const int TicksPerMinute = RunClock.TicksPerClockMinute;
    private const string LevelId = "bosco-smeraldo";
    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    private readonly PokedexFile _pokedex;
    private readonly TypeChart _typeChart;
    private readonly MoveLibrary _moves;
    private readonly ItemLibrary _items;
    private readonly StatusLibrary _statuses;
    private readonly LevelUpRules _levelUpRules;
    private readonly LootRules _loot;
    private readonly WaveTimeline _waves;
    private readonly EnemyLibrary _enemies;
    private readonly SpecialMoves _specials;
    private readonly CharacterLibrary _characters;

    public RosterSimulation(string contentRoot)
    {
        _pokedex = GameDataFiles.LoadPokedex(contentRoot);
        _typeChart = new TypeChart(GameDataFiles.LoadTypeChart(contentRoot).Matchups);
        _moves = MoveLibrary.Load(contentRoot);
        _items = ItemLibrary.Load(contentRoot);
        _statuses = StatusLibrary.Load(contentRoot);
        _levelUpRules = LevelUpRules.Load(contentRoot);
        _loot = LootRules.Load(contentRoot);
        _waves = WaveTimeline.Load(contentRoot, LevelId, _pokedex.Species, _moves);
        _enemies = EnemyLibrary.Load(contentRoot, _pokedex.Species, _moves);
        _specials = SpecialMoves.Load(contentRoot);
        _characters = CharacterLibrary.Load(contentRoot, _pokedex.Species, _moves);
        Trainers = TrainerLibrary.Load(contentRoot, _pokedex.Species, _moves);
    }

    public sealed record RunResult(int Level, int Kills, RunOutcome Outcome, int Ticks, float? BossSeconds, string FinalForm,
        IReadOnlyList<(string Name, int Minute)> Evolutions, int Specials, float SpecialShare, int TrainerUses = 0)
    {
        /// <summary>What the run did, for the meta-progression (Block 6).</summary>
        public PokeRogue.Core.Meta.RunRecord? Record { get; init; }

        /// <summary>The endless mode (phase 9.3): whether Darkrai came, and how long it lasted when beaten.</summary>
        public bool ReaperCame { get; init; }

        public float? ReaperSeconds { get; init; }
    }

    /// <summary>The trainers (phase 5.1); a run plays with Ash, as the game does, unless another one or none is given.</summary>
    public TrainerLibrary Trainers { get; }

    public CharacterLibrary Characters => _characters;

    /// <summary>A species' Italian name.</summary>
    public string Species(int id) => _pokedex.Species[id - 1].NameIt;

    /// <param name="trainer">The run's trainer: Ash when omitted, none with <paramref name="noTrainer"/>.</param>
    /// <param name="challenge">A challenge run (phase 5.3): this trainer's team replaces the level's boss, and the boss time is the whole team's.</param>
    /// <param name="level">Block 7: another level (built for its difficulty) instead of Bosco Smeraldo; at Ultra its legendaries come instead of the boss.</param>
    public RunResult Play(CharacterDefinition character, int seed, bool mortal, TrainerDefinition? trainer = null, bool noTrainer = false,
        TrainerDefinition? challenge = null, RunLoadout? loadout = null, WaveTimeline? level = null,
        PokeRogue.Core.Meta.Difficulty difficulty = PokeRogue.Core.Meta.Difficulty.Normal, PokeRogue.Core.Run.Worlds.EndlessRules? endless = null)
    {
        var waves = level ?? _waves;
        var run = new RunState(_pokedex.Species, _typeChart, character.First.Id, waves, new Vector2(640, 360), seed)
        {
            Difficulty = difficulty,
            EndlessRules = endless,
            TimeLimitTicks = waves.DurationTicks,
            MovePool = _moves.Enabled,
            ItemPool = _items.Enabled,
            LevelUpRules = _levelUpRules,
            StatusRules = _statuses,
            Evolutions = _moves.Evolutions,
            EnemyRules = _enemies,
            LootRules = _loot.File.Enabled ? _loot : null,
            SpecialRules = _specials,
        };
        run.UseCharacter(character, loadout);
        if (!noTrainer)
            run.UseTrainer(trainer ?? Trainers[TrainerLibrary.DefaultId]);
        if (challenge is not null)
            run.ChallengeTrainer(challenge, Trainers.Challenge!);
        else if (difficulty == PokeRogue.Core.Meta.Difficulty.Ultra)
            run.UseLegendaries(waves.Legendaries.Where(l => l.Requires.Count == 0).Select(l => l.Boss));
        run.Player.Invincible = !mortal;

        var evolutions = new List<(string, int)>();
        int? bossArrived = null;
        float? bossSeconds = null;
        int? reaperArrived = null;
        float? reaperSeconds = null;
        var limit = (endless?.ReaperAtTicks ?? waves.DurationTicks) + TicksPerMinute * 10;
        while (!run.IsOver && run.Ticks < limit)
        {
            if (run.SpecialPending)
            {
                run.UnleashSpecial();
                continue;
            }

            if (run.IsPaused)
            {
                run.ChooseLevelUp(AutoPilot.ChooseLevelUp(run));
                continue;
            }

            if (CharacterEvolution.Ready(run) is [var stage, ..])
            {
                CharacterEvolution.Evolve(run, stage);
                evolutions.Add((stage.Species.NameIt, run.Ticks / TicksPerMinute));
            }

            run.Tick(new PlayerInput(AutoPilot.Input(run), AbilitySystem.CanUse(run), run.Special.IsFull, AutoPilot.UseTrainer(run)));
            foreach (var e in run.Events)
            {
                // The first boss (or trainer) to arrive starts the clock; the last legendary's fall stops it.
                if (e.Type is RunEventType.BossAppeared or RunEventType.TrainerChallenged)
                    bossArrived ??= run.Ticks;
                if ((e.Type == RunEventType.BossDefeated && challenge is null && run.LegendariesLeft == 0 || e.Type == RunEventType.TrainerDefeated) && bossArrived is { } arrived)
                    bossSeconds ??= (run.Ticks - arrived) / (float)RunClock.TicksPerSecond;

                // The endless mode's Reaper (phase 9.3), timed on its own.
                if (e.Type == RunEventType.BossAppeared && run.Boss is { IsReaper: true })
                    reaperArrived ??= run.Ticks;
                if (e.Type == RunEventType.BossDefeated && run.Boss is { IsReaper: true } && reaperArrived is { } came)
                    reaperSeconds = (run.Ticks - came) / (float)RunClock.TicksPerSecond;
            }
        }

        var moves = run.Player.Moves.Sum(m => m.DamageDealt);
        var total = moves + run.Special.DamageDealt;
        return new RunResult(run.Player.Level, run.Stats.Kills, run.Outcome, run.Ticks, bossSeconds, run.Player.Species.NameIt, evolutions,
            run.Special.Uses, total > 0f ? run.Special.DamageDealt / total : 0f, run.TrainerState.Uses)
        {
            Record = PokeRogue.Core.Meta.RunRecord.From(run, waves.Id),
            ReaperSeconds = reaperSeconds,
            ReaperCame = run.ReaperCame,
        };
    }

    public string Report(int runs, IReadOnlyCollection<string>? only, Action<string> progress)
    {
        var characters = _characters.All.Where(c => only is null || only.Contains(c.Id)).ToList();
        var results = new ConcurrentDictionary<string, (List<RunResult> Strong, List<RunResult> Mortal)>();
        var done = 0;
        Parallel.ForEach(characters, new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount }, character =>
        {
            var strong = new List<RunResult>();
            var mortal = new List<RunResult>();
            for (var seed = 1; seed <= runs; seed++)
            {
                try
                {
                    strong.Add(Play(character, seed, mortal: false));
                    mortal.Add(Play(character, seed, mortal: true));
                }
                catch (Exception exception)
                {
                    throw new InvalidOperationException($"{character.Id}, seed {seed}: {exception.Message}", exception);
                }
            }

            results[character.Id] = (strong, mortal);
            progress($"{Interlocked.Increment(ref done)}/{characters.Count} {character.NameIt}");
        });

        var rows = characters.Select(c => (Character: c, results[c.Id].Strong, results[c.Id].Mortal)).ToList();
        var medianKills = Median(rows.Select(r => Median(r.Strong.Select(x => x.Kills))));
        var medianSurvival = Median(rows.Select(r => Median(r.Mortal.Select(Survival))));

        var text = new StringBuilder();
        text.AppendLine($"# Simulazione dei personaggi ({_waves.NameIt})");
        text.AppendLine();
        text.AppendLine($"Generata il {DateTime.Now.ToString("dd/MM/yyyy HH:mm", Invariant)} con `dotnet run --project tools/PokeRogue.DataTool -c Release -- personaggi --runs {runs}`.");
        text.AppendLine();
        text.AppendLine($"Ogni personaggio gioca {runs} partite col pilota automatico invincibile (quanto diventa forte) e le stesse {runs} con un pilota che subisce " +
            "danni e scappa dalla folla (quanto resiste). Evolve appena può (alla prima scelta), usa l'abilità attiva e la speciale appena sono pronte, " +
            "sceglie le carte puntando alle evoluzioni delle mosse. Il pilota non è un giocatore vero: i numeri servono a confrontare i personaggi tra loro.");
        text.AppendLine();
        text.AppendLine($"- KO in una partita (pilota invincibile, mediana dei personaggi): {medianKills}. Minuto di resistenza (pilota che subisce danni): {medianSurvival}.");
        text.AppendLine("- **Forte**: KO almeno 1,4 volte la mediana o resistenza almeno 3 minuti sopra. **Debole**: KO sotto 0,6 volte la mediana o resistenza almeno 3 minuti sotto.");
        text.AppendLine();
        text.AppendLine("| Personaggio | Gruppo | Forma finale | Evoluzioni (minuto) | Livello | KO | Boss (s) | Speciali | Danni speciale | Resiste fino al minuto | Vince | Giudizio |");
        text.AppendLine("|---|---|---|---|---:|---:|---:|---:|---:|---:|---:|---|");
        foreach (var (character, strong, mortal) in rows.OrderByDescending(r => Median(r.Strong.Select(x => x.Kills))))
        {
            var kills = Median(strong.Select(r => r.Kills));
            var survival = Median(mortal.Select(Survival));
            var verdict = kills >= medianKills * 1.4f || survival >= medianSurvival + 3 ? "forte"
                : kills <= medianKills * 0.6f || survival <= medianSurvival - 3 ? "debole" : "";
            var evolutions = strong.SelectMany(r => r.Evolutions).GroupBy(e => e.Name).Select(g => $"{g.Key} {Median(g.Select(e => e.Minute))}'");
            var boss = strong.Where(r => r.BossSeconds is not null).Select(r => (int)r.BossSeconds!.Value).ToList();
            text.AppendLine($"| {character.NameIt} | {GroupName(character.Group)} | {Mode(strong.Select(r => r.FinalForm))} | {string.Join(", ", evolutions)} | " +
                $"{Median(strong.Select(r => r.Level))} | {kills} | {(boss.Count > 0 ? Median(boss).ToString(Invariant) : "–")} | {Median(strong.Select(r => r.Specials))} | " +
                $"{MathF.Round(strong.Average(r => r.SpecialShare) * 100f).ToString(Invariant)}% | {survival} | {mortal.Count(r => r.Outcome == RunOutcome.Victory)}/{runs} | {verdict} |");
        }

        return text.ToString();
    }

    public static int Survival(RunResult r) => r.Ticks / TicksPerMinute;

    private static string GroupName(CharacterGroup group) => group switch
    {
        CharacterGroup.Starter => "Starter",
        CharacterGroup.Fossil => "Fossile",
        CharacterGroup.Legendary => "Leggendario",
        CharacterGroup.Mythical => "Misterioso",
        _ => "Lista",
    };

    private static string Mode(IEnumerable<string> values) => values.GroupBy(v => v).MaxBy(g => g.Count())?.Key ?? "";

    public static int Median(IEnumerable<int> values)
    {
        var sorted = values.Order().ToList();
        return sorted.Count == 0 ? 0 : sorted[sorted.Count / 2];
    }
}
