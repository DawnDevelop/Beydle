using System.Text.Json.Serialization;

namespace Beydle.Models;

/// <summary>Everything kept in the browser's local storage (see StorageService).</summary>
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
    /// <summary>The player's Xtreme-mode preference, applied to each game that has no round-1 guess yet.</summary>
    [JsonPropertyName("extremeMode")] // stored name predates the rename; keep it so players keep their preference
    public bool XtremeMode { get; set; }
    /// <summary>Whether <see cref="Day"/>'s daily game is played in Xtreme mode.</summary>
    [JsonPropertyName("dayExtreme")]
    public bool DayXtreme { get; set; }
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
