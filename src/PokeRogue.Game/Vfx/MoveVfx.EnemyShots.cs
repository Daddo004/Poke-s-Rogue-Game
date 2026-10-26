using System.Runtime.InteropServices;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PokeRogue.Core.Run;
using PokeRogue.Core.Run.Enemies;
using PokeRogue.Core.Run.Moves;
using PokeRogue.Game.Graphics;

namespace PokeRogue.Game.Vfx;

/// <summary>
/// Enemy shots (phase 3.2) borrow the look of a move, so they are drawn by the same code as the player's projectiles.
/// A red mark on the ground and a dark red rim tell them apart from the player's own shots.
/// </summary>
public sealed partial class MoveVfx
{
    private static readonly Color HostileRed = new(210, 36, 40);
    private static readonly Color HostileDark = new(90, 10, 20);

    /// <summary>The player's projectiles, then the enemy shots dressed as projectiles, rebuilt every update.</summary>
    private readonly List<Projectile> _views = new(256);
    private readonly Dictionary<MoveDefinition, MoveInstance> _shotMoves = [];
    private int _playerViews;

    private ReadOnlySpan<Projectile> ProjectileViews => CollectionsMarshal.AsSpan(_views);

    private ReadOnlySpan<Projectile> EnemyShotViews => CollectionsMarshal.AsSpan(_views)[_playerViews..];

    private void CollectProjectiles(RunState run)
    {
        _views.Clear();
        foreach (ref readonly var projectile in run.MoveSystem.Projectiles.Items)
        {
            if (projectile.Active && projectile.Move is not null)
                _views.Add(projectile);
        }

        _playerViews = _views.Count;
        foreach (ref readonly var shot in run.EnemyShots.Items)
        {
            if (shot.Active)
                _views.Add(AsProjectile(shot));
        }
    }

    private Projectile AsProjectile(in EnemyShot shot)
    {
        if (!_shotMoves.TryGetValue(shot.Move, out var move))
            _shotMoves[shot.Move] = move = new MoveInstance(shot.Move);

        return new Projectile
        {
            Active = true,
            Serial = shot.Serial,
            Move = move,
            Position = shot.Position,
            Velocity = shot.Velocity,
            Radius = shot.Radius,
            Angle = MathF.Atan2(shot.Velocity.Y, shot.Velocity.X),
        };
    }

    /// <summary>A puff at the shooter's mouth, in the move's colours.</summary>
    private void Muzzle(in RunEvent e)
    {
        if (Lookup(e.MoveId) is not { } info)
            return;

        var at = e.Position.ToXna() - new Vector2(0, BodyHeight);
        _dust.Burst(at, 4, 50f, info.Palette.Main, info.Palette.Dark * 0.4f, 10, _random, size: 2f);
    }

    /// <summary>Under each enemy shot, a red shadow on the ground where it will hit.</summary>
    private void DrawEnemyShotMarks(SpriteBatch batch)
    {
        var p = _services.Primitives;
        foreach (ref readonly var shot in EnemyShotViews)
        {
            var ground = shot.Position.ToXna();
            p.Ellipse(batch, ground, 11, 5, HostileRed * 0.4f);
            p.Ellipse(batch, ground, 5, 3, HostileDark * 0.6f);
        }
    }

    /// <summary>A dark red rim around each enemy shot, pulsing.</summary>
    private void DrawEnemyShotRims(SpriteBatch batch)
    {
        var p = _services.Primitives;
        foreach (ref readonly var shot in EnemyShotViews)
        {
            var at = shot.Position.ToXna() - new Vector2(0, BodyHeight);
            var pulse = (_tick + shot.Serial) % 16 < 8 ? 7f : 8f;
            p.Ring(batch, at, pulse, pulse, HostileRed * 0.85f);
        }
    }
}
