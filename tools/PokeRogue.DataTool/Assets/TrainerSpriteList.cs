namespace PokeRogue.DataTool.Assets;

/// <summary>Trainer sprite keys (Showdown file names, first match wins). Gameplay data lives in content/data/trainers.json.</summary>
public static class TrainerSpriteList
{
    public static readonly IReadOnlyList<(string Key, string[] Candidates)> All =
    [
        ("ash", ["ash"]),
        ("red", ["red"]),

        // Kanto
        ("brock", ["brock"]), ("misty", ["misty"]), ("ltsurge", ["ltsurge"]), ("erika", ["erika"]),
        ("koga", ["koga"]), ("sabrina", ["sabrina"]), ("blaine", ["blaine"]), ("giovanni", ["giovanni"]),
        ("lorelei", ["lorelei", "lorelei-gen3"]), ("bruno", ["bruno"]), ("agatha", ["agatha", "agatha-gen3"]), ("lance", ["lance"]), ("blue", ["blue"]),

        // Johto
        ("falkner", ["falkner"]), ("bugsy", ["bugsy"]), ("whitney", ["whitney"]), ("morty", ["morty"]),
        ("chuck", ["chuck"]), ("jasmine", ["jasmine"]), ("pryce", ["pryce"]), ("clair", ["clair"]), ("janine", ["janine"]),
        ("will", ["will"]), ("karen", ["karen"]),

        // Hoenn
        ("roxanne", ["roxanne"]), ("brawly", ["brawly"]), ("wattson", ["wattson"]), ("flannery", ["flannery"]),
        ("norman", ["norman"]), ("winona", ["winona"]), ("tateandliza", ["tateandliza-gen3", "tate"]),
        ("wallace", ["wallace", "wallace-gen3"]), ("juan", ["juan"]),
        ("sidney", ["sidney"]), ("phoebe", ["phoebe", "phoebe-gen3"]), ("glacia", ["glacia"]), ("drake", ["drake", "drake-gen3"]), ("steven", ["steven", "steven-gen3"]),

        // Sinnoh
        ("roark", ["roark"]), ("gardenia", ["gardenia"]), ("maylene", ["maylene"]), ("crasherwake", ["crasherwake"]),
        ("fantina", ["fantina"]), ("byron", ["byron"]), ("candice", ["candice"]), ("volkner", ["volkner"]),
        ("aaron", ["aaron"]), ("bertha", ["bertha"]), ("flint", ["flint"]), ("lucian", ["lucian"]), ("cynthia", ["cynthia"]),
    ];
}
