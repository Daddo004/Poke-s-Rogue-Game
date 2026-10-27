using Microsoft.Xna.Framework.Graphics;
using PokeRogue.Core.Run;
using PokeRogue.Core.Run.Items;
using PokeRogue.Core.Run.Moves;
using PokeRogue.Game.Scenes;

namespace PokeRogue.Game.Preview;

/// <summary>
/// Phase 3.4 preview of the level boss, Pinsir. Pikachu has a mid-run build and endless PS (it still flinches), and
/// stands still so the boss' warnings can be read:
/// - arrivo: the entrance (bars, drop, landing, name) and the first attack, a charge;
/// - raffica: the volley first (sight lines, then the fan of X-Scissors);
/// - massi: second phase, the rockfall first (red circles, falling rocks);
/// - fasi: the boss starts just above its second phase, so the roar comes soon;
/// - finale: the last phase (faster, more rocks and shots) with little PS left, until it falls and the level is won.
/// </summary>
public sealed class BossPreviewScene : Scene
{
    private readonly GameplayScene _game;
    private readonly string _variant;
    private bool _summoned;

    public BossPreviewScene(GameServices services, string variant) : base(services)
    {
        _variant = variant.ToLowerInvariant();
        _game = new GameplayScene(services, setup: Prepare);
    }

    public override IReadOnlyList<string> DebugLines => _game.DebugLines;

    public override bool HandlesEscape => _game.HandlesEscape;

    public override void Enter() => _game.Enter();

    public override void Update()
    {
        var run = _game.Run;
        if (!_summoned)
        {
            _summoned = true;
            run.SummonBoss();
            if (run.Boss is { } fight)
            {
                // Each variant sets up the phase and the next attack, so the preview gets to it at once.
                var last = fight.Definition.Phases.Count - 1;
                (fight.Phase, fight.AttackCount, var hp) = _variant switch
                {
                    "fasi" => (0, 0, 0.605f),
                    "raffica" => (0, 1, 1f),
                    "massi" => (1, 1, 0.55f),
                    "finale" => (last, 2, 0.03f),
                    _ => (0, 0, 1f),
                };
                ref var boss = ref run.Enemies[fight.Index];
                boss.Hp = boss.MaxHp * hp;
            }
        }

        _game.Update();

        // Endless PS so the preview never ends early; hits still show.
        if (!run.IsOver)
            run.Player.Hp = run.Player.MaxHp;
    }

    public override void Draw(SpriteBatch batch) => _game.Draw(batch);

    private void Prepare(RunState run)
    {
        run.Spawner.Enabled = false;
        var moves = Services.Moves.Value;
        var items = Services.Items.Value;
        run.Player.Moves.Clear();
        run.Player.Moves.Add(new MoveInstance(moves["thundershock"], level: 8));
        run.Player.Moves.Add(new MoveInstance(moves["ember"], level: 6));
        run.Player.Moves.Add(new MoveInstance(moves["razor-leaf"], level: 6));
        run.Player.Items.Add(new ItemInstance(items["magnet"]) { Level = 3 });
        run.Player.Items.Add(new ItemInstance(items["leftovers"]) { Level = 2 });
        run.Player.RecalculateStats();
        run.AdvanceTime(RunClock.TicksPerClockMinute * 15);

        // A few of the forest's Pokémon around, to show the roar throwing them back.
        var keys = Services.Pokedex.Value.Species.ToDictionary(s => s.Key, s => s.Id);
        for (var i = 0; i < 6; i++)
        {
            var angle = i * MathF.Tau / 6f + 0.3f;
            run.SpawnEnemy(keys[i % 2 == 0 ? "caterpie" : "weedle"], run.Player.Position + new System.Numerics.Vector2(MathF.Cos(angle), MathF.Sin(angle)) * 190f, hpMultiplier: 60f);
        }
    }
}
