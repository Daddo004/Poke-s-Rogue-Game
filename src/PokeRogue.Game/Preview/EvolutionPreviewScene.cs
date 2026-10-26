using Microsoft.Xna.Framework.Graphics;
using PokeRogue.Core.Run;
using PokeRogue.Core.Run.Progression;
using PokeRogue.Game.Scenes;

namespace PokeRogue.Game.Preview;

/// <summary>
/// Phase 4.2 preview of the player's evolution, on autopilot:
/// - sequenza: Charmander reaches level 14 and becomes Charmeleon (or --pokemon id, at its first evolution's level);
/// - fermata: the same, but the player stops it halfway;
/// - scelta: Eevee at level 20 chooses among its seven evolutions (--scelta N picks the Nth, default Umbreon);
/// - forziere: Pikachu at level 14 opens a Poké Ball, finds the Pietratuono and becomes Raichu.
/// </summary>
public sealed class EvolutionPreviewScene : Scene
{
    private readonly GameplayScene _game;
    private readonly string _variant;
    private bool _started;

    public EvolutionPreviewScene(GameServices services, string variant, string? pokemon, int choice) : base(services)
    {
        _variant = variant.ToLowerInvariant();
        var character = _variant switch
        {
            "scelta" => "eevee",
            "forziere" => "pikachu",
            _ => pokemon ?? "charmander",
        };
        _game = new GameplayScene(services, character, autopilot: true, setup: Prepare);
        _game.Evolution.ScriptedChoice = choice;
        if (_variant == "fermata")
            _game.Evolution.ScriptedStopTick = 150;
    }

    public override IReadOnlyList<string> DebugLines => _game.DebugLines;

    public override bool HandlesEscape => _game.HandlesEscape;

    public override void Enter() => _game.Enter();

    public override void Update()
    {
        if (_variant == "forziere" && !_started)
        {
            _started = true;
            _game.ChestWindow.Open(_game.Run.OpenChest(ChestTier.Poke, _game.Run.Player.Position));
        }

        _game.Update();
    }

    public override void Draw(SpriteBatch batch) => _game.Draw(batch);

    private void Prepare(RunState run)
    {
        run.Spawner.Enabled = false;

        // Straight to the level of the first evolution, without the level-up cards on the way.
        var first = run.Character!.EvolutionsOf(run.Stage!).First();
        var level = first.Level ?? Core.Run.Characters.CharacterEvolution.ItemLevel(first);
        while (run.Player.Level < level)
            run.Player.AddXp(run.Player.XpToNext - run.Player.Xp);

        // A few wild Pokémon around, so the rings and sparks after the window land in a real field.
        var caterpie = Services.Pokedex.Value.Species.Single(s => s.Key == "caterpie").Id;
        for (var i = 0; i < 6; i++)
        {
            var angle = i * MathF.Tau / 6f + 0.3f;
            run.SpawnEnemy(caterpie, run.Player.Position + new System.Numerics.Vector2(MathF.Cos(angle), MathF.Sin(angle)) * 190f);
        }
    }
}
