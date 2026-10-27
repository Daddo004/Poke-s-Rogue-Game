using System.Globalization;
using System.Numerics;
using System.Text;
using PokeRogue.Core.Data;
using PokeRogue.Core.Run;
using PokeRogue.Core.Run.Enemies;
using PokeRogue.Core.Run.Items;
using PokeRogue.Core.Run.Moves;
using PokeRogue.Core.Run.Pickups;
using PokeRogue.Core.Run.Progression;
using PokeRogue.Core.Run.Statuses;
using PokeRogue.Core.Run.Waves;

namespace PokeRogue.DataTool.Balance;

/// <summary>
/// Milestone B: many headless 10-minute prototype runs played by <see cref="AutoPilot"/>, summarised as a Markdown
/// report in Italian: who deals the damage, how varied the builds are, how often evolutions are reached.
/// It only reads the game data; it never changes it.
/// </summary>
public sealed class BalanceSimulation(string contentRoot, bool weightedCards = true)
{
    private const int TicksPerMinute = RunClock.TicksPerClockMinute;

    /// <summary>The level GameplayScene plays until worlds arrive in Block 7.</summary>
    private const string LevelId = "bosco-smeraldo";

    /// <summary>The project runs with invariant globalization: Italian decimal commas are put in by hand.</summary>
    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    private readonly PokedexFile _pokedex = GameDataFiles.LoadPokedex(contentRoot);
    private readonly TypeChart _typeChart = new(GameDataFiles.LoadTypeChart(contentRoot).Matchups);
    private readonly MoveLibrary _moves = MoveLibrary.Load(contentRoot);
    private readonly ItemLibrary _items = ItemLibrary.Load(contentRoot);
    private readonly StatusLibrary _statuses = StatusLibrary.Load(contentRoot);
    private readonly LevelUpRules? _levelUpRules = weightedCards ? LevelUpRules.Load(contentRoot) : null;
    private readonly string _contentRoot = contentRoot;
    private readonly LootRules _loot = LootRules.Load(contentRoot);
    private WaveTimeline? _waves;
    private EnemyLibrary? _enemies;

    private WaveTimeline Waves => _waves ??= WaveTimeline.Load(_contentRoot, LevelId, _pokedex.Species, _moves);

    private EnemyLibrary Enemies => _enemies ??= EnemyLibrary.Load(_contentRoot, _pokedex.Species, _moves);

    public sealed record MoveResult(string Id, string Name, int Level, int MaxLevel, float Damage, bool Evolved)
    {
        public bool Maxed => !Evolved && Level >= MaxLevel;
    }

    /// <summary>The state of a run at the end of one minute.</summary>
    public sealed record MinuteSample(int Alive, int Kills, int Level, float Hp);

    /// <summary>A Dominante of the run: when it arrived and how many seconds it took to beat (null if it never fell).</summary>
    public sealed record DominantResult(string Name, int Minute, float? Seconds);

    public sealed record RunResult(int Seed, int Level, int Kills, int GemsLeft, IReadOnlyList<MoveResult> Moves, IReadOnlyList<string> Items,
        IReadOnlyList<(string Name, int Minute)> Evolutions, IReadOnlyList<MinuteSample> Minutes, RunOutcome Outcome, int Ticks,
        IReadOnlyList<DominantResult> Dominants, float? BossSeconds)
    {
        public float TotalDamage => Moves.Sum(m => m.Damage);
    }

    /// <summary>
    /// One run: <paramref name="focused"/> picks cards like a player building towards evolutions, otherwise the first (random) card.
    /// A <paramref name="mortal"/> autopilot takes damage and flees crowds, to see how far the waves let it go.
    /// </summary>
    public RunResult Play(int seed, bool focused, bool mortal = false)
    {
        var run = new RunState(_pokedex.Species, _typeChart, 25, Waves, new Vector2(640, 360), seed)
        {
            TimeLimitTicks = Waves.DurationTicks,
            MovePool = _moves.Enabled,
            ItemPool = _items.Enabled,
            LevelUpRules = _levelUpRules,
            StatusRules = _statuses,
            Evolutions = _moves.Evolutions,
            EnemyRules = Enemies,
            LootRules = _loot.File.Enabled ? _loot : null,
        };
        run.Player.Moves.Add(new MoveInstance(_moves["thundershock"]));
        run.Player.Invincible = !mortal;

        var evolutions = new List<(string, int)>();
        var minutes = new List<MinuteSample>();
        var arrivals = new List<(int SpeciesId, int Tick)>();
        var dominants = new List<DominantResult>();
        int? bossArrived = null;
        float? bossSeconds = null;

        // Ten minutes past the time limit at most, in case the boss outlasts the pilot.
        while (!run.IsOver && run.Ticks < Waves.DurationTicks + TicksPerMinute * 10)
        {
            if (run.IsPaused)
            {
                run.ChooseLevelUp(focused ? AutoPilot.ChooseLevelUp(run) : 0);
                continue;
            }

            run.Tick(new PlayerInput(AutoPilot.Input(run)));
            foreach (var e in run.Events)
            {
                if (e.Type == RunEventType.MoveEvolved && e.MoveId is { } id)
                    evolutions.Add((_moves[id].NameIt, run.Ticks / TicksPerMinute));
                if (e.Type == RunEventType.DominantAppeared)
                    arrivals.Add((e.SpeciesId, run.Ticks));
                if (e.Type == RunEventType.BossAppeared)
                    bossArrived = run.Ticks;
                if (e.Type == RunEventType.BossDefeated && bossArrived is { } arrived)
                    bossSeconds = (run.Ticks - arrived) / (float)RunClock.TicksPerSecond;
                if (e.Type == RunEventType.ChestDropped && arrivals.FindIndex(a => a.SpeciesId == e.SpeciesId) is var found and >= 0)
                {
                    var (speciesId, tick) = arrivals[found];
                    arrivals.RemoveAt(found);
                    dominants.Add(new DominantResult(_pokedex.Species[speciesId - 1].NameIt, tick / TicksPerMinute, (run.Ticks - tick) / (float)RunClock.TicksPerSecond));
                }
            }

            if (run.Ticks % TicksPerMinute == 0)
                minutes.Add(new MinuteSample(run.Enemies.AliveCount, run.Stats.Kills, run.Player.Level, run.Player.Hp / run.Player.MaxHp));
        }

        var gemsLeft = run.Pickups.Gems.Items.ToArray().Where(g => g.Active).Sum(g => g.Value);
        return new RunResult(seed, run.Player.Level, run.Stats.Kills, gemsLeft,
            run.Player.Moves.Select(m => new MoveResult(m.Definition.Id, m.Definition.NameIt, m.Level, m.Definition.MaxLevel, m.DamageDealt, m.Definition.IsEvolution)).ToList(),
            run.Player.Items.Select(i => i.Definition.NameIt).ToList(),
            evolutions, minutes, run.Outcome, run.Ticks,
            dominants.Concat(arrivals.Select(a => new DominantResult(_pokedex.Species[a.SpeciesId - 1].NameIt, a.Tick / TicksPerMinute, null))).ToList(),
            bossSeconds);
    }

    /// <summary>Plays <paramref name="runs"/> runs with each way of picking cards and writes the report.</summary>
    public string Report(int runs, Action<string> progress)
    {
        var random = new List<RunResult>();
        var focused = new List<RunResult>();
        var mortal = new List<RunResult>();
        for (var seed = 1; seed <= runs; seed++)
        {
            random.Add(Play(seed, focused: false));
            focused.Add(Play(seed, focused: true));
            mortal.Add(Play(seed, focused: true, mortal: true));
            progress($"partita {seed}/{runs}");
        }

        var minutes = Waves.DurationTicks / TicksPerMinute;
        var text = new StringBuilder();
        text.AppendLine($"# Simulazione di bilanciamento ({Waves.NameIt})");
        text.AppendLine();
        text.AppendLine($"Generata il {DateTime.Now.ToString("dd/MM/yyyy HH:mm", Invariant)} con `dotnet run --project tools/PokeRogue.DataTool -c Release -- bilancio --runs {runs}{(weightedCards ? "" : " --senza-pesi")}`.");
        text.AppendLine();
        if (_levelUpRules is { Shares: var s })
        {
            text.AppendLine($"Carte del level-up pesate (content/data/levelup.json): potenziamenti di mosse {Percent(s.MoveUpgrade)}, di strumenti {Percent(s.ItemUpgrade)}, " +
                $"mosse nuove {Percent(s.NewMove)}, strumenti nuovi {Percent(s.NewItem)}.");
        }
        else
        {
            text.AppendLine("Carte del level-up tutte con la stessa probabilità (come prima della fase 2.7).");
        }

        text.AppendLine();
        text.AppendLine($"{runs} partite da {minutes} minuti per modo di scegliere, sulle ondate di content/data/waves/{LevelId}.json, giocate dal pilota automatico " +
            "(Pikachu invincibile: va a prendere le gemme anche in mezzo ai nemici e lascia lavorare le mosse), con tutte le mosse, gli strumenti e le evoluzioni approvate. " +
            "Il pilota non è un giocatore vero: i numeri servono a confrontare mosse e strumenti tra loro, non a misurare la difficoltà.");
        text.AppendLine();
        text.AppendLine("- **Scelta casuale**: a ogni livello prende la prima carta (le carte sono in ordine casuale; un'evoluzione pronta è sempre la prima).");
        text.AppendLine("- **Scelta mirata**: prende un'evoluzione, altrimenti potenzia la mossa più avanti, altrimenti lo strumento che fa evolvere una sua mossa (`AutoPilot.ChooseLevelUp`).");
        text.AppendLine();

        WavesSection(text, focused, mortal);
        Section(text, "Scelta casuale", random);
        Section(text, "Scelta mirata", focused);
        return text.ToString();
    }

    /// <summary>How the waves build up minute by minute, and how far a pilot that can be hurt gets.</summary>
    private static void WavesSection(StringBuilder text, List<RunResult> focused, List<RunResult> mortal)
    {
        text.AppendLine("## Ondate");
        text.AppendLine();
        var survived = mortal.Count(r => r.Outcome == RunOutcome.Victory);
        var fallen = mortal.Where(r => r.Outcome == RunOutcome.Defeat).ToList();
        text.AppendLine($"- Pilota che subisce danni (scelta mirata, scappa dalla folla): arriva alla fine in {survived} partite su {mortal.Count}" +
            (fallen.Count > 0 ? $"; le altre finiscono al minuto {Median(fallen.Select(r => r.Ticks / TicksPerMinute))} (mediana, minimo {fallen.Min(r => r.Ticks / TicksPerMinute)})." : "."));
        text.AppendLine();
        text.AppendLine("| Minuto | Nemici vivi | KO totali | Livello | Pilota che subisce danni: ancora in piedi | PS (mediana) |");
        text.AppendLine("|---:|---:|---:|---:|---:|---:|");
        var count = focused.Max(r => r.Minutes.Count);
        for (var m = 0; m < count; m++)
        {
            var samples = focused.Where(r => r.Minutes.Count > m).Select(r => r.Minutes[m]).ToList();
            var standing = mortal.Where(r => r.Minutes.Count > m && r.Minutes[m].Hp > 0f).Select(r => r.Minutes[m]).ToList();
            var hp = standing.Count > 0 ? $"{Median(standing.Select(s => (int)MathF.Round(s.Hp * 100f)))}%" : "–";
            text.AppendLine($"| {m + 1} | {Median(samples.Select(s => s.Alive))} | {Median(samples.Select(s => s.Kills))} | {Median(samples.Select(s => s.Level))} | {standing.Count} | {hp} |");
        }

        text.AppendLine();
        var bossTimes = focused.Where(r => r.BossSeconds is not null).Select(r => (int)MathF.Round(r.BossSeconds!.Value)).ToList();
        var mortalBoss = mortal.Count(r => r.BossSeconds is not null);
        text.AppendLine(bossTimes.Count == 0
            ? "- Boss: mai battuto dal pilota invincibile."
            : $"- Boss: battuto dal pilota invincibile in {bossTimes.Count} partite su {focused.Count}, in {Median(bossTimes)} secondi (mediana, massimo {bossTimes.Max()}); " +
              $"dal pilota che subisce danni in {mortalBoss} partite su {mortal.Count}.");
        text.AppendLine();
        var dominants = focused.SelectMany(r => r.Dominants).GroupBy(d => (d.Minute, d.Name)).OrderBy(g => g.Key.Minute).ToList();
        if (dominants.Count == 0)
            return;

        text.AppendLine("Dominanti (scelta mirata): quanto ci vuole a batterli da quando arrivano, compresa la strada per raggiungerli.");
        text.AppendLine();
        text.AppendLine("| Minuto | Dominante | Battuto in | Secondi (mediana) | Secondi (massimo) |");
        text.AppendLine("|---:|---|---:|---:|---:|");
        foreach (var group in dominants)
        {
            var times = group.Where(d => d.Seconds is not null).Select(d => (int)MathF.Round(d.Seconds!.Value)).ToList();
            text.AppendLine($"| {group.Key.Minute} | {group.Key.Name} | {times.Count} su {group.Count()} | {(times.Count > 0 ? Median(times).ToString(Invariant) : "–")} | {(times.Count > 0 ? times.Max().ToString(Invariant) : "–")} |");
        }

        text.AppendLine();
    }

    private static void Section(StringBuilder text, string title, List<RunResult> runs)
    {
        var levels = runs.Select(r => r.Level).Order().ToList();
        text.AppendLine($"## {title}");
        text.AppendLine();
        text.AppendLine($"- Livello finale: minimo {levels[0]}, mediana {levels[levels.Count / 2]}, massimo {levels[^1]}.");
        text.AppendLine($"- KO per partita: mediana {Median(runs.Select(r => r.Kills))}.");
        text.AppendLine($"- Esperienza lasciata a terra alla fine: mediana {Median(runs.Select(r => r.GemsLeft))}.");
        text.AppendLine($"- Mosse a fine partita: mediana {Median(runs.Select(r => r.Moves.Count))}; al livello massimo (o evolute) mediana " +
            $"{Median(runs.Select(r => r.Moves.Count(m => m.Maxed || m.Evolved)))}, almeno una in {runs.Count(r => r.Moves.Any(m => m.Maxed || m.Evolved))} partite.");
        text.AppendLine($"- Strumenti a fine partita: mediana {Median(runs.Select(r => r.Items.Count))}.");
        var evolved = runs.Where(r => r.Evolutions.Count > 0).ToList();
        text.AppendLine($"- Partite con almeno un'evoluzione: {evolved.Count} su {runs.Count}" +
            (evolved.Count > 0 ? $", la prima al minuto {Median(evolved.Select(r => r.Evolutions.Min(e => e.Minute)))} (mediana)." : "."));
        var topPairs = runs.Select(r => string.Join(" + ", r.Moves.OrderByDescending(m => m.Damage).Take(2).Select(m => m.Name).Order())).Distinct().Count();
        text.AppendLine($"- Coppie diverse di mosse più forti (le due con più danni): {topPairs} su {runs.Count} partite.");
        text.AppendLine();

        text.AppendLine("| Mossa | Presa in | Livello medio | Quota dei danni quando presa | Prima per danni |");
        text.AppendLine("|---|---:|---:|---:|---:|");
        var rows = runs.SelectMany(r => r.Moves.Select(m => (Run: r, Move: m)))
            .GroupBy(x => x.Move.Name)
            .Select(g => (
                Name: g.Key,
                Evolved: g.First().Move.Evolved,
                Count: g.Count(),
                Level: g.Average(x => x.Move.Level),
                Share: g.Average(x => x.Run.TotalDamage > 0 ? x.Move.Damage / x.Run.TotalDamage : 0f),
                Top: g.Count(x => x.Run.Moves.MaxBy(m => m.Damage) == x.Move)))
            .OrderByDescending(r => r.Share);
        foreach (var row in rows)
        {
            var name = row.Evolved ? $"{row.Name} (evoluta)" : row.Name;
            text.AppendLine($"| {name} | {row.Count} | {row.Level.ToString("0.0", Invariant).Replace('.', ',')} | {MathF.Round(row.Share * 100f).ToString(Invariant)}% | {row.Top} |");
        }

        text.AppendLine();
        text.AppendLine("| Strumento | Preso in |");
        text.AppendLine("|---|---:|");
        foreach (var item in runs.SelectMany(r => r.Items).GroupBy(i => i).OrderByDescending(g => g.Count()))
            text.AppendLine($"| {item.Key} | {item.Count()} |");

        var reached = runs.SelectMany(r => r.Evolutions).GroupBy(e => e.Name).OrderByDescending(g => g.Count()).ToList();
        if (reached.Count > 0)
        {
            text.AppendLine();
            text.AppendLine("| Evoluzione ottenuta | Partite | Minuto (mediana) |");
            text.AppendLine("|---|---:|---:|");
            foreach (var evolution in reached)
                text.AppendLine($"| {evolution.Key} | {evolution.Count()} | {Median(evolution.Select(e => e.Minute))} |");
        }

        text.AppendLine();
    }

    private static string Percent(float share) => $"{MathF.Round(share * 100f).ToString(Invariant)}%";

    private static int Median(IEnumerable<int> values)
    {
        var sorted = values.Order().ToList();
        return sorted.Count == 0 ? 0 : sorted[sorted.Count / 2];
    }
}
