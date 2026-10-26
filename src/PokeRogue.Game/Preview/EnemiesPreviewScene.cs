using Microsoft.Xna.Framework.Graphics;
using PokeRogue.Core.Run;
using PokeRogue.Core.Run.Enemies;
using PokeRogue.Core.Run.Moves;
using PokeRogue.Game.Scenes;

namespace PokeRogue.Game.Preview;

/// <summary>
/// Phase 3.2 preview of the enemy behaviours, approved or not. Pikachu stands still with endless PS (it still flinches
/// when hit) while a few enemies of one behaviour come at it; when they are all gone a new group arrives.
/// </summary>
public sealed class EnemiesPreviewScene : Scene
{
    /// <summary>Ticks between the last enemy of a group leaving and the next group arriving.</summary>
    private const int RespawnDelay = 60;

    private readonly GameplayScene _game;
    private readonly (string Pokemon, float Angle, float Distance)[] _group;
    private readonly bool _withMove;
    private int _emptyTicks;

    public EnemiesPreviewScene(GameServices services, string variant) : base(services)
    {
        variant = variant.ToLowerInvariant();
        _group = Group(variant);
        _withMove = variant == "tank";
        _game = new GameplayScene(services, setup: Prepare);
    }

    private static (string Pokemon, float Angle, float Distance)[] Group(string variant) => variant switch
    {
        "tiratore" => [("beedrill", 0.3f, 190f), ("butterfree", 2.4f, 200f), ("beedrill", 4.2f, 180f)],
        "caricatore" => [("tauros", 0.2f, 230f), ("raticate", 3.4f, 200f)],

        // Tanks next to a plain Caterpie, so Thundershock's push shows the difference.
        "tank" => [("metapod", 0f, 120f), ("kakuna", 2.1f, 120f), ("caterpie", 4.2f, 120f)],
        "kamikaze" => [("voltorb", 0.4f, 200f), ("voltorb", 2.5f, 230f), ("voltorb", 4.4f, 260f)],
        "teletrasporto" => [("abra", 0.8f, 150f), ("abra", 3.9f, 150f)],
        _ => [("beedrill", 0.3f, 190f), ("tauros", 1.6f, 230f), ("metapod", 2.8f, 140f), ("voltorb", 4.0f, 240f), ("abra", 5.2f, 150f)],
    };

    public override IReadOnlyList<string> DebugLines => _game.DebugLines;

    public override bool HandlesEscape => _game.HandlesEscape;

    public override void Enter() => _game.Enter();

    public override void Update()
    {
        _game.Update();
        var run = _game.Run;

        // Endless PS, so the preview never ends; the hurt flash and blink still show every hit.
        run.Player.Hp = run.Player.MaxHp;

        var anyone = false;
        foreach (ref readonly var e in run.Enemies.Items)
            anyone |= e.State != EnemyState.Inactive;

        if (anyone)
        {
            _emptyTicks = 0;
        }
        else if (++_emptyTicks >= RespawnDelay)
        {
            _emptyTicks = 0;
            SpawnGroup(run);
        }
    }

    public override void Draw(SpriteBatch batch) => _game.Draw(batch);

    private void Prepare(RunState run)
    {
        run.Spawner.Enabled = false;
        run.EnemyRules = EnemyLibrary.Load(Services.ContentRoot, Services.Pokedex.Value.Species, Services.Moves.Value).WithDisabled();
        run.Player.Moves.Clear();
        if (_withMove)
            run.Player.Moves.Add(new MoveInstance(Services.Moves.Value[GameplayScene.StartingMoveId], level: 8));
        SpawnGroup(run);
    }

    private void SpawnGroup(RunState run)
    {
        var keys = Services.Pokedex.Value.Species.ToDictionary(s => s.Key, s => s.Id);
        foreach (var (pokemon, angle, distance) in _group)
        {
            var at = run.Player.Position + new System.Numerics.Vector2(MathF.Cos(angle), MathF.Sin(angle)) * distance;
            run.SpawnEnemy(keys[pokemon], at, hpMultiplier: _withMove ? 6f : 1f);
        }
    }
}
