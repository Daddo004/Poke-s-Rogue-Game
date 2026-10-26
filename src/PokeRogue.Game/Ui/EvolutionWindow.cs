using FontStashSharp;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using PokeRogue.Core.Data;
using PokeRogue.Core.Geometry;
using PokeRogue.Core.Run;
using PokeRogue.Core.Run.Characters;
using PokeRogue.Game.Graphics;
using PokeRogue.Game.Input;

namespace PokeRogue.Game.Ui;

/// <summary>
/// The player's evolution (phase 4.2), as in the games: the game freezes, the Pokémon turns white and its old and new
/// shapes swap faster and faster, a flash, then the new Pokémon in colour with its cry and how its stats changed.
/// Esc stops it (it comes back at the next level). When more than one evolution is ready, the player first chooses.
/// </summary>
public sealed class EvolutionWindow(GameServices services)
{
    private const int FadeTicks = 20;
    private const int WhitenEnd = 50;
    private const int SwapEnd = 230;
    private const int FlashTick = 240;
    private const int FlashEnd = 262;
    private const int StatsTick = 272;
    private const int StoppedWaitTicks = 30;

    /// <summary>Scripted players close the window this long after it can be closed.</summary>
    private const int AutoCloseTicks = 90;

    /// <summary>Scripted players move the choice one step this often, then confirm.</summary>
    private const int ScriptedStepTicks = 24;

    private const float SpriteScale = 3f;
    private const int SpriteFeetY = 212;

    /// <summary>Titles sit under the HUD's timer, which stays visible through the dark.</summary>
    private const int TitleY = 40;

    private static readonly Color Ink = new(16, 12, 28);
    private static readonly Color Paper = new(244, 240, 228);
    private static readonly Color Gold = new(248, 208, 72);
    private static readonly Color Dim = new(160, 152, 190);
    private static readonly Color Up = new(120, 220, 120);
    private static readonly Color Down = new(232, 104, 88);
    private static readonly UiTheme Chosen = UiTheme.Dark with { Fill = new Color(52, 40, 34, 240), Border = Gold, Highlight = new Color(150, 116, 48) };

    private enum State
    {
        Choosing,
        Evolving,
        Stopped,
    }

    private readonly List<Sparkle> _sparkles = [];
    private readonly Random _random = new(42);
    private RunState? _run;
    private IReadOnlyList<CharacterStage> _options = [];
    private SpriteAnimator[] _optionAnimators = [];
    private SpriteAnimator _fromAnimator;
    private SpriteAnimator _toAnimator;
    private State _state;
    private int _selected;
    private CharacterStage? _target;
    private SpeciesData? _from;
    private Stats _before;
    private Stats _after;

    public bool IsOpen => _run is not null;

    /// <summary>Ticks since the window opened, or since the evolution started.</summary>
    public int Age { get; private set; }

    /// <summary>Previews: the option a scripted player walks to and picks.</summary>
    public int ScriptedChoice { get; set; }

    /// <summary>Previews: a scripted player stops the evolution at this age.</summary>
    public int? ScriptedStopTick { get; set; }

    public void Open(RunState run, IReadOnlyList<CharacterStage> options)
    {
        _run = run;
        _options = options;
        _from = run.Player.Species;
        _selected = 0;
        _sparkles.Clear();
        _fromAnimator = default;
        _fromAnimator.Play(Set(_from.Id).Resolve(PmdAnimations.Idle));
        _optionAnimators = options.Select(o =>
        {
            var animator = default(SpriteAnimator);
            animator.Play(Set(o.Species.Id).Resolve(PmdAnimations.Idle));
            return animator;
        }).ToArray();

        if (options.Count == 1)
            Start(options[0]);
        else
            _state = State.Choosing;
        Age = 0;
    }

    public void Close()
    {
        _run = null;
        _target = null;
    }

    public void Update(InputState? input)
    {
        if (_run is not { } run)
            return;

        Age++;
        _fromAnimator.Update();
        _toAnimator.Update();
        for (var i = 0; i < _optionAnimators.Length; i++)
            _optionAnimators[i].Update();
        UpdateSparkles();

        switch (_state)
        {
            case State.Choosing:
                UpdateChoice(run, input);
                break;
            case State.Evolving:
                UpdateEvolution(run, input);
                break;
            case State.Stopped:
                if (Age >= StoppedWaitTicks && Confirmed(input, Age - StoppedWaitTicks))
                {
                    CharacterEvolution.Decline(run);
                    Close();
                }

                break;
        }
    }

    private void UpdateChoice(RunState run, InputState? input)
    {
        if (input is null)
        {
            // Scripted: one step at a time towards the choice, then confirm.
            if (Age % ScriptedStepTicks != 0)
                return;
            if (_selected < Math.Min(ScriptedChoice, _options.Count - 1))
                _selected++;
            else
                Start(_options[_selected]);
            return;
        }

        if (input.Pressed(Keys.Left) || input.Pressed(Keys.A) || input.Pressed(Buttons.DPadLeft))
            _selected = (_selected + _options.Count - 1) % _options.Count;
        if (input.Pressed(Keys.Right) || input.Pressed(Keys.D) || input.Pressed(Buttons.DPadRight))
            _selected = (_selected + 1) % _options.Count;
        if (input.Pressed(Keys.Enter) || input.Pressed(Keys.Space) || input.Pressed(Buttons.A))
        {
            Start(_options[_selected]);
        }
        else if (input.Pressed(Keys.Escape) || input.Pressed(Buttons.B))
        {
            CharacterEvolution.Decline(run);
            Close();
        }
    }

    private void UpdateEvolution(RunState run, InputState? input)
    {
        var stop = input is null
            ? Age == ScriptedStopTick
            : input.Pressed(Keys.Escape) || input.Pressed(Buttons.B);
        if (stop && Age >= FadeTicks && Age < SwapEnd)
        {
            _state = State.Stopped;
            Age = 0;
            return;
        }

        if (Age == FlashTick)
        {
            CharacterEvolution.Evolve(run, _target!);
            _after = Stats.Of(run.Player);
            services.Cries.Play(_target!.Species.Id);
            for (var i = 0; i < 48; i++)
                Spawn(new Vector2(services.Screen.Width / 2f, SpriteFeetY - 30), 60f + _random.NextSingle() * 110f, 40 + _random.Next(20));
        }

        // Light rising around the Pokémon while it changes.
        if (Age is > WhitenEnd and < FlashTick && Age % 3 == 0)
            Spawn(new Vector2(services.Screen.Width / 2f + (_random.NextSingle() - 0.5f) * 90f, SpriteFeetY + 4), 0f, 50, rising: true);

        if (Age >= StatsTick && Confirmed(input, Age - StatsTick))
            Close();
    }

    private static bool Confirmed(InputState? input, int waited) => input is null
        ? waited >= AutoCloseTicks
        : input.Pressed(Keys.Enter) || input.Pressed(Keys.Space) || input.Pressed(Buttons.A);

    private void Start(CharacterStage target)
    {
        _target = target;
        _state = State.Evolving;
        _before = Stats.Of(_run!.Player);
        _toAnimator = default;
        _toAnimator.Play(Set(target.Species.Id).Resolve(PmdAnimations.Idle));
        Age = 0;
    }

    public void Draw(SpriteBatch batch)
    {
        if (_run is null || _from is null)
            return;

        var p = services.Primitives;
        var width = services.Screen.Width;
        batch.Begin(samplerState: SamplerState.PointClamp);
        // The screen darkens as the window opens; after a choice it is dark already.
        var fade = _state == State.Choosing ? MathHelper.Clamp(Age / 10f, 0f, 1f)
            : _options.Count > 1 || _state == State.Stopped ? 1f
            : MathHelper.Clamp(Age / (float)FadeTicks, 0f, 1f);
        p.Rect(batch, new Rectangle(0, 0, width, services.Screen.Height), Color.Black * (0.72f * fade));

        if (_state == State.Choosing)
            DrawChoice(batch);
        else
            DrawEvolution(batch);

        batch.End();
    }

    private void DrawChoice(SpriteBatch batch)
    {
        var p = services.Primitives;
        var centerX = services.Screen.Width / 2;
        UiDraw.TextCentered(batch, Title, "EVOLUZIONE", centerX, TitleY, Gold, Ink);
        UiDraw.TextCentered(batch, Body, $"{_from!.NameIt} può evolversi: scegli in cosa.", centerX, TitleY + 36, Paper, Ink);

        var count = _options.Count;
        var tileWidth = count <= 3 ? 120 : 80;
        const int gap = 6, tileHeight = 128, top = 104;
        var left = centerX - (count * tileWidth + (count - 1) * gap) / 2;
        for (var i = 0; i < count; i++)
        {
            var option = _options[i];
            var tile = new Rectangle(left + i * (tileWidth + gap), top, tileWidth, tileHeight);
            var selected = i == _selected;
            UiDraw.Frame(batch, p, tile, selected ? Chosen : UiTheme.Dark);

            var feet = new Vector2(tile.Center.X, tile.Y + 74 - (selected ? MathF.Abs(MathF.Sin(Age * 0.15f)) * 3f : 0f));
            p.Ellipse(batch, new Vector2(tile.Center.X, tile.Y + 75), 30, 7, Color.Black * 0.35f);
            var set = Set(option.Species.Id);
            set.Draw(batch, _optionAnimators[i].Anim!, _optionAnimators[i].Frame, Direction8.Down, feet, selected ? Color.White : new Color(170, 170, 185), 2f);

            UiDraw.TextCentered(batch, Body, option.Species.NameIt, tile.Center.X, tile.Y + 82, selected ? Gold : Paper);
            DrawTypes(batch, option.Species, tile.Center.X, tile.Y + 102);
            if (option.ItemNameIt is { } item)
                UiDraw.TextCentered(batch, Small, item.ToUpperInvariant(), tile.Center.X, tile.Y + 116, Dim);
        }

        var preview = Stats.Of(_run!.Player);
        DrawStats(batch, preview, preview.Evolved(_from, _options[_selected].Species), 240);
        UiDraw.TextCentered(batch, Small, "[FRECCE] SCEGLI     [INVIO] EVOLVI     [ESC] NON ORA", centerX, 330, Paper, Ink);
    }

    private void DrawEvolution(SpriteBatch batch)
    {
        var p = services.Primitives;
        var centerX = services.Screen.Width / 2;
        var feet = new Vector2(centerX, SpriteFeetY);
        var evolved = _state == State.Evolving && Age >= FlashTick;
        var stopped = _state == State.Stopped;

        // Soft light behind the Pokémon: rays turning slowly, brighter as the change speeds up.
        if (!stopped && Age >= FadeTicks)
        {
            var strength = evolved ? MathHelper.Clamp(1f - (Age - FlashTick) / 60f, 0.25f, 1f) : MathHelper.Clamp((Age - FadeTicks) / (float)(SwapEnd - FadeTicks), 0f, 1f);
            var glow = feet - new Vector2(0, 30);
            for (var k = 0; k < 14; k++)
            {
                var angle = k * MathF.Tau / 14f + Age * 0.008f;
                var direction = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
                var length = 70f + (k % 2) * 30f;
                UiDraw.Line(batch, p, glow + direction * 20f, glow + direction * (20f + length), (evolved ? Gold : Color.White) * (0.08f + 0.14f * strength), 3f);
            }

            p.Ellipse(batch, glow, 90, 90, (evolved ? Gold : Color.White) * (0.06f + 0.1f * strength));
        }

        p.Ellipse(batch, feet + new Vector2(0, 2), 64, 12, Color.Black * 0.4f);
        DrawPokemon(batch, feet, evolved, stopped);
        DrawSparkles(batch);

        // Texts: what is happening at the top, the stats once it is over, the key to press at the bottom.
        var name = _from!.NameIt;
        if (stopped)
        {
            UiDraw.TextCentered(batch, Title, "EH?", centerX, TitleY, Gold, Ink);
            UiDraw.TextCentered(batch, Body, $"{name} ha smesso di evolversi!", centerX, TitleY + 36, Paper, Ink);
        }
        else if (evolved)
        {
            UiDraw.TextCentered(batch, Title, "CONGRATULAZIONI!", centerX, TitleY, Gold, Ink);
            UiDraw.TextCentered(batch, Body, $"{name} si è evoluto in {_target!.Species.NameIt}!", centerX, TitleY + 36, Paper, Ink);
        }
        else
        {
            UiDraw.TextCentered(batch, Title, "COSA?", centerX, TitleY, Gold, Ink);
            UiDraw.TextCentered(batch, Body, $"{name} si sta evolvendo!", centerX, TitleY + 36, Paper, Ink);
        }

        if (evolved && Age >= StatsTick - 10)
            DrawStats(batch, _before, _after, 242);

        var canClose = stopped ? Age >= StoppedWaitTicks : evolved && Age >= StatsTick;
        if (canClose && Age / 15 % 2 == 0)
            UiDraw.TextCentered(batch, Small, "[INVIO] CONTINUA", centerX, 330, Paper, Ink);
        else if (!stopped && !evolved && Age >= FadeTicks && Age < SwapEnd)
            UiDraw.TextCentered(batch, Small, "[ESC] FERMA L'EVOLUZIONE", centerX, 330, Dim, Ink);

        // The flash: up to full white on the change, then fading out on the new Pokémon.
        if (!stopped && Age >= SwapEnd && Age < FlashEnd)
        {
            var white = Age < FlashTick ? (Age - SwapEnd) / (float)(FlashTick - SwapEnd) : 1f - (Age - FlashTick) / (float)(FlashEnd - FlashTick);
            p.Rect(batch, new Rectangle(0, 0, services.Screen.Width, services.Screen.Height), Color.White * MathHelper.Clamp(white, 0f, 1f));
        }
    }

    /// <summary>The colour fading to white, then old and new shapes swapping faster and faster, each one breathing in as it shows.</summary>
    private void DrawPokemon(SpriteBatch batch, Vector2 feet, bool evolved, bool stopped)
    {
        var from = Set(_from!.Id);
        if (evolved)
        {
            var to = Set(_target!.Species.Id);
            to.Draw(batch, _toAnimator.Anim!, _toAnimator.Frame, Direction8.Down, feet, Color.White, SpriteScale);
            return;
        }

        if (stopped || Age < FadeTicks)
        {
            from.Draw(batch, _fromAnimator.Anim!, _fromAnimator.Frame, Direction8.Down, feet, Color.White, SpriteScale);
            return;
        }

        if (Age < WhitenEnd)
        {
            var white = (Age - FadeTicks) / (float)(WhitenEnd - FadeTicks);
            from.Draw(batch, _fromAnimator.Anim!, _fromAnimator.Frame, Direction8.Down, feet, Color.White, SpriteScale);
            from.Draw(batch, _fromAnimator.Anim!, _fromAnimator.Frame, Direction8.Down, feet, Color.White * white, SpriteScale, silhouette: true);
            return;
        }

        // Swaps so far: slow at first (about one every half second), very fast at the end.
        var t = MathHelper.Clamp((Age - WhitenEnd) / (float)(SwapEnd - WhitenEnd), 0f, 1f);
        var swaps = 5f * t + 17f * t * t * t;
        var showNew = (int)swaps % 2 == 1 || Age >= SwapEnd;
        var breath = 1f - 0.12f * MathF.Sin((swaps - MathF.Floor(swaps)) * MathF.PI);
        var scale = SpriteScale * (Age >= SwapEnd ? 1f : breath);
        if (showNew)
        {
            var to = Set(_target!.Species.Id);
            to.Draw(batch, _toAnimator.Anim!, _toAnimator.Frame, Direction8.Down, feet, Color.White, scale, silhouette: true);
        }
        else
        {
            from.Draw(batch, _fromAnimator.Anim!, _fromAnimator.Frame, Direction8.Down, feet, Color.White, scale, silhouette: true);
        }
    }

    /// <summary>Six stats, before and after: PS and speed as the run uses them, the rest the species' base stats.</summary>
    private void DrawStats(SpriteBatch batch, Stats before, Stats after, int top)
    {
        var p = services.Primitives;
        var panel = new Rectangle(services.Screen.Width / 2 - 222, top, 444, 82);
        UiDraw.Frame(batch, p, panel, UiTheme.Dark);

        (string Label, int From, int To)[] rows =
        [
            ("PS", before.Hp, after.Hp), ("ATTACCO", before.Attack, after.Attack), ("ATT. SP.", before.SpAttack, after.SpAttack),
            ("VELOCITÀ", before.Speed, after.Speed), ("DIFESA", before.Defense, after.Defense), ("DIF. SP.", before.SpDefense, after.SpDefense),
        ];
        var numbers = services.Fonts.Numbers(services.Style);
        for (var i = 0; i < rows.Length; i++)
        {
            var (label, from, to) = rows[i];
            var x = panel.X + 14 + i % 3 * 146;
            var y = panel.Y + 8 + i / 3 * 36;
            UiDraw.Text(batch, Small, label, new Vector2(x, y), Dim);

            var color = to > from ? Up : to < from ? Down : Paper;
            var fromText = from.ToString(System.Globalization.CultureInfo.InvariantCulture);
            UiDraw.Text(batch, numbers, fromText, new Vector2(x, y + 9), Dim);
            var arrowX = x + (int)UiDraw.Measure(numbers, fromText).X + 6;
            DrawArrow(batch, arrowX, y + 19, color);
            UiDraw.Text(batch, numbers, to.ToString(System.Globalization.CultureInfo.InvariantCulture), new Vector2(arrowX + 12, y + 9), color);
        }
    }

    /// <summary>A small pixel arrow pointing right (the number font has no arrow).</summary>
    private void DrawArrow(SpriteBatch batch, int x, int y, Color color)
    {
        var p = services.Primitives;
        p.Rect(batch, new Rectangle(x, y - 1, 5, 2), color);
        p.Rect(batch, new Rectangle(x + 5, y - 3, 1, 6), color);
        p.Rect(batch, new Rectangle(x + 6, y - 2, 1, 4), color);
        p.Rect(batch, new Rectangle(x + 7, y - 1, 1, 2), color);
    }

    private void DrawTypes(SpriteBatch batch, SpeciesData species, int centerX, int y)
    {
        const int pillWidth = 36, gap = 2;
        var types = species.Types;
        var left = centerX - (types.Count * pillWidth + (types.Count - 1) * gap) / 2;
        for (var i = 0; i < types.Count; i++)
            UiDraw.Pill(batch, services.Primitives, Small, new Rectangle(left + i * (pillWidth + gap), y, pillWidth, 10), TypePalette.Of(types[i]), TypePalette.NameIt(types[i]).ToUpperInvariant());
    }

    private void Spawn(Vector2 at, float speed, int life, bool rising = false)
    {
        var angle = _random.NextSingle() * MathF.Tau;
        var velocity = rising
            ? new Vector2((_random.NextSingle() - 0.5f) * 10f, -40f - _random.NextSingle() * 50f)
            : new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * speed;
        var color = _random.Next(3) == 0 ? Gold : Color.White;
        _sparkles.Add(new Sparkle(at, velocity, 0, life, color));
    }

    private void UpdateSparkles()
    {
        for (var i = _sparkles.Count - 1; i >= 0; i--)
        {
            var s = _sparkles[i];
            s = s with { Position = s.Position + s.Velocity / 60f, Velocity = s.Velocity * 0.97f, Age = s.Age + 1 };
            if (s.Age >= s.Life)
                _sparkles.RemoveAt(i);
            else
                _sparkles[i] = s;
        }
    }

    private void DrawSparkles(SpriteBatch batch)
    {
        foreach (var s in _sparkles)
        {
            var alpha = 1f - s.Age / (float)s.Life;
            var size = s.Age < s.Life / 2 ? 2 : 1;
            services.Primitives.Rect(batch, new Rectangle((int)s.Position.X, (int)s.Position.Y, size, size), s.Color * alpha);
        }
    }

    private PokemonSpriteSet Set(int speciesId) => services.Sprites.Get(speciesId)!;

    private SpriteFontBase Title => services.Fonts.Title(services.Style);

    private SpriteFontBase Body => services.Fonts.Body(services.Style);

    private SpriteFontBase Small => services.Fonts.Small(services.Style);

    private readonly record struct Sparkle(Vector2 Position, Vector2 Velocity, int Age, int Life, Color Color);

    /// <summary>What the stats panel compares.</summary>
    private readonly record struct Stats(int Hp, int Speed, int Attack, int Defense, int SpAttack, int SpDefense)
    {
        public static Stats Of(Player player) => new(
            (int)MathF.Round(player.MaxHp), (int)MathF.Round(player.MoveSpeed),
            player.Species.BaseStats.Attack, player.Species.BaseStats.Defense, player.Species.BaseStats.SpAttack, player.Species.BaseStats.SpDefense);

        /// <summary>The same run stats once <paramref name="from"/> becomes <paramref name="to"/>: items and modifiers scale them alike.</summary>
        public Stats Evolved(SpeciesData from, SpeciesData to) => new(
            (int)MathF.Round(Hp * StatFormulas.PlayerMaxHp(to.BaseStats) / StatFormulas.PlayerMaxHp(from.BaseStats)),
            (int)MathF.Round(Speed * StatFormulas.PlayerMoveSpeed(to.BaseStats) / StatFormulas.PlayerMoveSpeed(from.BaseStats)),
            to.BaseStats.Attack, to.BaseStats.Defense, to.BaseStats.SpAttack, to.BaseStats.SpDefense);
    }
}
