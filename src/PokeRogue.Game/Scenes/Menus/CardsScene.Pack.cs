using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PokeRogue.Core.Meta.Cards;
using PokeRogue.Game.Graphics;
using PokeRogue.Game.Ui;

namespace PokeRogue.Game.Scenes.Menus;

/// <summary>
/// Opening a pack: the sealed pack leans towards the mouse; [INVIO] tears it along the top, the cards rise out of it, then
/// each press slides the front card away to show the next (a rare one first shows its back, glowing, then turns over);
/// at the end all the cards of the pack are laid out side by side.
/// </summary>
public sealed partial class CardsScene
{
    private enum PackStep
    {
        Sealed,
        Tearing,
        Rising,
        Reveal,
        Summary,
    }

    private const int TearTicks = 34;
    private const int RiseTicks = 40;
    private const int SlideTicks = 16;
    private const int TeaseTicks = 46;

    private static readonly Vector2 PackCenter = new(320, 186);
    private static readonly Vector2 PackSize = new(150, 252);
    private static readonly Vector2 RevealSize = new(200, 276);

    private sealed class PackState
    {
        public required Core.Run.Characters.CharacterDefinition Line { get; init; }

        public required IReadOnlyList<CardDefinition> Cards { get; init; }

        public RenderTarget2D? Art { get; set; }

        public PackStep Step { get; set; }

        public int Ticks { get; set; }

        public int Index { get; set; }

        /// <summary>Ticks left of the front card sliding away.</summary>
        public int Sliding { get; set; }

        /// <summary>The card shown has been turned face up (rare cards start face down).</summary>
        public bool FaceUp { get; set; }
    }

    private PackState? _pack;

    /// <summary>The random draws of the packs; previews set a seed.</summary>
    public Random PackRandom { get; set; } = new();

    private void StartPack()
    {
        var line = Line;
        if (Meta.CardLine(line).Packs <= 0)
        {
            Notice("NESSUNA BUSTINA DI QUESTA LINEA: GIOCA PER FAR CRESCERE IL LEGAME", Red);
            return;
        }

        if (Meta.PackPool(line).Count == 0)
        {
            Notice("NESSUNA CARTA NUOVA DA TROVARE ORA: EVOLVI O VINCI CON QUESTA LINEA", Red);
            return;
        }

        // The cards are drawn (and saved) at once, so closing the game mid-animation loses nothing.
        _pack = new PackState { Line = line, Cards = Meta.OpenPack(line, PackRandom) };
        _view ??= new CardView(Services.CardArt);
        _view.Reset();
        Services.Sounds.Play("menu-conferma");
    }

    /// <summary>Previews: jumps the opening to a step.</summary>
    public void ShowPack(int step, int ticks, int index = 0)
    {
        if (_pack is null)
            return;
        if ((PackStep)step == PackStep.Reveal)
            Present(_pack, index);
        _pack.Step = (PackStep)step;
        _pack.Ticks = ticks;
    }

    /// <summary>Previews: opens the chosen line's pack.</summary>
    public void OpenPack() => StartPack();

    private bool _advance;

    /// <summary>Previews: presses [INVIO] in the opening on the next update.</summary>
    public void AdvancePack() => _advance = true;

    private void UpdatePack()
    {
        var pack = _pack!;
        pack.Ticks++;
        _viewAge++;
        var input = Services.Input;
        _view!.Update(input.PointerAwake ? input.Pointer : null, PackCenter, pack.Step <= PackStep.Tearing ? PackSize : RevealSize, _viewAge);
        var next = Confirm() || ClickedOn("bustina-avanti") is not null || _advance;
        _advance = false;

        switch (pack.Step)
        {
            case PackStep.Sealed when next:
                pack.Step = PackStep.Tearing;
                pack.Ticks = 0;
                Services.Sounds.Play("bustina-strappo");
                break;
            case PackStep.Tearing when pack.Ticks >= TearTicks:
                pack.Step = PackStep.Rising;
                pack.Ticks = 0;
                break;
            case PackStep.Rising when pack.Ticks >= RiseTicks:
                Present(pack, 0);
                break;
            case PackStep.Reveal:
                if (pack.Sliding > 0 && --pack.Sliding == 0)
                {
                    if (pack.Index + 1 < pack.Cards.Count)
                        Present(pack, pack.Index + 1);
                    else
                    {
                        pack.Step = PackStep.Summary;
                        pack.Ticks = 0;
                    }
                }
                else if (!pack.FaceUp && pack.Ticks >= TeaseTicks)
                {
                    pack.FaceUp = true;
                    _view.Flip();
                    Services.Sounds.Play("carta-rara");
                }
                else if (next && pack.Sliding == 0 && pack.FaceUp && pack.Ticks > 12)
                {
                    pack.Sliding = SlideTicks;
                    Services.Sounds.Play("carta-via");
                }

                break;
            case PackStep.Summary when next || Back():
                Close(pack);
                break;
        }

        if (pack.Step < PackStep.Summary && Back())
        {
            // Skipping keeps the cards: they were saved when the pack was opened.
            pack.Step = PackStep.Summary;
            pack.Ticks = 0;
        }
    }

    /// <summary>Shows card <paramref name="index"/>: common ones face up, rare ones face down for a moment.</summary>
    private void Present(PackState pack, int index)
    {
        pack.Step = PackStep.Reveal;
        pack.Index = index;
        pack.Ticks = 0;
        pack.Sliding = 0;
        _view!.Reset();
        var card = pack.Cards[index];
        pack.FaceUp = card.Rarity < CardRarity.FullArt;
        if (!pack.FaceUp)
            _view.FaceDown();
        Meta.MarkSeen(card);
    }

    private void Close(PackState pack)
    {
        pack.Art?.Dispose();
        _pack = null;
        _inGrid = true;
        _cursor = Math.Max(0, Set.ToList().IndexOf(pack.Cards[^1]));
        _gridScroll = Math.Max(0, _cursor / GridColumns - GridRows + 1);
        foreach (var card in pack.Cards)
            Meta.MarkSeen(card);
    }

    private void BeforeDrawPack(SpriteBatch batch)
    {
        var pack = _pack!;
        var art = Services.CardArt;
        pack.Art ??= art.RenderPack(batch, pack.Line, 4);
        art.Back(batch);

        // The pack's faces load (or download) while it is still sealed.
        art.LoadFaces(pack.Cards, budget: 1);
        _composed = null;
        if (pack.Step is PackStep.Rising or PackStep.Reveal)
        {
            var card = pack.Cards[pack.Step == PackStep.Rising ? 0 : pack.Index];
            if (art.Face(card) is { } face)
                _composed = _view!.Compose(batch, face, card.Rarity, _viewAge);
        }
    }

    private void DrawPack(SpriteBatch batch)
    {
        var pack = _pack!;
        var art = Services.CardArt;
        var p = Services.Primitives;
        var type = TypePalette.Of(pack.Line.First.Types[0]);
        p.Rect(batch, new Rectangle(0, 0, Services.Screen.Width, Services.Screen.Height), Color.Black * 0.35f);
        batch.End();

        batch.BeginVirtual(samplerState: SamplerState.LinearClamp);
        batch.Draw(art.Glow, new Rectangle((int)PackCenter.X - 260, (int)PackCenter.Y - 230, 520, 460), Color.Lerp(type, Color.White, 0.3f) * 0.4f);
        batch.End();

        switch (pack.Step)
        {
            case PackStep.Sealed or PackStep.Tearing:
                DrawSealed(batch, pack);
                break;
            case PackStep.Rising:
                DrawRising(batch, pack);
                break;
            case PackStep.Reveal:
                DrawReveal(batch, pack);
                break;
            default:
                DrawSummary(batch, pack);
                break;
        }

        batch.BeginVirtual(samplerState: SamplerState.PointClamp);
        Services.Input.Region(new Rectangle(0, 30, Services.Screen.Width, Services.Screen.Height - 60), "bustina-avanti");
        UiDraw.TextOutlined(batch, Body, $"BUSTINA DI {pack.Line.NameIt.ToUpperInvariant()}", new Vector2(16, 10), Paper, Ink);
        var hint = pack.Step switch
        {
            PackStep.Sealed => "[INVIO] APRI LA BUSTINA     [ESC] SALTA",
            PackStep.Summary => "[INVIO] ALL'ALBUM",
            PackStep.Reveal => pack.Index + 1 < pack.Cards.Count ? "[INVIO] PROSSIMA CARTA     [ESC] SALTA" : "[INVIO] RIEPILOGO     [ESC] SALTA",
            _ => "[ESC] SALTA",
        };
        DrawFooter(batch, hint);
    }

    private void DrawSealed(SpriteBatch batch, PackState pack)
    {
        if (pack.Art is null)
            return;

        var art = Services.CardArt;
        var bob = MathF.Sin(_viewAge / 30f) * 3f;
        var center = PackCenter + new Vector2(0, bob);
        art.Mesh.Draw(pack.Art, pack.Art, center, PackSize, _view!.Yaw, _view.Pitch, Color.White);

        // Tearing: a bright cut runs along the top, sparks fly off its tip, then the top strip flies away.
        if (pack.Step != PackStep.Tearing)
            return;

        var t = Math.Min(1f, pack.Ticks / (float)(TearTicks - 8));
        var y = center.Y - PackSize.Y / 2f + 14;
        var left = center.X - PackSize.X / 2f;
        batch.BeginVirtual(BlendState.Additive, SamplerState.LinearClamp);
        var p = Services.Primitives;
        p.Rect(batch, new Vector2(left, y - 1), new Vector2(PackSize.X * t, 2), Color.White);
        batch.Draw(art.Glow, new Rectangle((int)(left + PackSize.X * t) - 14, (int)y - 14, 28, 28), Color.White);
        var random = new Random(pack.Ticks);
        for (var i = 0; i < 6; i++)
        {
            var spark = new Vector2(left + PackSize.X * t - random.Next(30), y + random.Next(-10, 10) - random.Next(12));
            p.Rect(batch, spark, new Vector2(2, 2), new Color(255, 230, 160));
        }

        if (pack.Ticks > TearTicks - 8)
            p.Rect(batch, new Vector2(0, 0), new Vector2(Services.Screen.Width, Services.Screen.Height), Color.White * ((pack.Ticks - (TearTicks - 8)) / 8f * 0.6f));
        batch.End();
    }

    private void DrawRising(SpriteBatch batch, PackState pack)
    {
        var art = Services.CardArt;
        var t = pack.Ticks / (float)RiseTicks;
        var ease = 1f - (1f - t) * (1f - t);

        // The opened pack drops away while the stack rises out of it and grows to full size.
        if (pack.Art is not null)
            art.Mesh.Draw(pack.Art, pack.Art, PackCenter + new Vector2(0, 40 + 220 * ease), PackSize, 0f, 0.25f * ease, Color.White * (1f - t));

        var size = Vector2.Lerp(PackSize * new Vector2(0.92f, 0.65f), RevealSize, ease);
        var center = PackCenter + new Vector2(0, 30 * (1f - ease));
        var back = art.Back(batch);
        for (var i = pack.Cards.Count - 1; i >= 1; i--)
            art.Mesh.Draw(back, back, center + new Vector2(i * 3, i * 3), size, 0f, 0f, Color.White * 0.9f);
        if (_composed is not null)
        {
            if (pack.Cards[0].Rarity >= CardRarity.FullArt)
                art.Mesh.Draw(back, back, center, size, 0f, 0f, Color.White);
            else
                art.Mesh.Draw(_composed, back, center, size, 0f, 0f, Color.White);
        }
    }

    private void DrawReveal(SpriteBatch batch, PackState pack)
    {
        var art = Services.CardArt;
        var card = pack.Cards[pack.Index];
        var back = art.Back(batch);

        // The cards still under it.
        for (var i = pack.Cards.Count - 1; i > pack.Index; i--)
            art.Mesh.Draw(back, back, PackCenter + new Vector2((i - pack.Index) * 3, (i - pack.Index) * 3), RevealSize, 0f, 0f, Color.White * 0.9f);

        // A rare card waiting face down glows ever brighter, then flashes as it turns.
        if (card.Rarity >= CardRarity.FullArt)
        {
            var glow = pack.FaceUp ? Math.Max(0.35f, 1f - pack.Ticks / 30f) : Math.Min(1f, pack.Ticks / (float)TeaseTicks);
            var color = card.Rarity == CardRarity.Crown ? new Color(255, 210, 110) : card.Rarity == CardRarity.Shiny ? new Color(210, 170, 255) : new Color(255, 240, 200);
            batch.BeginVirtual(BlendState.Additive, SamplerState.LinearClamp);
            var pulse = 1f + 0.05f * MathF.Sin(_viewAge / 5f);
            batch.Draw(art.Glow, new Rectangle((int)(PackCenter.X - 230 * pulse), (int)(PackCenter.Y - 250 * pulse), (int)(460 * pulse), (int)(500 * pulse)), color * (0.7f * glow));
            for (var i = 0; i < 12; i++)
            {
                var angle = i * MathF.Tau / 12 + _viewAge / 90f;
                var tip = PackCenter + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * 260;
                UiDraw.Line(batch, Services.Primitives, PackCenter, tip, color * (0.18f * glow), 10f);
            }

            batch.End();
        }

        var slide = pack.Sliding > 0 ? 1f - pack.Sliding / (float)SlideTicks : 0f;
        var center = PackCenter + new Vector2(-420 * slide * slide, -30 * slide);
        _view!.Draw(batch, _composed ?? back, back, center, RevealSize, 1f - slide * 0.3f);

        if (pack.FaceUp && pack.Sliding == 0)
        {
            batch.BeginVirtual(samplerState: SamplerState.PointClamp);
            var name = card.Tcg.Name;
            UiDraw.TextCentered(batch, Body, name, PackCenter.X, PackCenter.Y + RevealSize.Y / 2 + 4, Paper, Ink);
            UiDraw.TextCentered(batch, Small, $"{card.Spec.NameIt.ToUpperInvariant()}   {pack.Index + 1}/{pack.Cards.Count}", PackCenter.X, 26, Gold, Ink);
            batch.End();
        }
    }

    private void DrawSummary(SpriteBatch batch, PackState pack)
    {
        var art = Services.CardArt;
        var count = pack.Cards.Count;
        const int width = 120;
        const int height = 166;
        var left = (int)PackCenter.X - (count * (width + 16) - 16) / 2;
        var appear = Math.Min(1f, pack.Ticks / 20f);
        batch.BeginVirtual(samplerState: SamplerState.LinearClamp);
        for (var i = 0; i < count; i++)
        {
            var cell = new Rectangle(left + i * (width + 16), (int)(PackCenter.Y - height / 2f + 20 * (1f - appear)), width, height);
            if (art.Face(pack.Cards[i]) is { } thumb)
                batch.Draw(thumb, cell, Color.White * appear);
        }

        batch.End();
        batch.BeginVirtual(samplerState: SamplerState.PointClamp);
        UiDraw.TextCentered(batch, Services.Fonts.Title(Services.Style), "NUOVE CARTE!", PackCenter.X, 34, Gold, Ink);
        for (var i = 0; i < count; i++)
        {
            var card = pack.Cards[i];
            var x = left + i * (width + 16) + width / 2;
            var name = card.Tcg.Name;
            UiDraw.TextCentered(batch, Small, name.ToUpperInvariant(), x, (int)PackCenter.Y + height / 2 + 6, Paper, Ink);
            UiDraw.TextCentered(batch, Small, card.Spec.NameIt.ToUpperInvariant(), x, (int)PackCenter.Y + height / 2 + 16, card.Rarity >= CardRarity.FullArt ? Gold : Dim, Ink);
        }

        batch.End();
    }
}
