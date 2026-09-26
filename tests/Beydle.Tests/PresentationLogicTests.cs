using System.Text.Json;
using Beydle.Models;
using Beydle.Services;

namespace Beydle.Tests;

/// <summary>Logic that used to live in Home.razor: labels, Xtreme-mode rules, sharing and the round summary.</summary>
public class PresentationLogicTests
{
    private static Blade Make(string id, string type = "Attack", string ratchet = "3-60") =>
        new(id, $"Blade {id}", [], "BX-01", "BX", type, "Right", 30, 50, 20, 20, ratchet, "Flat", "F", "Attack", $"img/{id}.webp", 2023, null);

    [Fact]
    public void StockComboLeavesOutAnIntegratedRatchet()
    {
        Assert.Equal("Blade a 3-60F", Make("a").StockCombo);
        Assert.Equal("Blade b F", Make("b", ratchet: "INT").StockCombo);
    }

    [Theory]
    [InlineData(Hit.Exact, Direction.None, "exact")]
    [InlineData(Hit.Partial, Direction.None, "close")]
    [InlineData(Hit.Partial, Direction.Higher, "close, answer is higher")]
    [InlineData(Hit.Miss, Direction.Lower, "answer is lower")]
    [InlineData(Hit.Miss, Direction.None, "no match")]
    public void CellDescribesItsHint(Hit hit, Direction direction, string expected) =>
        Assert.Equal(expected, new Cell("Weight", "30.0 g", hit, direction).Description);

    [Fact]
    public void XtremeModeGoesOnOnlyBeforeTheFirstGuessAndOffUntilRoundOneIsSolved()
    {
        var a = Make("a");
        var b = Make("b", "Stamina");
        var off = new GameSession(a, b);
        Assert.False(off.XtremeModeLocked);
        off.Submit(b);
        Assert.True(off.XtremeModeLocked);

        var on = new GameSession(a, b) { XtremeMode = true };
        on.Submit(b);
        Assert.False(on.XtremeModeLocked);
        on.Submit(a);
        Assert.True(on.XtremeModeLocked);
    }

    [Fact]
    public void ShareCardDescribesTheFinishedGame()
    {
        var a = Make("a");
        var b = Make("b", "Stamina");
        var g = new GameSession(a, b);
        g.Submit(a);
        g.Submit(a);
        g.Submit(b);

        var card = g.ToShareCard("Beydle #1", "beydle.com", [1]);
        Assert.Equal("Blade: 1 guess · Image: 2 guesses · ⚡ Xtreme Finish", card.Summary);
        Assert.Equal(GuessResult.Labels.Length, card.Columns);
        Assert.Equal([""], card.Left);
        Assert.Equal([false, true], card.Picture);
        Assert.Equal("beydle.com", card.Site);
    }

    [Fact]
    public void SavedXtremeSettingsKeepTheirStoredNames()
    {
        var json = JsonSerializer.Serialize(new SavedState { XtremeMode = true, DayXtreme = true }, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.Contains("\"extremeMode\":true", json);
        Assert.Contains("\"dayExtreme\":true", json);

        var loaded = StorageService.Parse("""{"day":"2026-09-26","extremeMode":true,"dayExtreme":true}""");
        Assert.True(loaded.XtremeMode);
        Assert.True(loaded.DayXtreme);
    }

    [Fact]
    public void RoundSummaryNamesTheSharpestOfSeveralWrongGuesses()
    {
        var pool = new[] { Make("a"), Make("b", "Stamina"), Make("c", "Defense"), Make("d", "Balance") };
        var answer = pool[0];
        var guesses = new[] { pool[1], pool[2], answer }.Select(g => GuessResult.Compare(g, answer)).ToList();

        var text = RoundSummary.Describe(pool, guesses, [3, 1, 1], answer);
        Assert.StartsWith("🤖 Beydle Bot needed ", text);
        Assert.EndsWith(" · sharpest guess: Blade c, 3 → 1", text);
    }

    [Fact]
    public void RoundSummaryHasNoSharpestGuessWithOnlyOneMiss()
    {
        var pool = new[] { Make("a"), Make("b", "Stamina"), Make("c", "Defense") };
        var answer = pool[0];
        var guesses = new[] { pool[1], answer }.Select(g => GuessResult.Compare(g, answer)).ToList();
        Assert.DoesNotContain("sharpest", RoundSummary.Describe(pool, guesses, [1, 1], answer));
    }
}
