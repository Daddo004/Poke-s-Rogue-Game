using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PokeRogue.Core.Data;
using PokeRogue.Core.Geometry;
using PokeRogue.Game.Graphics;

namespace PokeRogue.Game.Preview.Artboard;

public sealed partial class ArtboardScene
{
    private static readonly int[] HordeSpecies = [19, 16, 10, 13, 21, 23, 19, 16, 29, 32];

    private readonly List<int> _boltTargets = [];
    private readonly List<(Vector2 Position, int Type)> _gems = [];
    private readonly List<Vector2> _coins = [];

    private void BuildHorde()
    {
        _hero = Mon(25, _player, Direction8.Right, PmdAnimations.Walk);

        _mons.Add(Mon(20, new Vector2(486, 132), Direction8.DownLeft, PmdAnimations.Walk, 1.5f, elite: true));
        for (var attempts = 0; _mons.Count < 52 && attempts < 2000; attempts++)
        {
            var angle = _random.NextSingle() * MathF.Tau;
            var radius = 70 + _random.NextSingle() * 260;
            var position = _player + new Vector2(MathF.Cos(angle) * radius * 1.2f, MathF.Sin(angle) * radius * 0.75f);
            if (position.X is < 12 or > 628 || position.Y is < 64 or > 300)
                continue;
            if (_mons.Any(m => Vector2.Distance(m.Position, position) < 18))
                continue;

            var species = HordeSpecies[_random.Next(HordeSpecies.Length)];
            _mons.Add(Mon(species, position, Direction8Extensions.FromVector(ToNumerics(_player - position)), PmdAnimations.Walk));
        }

        _boltTargets.AddRange(_mons
            .Select((m, i) => (Index: i, Distance: Vector2.Distance(m.Position, _player)))
            .Where(x => !_mons[x.Index].Elite && _mons[x.Index].Position.X > _player.X)
            .OrderBy(x => x.Distance)
            .Take(3)
            .Select(x => x.Index));

        for (var i = 0; i < 30; i++)
        {
            var p = new Vector2(_random.Next(20, 620), _random.Next(70, 300));
            if (Vector2.Distance(p, _player) > 30)
                _gems.Add((p, _random.Next(10) < 6 ? 0 : _random.Next(10) < 7 ? 1 : 2));
        }

        for (var i = 0; i < 6; i++)
            _coins.Add(new Vector2(_random.Next(40, 600), _random.Next(80, 290)));
    }

    private void DrawGameplay(SpriteBatch batch, bool battleBoxHud)
    {
        var p = Services.Primitives;
        var flashOn = _tick / 6 % 2 == 0;

        batch.BeginVirtual(samplerState: SamplerState.PointWrap);
        batch.Draw(_art.Grass, new Rectangle(0, 0, 640, 360), new Rectangle(37, 11, 640, 360), Color.White);

        foreach (var (position, type) in _gems)
        {
            var bob = MathF.Round(MathF.Sin((_tick + position.X) * 0.08f));
            var texture = type switch { 0 => _art.GemSmall, 1 => _art.GemMedium, _ => _art.GemLarge };
            batch.Draw(texture, position + new Vector2(0, bob), Color.White);
        }

        foreach (var coin in _coins)
            batch.Draw(_art.Coin, coin, Color.White);

        foreach (var m in _mons)
            DrawShadow(batch, m);
        DrawShadow(batch, _hero);

        // Elite ("Dominante") aura under the sprite
        foreach (var elite in _mons.Where(m => m.Elite))
        {
            var pulse = 0.5f + 0.5f * MathF.Sin(_tick * 0.12f);
            p.Ellipse(batch, elite.Position, 46, 18, new Color(248, 96, 32) * (0.25f + 0.2f * pulse));
            p.Ellipse(batch, elite.Position, 34, 12, new Color(255, 200, 64) * (0.25f + 0.2f * pulse));
        }

        var drawList = _mons.Select((m, i) => (Mon: m, Index: i)).Append((Mon: _hero, Index: -1)).OrderBy(x => x.Mon.Position.Y);
        foreach (var (mon, index) in drawList)
        {
            var m = mon;
            if (m.Elite)
            {
                var set = Services.Sprites.Get(m.Species)!;
                foreach (var offset in new[] { new Vector2(1, 0), new Vector2(-1, 0), new Vector2(0, 1), new Vector2(0, -1) })
                    set.Draw(batch, m.Animator.Anim!, m.Animator.Frame, m.Direction, m.Position + offset, new Color(255, 120, 40), m.Scale, silhouette: true);
            }

            m.Flash = index >= 0 && _boltTargets.Take(2).Contains(index) && flashOn;
            DrawMon(batch, m);
        }

        batch.End();

        // Thundershock: procedural pixel lightning with additive glow
        batch.BeginVirtual(blendState: BlendState.Additive, samplerState: SamplerState.PointClamp);
        for (var i = 0; i < _boltTargets.Count; i++)
        {
            var target = _mons[_boltTargets[i]].Position - new Vector2(0, 10);
            DrawBolt(batch, _player + new Vector2(6, -14), target, _tick / 4 * 31 + i * 7);
            DrawSpark(batch, target, _tick + i * 5);
        }

        batch.End();

        batch.BeginVirtual(samplerState: SamplerState.PointClamp);
        DrawDamageNumbers(batch);
        if (battleBoxHud)
            DrawHudBattleBox(batch);
        else
            DrawHudClassic(batch);
        batch.End();
    }

    private void DrawBolt(SpriteBatch batch, Vector2 from, Vector2 to, int seed)
    {
        var random = new Random(seed);
        var delta = to - from;
        var normal = Vector2.Normalize(new Vector2(-delta.Y, delta.X));
        var segments = System.Math.Max(3, (int)(delta.Length() / 12));
        var points = new List<Vector2> { from };
        for (var i = 1; i < segments; i++)
            points.Add(from + delta * (i / (float)segments) + normal * random.Next(-6, 7));
        points.Add(to);

        var p = Services.Primitives;
        for (var i = 0; i < points.Count - 1; i++)
            UiDraw.Line(batch, p, points[i], points[i + 1], new Color(255, 200, 40) * 0.35f, 5f);
        for (var i = 0; i < points.Count - 1; i++)
            UiDraw.Line(batch, p, points[i], points[i + 1], new Color(255, 240, 120) * 0.9f, 2f);
        for (var i = 0; i < points.Count - 1; i++)
            UiDraw.Line(batch, p, points[i], points[i + 1], Color.White * 0.8f, 1f);
    }

    private void DrawSpark(SpriteBatch batch, Vector2 at, int tick)
    {
        var p = Services.Primitives;
        var spin = tick * 0.15f;
        for (var r = 0; r < 6; r++)
        {
            var angle = spin + r * MathF.Tau / 6;
            var dir = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
            UiDraw.Line(batch, p, at + dir * 4, at + dir * (9 + tick % 3), new Color(255, 236, 128) * 0.9f, 1f);
        }

        p.Ellipse(batch, at, 3, 3, Color.White * 0.8f);
    }

    private void DrawDamageNumbers(SpriteBatch batch)
    {
        var rise = _tick % 45 * 0.35f;
        var font = Font(16);
        for (var i = 0; i < _boltTargets.Count; i++)
        {
            var target = _mons[_boltTargets[i]];
            var superEffective = _mons[_boltTargets[i]].Species is 16 or 21;
            var text = superEffective ? "36!" : (18 + i * 3).ToString();
            var color = superEffective ? new Color(255, 152, 48) : Paper;
            UiDraw.TextOutlined(batch, font, text, target.Position + new Vector2(-8 + i * 3, -38 - rise), color, Ink);
        }

        var elite = _mons.First(m => m.Elite);
        UiDraw.TextOutlined(batch, font, "7", elite.Position + new Vector2(14, -52 - rise * 0.6f), new Color(150, 150, 160), Ink);
    }

    private void DrawHudClassic(SpriteBatch batch)
    {
        var p = Services.Primitives;
        var theme = UiTheme.Dark;

        // XP bar across the top, level on the right
        UiDraw.Bar(batch, p, new Rectangle(2, 2, 636, 8), 0.62f, new Color(72, 152, 248), new Color(24, 20, 44), Ink);
        UiDraw.TextOutlined(batch, Font(16), "Lv 7", new Vector2(598, 11), Paper, Ink);

        // Move slots (row 1) and held items (row 2)
        for (var i = 0; i < 6; i++)
        {
            var moveSlot = new Rectangle(3 + i * 26, 13, 24, 24);
            var itemSlot = new Rectangle(3 + i * 26, 40, 24, 24);
            UiDraw.Frame(batch, p, moveSlot, theme);
            UiDraw.Frame(batch, p, itemSlot, theme);
        }

        DrawMoveIcon(batch, new Rectangle(3, 13, 24, 24), PokemonType.Electric, _art.Bolt, 4);
        DrawMoveIcon(batch, new Rectangle(29, 13, 24, 24), PokemonType.Normal, _art.Swoosh, 2);

        string[] items = ["leftovers", "quick-claw", "lucky-egg"];
        for (var i = 0; i < items.Length; i++)
        {
            if (Services.Sprites.Texture(Path.Combine("icons", "items", items[i] + ".png")) is { } icon)
                batch.Draw(icon, new Vector2(3 + i * 26, 40), new Rectangle(3, 3, 24, 24), Color.White);
        }

        // Timer and counters
        var timer = "05:42";
        var size = UiDraw.Measure(Font(16), timer);
        UiDraw.TextOutlined(batch, Font(16), timer, new Vector2(320 - size.X / 2, 12), Paper, Ink);
        DrawCounters(batch, new Vector2(636, 32));

        // HP bar under the player
        UiDraw.Bar(batch, p, new Rectangle((int)_player.X - 13, (int)_player.Y + 5, 26, 4), 0.8f, UiDraw.HpColor(0.8f), new Color(40, 24, 32), Ink);

        // Trainer card with active-ability cooldown
        var card = new Rectangle(4, 312, 150, 44);
        UiDraw.Frame(batch, p, card, theme);
        DrawTrainerHead(batch, "ash", new Rectangle(8, 316, 36, 36), cooldown: 0.4f, key: "Q");
        UiDraw.Text(batch, Font(16), "Ash", new Vector2(50, 314), theme.Text);
        UiDraw.Text(batch, Small, "Determinazione", new Vector2(50, 332), theme.TextDim);
        UiDraw.Text(batch, Small, "pronta tra 6 s", new Vector2(50, 342), theme.TextDim);

        // Special move gauge
        var glow = 0.75f + 0.25f * MathF.Sin(_tick * 0.2f);
        UiDraw.TextOutlined(batch, Small, "SPECIALE  Fulmine", new Vector2(236, 334), Gold, Ink);
        UiDraw.Bar(batch, p, new Rectangle(236, 345, 168, 7), 0.7f, new Color(248, 208, 72) * glow, new Color(40, 32, 20), Ink);
        UiDraw.TextOutlined(batch, Small, "[SPAZIO]", new Vector2(410, 344), theme.TextDim, Ink);
    }

    private void DrawHudBattleBox(SpriteBatch batch)
    {
        var p = Services.Primitives;
        var theme = UiTheme.Light;

        var timer = "05:42";
        var size = UiDraw.Measure(Font(16), timer);
        UiDraw.TextOutlined(batch, Font(16), timer, new Vector2(320 - size.X / 2, 6), Paper, Ink);
        DrawCounters(batch, new Vector2(636, 6));

        // Trainer portrait above the battle box
        var portrait = new Rectangle(6, 256, 44, 44);
        UiDraw.Frame(batch, p, portrait, theme);
        DrawTrainerHead(batch, "ash", new Rectangle(10, 260, 36, 36), cooldown: 0.4f, key: "Q");

        // Battle box: name, level, HP, special gauge, EXP strip
        var box = new Rectangle(6, 302, 190, 52);
        UiDraw.Frame(batch, p, box, theme);
        UiDraw.Text(batch, Font(16), "PIKACHU", new Vector2(14, 304), theme.Text);
        UiDraw.Text(batch, Font(16), "Lv7", new Vector2(160, 304), theme.Text);
        UiDraw.Text(batch, Small, "PS", new Vector2(14, 324), new Color(232, 136, 40));
        UiDraw.Bar(batch, p, new Rectangle(28, 324, 110, 6), 0.8f, UiDraw.HpColor(0.8f), new Color(80, 88, 88), new Color(40, 48, 56));
        UiDraw.Text(batch, Small, "48/60", new Vector2(146, 323), theme.Text);
        UiDraw.Text(batch, Small, "SP", new Vector2(14, 334), new Color(200, 152, 24));
        UiDraw.Bar(batch, p, new Rectangle(28, 334, 110, 6), 0.7f, Gold, new Color(80, 88, 88), new Color(40, 48, 56));
        UiDraw.Text(batch, Small, "[SPAZIO]", new Vector2(146, 333), theme.TextDim);
        UiDraw.Bar(batch, p, new Rectangle(10, 346, 182, 4), 0.62f, new Color(64, 168, 248), new Color(200, 208, 216), new Color(40, 48, 56));

        // Moves panel
        var panel = new Rectangle(452, 302, 182, 52);
        UiDraw.Frame(batch, p, panel, theme);
        (PokemonType Type, string Name, string Level)[] moves =
        [
            (PokemonType.Electric, "Tuonoshock", "Lv4"),
            (PokemonType.Normal, "Attacco Rapido", "Lv2"),
            (PokemonType.Steel, "Codacciaio", "--"),
        ];
        for (var i = 0; i < moves.Length; i++)
        {
            var y = 306 + i * 15;
            DrawTypePill(batch, new Rectangle(458, y + 1, 44, 11), moves[i].Type);
            UiDraw.Text(batch, Small, moves[i].Name, new Vector2(506, y + 2), moves[i].Level == "--" ? theme.TextDim : theme.Text);
            UiDraw.Text(batch, Small, moves[i].Level, new Vector2(610, y + 2), theme.TextDim);
        }

        // Held items floating above the moves panel
        string[] items = ["leftovers", "quick-claw", "lucky-egg"];
        for (var i = 0; i < items.Length; i++)
        {
            var slot = new Rectangle(610 - i * 28, 272, 26, 26);
            p.Rect(batch, slot, new Color(16, 12, 28) * 0.6f);
            if (Services.Sprites.Texture(Path.Combine("icons", "items", items[i] + ".png")) is { } icon)
                batch.Draw(icon, new Vector2(slot.X + 1, slot.Y + 1), new Rectangle(3, 3, 24, 24), Color.White);
        }
    }

    private void DrawCounters(SpriteBatch batch, Vector2 topRight)
    {
        var font = Font(16);
        var kills = "312";
        var coins = "128";
        var killWidth = UiDraw.Measure(font, kills).X;
        var coinWidth = UiDraw.Measure(font, coins).X;
        UiDraw.TextOutlined(batch, font, kills, topRight + new Vector2(-killWidth, -2), Paper, Ink);
        batch.Draw(_art.Skull, topRight + new Vector2(-killWidth - 10, 3), Color.White);
        UiDraw.TextOutlined(batch, font, coins, topRight + new Vector2(-coinWidth - 52, -2), Gold, Ink);
        batch.Draw(_art.Coin, topRight + new Vector2(-coinWidth - 62, 3), Color.White);
    }

    private void DrawMoveIcon(SpriteBatch batch, Rectangle slot, PokemonType type, Texture2D glyph, int level)
    {
        var p = Services.Primitives;
        var inner = new Rectangle(slot.X + 2, slot.Y + 2, slot.Width - 4, slot.Height - 4);
        p.Rect(batch, inner, TypePalette.Of(type) * 0.85f);
        p.Rect(batch, new Rectangle(inner.X, inner.Y, inner.Width, 2), Color.White * 0.35f);
        batch.Draw(glyph, new Vector2(slot.Center.X - glyph.Width / 2, slot.Center.Y - glyph.Height / 2 - 1), Color.White);
        for (var i = 0; i < 8; i++)
            p.Rect(batch, new Rectangle(inner.X + 1 + i * 2 + i / 4, inner.Bottom - 3, 1, 2), i < level ? Color.White : Ink * 0.6f);
    }

    private void DrawTrainerHead(SpriteBatch batch, string trainer, Rectangle destination, float cooldown, string key)
    {
        var p = Services.Primitives;
        p.Rect(batch, destination, new Color(120, 168, 216));
        if (Services.Sprites.Texture(Path.Combine("trainers", trainer + ".png")) is { } sprite)
            batch.Draw(sprite, destination, new Rectangle(22, 4, 36, 36), Color.White);

        var covered = (int)(destination.Height * cooldown);
        p.Rect(batch, new Rectangle(destination.X, destination.Y, destination.Width, covered), new Color(8, 6, 16) * 0.67f);
        UiDraw.TextOutlined(batch, Small, key, new Vector2(destination.Right - 7, destination.Bottom - 9), Gold, Ink);
    }

    private void DrawTypePill(SpriteBatch batch, Rectangle r, PokemonType type)
    {
        var p = Services.Primitives;
        var color = TypePalette.Of(type);
        p.Rect(batch, new Rectangle(r.X + 1, r.Y, r.Width - 2, r.Height), Color.Lerp(color, Ink, 0.45f));
        p.Rect(batch, new Rectangle(r.X, r.Y + 1, r.Width, r.Height - 2), Color.Lerp(color, Ink, 0.45f));
        p.Rect(batch, new Rectangle(r.X + 1, r.Y + 1, r.Width - 2, r.Height - 2), color);
        var name = TypePalette.NameIt(type).ToUpperInvariant();
        var size = UiDraw.Measure(Small, name);
        UiDraw.TextOutlined(batch, Small, name, new Vector2(r.Center.X - size.X / 2, r.Y + (r.Height - size.Y) / 2), Color.White, Color.Lerp(color, Ink, 0.6f));
    }

    private static System.Numerics.Vector2 ToNumerics(Vector2 v) => new(v.X, v.Y);
}
