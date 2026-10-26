using System.Collections.Concurrent;
using System.Globalization;
using System.Text;
using PokeRogue.Core.Data;
using PokeRogue.Core.Meta;
using PokeRogue.Core.Run;
using PokeRogue.Core.Run.Characters;
using PokeRogue.Core.Run.Moves;

namespace PokeRogue.DataTool.Balance;

/// <summary>
/// Block 6: careers from an empty save. A pilot that can be hurt plays run after run, banks the coins, completes missions
/// and spends in the shop by a fixed plan (the secondary move, the special, a few stat grades and cheap upgrades, then new
/// characters, whom it plays from then on). The report says how coins come in and how much stronger the runs get.
/// </summary>
public sealed class MetaSimulation(string contentRoot)
{
    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    private readonly RosterSimulation _roster = new(contentRoot);
    private readonly ShopRules _shop = ShopRules.Load(contentRoot);
    private readonly IReadOnlyList<SpeciesData> _pokedex = GameDataFiles.LoadPokedex(contentRoot).Species;

    private sealed record Step(int Run, string Character, int Minutes, bool Won, int Coins, int MissionCoins, int Bank, string Bought, string Missions);

    public string Report(int careers, int runs, Action<string> progress)
    {
        var missions = MissionLibrary.Load(contentRoot, _roster.Characters, _roster.Trainers);
        var all = new ConcurrentDictionary<int, List<Step>>();
        Parallel.For(1, careers + 1, new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount }, career =>
        {
            var meta = new MetaProgress(new SaveData(), _shop, _roster.Characters, _roster.Trainers, missions, _pokedex);
            var steps = new List<Step>();
            var character = _roster.Characters[CharacterLibrary.DefaultId];
            for (var run = 1; run <= runs; run++)
            {
                var result = _roster.Play(character, career * 1000 + run, mortal: true, loadout: meta.Loadout(character));
                var report = meta.RecordRun(result.Record!);
                var bought = Shop(meta, ref character);
                steps.Add(new Step(run, character.NameIt, RosterSimulation.Survival(result), result.Outcome == RunOutcome.Victory,
                    report.CoinsBanked, report.MissionCoins, meta.Coins, bought, string.Join(", ", report.Missions.Select(m => m.NameIt))));
            }

            all[career] = steps;
            progress($"carriera {career}/{careers}");
        });

        var text = new StringBuilder();
        text.AppendLine("# Simulazione della meta-progressione (Blocco 6)");
        text.AppendLine();
        text.AppendLine($"Generata il {DateTime.Now.ToString("dd/MM/yyyy HH:mm", Invariant)} con `dotnet run --project tools/PokeRogue.DataTool -c Release -- meta --carriere {careers} --partite {runs}`.");
        text.AppendLine();
        text.AppendLine($"{careers} carriere da un salvataggio vuoto, {runs} partite ciascuna su Bosco Smeraldo con Ash, col pilota automatico che subisce danni " +
            "(si ferma al primo KO, come un giocatore che perde). Dopo ogni partita spende nel negozio con un piano fisso: mossa secondaria, poi speciale, " +
            "poi Magnete e Avidità, poi un nuovo Pokémon (il più economico) che gioca da lì in poi, poi i gradi delle statistiche. " +
            "Il pilota non schiva come un giocatore vero: conta come crescono monete e resistenza, non i valori assoluti.");
        text.AppendLine();
        text.AppendLine("| Partita | Monete della partita (mediana) | Monete delle missioni (mediana) | In banca dopo gli acquisti | Resiste fino al minuto | Vittorie |");
        text.AppendLine("|---:|---:|---:|---:|---:|---:|");
        for (var run = 1; run <= runs; run++)
        {
            var steps = all.Values.Select(s => s[run - 1]).ToList();
            text.AppendLine($"| {run} | {RosterSimulation.Median(steps.Select(s => s.Coins))} | {RosterSimulation.Median(steps.Select(s => s.MissionCoins))} | " +
                $"{RosterSimulation.Median(steps.Select(s => s.Bank))} | {RosterSimulation.Median(steps.Select(s => s.Minutes))} | {steps.Count(s => s.Won)}/{careers} |");
        }

        text.AppendLine();
        text.AppendLine("## Una carriera nel dettaglio");
        text.AppendLine();
        text.AppendLine("| Partita | Pokémon | Minuto | Monete | Missioni | Acquisti |");
        text.AppendLine("|---:|---|---:|---:|---|---|");
        foreach (var step in all[1])
            text.AppendLine($"| {step.Run} | {step.Character} | {step.Minutes}{(step.Won ? " (vinta)" : "")} | {step.Coins} + {step.MissionCoins} | {step.Missions} | {step.Bought} |");
        return text.ToString();
    }

    /// <summary>The fixed shopping plan; returns what was bought, and switches to a newly bought character.</summary>
    private string Shop(MetaProgress meta, ref CharacterDefinition character)
    {
        var bought = new List<string>();
        var current = character;
        bool Try(Purchase purchase, string what)
        {
            if (purchase != Purchase.Done)
                return false;
            bought.Add(what);
            return true;
        }

        var progress = true;
        while (progress)
        {
            progress = Try(meta.BuySecondary(current), $"{current.NameIt}: secondaria")
                || Try(meta.BuySpecial(current), $"{current.NameIt}: speciale")
                || _shop.Upgrades.Where(u => u.Id is "magnete" or "avidita").Any(u => meta.UpgradeGrade(u) < 2 && Try(meta.BuyUpgrade(u), u.NameIt));

            if (!progress && meta.HasSpecial(current))
            {
                var next = meta.Characters.All.Where(c => !meta.IsUnlocked(c) && meta.Price(c) is not null).MinBy(c => meta.Price(c));
                if (next is not null && Try(meta.Buy(next), next.NameIt))
                {
                    current = next;
                    progress = true;
                }
                else
                {
                    progress = _shop.Pokemon.Stats.OrderBy(s => meta.Grade(current, s.Stat)).Any(s => meta.Grade(current, s.Stat) < 2
                        && Try(meta.BuyGrade(current, s.Stat), $"{current.NameIt}: {s.NameIt} {meta.Grade(current, s.Stat) + 1}"));
                }
            }
        }

        character = current;
        return string.Join(", ", bought);
    }
}
