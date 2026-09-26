using System.Net.Http.Json;
using Beydle.Models;

namespace Beydle.Services;

/// <summary>Loads the tournament data once, so moving between the meta pages does not download and parse it again.</summary>
public sealed class MetaService(HttpClient http)
{
    /// <summary>The WBO forum thread every result comes from.</summary>
    public const string SourceUrl = "https://worldbeyblade.org/Thread-Winning-Combinations-at-WBO-Organized-Events-Beyblade-X-BBX";

    private bool loaded;

    /// <summary>Set once <see cref="LoadAsync"/> has succeeded.</summary>
    public MetaLeaderboard Board { get; private set; } = null!;
    public IReadOnlyList<MetaBladePage> Pages { get; private set; } = [];

    /// <summary>Downloads the results and the list of blade pages the first time; false when they could not be loaded.</summary>
    public async Task<bool> LoadAsync()
    {
        if (loaded) return true;
        try
        {
            var appearances = http.GetFromJsonAsync<List<MetaAppearance>>("data/meta/appearances.json");
            var pages = http.GetFromJsonAsync<List<MetaBladePage>>("data/meta/blade-pages.json");
            Board = new MetaLeaderboard(await appearances ?? []);
            Pages = await pages ?? [];
            return loaded = true;
        }
        catch (HttpRequestException)
        {
            return false;
        }
    }
}
