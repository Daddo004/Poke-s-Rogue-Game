using Microsoft.Xna.Framework.Graphics;
using PokeRogue.Core.Run;
using PokeRogue.Core.Run.Merchant;
using PokeRogue.Core.Run.Moves;
using PokeRogue.Core.Run.Pickups;
using PokeRogue.Game.Scenes;
using PokeRogue.Game.Scenes.Menus;
using Vector2 = System.Numerics.Vector2;

namespace PokeRogue.Game.Preview;

/// <summary>
/// Block 9 previews, on the demo save of the shop's previews (five Plates found):
/// - lastre scelta: the Plate select after the level;
/// - lastre forziere: a chest's offer of a second Plate;
/// - lastre terra: Plates lying on the map, as they are found;
/// - lastre effetto: a run on autopilot with a Plate (--lastra id) and moves of its type, from minute 5 (--da);
/// - lastre hud: the HUD with two Plates held;
/// - kecleon campo: Kecleon on the field, camouflaged, with the player far; kecleon vicino: the player beside it;
/// - kecleon negozio: its shop open, a ware already sold.
/// </summary>
public sealed class Block9PreviewScene : Scene
{
    private readonly Scene _inner;
    private readonly GameplayScene? _game;
    private readonly string _variant;
    private readonly string _plate;
    private int _tick;

    public Block9PreviewScene(GameServices services, string what, string variant, string? plate, string? from) : base(services)
    {
        _variant = $"{what}-{variant}".ToLowerInvariant();
        _plate = plate ?? "lastrarogo";
        services.Meta = MetaPreviewScene.Demo(services);
        var pikachu = services.Characters["pikachu"];
        var ash = services.Trainers.Value["ash"];
        var start = from is not null ? Core.Run.Waves.WaveTimeline.ParseTime(from) : 0;

        _inner = _variant switch
        {
            "lastre-scelta" => new PlateSelectScene(services, pikachu, ash, "monte-luna", Core.Meta.Difficulty.Normal, null),
            "lastre-effetto" => _game = new GameplayScene(services, autopilot: true, setup: GiveTypeMoves) { PlateId = _plate, StartTicks = start > 0 ? start : 5 * 60 * 60 },
            "lastre-hud" => _game = new GameplayScene(services, autopilot: true, initialSpawn: 40, setup: run =>
            {
                run.UsePlate(services.Plates.Value["lastrasaetta"]);
                run.UsePlate(services.Plates.Value["lastramente"]);
            }),
            "lastre-forziere" or "lastre-terra" or "kecleon-campo" or "kecleon-vicino" or "kecleon-negozio" =>
                _game = new GameplayScene(services, setup: Prepare) { StartTicks = start },
            _ => throw new ArgumentException($"Unknown variant '{variant}'."),
        };
    }

    public override IReadOnlyList<string> DebugLines => _inner.DebugLines;

    public override bool HandlesEscape => _inner.HandlesEscape;

    public override void Enter() => _inner.Enter();

    public override void Update()
    {
        _tick++;
        _inner.Update();
        if (_game is null)
            return;

        var run = _game.Run;
        if (_variant == "kecleon-negozio" && _tick == 2)
            run.OpenShop();
        if (_variant == "lastre-forziere" && _tick == 2)
            run.OfferPlates([Services.Plates.Value["lastrarogo"], Services.Plates.Value["lastraidro"], Services.Plates.Value["lastrafiele"]]);
    }

    public override void Draw(SpriteBatch batch) => _inner.Draw(batch);

    /// <summary>The effect preview: moves of the Plate's type at a good level, so its boost and its effect both show.</summary>
    private void GiveTypeMoves(RunState run)
    {
        var plate = Services.Plates.Value[_plate];
        foreach (var move in Services.Moves.Value.Enabled.Where(m => m.Type == plate.Type && !m.IsEvolution).Take(2))
            run.Player.Moves.Add(new MoveInstance(move, level: Math.Min(6, move.MaxLevel)));
    }

    private void Prepare(RunState run)
    {
        run.Spawner.Enabled = false;
        var player = run.Player.Position;
        switch (_variant)
        {
            case "lastre-terra":
                run.Loot.Drop(player + new Vector2(-90, 30), LootKind.Plate, "lastrarogo");
                run.Loot.Drop(player + new Vector2(0, 60), LootKind.Plate, "lastraidro");
                run.Loot.Drop(player + new Vector2(90, 30), LootKind.Plate, "lastraprato");
                break;

            case "lastre-forziere":
                run.UsePlate(Services.Plates.Value["lastrasaetta"]);
                break;

            default:
                // Kecleon: two Rattata around, coins in the pocket, and a fixed stock so the pictures can be compared.
                run.AddCoins(160);
                MerchantSystem.Arrive(run, Services.Merchant.Value);
                run.Merchant.Stock.Clear();
                run.Merchant.Stock.Add(new Ware(WareKind.Potion, 40, "Superpozione", "Recupera il 50% dei PS.", "super-potion", power: 0.5f));
                run.Merchant.Stock.Add(new Ware(WareKind.RareCandy, 80, "Caramella Rara", "Sali subito di un livello.", "rare-candy"));
                var magnet = Services.Items.Value["magnet"];
                run.Merchant.Stock.Add(new Ware(WareKind.HeldItem, 120, magnet.NameIt, magnet.DescriptionIt, magnet.IconKey, magnet.Id));
                run.Merchant.Stock.Add(new Ware(WareKind.Reroll, 50, "Rilancio", "Un Rilancio in più per i level-up.", "max-elixir") { Sold = _variant == "kecleon-negozio" });
                if (_variant == "kecleon-vicino")
                    run.Player.Position = run.Merchant.Position + new Vector2(-40, 12);
                for (var i = 0; i < 2; i++)
                    run.Enemies.Spawn(Services.Pokedex.Value.Species[19 - 1], player + new Vector2(-160 + i * 60, -90), hpMultiplier: 20f);
                break;
        }
    }
}
