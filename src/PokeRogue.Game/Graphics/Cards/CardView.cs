using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PokeRogue.Core.Meta.Cards;

namespace PokeRogue.Game.Graphics;

/// <summary>
/// A card held up to be admired (phase 10), as in the TCG apps: it leans towards the mouse (or sways gently by itself),
/// a glare follows the light, and the rarer cards shine with a holo foil that slides as the card turns. Each frame the
/// face and its shine are composed flat into a target (<see cref="Compose"/>, no batch open), then drawn turned in 3D.
/// </summary>
public sealed class CardView(CardArt art)
{
    /// <summary>How far the card leans at most towards the mouse, in radians.</summary>
    public const float MaxTilt = 0.38f;

    private RenderTarget2D? _composite;
    private float _targetYaw;
    private float _targetPitch;
    private float _spin;
    private float _targetSpin;

    public float Yaw { get; private set; }

    public float Pitch { get; private set; }

    /// <summary>Turns the card over (half a turn, animated).</summary>
    public void Flip() => _targetSpin = _targetSpin == 0f ? MathF.PI : 0f;

    public bool ShowsBack => _targetSpin != 0f;

    /// <summary>Shows the back at once, ready to be turned over with <see cref="Flip"/>.</summary>
    public void FaceDown() => _spin = _targetSpin = MathF.PI;

    /// <summary>Back to facing straight on, front up.</summary>
    public void Reset()
    {
        Yaw = Pitch = _targetYaw = _targetPitch = _spin = _targetSpin = 0f;
    }

    /// <summary>Leans towards <paramref name="pointer"/> when it is near the card, otherwise sways a little by itself.</summary>
    public void Update(Vector2? pointer, Vector2 center, Vector2 size, int age)
    {
        if (pointer is { } at && Vector2.Distance(at, center) < size.Y)
        {
            _targetYaw = Math.Clamp((at.X - center.X) / (size.X * 0.5f), -1f, 1f) * MaxTilt;
            _targetPitch = -Math.Clamp((at.Y - center.Y) / (size.Y * 0.5f), -1f, 1f) * MaxTilt;
        }
        else
        {
            _targetYaw = 0.16f * MathF.Sin(age / 55f);
            _targetPitch = 0.08f * MathF.Sin(age / 41f);
        }

        Yaw += (_targetYaw - Yaw) * 0.14f;
        Pitch += (_targetPitch - Pitch) * 0.14f;
        _spin += (_targetSpin - _spin) * 0.16f;
    }

    /// <summary>The yaw drawn: the lean plus the flip.</summary>
    public float TotalYaw => Yaw + _spin;

    /// <summary>Composes the face with its shine into the view's target (no batch open); returns it.</summary>
    public Texture2D Compose(SpriteBatch batch, Texture2D face, CardRarity rarity, int age)
    {
        var s = art.Services;
        var device = s.Device;
        const int scale = CardArt.LargeScale;
        _composite ??= new RenderTarget2D(device, CardArt.Width * scale, CardArt.Height * scale, true, SurfaceFormat.Color, DepthFormat.None, 0, RenderTargetUsage.PreserveContents);

        device.SetRenderTarget(_composite);
        device.Clear(Color.Transparent);
        var transform = Matrix.CreateScale(scale, scale, 1f);
        batch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp, null, null, null);
        batch.Draw(face, new Rectangle(0, 0, _composite.Width, _composite.Height), Color.White);
        batch.End();

        // The holo foil slides with the tilt (stronger the rarer the card); the sparkles drift the other way.
        var lean = new Vector2(Yaw, Pitch) / MaxTilt;
        var region = new Rectangle(0, 0, CardArt.Width, CardArt.Height);
        var (foil, sparkles, tint) = rarity switch
        {
            CardRarity.Holo => (0.2f, 0f, Color.White),
            CardRarity.FullArt => (0.28f, 0.2f, Color.White),
            CardRarity.Shiny => (0.3f, 0.5f, new Color(230, 210, 255)),
            CardRarity.Crown => (0.32f, 0.6f, new Color(255, 222, 150)),
            _ => (0f, 0f, Color.White),
        };

        batch.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearWrap, null, null, null, transform);
        if (foil > 0f)
        {
            var shift = new Point((int)(lean.X * 220 + age * 0.4f), (int)(lean.Y * 160));
            batch.Draw(art.Foil, region, new Rectangle(shift.X, shift.Y, region.Width * 2, region.Height * 2), tint * foil);
        }

        if (sparkles > 0f)
        {
            var shift = new Point((int)(-lean.X * 90), (int)(-lean.Y * 90));
            var twinkle = 0.75f + 0.25f * MathF.Sin(age / 9f);
            batch.Draw(art.Sparkles, region, new Rectangle(shift.X, shift.Y, region.Width * 3, region.Height * 3), tint * (sparkles * twinkle));
        }

        batch.End();

        // The glare, where the light catches the card.
        batch.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp, null, null, null, transform);
        var glare = new Vector2(CardArt.Width / 2f + lean.X * 42, CardArt.Height / 2f - lean.Y * 58);
        batch.Draw(art.Glow, new Rectangle((int)glare.X - 60, (int)glare.Y - 60, 120, 120), Color.White * (rarity == CardRarity.Base ? 0.22f : 0.32f));
        batch.End();

        // Cut to the rounded corners.
        batch.Begin(SpriteSortMode.Deferred, CardArt.Mask, SamplerState.LinearClamp, null, null, null);
        batch.Draw(art.Rounded(scale), Vector2.Zero, Color.White);
        batch.End();

        device.SetRenderTarget(s.Screen.Target);
        return _composite;
    }

    /// <summary>Draws the composed card turned, with its shadow, at <paramref name="center"/> (no batch open).</summary>
    public void Draw(SpriteBatch batch, Texture2D composed, Texture2D back, Vector2 center, Vector2 size, float alpha = 1f)
    {
        var s = art.Services;
        batch.BeginVirtual(samplerState: SamplerState.LinearClamp);
        var lean = new Vector2(Yaw, Pitch) / MaxTilt;
        batch.Draw(art.Glow, new Rectangle((int)(center.X - size.X * 0.75f - lean.X * 6), (int)(center.Y - size.Y * 0.62f + 10 + lean.Y * 6), (int)(size.X * 1.5f), (int)(size.Y * 1.3f)),
            Color.Black * (0.55f * alpha));
        batch.End();
        art.Mesh.Draw(composed, back, center, size, TotalYaw, Pitch, Color.White * alpha);
    }
}
