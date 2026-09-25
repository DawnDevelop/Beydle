using System.Runtime.CompilerServices;
using Beydle.Models;

namespace Beydle.Services;

/// <summary>
/// What round-1 hints prove. A blade is still possible when it would have produced exactly the colours and
/// arrows every guess so far got. Drives the "still possible" counter, Xtreme mode and Beydle Bot.
/// </summary>
public static class Deduction
{
    /// <summary>True when <paramref name="candidate"/> could still be the answer given these guesses.</summary>
    public static bool Fits(Blade candidate, IEnumerable<GuessResult> guesses) => guesses.All(g => Fits(candidate, g));

    private static bool Fits(Blade candidate, GuessResult g) =>
        g.IsCorrect ? g.Guess.Id == candidate.Id : g.Guess.Id != candidate.Id && SameHints(GuessResult.Compare(g.Guess, candidate), g);

    private static bool SameHints(GuessResult a, GuessResult b)
    {
        for (var i = 0; i < a.Cells.Length; i++)
            if (a.Cells[i].Hit != b.Cells[i].Hit || a.Cells[i].Direction != b.Cells[i].Direction) return false;
        return true;
    }

    /// <summary>How many blades of the pool were still possible after each guess (index i = after guess i).</summary>
    public static int[] RemainingAfterEach(IReadOnlyList<Blade> pool, IReadOnlyList<GuessResult> guesses)
    {
        var possible = pool.ToList();
        var counts = new int[guesses.Count];
        for (var i = 0; i < guesses.Count; i++)
        {
            var g = guesses[i];
            possible.RemoveAll(b => !Fits(b, g));
            counts[i] = possible.Count;
        }
        return counts;
    }

    /// <summary>Xtreme mode: the first hint <paramref name="candidate"/> contradicts, phrased for the player; null when it fits.</summary>
    public static string? Violation(Blade candidate, IEnumerable<GuessResult> guesses)
    {
        foreach (var g in guesses)
        {
            if (g.IsCorrect) continue;
            var would = GuessResult.Compare(g.Guess, candidate);
            for (var i = 0; i < g.Cells.Length; i++)
            {
                var hint = g.Cells[i];
                if (would.Cells[i].Hit != hint.Hit || would.Cells[i].Direction != hint.Direction) return Describe(hint, g.Guess);
            }
        }
        return null;
    }

    /// <summary>The rule one hint cell sets, e.g. "Type must be Attack." or "Weight must be more than 1.5 g below 34.5 g."</summary>
    private static string Describe(Cell hint, Blade guessed)
    {
        var exact = hint.Hit == Hit.Exact;
        var higher = hint.Direction == Direction.Higher;
        switch (hint.Label)
        {
            case "Weight":
                var w = hint.Value;
                if (exact) return $"Weight must be {w}.";
                return hint.Hit == Hit.Partial
                    ? $"Weight must be {(higher ? "above" : "below")} {w}, within 1.5 g of it."
                    : $"Weight must be more than 1.5 g {(higher ? "above" : "below")} {w}.";
            case "Year":
                return exact ? $"Year must be {hint.Value}." : $"Year must be {(higher ? "after" : "before")} {hint.Value}.";
            case "Ratchet":
                if (guessed.HasIntegratedRatchet) return exact ? "Ratchet must be integrated." : "Ratchet can't be integrated.";
                return hint.Hit switch
                {
                    Hit.Exact => $"Ratchet must be {guessed.Ratchet}.",
                    Hit.Partial => $"Ratchet must share its blade count or height with {guessed.Ratchet}, but not be {guessed.Ratchet}.",
                    _ => $"Ratchet can't share its blade count or height with {guessed.Ratchet}.",
                };
            case "Bit":
                return hint.Hit switch
                {
                    Hit.Exact => $"Bit must be {guessed.Bit}.",
                    Hit.Partial => $"Bit must be a {guessed.BitType} bit other than {guessed.Bit}.",
                    _ => $"Bit can't be a {guessed.BitType} bit.",
                };
            case "Owner" when guessed.Owner is null:
                return exact ? "Must be a blade without an anime owner." : "Must be a blade with an anime owner.";
            default:
                return exact ? $"{hint.Label} must be {hint.Value}." : $"{hint.Label} can't be {hint.Value}.";
        }
    }

    /// <summary>
    /// Guesses Beydle Bot needs for <paramref name="answer"/>. The bot is greedy: each turn it plays whichever blade
    /// leaves the smallest expected field (sum of squared hint-group sizes), preferring blades that are still possible.
    /// </summary>
    public static int BotGuesses(IReadOnlyList<Blade> pool, Blade answer)
    {
        var cache = Bots.GetValue(pool, p => new BotCache(BestGuess(p, p.ToList())));
        if (cache.Guesses.TryGetValue(answer.Id, out var known)) return known;
        var possible = pool.ToList();
        var guess = cache.Opener;
        for (var n = 1; ; n++)
        {
            if (guess.Id == answer.Id) return cache.Guesses[answer.Id] = n;
            var hints = GuessResult.Compare(guess, answer);
            possible.RemoveAll(b => !Fits(b, hints));
            guess = BestGuess(pool, possible);
        }
    }

    /// <summary>
    /// Per pool: the opening guess, which depends only on the pool and is the expensive part (a few hundred ms in
    /// the browser), and each answer's result, which achievements and the page both ask for.
    /// </summary>
    private sealed record BotCache(Blade Opener)
    {
        public Dictionary<string, int> Guesses { get; } = [];
    }

    private static readonly ConditionalWeakTable<IReadOnlyList<Blade>, BotCache> Bots = [];

    private static Blade BestGuess(IReadOnlyList<Blade> pool, List<Blade> possible)
    {
        if (possible.Count <= 2) return possible[0];
        var inPossible = possible.Select(b => b.Id).ToHashSet();
        Blade best = possible[0];
        var bestScore = long.MaxValue;
        var bestIsPossible = true;
        foreach (var guess in pool)
        {
            long score = 0;
            foreach (var group in possible.GroupBy(b => HintKey(GuessResult.Compare(guess, b))))
                if (group.Key != Solved) score += (long)group.Count() * group.Count();
            var isPossible = inPossible.Contains(guess.Id);
            if (score < bestScore || (score == bestScore && isPossible && !bestIsPossible))
                (best, bestScore, bestIsPossible) = (guess, score, isPossible);
        }
        return best;
    }

    private const string Solved = "!";

    private static string HintKey(GuessResult r) =>
        r.IsCorrect ? Solved : string.Concat(r.Cells.Select(c => (char)('a' + (int)c.Hit * 3 + (int)c.Direction)));
}
