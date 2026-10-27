using System.Numerics;
using PokeRogue.Core.Run;

namespace PokeRogue.Game.Rendering;

/// <summary>
/// Smooth motion on screens faster than the 60 Hz tick (phase 8.6): a 144 Hz monitor shows each tick for 2 or 3 refreshes,
/// so things moved in uneven steps. Before each tick the positions are noted; a frame drawn between two ticks then shows
/// the player, the horde, the shots and the gems part of the way from the last tick's positions to the current ones.
/// <see cref="Apply"/> writes the blended positions into the run just for the drawing and <see cref="Restore"/> puts the
/// exact ones back, so the simulation never sees them.
/// </summary>
public sealed class MotionBlend
{
    /// <summary>A jump longer than this in one tick (teleport, a reused slot) is shown as it is, never slid.</summary>
    private const float MaxSlide = 40f;

    private readonly Snapshot _enemies = new();
    private readonly Snapshot _projectiles = new();
    private readonly Snapshot _shots = new();
    private readonly Snapshot _gems = new();
    private Vector2 _player;
    private Vector2 _playerNow;
    private long _tick = -1;
    private bool _applied;

    /// <summary>The run did not tick (a frozen window): draw the exact positions until the next tick.</summary>
    public void Invalidate() => _tick = -1;

    /// <summary>Notes where everything is, just before the run ticks.</summary>
    public void Capture(RunState run)
    {
        _tick = run.Ticks;
        _player = run.Player.Position;
        var enemies = run.Enemies.Items;
        _enemies.Begin(enemies.Length);
        for (var i = 0; i < enemies.Length; i++)
            _enemies.Set(i, enemies[i].Serial, enemies[i].Position);
        var projectiles = run.MoveSystem.Projectiles.Items;
        _projectiles.Begin(projectiles.Length);
        for (var i = 0; i < projectiles.Length; i++)
            _projectiles.Set(i, projectiles[i].Active ? projectiles[i].Serial : -1, projectiles[i].Position);
        var shots = run.EnemyShots.Items;
        _shots.Begin(shots.Length);
        for (var i = 0; i < shots.Length; i++)
            _shots.Set(i, shots[i].Active ? shots[i].Serial : -1, shots[i].Position);
        var gems = run.Pickups.Gems.Items;
        _gems.Begin(gems.Length);
        for (var i = 0; i < gems.Length; i++)
            _gems.Set(i, gems[i].Active ? gems[i].Serial : -1, gems[i].Position);
    }

    /// <summary>
    /// Moves everything back towards the last tick by 1 − <paramref name="blend"/> (0 = the last tick, 1 = the current one).
    /// Only when exactly one tick passed since <see cref="Capture"/>.
    /// </summary>
    public void Apply(RunState run, float blend)
    {
        _applied = false;
        if (_tick < 0 || run.Ticks != _tick + 1 || blend >= 1f)
            return;

        _applied = true;
        blend = Math.Clamp(blend, 0f, 1f);
        _playerNow = run.Player.Position;
        run.Player.Position = Slide(_player, _playerNow, blend);
        var enemies = run.Enemies.Items;
        for (var i = 0; i < enemies.Length; i++)
            _enemies.Blend(i, enemies[i].Serial, ref enemies[i].Position, blend);
        var projectiles = run.MoveSystem.Projectiles.Items;
        for (var i = 0; i < projectiles.Length; i++)
            _projectiles.Blend(i, projectiles[i].Active ? projectiles[i].Serial : -1, ref projectiles[i].Position, blend);
        var shots = run.EnemyShots.Items;
        for (var i = 0; i < shots.Length; i++)
            _shots.Blend(i, shots[i].Active ? shots[i].Serial : -1, ref shots[i].Position, blend);
        var gems = run.Pickups.Gems.Items;
        for (var i = 0; i < gems.Length; i++)
            _gems.Blend(i, gems[i].Active ? gems[i].Serial : -1, ref gems[i].Position, blend);
    }

    /// <summary>Puts back the exact positions <see cref="Apply"/> replaced.</summary>
    public void Restore(RunState run)
    {
        if (!_applied)
            return;

        _applied = false;
        run.Player.Position = _playerNow;
        var enemies = run.Enemies.Items;
        for (var i = 0; i < enemies.Length; i++)
            _enemies.Restore(i, ref enemies[i].Position);
        var projectiles = run.MoveSystem.Projectiles.Items;
        for (var i = 0; i < projectiles.Length; i++)
            _projectiles.Restore(i, ref projectiles[i].Position);
        var shots = run.EnemyShots.Items;
        for (var i = 0; i < shots.Length; i++)
            _shots.Restore(i, ref shots[i].Position);
        var gems = run.Pickups.Gems.Items;
        for (var i = 0; i < gems.Length; i++)
            _gems.Restore(i, ref gems[i].Position);
    }

    private static Vector2 Slide(Vector2 from, Vector2 to, float blend) =>
        Vector2.DistanceSquared(from, to) > MaxSlide * MaxSlide ? to : Vector2.Lerp(from, to, blend);

    /// <summary>The positions of one pool before the tick, by slot, plus the exact ones while blended.</summary>
    private sealed class Snapshot
    {
        private int[] _serials = new int[256];
        private Vector2[] _before = new Vector2[256];
        private Vector2[] _now = new Vector2[256];
        private int _count;

        public void Begin(int count)
        {
            if (_serials.Length < count)
            {
                var size = Math.Max(count, _serials.Length * 2);
                Array.Resize(ref _serials, size);
                Array.Resize(ref _before, size);
                Array.Resize(ref _now, size);
            }

            _count = count;
        }

        public void Set(int slot, int serial, Vector2 position)
        {
            _serials[slot] = serial;
            _before[slot] = position;
        }

        public void Blend(int slot, int serial, ref Vector2 position, float blend)
        {
            if (slot >= _now.Length)
                return;

            _now[slot] = position;
            if (slot < _count && serial >= 0 && _serials[slot] == serial)
                position = Slide(_before[slot], position, blend);
        }

        public void Restore(int slot, ref Vector2 position)
        {
            if (slot < _now.Length)
                position = _now[slot];
        }
    }
}
