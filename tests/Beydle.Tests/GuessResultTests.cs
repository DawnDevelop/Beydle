using Beydle.Models;

namespace Beydle.Tests;

public class GuessResultTests
{
    private static readonly Blade DranSword = new("dran-sword", "Dran Sword", ["Sword Dran"], "BX-01", "BX", "Attack", "Right", 34.9, 55, 25, 20, "3-60", "Flat", "F", "Attack", "", 2023, "Ekusu Kurosu");
    private static readonly Blade WizardArrow = new("wizard-arrow", "Wizard Arrow", [], "BX-03", "BX", "Stamina", "Right", 31.8, 20, 30, 60, "4-80", "Ball", "B", "Stamina", "", 2023, "Multi Nanairo");
    private static readonly Blade SharkEdge = new("shark-edge", "Shark Edge", [], "BX-14", "BX", "Attack", "Right", 34.5, 55, 25, 20, "3-60", "Low Flat", "LF", "Attack", "", 2023, "Meiko Myoden");
    private static readonly Blade Griffon = new("bullet-griffon", "Bullet Griffon", [], "UX-19", "UX", "Balance", "Right", 38.5, 45, 40, 35, "INT", "Hexa", "H", "Defense", "", 2026, null);

    private static Cell Cell(GuessResult r, string label) => r.Cells.Single(c => c.Label == label);

    [Fact]
    public void GuessingTheAnswerIsAllGreen()
    {
        var r = GuessResult.Compare(DranSword, DranSword);
        Assert.True(r.IsCorrect);
        Assert.All(r.Cells, c => Assert.Equal(Hit.Exact, c.Hit));
        Assert.All(r.Cells, c => Assert.Equal(Direction.None, c.Direction));
    }

    [Fact]
    public void ArrowsPointTowardTheAnswer()
    {
        var r = GuessResult.Compare(WizardArrow, DranSword);
        Assert.False(r.IsCorrect);
        Assert.Equal(Direction.Higher, Cell(r, "Weight").Direction);   // 31.8 -> 34.9
        Assert.Equal(Hit.Miss, Cell(r, "Type").Hit);
        Assert.Equal(Hit.Exact, Cell(r, "Spin").Hit);
    }

    [Fact]
    public void CloseNumbersArePartial()
    {
        var r = GuessResult.Compare(SharkEdge, DranSword);
        Assert.Equal(Hit.Partial, Cell(r, "Weight").Hit); // 0.4 g apart
    }

    [Fact]
    public void LineIsExactOrMiss()
    {
        Assert.Equal(Hit.Exact, Cell(GuessResult.Compare(WizardArrow, DranSword), "Line").Hit); // BX vs BX
        var other = Cell(GuessResult.Compare(Griffon, DranSword), "Line");                       // UX vs BX
        Assert.Equal(Hit.Miss, other.Hit);
        Assert.Equal(Direction.None, other.Direction);
        Assert.Equal("UX", other.Value);
    }

    [Fact]
    public void RatchetSharingHeightOrCountIsPartial()
    {
        var wizardRod = WizardArrow with { Ratchet = "5-70" };
        Assert.Equal(Hit.Miss, Cell(GuessResult.Compare(wizardRod, DranSword), "Ratchet").Hit);          // 5-70 vs 3-60
        Assert.Equal(Hit.Partial, Cell(GuessResult.Compare(WizardArrow with { Ratchet = "3-80" }, DranSword), "Ratchet").Hit); // same count
        Assert.Equal(Hit.Partial, Cell(GuessResult.Compare(WizardArrow with { Ratchet = "4-60" }, DranSword), "Ratchet").Hit); // same height
    }

    [Fact]
    public void IntegratedRatchetOnlyMatchesIntegrated()
    {
        Assert.Equal(Hit.Miss, Cell(GuessResult.Compare(Griffon, DranSword), "Ratchet").Hit);
        Assert.Equal(Hit.Exact, Cell(GuessResult.Compare(Griffon, Griffon with { Id = "x" }), "Ratchet").Hit);
        Assert.Equal("Integrated", Cell(GuessResult.Compare(Griffon, DranSword), "Ratchet").Value);
    }

    [Fact]
    public void BitOfSameTypeIsPartial()
    {
        Assert.Equal(Hit.Partial, Cell(GuessResult.Compare(SharkEdge, DranSword), "Bit").Hit); // Low Flat vs Flat, both Attack
        Assert.Equal(Hit.Miss, Cell(GuessResult.Compare(WizardArrow, DranSword), "Bit").Hit);  // Ball (Stamina) vs Flat (Attack)
    }

    [Fact]
    public void YearIsExactOrArrowNeverPartial()
    {
        Assert.Equal(Hit.Exact, Cell(GuessResult.Compare(WizardArrow, DranSword), "Year").Hit);
        var later = Cell(GuessResult.Compare(Griffon, DranSword), "Year");
        Assert.Equal(Hit.Miss, later.Hit);
        Assert.Equal(Direction.Lower, later.Direction); // 2026 -> 2023
        Assert.Equal("2026", later.Value);
    }

    [Fact]
    public void OwnerIsExactOrMissAndNullShowsAsNone()
    {
        Assert.Equal(Hit.Miss, Cell(GuessResult.Compare(WizardArrow, DranSword), "Owner").Hit);
        Assert.Equal(Hit.Exact, Cell(GuessResult.Compare(DranSword, DranSword with { Id = "x" }), "Owner").Hit);
        var none = Cell(GuessResult.Compare(Griffon, DranSword), "Owner");
        Assert.Equal("None", none.Value);
        Assert.Equal(Hit.Miss, none.Hit);
    }

    [Fact]
    public void RowHasOneCellPerLabelInOrder()
    {
        var r = GuessResult.Compare(WizardArrow, DranSword);
        Assert.Equal(GuessResult.Labels, r.Cells.Select(c => c.Label).ToArray());
    }

    [Fact]
    public void NameMatchingCoversAliasesCaseInsensitively()
    {
        Assert.True(DranSword.Matches("sword dran"));
        Assert.True(DranSword.Matches("DRAN"));
        Assert.False(DranSword.Matches("wizard"));
    }
}
