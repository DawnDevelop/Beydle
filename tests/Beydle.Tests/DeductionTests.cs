using Beydle.Models;
using Beydle.Services;

namespace Beydle.Tests;

public class DeductionTests
{
    private static Blade Make(string id, string type = "Attack", double weight = 30, string ratchet = "3-60", string bit = "Flat",
        string bitType = "Attack", int year = 2023, string? owner = "Bird", string spin = "Right", string line = "BX") =>
        new(id, id, [], "BX-01", line, type, spin, weight, 50, 20, 20, ratchet, bit, "F", bitType, $"img/{id}.webp", year, owner);

    private static GuessResult[] Guesses(Blade answer, params Blade[] guesses) => guesses.Select(g => GuessResult.Compare(g, answer)).ToArray();

    [Fact]
    public void OnlyBladesThatWouldGiveTheSameHintsStayPossible()
    {
        var answer = Make("answer", type: "Stamina");
        var twin = Make("twin", type: "Stamina");
        var other = Make("other", type: "Defense");
        var guess = Make("guess", type: "Attack");
        var hints = Guesses(answer, guess);

        Assert.True(Deduction.Fits(answer, hints));
        Assert.True(Deduction.Fits(twin, hints)); // "Type is not Attack" cannot tell Stamina from Stamina
        Assert.True(Deduction.Fits(other, hints));
        Assert.False(Deduction.Fits(guess, hints)); // a wrong guess is ruled out
        Assert.False(Deduction.Fits(Make("attacker", type: "Attack"), hints));
    }

    [Fact]
    public void ArrowsCountAsHints()
    {
        var answer = Make("answer", weight: 40);
        var hints = Guesses(answer, Make("light", weight: 30)); // answer is heavier
        Assert.True(Deduction.Fits(Make("heavier", weight: 35), hints));
        Assert.False(Deduction.Fits(Make("lighter", weight: 25), hints));
    }

    [Fact]
    public void RemainingCountsShrinkAndEndAtOneOnTheSolve()
    {
        var pool = RealPool();
        var answer = pool[10];
        var guesses = Guesses(answer, pool[0], pool[40], pool[60], answer);
        var left = Deduction.RemainingAfterEach(pool, guesses);
        Assert.Equal(4, left.Length);
        for (var i = 1; i < left.Length; i++) Assert.True(left[i] <= left[i - 1]);
        Assert.True(left[0] < pool.Count);
        Assert.Equal(1, left[^1]);
    }

    [Fact]
    public void TheAnswerAlwaysFitsAndViolationAgreesWithFits()
    {
        var pool = RealPool();
        var rng = new Random(1234);
        for (var game = 0; game < 60; game++)
        {
            var answer = pool[rng.Next(pool.Count)];
            var guesses = Guesses(answer, Enumerable.Range(0, 3).Select(_ => pool[rng.Next(pool.Count)]).Where(b => b != answer).Distinct().ToArray());
            Assert.True(Deduction.Fits(answer, guesses));
            Assert.Null(Deduction.Violation(answer, guesses));
            foreach (var b in pool.Where(b => guesses.All(g => g.Guess.Id != b.Id)))
                Assert.Equal(Deduction.Fits(b, guesses), Deduction.Violation(b, guesses) is null);
        }
    }

    [Theory]
    [InlineData("type", "Type can't be Attack.")]
    [InlineData("heavy", "Weight must be more than 1.5 g below 45.0 g.")]
    [InlineData("close", "Weight must be above 29.0 g, within 1.5 g of it.")]
    [InlineData("ratchet", "Ratchet must share its blade count or height with 3-80, but not be 3-80.")]
    [InlineData("bit", "Bit must be a Stamina bit other than Ball.")]
    [InlineData("year", "Year must be after 2023.")]
    [InlineData("ownerless", "Must be a blade with an anime owner.")]
    public void ViolationsNameTheBrokenHint(string guessKind, string expected)
    {
        var answer = Make("answer", type: "Stamina", weight: 30, ratchet: "3-60", bit: "Orb", bitType: "Stamina", year: 2024);
        var guess = guessKind switch
        {
            "type" => Make("g", type: "Attack", weight: 30, ratchet: "3-60", bit: "Orb", bitType: "Stamina", year: 2024),
            "heavy" => Make("g", type: "Stamina", weight: 45, ratchet: "3-60", bit: "Orb", bitType: "Stamina", year: 2024),
            "close" => Make("g", type: "Stamina", weight: 29, ratchet: "3-60", bit: "Orb", bitType: "Stamina", year: 2024),
            "ratchet" => Make("g", type: "Stamina", weight: 30, ratchet: "3-80", bit: "Orb", bitType: "Stamina", year: 2024),
            "bit" => Make("g", type: "Stamina", weight: 30, ratchet: "3-60", bit: "Ball", bitType: "Stamina", year: 2024),
            "year" => Make("g", type: "Stamina", weight: 30, ratchet: "3-60", bit: "Orb", bitType: "Stamina", year: 2023),
            _ => Make("g", type: "Stamina", weight: 30, ratchet: "3-60", bit: "Orb", bitType: "Stamina", year: 2024, owner: null),
        };
        var hints = Guesses(answer, guess);
        // A candidate identical to the guess breaks exactly the hint the guess got wrong.
        var copy = guess with { Id = "copy" };
        Assert.Equal(expected, Deduction.Violation(copy, hints));
    }

    [Fact]
    public void BeydleBotSolvesEveryBladeInAFewGuesses()
    {
        var pool = RealPool();
        var counts = pool.Select(b => Deduction.BotGuesses(pool, b)).ToList();
        Assert.All(counts, c => Assert.InRange(c, 1, 6));
        Assert.True(counts.Average() < 4.5, $"average {counts.Average():0.00}");
        Assert.Equal(counts, pool.Select(b => Deduction.BotGuesses(pool, b)).ToList()); // deterministic
    }

    [Fact]
    public void BeydleBotHandlesBladesItCannotTellApart()
    {
        var a = Make("a");
        var b = Make("b");
        var c = Make("c");
        IReadOnlyList<Blade> pool = [a, b, c];
        Assert.Equal([1, 2, 3], pool.Select(x => Deduction.BotGuesses(pool, x)).ToArray());
    }

    internal static IReadOnlyList<Blade> RealPool()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Beydle.csproj"))) dir = dir.Parent;
        var json = new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web);
        return System.Text.Json.JsonSerializer.Deserialize<List<Blade>>(File.ReadAllText(Path.Combine(dir!.FullName, "wwwroot", "data", "blades.json")), json)!;
    }
}
