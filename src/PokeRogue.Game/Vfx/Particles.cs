using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PokeRogue.Game.Graphics;

namespace PokeRogue.Game.Vfx;

/// <summary>Square pixel particles in world space, drawn additively. Fixed pool; the oldest are dropped when full.</summary>
public sealed class Particles(int capacity = 2048)
{
    private readonly Particle[] _items = new Particle[capacity];
    private int _next;

    private struct Particle
    {
        public Vector2 Position;
        public Vector2 Velocity;
        public Color Start;
        public Color End;
        public float Drag;
        public float Gravity;
        public int Life;
        public int Age;
        public float Size;

        /// <summary>Height ÷ width: 1 = square speck, 2+ = upright tongue (flames).</summary>
        public float Stretch;
    }

    public int Active { get; private set; }

    public void Emit(Vector2 position, Vector2 velocity, Color start, Color end, int life, float size = 1f, float drag = 0.9f, float gravity = 0f, float stretch = 1f)
    {
        ref var p = ref _items[_next];
        _next = (_next + 1) % _items.Length;
        p = new Particle
        {
            Position = position,
            Velocity = velocity,
            Start = start,
            End = end,
            Life = life,
            Size = size,
            Drag = drag,
            Gravity = gravity,
            Stretch = stretch,
        };
    }

    /// <summary>Radial burst, e.g. electric sparks on impact.</summary>
    public void Burst(Vector2 at, int count, float speed, Color start, Color end, int life, Random random, float size = 1f)
    {
        for (var i = 0; i < count; i++)
        {
            var angle = random.NextSingle() * MathF.Tau;
            var v = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * speed * (0.5f + random.NextSingle() * 0.7f);
            Emit(at, v, start, end, life - random.Next(life / 3 + 1), size, 0.86f);
        }
    }

    public void Update()
    {
        var active = 0;
        for (var i = 0; i < _items.Length; i++)
        {
            ref var p = ref _items[i];
            if (p.Age >= p.Life)
                continue;

            p.Age++;
            p.Position += p.Velocity / 60f;
            p.Velocity *= p.Drag;
            p.Velocity.Y += p.Gravity;
            active++;
        }

        Active = active;
    }

    /// <summary>Call inside an additive batch using the camera transform.</summary>
    public void Draw(SpriteBatch batch, Primitives primitives)
    {
        foreach (ref readonly var p in _items.AsSpan())
        {
            if (p.Age >= p.Life)
                continue;

            var t = p.Age / (float)p.Life;
            var color = Color.Lerp(p.Start, p.End, t) * (1f - t * t);
            var size = System.Math.Max(1, (int)MathF.Round(p.Size * (1f - t * 0.5f)));
            var height = System.Math.Max(size, (int)MathF.Round(size * System.Math.Max(1f, p.Stretch) * (1f - t * 0.3f)));
            primitives.Rect(batch, new Rectangle((int)MathF.Round(p.Position.X - size / 2f), (int)MathF.Round(p.Position.Y - height / 2f), size, height), color);
        }
    }
}
