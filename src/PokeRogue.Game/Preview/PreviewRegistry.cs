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
            ["run"] = ("Gameplay on autopilot: --pokemon id (characters.json) --stress N (enemies spawned at start, player invincible) --speed N --da m:ss (start later) [--evoluzioni] [--nemici]",
                (s, o) => new GameplayScene(s, o.Str("pokemon"), initialSpawn: o.Int("stress", 0), autopilot: true)
                {
                    Speed = System.Math.Max(1, o.Int("speed", 1)),
                    PendingEvolutions = o.Values.ContainsKey("evoluzioni"),
                    PendingEnemies = o.Values.ContainsKey("nemici"),
                    PendingLoot = o.Values.ContainsKey("oggetti"),
                    StartTicks = o.Str("da") is { } start ? Core.Run.Waves.WaveTimeline.ParseTime(start) : 0,
                }),
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
            ["evoluzione"] = ("Player's evolution (4.2): --variant sequenza|fermata|scelta|forziere [--pokemon id] [--scelta N]",
                (s, o) => new EvolutionPreviewScene(s, o.Str("variant") ?? "sequenza", o.Str("pokemon"), o.Int("scelta", 4))),
            ["abilita"] = ("Abilities (4.3): --variant pannello|teletrasporto|trasformazione|muta",
                (s, o) => new AbilityPreviewScene(s, o.Str("variant") ?? "pannello")),
            ["speciale"] = ("Special move (4.4): --tipo Fire (any of the 17) [--soloeffetto]",
                (s, o) => new SpecialPreviewScene(s, Enum.TryParse<Core.Data.PokemonType>(o.Str("tipo"), true, out var type) ? type : Core.Data.PokemonType.Fire,
                    o.Values.ContainsKey("soloeffetto"))),
            ["personaggi"] = ("Album of the playable characters (4.5): --pagina N, nine to a page", (s, o) => new CharacterAlbumScene(s, o.Int("pagina", 1))),
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
