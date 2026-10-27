using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using PokeRogue.Core.Run;
using PokeRogue.Core.Run.Plates;
using PokeRogue.Game.Graphics;
using PokeRogue.Game.Input;

namespace PokeRogue.Game.Ui;

/// <summary>
/// The chest's offer of a Plate (phase 9.1): after the chest's rewards, up to three cards of the Plates found and not
/// held rise from below; one is taken with the arrows and [INVIO] (or a click), or [ESC] leaves them. The game stays
/// frozen meanwhile.
/// </summary>
public sealed class PlateWindow(GameServices services)
{
    private const int RiseTicks = 14;
    private const int CardWidth = 170;
    private const int CardHeight = 168;
    private const int Gap = 12;

    /// <summary>Scripted players take the first card after this long.</summary>
    private const int AutoTicks = 70;

    private static readonly Color Ink = new(16, 12, 28);
    private static readonly Color Paper = new(244, 240, 228);
    private static readonly Color Dim = new(160, 152, 190);

    private IReadOnlyList<PlateDefinition>? _options;
    private int _selected;
    private int _age;

    public bool IsOpen => _options is not null;

    public void Open(IReadOnlyList<PlateDefinition> options)
    {
        _options = options;
        _selected = 0;
        _age = 0;
        services.Sounds.Play("forziere-apre", 0.7f);
    }

    public void Close() => _options = null;

    /// <summary>Moves the choice; true once a card is taken or the offer is left (the run is told).</summary>
    public bool Update(RunState run, InputState? input)
    {
        if (_options is not { } options)
            return false;

        _age++;
        if (_age < RiseTicks)
            return false;

        int? chosen = null;
        if (input is null)
        {
            if (_age >= AutoTicks)
                chosen = 0;
        }
        else
        {
            if (input.Pressed(Keys.Left) || input.Pressed(Keys.A) || input.Pressed(Buttons.DPadLeft))
                Move(-1, options.Count);
            if (input.Pressed(Keys.Right) || input.Pressed(Keys.D) || input.Pressed(Buttons.DPadRight))
                Move(1, options.Count);
            if (input.HoverMoved("lastra-carta") is { } pointed)
                _selected = pointed;
            if (input.Click("lastra-carta") is { } clicked)
                chosen = clicked;
            else if (input.Pressed(Keys.Enter) || input.Pressed(Keys.Space) || input.Pressed(Buttons.A))
                chosen = _selected;
            else if (input.Pressed(Keys.Escape) || input.Pressed(Buttons.B))
                chosen = -1;
        }

        if (chosen is not { } index)
            return false;

        services.Sounds.Play(index >= 0 ? "menu-conferma" : "menu-indietro");
        run.ChoosePlate(index);
        Close();
        return true;
    }

    private void Move(int delta, int count)
    {
        _selected = (_selected + delta + count) % count;
        services.Sounds.Play("menu-sposta");
    }

    public void Draw(SpriteBatch batch)
    {
        if (_options is not { } options)
            return;

        var p = services.Primitives;
        var width = services.Screen.Width;
        var body = services.Fonts.Body(services.Style);
        var typePower = services.Plates.Value.File.TypePower;

        batch.BeginVirtual(samplerState: SamplerState.PointClamp);
        p.Rect(batch, new Rectangle(0, 0, width, services.Screen.Height), Color.Black * (0.65f * MathHelper.Clamp(_age / 10f, 0f, 1f)));
        services.Input.Blocker(new Rectangle(0, 0, width, services.Screen.Height));
        UiDraw.TextCentered(batch, body, "UNA LASTRA DI ARCEUS!", width / 2f, 20, new Color(248, 208, 72), Ink);
        UiDraw.TextCentered(batch, services.Fonts.Small(services.Style), "SCEGLINE UNA: VALE FINO ALLA FINE DELLA PARTITA", width / 2f, 38, Dim, Ink);

        var left = width / 2 - (options.Count * CardWidth + (options.Count - 1) * Gap) / 2;
        for (var i = 0; i < options.Count; i++)
        {
            var t = MathHelper.Clamp((_age - i * 3) / (float)RiseTicks, 0f, 1f);
            var ease = 1f - (1f - t) * (1f - t);
            var y = (int)MathHelper.Lerp(services.Screen.Height + 10, 84, ease);
            var card = new Rectangle(left + i * (CardWidth + Gap), y - (i == _selected ? 4 : 0), CardWidth, CardHeight);
            services.Input.Region(card, "lastra-carta", i);
            PlateArt.DrawCard(batch, services, card, options[i], typePower, _age + i * 11, highlight: i == _selected);
            if (i == _selected)
                p.Outline(batch, new Rectangle(card.X - 2, card.Y - 2, card.Width + 4, card.Height + 4), _age / 20 % 2 == 0 ? new Color(248, 208, 72) : Paper, 2);
        }

        if (_age >= RiseTicks)
            KeyHints.Draw(batch, services, "[INVIO] PRENDI   [ESC] LASCIA", new Vector2(width / 2f, 276), Dim, centered: true, outline: Ink);
        batch.End();
    }
}
