namespace PokeRogue.Core.Combat;

/// <summary>Status conditions moves can inflict; rules in content/data/statuses.json, behaviour in <see cref="Run.Statuses.StatusSystem"/>.</summary>
public enum StatusKind
{
    None,
    Burn,
    Poison,
    Paralysis,
    Freeze,
    Sleep,
    Confusion,
}
