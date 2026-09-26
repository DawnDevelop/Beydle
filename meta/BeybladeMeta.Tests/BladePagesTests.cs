using BeybladeMeta.Indexer;

namespace BeybladeMeta.Tests;

public class BladePagesTests
{
    private static IEnumerable<string> Times(string blade, int n) => Enumerable.Repeat(blade, n);

    private static readonly BladePages.GameBlade[] Game =
    [
        new("Shark Scale", [], "UX", "img/shark.webp"),
        new("Heavens Ring", ["Ring Aether"], "BX", "img/ring.webp"),
        new("Leon Fang", [], "CX", "img/leon.webp"),
    ];

    [Fact]
    public void Only_blades_with_enough_finishes_get_a_page()
    {
        var pages = BladePages.From([.. Times("SharkScale", 5), .. Times("WizardRod M-85Under", 4)], Game);

        var page = Assert.Single(pages);
        Assert.Equal(new BladePages.Page("sharkscale", "SharkScale", "img/shark.webp"), page);
    }

    [Theory]
    [InlineData("SharkScale", "sharkscale")]
    [InlineData("Bite Croc", "bitecroc")]
    [InlineData("Dran-Sword 2", "dransword2")]
    public void Slug_keeps_lowercased_letters_and_digits(string name, string slug) =>
        Assert.Equal(slug, BladePages.Slug(name));

    [Fact]
    public void Names_sharing_a_slug_get_one_page_sorted_by_slug()
    {
        var pages = BladePages.From([.. Times("Wizard Rod", 5), .. Times("WizardRod", 5), .. Times("AeroPegasus", 5)], Game);

        Assert.Equal(["aeropegasus", "wizardrod"], pages.Select(p => p.Slug));
        Assert.Equal("Wizard Rod", pages[1].Name); // ordinal: the space sorts first
    }

    [Theory]
    [InlineData("sharkscale", "img/shark.webp")]
    [InlineData("ringaether", "img/ring.webp")] // by alias
    [InlineData("leonfang", null)] // CX: the meta name is only the main blade
    [InlineData("blast", null)] // not in the game
    public void Pictures_come_from_the_game_blade_with_that_name_or_alias_but_never_a_CX_blade(string slug, string? image) =>
        Assert.Equal(image, BladePages.ImageFor(slug, Game));
}
