using Beydle.Models;
using Beydle.Services;

namespace Beydle.Tests;

public class GamesTests
{
    private static Blade Make(string id, string type) =>
        new(id, id, [], "BX-01", "BX", type, "Right", 30, 50, 20, 20, "3-60", "Flat", "F", "Attack", $"img/{id}.webp", 2023, null);

    private static readonly IReadOnlyList<Blade> Pool =
        [Make("a", "Attack"), Make("b", "Stamina"), Make("c", "Defense"), Make("d", "Balance"), Make("e", "Attack"), Make("f", "Stamina")];

    private static readonly DateOnly Day = new(2026, 9, 26);

    private static Games Start(SavedState? state = null, int seed = 1)
    {
        var games = new Games(Pool, null, state ?? new SavedState(), new Random(seed));
        games.StartDay(Day);
        games.NewPractice();
        return games;
    }

    private static Blade Wrong(GameSession g) => Pool.First(b => b.Id != g.Blade.Answer.Id && g.Rejects(b) is null);

    [Fact]
    public void StartDayReplaysGuessesSavedForTheSameDay()
    {
        var first = Start();
        var wrong = Wrong(first.Daily);
        first.Daily.Submit(wrong);
        first.RecordDailyGuess(wrong, inImageRound: false);

        var again = new Games(Pool, null, first.State);
        Assert.True(again.StartDay(Day));
        Assert.Equal([wrong.Id], again.Daily.GuessedIds);
    }

    [Fact]
    public void ANewDayStartsEmptyWithThePlayersXtremePreference()
    {
        var state = new SavedState { Day = "2026-09-25", Guesses = ["a"], ImageGuesses = ["b"], XtremeMode = true, DayXtreme = false };
        var games = new Games(Pool, null, state);
        Assert.False(games.StartDay(Day));
        Assert.Equal("2026-09-26", state.Day);
        Assert.Empty(state.Guesses);
        Assert.Empty(state.ImageGuesses);
        Assert.True(state.DayXtreme);
        Assert.True(games.Daily.XtremeMode);
    }

    [Fact]
    public void PracticeNeverUsesTodaysDailyBlades()
    {
        for (var seed = 0; seed < 50; seed++)
        {
            var games = Start(seed: seed);
            var daily = new[] { games.Daily.Blade.Answer.Id, games.Daily.Image.Answer.Id };
            Assert.DoesNotContain(games.Practice.Blade.Answer.Id, daily);
            Assert.DoesNotContain(games.Practice.Image.Answer.Id, daily);
            Assert.NotEqual(games.Practice.Blade.Answer.Id, games.Practice.Image.Answer.Id);
        }
    }

    [Fact]
    public void TogglingXtremeModeIsRememberedAndReachesTheOtherGameIfItHasNotStarted()
    {
        var games = Start();
        Assert.True(games.ToggleXtremeMode(games.Practice));
        Assert.True(games.State.XtremeMode);
        Assert.True(games.Practice.XtremeMode);
        Assert.True(games.Daily.XtremeMode);
        Assert.True(games.State.DayXtreme);
    }

    [Fact]
    public void TogglingXtremeModeLeavesAStartedGameAlone()
    {
        var games = Start();
        games.Daily.Submit(Wrong(games.Daily));
        games.ToggleXtremeMode(games.Practice);
        Assert.False(games.Daily.XtremeMode);
        Assert.False(games.State.DayXtreme);
    }

    [Fact]
    public void RecordDailyGuessSavesBothRoundsAndCountsTheWin()
    {
        var games = Start();
        var daily = games.Daily;
        var wrong = Wrong(daily);
        foreach (var (blade, round2) in new[] { (wrong, false), (daily.Blade.Answer, false), (daily.Blade.Answer, true) })
        {
            daily.Submit(blade);
            games.RecordDailyGuess(blade, round2);
        }
        Assert.Equal([wrong.Id, daily.Blade.Answer.Id], games.State.Guesses);
        Assert.Equal([daily.Blade.Answer.Id], games.State.ImageGuesses);
        Assert.Equal(1, games.Stats.Played);
        Assert.Equal(1, games.Stats.Won);
        Assert.Equal(2, games.Stats.TotalWinGuesses);
        Assert.Contains(wrong.Id, games.State.Seen);
    }

    [Fact]
    public void AnAchievementUnlocksOnceWithTodaysDate()
    {
        var games = Start();
        Assert.True(games.Unlock(Achievements.LetItRip));
        Assert.False(games.Unlock(Achievements.LetItRip));
        Assert.Equal("2026-09-26", games.State.Achievements[Achievements.LetItRip.Id]);
    }

    [Fact]
    public void GrinderIsDueAfterEnoughPracticeRounds()
    {
        var games = Start();
        for (var i = 1; i < Achievements.GrinderRounds; i++) Assert.False(games.FinishPracticeRound());
        Assert.True(games.FinishPracticeRound());
    }
}
