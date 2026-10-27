using System.Numerics;
using System.Text.Json;
using PokeRogue.Core.Data;
using PokeRogue.Core.Run.Characters;
using PokeRogue.Core.Run.Pickups;
using PokeRogue.Core.Run.Progression;

namespace PokeRogue.Core.Run.Merchant;

/// <summary>What Kecleon sells (phase 9.2).</summary>
public enum WareKind
{
    Potion,
    RareCandy,
    HeldItem,
    Reroll,
    Repel,
    ExpShare,
    EvolutionItem,
}

public sealed record WareSpec
{
    public WareKind Kind { get; set; }

    public int Price { get; set; }

    /// <summary>How often it is drawn; 0 = only when it is due (the evolution item).</summary>
    public int Weight { get; set; } = 1;

    /// <summary>Potion: share of the PS it heals.</summary>
    public float Power { get; set; }
}

/// <summary>Kecleon as written in content/data/merchant.json.</summary>
public sealed record MerchantFile
{
    public int Version { get; set; } = 1;

    public bool Enabled { get; set; } = true;

    /// <summary>When it comes ("m:ss"), once each.</summary>
    public IReadOnlyList<string> AppearAt { get; set; } = [];

    /// <summary>Seconds it stays.</summary>
    public float Stay { get; set; } = 60f;

    /// <summary>How far from the player it shows up.</summary>
    public float Distance { get; set; } = 150f;

    /// <summary>The shop opens when the player comes this close (plus its own radius).</summary>
    public float Reach { get; set; } = 20f;

    /// <summary>Wares it brings.</summary>
    public int Stock { get; set; } = 4;

    public IReadOnlyList<WareSpec> Wares { get; set; } = [];
}

/// <summary>One ware on Kecleon's mat, ready to show; <see cref="Sold"/> once bought.</summary>
public sealed class Ware(WareKind kind, int price, string nameIt, string descriptionIt, string iconKey, string? itemId = null, float power = 0f)
{
    public WareKind Kind { get; } = kind;

    public int Price { get; } = price;

    public string NameIt { get; } = nameIt;

    public string DescriptionIt { get; } = descriptionIt;

    public string IconKey { get; } = iconKey;

    /// <summary>The held item or evolution item it is.</summary>
    public string? ItemId { get; } = itemId;

    public float Power { get; } = power;

    public bool Sold { get; set; }
}

/// <summary>Kecleon's visit to the level.</summary>
public sealed class MerchantState
{
    public bool Present { get; internal set; }

    public Vector2 Position { get; internal set; }

    public int ArrivedTick { get; internal set; }

    public int LeavesTick { get; internal set; }

    /// <summary>Visits made this run (the next time in the list).</summary>
    public int Visits { get; internal set; }

    public List<Ware> Stock { get; } = [];

    /// <summary>The player is still against it since the shop last closed: it opens again only after stepping away.</summary>
    internal bool Touching;
}

/// <summary>The rules of content/data/merchant.json, checked.</summary>
public sealed class MerchantRules
{
    public const string FileName = "merchant.json";

    private MerchantRules(MerchantFile file, IReadOnlyList<int> appearTicks)
    {
        File = file;
        AppearTicks = appearTicks;
    }

    public MerchantFile File { get; }

    public IReadOnlyList<int> AppearTicks { get; }

    public int StayTicks => RunClock.Seconds(File.Stay);

    public static MerchantRules Load(string contentRoot)
    {
        var path = Path.Combine(contentRoot, "data", FileName);
        using var stream = System.IO.File.OpenRead(path);
        var file = JsonSerializer.Deserialize(stream, DataJsonContext.Default.MerchantFile)
            ?? throw new InvalidDataException($"{path} is empty.");
        return From(file);
    }

    public static MerchantRules From(MerchantFile file)
    {
        var times = file.AppearAt.Select(t => Waves.WaveTimeline.ParseTime(t, FileName)).ToList();
        if (times.Zip(times.Skip(1)).Any(pair => pair.Second <= pair.First))
            throw new InvalidDataException($"{FileName}: the visits go in order.");
        if (file.Stay <= 0f || file.Distance <= 0f || file.Reach <= 0f || file.Stock < 1)
            throw new InvalidDataException($"{FileName}: stay, distance and reach above zero, at least one ware.");
        if (file.Wares.Any(w => w.Price <= 0 || w.Weight < 0) || file.Wares.GroupBy(w => w.Kind).Any(g => g.Count() > 1))
            throw new InvalidDataException($"{FileName}: each kind once, with a positive price and a weight of 0 or more.");
        if (file.Wares.FirstOrDefault(w => w.Kind == WareKind.Potion) is { Power: <= 0f or > 1f })
            throw new InvalidDataException($"{FileName}: the potion heals a share of the PS between 0 and 1.");

        return new MerchantRules(file, times);
    }
}

/// <summary>
/// Kecleon (phase 9.2): a merchant that visits each level twice, as the one of Vampire Survivors' stages. It stands
/// still, camouflaged; walking into it opens the shop (<see cref="RunState.ShopOpen"/>), where the run's coins buy
/// potions, candies, held items and charges.
/// </summary>
public static class MerchantSystem
{
    /// <summary>Every tick: Kecleon arrives at its times, leaves when its stay is over, and opens its shop when touched.</summary>
    public static void Tick(RunState run)
    {
        if (run.MerchantRules is not { } rules)
            return;

        var state = run.Merchant;
        if (state.Present)
        {
            if (run.Ticks >= state.LeavesTick || run.Boss is not null)
            {
                Leave(run);
                return;
            }

            var reach = rules.File.Reach + run.Player.Radius;
            var distance = Vector2.DistanceSquared(run.Player.Position, state.Position);
            if (distance > reach * reach * 2.25f)
                state.Touching = false;
            else if (distance <= reach * reach && !state.Touching && !run.Player.IsDown)
            {
                state.Touching = true;
                run.OpenShop();
            }

            return;
        }

        // Its next visit; one missed while a boss fought (or skipped with a time jump) is gone.
        while (state.Visits < rules.AppearTicks.Count && run.Ticks > rules.AppearTicks[state.Visits] + RunClock.TicksPerSecond)
            state.Visits++;
        if (state.Visits < rules.AppearTicks.Count && run.Ticks >= rules.AppearTicks[state.Visits] && run.Boss is null)
            Arrive(run, rules);
    }

    /// <summary>Kecleon comes now, ahead of the player (previews and the schedule).</summary>
    public static void Arrive(RunState run, MerchantRules rules)
    {
        var state = run.Merchant;
        var player = run.Player;
        var heading = player.IsMoving ? Vector2.Normalize(player.Velocity) : new Vector2(MathF.Cos(state.Visits * 2.4f + 0.6f), MathF.Sin(state.Visits * 2.4f + 0.6f));
        var at = player.Position + heading * rules.File.Distance;
        if (run.Map.IsConstrained)
            at = run.Map.FreePoint(run.Map.Clamp(at, 40f), 18f);

        state.Present = true;
        state.Position = at;
        state.ArrivedTick = run.Ticks;
        state.LeavesTick = run.Ticks + rules.StayTicks;
        state.Visits++;
        state.Touching = false;
        Restock(run, rules);
        run.AddEvent(new RunEvent(RunEventType.MerchantArrived, at));
    }

    private static void Leave(RunState run)
    {
        var state = run.Merchant;
        state.Present = false;
        state.Stock.Clear();
        run.AddEvent(new RunEvent(RunEventType.MerchantLeft, state.Position));
    }

    /// <summary>The wares of this visit: the evolution item first when due, then distinct kinds drawn by weight.</summary>
    private static void Restock(RunState run, MerchantRules rules)
    {
        var stock = run.Merchant.Stock;
        stock.Clear();
        if (rules.File.Wares.FirstOrDefault(w => w.Kind == WareKind.EvolutionItem) is { } evolution && CharacterEvolution.ItemDue(run) is { } stage)
        {
            var reward = CharacterEvolution.ItemReward(run, stage);
            stock.Add(new Ware(WareKind.EvolutionItem, evolution.Price, reward.NameIt, reward.DescriptionIt, reward.IconKey, reward.Id));
        }

        var pool = rules.File.Wares.Where(w => w.Weight > 0).ToList();
        while (stock.Count < rules.File.Stock && pool.Count > 0)
        {
            var roll = run.Random.Next(pool.Sum(w => w.Weight));
            var spec = pool.First(w => (roll -= w.Weight) < 0);
            pool.Remove(spec);
            if (Make(run, spec) is { } ware)
                stock.Add(ware);
        }
    }

    private static Ware? Make(RunState run, WareSpec spec) => spec.Kind switch
    {
        WareKind.Potion => new Ware(spec.Kind, spec.Price, "Superpozione", $"Recupera il {MathF.Round(spec.Power * 100f)}% dei PS.", "super-potion", power: spec.Power),
        WareKind.RareCandy => new Ware(spec.Kind, spec.Price, "Caramella Rara", "Sali subito di un livello.", "rare-candy"),
        WareKind.Reroll => new Ware(spec.Kind, spec.Price, "Rilancio", "Un Rilancio in più per i level-up.", "max-elixir"),
        WareKind.Repel => new Ware(spec.Kind, spec.Price, "Repellente", "I Pokémon selvatici sullo schermo scappano e lasciano la gemma.", "max-repel"),
        WareKind.ExpShare => new Ware(spec.Kind, spec.Price, "Condividi Esp.", "Tutte le gemme della mappa volano da te.", "exp-share"),
        WareKind.HeldItem when LootSystem.ShinyItem(run) is { } id && run.ItemPool.FirstOrDefault(d => d.Id == id) is { } item =>
            new Ware(spec.Kind, spec.Price, item.NameIt,
                run.Player.Items.FirstOrDefault(i => i.Definition.Id == id) is { } held ? $"Livello {held.Level + 1}: {item.DescriptionIt}" : item.DescriptionIt,
                item.IconKey, id),
        _ => null,
    };

    /// <summary>Buys the ware at <paramref name="index"/> with the run's coins. False when sold, too dear, or no shop is open.</summary>
    public static bool Buy(RunState run, int index)
    {
        var stock = run.Merchant.Stock;
        if (!run.ShopOpen || (uint)index >= (uint)stock.Count)
            return false;

        var ware = stock[index];
        if (ware.Sold || run.Player.Coins < ware.Price)
            return false;

        var player = run.Player;
        switch (ware.Kind)
        {
            case WareKind.Potion:
                player.Heal(player.MaxHp * ware.Power * Abilities.AbilitySystem.HealMultiplier(run) * Plates.PlateSystem.HealMultiplier(run));
                break;
            case WareKind.RareCandy:
                run.GrantLevel();
                break;
            case WareKind.HeldItem when ware.ItemId is { } id:
                if (!LootSystem.TryGiveItem(run, id))
                    return false;
                break;
            case WareKind.Reroll:
                run.RerollsLeft++;
                break;
            case WareKind.Repel:
                LootSystem.Repel(run);
                break;
            case WareKind.ExpShare:
                run.Pickups.Gems.MagnetizeAll();
                break;
            case WareKind.EvolutionItem when ware.ItemId is { } stone:
                run.EvolutionItems.Add(stone);
                break;
        }

        player.Coins -= ware.Price;
        run.Stats.RecordSpent(ware.Price);
        ware.Sold = true;
        run.AddEventOnResume(new RunEvent(RunEventType.MerchantSale, run.Merchant.Position, ware.Price, MoveId: ware.Kind.ToString()));
        return true;
    }
}
