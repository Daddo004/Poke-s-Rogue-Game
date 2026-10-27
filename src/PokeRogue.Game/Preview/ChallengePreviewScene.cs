using Microsoft.Xna.Framework.Graphics;
using PokeRogue.Core.Run;
using PokeRogue.Game.Scenes;

namespace PokeRogue.Game.Preview;

/// <summary>
/// Phase 5.3 preview of a trainer's challenge, on Bosco Smeraldo one second before the time runs out, Pikachu on autopilot:
/// - arrivo: the trainer steps in and sends out the first Pokémon;
/// - cambio: the first Pokémon faints (knocked out by the preview) and the next one comes out;
/// - squadra: the fight against the ace, with the team's Poké Balls under the boss bar;
/// - vittoria: the whole team faints and the trainer is beaten.
/// </summary>
public sealed class ChallengePreviewScene : Scene
{
    private readonly string _variant;
    private readonly GameplayScene _game;
    private int _tick;

    public ChallengePreviewScene(GameServices services, string variant, string trainerId) : base(services)
    {
        _variant = variant.ToLowerInvariant();
        var waves = Core.Run.Waves.WaveTimeline.Load(services.ContentRoot, GameplayScene.DefaultLevelId, services.Pokedex.Value.Species, services.Moves.Value);
        _game = new GameplayScene(services, "pikachu", autopilot: true)
        {
            ChallengeId = trainerId,
            StartTicks = waves.DurationTicks - RunClock.Seconds(1f),
        };
    }

    public override IReadOnlyList<string> DebugLines => _game.DebugLines;

    public override bool HandlesEscape => _game.HandlesEscape;

    public override void Enter() => _game.Enter();

    public override void Update()
    {
        _tick++;
        var run = _game.Run;

        // The preview knocks the trainer's Pokémon out itself, so it does not wait for Pikachu's level-one moves.
        var knockOut = _variant switch
        {
            "cambio" => run.Challenge!.SentOut == 1 && _tick > 300,
            "squadra" => run.Challenge!.SentOut < run.Challenge.Team.Count && _tick > 260,
            "vittoria" => _tick > 260,
            _ => false,
        };
        if (knockOut && run.Boss is { DefeatedTick: null } fight && run.Enemies[fight.Index].IsAlive)
            run.DamageEnemy(fight.Index, float.MaxValue);

        _game.Update();
    }

    public override void Draw(SpriteBatch batch) => _game.Draw(batch);
}
