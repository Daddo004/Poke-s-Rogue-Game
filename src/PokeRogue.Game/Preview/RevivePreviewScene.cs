using Microsoft.Xna.Framework.Graphics;
using PokeRogue.Core.Run;
using PokeRogue.Core.Run.Items;
using PokeRogue.Game.Scenes;

namespace PokeRogue.Game.Preview;

/// <summary>
/// Phase 2.5 preview of Focalnastro: Pikachu holds the sash with 4 PS and no moves, a ring of Rattata closes in,
/// knocks it out, and it stands back up with half its PS.
/// </summary>
public sealed class RevivePreviewScene : Scene
{
    private readonly GameplayScene _game;

    public RevivePreviewScene(GameServices services) : base(services)
    {
        _game = new GameplayScene(services, setup: Prepare);
    }

    public override IReadOnlyList<string> DebugLines => _game.DebugLines;

    public override void Enter() => _game.Enter();

    public override void Update() => _game.Update();

    public override void Draw(SpriteBatch batch) => _game.Draw(batch);

    private void Prepare(RunState run)
    {
        run.Spawner.Enabled = false;
        run.Player.Moves.Clear();
        run.Player.Items.Add(new ItemInstance(Services.Items.Value["focus-sash"]));
        run.Player.RecalculateStats();
        run.Player.Hp = 4f;

        for (var i = 0; i < 10; i++)
        {
            var angle = i * MathF.Tau / 10f;
            var at = run.Player.Position + new System.Numerics.Vector2(MathF.Cos(angle), MathF.Sin(angle)) * 70f;
            run.Enemies.Spawn(Services.Pokedex.Value.Species[19 - 1], at);
        }
    }
}
