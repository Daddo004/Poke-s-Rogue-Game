using System.Globalization;
using System.Numerics;
using System.Text;
using PokeRogue.Core.Data;
using PokeRogue.Core.Run;
using PokeRogue.Core.Run.Items;
using PokeRogue.Core.Run.Moves;
using PokeRogue.Core.Run.Progression;
using PokeRogue.Core.Run.Statuses;

namespace PokeRogue.DataTool.Balance;

/// <summary>
/// Milestone B: many headless 10-minute prototype runs played by <see cref="AutoPilot"/>, summarised as a Markdown
/// report in Italian: who deals the damage, how varied the builds are, how often evolutions are reached.
/// It only reads the game data; it never changes it.
/// </summary>
public sealed class BalanceSimulation(string contentRoot, bool weightedCards = true)
{
    private const int TenMinutes = RunClock.TicksPerSecond * 60 * 10;
    private const int TicksPerMinute = RunClock.TicksPerSecond * 60;

    /// <summary>The prototype table of GameplayScene (Bosco Smeraldo-like), until worlds and waves arrive.</summary>
    private static readonly SpawnEntry[] PrototypeSpawns = [new(10, 3), new(13, 3), new(16, 4), new(19, 5), new(21, 2), new(23, 2)];

    /// <summary>The project runs with invariant globalization: Italian decimal commas are put in by hand.</summary>
    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    private readonly PokedexFile _pokedex = GameDataFiles.LoadPokedex(contentRoot);
    private readonly TypeChart _typeChart = new(GameDataFiles.LoadTypeChart(contentRoot).Matchups);
    private readonly MoveLibrary _moves = MoveLibrary.Load(contentRoot);
    private readonly ItemLibrary _items = ItemLibrary.Load(contentRoot);
    private readonly StatusLibrary _statuses = StatusLibrary.Load(contentRoot);
    private readonly LevelUpRules? _levelUpRules = weightedCards ? LevelUpRules.Load(contentRoot) : null;

    public sealed record MoveResult(string Id, string Name, int Level, int MaxLevel, float Damage, bool Evolved)
    {
        public bool Maxed => !Evolved && Level >= MaxLevel;
    }

    public sealed record RunResult(int Seed, int Level, int Kills, int GemsLeft, IReadOnlyList<MoveResult> Moves, IReadOnlyList<string> Items, IReadOnlyList<(string Name, int Minute)> Evolutions)
    {
        public float TotalDamage => Moves.Sum(m => m.Damage);
    }

    /// <summary>One run: <paramref name="focused"/> picks cards like a player building towards evolutions, otherwise the first (random) card.</summary>
    public RunResult Play(int seed, bool focused)
    {
        var run = new RunState(_pokedex.Species, _typeChart, 25, PrototypeSpawns, new Vector2(640, 360), seed)
        {
            TimeLimitTicks = TenMinutes,
            MovePool = _moves.Enabled,
            ItemPool = _items.Enabled,
            LevelUpRules = _levelUpRules,
            StatusRules = _statuses,
            Evolutions = _moves.Evolutions,
        };
        run.Player.Moves.Add(new MoveInstance(_moves["thundershock"]));
        run.Player.Invincible = true;

        var evolutions = new List<(string, int)>();
        while (!run.IsOver)
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
            }
        }

        var gemsLeft = run.Pickups.Gems.Items.ToArray().Where(g => g.Active).Sum(g => g.Value);
        return new RunResult(seed, run.Player.Level, run.Stats.Kills, gemsLeft,
            run.Player.Moves.Select(m => new MoveResult(m.Definition.Id, m.Definition.NameIt, m.Level, m.Definition.MaxLevel, m.DamageDealt, m.Definition.IsEvolution)).ToList(),
            run.Player.Items.Select(i => i.Definition.NameIt).ToList(),
            evolutions);
    }

    /// <summary>Plays <paramref name="runs"/> runs with each way of picking cards and writes the report.</summary>
    public string Report(int runs, Action<string> progress)
    {
        var random = new List<RunResult>();
        var focused = new List<RunResult>();
        for (var seed = 1; seed <= runs; seed++)
        {
            random.Add(Play(seed, focused: false));
            focused.Add(Play(seed, focused: true));
            progress($"partita {seed}/{runs}");
        }

        var text = new StringBuilder();
        text.AppendLine("# Simulazione di bilanciamento (Milestone B)");
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
        text.AppendLine($"{runs} partite da 10 minuti per modo di scegliere, sulla tabella di nemici del prototipo, giocate dal pilota automatico " +
            "(Pikachu invincibile: va a prendere le gemme anche in mezzo ai nemici e lascia lavorare le mosse), con tutte le mosse, gli strumenti e le evoluzioni approvate. " +
            "Il pilota non è un giocatore vero: i numeri servono a confrontare mosse e strumenti tra loro, non a misurare la difficoltà.");
        text.AppendLine();
        text.AppendLine("- **Scelta casuale**: a ogni livello prende la prima carta (le carte sono in ordine casuale; un'evoluzione pronta è sempre la prima).");
        text.AppendLine("- **Scelta mirata**: prende un'evoluzione, altrimenti potenzia la mossa più avanti, altrimenti lo strumento che fa evolvere una sua mossa (`AutoPilot.ChooseLevelUp`).");
        text.AppendLine();

        Section(text, "Scelta casuale", random);
        Section(text, "Scelta mirata", focused);
        return text.ToString();
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
