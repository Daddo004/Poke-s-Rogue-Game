using Microsoft.Xna.Framework.Graphics;
using PokeRogue.Core.Meta;
using PokeRogue.Core.Run;
using PokeRogue.Core.Run.Waves;
using PokeRogue.Game.Scenes;
using PokeRogue.Game.Scenes.Menus;

namespace PokeRogue.Game.Preview;

/// <summary>
/// Block 6 previews: the menus and the shop over a demo save in memory (2.340 coins, a few characters and trainers
/// unlocked, some grades and upgrades bought, part of the Pokédex), never the real one. Variants: menu, pokemon,
/// potenzia, conferma, allenatori, sfida, potenziamenti, pokedex, mosse, strumenti, missioni, the level select of Block 7
/// (livelli, ultra with a challenge picked, bloccato on a locked level), and in a run: missione (the notice), novita (the
/// results' news) and carte (Fortuna's four cards and the sixth slot).
/// </summary>
public sealed class MetaPreviewScene : Scene
{
    private readonly string _variant;
    private readonly string? _pokemon;
    private Scene _inner = null!;
    private GameplayScene? _game;
    private int _tick;

    public MetaPreviewScene(GameServices services, string variant, string? pokemon) : base(services)
    {
        _variant = variant;
        _pokemon = pokemon;
        services.Meta = Demo(services);
    }

    public override bool HandlesEscape => _inner.HandlesEscape;

    public override IReadOnlyList<string> DebugLines => _inner.DebugLines;

    public override void Enter()
    {
        var pikachu = Services.Characters["pikachu"];
        _inner = _variant switch
        {
            "menu" or "mouse" => new MainMenuScene(Services),
            // Block 8: the title, the options, the credits, the pause with its options.
            "titolo" => new TitleScene(Services),
            "opzioni" => new OptionsScene(Services) { Cursor = 7 },
            "crediti" => Credits(),
            "pausa" or "pausa-opzioni" => _game = new GameplayScene(Services, autopilot: true, initialSpawn: 60),
            "pokemon" or "conferma" => new PokemonSelectScene(Services, _pokemon ?? (_variant == "conferma" ? "chikorita" : "pikachu")),
            "potenzia" => Upgrading(),
            "allenatori" => new TrainerSelectScene(Services, pikachu, _pokemon ?? "brock"),
            "sfida" => new TrainerSelectScene(Services, pikachu, _pokemon ?? "erika"),
            "potenziamenti" => new UpgradesScene(Services) { Cursor = 3 },
            "pokedex" => Collection(CollectionScene.Section.Pokedex, 24),
            "mosse" => Collection(CollectionScene.Section.Moves, 0),
            "strumenti" => Collection(CollectionScene.Section.Items, 2),
            "missioni" => Collection(CollectionScene.Section.Missions, 0),
            // Block 7: the level select.
            "livelli" => new LevelSelectScene(Services, pikachu, Services.Trainers.Value["ash"]),
            "ultra" => new LevelSelectScene(Services, pikachu, Services.Trainers.Value["ash"], "centrale-elettrica", Difficulty.Ultra),
            "bloccato" => new LevelSelectScene(Services, pikachu, Services.Trainers.Value["ash"], "bosco-di-lecci"),
            "missione" => _game = new GameplayScene(Services, autopilot: true, initialSpawn: 40) { Meta = Services.Meta },
            "novita" => _game = new GameplayScene(Services, "magikarp", autopilot: true, setup: run => run.AdvanceTime(WaveTimeline.ParseTime("14:20")))
            {
                Meta = Services.Meta,
            },
            "carte" => _game = new GameplayScene(Services, initialSpawn: 30, setup: run => run.GrantLevel())
            {
                Loadout = new RunLoadout { SecondaryMove = true, MaxMoves = 6, Choices = 4, ExtraRerolls = 1 },
            },
            _ => throw new ArgumentException($"Unknown variant '{_variant}'."),
        };
        _inner.Enter();
    }

    private CreditsScene Credits()
    {
        var scene = new CreditsScene(Services);
        scene.RollTo(420f);
        return scene;
    }

    private PokemonSelectScene Upgrading()
    {
        var scene = new PokemonSelectScene(Services, _pokemon ?? "pikachu");
        scene.OpenUpgrades(7);
        return scene;
    }

    private CollectionScene Collection(CollectionScene.Section section, int cursor)
    {
        var scene = new CollectionScene(Services);
        scene.Show(section, 0, cursor);
        return scene;
    }

    public override void Update()
    {
        _tick++;
        if (_variant == "conferma" && _tick == 2)
            ((PokemonSelectScene)_inner).Choose();
        if (_variant == "missione" && _tick == 20)
            _game!.Hud.ShowMission(Services.Meta.Missions.Find("mille-ko")!);
        if (_variant == "novita" && _tick == 30)
            _game!.ForceEnd(RunOutcome.Victory);
        if (_variant == "ultra" && _tick == 2)
            ((LevelSelectScene)_inner).PickChallenge(0);
        if (_variant == "mouse" && _tick == 2)
            Services.Input.SimulatedPointer = new Microsoft.Xna.Framework.Vector2(120, 145);
        if (_variant is "pausa" or "pausa-opzioni" && _tick == 50)
        {
            _game!.OpenPause();
            if (_variant == "pausa-opzioni")
                _game.Pause.OpenOptions(2);
            else
                Services.Input.SimulatedPointer = new Microsoft.Xna.Framework.Vector2(65, 246);
        }
        _inner.Update();
    }

    public override void Draw(SpriteBatch batch) => _inner.Draw(batch);

    /// <summary>A save some evenings in: coins to spend, a few unlocks, grades and upgrades, part of the Pokédex and missions.</summary>
    public static MetaProgress Demo(GameServices services)
    {
        var data = new SaveData
        {
            Coins = 2340,
            Characters = ["bulbasaur", "charmander", "squirtle", "cyndaquil", "eevee", "gastly", "abra", "dratini", "ditto", "magikarp"],
            Trainers = ["brock", "misty"],
            TrainersDefeated = ["brock", "misty"],
            Upgrades = new() { ["magnete"] = 2, ["avidita"] = 1, ["rilancio"] = 1 },
            UpgradeCoins = 150 + 300 + 200 + 400,
            Missions = ["prima-partita", "cinque-minuti", "mille-ko", "cromatico", "prima-medaglia", "livello-30", "prima-evoluzione", "dieci-partite"],
            LastCharacter = "pikachu",
            LastTrainer = "ash",
            Totals = new SaveTotals { Runs = 12, Victories = 3, Kills = 14520, Shinies = 21, Dominants = 31, Chests = 19, CoinsEarned = 5210, Specials = 6, TrainerUses = 40, MovesEvolved = 2, BestLevel = 48, BestTicks = RunClock.TicksPerSecond * 60 * 15 },
        };
        data.CharacterProgress["pikachu"] = new CharacterProgress
        {
            Secondary = true,
            Grades = new() { [StatKind.Hp] = 2, [StatKind.SpAttack] = 2, [StatKind.Speed] = 3, [StatKind.Attack] = 1 },
            GradeCoins = 300 + 300 + 600 + 100,
        };
        data.CharacterProgress["charmander"] = new CharacterProgress { Secondary = true, Special = true };
        data.TrainerProgress["ash"] = new TrainerProgress { Grade = 2, Coins = 640 };
        data.TrainerProgress["brock"] = new TrainerProgress { Grade = 1, Coins = 240 };
        data.CharacterWins["pikachu"] = 2;
        data.CharacterWins["charmander"] = 1;

        // The species of Bosco Smeraldo, nearly all defeated; a few others seen in challenges.
        var waves = WaveTimeline.Load(services.ContentRoot, GameplayScene.DefaultLevelId, services.Pokedex.Value.Species, services.Moves.Value);
        var i = 0;
        foreach (var id in waves.SpeciesIds.Append(25).Append(4).Append(1).Append(7).Append(74).Append(95).Append(120).Append(121))
        {
            var entry = data.EntryOf(id);
            entry.Seen = 40 + i * 17 % 300;
            entry.Defeated = i % 5 == 4 || id is 25 or 4 or 1 or 7 ? 0 : 30 + i * 13 % 250;
            i++;
        }

        var moves = services.Moves.Value;
        data.MovesFound = moves.Enabled.Take(16).Select(m => m.Id).Append("thunder").Append("flamethrower").Distinct().ToList();
        data.ItemsFound = services.Items.Value.Enabled.Take(9).Select(it => it.Id).ToList();
        // Block 7: Bosco Smeraldo won at every difficulty, the next three Kanto levels at Normale, the Power Plant at Difficile too.
        void Won(string level, Difficulty difficulty, int runs, int minutes, int kills)
        {
            var record = data.LevelOf(level, difficulty);
            record.Runs = runs;
            record.Wins = 1;
            record.BestTicks = RunClock.TicksPerSecond * 60 * minutes;
            record.BestKills = kills;
        }

        Won(GameplayScene.DefaultLevelId, Difficulty.Normal, 12, 16, 4512);
        Won(GameplayScene.DefaultLevelId, Difficulty.Hard, 3, 16, 5980);
        Won(GameplayScene.DefaultLevelId, Difficulty.Ultra, 2, 16, 7012);
        Won("monte-luna", Difficulty.Normal, 4, 16, 4210);
        Won("torre-pokemon", Difficulty.Normal, 2, 16, 3990);
        Won("centrale-elettrica", Difficulty.Normal, 2, 16, 4120);
        Won("centrale-elettrica", Difficulty.Hard, 1, 16, 5233);
        data.Missions.AddRange(["bosco-normale", "bosco-difficile", "bosco-ultra", "monte-luna-normale", "torre-pokemon-normale", "centrale-elettrica-normale", "centrale-elettrica-difficile"]);
        data.Fossils.Add("omanyte");
        data.Plates.AddRange(["lastrabaco", "lastrapietra", "lastratetra", "lastrasaetta", "lastramente"]);
        data.LastPlate = "lastrasaetta";
        data.LastLevel = "monte-luna";
        data.LastDifficulty = Difficulty.Hard;

        var worlds = services.Worlds.Value;
        return new MetaProgress(data, services.Shop.Value, services.Characters, services.Trainers.Value,
            MissionLibrary.Load(services.ContentRoot, services.Characters, services.Trainers.Value, worlds), services.Pokedex.Value.Species, worlds: worlds,
            plates: services.Plates.Value);
    }
}
