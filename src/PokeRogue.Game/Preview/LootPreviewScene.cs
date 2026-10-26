using Microsoft.Xna.Framework.Graphics;
using PokeRogue.Core.Run;
using PokeRogue.Core.Run.Moves;
using PokeRogue.Core.Run.Pickups;
using PokeRogue.Game.Scenes;
using Vector2 = System.Numerics.Vector2;

namespace PokeRogue.Game.Preview;

/// <summary>
/// Phase 3.5 preview of breakables, items and shinies:
/// - campo: tall grass and boulders around; Pikachu's moves aim at a few Rattata and break what they pass through;
/// - terra: every item on the ground beside Pikachu, as it lies there;
/// - raccolta: the autopilot walks over each item in turn (coins, nugget, potion, candy, Condividi Esp., Repellente);
/// - cromatico: a shiny Pidgey beside a normal one, with its glints and the announcement.
/// </summary>
public sealed class LootPreviewScene : Scene
{
    private static readonly LootKind[] Everything =
        [LootKind.Coin, LootKind.Nugget, LootKind.BigNugget, LootKind.Potion, LootKind.RareCandy, LootKind.ExpShare, LootKind.Repel];

    private readonly GameplayScene _game;
    private readonly string _variant;
    private bool _started;

    public LootPreviewScene(GameServices services, string variant) : base(services)
    {
        _variant = variant.ToLowerInvariant();
        _game = new GameplayScene(services, autopilot: _variant is "campo" or "raccolta", setup: Prepare) { PendingLoot = true };
    }

    public override IReadOnlyList<string> DebugLines => _game.DebugLines;

    public override bool HandlesEscape => _game.HandlesEscape;

    public override void Enter() => _game.Enter();

    public override void Update()
    {
        var run = _game.Run;
        if (!_started)
        {
            _started = true;
            Start(run);
        }

        _game.Update();
        if (_variant == "campo" && run.Ticks % 90 == 0 && run.Enemies.AliveCount < 4)
            SpawnRattata(run, run.Ticks / 90 * 1.3f);
    }

    public override void Draw(SpriteBatch batch) => _game.Draw(batch);

    private void Prepare(RunState run)
    {
        run.Spawner.Enabled = false;
        run.Player.Moves.Clear();
        if (_variant == "campo")
        {
            var moves = Services.Moves.Value;
            run.Player.Moves.Add(new MoveInstance(moves["ember"], level: 5));
            run.Player.Moves.Add(new MoveInstance(moves["razor-leaf"], level: 5));
        }

        // The field of the campo variant only: elsewhere grass and rocks would get in the way of the items.
        if (_variant != "campo" && run.LootRules is { } rules)
        {
            run.LootRules = LootRules.From(rules.File with
            {
                Grass = rules.File.Grass with { Chance = 0f },
                Rock = rules.File.Rock with { Chance = 0f },
                ShinyChance = 0f,
            });
        }
    }

    private void Start(RunState run)
    {
        var player = run.Player.Position;
        switch (_variant)
        {
            case "terra":
                for (var i = 0; i < Everything.Length; i++)
                    run.Loot.Drop(player + new Vector2(-105 + i * 35, 40), Everything[i]);

                // Phase 3.6: the Baccarancia a defeated Pokémon leaves, and held items to find (on the map, from shinies).
                run.Loot.Drop(player + new Vector2(-70, 85), LootKind.OranBerry);
                run.Loot.Drop(player + new Vector2(0, 85), LootKind.HeldItem, "magnet");
                run.Loot.Drop(player + new Vector2(70, 85), LootKind.HeldItem, "lucky-egg");
                break;

            case "raccolta":
            {
                // Rattata around and gems far away, so Repellente and Condividi Esp. have something to do.
                for (var i = 0; i < 12; i++)
                {
                    var angle = i * MathF.Tau / 12f;
                    run.Enemies.Spawn(Services.Pokedex.Value.Species[19 - 1], player + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * 150f, hpMultiplier: 50f);
                }

                for (var i = 0; i < 10; i++)
                    run.Pickups.Gems.Spawn(player + new Vector2(260 + i * 20, -120 + i * 25), 3);

                for (var i = 0; i < Everything.Length; i++)
                    run.Loot.Drop(player + new Vector2(30 + i * 18, (i % 2 == 0 ? 1 : -1) * 10), Everything[i]);
                break;
            }

            case "cromatico":
            {
                var pidgey = Services.Pokedex.Value.Species.Single(s => s.Key == "pidgey").Id;
                run.SpawnEnemy(pidgey, player + new Vector2(-150, -30));
                var shiny = run.SpawnEnemy(pidgey, player + new Vector2(150, -30));
                Spawner.MakeShiny(run, shiny);
                break;
            }

            default:
            {
                // Besides the map's own, a few boulders and tufts right around Pikachu, inside the leaves' orbit.
                (BreakableKind Kind, float X, float Y)[] near =
                [
                    (BreakableKind.Rock, 34, -22), (BreakableKind.Rock, -36, 18), (BreakableKind.Rock, 10, 44),
                    (BreakableKind.Grass, -30, -30), (BreakableKind.Grass, 40, 20), (BreakableKind.Grass, -8, -42), (BreakableKind.Grass, 70, -40),
                ];
                for (var i = 0; i < near.Length; i++)
                {
                    var (kind, x, y) = near[i];
                    run.Enemies.SpawnProp(kind, player + new Vector2(x, y), kind == BreakableKind.Rock ? 40f : 1f, kind == BreakableKind.Rock ? 80f : 10f,
                        kind == BreakableKind.Rock ? Core.Data.PokemonType.Rock : Core.Data.PokemonType.Grass, long.MaxValue - i);
                }

                for (var i = 0; i < 3; i++)
                    SpawnRattata(run, i * 2.1f);
                break;
            }
        }
    }

    private void SpawnRattata(RunState run, float angle) =>
        run.Enemies.Spawn(Services.Pokedex.Value.Species[19 - 1], run.Player.Position + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * 200f, hpMultiplier: 6f);
}
