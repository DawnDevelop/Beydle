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
    public bool XtremeMode { get; set; }

    /// <summary>Xtreme mode goes on only before the first guess, and off at any time until round 1 is solved.</summary>
    public bool XtremeModeLocked => Blade.Won || (!XtremeMode && Blade.Guesses.Count > 0);

    /// <summary>Round 1 solved with the very first guess.</summary>
    public bool XtremeFinish => Blade.Won && Blade.Guesses.Count == 1;

    public bool Complete => Image.Won;
    public bool InImageRound => Blade.Won && !Image.Won;

    /// <summary>Ids already tried in the active round; excluded from suggestions.</summary>
    public IEnumerable<string> GuessedIds => Blade.Won ? Image.Guesses.Select(b => b.Id) : Blade.Guesses.Select(g => g.Guess.Id);

    /// <summary>Why Xtreme mode refuses this round-1 guess, or null when it is allowed.</summary>
    public string? Rejects(Blade guess) => XtremeMode && !Blade.Won ? Deduction.Violation(guess, Blade.Guesses) : null;

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
        var finish = XtremeFinish ? " ⚡" : "";
        var mode = XtremeMode ? "*" : "";
        var lines = new List<string> { $"{title} – Blade {Blade.Guesses.Count}/∞{mode} · Image {Image.Guesses.Count}/∞{finish}" };
        lines.AddRange(Blade.Guesses.Select(g => string.Concat(g.Cells.Select(c => c.Emoji))));
        lines.Add("🖼 " + string.Concat(Image.Guesses.Select(b => b.Id == Image.Answer.Id ? "🟩" : "⬛")));
        lines.Add(url);
        return string.Join("\n", lines);
    }

    /// <summary>The result image's data. <paramref name="left"/> is the blades still possible after each round-1 guess.</summary>
    public ShareCard ToShareCard(string title, string site, int[] left) => new(
        Title: title,
        Summary: $"Blade: {Text.Count(Blade.Guesses.Count, "guess", "guesses")} · Image: {Text.Count(Image.Guesses.Count, "guess", "guesses")}{(XtremeFinish ? " · ⚡ Xtreme Finish" : "")}",
        XtremeMode: XtremeMode,
        Columns: GuessResult.Labels.Length,
        Rows: Blade.Guesses.Select(r => r.Cells.Select(c => (int)c.Hit).ToArray()).ToArray(),
        Left: Blade.Guesses.Select((r, i) => r.IsCorrect ? "" : $"{left[i]} left").ToArray(),
        Picture: Image.Guesses.Select(b => b.Id == Image.Answer.Id).ToArray(),
        Site: site);
}
