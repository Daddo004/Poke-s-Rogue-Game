using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PokeRogue.Core.Data;
using PokeRogue.Game.Graphics;
using PokeRogue.Game.Vfx;

namespace PokeRogue.Game.Ui;

/// <summary>The full-screen picture of each type's special move. <c>t</c> runs 0 to 1; the impact comes at about 0.73.</summary>
public sealed partial class SpecialWindow
{
    /// <summary>Fuocobomba: a wall of flames rising from the bottom of the screen, embers flying up.</summary>
    private void Fire(SpriteBatch batch, float t, int age)
    {
        var p = services.Primitives;
        var pal = TypeFx.For(PokemonType.Fire);
        var rise = Ease(MathHelper.Clamp(t * 1.5f, 0f, 1f)) * (t > 0.85f ? 1f - (t - 0.85f) / 0.15f : 1f);
        for (var x = -8; x < W + 16; x += 14)
        {
            var i = x + 8;
            var height = H * (0.35f + 0.45f * R(i)) * rise + MathF.Sin(age * 0.4f + i) * 6f;
            for (var k = 0; k < 6; k++)
            {
                var f = k / 5f;
                var color = f < 0.4f ? Color.Lerp(pal.Dark, pal.Main, f / 0.4f) : Color.Lerp(pal.Main, pal.Hot, (f - 0.4f) / 0.6f);
                var w = (int)(30 * (1f - f * 0.75f));
                var y = H - height * (1f - f * 0.85f);
                p.Ellipse(batch, new Vector2(x + MathF.Sin(age * 0.3f + i + k) * 3f, y + height * 0.5f * (1f - f)), w, (int)(height * (1f - f * 0.7f)) + 2, color * 0.9f);
            }
        }

        for (var i = 0; i < 40; i++)
        {
            var y = H - (age * (2f + R(i, 1) * 3f) + R(i, 2) * H) % H;
            p.Rect(batch, new Rectangle((int)(R(i) * W), (int)y, 2, 3), pal.Spark * 0.9f);
        }
    }

    /// <summary>Idropompa: a huge jet crossing the screen from left to right, with foam at its head.</summary>
    private void Water(SpriteBatch batch, float t, int age)
    {
        var p = services.Primitives;
        var pal = TypeFx.For(PokemonType.Water);
        var head = W * 1.35f * Ease(MathHelper.Clamp(t * 1.25f, 0f, 1f)) - 60f;
        var thick = 150f * (t < 0.8f ? 1f : 1f - (t - 0.8f) / 0.2f);
        var top = H / 2f - thick / 2f;
        p.Rect(batch, new Rectangle(0, (int)top, (int)MathF.Max(0, head), (int)thick), pal.Dark * 0.85f);
        p.Rect(batch, new Rectangle(0, (int)(top + thick * 0.15f), (int)MathF.Max(0, head), (int)(thick * 0.7f)), pal.Main * 0.9f);
        p.Rect(batch, new Rectangle(0, (int)(top + thick * 0.38f), (int)MathF.Max(0, head), (int)(thick * 0.24f)), pal.Hot * 0.8f);
        for (var i = 0; i < 26; i++)
        {
            var y = top + R(i) * thick;
            var x = (age * 14f + R(i, 1) * W) % (W + 80) - 80;
            if (x < head - 20)
                p.Rect(batch, new Rectangle((int)x, (int)y, 40 + (int)(R(i, 2) * 40), 2), pal.Hot * 0.6f);
        }

        for (var i = 0; i < 12; i++)
        {
            var angle = R(i) * MathF.Tau;
            p.Ellipse(batch, new Vector2(head + MathF.Cos(angle) * 22f, H / 2f + MathF.Sin(angle) * thick * 0.5f), 26, 26, Color.White * 0.85f);
        }
    }

    /// <summary>Solarraggio: light gathers into a sun at the top, then broad beams fall on the field.</summary>
    private void Grass(SpriteBatch batch, float t, int age)
    {
        var p = services.Primitives;
        var pal = TypeFx.For(PokemonType.Grass);
        var sun = new Vector2(W / 2f, 40);
        var gather = MathHelper.Clamp(t / 0.45f, 0f, 1f);
        p.Ellipse(batch, sun, (int)(30 + 50 * gather), (int)(30 + 50 * gather), new Color(255, 250, 200) * (0.5f + 0.4f * gather));
        for (var i = 0; i < 24; i++)
        {
            var angle = R(i) * MathF.Tau;
            var distance = 160f * (1f - gather) + 20f;
            var at = sun + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * distance * (0.6f + R(i, 1) * 0.4f);
            p.Rect(batch, new Rectangle((int)at.X, (int)at.Y, 3, 3), pal.Hot);
        }

        if (t < 0.45f)
            return;

        var fall = Ease(MathHelper.Clamp((t - 0.45f) / 0.25f, 0f, 1f));
        var fade = t > 0.85f ? 1f - (t - 0.85f) / 0.15f : 1f;
        for (var k = 0; k < 7; k++)
        {
            var x = W * (k + 0.5f) / 7f + (R(k) - 0.5f) * 40f;
            var width = 40 + (int)(R(k, 1) * 30);
            var height = (int)(H * fall);
            p.Rect(batch, new Rectangle((int)x - width / 2, 0, width, height), pal.Main * (0.45f * fade));
            p.Rect(batch, new Rectangle((int)x - width / 4, 0, width / 2, height), pal.Hot * (0.6f * fade));
        }

        for (var i = 0; i < 20; i++)
        {
            var at = new Vector2((R(i, 3) * W + age * 3f) % W, (R(i, 4) * H + age * 4f) % H);
            MoveShapes.DrawRotated(batch, Shapes.Leaf, at, age * 0.2f + i, Color.White * fade, 2f);
        }
    }

    /// <summary>Falcecannone: an electric ball swelling in the middle, then bolts lashing out to every edge.</summary>
    private void Electric(SpriteBatch batch, float t, int age)
    {
        var p = services.Primitives;
        var pal = TypeFx.For(PokemonType.Electric);
        var grow = Ease(MathHelper.Clamp(t / 0.6f, 0f, 1f));
        var size = (int)(20 + 90 * grow + MathF.Sin(age * 0.9f) * 6f);
        p.Ellipse(batch, Center, size + 20, size + 20, pal.Main * 0.35f);
        p.Ellipse(batch, Center, size, size, pal.Main * 0.85f);
        p.Ellipse(batch, Center, size / 2, size / 2, pal.Hot);

        if (t < 0.55f)
            return;

        var fade = t > 0.85f ? 1f - (t - 0.85f) / 0.15f : 1f;
        for (var b = 0; b < 12; b++)
        {
            if ((age + b) % 3 == 0)
                continue;

            var angle = b * MathF.Tau / 12f + R(b + age / 4) * 0.4f;
            var from = Center;
            var length = MathF.Max(W, H) * 0.75f;
            for (var s = 1; s <= 8; s++)
            {
                var along = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * (length * s / 8f);
                var jitter = new Vector2(-MathF.Sin(angle), MathF.Cos(angle)) * ((R(b * 8 + s, age / 3) - 0.5f) * 30f);
                var to = Center + along + jitter;
                UiDraw.Line(batch, p, from, to, pal.Main * fade, 4f);
                UiDraw.Line(batch, p, from, to, pal.Hot * fade, 2f);
                from = to;
            }
        }
    }

    /// <summary>Bora: a blizzard driving across the screen while frost closes in from the edges.</summary>
    private void Ice(SpriteBatch batch, float t, int age)
    {
        var p = services.Primitives;
        var pal = TypeFx.For(PokemonType.Ice);
        var strength = MathHelper.Clamp(t * 2f, 0f, 1f) * (t > 0.85f ? 1f - (t - 0.85f) / 0.15f : 1f);
        for (var i = 0; i < 140; i++)
        {
            var speed = 9f + R(i, 1) * 8f;
            var x = (R(i) * (W + 200) - age * speed) % (W + 200);
            if (x < 0)
                x += W + 200;
            var y = (R(i, 2) * H + age * speed * 0.5f) % H;
            var at = new Vector2(x - 100, y);
            UiDraw.Line(batch, p, at, at + new Vector2(14, -7), (i % 3 == 0 ? Color.White : pal.Main) * (0.85f * strength), 2f);
        }

        var frost = (int)(60 * strength);
        for (var k = 0; k < 4; k++)
        {
            var inset = frost * (k + 1) / 4;
            var alpha = 0.18f * strength;
            p.Rect(batch, new Rectangle(0, 0, W, inset), pal.Hot * alpha);
            p.Rect(batch, new Rectangle(0, H - inset, W, inset), pal.Hot * alpha);
            p.Rect(batch, new Rectangle(0, 0, inset, H), pal.Hot * alpha);
            p.Rect(batch, new Rectangle(W - inset, 0, inset, H), pal.Hot * alpha);
        }
    }

    /// <summary>Zuffa: a flurry of blows landing all over the screen, faster and faster.</summary>
    private void Fighting(SpriteBatch batch, float t, int age)
    {
        var p = services.Primitives;
        var pal = TypeFx.For(PokemonType.Fighting);
        var count = (int)(28 * MathHelper.Clamp(t * 1.3f, 0f, 1f));
        for (var i = 0; i < count; i++)
        {
            var start = i * 44f / 28f;
            var a = age - start;
            if (a < 0 || a > 12)
                continue;

            var at = new Vector2(40 + R(i) * (W - 80), 40 + R(i, 1) * (H - 80));
            var k = a / 12f;
            var radius = 10f + 34f * Ease(k);
            p.Ellipse(batch, at, (int)(radius * 1.2f), (int)(radius * 1.2f), pal.Main * (0.5f * (1f - k)));
            p.Ellipse(batch, at, (int)(radius * 0.6f), (int)(radius * 0.6f), pal.Hot * (1f - k));
            for (var s = 0; s < 8; s++)
            {
                var angle = s * MathF.Tau / 8f + R(i, 2);
                var dir = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
                UiDraw.Line(batch, p, at + dir * radius * 0.7f, at + dir * radius * 1.3f, Color.White * (1f - k), 2f);
            }
        }
    }

    /// <summary>Sporcolancio: globs of sludge raining on the field and spreading into bubbling pools.</summary>
    private void Poison(SpriteBatch batch, float t, int age)
    {
        var p = services.Primitives;
        var pal = TypeFx.For(PokemonType.Poison);
        for (var i = 0; i < 22; i++)
        {
            var start = R(i) * 30f;
            var a = age - start;
            if (a < 0)
                continue;

            var x = 20 + R(i, 1) * (W - 40);
            var groundY = 60 + R(i, 2) * (H - 90);
            var fall = MathHelper.Clamp(a / 12f, 0f, 1f);
            if (fall < 1f)
            {
                var y = -30 + (groundY + 30) * fall * fall;
                p.Ellipse(batch, new Vector2(x, y), 18, 24, pal.Main);
                p.Ellipse(batch, new Vector2(x - 3, y - 4), 6, 8, pal.Hot);
                continue;
            }

            var spread = Ease(MathHelper.Clamp((a - 12f) / 10f, 0f, 1f));
            var fade = t > 0.85f ? 1f - (t - 0.85f) / 0.15f : 1f;
            p.Ellipse(batch, new Vector2(x, groundY), (int)(30 + 50 * spread), (int)(12 + 18 * spread), pal.Dark * (0.85f * fade));
            p.Ellipse(batch, new Vector2(x, groundY - 2), (int)(20 + 34 * spread), (int)(8 + 10 * spread), pal.Main * (0.8f * fade));
            var bubble = (age + i * 5) % 18;
            p.Ring(batch, new Vector2(x + (R(i, 3) - 0.5f) * 30f, groundY - bubble), 3, 3, pal.Hot * (fade * (1f - bubble / 18f)));
        }
    }

    /// <summary>Magnitudo: cracks racing out from the middle, dust rising along them.</summary>
    private void Ground(SpriteBatch batch, float t, int age)
    {
        var p = services.Primitives;
        var pal = TypeFx.For(PokemonType.Ground);
        var reach = Ease(MathHelper.Clamp(t * 1.4f, 0f, 1f));
        var fade = t > 0.85f ? 1f - (t - 0.85f) / 0.15f : 1f;
        p.Rect(batch, new Rectangle(0, 0, W, H), pal.Dark * (0.25f * fade));
        for (var c = 0; c < 9; c++)
        {
            var angle = c * MathF.Tau / 9f + R(c);
            var from = Center;
            var steps = (int)(10 * reach);
            for (var s = 1; s <= steps; s++)
            {
                angle += (R(c * 10 + s, 1) - 0.5f) * 0.8f;
                var to = from + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * 36f;
                UiDraw.Line(batch, p, from, to, Ink * fade, 5f - s * 0.3f);
                UiDraw.Line(batch, p, from + new Vector2(0, -1), to + new Vector2(0, -1), pal.Hot * (0.5f * fade), 1f);
                if (s % 3 == 0)
                    p.Ellipse(batch, to + new Vector2(0, -8 - (age % 10)), 24, 14, pal.Spark * (0.35f * fade));
                from = to;
            }
        }
    }

    /// <summary>Baldeali: a great bird of light sweeping across the screen in a gale.</summary>
    private void Flying(SpriteBatch batch, float t, int age)
    {
        var p = services.Primitives;
        var pal = TypeFx.For(PokemonType.Flying);
        for (var i = 0; i < 50; i++)
        {
            var x = (R(i) * (W + 120) + age * (16f + R(i, 1) * 10f)) % (W + 120) - 60;
            var y = R(i, 2) * H;
            UiDraw.Line(batch, p, new Vector2(x, y), new Vector2(x + 40, y), Color.White * 0.5f, 1f);
        }

        // The bird: a broad chevron of light with a bright head, flying left to right through the middle.
        var head = new Vector2(-160 + (W + 320) * MathHelper.Clamp((t - 0.15f) / 0.75f, 0f, 1f), H / 2f);
        var flap = MathF.Sin(age * 0.5f) * 24f;
        for (var k = 0; k < 6; k++)
        {
            var back = 30f + k * 22f;
            var span = 50f + k * 26f + flap;
            var color = Color.Lerp(pal.Hot, pal.Main, k / 6f) * (1f - k * 0.12f);
            UiDraw.Line(batch, p, head - new Vector2(k * 8f, 0), head + new Vector2(-back, -span), color, 6f - k * 0.6f);
            UiDraw.Line(batch, p, head - new Vector2(k * 8f, 0), head + new Vector2(-back, span), color, 6f - k * 0.6f);
        }

        p.Ellipse(batch, head, 34, 22, pal.Hot);
        p.Ellipse(batch, head + new Vector2(-60, 0), 120, 10, pal.Main * 0.6f);
    }

    /// <summary>Psichico: pink rings pulsing out from the middle while the field ripples.</summary>
    private void Psychic(SpriteBatch batch, float t, int age)
    {
        var p = services.Primitives;
        var pal = TypeFx.For(PokemonType.Psychic);
        var fade = t > 0.85f ? 1f - (t - 0.85f) / 0.15f : 1f;
        p.Rect(batch, new Rectangle(0, 0, W, H), pal.Main * (0.12f + 0.08f * MathF.Sin(age * 0.3f)) * fade);
        for (var y = 0; y < H; y += 8)
        {
            var shift = MathF.Sin(y * 0.06f + age * 0.35f) * 10f;
            p.Rect(batch, new Rectangle((int)shift, y, W, 1), pal.Hot * (0.1f * fade));
        }

        for (var r = 0; r < 7; r++)
        {
            var a = (age * 6f + r * 60f) % 420f;
            var alpha = (1f - a / 420f) * fade;
            p.Ring(batch, Center, a, a * 0.75f, pal.Hot * alpha, 3);
            p.Ring(batch, Center, a + 4, (a + 4) * 0.75f, pal.Main * (alpha * 0.6f), 2);
        }
    }

    /// <summary>Ronzio: arcs of sound buzzing out from the middle in quick green waves.</summary>
    private void Bug(SpriteBatch batch, float t, int age)
    {
        var p = services.Primitives;
        var pal = TypeFx.For(PokemonType.Bug);
        var fade = t > 0.85f ? 1f - (t - 0.85f) / 0.15f : 1f;
        p.Rect(batch, new Rectangle(0, 0, W, H), pal.Dark * (0.2f * fade));
        for (var r = 0; r < 10; r++)
        {
            var radius = (age * 9f + r * 45f) % 450f;
            var alpha = MathF.Min(1f, 1.6f * (1f - radius / 450f)) * fade;
            var steps = 40;
            for (var s = 0; s < steps; s++)
            {
                // Dashed arcs, the dashes turning a little each wave, like a buzz.
                if ((s + r) % 4 == 3)
                    continue;
                var angle = s * MathF.Tau / steps + r * 0.2f;
                var wobble = MathF.Sin(angle * 6f + age * 0.8f) * 4f;
                var at = Center + new Vector2(MathF.Cos(angle), MathF.Sin(angle) * 0.75f) * (radius + wobble);
                p.Rect(batch, new Rectangle((int)at.X - 4, (int)at.Y - 3, 8, 6), pal.Dark * alpha);
                p.Rect(batch, new Rectangle((int)at.X - 3, (int)at.Y - 2, 6, 4), (r % 2 == 0 ? pal.Main : pal.Hot) * alpha);
            }
        }
    }

    /// <summary>Devastomasso: huge boulders crashing down on the field, dust bursting where they land.</summary>
    private void Rock(SpriteBatch batch, float t, int age)
    {
        var p = services.Primitives;
        var pal = TypeFx.For(PokemonType.Rock);
        for (var i = 0; i < 7; i++)
        {
            var start = 6f + i * 5f;
            var a = age - start;
            if (a < 0)
                continue;

            var ground = new Vector2(60 + R(i) * (W - 120), 90 + R(i, 1) * (H - 140));
            var fall = MathHelper.Clamp(a / 10f, 0f, 1f);
            var scale = 5f + R(i, 2) * 3f;
            if (fall < 1f)
            {
                p.Ellipse(batch, ground, (int)(30 * scale * fall / 4f), (int)(10 * scale * fall / 4f), Color.Black * 0.35f);
                MoveShapes.DrawRotated(batch, Shapes.Rock, new Vector2(ground.X, -60 + (ground.Y + 60) * fall * fall), i, Color.White, scale);
                continue;
            }

            var dust = MathHelper.Clamp((a - 10f) / 16f, 0f, 1f);
            MoveShapes.DrawRotated(batch, Shapes.Rock, ground, i, Color.White * (1f - dust * 0.6f), scale);
            p.Ring(batch, ground, 20 + 70 * dust, (20 + 70 * dust) * 0.5f, pal.Spark * (1f - dust), 3);
            p.Ellipse(batch, ground + new Vector2(0, 10), (int)(60 + 60 * dust), (int)(20 + 20 * dust), pal.Main * (0.4f * (1f - dust)));
        }
    }

    /// <summary>Oscurotuffo: the Pokémon vanishes into the dark, ghostly wisps rise, then it strikes out of nowhere.</summary>
    private void Ghost(SpriteBatch batch, float t, int age)
    {
        var p = services.Primitives;
        var pal = TypeFx.For(PokemonType.Ghost);
        var fade = t > 0.85f ? 1f - (t - 0.85f) / 0.15f : 1f;
        p.Rect(batch, new Rectangle(0, 0, W, H), pal.Dark * (0.4f * fade));
        // Ghosts: a pale round head with two dark eyes and a wavering tail fading below it.
        for (var i = 0; i < 11; i++)
        {
            var x = 30 + R(i) * (W - 60) + MathF.Sin(age * 0.12f + i) * 16f;
            var y = H + 50 - ((age * (2.5f + R(i, 1) * 2f) + R(i, 2) * H) % (H + 100));
            for (var k = 3; k >= 1; k--)
                p.Ellipse(batch, new Vector2(x + MathF.Sin(age * 0.25f + k) * 4f, y + 8 + k * 9f), 22 - k * 5, 14 - k * 2, pal.Main * ((0.55f - k * 0.13f) * fade));
            p.Ellipse(batch, new Vector2(x, y), 26, 24, pal.Main * (0.8f * fade));
            p.Ellipse(batch, new Vector2(x, y - 1), 22, 20, pal.Hot * (0.9f * fade));
            p.Rect(batch, new Rectangle((int)x - 6, (int)y - 3, 3, 5), Ink * fade);
            p.Rect(batch, new Rectangle((int)x + 3, (int)y - 3, 3, 5), Ink * fade);
        }

        // The strike: a purple slash from nowhere just before the impact.
        if (t > 0.62f)
        {
            var k = MathHelper.Clamp((t - 0.62f) / 0.12f, 0f, 1f);
            var from = new Vector2(W * 0.1f, H * 0.85f);
            var to = from + (new Vector2(W * 0.8f, -H * 0.7f) * Ease(k));
            UiDraw.Line(batch, p, from, to, pal.Main * fade, 14f);
            UiDraw.Line(batch, p, from, to, pal.Hot * fade, 5f);
        }
    }

    /// <summary>Oltraggio: a dragon's fury, a red and violet aura flaring from the middle with spikes of energy.</summary>
    private void Dragon(SpriteBatch batch, float t, int age)
    {
        var p = services.Primitives;
        var pal = TypeFx.For(PokemonType.Dragon);
        var fury = new Color(230, 60, 80);
        var fade = t > 0.85f ? 1f - (t - 0.85f) / 0.15f : 1f;
        var grow = Ease(MathHelper.Clamp(t * 1.4f, 0f, 1f));
        for (var k = 0; k < 4; k++)
        {
            var size = (int)((80 + k * 70) * grow + MathF.Sin(age * 0.5f + k) * 10f);
            p.Ellipse(batch, Center, size, (int)(size * 0.8f), Color.Lerp(pal.Main, fury, k / 4f) * ((0.35f - k * 0.06f) * fade));
        }

        for (var s = 0; s < 16; s++)
        {
            var angle = s * MathF.Tau / 16f + age * 0.03f;
            var length = (120f + R(s, age / 5) * 160f) * grow;
            var dir = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
            UiDraw.Line(batch, p, Center + dir * 30f, Center + dir * length, (s % 2 == 0 ? fury : pal.Hot) * fade, 4f);
        }
    }

    /// <summary>Nottesferza: the screen goes black, then three slashes cut across it.</summary>
    private void Dark(SpriteBatch batch, float t, int age)
    {
        var p = services.Primitives;
        var fade = t > 0.85f ? 1f - (t - 0.85f) / 0.15f : 1f;
        p.Rect(batch, new Rectangle(0, 0, W, H), Color.Black * (0.55f * MathHelper.Clamp(t * 3f, 0f, 1f) * fade));
        (Vector2 From, Vector2 To, float At)[] cuts =
        [
            (new Vector2(-20, 60), new Vector2(W + 20, H - 80), 0.42f),
            (new Vector2(W + 20, 40), new Vector2(-20, H - 30), 0.52f),
            (new Vector2(-20, H / 2f + 10), new Vector2(W + 20, H / 2f - 30), 0.62f),
        ];
        foreach (var (from, to, at) in cuts)
        {
            if (t < at)
                continue;

            var k = MathHelper.Clamp((t - at) / 0.06f, 0f, 1f);
            var tip = Vector2.Lerp(from, to, Ease(k));
            UiDraw.Line(batch, p, from, tip, new Color(120, 100, 140) * fade, 9f);
            UiDraw.Line(batch, p, from, tip, Color.White * fade, 3f);
        }
    }

    /// <summary>Meteorpugno: silver comets streaking down from the top right.</summary>
    private void Steel(SpriteBatch batch, float t, int age)
    {
        var p = services.Primitives;
        var pal = TypeFx.For(PokemonType.Steel);
        var direction = Vector2.Normalize(new Vector2(-1f, 0.7f));
        for (var i = 0; i < 9; i++)
        {
            var start = i * 3.5f;
            var a = age - start;
            if (a < 0)
                continue;

            var origin = new Vector2(W * (0.4f + R(i) * 0.9f), -40 - R(i, 1) * 60f);
            var head = origin + direction * (a * 22f);
            if (head.X < -80 || head.Y > H + 80)
                continue;

            for (var k = 0; k < 8; k++)
            {
                var at = head - direction * (k * 12f);
                p.Ellipse(batch, at, 26 - k * 3, 26 - k * 3, Color.Lerp(pal.Hot, pal.Dark, k / 8f) * (1f - k * 0.1f));
            }

            p.Ellipse(batch, head, 16, 16, Color.White);
        }
    }

    /// <summary>Iper Raggio: light gathering in the middle, then a giant beam across the whole screen.</summary>
    private void Normal(SpriteBatch batch, float t, int age)
    {
        var p = services.Primitives;
        var pal = TypeFx.For(PokemonType.Normal);
        if (t < 0.4f)
        {
            var gather = t / 0.4f;
            p.Ellipse(batch, Center, (int)(20 + 40 * gather), (int)(20 + 40 * gather), pal.Hot * 0.9f);
            for (var i = 0; i < 20; i++)
            {
                var angle = R(i) * MathF.Tau;
                var at = Center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * (200f * (1f - gather) + 20f);
                p.Rect(batch, new Rectangle((int)at.X, (int)at.Y, 3, 3), Color.White);
            }

            return;
        }

        var k = MathHelper.Clamp((t - 0.4f) / 0.15f, 0f, 1f);
        var thick = 120f * Ease(k) * (t > 0.8f ? 1f - (t - 0.8f) / 0.2f : 1f) + MathF.Sin(age * 1.3f) * 4f;
        var top = (int)(H / 2f - thick / 2f);
        p.Rect(batch, new Rectangle(0, top, W, (int)thick), new Color(255, 200, 120) * 0.6f);
        p.Rect(batch, new Rectangle(0, top + (int)(thick * 0.2f), W, (int)(thick * 0.6f)), pal.Hot * 0.9f);
        p.Rect(batch, new Rectangle(0, top + (int)(thick * 0.38f), W, (int)(thick * 0.24f)), Color.White);
        p.Ellipse(batch, Center, (int)(thick * 1.3f), (int)(thick * 1.3f), Color.White * 0.8f);
    }
}
