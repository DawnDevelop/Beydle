using Beydle.Models;

namespace Beydle.Services;

/// <summary>
/// The player's two games, today's daily game and a practice game, and the progress saved for them. The rules
/// that span both games or touch saved state live here; the page renders this and saves <see cref="State"/>
/// after every change.
/// </summary>
public sealed class Games(IReadOnlyList<Blade> pool, IReadOnlyDictionary<string, string[]>? schedule, SavedState state, Random? random = null)
{
    private readonly Random random = random ?? Random.Shared;

    public IReadOnlyList<Blade> Pool { get; } = pool;
    public SavedState State { get; } = state;
    public Stats Stats => State.Stats;

    public DateOnly Today { get; private set; }
    public int DayNumber => DailyPicker.DayNumber(Today);
    public GameSession Daily { get; private set; } = null!;
    public GameSession Practice { get; private set; } = null!;

    /// <summary>Practice games finished since the page loaded, for Grinder.</summary>
    public int PracticeRoundsDone { get; private set; }

    /// <summary>
    /// Sets up <paramref name="day"/>'s daily game. Guesses saved for the same day are replayed; a new day starts
    /// empty and takes the player's Xtreme-mode preference. Returns true when saved guesses were replayed.
    /// </summary>
    public bool StartDay(DateOnly day)
    {
        Today = day;
        var (first, image) = DailyPicker.PickDay(Pool, day, schedule);
        Daily = new GameSession(first, image);

        var key = DailyPicker.DayKey(day);
        if (State.Day != key)
        {
            State.Day = key;
            State.Guesses = [];
            State.ImageGuesses = [];
            State.DayXtreme = State.XtremeMode;
        }
        Daily.XtremeMode = State.DayXtreme;
        Daily.Restore(Pool, State.Guesses, State.ImageGuesses);
        return Daily.Blade.Guesses.Count > 0;
    }

    /// <summary>A fresh practice game. Never uses today's daily blades, so practice cannot spoil the daily puzzle.</summary>
    public void NewPractice()
    {
        var candidates = Pool.Where(b => b.Id != Daily.Blade.Answer.Id && b.Id != Daily.Image.Answer.Id).ToList();
        var first = candidates[random.Next(candidates.Count)];
        var rest = candidates.Where(b => b.Id != first.Id).ToList();
        var second = rest[random.Next(rest.Count)];
        Practice = new GameSession(first, second) { XtremeMode = State.XtremeMode };
    }

    /// <summary>
    /// Flips Xtreme mode for <paramref name="game"/> (which must not be <see cref="GameSession.XtremeModeLocked"/>)
    /// and stores it as the player's preference. The preference also reaches the other game if it has not started,
    /// so switching tabs never shows a stale switch. Returns the new setting.
    /// </summary>
    public bool ToggleXtremeMode(GameSession game)
    {
        var on = !game.XtremeMode;
        State.XtremeMode = on;
        game.XtremeMode = on;
        if (ReferenceEquals(game, Daily)) State.DayXtreme = on;
        if (Daily.Blade.Guesses.Count == 0) Daily.XtremeMode = State.DayXtreme = on;
        if (Practice.Blade.Guesses.Count == 0) Practice.XtremeMode = on;
        return on;
    }

    /// <summary>Saves a guess the daily game has just accepted, and updates the stats for a round-1 guess.</summary>
    public void RecordDailyGuess(Blade blade, bool inImageRound)
    {
        State.Seen.Add(blade.Id);
        if (inImageRound)
        {
            State.ImageGuesses.Add(blade.Id);
            return;
        }
        State.Guesses.Add(blade.Id);
        StatsTracker.RecordFirstGuess(Stats, State.Day);
        if (Daily.Blade.Won) StatsTracker.RecordWin(Stats, Today, Daily.Blade.Guesses.Count);
    }

    /// <summary>Counts a finished practice game. Returns true once enough have been played for Grinder.</summary>
    public bool FinishPracticeRound() => ++PracticeRoundsDone >= Achievements.GrinderRounds;

    /// <summary>Records <paramref name="a"/> as unlocked today. Returns false when it already was.</summary>
    public bool Unlock(Achievement a) => State.Achievements.TryAdd(a.Id, DailyPicker.DayKey(Today));

    /// <summary>Everything the daily game and stats satisfy; daily mode only, so practice rounds cannot farm these.</summary>
    public IEnumerable<Achievement> EarnedDaily() => Achievements.Earned(Daily, Stats, Pool, State.Seen, Today);
}
