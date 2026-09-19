using System.Text.Json;
using Microsoft.JSInterop;

namespace Beydle.Services;

public sealed class SavedState
{
    public string Day { get; set; } = "";
    public List<string> Guesses { get; set; } = [];
    public List<string> ImageGuesses { get; set; } = [];
    public Stats Stats { get; set; } = new();
    /// <summary>Achievement id to the day (yyyy-MM-dd) it was unlocked.</summary>
    public Dictionary<string, string> Achievements { get; set; } = [];
    /// <summary>Every blade id ever guessed in daily mode, for the Encyclopedia achievement.</summary>
    public HashSet<string> Seen { get; set; } = [];
}

public sealed class Stats
{
    public int Played { get; set; }
    public int Won { get; set; }
    public int TotalWinGuesses { get; set; }
    public int Streak { get; set; }
    public int MaxStreak { get; set; }
    public string? LastPlayedDay { get; set; }
    public string? LastWinDay { get; set; }
    public Dictionary<string, int> Distribution { get; set; } = [];
}

public sealed class StorageService(IJSRuntime js)
{
    private const string Key = "beydle:v1";
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    public async Task<SavedState> LoadAsync()
    {
        try
        {
            var raw = await js.InvokeAsync<string?>("localStorage.getItem", Key);
            return Parse(raw);
        }
        catch (JSException)
        {
            return new SavedState();
        }
    }

    /// <summary>Malformed or foreign data must never block startup; it is replaced by a fresh state.</summary>
    public static SavedState Parse(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return new SavedState();
        try
        {
            var state = JsonSerializer.Deserialize<SavedState>(raw, Options) ?? new SavedState();
            state.Guesses ??= [];
            state.ImageGuesses ??= [];
            state.Stats ??= new Stats();
            state.Stats.Distribution ??= [];
            state.Achievements ??= [];
            state.Seen ??= [];
            return state;
        }
        catch (JsonException)
        {
            return new SavedState();
        }
    }

    public async Task SaveAsync(SavedState state)
    {
        try { await js.InvokeVoidAsync("localStorage.setItem", Key, JsonSerializer.Serialize(state, Options)); }
        catch (JSException) { /* storage blocked (private mode); the game still works for this session */ }
    }

    public async Task<bool> CopyToClipboardAsync(string text)
    {
        try { await js.InvokeVoidAsync("navigator.clipboard.writeText", text); return true; }
        catch (JSException) { return false; }
    }
}
