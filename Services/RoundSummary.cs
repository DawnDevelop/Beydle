using Beydle.Models;

namespace Beydle.Services;

/// <summary>The line under a solved round 1: how Beydle Bot did, and which wrong guess narrowed the field the most.</summary>
public static class RoundSummary
{
    /// <param name="left">Blades still possible after each guess, as from <see cref="Deduction.RemainingAfterEach"/>.</param>
    public static string Describe(IReadOnlyList<Blade> pool, IReadOnlyList<GuessResult> guesses, int[] left, Blade answer)
    {
        var bot = Deduction.BotGuesses(pool, answer);
        var verdict = guesses.Count < bot ? ", you beat it" : guesses.Count == bot ? ", you matched it" : "";
        var text = $"🤖 Beydle Bot needed {Text.Count(bot, "guess", "guesses")}{verdict}";
        int? best = null;
        var bestRatio = 1.0;
        for (var i = 0; i < guesses.Count; i++)
        {
            if (guesses[i].IsCorrect) continue;
            var ratio = (double)left[i] / (i == 0 ? pool.Count : left[i - 1]);
            if (ratio < bestRatio) (best, bestRatio) = (i, ratio);
        }
        // With a single wrong guess there is nothing to compare it with.
        if (best is int b && guesses.Count(g => !g.IsCorrect) >= 2) text += $" · sharpest guess: {guesses[b].Guess.Name}, {(b == 0 ? pool.Count : left[b - 1])} → {left[b]}";
        return text;
    }
}
