using Beydle.Models;

namespace Beydle.Services;

/// <summary>Round 1: guess the blade from property hints.</summary>
public sealed class BladeRound(Blade answer)
{
    public Blade Answer { get; } = answer;
    public List<GuessResult> Guesses { get; } = [];
    public bool Won { get; private set; }

    public GuessResult Submit(Blade guess)
    {
        var r = GuessResult.Compare(guess, Answer);
        Guesses.Add(r);
        if (r.IsCorrect) Won = true;
        return r;
    }
}

/// <summary>Round 2: name the blade in a picture that sharpens with every miss.</summary>
public sealed class ImageRound(Blade answer)
{
    public const int MaxStage = 7;

    public Blade Answer { get; } = answer;
    public List<Blade> Guesses { get; } = [];
    public bool Won { get; private set; }

    /// <summary>0 = silhouette; each wrong guess sharpens the image up to <see cref="MaxStage"/>.</summary>
    public int Stage => Math.Min(Guesses.Count, MaxStage);

    public bool Submit(Blade guess)
    {
        Guesses.Add(guess);
        if (guess.Id == Answer.Id) Won = true;
        return Won;
    }
}

/// <summary>One two-round game. The active round is round 1 until it is won, then round 2 until it is won.</summary>
public sealed class GameSession(Blade bladeAnswer, Blade imageAnswer)
{
    public BladeRound Blade { get; } = new(bladeAnswer);
    public ImageRound Image { get; } = new(imageAnswer);

    /// <summary>
    /// Xtreme mode: every round-1 guess must fit all hints so far. It can only be switched on before the first
    /// guess, so while it is on, the whole of round 1 was played that way.
    /// </summary>
    public bool Extreme { get; set; }

    public bool Complete => Image.Won;
    public bool InImageRound => Blade.Won && !Image.Won;

    /// <summary>Ids already tried in the active round; excluded from suggestions.</summary>
    public IEnumerable<string> GuessedIds => Blade.Won ? Image.Guesses.Select(b => b.Id) : Blade.Guesses.Select(g => g.Guess.Id);

    /// <summary>Why Xtreme mode refuses this round-1 guess, or null when it is allowed.</summary>
    public string? Rejects(Blade guess) => Extreme && !Blade.Won ? Deduction.Violation(guess, Blade.Guesses) : null;

    /// <summary>Feeds a guess to the active round. Returns false when the game is complete or Xtreme mode refuses it.</summary>
    public bool Submit(Blade guess)
    {
        if (Rejects(guess) is not null) return false;
        if (!Blade.Won) { Blade.Submit(guess); return true; }
        if (!Image.Won) { Image.Submit(guess); return true; }
        return false;
    }

    /// <summary>Replays saved guesses. Unknown ids (e.g. a blade removed from the catalogue) are skipped.</summary>
    public void Restore(IReadOnlyList<Blade> pool, IEnumerable<string> bladeGuessIds, IEnumerable<string> imageGuessIds)
    {
        foreach (var id in bladeGuessIds)
        {
            if (Blade.Won) break;
            var b = pool.FirstOrDefault(x => x.Id == id);
            if (b is not null) Blade.Submit(b);
        }
        foreach (var id in imageGuessIds)
        {
            if (Image.Won) break;
            var b = pool.FirstOrDefault(x => x.Id == id);
            if (b is not null) Image.Submit(b);
        }
    }

    public string ShareText(string title, string url)
    {
        var xtreme = Blade.Guesses.Count == 1 ? " ⚡" : "";
        var extreme = Extreme ? "*" : "";
        var lines = new List<string> { $"{title} – Blade {Blade.Guesses.Count}/∞{extreme} · Image {Image.Guesses.Count}/∞{xtreme}" };
        lines.AddRange(Blade.Guesses.Select(g => string.Concat(g.Cells.Select(c => c.Emoji))));
        lines.Add("🖼 " + string.Concat(Image.Guesses.Select(b => b.Id == Image.Answer.Id ? "🟩" : "⬛")));
        lines.Add(url);
        return string.Join("\n", lines);
    }
}

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
        var key = day.ToString("yyyy-MM-dd");
        if (s.LastWinDay == key) return;
        var yesterday = day.AddDays(-1).ToString("yyyy-MM-dd");
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
        var yesterday = today.AddDays(-1).ToString("yyyy-MM-dd");
        var key = today.ToString("yyyy-MM-dd");
        return s.LastWinDay == key || s.LastWinDay == yesterday ? s.Streak : 0;
    }

    /// <summary>Mean round-1 guesses per solved day, one decimal; 0 before the first win.</summary>
    public static double AverageGuesses(Stats s) => s.Won == 0 ? 0 : Math.Round((double)s.TotalWinGuesses / s.Won, 1);
}
