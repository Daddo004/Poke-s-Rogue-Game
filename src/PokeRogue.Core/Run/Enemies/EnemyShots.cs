using System.Numerics;
using PokeRogue.Core.Data;
using PokeRogue.Core.Run.Moves;

namespace PokeRogue.Core.Run.Enemies;

/// <summary>A shot fired by a ranged enemy: it flies straight and hurts the player on contact.</summary>
public struct EnemyShot
{
    public bool Active;
    public int Serial;

    /// <summary>Where it touches the ground; renderers draw it at body height above.</summary>
    public Vector2 Position;

    public Vector2 Velocity;
    public float Radius;
    public float Damage;

    /// <summary>Meets Special Defense and Zinco rather than Defense and Ferro.</summary>
    public bool Special;

    public int TicksLeft;

    /// <summary>The move whose look and type it borrows.</summary>
    public MoveDefinition Move;

    public int SpeciesId;
}

/// <summary>Enemy shots (phase 3.2). They pop on the player, hurting it unless it is invulnerable, or fade out of range.</summary>
public sealed class EnemyShotPool(int capacity)
{
    public const float ShotRadius = 4f;

    private readonly EnemyShot[] _items = new EnemyShot[capacity];
    private int _serial;

    public int HighWater { get; private set; }

    public int ActiveCount { get; private set; }

    public Span<EnemyShot> Items => _items.AsSpan(0, HighWater);

    public void Fire(RunState run, Vector2 from, Vector2 velocity, float damage, MoveDefinition move, int speciesId, int lifetimeTicks)
    {
        var index = Array.FindIndex(_items, s => !s.Active);
        if (index < 0)
            return;

        HighWater = Math.Max(HighWater, index + 1);
        ActiveCount++;
        _items[index] = new EnemyShot
        {
            Active = true,
            Serial = ++_serial,
            Position = from,
            Velocity = velocity,
            Radius = ShotRadius,
            Damage = damage,
            Special = move.Category == MoveCategory.Special,
            TicksLeft = lifetimeTicks,
            Move = move,
            SpeciesId = speciesId,
        };
        run.AddEvent(new RunEvent(RunEventType.EnemyShotFired, from, 0f, speciesId, Target: velocity, MoveId: move.Id));
    }

    public void Tick(RunState run)
    {
        var player = run.Player;
        for (var i = 0; i < HighWater; i++)
        {
            ref var shot = ref _items[i];
            if (!shot.Active)
                continue;

            shot.Position += shot.Velocity * RunClock.Dt;
            var reach = shot.Radius + player.Radius;
            if (Vector2.DistanceSquared(shot.Position, player.Position) < reach * reach && !player.IsDown)
            {
                var taken = player.TakeContactDamage(shot.Damage, shot.Special);
                if (taken > 0f)
                {
                    run.Stats.RecordDamageTaken(taken);
                    run.AddEvent(new RunEvent(RunEventType.PlayerHurt, player.Position, taken, shot.SpeciesId));
                }

                End(run, ref shot, hit: true);
                continue;
            }

            if (--shot.TicksLeft <= 0)
                End(run, ref shot, hit: false);
        }
    }

    /// <summary>The run is over: every shot vanishes.</summary>
    public void Clear()
    {
        for (var i = 0; i < HighWater; i++)
            _items[i].Active = false;
        ActiveCount = 0;
    }

    private void End(RunState run, ref EnemyShot shot, bool hit)
    {
        shot.Active = false;
        ActiveCount--;
        run.AddEvent(new RunEvent(RunEventType.EnemyShotEnded, shot.Position, hit ? 1f : 0f, shot.SpeciesId, MoveId: shot.Move.Id));
    }
}
