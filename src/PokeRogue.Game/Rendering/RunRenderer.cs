using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PokeRogue.Core.Combat;
using PokeRogue.Core.Data;
using PokeRogue.Core.Geometry;
using PokeRogue.Core.Run;
using PokeRogue.Core.Run.Enemies;
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
    private readonly EnemyVfx _enemyVfx;
    private readonly AbilityVfx _abilityVfx;
    private readonly List<int> _levelBursts = [];

    /// <summary>Ages of Focalnastro revivals still showing (rings and the sash rising over Pikachu).</summary>
    private readonly List<int> _revivals = [];

    /// <summary>Move evolutions still showing (gold rings and the new move's icon rising over Pikachu).</summary>
    private readonly List<(int Age, string MoveId)> _evolutions = [];

    /// <summary>Ages of the player's own evolutions still showing their rings (phase 4.2).</summary>
    private readonly List<int> _playerEvolutions = [];

    /// <summary>The species the player animator plays; it changes when the player evolves.</summary>
    private int _playerSpeciesId;
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
        _enemyVfx = new EnemyVfx(services, _vfx.Shake);
        _abilityVfx = new AbilityVfx(services);
    }

    public FloorRenderer Floor { get; }

    public Camera2D Camera { get; }

    public int ParticleCount => _particles.Active + _vfx.ParticleCount + _statusVfx.ParticleCount + _enemyVfx.ParticleCount;

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
        _playerSpeciesId = run.Player.Form.Id;
        _playerAnimator.Play(_services.Sprites.Get(run.Player.Form.Id)!.Resolve(PmdAnimations.Idle));
        _hurtTicks = _attackTicks = 0;
        _vfx.Reset();
        _statusVfx.Reset();
        _enemyVfx.Reset();
        _abilityVfx.Reset();
        _levelBursts.Clear();
        _bossEntrance = -1;
        _revivals.Clear();
        _evolutions.Clear();
        _playerEvolutions.Clear();
        _debugShapes.Clear();
    }

    /// <summary>
    /// Consumes this tick's events and advances animations. Call once after every <see cref="RunState.Tick"/>.
    /// With <paramref name="paused"/> (level-up), Pokémon freeze and only effects keep playing.
    /// </summary>
    public void Update(RunState run, bool paused = false)
    {
        _tick++;

        // The player evolved (phase 4.2, while the game is frozen): its animations come from the new sprite sheet.
        if (run.Player.Form.Id != _playerSpeciesId)
        {
            _playerSpeciesId = run.Player.Form.Id;
            _playerAnimator.Play(_services.Sprites.Get(_playerSpeciesId)!.Resolve(PmdAnimations.Idle), restart: true);
            _attackTicks = 0;
        }

        UpdateEffects(run);
        _vfx.Update(run, paused);
        _statusVfx.Update(run, paused, Cull());
        _enemyVfx.Update(run, paused);
        if (paused)
            return;

        var playerSet = _services.Sprites.Get(run.Player.Form.Id)!;

        foreach (var e in run.Events)
        {
            _vfx.OnEvent(e, run);
            _statusVfx.OnEvent(e);
            _enemyVfx.OnEvent(e);
            _abilityVfx.OnEvent(e, run, _playerAnimator.Anim, _playerAnimator.Frame);
            switch (e.Type)
            {
                case RunEventType.PlayerHurt:
                    _hurtTicks = 12;
                    break;
                case RunEventType.BossAppeared:
                    _bossEntrance = 0;
                    break;
                case RunEventType.BossPhaseChanged:
                    _services.Cries.Play(e.SpeciesId, pitch: 0.15f);
                    break;
                case RunEventType.BossDefeated:
                    _services.Cries.Play(e.SpeciesId, pitch: -0.3f);
                    break;
                case RunEventType.EnemyHit when e.SpeciesId == 0 || !_services.Settings.ShowDamageNumbers:
                    // Grass and boulders show no numbers; nor does anything when the player turned them off.
                    break;
                case RunEventType.EnemyHit:
                    _numbers.Add(e.Position.ToXna(), e.Value, e.Effectiveness, e.Critical, _random, e.Ohko);
                    break;
                case RunEventType.StatusDamage when !_services.Settings.ShowDamageNumbers:
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
                case RunEventType.PlayerEvolved:
                    // Back in the field after the evolution window: gold rings and sparks from the new Pokémon's feet.
                    _playerEvolutions.Add(0);
                    _particles.Burst(run.Player.Position.ToXna() - new Vector2(0, 10), 40, 180f, Color.White, EvolveGold, 36, _random, size: 2f);
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
    private void UpdateEffects(RunState run)
    {
        // The boss' entrance plays during the introduction, while the game is frozen: it lands with a crash and its cry.
        if (_bossEntrance >= 0 && ++_bossEntrance == BossDropTicks && run.Boss is { } fight)
        {
            var boss = run.Enemies[fight.Index];
            _enemyVfx.BossLanded(boss.Position.ToXna());
            _services.Cries.Play(boss.SpeciesId);
        }

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

        for (var i = _playerEvolutions.Count - 1; i >= 0; i--)
        {
            if (++_playerEvolutions[i] >= EvolveLife)
                _playerEvolutions.RemoveAt(i);
        }

        for (var i = _debugShapes.Count - 1; i >= 0; i--)
        {
            _debugShapes[i] = _debugShapes[i] with { Age = _debugShapes[i].Age + 1 };
            if (_debugShapes[i].Age >= DebugShape.Life)
                _debugShapes.RemoveAt(i);
        }

        _particles.Update();
        _numbers.Update();
        _abilityVfx.Update();
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
        _enemyVfx.DrawGround(batch, run, cull);
        DrawGems(batch, run, cull);
        DrawLoot(batch, run, cull);
        DrawChests(batch, run, cull);
        BuildDrawList(run, cull);
        DrawShadows(batch, run);
        foreach (var item in _drawList)
            DrawItemSprite(batch, run, item);
        _vfx.DrawObjects(batch, run);
        _enemyVfx.DrawObjects(batch, run);
        _statusVfx.DrawObjects(batch);
        _abilityVfx.DrawObjects(batch);

        var player = run.Player;
        var hpRatio = player.Hp / player.MaxHp;
        var feet = player.Position.ToXna();
        if (!run.IsOver && DrawPlayer)
            UiDraw.Bar(batch, p, new Rectangle((int)feet.X - 13, (int)feet.Y + 5, 26, 4), hpRatio, UiDraw.HpColor(hpRatio), new Color(40, 24, 32), Ink);
        batch.End();

        batch.Begin(blendState: BlendState.Additive, samplerState: SamplerState.PointClamp, transformMatrix: Camera.Transform);
        _vfx.DrawGlow(batch, run);
        _enemyVfx.DrawGlow(batch);
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

        // Evolution, of a move or of the player: three white rings turning gold as they spread from Pikachu's feet.
        foreach (var age in _evolutions.Select(e => e.Age).Concat(_playerEvolutions))
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
        DrawDominantBars(batch, run, cull);
        if (!run.IsOver)
            DrawEdgeArrows(batch, run);
        DrawRevivals(batch, run);
        DrawEvolutions(batch, run);
        _abilityVfx.DrawOverhead(batch, run);
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
        var head = _services.Sprites.Get(run.Player.Form.Id)?.HeadHeight ?? 20;
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
        var head = _services.Sprites.Get(run.Player.Form.Id)?.HeadHeight ?? 20;
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
            if (e.State == EnemyState.Inactive || e.Rank == EnemyRank.Prop)
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
            else if (e.Status == StatusKind.Sleep)
                _enemyAnimators[i].Play(set.Resolve(PmdAnimations.Sleep));
            else
                _enemyAnimators[i].Play(ActionAnim(set, e.Action), loop: e.Action != EnemyAction.Aim);

            // Frozen and locked-up Pokémon hold their pose; sleeping ones keep breathing; chargers run at double pace.
            if (e.State == EnemyState.Fainting || e.Status == StatusKind.Sleep || !e.IsImmobilized)
                _enemyAnimators[i].Update(e.Action == EnemyAction.Dash ? 2 : 1);
        }
    }

    /// <summary>
    /// Tall grass sways when hit; a boulder shakes and cracks as its PS drop (one crack below two thirds, two below one
    /// third).
    /// </summary>
    private void DrawProp(SpriteBatch batch, in Enemy prop)
    {
        var art = _services.Breakables;
        var texture = prop.Breakable == BreakableKind.Rock ? art.Rock : art.Grass;
        var feet = prop.Position.ToXna();
        if (prop.FlashTicks > 0)
            feet.X += prop.FlashTicks % 2 == 0 ? 1f : -1f;

        var topLeft = new Vector2(MathF.Round(feet.X - texture.Width / 2f), MathF.Round(feet.Y - texture.Height + 1));
        batch.Draw(texture, topLeft, Color.White);
        if (prop.Breakable != BreakableKind.Rock)
            return;

        var p = _services.Primitives;
        var ratio = prop.Hp / prop.MaxHp;
        if (ratio < 0.67f)
        {
            UiDraw.Line(batch, p, topLeft + new Vector2(6, 2), topLeft + new Vector2(8, 5), BreakableArt.Crack);
            UiDraw.Line(batch, p, topLeft + new Vector2(8, 5), topLeft + new Vector2(7, 8), BreakableArt.Crack);
        }

        if (ratio < 0.34f)
        {
            UiDraw.Line(batch, p, topLeft + new Vector2(11, 3), topLeft + new Vector2(12, 6), BreakableArt.Crack);
            UiDraw.Line(batch, p, topLeft + new Vector2(3, 5), topLeft + new Vector2(5, 7), BreakableArt.Crack);
        }
    }

    /// <summary>Items on the ground: they pop out where they fell, then bob with a glint now and then.</summary>
    private void DrawLoot(SpriteBatch batch, RunState run, Rectangle cull)
    {
        var p = _services.Primitives;
        foreach (var loot in run.Loot.Items)
        {
            var ground = loot.Position.ToXna();
            if (!cull.Contains(ground))
                continue;

            var t = MathHelper.Clamp(loot.Age / 16f, 0f, 1f);
            var hop = loot.Age < 16 ? MathF.Sin(t * MathF.PI) * 14f : MathF.Round(MathF.Sin((_tick + loot.Serial * 9) * 0.1f));
            if (loot.Magnetized)
                hop = 2f;

            p.Ellipse(batch, ground + new Vector2(0, 1), 8, 3, Color.Black * 0.25f);
            if (loot.Kind == Core.Run.Pickups.LootKind.Coin)
            {
                var coin = _services.Pickups.Coin;
                batch.Draw(coin, new Vector2(MathF.Round(ground.X - coin.Width / 2f), MathF.Round(ground.Y - coin.Height - hop)), Color.White);
                continue;
            }

            // A held item (phase 3.6) stands on a slowly beating gold ring, so it reads as a lasting find.
            if (loot.Kind == Core.Run.Pickups.LootKind.HeldItem)
            {
                var beat = 0.45f + 0.25f * MathF.Sin((_tick + loot.Serial * 5) * 0.08f);
                p.Ring(batch, ground, 11f, 5f, EnemyVfx.ShinyGold * beat, 2);
            }

            // The item's sprite, its 24 px middle as in the HUD.
            var icon = loot.Kind == Core.Run.Pickups.LootKind.HeldItem && loot.ItemId is { } id ? id : LootIcon(loot.Kind);
            _services.ItemIcons.Draw(batch, icon, new Rectangle((int)ground.X - 12, (int)(ground.Y - 22 - hop), 24, 24));
            if ((_tick + loot.Serial * 13) % 70 < 5)
                p.Rect(batch, new Rectangle((int)ground.X + 3, (int)(ground.Y - 16 - hop), 2, 2), Color.White);
        }
    }

    public static string LootIcon(Core.Run.Pickups.LootKind kind) => kind switch
    {
        Core.Run.Pickups.LootKind.Nugget => "nugget",
        Core.Run.Pickups.LootKind.BigNugget => "big-nugget",
        Core.Run.Pickups.LootKind.Potion => "potion",
        Core.Run.Pickups.LootKind.RareCandy => "rare-candy",
        Core.Run.Pickups.LootKind.Repel => "repel",
        Core.Run.Pickups.LootKind.ExpShare => "exp-share",
        Core.Run.Pickups.LootKind.OranBerry => "oran-berry",
        _ => "nugget",
    };

    /// <summary>The PMD animation for what an enemy is doing: shooting, getting ready, standing dazed, or walking.</summary>
    private static PmdAnim ActionAnim(PokemonSpriteSet set, EnemyAction action) => action switch
    {
        EnemyAction.Aim => set.Anim(PmdAnimations.Shoot) ?? set.Resolve(PmdAnimations.Attack),
        EnemyAction.Slam => set.Resolve(PmdAnimations.Attack),
        EnemyAction.Roar => set.Anim(PmdAnimations.Pose) ?? set.Anim(PmdAnimations.Charge) ?? set.Resolve(PmdAnimations.Idle),
        EnemyAction.WindUp or EnemyAction.Fuse => set.Anim(PmdAnimations.Charge) ?? set.Resolve(PmdAnimations.Idle),
        EnemyAction.Recover or EnemyAction.Vanish or EnemyAction.Appear => set.Resolve(PmdAnimations.Idle),
        _ => set.Resolve(PmdAnimations.Walk),
    };

    /// <summary>How big a teleporter is drawn while it shrinks away or grows back; 1 otherwise.</summary>
    private static float HopScale(in Enemy e) => e.Action switch
    {
        EnemyAction.Vanish => e.ActionTicks / (float)Core.Run.Enemies.EnemyAi.HopTicks,
        EnemyAction.Appear => 1f - e.ActionTicks / (float)Core.Run.Enemies.EnemyAi.HopTicks,
        _ => 1f,
    };

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
                case DrawKind.Enemy when run.Enemies[item.Index].Rank == EnemyRank.Prop:
                    _services.Primitives.Ellipse(batch, run.Enemies[item.Index].Position.ToXna() + new Vector2(0, 1),
                        run.Enemies[item.Index].Breakable == BreakableKind.Rock ? 16 : 12, 4, Color.Black * 0.3f);
                    break;
                case DrawKind.Enemy:
                    ref readonly var e = ref run.Enemies[item.Index];
                    DrawShadow(batch, e.SpeciesId, e.Position.ToXna(), e.State == EnemyState.Fainting ? FaintAlpha(e) : HopScale(e),
                        e.Rank switch { EnemyRank.Dominant => Spawner.DominantScale, EnemyRank.Boss => BossAi.DrawScale * (1f - BossDropHeight() / 300f), _ => 1f });
                    break;
                default:
                    DrawShadow(batch, run.Player.Form.Id, run.Player.Position.ToXna(), 1f);
                    break;
            }
        }
    }

    private void DrawShadow(SpriteBatch batch, int speciesId, Vector2 feet, float alpha, float scale = 1f)
    {
        var set = _services.Sprites.Get(speciesId)!;
        var (w, h) = set.Data.ShadowSize switch { 0 => (10, 4), 2 => (22, 8), _ => (16, 6) };
        _services.Primitives.Ellipse(batch, feet + new Vector2(0, 1), (int)(w * scale), (int)(h * scale), Color.Black * (0.3f * alpha));
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
            case DrawKind.Enemy when run.Enemies[item.Index].Rank == EnemyRank.Prop:
                DrawProp(batch, run.Enemies[item.Index]);
                break;
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
                var scale = 1f;

                // Bosses and Dominanti are hit all the time and would blink white without pause: their damage numbers
                // and their bar show the hits instead.
                var silhouette = e.Rank == EnemyRank.Normal && e.FlashTicks > 0;
                switch (e.IsAlive ? e.Action : EnemyAction.None)
                {
                    case EnemyAction.WindUp:
                        // Trembling with impatience before the charge.
                        feet.X += _tick / 2 % 2 == 0 ? 1f : -1f;
                        break;

                    case EnemyAction.Dash:
                    {
                        // Two fading after-images along the way it came.
                        var step = e.Velocity.ToXna() / RunClock.TicksPerSecond * 3f;
                        set.Draw(batch, animator.Anim, animator.Frame, e.Facing, feet - step * 2f, Color.White * 0.2f);
                        set.Draw(batch, animator.Anim, animator.Frame, e.Facing, feet - step, Color.White * 0.4f);
                        break;
                    }

                    case EnemyAction.Fuse when e.Profile is { } profile:
                        // Flashing white, faster and faster as the fuse burns down.
                        silhouette |= _tick / (e.ActionTicks < profile.WindUpTicks / 3 ? 3 : 7) % 2 == 0;
                        break;

                    case EnemyAction.Vanish or EnemyAction.Appear:
                        // A pink figure shrinking to nothing, or growing back from it.
                        scale = HopScale(e);
                        silhouette |= scale < 0.8f;
                        tint = silhouette ? EnemyVfx.PsyPink : tint;
                        break;
                }

                if (e.Rank == EnemyRank.Dominant)
                {
                    scale *= Spawner.DominantScale;
                    if (e.IsAlive)
                        DrawAura(batch, set, animator, e, feet, scale, 2f);
                }
                else if (e.Rank == EnemyRank.Boss)
                {
                    // Three times the size, a wider aura; it drops from the sky on arrival and flashes while it roars.
                    scale *= BossAi.DrawScale;
                    feet.Y -= BossDropHeight();

                    // No hit flash during the entrance: the game is frozen and the flash would stay on.
                    if (_bossEntrance < Ui.BossIntro.Ticks)
                        silhouette = false;
                    silhouette |= e.Action == EnemyAction.Roar && _tick / 3 % 2 == 0;
                    if (e.IsAlive)
                        DrawAura(batch, set, animator, e, feet, scale, 3f);
                }

                if (scale >= 0.1f)
                    set.Draw(batch, animator.Anim, animator.Frame, e.Facing, feet, tint, scale, silhouette, shiny: e.Shiny);
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

                var set = _services.Sprites.Get(player.Form.Id)!;
                var tint = _hurtTicks > 0 ? new Color(255, 120, 120) : Color.White;
                // While attacking on the spot, turn towards the target; walking keeps the movement facing.
                var facing = _attackTicks > 0 && !player.IsMoving ? _attackFacing : player.Facing;
                set.Draw(batch, _playerAnimator.Anim!, _playerAnimator.Frame, facing, player.Position.ToXna(), tint);
                if (_abilityVfx.Flash > 0)
                    set.Draw(batch, _playerAnimator.Anim!, _playerAnimator.Frame, facing, player.Position.ToXna(), Color.White * (_abilityVfx.Flash / 14f), silhouette: true);
                break;
            }
        }
    }

    /// <summary>
    /// A Dominante's aura, like the Totem Pokémon of Alola: its silhouette in red-orange, two pixels out on every side,
    /// beating slowly. Alpha-blended, so the colour holds on the grass.
    /// </summary>
    private void DrawAura(SpriteBatch batch, PokemonSpriteSet set, SpriteAnimator animator, in Enemy e, Vector2 feet, float scale, float width)
    {
        var beat = 0.45f + 0.25f * MathF.Sin((_tick + e.Serial * 7) * 0.12f);
        var color = EnemyVfx.AuraRed * beat;
        ReadOnlySpan<Vector2> offsets = [new(-1, 0), new(1, 0), new(0, -1), new(0, 1), new(-1, -1), new(1, -1), new(-1, 1), new(1, 1)];
        foreach (var offset in offsets)
            set.Draw(batch, animator.Anim!, animator.Frame, e.Facing, feet + offset * width, color, scale, silhouette: true);
    }

    /// <summary>How long the boss takes to drop from the sky when it arrives.</summary>
    public const int BossDropTicks = 30;

    /// <summary>Ticks since the boss arrived (its drop, then its landing); -1 before.</summary>
    private int _bossEntrance = -1;

    /// <summary>How high above its feet the arriving boss still is (out of sight until its entrance starts).</summary>
    private float BossDropHeight()
    {
        if (_bossEntrance < 0)
            return 240f;
        if (_bossEntrance >= BossDropTicks)
            return 0f;

        var t = 1f - _bossEntrance / (float)BossDropTicks;
        return t * t * 240f;
    }

    /// <summary>Over each Dominante's head, a red bar with what is left of its PS.</summary>
    private void DrawDominantBars(SpriteBatch batch, RunState run, Rectangle cull)
    {
        foreach (ref readonly var e in run.Enemies.Items)
        {
            if (!e.IsAlive || e.Rank != EnemyRank.Dominant || !cull.Contains(e.Position.ToXna()))
                continue;

            var head = (_services.Sprites.Get(e.SpeciesId)?.HeadHeight ?? 16) * Spawner.DominantScale;
            var at = e.Position.ToXna() - new Vector2(18, head + 8);
            UiDraw.Bar(batch, _services.Primitives, new Rectangle((int)at.X, (int)at.Y, 36, 4), e.Hp / e.MaxHp, EnemyVfx.AuraRed, new Color(40, 24, 32), Ink);
        }
    }

    /// <summary>Chests on the ground: a Ball that pops out of the fallen Dominante, then bobs under a column of light.</summary>
    private void DrawChests(SpriteBatch batch, RunState run, Rectangle cull)
    {
        var p = _services.Primitives;
        foreach (var chest in run.Pickups.Chests)
        {
            var ground = chest.Position.ToXna();
            if (!cull.Contains(ground))
                continue;

            var color = ChestColor(chest.Tier);
            var t = MathHelper.Clamp(chest.Age / 24f, 0f, 1f);
            var hop = chest.Age < 24 ? MathF.Sin(t * MathF.PI) * 28f : MathF.Round(MathF.Sin((_tick + chest.Serial * 11) * 0.08f) * 1.5f) + 1.5f;

            // The column of light, only once it has landed.
            if (chest.Age >= 24)
            {
                var glow = 0.25f + 0.1f * MathF.Sin(_tick * 0.1f);
                for (var k = 0; k < 3; k++)
                    UiDraw.Line(batch, p, ground, ground - new Vector2(0, 46 - k * 8), color * (glow * (k + 1) / 3f), 8f - k * 3f);
            }

            p.Ellipse(batch, ground + new Vector2(0, 1), 14 - (int)(hop / 4f), 5, Color.Black * 0.3f);
            _services.ItemIcons.Draw(batch, ChestIcon(chest.Tier), new Rectangle((int)ground.X - 12, (int)(ground.Y - 20 - hop), 24, 24));

            // A twinkle on top now and then.
            if ((_tick + chest.Serial * 17) % 50 < 6)
                p.Rect(batch, new Rectangle((int)ground.X + 3, (int)(ground.Y - 18 - hop), 2, 2), Color.White);
        }
    }

    /// <summary>
    /// For each Dominante and chest off screen, an arrow at the edge of the view pointing at it: red for a Dominante,
    /// the Ball's colour (with the Ball beside it) for a chest.
    /// </summary>
    private void DrawEdgeArrows(SpriteBatch batch, RunState run)
    {
        var view = Camera.VisibleWorld;
        foreach (ref readonly var e in run.Enemies.Items)
        {
            if (e.IsAlive && e.Rank == EnemyRank.Dominant)
                EdgeArrow(batch, view, run.Player.Position.ToXna(), e.Position.ToXna(), EnemyVfx.AuraRed, null);
            else if (e.IsAlive && e.Shiny)
                EdgeArrow(batch, view, run.Player.Position.ToXna(), e.Position.ToXna(), EnemyVfx.ShinyGold, null);
        }

        foreach (var chest in run.Pickups.Chests)
            EdgeArrow(batch, view, run.Player.Position.ToXna(), chest.Position.ToXna(), ChestColor(chest.Tier), ChestIcon(chest.Tier));
    }

    private void EdgeArrow(SpriteBatch batch, Rectangle view, Vector2 from, Vector2 target, Color color, string? icon)
    {
        var inner = new Rectangle(view.X + 16, view.Y + 16, view.Width - 32, view.Height - 32);
        if (inner.Contains(target))
            return;

        var direction = Vector2.Normalize(target - from);
        var half = new Vector2(inner.Width / 2f, inner.Height / 2f);
        var reach = MathF.Min(MathF.Abs(direction.X) > 1e-4f ? half.X / MathF.Abs(direction.X) : float.MaxValue,
            MathF.Abs(direction.Y) > 1e-4f ? half.Y / MathF.Abs(direction.Y) : float.MaxValue);
        var tip = inner.Center.ToVector2() + direction * reach;
        var side = new Vector2(-direction.Y, direction.X);
        var pulse = _tick / 10 % 2 == 0 ? 1f : 0.7f;
        var p = _services.Primitives;

        var back = tip - direction * 9f;
        UiDraw.Line(batch, p, tip, back + side * 6f, Ink, 4f);
        UiDraw.Line(batch, p, tip, back - side * 6f, Ink, 4f);
        UiDraw.Line(batch, p, tip, back + side * 6f, color * pulse, 2f);
        UiDraw.Line(batch, p, tip, back - side * 6f, color * pulse, 2f);
        if (icon is not null)
        {
            var at = tip - direction * 22f;
            _services.ItemIcons.Draw(batch, icon, new Rectangle((int)at.X - 8, (int)at.Y - 8, 16, 16));
        }
    }

    public static string ChestIcon(ChestTier tier) => tier switch
    {
        ChestTier.Great => "great-ball",
        ChestTier.Ultra => "ultra-ball",
        _ => "poke-ball",
    };

    public static Color ChestColor(ChestTier tier) => tier switch
    {
        ChestTier.Great => new Color(96, 150, 255),
        ChestTier.Ultra => new Color(248, 208, 72),
        _ => new Color(255, 96, 90),
    };

    private static float FaintAlpha(in Enemy e) => MathHelper.Clamp(e.FaintTicksLeft / (Enemy.FaintTicks * 0.5f), 0f, 1f);

    private enum DrawKind : byte
    {
        Prop,
        Enemy,
        Player,
    }

    private readonly record struct DrawItem(float Y, DrawKind Kind, int Index);
}
