using FontStashSharp;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PokeRogue.Game.Graphics;

namespace PokeRogue.Game.Vfx;

/// <summary>
/// Floating damage numbers: white normal, orange "!" super effective, grey resisted, yellow critical, red "KO!" for a
/// one-hit knock-out; damage over time from statuses in small numbers of the status colour.
/// </summary>
public sealed class DamageNumbers(int capacity = 256)
{
    public const int Life = 40;

    public static readonly Color Normal = new(244, 240, 228);
    public static readonly Color SuperEffective = new(255, 152, 48);
    public static readonly Color Resisted = new(160, 160, 172);
    public static readonly Color Immune = new(112, 112, 124);
    public static readonly Color Critical = new(255, 228, 72);

    /// <summary>"KO!" of a one-hit knock-out (Abisso).</summary>
    public static readonly Color Ohko = new(255, 84, 72);
    private static readonly Color Outline = new(16, 12, 28);

    private readonly Number[] _items = new Number[capacity];
    private int _next;

    private struct Number
    {
        public Vector2 Position;
        public string Text;
        public Color Color;
        public int Age;
        public float Drift;
        public bool Small;
    }

    public void Add(Vector2 position, float amount, float effectiveness, bool critical, Random random, bool ohko = false)
    {
        var color = ohko ? Ohko
            : critical ? Critical
            : effectiveness > 1.01f ? SuperEffective
            : effectiveness <= 0.26f ? Immune
            : effectiveness < 0.99f ? Resisted
            : Normal;
        var text = ohko ? "KO!" : ((int)amount).ToString() + (effectiveness > 1.01f ? "!" : "");

        ref var n = ref _items[_next];
        _next = (_next + 1) % _items.Length;
        n = new Number { Position = position, Text = text, Color = color, Drift = (random.NextSingle() - 0.5f) * 10f };
    }

    /// <summary>Damage from a status condition: a small number in the status colour.</summary>
    public void AddStatus(Vector2 position, float amount, Color color, Random random)
    {
        ref var n = ref _items[_next];
        _next = (_next + 1) % _items.Length;
        var text = System.Math.Max(1, (int)MathF.Round(amount)).ToString();
        n = new Number { Position = position, Text = text, Color = color, Drift = (random.NextSingle() - 0.5f) * 8f, Small = true };
    }

    public void Update()
    {
        for (var i = 0; i < _items.Length; i++)
        {
            if (_items[i].Text is not null && _items[i].Age < Life)
                _items[i].Age++;
        }
    }

    /// <summary>Call inside a batch using the camera transform; <paramref name="small"/> is for status damage.</summary>
    public void Draw(SpriteBatch batch, SpriteFontBase font, SpriteFontBase small)
    {
        foreach (ref readonly var n in _items.AsSpan())
        {
            if (n.Text is null || n.Age >= Life)
                continue;

            // Quick ease-out rise, then hold and fade; small numbers start on the body and stay under the status icon.
            var t = n.Age / (float)Life;
            var rise = 1f - (1f - MathF.Min(1f, t * 2.2f)) * (1f - MathF.Min(1f, t * 2.2f));
            var alpha = t < 0.75f ? 1f : 1f - (t - 0.75f) / 0.25f;
            var face = n.Small ? small : font;
            var size = UiDraw.Measure(face, n.Text);
            var lift = n.Small ? -12f - rise * 8f : -26f - rise * 16f;
            var position = n.Position + new Vector2(n.Drift * rise - size.X / 2f, lift);
            UiDraw.TextOutlined(batch, face, n.Text, position, n.Color * alpha, Outline * alpha);
        }
    }
}
