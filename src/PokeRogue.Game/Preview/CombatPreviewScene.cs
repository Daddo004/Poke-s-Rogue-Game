using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using PokeRogue.Core.Run;
using PokeRogue.Core.Run.Moves;
using PokeRogue.Game.Graphics;
using PokeRogue.Game.Graphics.Floor;
using PokeRogue.Game.Rendering;
using PokeRogue.Game.Scenes;

namespace PokeRogue.Game.Preview;

/// <summary>
/// Move Lab: Pikachu uses one move of content/data/moves.json on slow, respawning dummies chosen to show every
/// damage-number colour (Pidgey ×2, Rattata/Caterpie ×1, Bellsprout ×0.5, Diglett/Geodude ×0.25).
/// Left/Right change move, Up/Down change level, H toggles hitboxes. Also the phase 1.4 preview ("combat").
/// </summary>
public sealed class CombatPreviewScene : Scene
{
    private static readonly int[] Dummies = [16, 19, 69, 50, 10, 74, 16, 19];
    private static readonly Color Paper = new(244, 240, 228);
    private static readonly Color Ink = new(16, 12, 28);

    private readonly RunRenderer _renderer;
    private readonly (int Slot, int Serial)[] _slots = new (int, int)[Dummies.Length];
    private readonly IReadOnlyList<MoveDefinition> _moves;
    private readonly string _title;
    private readonly int _cycleTicks;
    private int _moveIndex;
    private int _level;
    private int _ticksOnMove;
    private RunState _run = null!;

    /// <param name="reel">Optional comma-separated move ids to cycle through (preview reels); null = every move.</param>
    /// <param name="cycleTicks">Switch to the next move after this many ticks; 0 = only with the arrow keys.</param>
    public CombatPreviewScene(GameServices services, string moveId, int level, bool hitboxes, string title, string? reel = null, int cycleTicks = 0)
        : base(services)
    {
        var all = services.Moves.Value.All;
        _moves = reel is null ? all : reel.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(id => services.Moves.Value[id.Trim()]).ToList();
        _moveIndex = Math.Max(0, _moves.ToList().FindIndex(m => m.Id == moveId));
        _level = Math.Clamp(level, 1, MoveLibrary.MaxLevels);
        _title = title;
        _cycleTicks = cycleTicks;
        _renderer = new RunRenderer(services, FloorStyle.MeadowDecorated) { ShowHitboxes = hitboxes };
    }

    private MoveDefinition Move => _moves[_moveIndex];

    public override void Enter() => Restart();

    public override void Update()
    {
        var input = Services.Input;
        if (input.Pressed(Keys.Right))
            Switch(1, 0);
        if (input.Pressed(Keys.Left))
            Switch(-1, 0);
        if (input.Pressed(Keys.Up))
            Switch(0, 1);
        if (input.Pressed(Keys.Down))
            Switch(0, -1);
        if (input.Pressed(Keys.H))
            _renderer.ShowHitboxes = !_renderer.ShowHitboxes;
        if (_cycleTicks > 0 && ++_ticksOnMove >= _cycleTicks)
            Switch(1, 0);

        // Trail moves only show while moving: walk a slow circle for them.
        var walk = Move.SpawnAtPlayer
            ? new System.Numerics.Vector2(MathF.Cos(_run.Ticks / 40f), MathF.Sin(_run.Ticks / 40f)) * 0.6f
            : System.Numerics.Vector2.Zero;
        _run.Tick(new PlayerInput(walk));
        _renderer.Update(_run);
        RespawnDummies();
    }

    public override void Draw(SpriteBatch batch)
    {
        _renderer.Draw(batch, _run);

        var small = Services.Fonts.Small(Services.Style);
        var move = _run.Player.Moves[0];
        var stats = move.Stats;
        batch.Begin(samplerState: SamplerState.PointClamp);
        UiDraw.TextOutlined(batch, small, $"{_title}  -  {Move.NameIt.ToUpperInvariant()}  ({Move.Archetype})  LV {move.Level}{(Move.Enabled ? "" : "  [NON ANCORA APPROVATA]")}", new Vector2(8, 8), Paper, Ink);
        UiDraw.TextOutlined(batch, small,
            $"POTENZA {stats.Power:0.#}  RICARICA {stats.Cooldown:0.##}S  QUANTITA {stats.Amount}  PORTATA {stats.Range:0}  RAGGIO {stats.Radius:0}  DURATA {stats.Duration:0.#}S",
            new Vector2(8, 20), Paper, Ink);
        UiDraw.TextOutlined(batch, small, "[SINISTRA/DESTRA] MOSSA   [SU/GIU] LIVELLO   [H] HITBOX", new Vector2(8, 344), Paper, Ink);

        // Reels are captured cropped to the middle: repeat the name, with the move's icon, where the crop can see it.
        if (_cycleTicks > 0)
        {
            var body = Services.Fonts.Body(Services.Style);
            var center = Services.Screen.Width / 2f;
            UiDraw.TextCentered(batch, body, Move.NameIt, center, 64, Paper, Ink);
            var label = $"{TypePalette.NameIt(Move.Type).ToUpperInvariant()}  -  {ArchetypeName(Move).ToUpperInvariant()}";
            UiDraw.TextCentered(batch, small, label, center, 82, new Color(248, 208, 72), Ink);

            var slot = new Rectangle((int)(center - UiDraw.Measure(body, Move.NameIt).X / 2f) - 30, 60, 24, 24);
            UiDraw.Frame(batch, Services.Primitives, slot, Services.Style.Theme);
            Services.MoveIcons.Draw(batch, Services.Primitives, new Rectangle(slot.X + 2, slot.Y + 2, 20, 20), Move.Id, Move.Type);
        }

        batch.End();
    }

    private static string ArchetypeName(MoveDefinition move) => move.Archetype switch
    {
        MoveArchetype.Projectile => "Proiettile",
        MoveArchetype.Homing => "A ricerca",
        MoveArchetype.Boomerang => "Boomerang",
        MoveArchetype.Orbit => "Orbita",
        MoveArchetype.Aura => "Aura",
        MoveArchetype.Slash => "Fendente",
        MoveArchetype.Beam => "Raggio",
        MoveArchetype.Strike => "Pioggia dall'alto",
        MoveArchetype.Arc => "Catena",
        MoveArchetype.Zone => move.SpawnAtPlayer ? "Scia" : "Zona",
        MoveArchetype.Ring => "Anello",
        _ => move.Archetype.ToString(),
    };

    private void Switch(int moveDelta, int levelDelta)
    {
        _moveIndex = (_moveIndex + moveDelta + _moves.Count) % _moves.Count;
        _level = Math.Clamp(_level + levelDelta, 1, MoveLibrary.MaxLevels);
        Restart();
    }

    private void Restart()
    {
        _ticksOnMove = 0;
        var view = new System.Numerics.Vector2(Services.Screen.Width, Services.Screen.Height);
        _run = new RunState(Services.Pokedex.Value.Species, Services.TypeChart.Value, 25, [new SpawnEntry(19, 1)], view, seed: 3)
        {
            StatusRules = Services.Statuses.Value,
        };
        _run.Spawner.Enabled = false;
        _run.Player.Invincible = true;
        _run.Player.Moves.Add(new MoveInstance(Move, _level));
        _renderer.Reset(_run);

        for (var i = 0; i < Dummies.Length; i++)
            _slots[i] = (-1, 0);
        RespawnDummies();
    }

    /// <summary>Two tight clusters (so chains and areas show), otherwise a wide ring.</summary>
    private System.Numerics.Vector2 Home(int i)
    {
        if (Move.Archetype is MoveArchetype.Arc or MoveArchetype.Strike or MoveArchetype.Zone)
        {
            var cluster = i % 2 == 0 ? new System.Numerics.Vector2(95, 20) : new System.Numerics.Vector2(-100, -30);
            var k = i / 2;
            return cluster + new System.Numerics.Vector2(k % 2 * 42 - 21, k / 2 * 40 - 20);
        }

        var angle = i * MathF.Tau / Dummies.Length + 0.3f;
        var radius = Move.Archetype is MoveArchetype.Aura or MoveArchetype.Slash or MoveArchetype.Orbit ? 0.5f : 1f;
        return new System.Numerics.Vector2(MathF.Cos(angle) * 150f * radius, MathF.Sin(angle) * 95f * radius);
    }

    private void RespawnDummies()
    {
        for (var i = 0; i < Dummies.Length; i++)
        {
            var (slot, serial) = _slots[i];
            if (slot >= 0 && _run.Enemies[slot].Serial == serial && _run.Enemies[slot].State != EnemyState.Inactive)
                continue;

            var index = _run.Enemies.Spawn(Services.Pokedex.Value.Species[Dummies[i] - 1], Home(i), hpMultiplier: 4f);
            _run.Enemies[index].Speed = 6f;
            _slots[i] = (index, _run.Enemies[index].Serial);
            Services.Sprites.Get(Dummies[i]);
        }
    }
}
