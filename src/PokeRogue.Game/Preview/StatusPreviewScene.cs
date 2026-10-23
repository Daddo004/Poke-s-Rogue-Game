using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PokeRogue.Core.Combat;
using PokeRogue.Core.Run;
using PokeRogue.Core.Run.Moves;
using PokeRogue.Core.Run.Statuses;
using PokeRogue.Game.Graphics;
using PokeRogue.Game.Graphics.Floor;
using PokeRogue.Game.Rendering;
using PokeRogue.Game.Scenes;
using PokeRogue.Game.Ui;

namespace PokeRogue.Game.Preview;

/// <summary>
/// Phase 2.3 preview of status conditions. "vetrina": one Pokémon per status (columns) in each icon style (rows);
/// "icone": the icons enlarged; "orda": a real run where moves inflict statuses and more are sprinkled on the horde.
/// </summary>
public sealed class StatusPreviewScene : Scene
{
    private static readonly StatusKind[] Kinds =
        [StatusKind.Burn, StatusKind.Poison, StatusKind.Paralysis, StatusKind.Freeze, StatusKind.Sleep, StatusKind.Confusion];

    /// <summary>One prototype-horde species per status, none of them immune to it: Rattata, Pidgey, Caterpie, Spearow, Ekans, Weedle.</summary>
    private static readonly int[] Species = [19, 16, 10, 21, 23, 13];

    private static readonly StatusIconStyle[] Styles = [StatusIconStyle.Symbol, StatusIconStyle.Badge, StatusIconStyle.None];
    private static readonly string[] StyleNames = ["SIMBOLO", "RIQUADRO", "SENZA ICONA"];

    private static readonly Color Paper = new(244, 240, 228);
    private static readonly Color Ink = new(16, 12, 28);
    private static readonly Color Gold = new(248, 208, 72);

    private const float ColumnX = -215f;
    private const float ColumnStep = 95f;
    private const float RowY = -58f;
    private const float RowStep = 95f;

    /// <summary>Pause between a status wearing off and the next one in the showcase.</summary>
    private const int RestTicks = 40;

    private readonly string _variant;
    private readonly string[] _hordeMoves;
    private readonly RunRenderer _renderer;
    private readonly GameplayScene? _horde;
    private readonly List<(int Slot, StatusKind Kind)> _dummies = [];
    private readonly Dictionary<int, StatusIconStyle> _styleBySlot = [];
    private readonly Dictionary<int, int> _restUntil = [];
    private readonly Random _random = new(7);
    private RunState _run = null!;
    private int _tick;

    /// <param name="icons">Horde only: simbolo (default), riquadro or nessuna.</param>
    /// <param name="hordeSize">Horde only: enemies spawned at the start (stress tests use 1500).</param>
    /// <param name="moves">Horde only: comma-separated moves added to Thundershock (default ember,smog,aurora-beam).</param>
    public StatusPreviewScene(GameServices services, string? variant, string? icons, int hordeSize = 150, string? moves = null) : base(services)
    {
        _variant = variant ?? "vetrina";
        _hordeMoves = moves?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries) ?? ["ember", "smog", "aurora-beam"];
        _renderer = new RunRenderer(services, FloorStyle.MeadowDecorated) { DrawPlayer = false };
        _renderer.Statuses.IconStyleForSlot = slot => _styleBySlot.GetValueOrDefault(slot, StatusIconStyle.Symbol);

        if (_variant == "orda")
        {
            _horde = new GameplayScene(services, initialSpawn: hordeSize, autopilot: true, setup: PrepareHorde);
            _horde.Renderer.Statuses.IconStyle = icons switch
            {
                "riquadro" => StatusIconStyle.Badge,
                "nessuna" => StatusIconStyle.None,
                _ => StatusIconStyle.Symbol,
            };
        }
    }

    public override IReadOnlyList<string> DebugLines => _horde?.DebugLines ?? [];

    public override void Enter()
    {
        if (_horde is not null)
        {
            _horde.Enter();
            return;
        }

        var view = new System.Numerics.Vector2(Services.Screen.Width, Services.Screen.Height);
        _run = new RunState(Services.Pokedex.Value.Species, Services.TypeChart.Value, 25, [new SpawnEntry(19, 1)], view, seed: 3)
        {
            StatusRules = Services.Statuses.Value.WithAllEnabled(),
        };
        _run.Spawner.Enabled = false;
        _run.Player.Invincible = true;
        _renderer.Reset(_run);

        if (_variant == "vetrina")
            SpawnShowcase();
    }

    public override void Update()
    {
        _tick++;
        if (_horde is not null)
        {
            SprinkleStatuses(_horde.Run);
            _horde.Update();
            return;
        }

        _run.Tick(default);
        _renderer.Update(_run);

        foreach (var (slot, kind) in _dummies)
        {
            // Showcase Pokémon never faint; a status that wears off comes back after a short rest.
            ref var e = ref _run.Enemies[slot];
            e.Hp = e.MaxHp;
            if (e.Status != StatusKind.None)
                continue;

            if (!_restUntil.TryGetValue(slot, out var until))
            {
                _restUntil[slot] = _tick + RestTicks;
            }
            else if (_tick >= until)
            {
                StatusSystem.Apply(_run, slot, kind, hitDamage: 20f);
                _restUntil.Remove(slot);
            }
        }
    }

    public override void Draw(SpriteBatch batch)
    {
        if (_horde is not null)
        {
            _horde.Draw(batch);
            return;
        }

        _renderer.Draw(batch, _run);

        var small = Services.Fonts.Small(Services.Style);
        batch.Begin(samplerState: SamplerState.PointClamp);
        // No digits in the label font: they read badly there.
        UiDraw.TextOutlined(batch, small, "ANTEPRIMA  -  STATI ALTERATI", new Vector2(8, 8), Paper, Ink);
        if (_variant == "icone")
            DrawIconBoard(batch, small);
        else
            DrawShowcaseLabels(batch, small);
        batch.End();
    }

    // ---------------------------------------------------------------- showcase

    private void SpawnShowcase()
    {
        for (var row = 0; row < Styles.Length; row++)
        {
            for (var column = 0; column < Kinds.Length; column++)
            {
                var position = new System.Numerics.Vector2(ColumnX + column * ColumnStep, RowY + row * RowStep);
                var slot = _run.Enemies.Spawn(Services.Pokedex.Value.Species[Species[column] - 1], position, hpMultiplier: 8f);
                _run.Enemies[slot].Speed = 0f;
                _styleBySlot[slot] = Styles[row];
                _dummies.Add((slot, Kinds[column]));

                // Staggered starts, so the six columns do not blink and shatter in unison.
                _restUntil[slot] = column * 9 + row * 3;
                Services.Sprites.Get(Species[column]);
            }
        }
    }

    private void DrawShowcaseLabels(SpriteBatch batch, FontStashSharp.SpriteFontBase small)
    {
        var center = new Vector2(Services.Screen.Width / 2f, Services.Screen.Height / 2f);
        for (var column = 0; column < Kinds.Length; column++)
        {
            var name = Services.Statuses.Value[Kinds[column]]!.NameIt.ToUpperInvariant();
            UiDraw.TextCentered(batch, small, name, center.X + ColumnX + column * ColumnStep, 40, Gold, Ink);
        }

        for (var row = 0; row < Styles.Length; row++)
            UiDraw.TextOutlined(batch, small, StyleNames[row], new Vector2(8, center.Y + RowY + row * RowStep - 14), Paper, Ink);

        UiDraw.TextCentered(batch, small, "TINTA ED EFFETTI SONO UGUALI IN OGNI RIGA: CAMBIA SOLO L'ICONA SOPRA LA TESTA", center.X, 344, Paper, Ink);
    }

    // ---------------------------------------------------------------- icon board

    private void DrawIconBoard(SpriteBatch batch, FontStashSharp.SpriteFontBase small)
    {
        var icons = Services.StatusIcons;
        var center = Services.Screen.Width / 2f;
        (string Label, int Y)[] rows = [("SIMBOLO", 92), ("RIQUADRO", 170), ("VERA GRANDEZZA", 244), ("E DOPPIA", 256)];
        foreach (var (label, y) in rows)
            UiDraw.TextOutlined(batch, small, label, new Vector2(8, y), Paper, Ink);

        for (var column = 0; column < Kinds.Length; column++)
        {
            var kind = Kinds[column];
            var x = center + ColumnX + column * ColumnStep;
            UiDraw.TextCentered(batch, small, Services.Statuses.Value[kind]!.NameIt.ToUpperInvariant(), x, 48, Gold, Ink);

            DrawScaled(batch, icons.Icon(kind, StatusIconStyle.Symbol)!, new Vector2(x, 72), 5);
            DrawScaled(batch, icons.Icon(kind, StatusIconStyle.Badge)!, new Vector2(x, 150), 5);

            // Real size, as over the horde (x1), and as at twice the size (x2) for comparison.
            DrawScaled(batch, icons.Icon(kind, StatusIconStyle.Symbol)!, new Vector2(x - 24, 244), 1);
            DrawScaled(batch, icons.Icon(kind, StatusIconStyle.Badge)!, new Vector2(x - 12, 244), 1);
            DrawScaled(batch, icons.Icon(kind, StatusIconStyle.Symbol)!, new Vector2(x + 6, 240), 2);
            DrawScaled(batch, icons.Icon(kind, StatusIconStyle.Badge)!, new Vector2(x + 26, 240), 2);
        }

        // The extra art of sleep and confusion.
        UiDraw.TextOutlined(batch, small, "SONNO E CONFUSIONE", new Vector2(8, 300), Paper, Ink);
        DrawScaled(batch, icons.SmallZ, new Vector2(center - 60, 296), 5);
        DrawScaled(batch, icons.BigZ, new Vector2(center - 20, 292), 5);
        DrawScaled(batch, icons.Star, new Vector2(center + 30, 296), 5);
    }

    /// <summary>Draws <paramref name="texture"/> scaled, its top edge at <paramref name="top"/> and centred on its X.</summary>
    private static void DrawScaled(SpriteBatch batch, Texture2D texture, Vector2 top, int scale) =>
        batch.Draw(texture, new Vector2(MathF.Round(top.X - texture.Width * scale / 2f), top.Y), null, Color.White, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);

    // ---------------------------------------------------------------- horde

    /// <summary>Moves at level 4, every status enabled, and enough levels banked that no level-up window interrupts.</summary>
    private void PrepareHorde(RunState run)
    {
        run.StatusRules = run.StatusRules?.WithAllEnabled();
        run.Player.AddXp(5 + 15 + 25 + 35 + 45 + 55 + 65 + 75 + 85 + 95 + 105 + 115 + 125 + 135);
        run.Player.Moves[0].Level = 4;

        foreach (var id in _hordeMoves.Take(Hud.Slots - 1))
            run.Player.Moves.Add(new MoveInstance(Services.Moves.Value[id], 4));
    }

    /// <summary>Sleep and confusion have no moves yet: every quarter of a second a random status lands on an enemy near the player.</summary>
    private void SprinkleStatuses(RunState run)
    {
        if (_tick % 15 != 0 || run.IsOver)
            return;

        for (var attempt = 0; attempt < 30; attempt++)
        {
            var slot = _random.Next(Math.Max(1, run.Enemies.HighWater));
            ref readonly var e = ref run.Enemies[slot];
            if (!e.IsAlive || e.Status != StatusKind.None || System.Numerics.Vector2.Distance(e.Position, run.Player.Position) > 190f)
                continue;

            StatusSystem.Apply(run, slot, Kinds[_random.Next(Kinds.Length)], hitDamage: 12f);
            return;
        }
    }
}
