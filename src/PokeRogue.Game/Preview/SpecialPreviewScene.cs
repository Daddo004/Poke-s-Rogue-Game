using Microsoft.Xna.Framework.Graphics;
using PokeRogue.Core.Data;
using PokeRogue.Core.Run;
using PokeRogue.Game.Scenes;

namespace PokeRogue.Game.Preview;

/// <summary>
/// Phase 4.4 preview of a special move: a character of the type (--tipo Fire; any line can lend it, e.g. Poison has no
/// user yet) stands among Rattata, tall grass and a Dominante with a full gauge, and the autopilot unleashes it at once.
/// --soloeffetto leaves out the cut-in band, for stills of the picture.
/// </summary>
public sealed class SpecialPreviewScene : Scene
{
    private readonly GameplayScene _game;
    private bool _started;

    public SpecialPreviewScene(GameServices services, PokemonType type, bool pictureOnly) : base(services)
    {
        var characters = services.Characters.All;
        var character = characters.FirstOrDefault(c => c.Special == type && c.Group != Core.Run.Characters.CharacterGroup.Legendary)
            ?? characters.First(c => c.Id == "bulbasaur") with { Special = type };
        _game = new GameplayScene(services, autopilot: true, setup: Prepare) { Character = character };
        _game.Special.SkipCutIn = pictureOnly;
    }

    public override IReadOnlyList<string> DebugLines => _game.DebugLines;

    public override bool HandlesEscape => _game.HandlesEscape;

    public override void Enter() => _game.Enter();

    public override void Update()
    {
        if (!_started)
        {
            _started = true;
            _game.Run.Special.Charge = 1f;
        }

        _game.Update();
    }

    public override void Draw(SpriteBatch batch) => _game.Draw(batch);

    private void Prepare(RunState run)
    {
        run.Spawner.Enabled = false;
        var keys = Services.Pokedex.Value.Species.ToDictionary(s => s.Key, s => s.Id);
        string[] crowd = ["rattata", "caterpie", "pidgey", "geodude", "oddish", "zubat"];
        for (var i = 0; i < 24; i++)
        {
            var angle = i * MathF.Tau / 24f;
            var distance = 90f + i % 3 * 50f;
            run.SpawnEnemy(keys[crowd[i % crowd.Length]], run.Player.Position + new System.Numerics.Vector2(MathF.Cos(angle) * distance * 1.6f, MathF.Sin(angle) * distance));
        }

        var dominant = run.SpawnEnemy(keys["pidgeotto"], run.Player.Position + new System.Numerics.Vector2(200, -60), hpMultiplier: 20f);
        Spawner.MakeDominant(run, dominant, ChestTier.Poke);
    }
}
