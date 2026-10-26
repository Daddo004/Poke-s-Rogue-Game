using Microsoft.Xna.Framework.Graphics;
using PokeRogue.Core.Run;
using PokeRogue.Core.Run.Items;
using PokeRogue.Core.Run.Moves;
using PokeRogue.Game.Scenes;

namespace PokeRogue.Game.Preview;

/// <summary>
/// Phase 3.3 preview of the Dominanti and their chests:
/// - campo: a Dominante Pidgeotto with its aura comes at a Pikachu with a few moves, falls, drops a Poké Ball, and the
///   autopilot walks over to open it;
/// - arrivo: a Dominante Beedrill arrives from off screen (the announcement and the arrow at the edge);
/// - forziere: a chest opens at once (--ball poke|mega|ultra); the Ultra Ball starts with a ready evolution.
/// </summary>
public sealed class DominantsPreviewScene : Scene
{
    private readonly GameplayScene _game;
    private readonly string _variant;
    private readonly ChestTier _tier;
    private bool _opened;

    public DominantsPreviewScene(GameServices services, string variant, string? ball) : base(services)
    {
        _variant = variant.ToLowerInvariant();
        _tier = ball?.ToLowerInvariant() switch
        {
            "mega" => ChestTier.Great,
            "ultra" => ChestTier.Ultra,
            _ => ChestTier.Poke,
        };
        _game = new GameplayScene(services, autopilot: _variant is not ("forziere" or "terra"), setup: Prepare);
    }

    public override IReadOnlyList<string> DebugLines => _game.DebugLines;

    public override bool HandlesEscape => _game.HandlesEscape;

    public override void Enter() => _game.Enter();

    public override void Update()
    {
        if (_variant == "forziere" && !_opened)
        {
            _opened = true;
            _game.ChestWindow.Open(_game.Run.OpenChest(_tier, _game.Run.Player.Position));
        }

        _game.Update();
    }

    public override void Draw(SpriteBatch batch) => _game.Draw(batch);

    private void Prepare(RunState run)
    {
        run.Spawner.Enabled = false;
        var moves = Services.Moves.Value;
        var items = Services.Items.Value;
        run.Player.Moves.Clear();
        run.Player.Moves.Add(new MoveInstance(moves[GameplayScene.StartingMoveId], level: _tier == ChestTier.Ultra ? 8 : 5));
        run.Player.Moves.Add(new MoveInstance(moves["ember"], level: 4));
        run.Player.Moves.Add(new MoveInstance(moves["razor-leaf"], level: 3));
        run.Player.Items.Add(new ItemInstance(items["magnet"]) { Level = 2 });
        run.Player.Items.Add(new ItemInstance(items["leftovers"]) { Level = 1 });
        run.Player.RecalculateStats();

        var keys = Services.Pokedex.Value.Species.ToDictionary(s => s.Key, s => s.Id);
        switch (_variant)
        {
            case "campo":
            {
                var index = run.SpawnEnemy(keys["pidgeotto"], run.Player.Position + new System.Numerics.Vector2(170, -40), hpMultiplier: 18f);
                Spawner.MakeDominant(run, index, ChestTier.Poke);
                for (var i = 0; i < 6; i++)
                {
                    var angle = i * MathF.Tau / 6f + 0.4f;
                    run.SpawnEnemy(keys["caterpie"], run.Player.Position + new System.Numerics.Vector2(MathF.Cos(angle), MathF.Sin(angle)) * 210f);
                }

                break;
            }

            case "terra":
            {
                // The three Balls popping out on the ground, the Ultra Ball off screen behind its arrow.
                var at = run.Player.Position;
                run.Pickups.Chests.Add(new Core.Run.Pickups.Chest(1, at + new System.Numerics.Vector2(-90, 30), ChestTier.Poke));
                run.Pickups.Chests.Add(new Core.Run.Pickups.Chest(2, at + new System.Numerics.Vector2(90, 30), ChestTier.Great));
                run.Pickups.Chests.Add(new Core.Run.Pickups.Chest(3, at + new System.Numerics.Vector2(480, -60), ChestTier.Ultra));
                break;
            }

            case "arrivo":
            {
                var index = run.SpawnEnemy(keys["beedrill"], run.Player.Position + new System.Numerics.Vector2(420, 120), hpMultiplier: 40f);
                Spawner.MakeDominant(run, index, ChestTier.Great);
                break;
            }
        }
    }
}
