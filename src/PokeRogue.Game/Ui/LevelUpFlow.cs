using PokeRogue.Core.Run;
using PokeRogue.Game.Input;

namespace PokeRogue.Game.Ui;

/// <summary>
/// Sequences a level-up: short intro (the burst around the player plays while the game is frozen),
/// then the choice window, then the next window if more level-ups are pending. Rilancio, Salta and Bandisci
/// go through the run, which keeps their charges.
/// </summary>
public sealed class LevelUpFlow(RunState run, LevelUpWindow window)
{
    public const int IntroTicks = 30;

    private int _intro = -1;

    /// <summary>True while gameplay must stay frozen.</summary>
    public bool Active => window.IsOpen || run.IsPaused;

    public LevelUpWindow Window => window;

    /// <summary>Level reached by the choice currently on screen (several can be queued).</summary>
    private int ChoiceLevel => run.Player.Level - run.PendingLevelUps + 1;

    public void Update(InputState? input)
    {
        if (window.IsOpen)
        {
            window.Charges = (run.RerollsLeft, run.SkipsLeft, run.BanishesLeft);
            if (window.Update(input) is { } action)
                Apply(action);
            return;
        }

        if (run.LevelUpChoices is not { } choices)
            return;

        if (_intro < 0)
            _intro = IntroTicks;
        if (--_intro > 0)
            return;

        _intro = -1;
        window.Open(choices, ChoiceLevel);
    }

    private void Apply(LevelUpAction action)
    {
        switch (action.Command)
        {
            case LevelUpCommand.Choose:
                run.ChooseLevelUp(action.Index);
                OpenNextOrClose();
                break;
            case LevelUpCommand.Skip when run.SkipLevelUp():
                OpenNextOrClose();
                break;
            case LevelUpCommand.Reroll when run.RerollLevelUp():
            case LevelUpCommand.Banish when run.BanishLevelUp(action.Index):
                window.Refresh(run.LevelUpChoices!);
                break;
        }

        window.Charges = (run.RerollsLeft, run.SkipsLeft, run.BanishesLeft);
    }

    private void OpenNextOrClose()
    {
        if (run.LevelUpChoices is { } next)
            window.Open(next, ChoiceLevel);
        else
            window.Close();
    }
}
