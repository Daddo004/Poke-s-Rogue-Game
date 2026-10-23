namespace PokeRogue.Core.Data;

/// <summary>PMD SpriteCollab animation names the game uses.</summary>
public static class PmdAnimations
{
    public const string Idle = "Idle";
    public const string Walk = "Walk";
    public const string Attack = "Attack";
    public const string Shoot = "Shoot";
    public const string Charge = "Charge";
    public const string Hurt = "Hurt";
    public const string Faint = "Faint";
    public const string Sleep = "Sleep";

    /// <summary>Victory pose at the end of a won run.</summary>
    public const string Pose = "Pose";

    public static readonly IReadOnlyList<string> Required = [Idle, Walk, Attack, Shoot, Charge, Hurt, Faint, Sleep, Pose];

    public const string AnimDataFile = "AnimData.xml";

    public static string SheetFile(string animName) => $"{animName}-Anim.png";
}
