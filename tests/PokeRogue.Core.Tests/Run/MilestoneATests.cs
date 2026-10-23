using System.Diagnostics;
using System.Numerics;
using PokeRogue.Core.Run;
using PokeRogue.Core.Run.Moves;

namespace PokeRogue.Core.Tests.Run;

/// <summary>Milestone A: a full 10-minute prototype run, simulated headless with a simple automatic player.</summary>
public class MilestoneATests(Xunit.Abstractions.ITestOutputHelper output)
{
    private const int TenMinutes = RunClock.TicksPerSecond * 60 * 10;

    [Fact]
    public void A_ten_minute_prototype_run_ends_in_victory_with_steady_progress()
    {
        var run = new RunState(TestContent.Pokedex.Value.Species, TestContent.TypeChart.Value, 25,
            [new(10, 3), new(13, 3), new(16, 4), new(19, 5), new(21, 2), new(23, 2)], new Vector2(640, 360), seed: 42)
        {
            TimeLimitTicks = TenMinutes,
            MovePool = TestContent.Moves.Value.Enabled,
        };
        run.Player.Moves.Add(new MoveInstance(TestContent.Move("thundershock")));
        run.Player.Invincible = true;

        var levelAtMinute = new Dictionary<int, int>();
        var clock = Stopwatch.StartNew();
        while (!run.IsOver && run.Ticks <= TenMinutes)
        {
            if (run.IsPaused)
            {
                run.ChooseLevelUp(0);
                continue;
            }

            run.Tick(new PlayerInput(AutoPilot.Input(run)));
            if (run.Ticks % (RunClock.TicksPerSecond * 60) == 0)
            {
                var minute = run.Ticks / (RunClock.TicksPerSecond * 60);
                levelAtMinute[minute] = run.Player.Level;
                var gemsOnGround = run.Pickups.Gems.Items.ToArray().Where(g => g.Active).Sum(g => g.Value);
                output.WriteLine($"min {minute,2}: Lv {run.Player.Level,2}  KO {run.Stats.Kills,5}  vivi {run.Enemies.AliveCount,4}  " +
                    $"gemme a terra {run.Pickups.Gems.ActiveCount,3} ({gemsOnGround} Esp)  " +
                    $"build {string.Join(", ", run.Player.Moves.Select(m => $"{m.Definition.Id} {m.Level}").Concat(run.Player.Items.Select(i => $"{i.Definition.Id} {i.Level}")))}");
            }
        }

        Assert.Equal(RunOutcome.Victory, run.Outcome);
        Assert.Equal(TenMinutes, run.Ticks);
        Assert.True(run.Player.Level >= 10, $"Level {run.Player.Level} after 10 minutes");
        Assert.True(run.Stats.Kills >= 300, $"{run.Stats.Kills} KO after 10 minutes");
        Assert.True(levelAtMinute[1] >= 2, $"Level {levelAtMinute[1]} after the first minute");

        // The whole simulation must be far faster than real time (36,000 ticks).
        Assert.True(clock.Elapsed < TimeSpan.FromSeconds(60), $"Simulation took {clock.Elapsed}");
    }
}
