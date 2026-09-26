using System.Text.Json;
using Beydle.Models;
using Microsoft.JSInterop;

namespace Beydle.Services;

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
        if (string.IsNullOrWhiteSpace(raw))
            return new SavedState();
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
        try
        { await js.InvokeVoidAsync("localStorage.setItem", Key, JsonSerializer.Serialize(state, Options)); }
        catch (JSException) { /* storage blocked (private mode); the game still works for this session */ }
    }
}
