using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PokeRogue.Core.Data;
using PokeRogue.Core.Geometry;
using PokeRogue.Core.Run;
using PokeRogue.Core.Run.Abilities;
using PokeRogue.Game.Graphics;

namespace PokeRogue.Game.Vfx;

/// <summary>
/// What the player's ability shows in the field (phase 4.3): its name popping up over the head when it does something
/// (as the ability banner of the games), a fading afterimage and sparks for Teletrasporto, and a white flash when Ditto
/// changes shape. Everything is alpha-blended and stays clear of the face.
/// </summary>
public sealed class AbilityVfx(GameServices services)
{
    private const int PopupTicks = 54;
    private const int AfterimageTicks = 24;
    private const int FlashTicks = 14;

    private static readonly Color Ink = new(16, 12, 28);
    private static readonly Color Guard = new(150, 210, 255);
    private static readonly Color Gold = new(248, 208, 72);
    private static readonly Color Heal = new(120, 220, 120);
    private static readonly Color Psychic = new(248, 120, 176);
    private static readonly Color Lavender = new(200, 170, 255);

    private readonly Particles _particles = new(256);
    private readonly Random _random = new(9);
    private readonly List<Afterimage> _afterimages = [];
    private Popup? _popup;

    /// <summary>Ticks left of the white flash over the player (Trasformazione).</summary>
    public int Flash { get; private set; }

    public void Reset()
    {
        _afterimages.Clear();
        _popup = null;
        Flash = 0;
    }

    /// <param name="anim">The player's animation right now, for Teletrasporto's afterimage.</param>
    public void OnEvent(in RunEvent e, RunState run, PmdAnim? anim, int frame)
    {
        if (e.Type != RunEventType.AbilityTriggered || run.Stage?.Ability is not { } ability)
            return;

        var effect = (AbilityEffect)(int)e.Value;
        var feet = run.Player.Position.ToXna();
        switch (effect)
        {
            case AbilityEffect.Teleported:
                if (anim is not null)
                    _afterimages.Add(new Afterimage(e.Position.ToXna(), run.Player.Form.Id, anim, frame, run.Player.Facing, 0));
                Sparks(e.Position.ToXna(), Psychic, 14);
                Sparks(e.Target.ToXna(), Psychic, 18);
                break;
            case AbilityEffect.Transformed or AbilityEffect.TransformEnded:
                Flash = FlashTicks;
                Sparks(feet, Lavender, 24);
                break;
        }

        var color = effect switch
        {
            AbilityEffect.Blocked or AbilityEffect.Dodged or AbilityEffect.Endured or AbilityEffect.Immune => Guard,
            AbilityEffect.Healed => Heal,
            AbilityEffect.Teleported => Psychic,
            AbilityEffect.Transformed => Lavender,
            AbilityEffect.Boosted or AbilityEffect.Awakened => Gold,
            _ => Color.White,
        };

        // The recharge of Muta and the end of a disguise show in the HUD and in the flash: no name for them.
        if (effect is not (AbilityEffect.Ready or AbilityEffect.TransformEnded))
            _popup = new Popup(ability.NameIt, color, 0);
    }

    /// <summary>Shows <paramref name="text"/> over the head as an ability's name (the trainer's abilities, phase 5.2).</summary>
    public void Announce(string text, Color color) => _popup = new Popup(text, color, 0);

    public void Update()
    {
        _particles.Update();
        if (Flash > 0)
            Flash--;
        for (var i = _afterimages.Count - 1; i >= 0; i--)
        {
            _afterimages[i] = _afterimages[i] with { Age = _afterimages[i].Age + 1 };
            if (_afterimages[i].Age >= AfterimageTicks)
                _afterimages.RemoveAt(i);
        }

        if (_popup is { } popup)
            _popup = popup.Age + 1 >= PopupTicks ? null : popup with { Age = popup.Age + 1 };
    }

    /// <summary>Afterimages in pink light, fading where the player left from. World space, alpha batch.</summary>
    public void DrawObjects(SpriteBatch batch)
    {
        foreach (var image in _afterimages)
        {
            if (services.Sprites.Get(image.SpeciesId) is not { } set)
                continue;

            var alpha = 0.6f * (1f - image.Age / (float)AfterimageTicks);
            set.Draw(batch, image.Anim, image.Frame, image.Facing, image.Feet, Psychic * alpha, silhouette: true);
        }
    }

    /// <summary>The ability's name over the head, rising a little and fading, plus the sparks. World space, alpha batch.</summary>
    public void DrawOverhead(SpriteBatch batch, RunState run)
    {
        _particles.Draw(batch, services.Primitives);
        if (_popup is not { } popup)
            return;

        var head = services.Sprites.Get(run.Player.Form.Id)?.HeadHeight ?? 20;
        var rise = MathF.Min(popup.Age, 12) * 0.8f;
        var alpha = popup.Age > PopupTicks - 15 ? (PopupTicks - popup.Age) / 15f : 1f;
        var pop = popup.Age < 4 ? 1f - popup.Age / 8f : 0f;
        var font = services.Fonts.Small(services.Style);
        var text = popup.Text.ToUpperInvariant();
        var feet = run.Player.Position.ToXna();
        var y = MathF.Round(feet.Y - head - 26 - rise - pop * 3f);

        // A small dark label behind the name, with a rim of the effect's colour, so it reads on grass and in a crowd.
        var size = UiDraw.Measure(font, text);
        var label = new Rectangle((int)MathF.Round(feet.X - size.X / 2f) - 4, (int)y - 2, (int)size.X + 8, 11);
        var p = services.Primitives;
        p.Rect(batch, new Rectangle(label.X + 1, label.Y, label.Width - 2, label.Height), popup.Color * (0.8f * alpha));
        p.Rect(batch, new Rectangle(label.X, label.Y + 1, label.Width, label.Height - 2), popup.Color * (0.8f * alpha));
        p.Rect(batch, new Rectangle(label.X + 1, label.Y + 1, label.Width - 2, label.Height - 2), Ink * (0.85f * alpha));
        UiDraw.TextCentered(batch, font, text, feet.X, y, popup.Color * alpha);
    }

    private void Sparks(Vector2 feet, Color color, int count) =>
        _particles.Burst(feet - new Vector2(0, 8), count, 110f, Color.White, color, 26, _random, size: 2f);

    private readonly record struct Afterimage(Vector2 Feet, int SpeciesId, PmdAnim Anim, int Frame, Direction8 Facing, int Age);

    private readonly record struct Popup(string Text, Color Color, int Age);
}
