using System.Collections.Concurrent;
using System.Globalization;
using System.Text;
using PokeRogue.Core.Data;
using PokeRogue.Core.Run;
using PokeRogue.Core.Run.Trainers;

namespace PokeRogue.DataTool.Balance;

/// <summary>
/// Phase 5.1/5.2: the trainers. <see cref="Summary"/> lists them from trainers.json (the page to approve);
/// <see cref="Report"/> plays a few characters with every trainer, and with none, as the game would (the autopilot presses
/// Q when <see cref="AutoPilot.UseTrainer"/> says so), with an invincible pilot and with one that can be hurt.
/// </summary>
public sealed class TrainerSimulation(string contentRoot)
{
    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    /// <summary>A starter of each kind and the default character: frail and sturdy, physical and special.</summary>
    private static readonly string[] Players = ["pikachu", "charmander", "squirtle", "gible"];

    private readonly RosterSimulation _roster = new(contentRoot);

    public string Summary()
    {
        var text = new StringBuilder();
        var trainers = _roster.Trainers.All;
        text.AppendLine("# Allenatori (fase 5.1)");
        text.AppendLine();
        text.AppendLine($"Generato da `content/data/trainers.json` con `dotnet run --project tools/PokeRogue.DataTool -- allenatori`. {trainers.Count} allenatori: " +
            "Ash (di base, sbloccato dall'inizio) e capipalestra, Superquattro e Campioni delle prime quattro generazioni, più Rosso. Ognuno compare una volta " +
            "sola, nella regione dove ha guidato per primo una palestra o la Lega (Koga, Bruno e Lance stanno in Kanto). Gli altri si sbloccano battendoli a " +
            "difficoltà Ultra nella loro sfida (fase 5.3).");
        text.AppendLine();
        text.AppendLine("- **Passiva**: sempre attiva. Gli allenatori con un tipo danno +15% alle mosse di quel tipo e un piccolo bonus (circa mezzo livello di uno strumento); " +
            "Ash, Blu, Rosso e Camilla non hanno un tipo e danno due bonus.");
        text.AppendLine("- **Attiva**: tasto Q (o un dorsale del pad), poi ricarica. Dieci archetipi:");
        text.AppendLine();
        text.AppendLine("| Archetipo | Cosa fa | Allenatori |");
        text.AppendLine("|---|---|---|");
        foreach (var kind in Enum.GetValues<TrainerActiveKind>())
        {
            var who = trainers.Where(t => t.Active.Kind == kind).ToList();
            text.AppendLine($"| {KindName(kind)} | {KindText(kind)} | {string.Join(", ", who.Select(t => t.NameIt))} ({who.Count}) |");
        }

        foreach (var region in Enum.GetValues<TrainerRegion>())
        {
            text.AppendLine();
            text.AppendLine($"## {region}");
            text.AppendLine();
            text.AppendLine("| Allenatore | Ruolo | Tipo | Passiva | Attiva (Q) | Ricarica | Squadra della sfida (5.3) |");
            text.AppendLine("|---|---|---|---|---|---:|---|");
            foreach (var t in trainers.Where(t => t.Region == region))
            {
                var team = t.Team.Count > 0 ? string.Join(", ", t.Team.Select(m => _roster.Species(m.SpeciesId))) : "–";
                text.AppendLine($"| {t.NameIt} | {RoleName(t.Role)} | {(t.Type is { } type ? TypeName(type) : "–")} | **{t.Passive.NameIt}**: {t.Passive.DescriptionIt} | " +
                    $"**{t.Active.NameIt}** ({KindName(t.Active.Kind)}): {t.Active.DescriptionIt} | {t.Active.Cooldown.ToString(Invariant)} s | {team} |");
            }
        }

        return text.ToString();
    }

    public string Report(int runs, Action<string> progress)
    {
        var characters = Players.Select(id => _roster.Characters[id]).ToList();
        var entries = new List<TrainerDefinition?> { null };
        entries.AddRange(_roster.Trainers.All);

        var results = new ConcurrentDictionary<string, (List<RosterSimulation.RunResult> Strong, List<RosterSimulation.RunResult> Mortal)>();
        var done = 0;
        Parallel.ForEach(entries, new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount }, trainer =>
        {
            var strong = new List<RosterSimulation.RunResult>();
            var mortal = new List<RosterSimulation.RunResult>();
            foreach (var character in characters)
            {
                for (var seed = 1; seed <= runs; seed++)
                {
                    strong.Add(_roster.Play(character, seed, mortal: false, trainer, noTrainer: trainer is null));
                    mortal.Add(_roster.Play(character, seed, mortal: true, trainer, noTrainer: trainer is null));
                }
            }

            results[trainer?.Id ?? ""] = (strong, mortal);
            progress($"{Interlocked.Increment(ref done)}/{entries.Count} {trainer?.NameIt ?? "nessuno"}");
        });

        var (baseStrong, baseMortal) = results[""];
        var baseKills = RosterSimulation.Median(baseStrong.Select(r => r.Kills));
        var baseSurvival = RosterSimulation.Median(baseMortal.Select(RosterSimulation.Survival));
        var games = characters.Count * runs;

        var text = new StringBuilder();
        text.AppendLine("# Simulazione degli allenatori (fase 5.2)");
        text.AppendLine();
        text.AppendLine($"Generata il {DateTime.Now.ToString("dd/MM/yyyy HH:mm", Invariant)} con `dotnet run --project tools/PokeRogue.DataTool -c Release -- allenatori --runs {runs}`.");
        text.AppendLine();
        text.AppendLine($"{string.Join(", ", characters.Select(c => c.NameIt))} giocano {runs} partite ciascuno con ogni allenatore e senza allenatore: " +
            "una col pilota invincibile (quanto diventa forte la build) e la stessa con un pilota che subisce danni e scappa dalla folla (quanto resiste). " +
            "Il pilota preme Q quando serve: cura sotto metà PS, scudo, intangibilità e scatto con un nemico addosso, onda con 8 nemici a portata, " +
            "le altre con 25 nemici sullo schermo o con un Dominante o il boss; Fortuna con 30 gemme in giro. Il pilota non schiva, quindi i numeri servono a confrontare gli allenatori fra loro.");
        text.AppendLine();
        text.AppendLine($"- **Senza allenatore**: KO {baseKills}, resiste fino al minuto {baseSurvival}, vince {baseMortal.Count(r => r.Outcome == RunOutcome.Victory)}/{games}.");
        text.AppendLine("- **Più forte / più debole**: KO almeno il 15% sopra o sotto quelli senza allenatore, oppure resistenza di almeno 2 minuti in più o in meno.");
        text.AppendLine();
        text.AppendLine("| Allenatore | Attiva | Usi a partita | KO | KO rispetto a nessuno | Resiste fino al minuto | Vince | Giudizio |");
        text.AppendLine("|---|---|---:|---:|---:|---:|---:|---|");
        foreach (var trainer in _roster.Trainers.All.OrderByDescending(t => RosterSimulation.Median(results[t.Id].Mortal.Select(RosterSimulation.Survival)))
                     .ThenByDescending(t => RosterSimulation.Median(results[t.Id].Strong.Select(r => r.Kills))))
        {
            var (strong, mortal) = results[trainer.Id];
            var kills = RosterSimulation.Median(strong.Select(r => r.Kills));
            var survival = RosterSimulation.Median(mortal.Select(RosterSimulation.Survival));
            var ratio = baseKills > 0 ? kills / (float)baseKills : 1f;
            var verdict = ratio >= 1.15f || survival >= baseSurvival + 2 ? "più forte" : ratio <= 0.85f || survival <= baseSurvival - 2 ? "più debole" : "";
            text.AppendLine($"| {trainer.NameIt} | {trainer.Active.NameIt} ({KindName(trainer.Active.Kind)}) | {RosterSimulation.Median(strong.Select(r => r.TrainerUses))} | " +
                $"{kills} | {MathF.Round((ratio - 1f) * 100f).ToString("+0;-0;0", Invariant)}% | {survival} | {mortal.Count(r => r.Outcome == RunOutcome.Victory)}/{games} | {verdict} |");
        }

        return text.ToString();
    }

    /// <summary>
    /// Phase 5.3: every trainer's challenge, played by the same characters with Ash. The invincible pilot measures how
    /// long the whole team lasts; the one that can be hurt, whether it gets through.
    /// </summary>
    public string ChallengeReport(int runs, Action<string> progress)
    {
        var characters = Players.Select(id => _roster.Characters[id]).ToList();
        var trainers = _roster.Trainers.All.Where(t => t.Team.Count > 0).ToList();
        var results = new ConcurrentDictionary<string, (List<RosterSimulation.RunResult> Strong, List<RosterSimulation.RunResult> Mortal)>();
        var done = 0;
        Parallel.ForEach(trainers, new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount }, trainer =>
        {
            var strong = new List<RosterSimulation.RunResult>();
            var mortal = new List<RosterSimulation.RunResult>();
            foreach (var character in characters)
            {
                for (var seed = 1; seed <= runs; seed++)
                {
                    strong.Add(_roster.Play(character, seed, mortal: false, challenge: trainer));
                    mortal.Add(_roster.Play(character, seed, mortal: true, challenge: trainer));
                }
            }

            results[trainer.Id] = (strong, mortal);
            progress($"{Interlocked.Increment(ref done)}/{trainers.Count} {trainer.NameIt}");
        });

        var games = characters.Count * runs;
        var text = new StringBuilder();
        text.AppendLine("# Simulazione delle sfide agli allenatori (fase 5.3)");
        text.AppendLine();
        text.AppendLine($"Generata il {DateTime.Now.ToString("dd/MM/yyyy HH:mm", Invariant)} con `dotnet run --project tools/PokeRogue.DataTool -c Release -- sfide --runs {runs}`.");
        text.AppendLine();
        text.AppendLine($"{string.Join(", ", characters.Select(c => c.NameIt))} giocano Bosco Smeraldo con Ash, {runs} partite ciascuno per sfida; allo scadere dei 15 minuti " +
            "arriva l'allenatore al posto di Pinsir. Il pilota invincibile misura quanto dura la squadra intera (dall'ingresso dell'allenatore all'ultimo KO, ingresso e " +
            "lanci delle Poké Ball esclusi perché il gioco è fermo); quello che subisce danni dice se ce la fa. Per confronto, Pinsir dura circa 70 s.");
        text.AppendLine();
        text.AppendLine("| Allenatore | Ruolo | Squadra | Durata della squadra (s, mediana) | Più lunga (s) | Vince il pilota che subisce danni |");
        text.AppendLine("|---|---|---|---:|---:|---:|");
        foreach (var trainer in trainers.OrderBy(t => t.Region).ThenBy(t => t.Role))
        {
            var (strong, mortal) = results[trainer.Id];
            var times = strong.Where(r => r.BossSeconds is not null).Select(r => (int)r.BossSeconds!.Value).ToList();
            var team = string.Join(", ", trainer.Team.Select(m => _roster.Species(m.SpeciesId)));
            text.AppendLine($"| {trainer.NameIt} | {RoleName(trainer.Role)} | {team} | {(times.Count > 0 ? RosterSimulation.Median(times).ToString(Invariant) : "–")} | " +
                $"{(times.Count > 0 ? times.Max().ToString(Invariant) : "–")} | {mortal.Count(r => r.Outcome == RunOutcome.Victory)}/{games} |");
        }

        return text.ToString();
    }

    public static string KindName(TrainerActiveKind kind) => kind switch
    {
        TrainerActiveKind.Shield => "Scudo",
        TrainerActiveKind.Heal => "Cura",
        TrainerActiveKind.Wave => "Onda",
        TrainerActiveKind.SlowTime => "Tempo",
        TrainerActiveKind.Intangible => "Intangibile",
        TrainerActiveKind.TypeBoost => "Tipo",
        TrainerActiveKind.Fortune => "Fortuna",
        TrainerActiveKind.Frenzy => "Frenesia",
        TrainerActiveKind.StatusStorm => "Stato",
        _ => "Scatto",
    };

    private static string KindText(TrainerActiveKind kind) => kind switch
    {
        TrainerActiveKind.Shield => "blocca i prossimi colpi (3, entro 10 s)",
        TrainerActiveKind.Heal => "recupera una parte dei PS massimi",
        TrainerActiveKind.Wave => "anello attorno: toglie metà dei PS ai nemici vicini e li respinge (un decimo ai Dominanti e al boss)",
        TrainerActiveKind.SlowTime => "nemici e proiettili rallentano per qualche secondo",
        TrainerActiveKind.Intangible => "nessun colpo arriva per 4 s",
        TrainerActiveKind.TypeBoost => "le mosse del suo tipo fanno di più per 10 s, le altre un terzo di quel di più",
        TrainerActiveKind.Fortune => "attira tutte le gemme; esperienza e monete raddoppiano per 15 s",
        TrainerActiveKind.Frenzy => "le mosse si ricaricano più in fretta per 8 s",
        TrainerActiveKind.StatusStorm => "uno stato a tutti i nemici sullo schermo",
        _ => "corri più veloce per 6 s",
    };

    private static string RoleName(TrainerRole role) => role switch
    {
        TrainerRole.Base => "Di base",
        TrainerRole.GymLeader => "Capopalestra",
        TrainerRole.EliteFour => "Superquattro",
        TrainerRole.Champion => "Campione",
        _ => "Leggenda",
    };

    private static string TypeName(PokemonType type) => type switch
    {
        PokemonType.Normal => "Normale",
        PokemonType.Fire => "Fuoco",
        PokemonType.Water => "Acqua",
        PokemonType.Grass => "Erba",
        PokemonType.Electric => "Elettro",
        PokemonType.Ice => "Ghiaccio",
        PokemonType.Fighting => "Lotta",
        PokemonType.Poison => "Veleno",
        PokemonType.Ground => "Terra",
        PokemonType.Flying => "Volante",
        PokemonType.Psychic => "Psico",
        PokemonType.Bug => "Coleottero",
        PokemonType.Rock => "Roccia",
        PokemonType.Ghost => "Spettro",
        PokemonType.Dragon => "Drago",
        PokemonType.Dark => "Buio",
        _ => "Acciaio",
    };
}
