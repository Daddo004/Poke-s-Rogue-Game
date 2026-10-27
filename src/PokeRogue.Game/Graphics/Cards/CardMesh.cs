using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace PokeRogue.Game.Graphics;

/// <summary>
/// Draws a card (or a pack) as a plane turned in 3D (phase 10): <c>yaw</c> turns it round its upright axis (positive
/// pushes the right edge away), <c>pitch</c> round its horizontal one (positive pushes the top away). The corners are
/// projected on the CPU with a little perspective over a grid fine enough that the texture does not visibly bend.
/// A plane seen from behind shows <c>back</c>, mirrored so it reads right. Drawn with no batch open, onto the screen's target.
/// </summary>
public sealed class CardMesh(GraphicsDevice device)
{
    private const int Columns = 10;
    private const int Rows = 14;

    private readonly VertexPositionColorTexture[] _vertices = new VertexPositionColorTexture[(Columns + 1) * (Rows + 1)];
    private readonly short[] _indices = Indices();
    private BasicEffect? _effect;

    /// <summary>Whether a plane turned this way shows its front.</summary>
    public static bool FacesFront(float yaw, float pitch) => MathF.Cos(yaw) * MathF.Cos(pitch) >= 0f;

    /// <param name="center">Centre on the virtual screen.</param>
    /// <param name="size">Width and height on the virtual screen, before the perspective.</param>
    public void Draw(Texture2D front, Texture2D? back, Vector2 center, Vector2 size, float yaw, float pitch, Color color, BlendState? blend = null)
    {
        var showsFront = FacesFront(yaw, pitch);
        var texture = showsFront ? front : back;
        if (texture is null)
            return;

        var zoom = VirtualScreen.CurrentZoom;
        var distance = size.Y * 2.6f;
        var (sinYaw, cosYaw) = MathF.SinCos(yaw);
        var (sinPitch, cosPitch) = MathF.SinCos(pitch);

        // A light from the front: the plane darkens a little as it turns away.
        var light = 0.82f + 0.18f * MathF.Abs(cosYaw * cosPitch);
        var shaded = new Color(color.ToVector3() * light) { A = color.A };

        for (var row = 0; row <= Rows; row++)
        {
            for (var column = 0; column <= Columns; column++)
            {
                var u = column / (float)Columns;
                var v = row / (float)Rows;
                var x = (u - 0.5f) * size.X;
                var y = (v - 0.5f) * size.Y;

                // Pitch round the horizontal axis, then yaw round the upright one.
                var y1 = y * cosPitch;
                var z1 = y * sinPitch;
                var x2 = x * cosYaw + z1 * sinYaw;
                var z2 = -x * sinYaw + z1 * cosYaw;
                var perspective = distance / (distance - z2);

                var screen = (center + new Vector2(x2, y1) * perspective) * zoom;
                _vertices[row * (Columns + 1) + column] = new VertexPositionColorTexture(
                    new Vector3(screen, 0f), shaded, new Vector2(showsFront ? u : 1f - u, v));
            }
        }

        var effect = _effect ??= new BasicEffect(device) { TextureEnabled = true, VertexColorEnabled = true, LightingEnabled = false };
        var viewport = device.Viewport;
        effect.World = Matrix.Identity;
        effect.View = Matrix.Identity;
        effect.Projection = Matrix.CreateOrthographicOffCenter(0, viewport.Width, viewport.Height, 0, 0, 1);
        effect.Texture = texture;

        device.BlendState = blend ?? BlendState.AlphaBlend;
        device.DepthStencilState = DepthStencilState.None;
        device.RasterizerState = RasterizerState.CullNone;
        device.SamplerStates[0] = SamplerState.LinearClamp;
        foreach (var pass in effect.CurrentTechnique.Passes)
        {
            pass.Apply();
            device.DrawUserIndexedPrimitives(PrimitiveType.TriangleList, _vertices, 0, _vertices.Length, _indices, 0, _indices.Length / 3);
        }
    }

    private static short[] Indices()
    {
        var indices = new List<short>();
        for (var row = 0; row < Rows; row++)
        {
            for (var column = 0; column < Columns; column++)
            {
                var a = (short)(row * (Columns + 1) + column);
                var b = (short)(a + 1);
                var c = (short)(a + Columns + 1);
                var d = (short)(c + 1);
                indices.AddRange([a, b, c, b, d, c]);
            }
        }

        return indices.ToArray();
    }
}
