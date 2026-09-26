using System.Text.RegularExpressions;
using Beydle.Models;

namespace Beydle.Services;

public sealed record Achievement(string Id, string Title, string Description, string Icon);

/// <summary>Achievements. Each is unlocked once and stored with the day it was found.</summary>
public static class Achievements
{
    public static readonly Achievement LetItRip = new("let-it-rip", "Let It Rip!", "Shouted a battle call into the guess box.", "📣");
    public static readonly Achievement XtremeFinish = new("xtreme-finish", "Xtreme Finish", "Solved round 1 with the very first guess.", "⚡");
    public static readonly Achievement SharpEye = new("sharp-eye", "Sharp Eye", "Named the round 2 blade from the silhouette alone.", "👁️");
    public static readonly Achievement PerfectDay = new("perfect-day", "Perfect Day", "Cleared both rounds with a single guess each.", "🏆");
    public static readonly Achievement StaminaType = new("stamina-type", "Stamina Type", "Needed 15 or more guesses in round 1 and still got there.", "🔋");
    public static readonly Achievement AsClearAsItGets = new("as-clear-as-it-gets", "As Clear As It Gets", "Solved round 2 only after the picture was fully sharp.", "🔍");
    public static readonly Achievement FamilyReunion = new("family-reunion", "Family Reunion", "Guessed three blades of the same family in a row.", "👨‍👩‍👧");
    public static readonly Achievement DejaVu = new("deja-vu", "Déjà Vu", "Guessed the round 1 answer again in round 2.", "🔁");
    public static readonly Achievement OnARoll = new("on-a-roll", "On a Roll", "Kept a 7-day streak.", "🔥");
    public static readonly Achievement IronWill = new("iron-will", "Iron Will", "Kept a 30-day streak.", "💎");
    public static readonly Achievement Grinder = new("grinder", "Grinder", "Finished 10 practice rounds in one sitting.", "🏋️");
    public static readonly Achievement Encyclopedia = new("encyclopedia", "Encyclopedia", "Guessed every blade in the pool at least once in daily mode.", "📚");
    public static readonly Achievement PhotoFinish = new("photo-finish", "Photo Finish", "Made a wrong guess that matched on every column but one.", "📸");
    public static readonly Achievement CounterSpin = new("counter-spin", "Counter-Spin", "Solved a day whose blade spins left.", "🌀");
    public static readonly Achievement ManyHappyReturns = new("many-happy-returns", "Many Happy Returns", "Solved a blade on its release anniversary.", "🎂");
    public static readonly Achievement ByTheBook = new("by-the-book", "By the Book", "Solved round 1 in Xtreme mode.", "📏");
    public static readonly Achievement OutsmartedTheBot = new("outsmarted-the-bot", "Outsmarted the Bot", "Solved round 1 in fewer guesses than Beydle Bot.", "🤖");

    public static readonly IReadOnlyList<Achievement> All =
    [
        LetItRip, XtremeFinish, SharpEye, PerfectDay, StaminaType, AsClearAsItGets,
        FamilyReunion, DejaVu, OnARoll, IronWill, Grinder, Encyclopedia,
        PhotoFinish, CounterSpin, ManyHappyReturns, ByTheBook, OutsmartedTheBot,
    ];

    public const int StaminaGuesses = 15;
    public const int GrinderRounds = 10;

    private static readonly Dictionary<string, string> Replies = new()
    {
        ["let it rip"] = "Let it rip! Now name a blade.",
        ["3 2 1 go shoot"] = "GO SHOOT! Now name a blade.",
        ["321 go shoot"] = "GO SHOOT! Now name a blade.",
        ["go shoot"] = "GO SHOOT! Now name a blade.",
        ["spin finish"] = "One point. The other blade simply outlasted you.",
        ["over finish"] = "Two points, but only if it actually left the stadium.",
        ["burst finish"] = "Two points. Xtreme is worth three.",
        ["xtreme finish"] = "Three points. You still have to earn them.",
    };

    /// <summary>The game's reply to a typed battle call, or null when the text is not one.</summary>
    public static string? Reply(string input)
    {
        var key = Regex.Replace(input.ToLowerInvariant(), "[^a-z0-9]+", " ").Trim();
        return Replies.GetValueOrDefault(key);
    }

    /// <summary>
    /// Everything the daily game and stats currently satisfy. Callers unlock whatever is new; evaluating
    /// the whole set every time keeps the rules in one place and makes restoring a saved day trivial.
    /// </summary>
    public static IEnumerable<Achievement> Earned(GameSession daily, Stats stats, IReadOnlyList<Blade> pool, IReadOnlySet<string> seen, DateOnly day)
    {
        var r1 = daily.Blade;
        var r2 = daily.Image;

        if (daily.XtremeFinish) yield return XtremeFinish;
        if (r1.Won && r1.Guesses.Count >= StaminaGuesses) yield return StaminaType;
        if (r2.Won && r2.Guesses.Count == 1) yield return SharpEye;
        if (r2.Won && r1.Guesses.Count == 1 && r2.Guesses.Count == 1) yield return PerfectDay;
        if (r2.Won && r2.Guesses.Count > ImageRound.MaxStage) yield return AsClearAsItGets;
        if (HasFamilyRun(r1.Guesses.Select(g => g.Guess))) yield return FamilyReunion;
        if (r2.Guesses.Any(b => b.Id == r1.Answer.Id)) yield return DejaVu;
        if (stats.Streak >= 7) yield return OnARoll;
        if (stats.Streak >= 30) yield return IronWill;
        if (pool.Count > 0 && pool.All(b => seen.Contains(b.Id))) yield return Encyclopedia;
        if (r1.Guesses.Any(g => !g.IsCorrect && g.Cells.Count(c => c.Hit != Hit.Exact) == 1)) yield return PhotoFinish;
        if (r1.Won && r1.Answer.Spin == "Left") yield return CounterSpin;
        if (r1.Won && AnniversaryYears(r1.Answer, day) is not null) yield return ManyHappyReturns;
        if (r1.Won && daily.XtremeMode) yield return ByTheBook;
        if (r1.Won && r1.Guesses.Count < Deduction.BotGuesses(pool, r1.Answer)) yield return OutsmartedTheBot;
    }

    /// <summary>The family is the first word of the name: Dran, Hells, Phoenix, Knight…</summary>
    public static string Family(Blade b) => b.Name.Split(' ', 2)[0];

    /// <summary>Three consecutive guesses from the same family.</summary>
    public static bool HasFamilyRun(IEnumerable<Blade> guesses)
    {
        var run = 0;
        string? last = null;
        foreach (var b in guesses)
        {
            var f = Family(b);
            run = f == last ? run + 1 : 1;
            last = f;
            if (run >= 3) return true;
        }
        return false;
    }

    /// <summary>Whole years since the blade's release when today is its anniversary; otherwise null.</summary>
    public static int? AnniversaryYears(Blade blade, DateOnly today)
    {
        if (!DateOnly.TryParseExact(blade.Released, DailyPicker.DayFormat, out var released)) return null;
        if (released.Month != today.Month || released.Day != today.Day) return null;
        var years = today.Year - released.Year;
        return years > 0 ? years : null;
    }
}
