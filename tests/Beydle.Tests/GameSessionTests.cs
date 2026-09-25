using Beydle.Models;
using Beydle.Services;

namespace Beydle.Tests;

public class GameSessionTests
{
    private static Blade Make(string id, string type = "Attack") =>
        new(id, id, [], "BX-01", "BX", type, "Right", 30, 50, 20, 20, "3-60", "Flat", "F", "Attack", $"img/{id}.webp", 2023, "Someone");

    private static readonly Blade A = Make("a");
    private static readonly Blade B = Make("b", "Stamina");
    private static readonly Blade C = Make("c", "Defense");
    private static readonly IReadOnlyList<Blade> Pool = [A, B, C];

    [Fact]
    public void StartsInRoundOneWithNothingGuessed()
    {
        var g = new GameSession(A, B);
        Assert.False(g.Blade.Won);
        Assert.False(g.InImageRound);
        Assert.False(g.Complete);
        Assert.Empty(g.GuessedIds);
    }

    [Fact]
    public void WrongGuessStaysInRoundOneAndIsExcludedFromSuggestions()
    {
        var g = new GameSession(A, B);
        Assert.True(g.Submit(C));
        Assert.False(g.Blade.Won);
        Assert.Equal(["c"], g.GuessedIds);
    }

    [Fact]
    public void SolvingRoundOneMovesToImageRoundWithFreshGuessList()
    {
        var g = new GameSession(A, B);
        g.Submit(C);
        g.Submit(A);
        Assert.True(g.Blade.Won);
        Assert.True(g.InImageRound);
        Assert.False(g.Complete);
        Assert.Empty(g.GuessedIds); // round 1 guesses do not carry over
        Assert.Equal(0, g.Image.Stage);
    }

    [Fact]
    public void ImageRoundSharpensPerMissAndCapsAtMaxStage()
    {
        var g = new GameSession(A, B);
        g.Submit(A);
        for (var i = 0; i < ImageRound.MaxStage + 3; i++) g.Submit(C);
        Assert.Equal(ImageRound.MaxStage, g.Image.Stage);
        Assert.False(g.Image.Won);
    }

    [Fact]
    public void SolvingImageRoundCompletesTheGameAndRejectsFurtherGuesses()
    {
        var g = new GameSession(A, B);
        g.Submit(A);
        g.Submit(C);
        g.Submit(B);
        Assert.True(g.Complete);
        Assert.False(g.Submit(C));
        Assert.Equal(2, g.Image.Guesses.Count);
    }

    [Fact]
    public void RestoreReplaysBothRoundsAndSkipsUnknownIds()
    {
        var g = new GameSession(A, B);
        g.Restore(Pool, ["c", "removed-blade", "a"], ["c", "b"]);
        Assert.True(g.Blade.Won);
        Assert.Equal(2, g.Blade.Guesses.Count);
        Assert.True(g.Complete);
        Assert.Equal(2, g.Image.Guesses.Count);
    }

    [Fact]
    public void RestoreStopsReplayingOnceARoundIsWon()
    {
        var g = new GameSession(A, B);
        g.Restore(Pool, ["a", "c"], []);
        Assert.Single(g.Blade.Guesses);
    }

    [Fact]
    public void ShareTextHasOneEmojiRowPerRoundOneGuessAndAnImageLine()
    {
        var g = new GameSession(A, B);
        g.Submit(C);
        g.Submit(A);
        g.Submit(C);
        g.Submit(B);
        var lines = g.ShareText("Beydle #5", "https://x/").Split('\n');
        Assert.Equal("Beydle #5 – Blade 2/∞ · Image 2/∞", lines[0]);
        Assert.Equal(GuessResult.Labels.Length, lines[1].EnumerateRunes().Count());
        Assert.Equal(string.Concat(Enumerable.Repeat("🟩", GuessResult.Labels.Length)), lines[2]);
        Assert.Equal("🖼 ⬛🟩", lines[3]);
        Assert.Equal("https://x/", lines[4]);
    }

    [Fact]
    public void ExtremeModeRefusesGuessesThatContradictTheHints()
    {
        var g = new GameSession(A, B) { Extreme = true };
        Assert.Null(g.Rejects(C)); // nothing to contradict yet
        g.Submit(B); // Stamina, so the answer is not Stamina
        var staminaAgain = Make("d", "Stamina");
        Assert.Equal("Type can't be Stamina.", g.Rejects(staminaAgain));
        Assert.False(g.Submit(staminaAgain));
        Assert.Single(g.Blade.Guesses);
        Assert.True(g.Submit(A));
        Assert.True(g.Blade.Won);
    }

    [Fact]
    public void ExtremeModeDoesNotApplyToRoundTwoOrWhenOff()
    {
        var easy = new GameSession(A, B);
        easy.Submit(B);
        Assert.Null(easy.Rejects(Make("d", "Stamina")));

        var extreme = new GameSession(A, B) { Extreme = true };
        extreme.Submit(A);
        Assert.Null(extreme.Rejects(C));
        Assert.True(extreme.Submit(C));
    }

    [Fact]
    public void SwitchingExtremeModeOffMidRoundLiftsTheRule()
    {
        var g = new GameSession(A, B) { Extreme = true };
        g.Submit(B);
        var staminaAgain = Make("d", "Stamina");
        Assert.NotNull(g.Rejects(staminaAgain));
        g.Extreme = false;
        Assert.Null(g.Rejects(staminaAgain));
        Assert.True(g.Submit(staminaAgain));
    }

    [Fact]
    public void ShareTextMarksExtremeMode()
    {
        var g = new GameSession(A, B) { Extreme = true };
        g.Submit(A);
        g.Submit(B);
        Assert.StartsWith("Beydle #5 – Blade 1/∞* · Image 1/∞", g.ShareText("Beydle #5", "u"));
        g.Extreme = false;
        Assert.StartsWith("Beydle #5 – Blade 1/∞ · Image 1/∞", g.ShareText("Beydle #5", "u"));
    }
}

public class StatsTrackerTests
{
    [Fact]
    public void PlayedCountsDaysWithAGuessOnceAndWonCountsSolvedDays()
    {
        var s = new Stats();
        StatsTracker.RecordFirstGuess(s, "2026-09-19");
        StatsTracker.RecordFirstGuess(s, "2026-09-19");
        Assert.Equal(1, s.Played);
        Assert.Equal(0, s.Won);
        Assert.Equal(0, StatsTracker.AverageGuesses(s));

        StatsTracker.RecordFirstGuess(s, "2026-09-20");
        StatsTracker.RecordWin(s, new DateOnly(2026, 9, 20), 3);
        Assert.Equal(2, s.Played);
        Assert.Equal(1, s.Won);
        Assert.Equal(3.0, StatsTracker.AverageGuesses(s));
        Assert.Equal(1, s.Distribution["3"]);

        StatsTracker.RecordWin(s, new DateOnly(2026, 9, 21), 6);
        Assert.Equal(4.5, StatsTracker.AverageGuesses(s));
    }

    [Fact]
    public void StreakContinuesOnConsecutiveDaysAndResetsAfterAGap()
    {
        var s = new Stats();
        StatsTracker.RecordWin(s, new DateOnly(2026, 9, 18), 2);
        StatsTracker.RecordWin(s, new DateOnly(2026, 9, 19), 2);
        Assert.Equal(2, s.Streak);
        StatsTracker.RecordWin(s, new DateOnly(2026, 9, 21), 2);
        Assert.Equal(1, s.Streak);
        Assert.Equal(2, s.MaxStreak);
    }

    [Fact]
    public void DisplayedStreakDropsToZeroWhenYesterdayWasMissed()
    {
        var s = new Stats();
        StatsTracker.RecordWin(s, new DateOnly(2026, 9, 18), 2);
        Assert.Equal(1, StatsTracker.CurrentStreak(s, new DateOnly(2026, 9, 19)));
        Assert.Equal(0, StatsTracker.CurrentStreak(s, new DateOnly(2026, 9, 20)));
        Assert.Equal(1, s.Streak); // stored value is untouched; it is recomputed on the next win
    }

    [Fact]
    public void WinIsRecordedOncePerDay()
    {
        var s = new Stats();
        StatsTracker.RecordWin(s, new DateOnly(2026, 9, 19), 2);
        StatsTracker.RecordWin(s, new DateOnly(2026, 9, 19), 5);
        Assert.Equal(1, s.Won);
        Assert.Single(s.Distribution);
    }

    [Fact]
    public void SevenOrMoreGuessesShareOneBucket()
    {
        var s = new Stats();
        StatsTracker.RecordWin(s, new DateOnly(2026, 9, 19), 12);
        Assert.Equal(1, s.Distribution["7+"]);
    }
}

public class StorageParseTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("{not json")]
    [InlineData("[1,2,3]")]
    [InlineData("\"a string\"")]
    [InlineData("{\"day\":\"2026-09-19\",\"guesses\":null,\"stats\":null}")]
    public void MalformedOrForeignDataYieldsAUsableState(string? raw)
    {
        var s = StorageService.Parse(raw);
        Assert.NotNull(s.Guesses);
        Assert.NotNull(s.ImageGuesses);
        Assert.NotNull(s.Stats);
        Assert.NotNull(s.Stats.Distribution);
    }

    [Fact]
    public void ValidDataRoundTrips()
    {
        var s = StorageService.Parse("{\"day\":\"2026-09-19\",\"guesses\":[\"dran-sword\"],\"imageGuesses\":[],\"stats\":{\"played\":3,\"won\":2,\"streak\":1,\"maxStreak\":2,\"lastWinDay\":\"2026-09-19\",\"distribution\":{\"2\":2}}}");
        Assert.Equal("2026-09-19", s.Day);
        Assert.Equal(["dran-sword"], s.Guesses);
        Assert.Equal(3, s.Stats.Played);
        Assert.Equal(2, s.Stats.Distribution["2"]);
    }

    [Fact]
    public void StateFromBeforeExtremeModeLoadsWithExtremeModeOff()
    {
        var s = StorageService.Parse("{\"day\":\"2026-09-19\",\"guesses\":[\"dran-sword\"],\"stats\":{}}");
        Assert.False(s.ExtremeMode);
        Assert.False(s.DayExtreme);
    }

    [Fact]
    public void OlderStateWithoutNewFieldsStillLoads()
    {
        var s = StorageService.Parse("{\"day\":\"2026-09-19\",\"guesses\":[],\"stats\":{\"played\":1,\"won\":1}}");
        Assert.Empty(s.ImageGuesses);
        Assert.Null(s.Stats.LastPlayedDay);
    }
}
