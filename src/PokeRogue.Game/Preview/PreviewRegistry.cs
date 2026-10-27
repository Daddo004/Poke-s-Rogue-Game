using PokeRogue.Game.Preview.Artboard;
using PokeRogue.Game.Scenes;

namespace PokeRogue.Game.Preview;

/// <summary>Isolated scenes reachable with <c>--preview &lt;id&gt;</c>, used for approval previews and debugging.</summary>
public static class PreviewRegistry
{
    private static readonly Dictionary<string, (string Description, Func<GameServices, LaunchOptions, Scene> Create)> Entries =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["sprites"] = ("Sprite Viewer: --species N --anim Walk", (s, o) => new SpriteViewerScene(s, o.Int("species", 25), o.Str("anim"))),
            ["run"] = ("Gameplay on autopilot: --pokemon id (characters.json) --allenatore id (trainers.json) --livello id (worlds.json) --difficolta normale|difficile|ultra --stress N (enemies spawned at start, player invincible) --speed N --da m:ss (start later) [--evoluzioni] [--nemici]",
                (s, o) => new GameplayScene(s, o.Str("pokemon"), initialSpawn: o.Int("stress", 0), autopilot: true, trainerId: o.Str("allenatore"))
                {
                    LevelId = o.Str("livello") ?? GameplayScene.DefaultLevelId,
                    Difficulty = GameplayScene.ParseDifficulty(o.Str("difficolta")),
                    Speed = System.Math.Max(1, o.Int("speed", 1)),
                    PendingEvolutions = o.Values.ContainsKey("evoluzioni"),
                    PendingEnemies = o.Values.ContainsKey("nemici"),
                    PendingLoot = o.Values.ContainsKey("oggetti"),
                    StartTicks = o.Str("da") is { } start ? Core.Run.Waves.WaveTimeline.ParseTime(start) : 0,
                    ChallengeId = o.Str("sfida"),
                    PlateId = o.Str("lastra"),
                    Endless = o.Values.ContainsKey("infinita"),
                }),
            ["sfida"] = ("Trainer challenge (5.3): --variant arrivo|cambio|squadra|vittoria [--sfida id] (Brock by default)",
                (s, o) => new ChallengePreviewScene(s, o.Str("variant") ?? "arrivo", o.Str("sfida") ?? "brock")),
            ["movelab"] = ("Move Lab (2.1/2.2): --move id --level 1-8 --hitbox 0|1 --reel a,b,c --every N",
                (s, o) => new CombatPreviewScene(s, o.Str("move") ?? o.Str("reel")?.Split(',')[0] ?? "thundershock", o.Int("level", 1),
                    hitboxes: o.Int("hitbox", 1) == 1, "LAB MOSSE", o.Str("reel"), o.Int("every", 0))),
            ["iconemosse"] = ("Move icons (2.4): --tipi Normal,Water (all moves when omitted)", (s, o) => new MoveIconsPreviewScene(s, o.Str("tipi"))),
            ["evoluzioni"] = ("Evolved moves (2.6): each with its recipe, icon and HUD slot", (s, _) => new EvolutionsPreviewScene(s)),
            ["strumenti"] = ("Held items (2.5): every item with its icon, levels and effect", (s, _) => new ItemsPreviewScene(s)),
            ["nemici"] = ("Enemy behaviours (3.2): --variant tiratore|caricatore|tank|kamikaze|teletrasporto|tutti",
                (s, o) => new EnemiesPreviewScene(s, o.Str("variant") ?? "tutti")),
            ["dominanti"] = ("Dominanti and chests (3.3): --variant campo|arrivo|terra|forziere [--ball poke|mega|ultra]",
                (s, o) => new DominantsPreviewScene(s, o.Str("variant") ?? "campo", o.Str("ball"))),
            ["boss"] = ("Level boss (3.4): --variant arrivo|raffica|massi|fasi|finale", (s, o) => new BossPreviewScene(s, o.Str("variant") ?? "arrivo")),
            ["oggetti"] = ("Breakables, items and shinies (3.5): --variant campo|terra|raccolta|cromatico", (s, o) => new LootPreviewScene(s, o.Str("variant") ?? "campo")),
            ["lastre"] = ("Arceus' Plates (9.1): --variant scelta|forziere|terra|effetto|hud [--lastra id] [--da m:ss]",
                (s, o) => new Block9PreviewScene(s, "lastre", o.Str("variant") ?? "scelta", o.Str("lastra"), o.Str("da"))),
            ["infinita"] = ("Endless mode and Darkrai (9.3): --variant livello|mietitore|vuoto|sonno",
                (s, o) => new Block9PreviewScene(s, "infinita", o.Str("variant") ?? "mietitore", null, null)),
            ["kecleon"] = ("Kecleon the merchant (9.2): --variant campo|vicino|negozio", (s, o) => new Block9PreviewScene(s, "kecleon", o.Str("variant") ?? "campo", null, null)),
            ["evoluzione"] = ("Player's evolution (4.2): --variant sequenza|fermata|scelta|forziere [--pokemon id] [--scelta N]",
                (s, o) => new EvolutionPreviewScene(s, o.Str("variant") ?? "sequenza", o.Str("pokemon"), o.Int("scelta", 4))),
            ["abilita"] = ("Abilities (4.3): --variant pannello|teletrasporto|trasformazione|muta",
                (s, o) => new AbilityPreviewScene(s, o.Str("variant") ?? "pannello")),
            ["speciale"] = ("Special move (4.4): --tipo Fire (any of the 17) [--soloeffetto]",
                (s, o) => new SpecialPreviewScene(s, Enum.TryParse<Core.Data.PokemonType>(o.Str("tipo"), true, out var type) ? type : Core.Data.PokemonType.Fire,
                    o.Values.ContainsKey("soloeffetto"))),
            ["allenatori"] = ("Trainers (5.1/5.2): --variant scheda|album|<archetype: scudo|cura|onda|tempo|intangibile|tipo|fortuna|frenesia|stato|scatto> [--allenatore id] [--pagina N]",
                (s, o) => new TrainerPreviewScene(s, o.Str("variant") ?? "scheda", o.Str("allenatore"), o.Int("pagina", 1))),
            ["negozio"] = ("Meta-progression (Blocks 6 and 7) on a demo save: --variant menu|pokemon|potenzia|conferma|allenatori|sfida|livelli|ultra|bloccato|potenziamenti|pokedex|mosse|strumenti|missioni|missione|novita|carte [--pokemon id]",
                (s, o) => new MetaPreviewScene(s, o.Str("variant") ?? "menu", o.Str("pokemon"))),
            ["carte"] = ("Collectible cards (10): --variant menu|album|visore|rarita|bustina|strappo|uscita|carta|rara|girata|riepilogo|sequenza [--carta id] [--mouse x,y]",
                (s, o) => new CardsPreviewScene(s, o.Str("variant") ?? "album", o.Str("carta"), o.Str("mouse"))),
            ["mappe"] =("Album of the levels' maps (7.2): --pagina N, six to a page", (s, o) => new LevelAlbumScene(s, o.Int("pagina", 1))),
            ["personaggi"] =("Album of the playable characters (4.5): --pagina N, nine to a page", (s, o) => new CharacterAlbumScene(s, o.Int("pagina", 1))),
            ["rinascita"] = ("Focalnastro (2.5): Pikachu is knocked out and stands back up", (s, _) => new RevivePreviewScene(s)),
            ["stati"] = ("Status conditions (2.3): --variant vetrina|icone|orda [--icone simbolo|riquadro|nessuna] [--stress N] [--mosse a,b,c]",
                (s, o) => new StatusPreviewScene(s, o.Str("variant"), o.Str("icone"), o.Int("stress", 150), o.Str("mosse"))),
            ["hud"] = ("HUD and end of run (1.6): --variant partita|pausa|sconfitta|vittoria|risultati [--evoluta]",
                (s, o) => new HudPreviewScene(s, o.Str("variant"), o.Values.ContainsKey("evoluta"))),
            ["digits"] = ("Font digit legibility check", (s, _) => new DigitsPreviewScene(s)),
            ["levelup"] = ("Gems and level-up (1.5): --variant lista|carte|azioni|evoluzione [--window]", (s, o) => new LevelUpPreviewScene(s, o.Str("variant"), o.Values.ContainsKey("window"))),
            ["combat"] = ("Combat (1.4): Thundershock on dummies --level 1-8",
                (s, o) => new CombatPreviewScene(s, "thundershock", o.Int("level", 4), hitboxes: false, "ANTEPRIMA 1.4")),
            ["floor"] = ("Floor (1.1): --variant prato|decorato|sentiero", (s, o) => new FloorPreviewScene(s, o.Str("variant"))),
            ["artboard"] = ("Art direction (0.6): --variant hud-a|hud-b|menu|style --font Name", (s, o) => new ArtboardScene(s, o.Str("variant") ?? "hud-a", o.Str("font"))),
        };

    public static IEnumerable<(string Id, string Description)> List => Entries.Select(e => (e.Key, e.Value.Description));

    public static Scene? Create(string id, GameServices services, LaunchOptions options) =>
        Entries.TryGetValue(id, out var entry) ? entry.Create(services, options) : null;
}
