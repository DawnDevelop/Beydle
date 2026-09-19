using Beydle.Models;
using Beydle.Services;

namespace Beydle.Tests;

public class AchievementsTests
{
    private static Blade Make(string id, string? released) =>
        new(id, id, [], "BX-01", "BX", "Attack", "Right", 30, 50, 20, 20, "3-60", "Flat", "F", "Attack", $"img/{id}.webp", 2023, null, released);

    [Theory]
    [InlineData("let it rip")]
    [InlineData("  LET IT RIP!!  ")]
    [InlineData("3, 2, 1, Go Shoot!")]
    [InlineData("Xtreme Finish")]
    public void BattleCallsGetAReply(string input) => Assert.NotNull(Achievements.Reply(input));

    [Theory]
    [InlineData("dran")]
    [InlineData("")]
    [InlineData("let it")]
    public void OtherTextGetsNoReply(string input) => Assert.Null(Achievements.Reply(input));

    [Fact]
    public void AnniversaryCountsWholeYearsOnTheSameMonthAndDay()
    {
        var b = Make("a", "2023-07-15");
        Assert.Equal(3, Achievements.AnniversaryYears(b, new DateOnly(2026, 7, 15)));
        Assert.Null(Achievements.AnniversaryYears(b, new DateOnly(2026, 7, 16)));
        Assert.Null(Achievements.AnniversaryYears(b, new DateOnly(2023, 7, 15)));
    }

    [Fact]
    public void BladesWithoutAFullDateNeverHaveAnAnniversary()
    {
        Assert.Null(Achievements.AnniversaryYears(Make("a", null), new DateOnly(2026, 7, 15)));
    }

    private static Blade Named(string name) =>
        new(name.ToLower().Replace(' ', '-'), name, [], "BX-01", "BX", "Attack", "Right", 30, 50, 20, 20, "3-60", "Flat", "F", "Attack", "img/x.webp", 2023, null);

    private static readonly Blade DranSword = Named("Dran Sword");
    private static readonly Blade DranBuster = Named("Dran Buster");
    private static readonly Blade DranDagger = Named("Dran Dagger");
    private static readonly Blade HellsScythe = Named("Hells Scythe");
    private static readonly Blade KnightShield = Named("Knight Shield");
    private static readonly IReadOnlyList<Blade> Pool = [DranSword, DranBuster, DranDagger, HellsScythe, KnightShield];

    private static List<Achievement> Earned(GameSession g, Stats? stats = null, IReadOnlySet<string>? seen = null) =>
        Achievements.Earned(g, stats ?? new Stats(), Pool, seen ?? new HashSet<string>()).ToList();

    [Fact]
    public void NothingIsEarnedBeforeTheFirstGuess() => Assert.Empty(Earned(new GameSession(DranSword, HellsScythe)));

    [Fact]
    public void FirstGuessSolvesEarnXtremeSharpEyeAndPerfectDay()
    {
        var g = new GameSession(DranSword, HellsScythe);
        g.Submit(DranSword);
        Assert.Equal([Achievements.XtremeFinish], Earned(g));
        g.Submit(HellsScythe);
        Assert.Equal([Achievements.XtremeFinish, Achievements.SharpEye, Achievements.PerfectDay], Earned(g));
    }

    [Fact]
    public void FamilyReunionNeedsThreeInARow()
    {
        var g = new GameSession(KnightShield, HellsScythe);
        g.Submit(DranSword);
        g.Submit(DranBuster);
        g.Submit(HellsScythe);
        g.Submit(DranDagger);
        Assert.DoesNotContain(Achievements.FamilyReunion, Earned(g));

        var run = new GameSession(KnightShield, HellsScythe);
        run.Submit(DranSword);
        run.Submit(DranBuster);
        run.Submit(DranDagger);
        Assert.Contains(Achievements.FamilyReunion, Earned(run));
    }

    [Fact]
    public void DejaVuIsGuessingTheRoundOneAnswerInRoundTwo()
    {
        var g = new GameSession(DranSword, HellsScythe);
        g.Submit(DranSword);
        g.Submit(DranSword);
        Assert.Contains(Achievements.DejaVu, Earned(g));
    }

    [Fact]
    public void StaminaAndFullyRevealedSolvesCountGuesses()
    {
        var g = new GameSession(DranSword, HellsScythe);
        for (var i = 0; i < Achievements.StaminaGuesses - 1; i++) g.Submit(KnightShield);
        g.Submit(DranSword);
        Assert.Contains(Achievements.StaminaType, Earned(g));

        for (var i = 0; i < ImageRound.MaxStage; i++) g.Submit(KnightShield);
        Assert.DoesNotContain(Achievements.AsClearAsItGets, Earned(g));
        g.Submit(HellsScythe);
        Assert.Contains(Achievements.AsClearAsItGets, Earned(g));
    }

    [Fact]
    public void StreakAndEncyclopediaComeFromStatsAndSeenBlades()
    {
        var g = new GameSession(DranSword, HellsScythe);
        g.Submit(KnightShield);
        Assert.Equal([Achievements.OnARoll], Earned(g, new Stats { Streak = 7 }));
        Assert.Equal([Achievements.OnARoll, Achievements.IronWill], Earned(g, new Stats { Streak = 30 }));

        var allSeen = Pool.Select(b => b.Id).ToHashSet();
        Assert.Contains(Achievements.Encyclopedia, Earned(g, seen: allSeen));
        allSeen.Remove(DranDagger.Id);
        Assert.DoesNotContain(Achievements.Encyclopedia, Earned(g, seen: allSeen));
    }

    [Fact]
    public void ShareTextMarksAFirstGuessSolve()
    {
        var a = Make("a", null);
        var b = Make("b", null);
        var g = new GameSession(a, b);
        g.Submit(a);
        g.Submit(b);
        Assert.Contains("Blade 1/∞", g.ShareText("Beydle #1", "u"));
        Assert.Contains("⚡", g.ShareText("Beydle #1", "u").Split('\n')[0]);

        var slow = new GameSession(a, b);
        slow.Submit(b);
        slow.Submit(a);
        Assert.DoesNotContain("⚡", slow.ShareText("Beydle #1", "u"));
    }

    [Fact]
    public void OldSavedStateLoadsWithEmptyAchievements()
    {
        var state = StorageService.Parse("""{"day":"2026-01-01","guesses":[],"imageGuesses":[],"stats":{}}""");
        Assert.NotNull(state.Achievements);
        Assert.Empty(state.Achievements);
        Assert.Empty(state.Seen);
    }
}
