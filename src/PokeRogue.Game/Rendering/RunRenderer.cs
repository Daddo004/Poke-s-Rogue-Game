using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PokeRogue.Core.Combat;
using PokeRogue.Core.Data;
using PokeRogue.Core.Geometry;
using PokeRogue.Core.Run;
using PokeRogue.Game.Graphics;
using PokeRogue.Game.Graphics.Floor;
using PokeRogue.Game.Vfx;

namespace PokeRogue.Game.Rendering;

/// <summary>
/// Draws a <see cref="RunState"/>: floor, y-sorted props and Pokémon, projectiles, arcs, particles and damage numbers.
/// Owns all presentation-only state (animators, VFX); the simulation never depends on it.
/// </summary>
public sealed class RunRenderer
{
    /// <summary>Projectiles and bolts are drawn this far above the feet, roughly at body height.</summary>
    public const float BodyHeight = 12f;

    private static readonly Color Ink = new(16, 12, 28);

    private readonly GameServices _services;
    private readonly List<DrawItem> _drawList = new(4096);
    private readonly List<FloorProp> _props = [];
    private readonly MoveVfx _vfx;
    private readonly StatusVfx _statusVfx;
    private readonly List<int> _levelBursts = [];

    /// <summary>Ages of Focalnastro revivals still showing (rings and the sash rising over Pikachu).</summary>
    private readonly List<int> _revivals = [];

    /// <summary>Move evolutions still showing (gold rings and the new move's icon rising over Pikachu).</summary>
    private readonly List<(int Age, string MoveId)> _evolutions = [];
    private readonly List<DebugShape> _debugShapes = [];
    private readonly Particles _particles = new();
    private readonly DamageNumbers _numbers = new();
    private readonly Random _random = new(5);

    private SpriteAnimator _playerAnimator;
    private SpriteAnimator[] _enemyAnimators = [];
    private int[] _enemySerials = [];
    private int _hurtTicks;
    private int _attackTicks;
    private Direction8 _attackFacing;
    private int _tick;

    public RunRenderer(GameServices services, FloorStyle floorStyle)
    {
        _services = services;
        Floor = new FloorRenderer(services.Device, floorStyle);
        Camera = new Camera2D(services.Screen.Width, services.Screen.Height);
        _vfx = new MoveVfx(services);
        _statusVfx = new StatusVfx(services);
    }

    public FloorRenderer Floor { get; }

    public Camera2D Camera { get; }

    public int ParticleCount => _particles.Active + _vfx.ParticleCount + _statusVfx.ParticleCount;

    public ScreenShake Shake => _vfx.Shake;

    /// <summary>Status conditions on enemies; previews pick the icon style here.</summary>
    public StatusVfx Statuses => _statusVfx;

    /// <summary>Developer view: outlines of every move hitbox (F12 in a run, on by default in the Move Lab).</summary>
    public bool ShowHitboxes { get; set; }

    /// <summary>Previews that only show enemies hide the player (the camera still follows it).</summary>
    public bool DrawPlayer { get; set; } = true;

    public void Reset(RunState run)
    {
        _enemyAnimators = new SpriteAnimator[run.Enemies.Capacity];
        _enemySerials = new int[run.Enemies.Capacity];

        // Ready before the first tick: a run can start paused (e.g. a level-up offered immediately).
        _playerAnimator = default;
        _playerAnimator.Play(_services.Sprites.Get(run.Player.Species.Id)!.Resolve(PmdAnimations.Idle));
        _hurtTicks = _attackTicks = 0;
        _vfx.Reset();
        _statusVfx.Reset();
        _levelBursts.Clear();
        _revivals.Clear();
        _evolutions.Clear();
        _debugShapes.Clear();
    }

    /// <summary>
    /// Consumes this tick's events and advances animations. Call once after every <see cref="RunState.Tick"/>.
    /// With <paramref name="paused"/> (level-up), Pokémon freeze and only effects keep playing.
    /// </summary>
    public void Update(RunState run, bool paused = false)
    {
        _tick++;
        UpdateEffects();
        _vfx.Update(run, paused);
        _statusVfx.Update(run, paused, Cull());
        if (paused)
            return;

        var playerSet = _services.Sprites.Get(run.Player.Species.Id)!;

        foreach (var e in run.Events)
        {
            _vfx.OnEvent(e, run);
            _statusVfx.OnEvent(e);
            switch (e.Type)
            {
                case RunEventType.PlayerHurt:
                    _hurtTicks = 12;
                    break;
                case RunEventType.EnemyHit:
                    _numbers.Add(e.Position.ToXna(), e.Value, e.Effectiveness, e.Critical, _random, e.Ohko);
                    break;
                case RunEventType.StatusDamage:
                    var art = StatusIcons.Art(e.Status);
                    _numbers.AddStatus(e.Position.ToXna(), e.Value, Color.Lerp(art.Main, art.Light, 0.35f), _random);
                    break;
                case RunEventType.Slash or RunEventType.AuraPulse or RunEventType.StrikeImpact:
                    _debugShapes.Add(new DebugShape(e.Type, e.Position.ToXna(), e.Target.ToXna(), e.Value));
                    break;
                case RunEventType.MoveUsed:
                    var attack = playerSet.Anim(PmdAnimations.Shoot) ?? playerSet.Resolve(PmdAnimations.Attack);
                    _playerAnimator.Play(attack, loop: false, restart: true);
                    _attackTicks = attack.TotalTicks;
                    _attackFacing = Direction8Extensions.FromVector(e.Target - e.Position, run.Player.Facing);
                    break;
                case RunEventType.GemCollected:
                    var color = PickupArt.GemColor(Core.Run.Pickups.Gem.TierOf((int)e.Value));
                    _particles.Burst(run.Player.Position.ToXna() - new Vector2(0, 8), 3, 70f, Color.White, color, 12, _random);
                    break;
                case RunEventType.LevelUp:
                    _levelBursts.Add(0);
                    _particles.Burst(run.Player.Position.ToXna() - new Vector2(0, 10), 28, 150f, new Color(255, 250, 210), new Color(248, 192, 48), 30, _random, size: 2f);
                    break;
                case RunEventType.PlayerRevived:
                    // Focalnastro: a white and pink flash, the sash rising over Pikachu, a jolt of the camera.
                    _revivals.Add(0);
                    _particles.Burst(run.Player.Position.ToXna() - new Vector2(0, 10), 36, 170f, Color.White, ReviveRed, 34, _random, size: 2f);
                    _vfx.Shake.Add(0.25f);
                    break;
                case RunEventType.MoveEvolved when e.MoveId is not null:
                    // A move evolved: gold rings, a shower of gold and white sparks, the new icon rising over Pikachu.
                    _evolutions.Add((0, e.MoveId));
                    _particles.Burst(run.Player.Position.ToXna() - new Vector2(0, 10), 40, 180f, Color.White, EvolveGold, 36, _random, size: 2f);
                    _vfx.Shake.Add(0.2f);
                    break;
                case RunEventType.RunEnded when (RunOutcome)(int)e.Value == RunOutcome.Victory:
                    _levelBursts.Add(0);
                    _particles.Burst(run.Player.Position.ToXna() - new Vector2(0, 10), 48, 190f, Color.White, new Color(248, 208, 72), 40, _random, size: 2f);
                    break;
            }
        }

        if (_hurtTicks > 0)
            _hurtTicks--;

        UpdateAnimators(run, playerSet);
    }

    /// <summary>Effects that keep moving even while the game is frozen.</summary>
    private void UpdateEffects()
    {
        for (var i = _levelBursts.Count - 1; i >= 0; i--)
        {
            if (++_levelBursts[i] >= LevelBurstLife)
                _levelBursts.RemoveAt(i);
        }

        for (var i = _revivals.Count - 1; i >= 0; i--)
        {
            if (++_revivals[i] >= ReviveLife)
                _revivals.RemoveAt(i);
        }

        for (var i = _evolutions.Count - 1; i >= 0; i--)
        {
            _evolutions[i] = (_evolutions[i].Age + 1, _evolutions[i].MoveId);
            if (_evolutions[i].Age >= EvolveLife)
                _evolutions.RemoveAt(i);
        }

        for (var i = _debugShapes.Count - 1; i >= 0; i--)
        {
            _debugShapes[i] = _debugShapes[i] with { Age = _debugShapes[i].Age + 1 };
            if (_debugShapes[i].Age >= DebugShape.Life)
                _debugShapes.RemoveAt(i);
        }

        _particles.Update();
        _numbers.Update();
    }

    private const int LevelBurstLife = 30;

    /// <summary>How long a Focalnastro revival shows: rings in the first half, the rising sash throughout.</summary>
    private const int ReviveLife = 72;

    private static readonly Color ReviveRed = new(240, 80, 90);

    /// <summary>How long a move evolution shows: rings in the first half, the rising icon throughout.</summary>
    private const int EvolveLife = 80;

    private static readonly Color EvolveGold = new(248, 200, 56);

    /// <summary>The visible world plus a margin, for culling sprites and effects.</summary>
    private Rectangle Cull()
    {
        var view = Camera.VisibleWorld;
        return new Rectangle(view.X - 48, view.Y - 48, view.Width + 96, view.Height + 96);
    }

    public void Draw(SpriteBatch batch, RunState run)
    {
        var p = _services.Primitives;
        Camera.Position = run.Player.Position.ToXna();
        Camera.Shake = _vfx.Shake.Offset;
        var view = Camera.VisibleWorld;
        var cull = Cull();

        batch.Begin(samplerState: SamplerState.PointClamp, transformMatrix: Camera.Transform);
        Floor.Draw(batch, view);
        _vfx.DrawGround(batch, run);
        DrawGems(batch, run, cull);
        BuildDrawList(run, cull);
        DrawShadows(batch, run);
        foreach (var item in _drawList)
            DrawItemSprite(batch, run, item);
        _vfx.DrawObjects(batch, run);
        _statusVfx.DrawObjects(batch);

        var player = run.Player;
        var hpRatio = player.Hp / player.MaxHp;
        var feet = player.Position.ToXna();
        if (!run.IsOver && DrawPlayer)
            UiDraw.Bar(batch, p, new Rectangle((int)feet.X - 13, (int)feet.Y + 5, 26, 4), hpRatio, UiDraw.HpColor(hpRatio), new Color(40, 24, 32), Ink);
        batch.End();

        batch.Begin(blendState: BlendState.Additive, samplerState: SamplerState.PointClamp, transformMatrix: Camera.Transform);
        _vfx.DrawGlow(batch, run);
        DrawStatusGlow(batch, run);
        _statusVfx.DrawGlow(batch);

        // Level-up shock waves: two flattened rings expanding from the player's feet.
        foreach (var age in _levelBursts)
        {
            for (var ring = 0; ring < 2; ring++)
            {
                var a = age - ring * 7;
                if (a < 0)
                    continue;

                var t = a / (float)LevelBurstLife;
                var radius = 6f + a * 2.6f;
                var color = Color.Lerp(new Color(255, 250, 210), new Color(248, 192, 48), t) * (1f - t);
                p.Ring(batch, feet, radius, radius * 0.5f, color, 2);
            }
        }

        // Focalnastro: three white rings turning pink as they spread from Pikachu's feet.
        foreach (var age in _revivals)
        {
            for (var ring = 0; ring < 3; ring++)
            {
                var a = age - ring * 6;
                var t = a / (ReviveLife * 0.5f);
                if (a < 0 || t > 1f)
                    continue;

                var radius = 8f + a * 3.2f;
                p.Ring(batch, feet, radius, radius * 0.5f, Color.Lerp(Color.White, ReviveRed, t) * (1f - t), 2);
            }
        }

        // Evolution: three white rings turning gold as they spread from Pikachu's feet.
        foreach (var (age, _) in _evolutions)
        {
            for (var ring = 0; ring < 3; ring++)
            {
                var a = age - ring * 6;
                var t = a / (EvolveLife * 0.5f);
                if (a < 0 || t > 1f)
                    continue;

                var radius = 8f + a * 3.4f;
                p.Ring(batch, feet, radius, radius * 0.5f, Color.Lerp(Color.White, EvolveGold, t) * (1f - t), 2);
            }
        }

        _particles.Draw(batch, p);
        batch.End();

        batch.Begin(samplerState: SamplerState.PointClamp, transformMatrix: Camera.Transform);
        _statusVfx.DrawOverhead(batch, run, cull);
        DrawRevivals(batch, run);
        DrawEvolutions(batch, run);
        _numbers.Draw(batch, _services.Fonts.Numbers(_services.Style), _services.Fonts.Get(_services.Style.NumberFont, _services.Style.NumberFontSize / 2f));
        if (ShowHitboxes)
            DrawHitboxes(batch, run);
        batch.End();
    }

    /// <summary>The Focalnastro icon popping up over Pikachu's head and drifting higher as it fades.</summary>
    private void DrawRevivals(SpriteBatch batch, RunState run)
    {
        if (_revivals.Count == 0)
            return;

        var age = _revivals[^1];
        var t = age / (float)ReviveLife;
        var pop = age < 6 ? 1.5f - age / 12f : 1f;
        var size = (int)(ItemIcons.Crop.Width * pop);
        var head = _services.Sprites.Get(run.Player.Species.Id)?.HeadHeight ?? 20;
        var center = run.Player.Position.ToXna() - new Vector2(0, head + 14 + t * 14f);
        var alpha = t < 0.7f ? 1f : (1f - t) / 0.3f;
        _services.ItemIcons.Draw(batch, "focus-sash", new Rectangle((int)(center.X - size / 2f), (int)(center.Y - size / 2f), size, size), alpha: alpha);
    }

    /// <summary>The evolved move's icon, in a gold frame, popping up over Pikachu's head, drifting higher, then blinking out.</summary>
    private void DrawEvolutions(SpriteBatch batch, RunState run)
    {
        if (_evolutions.Count == 0)
            return;

        var (age, moveId) = _evolutions[^1];
        if (_services.Moves.Value.All.FirstOrDefault(m => m.Id == moveId) is not { } move)
            return;

        // Blinks in its last quarter of a second, like the status icons before they end.
        var left = EvolveLife - age;
        if (left < 16 && left / 3 % 2 == 1)
            return;

        var t = age / (float)EvolveLife;
        var size = age < 6 ? 30 - age : 24;
        var head = _services.Sprites.Get(run.Player.Species.Id)?.HeadHeight ?? 20;
        var center = run.Player.Position.ToXna() - new Vector2(0, head + 16 + t * 14f);
        var slot = new Rectangle((int)(center.X - size / 2f), (int)(center.Y - size / 2f), size, size);
        UiDraw.Frame(batch, _services.Primitives, slot, UiTheme.Dark with { Border = EvolveGold, Highlight = new Color(176, 136, 48) });
        _services.MoveIcons.Draw(batch, _services.Primitives, new Rectangle(slot.X + 2, slot.Y + 2, slot.Width - 4, slot.Height - 4), move.Id, move.Type);
    }

    /// <summary>Status light over the enemies' silhouettes (burning orange, toxic purple, sparks of paralysis, icy blue). Additive.</summary>
    private void DrawStatusGlow(SpriteBatch batch, RunState run)
    {
        foreach (var item in _drawList)
        {
            if (item.Kind != DrawKind.Enemy)
                continue;

            ref readonly var e = ref run.Enemies[item.Index];
            var animator = _enemyAnimators[item.Index];
            if (!e.IsAlive || e.Status == StatusKind.None || animator.Anim is null || _enemySerials[item.Index] != e.Serial)
                continue;

            var glow = _statusVfx.Glow(e, item.Index);
            if (glow.A > 0)
                _services.Sprites.Get(e.SpeciesId)!.Draw(batch, animator.Anim, animator.Frame, e.Facing, e.Position.ToXna() + _statusVfx.Jitter(e), glow, silhouette: true);
        }
    }

    /// <summary>Plain outlines of every move hitbox, at ground level where collisions happen. Not game art.</summary>
    private void DrawHitboxes(SpriteBatch batch, RunState run)
    {
        var p = _services.Primitives;
        var moves = run.MoveSystem;
        Color cyan = new(80, 230, 255), orange = new(255, 160, 40), yellow = new(255, 240, 80), magenta = new(255, 90, 220), red = new(255, 70, 70), green = new(110, 255, 120);

        foreach (ref readonly var projectile in moves.Projectiles.Items)
        {
            if (projectile.Active)
                p.Ring(batch, projectile.Position.ToXna(), projectile.Radius, projectile.Radius, cyan);
        }

        foreach (var zone in moves.Zones)
            p.Ring(batch, zone.Position.ToXna(), zone.Radius, zone.Radius, orange * (0.4f + 0.6f * zone.TicksLeft / zone.Lifetime));

        foreach (var ring in moves.Rings)
        {
            p.Ring(batch, ring.Center.ToXna(), MathF.Max(1f, ring.Radius - ring.HalfThickness), MathF.Max(1f, ring.Radius - ring.HalfThickness), yellow);
            p.Ring(batch, ring.Center.ToXna(), ring.Radius + ring.HalfThickness, ring.Radius + ring.HalfThickness, yellow);
        }

        foreach (var beam in moves.Beams)
        {
            var origin = beam.Origin.ToXna();
            var direction = beam.Direction.ToXna();
            var side = new Vector2(-direction.Y, direction.X) * beam.HalfWidth;
            var end = origin + direction * beam.Length;
            UiDraw.Line(batch, p, origin + side, end + side, magenta);
            UiDraw.Line(batch, p, origin - side, end - side, magenta);
            UiDraw.Line(batch, p, end + side, end - side, magenta);
        }

        foreach (var pending in moves.Pending)
        {
            if (pending.Kind != Core.Run.Moves.PendingKind.Strike)
                continue;

            // Warning circle filling up until the strike lands.
            var progress = (run.Ticks - pending.StartTick) / (float)Math.Max(1, pending.DueTick - pending.StartTick);
            p.Ring(batch, pending.Position.ToXna(), pending.Radius, pending.Radius, red * 0.6f);
            p.Ring(batch, pending.Position.ToXna(), pending.Radius * progress, pending.Radius * progress, red);
        }

        foreach (var shape in _debugShapes)
        {
            var fade = 1f - shape.Age / (float)DebugShape.Life;
            switch (shape.Kind)
            {
                case RunEventType.Slash:
                    var reach = shape.Vector.Length();
                    var facing = MathF.Atan2(shape.Vector.Y, shape.Vector.X);
                    var half = shape.Value * MathF.PI / 360f;
                    for (var a = -half; a <= half + 0.001f; a += half / 6f)
                        UiDraw.Line(batch, p, shape.Position, shape.Position + new Vector2(MathF.Cos(facing + a), MathF.Sin(facing + a)) * reach, green * fade);
                    break;
                case RunEventType.AuraPulse:
                    p.Ring(batch, shape.Position, shape.Value, shape.Value, green * fade, 2);
                    break;
                case RunEventType.StrikeImpact:
                    p.Ellipse(batch, shape.Position, (int)(shape.Value * 2), (int)(shape.Value * 2), red * (0.4f * fade));
                    break;
            }
        }
    }

    private readonly record struct DebugShape(RunEventType Kind, Vector2 Position, Vector2 Vector, float Value, int Age = 0)
    {
        public const int Life = 12;
    }

    private void DrawGems(SpriteBatch batch, RunState run, Rectangle cull)
    {
        var p = _services.Primitives;
        foreach (ref readonly var gem in run.Pickups.Gems.Items)
        {
            if (!gem.Active || !cull.Contains(gem.Position.ToXna()))
                continue;

            var texture = _services.Pickups.Gem(gem.Tier);
            var ground = gem.Position.ToXna();
            var bob = gem.Magnetized ? -3f : MathF.Round(MathF.Sin((_tick + gem.Serial * 13) * 0.1f)) - 1f;
            if (!gem.Magnetized)
                p.Rect(batch, new Rectangle((int)ground.X - 2, (int)ground.Y, 5, 1), Color.Black * 0.25f);

            var topLeft = new Vector2(MathF.Round(ground.X - texture.Width / 2f), MathF.Round(ground.Y - texture.Height + bob));
            batch.Draw(texture, topLeft, Color.White);

            // Occasional twinkle on the top facet.
            if ((_tick + gem.Serial * 37) % 100 < 5)
                p.Rect(batch, new Rectangle((int)topLeft.X + 1, (int)topLeft.Y + 1, 1, 1), Color.White);
        }
    }

    private void UpdateAnimators(RunState run, PokemonSpriteSet playerSet)
    {
        if (run.Player.IsDown)
        {
            _playerAnimator.Play(playerSet.Anim(PmdAnimations.Faint) ?? playerSet.Resolve(PmdAnimations.Hurt), loop: false);
        }
        else if (run.Outcome == RunOutcome.Victory)
        {
            _playerAnimator.Play(playerSet.Anim(PmdAnimations.Pose) ?? playerSet.Resolve(PmdAnimations.Charge), loop: false);
        }
        else if (_attackTicks > 0)
        {
            _attackTicks--;
        }
        else
        {
            _playerAnimator.Play(playerSet.Resolve(run.Player.IsMoving ? PmdAnimations.Walk : PmdAnimations.Idle));
        }

        _playerAnimator.Update();

        var items = run.Enemies.Items;
        for (var i = 0; i < items.Length; i++)
        {
            ref readonly var e = ref items[i];
            if (e.State == EnemyState.Inactive)
                continue;

            var set = _services.Sprites.Get(e.SpeciesId)!;
            if (_enemySerials[i] != e.Serial)
            {
                _enemySerials[i] = e.Serial;
                _enemyAnimators[i] = default;
                _enemyAnimators[i].Play(set.Resolve(PmdAnimations.Walk));
                _enemyAnimators[i].Update(e.Serial % 30);
            }

            if (e.State == EnemyState.Fainting)
                _enemyAnimators[i].Play(set.Anim(PmdAnimations.Faint) ?? set.Resolve(PmdAnimations.Hurt), loop: false);
            else
                _enemyAnimators[i].Play(set.Resolve(e.Status == StatusKind.Sleep ? PmdAnimations.Sleep : PmdAnimations.Walk));

            // Frozen and locked-up Pokémon hold their pose; sleeping ones keep breathing.
            if (e.State == EnemyState.Fainting || e.Status == StatusKind.Sleep || !e.IsImmobilized)
                _enemyAnimators[i].Update();
        }
    }

    private void BuildDrawList(RunState run, Rectangle cull)
    {
        _drawList.Clear();
        _props.Clear();
        _props.AddRange(Floor.Props(cull));
        for (var i = 0; i < _props.Count; i++)
            _drawList.Add(new DrawItem(_props[i].Feet.Y, DrawKind.Prop, i));

        var items = run.Enemies.Items;
        for (var i = 0; i < items.Length; i++)
        {
            if (items[i].State != EnemyState.Inactive && cull.Contains(items[i].Position.ToXna()))
                _drawList.Add(new DrawItem(items[i].Position.Y, DrawKind.Enemy, i));
        }

        if (DrawPlayer)
            _drawList.Add(new DrawItem(run.Player.Position.Y, DrawKind.Player, 0));
        _drawList.Sort(static (a, b) => a.Y.CompareTo(b.Y));
    }

    private void DrawShadows(SpriteBatch batch, RunState run)
    {
        foreach (var item in _drawList)
        {
            switch (item.Kind)
            {
                case DrawKind.Prop:
                    _services.Primitives.Ellipse(batch, _props[item.Index].Feet, _props[item.Index].ShadowWidth, 4, Color.Black * 0.3f);
                    break;
                case DrawKind.Enemy:
                    ref readonly var e = ref run.Enemies[item.Index];
                    DrawShadow(batch, e.SpeciesId, e.Position.ToXna(), e.State == EnemyState.Fainting ? FaintAlpha(e) : 1f);
                    break;
                default:
                    DrawShadow(batch, run.Player.Species.Id, run.Player.Position.ToXna(), 1f);
                    break;
            }
        }
    }

    private void DrawShadow(SpriteBatch batch, int speciesId, Vector2 feet, float alpha)
    {
        var set = _services.Sprites.Get(speciesId)!;
        var (w, h) = set.Data.ShadowSize switch { 0 => (10, 4), 2 => (22, 8), _ => (16, 6) };
        _services.Primitives.Ellipse(batch, feet + new Vector2(0, 1), w, h, Color.Black * (0.3f * alpha));
    }

    private void DrawItemSprite(SpriteBatch batch, RunState run, DrawItem item)
    {
        switch (item.Kind)
        {
            case DrawKind.Prop:
            {
                var prop = _props[item.Index];
                batch.Draw(prop.Texture, prop.Feet - new Vector2(prop.Texture.Width / 2f, prop.Texture.Height - 1), Color.White);
                break;
            }
            case DrawKind.Enemy:
            {
                ref readonly var e = ref run.Enemies[item.Index];
                var animator = _enemyAnimators[item.Index];

                // A slot reused since the last Update still holds the previous Pokémon's animation: wait one frame.
                if (animator.Anim is null || _enemySerials[item.Index] != e.Serial)
                    break;

                var set = _services.Sprites.Get(e.SpeciesId)!;
                var tint = e.State == EnemyState.Fainting ? Color.White * FaintAlpha(e) : _statusVfx.Tint(e, item.Index);
                var feet = e.Position.ToXna() + _statusVfx.Jitter(e);
                set.Draw(batch, animator.Anim, animator.Frame, e.Facing, feet, tint, silhouette: e.FlashTicks > 0);
                if (e.IsAlive && e.Status == StatusKind.Freeze)
                    _statusVfx.DrawIce(batch, e, feet);
                break;
            }
            default:
            {
                var player = run.Player;
                // Blink while invulnerable, tint red right after a hit (not once the run is over).
                if (!run.IsOver && player.InvulnerableTicks > 0 && player.InvulnerableTicks / 3 % 2 == 1)
                    break;

                var set = _services.Sprites.Get(player.Species.Id)!;
                var tint = _hurtTicks > 0 ? new Color(255, 120, 120) : Color.White;
                // While attacking on the spot, turn towards the target; walking keeps the movement facing.
                var facing = _attackTicks > 0 && !player.IsMoving ? _attackFacing : player.Facing;
                set.Draw(batch, _playerAnimator.Anim!, _playerAnimator.Frame, facing, player.Position.ToXna(), tint);
                break;
            }
        }
    }

    private static float FaintAlpha(in Enemy e) => MathHelper.Clamp(e.FaintTicksLeft / (Enemy.FaintTicks * 0.5f), 0f, 1f);

    private enum DrawKind : byte
    {
        Prop,
        Enemy,
        Player,
    }

    private readonly record struct DrawItem(float Y, DrawKind Kind, int Index);
}
