using System.Text.Json;

namespace BeybladeMeta.Indexer;

/// <summary>
/// The blades that get their own page at beydle.com/meta/{slug}, written next to appearances.json as blade-pages.json.
/// The site links to these pages and shows their pictures, the Worker titles them and answers 404 for any other slug,
/// and tools/build-og-cards.js draws their link previews. Blades with fewer than <see cref="MinFinishes"/> top-3
/// finishes are left out: most are parsing noise ("WizardRod M-85Under") or one-offs that would make empty pages.
/// </summary>
public static class BladePages
{
    public const int MinFinishes = 5;
    public const string FileName = "blade-pages.json";

    /// <summary>The game's blade pool (Beydle's wwwroot/data/blades.json), for the pictures. Override with INDEXER_GAME_BLADES.</summary>
    public static string GameBladesPath => Environment.GetEnvironmentVariable("INDEXER_GAME_BLADES") ?? "wwwroot/data/blades.json";

    /// <param name="Image">The render of the matching game blade (path under wwwroot); null for CX blades and blades the game does not have.</param>
    public sealed record Page(string Slug, string Name, string? Image);

    /// <summary>The fields of a game blade that matching needs.</summary>
    public sealed record GameBlade(string Name, string[] Aliases, string Line, string Image);

    public static IReadOnlyList<Page> From(IEnumerable<string> blades, IReadOnlyList<GameBlade> game) =>
        blades.GroupBy(b => b)
            .Where(g => g.Count() >= MinFinishes)
            .Select(g => (Slug: Slug(g.Key), Name: g.Key))
            .Where(p => p.Slug.Length > 0)
            .GroupBy(p => p.Slug)
            .Select(g => g.OrderBy(p => p.Name, StringComparer.Ordinal).First()) // two names, one slug: keep one page
            .OrderBy(p => p.Slug, StringComparer.Ordinal)
            .Select(p => new Page(p.Slug, p.Name, ImageFor(p.Slug, game)))
            .ToList();

    /// <summary>"SharkScale" → "sharkscale": ASCII letters and digits, lowercased.</summary>
    public static string Slug(string name) => string.Concat(name.Where(char.IsAsciiLetterOrDigit)).ToLowerInvariant();

    /// <summary>
    /// The render of the game blade whose name or alias has this slug ("sharkscale" is Shark Scale). CX blades never
    /// match: the meta names only their main blade ("Blast"), which does not identify one game blade.
    /// </summary>
    public static string? ImageFor(string slug, IEnumerable<GameBlade> game) =>
        game.FirstOrDefault(b => b.Line != "CX" && b.Aliases.Prepend(b.Name).Any(n => Slug(n) == slug))?.Image;

    public static void Write(string dir, IEnumerable<string> blades, JsonSerializerOptions options)
    {
        var game = JsonSerializer.Deserialize<List<GameBlade>>(File.ReadAllText(GameBladesPath), new JsonSerializerOptions(JsonSerializerDefaults.Web)) ?? [];
        File.WriteAllText(Path.Combine(dir, FileName), JsonSerializer.Serialize(From(blades, game), options));
    }
}
