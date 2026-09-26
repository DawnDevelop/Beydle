using Beydle.Models;
using Beydle.Services;

namespace Beydle.Tests;

public class MetaLeaderboardTests
{
    private static MetaAppearance A(string blade, int placement, string date, int? deck = null, string bit = "Rush", string ratchet = "1-60") =>
        new(blade, $"{blade} {ratchet}{bit}", ratchet, bit, placement, date, deck);

    [Fact]
    public void ScoresThreeTwoOneAndBreaksTiesByTotalThenName()
    {
        var board = new MetaLeaderboard([
            A("Wizard", 1, "2026-09-01"), // 3
            A("Shark", 2, "2026-09-01"), A("Shark", 3, "2026-09-01"), // 3, but two finishes
            A("Cobalt", 1, "2026-09-01"), // 3, same as Wizard
            A("Aero", 3, "2026-09-01"),
        ]);

        var rows = board.Rank(MetaView.Blades, "all");

        Assert.Equal(["Shark", "Cobalt", "Wizard", "Aero"], rows.Select(r => r.Name));
        Assert.Equal([1, 2, 3, 4], rows.Select(r => r.Rank));
        Assert.Equal((0, 1, 1, 2, 3), (rows[0].First, rows[0].Second, rows[0].Third, rows[0].Total, rows[0].Score));
        Assert.All(rows, r => Assert.Null(r.Delta)); // all time has no previous period
        Assert.All(rows, r => Assert.False(r.IsNew));
    }

    [Fact]
    public void PeriodsEndAtTheNewestEventAndMovementComparesWithThePeriodBefore()
    {
        var board = new MetaLeaderboard([
            // Previous week (Aug 26 - Sep 1): Shark first, Wizard second.
            A("Shark", 1, "2026-08-28"), A("Shark", 1, "2026-08-29"),
            A("Wizard", 1, "2026-08-30"),
            // Last week (from Sep 2): Wizard climbs, Aero is new. Far older data counts in neither.
            A("Wizard", 1, "2026-09-05"), A("Wizard", 1, "2026-09-09"),
            A("Shark", 2, "2026-09-06"),
            A("Aero", 3, "2026-09-07"),
            A("Old", 1, "2026-01-01"),
        ]);

        Assert.Equal(new DateOnly(2026, 9, 9), board.Anchor);
        var rows = board.Rank(MetaView.Blades, "1w");

        Assert.Equal(["Wizard", "Shark", "Aero"], rows.Select(r => r.Name));
        Assert.Equal(1, rows[0].Delta);
        Assert.Equal(-1, rows[1].Delta);
        Assert.True(rows[2].IsNew);
        Assert.Null(rows[2].Delta);
    }

    [Fact]
    public void DecksAreCompleteThreeBladeLineUpsWithSortedNames()
    {
        var board = new MetaLeaderboard([
            A("Wizard", 1, "2026-09-01", deck: 0), A("Shark", 1, "2026-09-01", deck: 0), A("Aero", 1, "2026-09-01", deck: 0),
            A("Shark", 2, "2026-09-01", deck: 1), A("Aero", 2, "2026-09-01", deck: 1), A("Wizard", 2, "2026-09-01", deck: 1, bit: "Hexa"),
            A("Shark", 3, "2026-09-01", deck: 2), A("Aero", 3, "2026-09-01", deck: 2), // two combos: not a full deck
        ]);

        var deck = Assert.Single(board.Rank(MetaView.Decks, "all"));
        Assert.Equal("Aero + Shark + Wizard", deck.Name);
        Assert.Equal((1, 1, 0), (deck.First, deck.Second, deck.Third));

        var lineUps = board.CombosFor(MetaView.Decks, "all", deck.Name);
        Assert.Equal(["Aero 1-60Rush + Shark 1-60Rush + Wizard 1-60Rush", "Aero 1-60Rush + Shark 1-60Rush + Wizard 1-60Hexa"], lineUps.Select(r => r.Name));
    }

    [Fact]
    public void CombosForAPartListsTheCombosUsingItBestFirst()
    {
        var board = new MetaLeaderboard([
            A("Wizard", 3, "2026-09-01", bit: "Rush"),
            A("Wizard", 1, "2026-09-01", bit: "Hexa"),
            A("Shark", 1, "2026-09-01", bit: "Hexa"),
        ]);

        Assert.Equal(["Wizard 1-60Hexa", "Wizard 1-60Rush"], board.CombosFor(MetaView.Blades, "all", "Wizard").Select(r => r.Name));
        Assert.Equal(["Shark 1-60Hexa", "Wizard 1-60Hexa"], board.CombosFor(MetaView.Bits, "all", "Hexa").Select(r => r.Name));
    }

    [Fact]
    public void UndatedDataIsRankedAsAllTime()
    {
        var board = new MetaLeaderboard([A("Wizard", 1, null!), A("Shark", 2, "not a date")]);

        Assert.Null(board.Anchor);
        Assert.Equal(["Wizard", "Shark"], board.Rank(MetaView.Blades, MetaLeaderboard.DefaultWindow).Select(r => r.Name));
    }
}
