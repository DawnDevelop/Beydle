using Beydle.Models;

namespace Beydle.Services;

/// <summary>Daily-mode statistics. "Played" counts days with at least one guess; "Won" counts solved round 1s.</summary>
public static class StatsTracker
{
    public static void RecordFirstGuess(Stats s, string day)
    {
        if (s.LastPlayedDay == day) return;
        s.LastPlayedDay = day;
        s.Played++;
    }

    public static void RecordWin(Stats s, DateOnly day, int guessCount)
    {
        var key = DailyPicker.DayKey(day);
        if (s.LastWinDay == key) return;
        var yesterday = DailyPicker.DayKey(day.AddDays(-1));
        s.Streak = s.LastWinDay == yesterday ? s.Streak + 1 : 1;
        s.MaxStreak = Math.Max(s.MaxStreak, s.Streak);
        s.LastWinDay = key;
        s.Won++;
        s.TotalWinGuesses += guessCount;
        var bucket = guessCount <= 6 ? guessCount.ToString() : "7+";
        s.Distribution[bucket] = s.Distribution.GetValueOrDefault(bucket) + 1;
    }

    /// <summary>The streak shown to the player: zero once a day has been missed.</summary>
    public static int CurrentStreak(Stats s, DateOnly today)
    {
        if (s.LastWinDay is null) return 0;
        var yesterday = DailyPicker.DayKey(today.AddDays(-1));
        var key = DailyPicker.DayKey(today);
        return s.LastWinDay == key || s.LastWinDay == yesterday ? s.Streak : 0;
    }

    /// <summary>Mean round-1 guesses per solved day, one decimal; 0 before the first win.</summary>
    public static double AverageGuesses(Stats s) => s.Won == 0 ? 0 : Math.Round((double)s.TotalWinGuesses / s.Won, 1);
}
