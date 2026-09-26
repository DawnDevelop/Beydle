using Beydle.Models;

namespace Beydle.Services;

/// <summary>Deterministically picks the two blades of each calendar day in Europe/Berlin time.</summary>
public static class DailyPicker
{
    public static readonly DateOnly Epoch = new(2026, 1, 1);
    /// <summary>How days are written in saved state, the schedule and stats events.</summary>
    public const string DayFormat = "yyyy-MM-dd";
    private static readonly TimeZoneInfo Berlin = ResolveBerlin();

    public static DateOnly Today() => Today(DateTime.UtcNow);

    public static DateOnly Today(DateTime utcNow) => DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(utcNow, Berlin));

    public static int DayNumber(DateOnly day) => day.DayNumber - Epoch.DayNumber;

    public static string DayKey(DateOnly day) => day.ToString(DayFormat);

    public static TimeSpan UntilNextDay() => UntilNextDay(DateTime.UtcNow);

    /// <summary>Time until the next Berlin midnight, computed in UTC so DST transitions don't shift it by an hour.</summary>
    public static TimeSpan UntilNextDay(DateTime utcNow)
    {
        var berlinDate = Today(utcNow);
        var nextMidnightLocal = berlinDate.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);
        var nextMidnightUtc = TimeZoneInfo.ConvertTimeToUtc(nextMidnightLocal, Berlin);
        var remaining = nextMidnightUtc - utcNow;
        return remaining < TimeSpan.Zero ? TimeSpan.Zero : remaining;
    }

    /// <summary>
    /// Today's two blades. The committed schedule wins, so catalogue changes never move a published day.
    /// Schedule entries are <see cref="ScheduleHash"/> values; days outside the schedule fall back to the generator.
    /// </summary>
    public static (Blade First, Blade Image) PickDay(IReadOnlyList<Blade> blades, DateOnly day, IReadOnlyDictionary<string, string[]>? schedule)
    {
        if (schedule is not null && schedule.TryGetValue(DayKey(day), out var hashes) && hashes.Length == 2)
        {
            Blade? first = null, image = null;
            foreach (var b in blades)
            {
                var h = ScheduleHash.For(day, b.Id);
                if (h == hashes[0]) first = b;
                else if (h == hashes[1]) image = b;
            }
            if (first is not null && image is not null) return (first, image);
        }
        return (Pick(blades, day), PickImage(blades, day));
    }

    /// <summary>
    /// Generator for the schedule. Days are grouped into cycles of <c>blades.Count</c> days. Each cycle is a
    /// seeded shuffle of the whole pool, so every blade appears exactly once per cycle and never on two consecutive days.
    /// </summary>
    public static Blade Pick(IReadOnlyList<Blade> blades, DateOnly day)
    {
        var count = blades.Count;
        if (count == 1) return blades[0];
        var n = DayNumber(day);
        if (count == 2) return blades[Math.Abs(n) % 2]; // alternate; the shuffle's boundary fix assumes 3+ blades
        return AtPosition(blades, n);
    }

    /// <summary>
    /// The second (image) puzzle of the day: a different blade, drawn from the same shuffled cycles but read
    /// from a fixed offset so it is just as evenly distributed and never equals the first blade of the day.
    /// When the offset position holds the first blade, a blade from half a cycle further on stands in, one that is not
    /// the image of the day before or after, so the same silhouette never shows on two consecutive days (given 4 or more blades).
    /// </summary>
    public static Blade PickImage(IReadOnlyList<Blade> blades, DateOnly day)
    {
        var count = blades.Count;
        if (count == 1) return blades[0];
        var first = Pick(blades, day);
        if (count == 2) return blades[0].Id == first.Id ? blades[1] : blades[0];
        return ImageAt(blades, DayNumber(day));
    }

    // The stand-in avoids yesterday's actual image and tomorrow's regular one; if tomorrow needs a stand-in as well,
    // that one avoids today's. Yesterday's image only recurses further while days in a row need stand-ins.
    private static Blade ImageAt(IReadOnlyList<Blade> blades, int day)
    {
        var count = blades.Count;
        var first = AtPosition(blades, day);
        var n = day + ImageOffset;
        var regular = AtPosition(blades, n);
        if (regular.Id != first.Id) return regular;
        var yesterday = ImageAt(blades, day - 1).Id;
        var tomorrow = AtPosition(blades, n + 1).Id;
        Blade? any = null, fallback = null;
        for (var k = n + count / 2; k < n + count / 2 + 2 * count; k++) // two cycles' worth covers the whole pool
        {
            var candidate = AtPosition(blades, k);
            if (candidate.Id == first.Id) continue;
            any ??= candidate;
            if (candidate.Id == yesterday) continue;
            if (candidate.Id != tomorrow) return candidate;
            fallback ??= candidate;
        }
        return fallback ?? any ?? first;
    }

    private const int ImageOffset = 7919;

    /// <summary>The blade at position <paramref name="n"/> of the endless sequence of shuffled cycles.</summary>
    private static Blade AtPosition(IReadOnlyList<Blade> blades, int n)
    {
        var count = blades.Count;
        var cycle = (int)Math.Floor(n / (double)count);
        var pos = n - cycle * count;
        return blades[Permutation(count, cycle)[pos]];
    }

    private static int[] Permutation(int count, int cycle)
    {
        var perm = Shuffle(count, cycle);
        // Avoid a repeat across the cycle boundary.
        var previousLast = Shuffle(count, cycle - 1)[count - 1];
        if (perm[0] == previousLast) (perm[0], perm[1]) = (perm[1], perm[0]);
        return perm;
    }

    private static int[] Shuffle(int count, int seed)
    {
        var perm = Enumerable.Range(0, count).ToArray();
        uint state = (uint)seed * 0x9E3779B9u + 0x7F4A7C15u;
        for (var i = count - 1; i > 0; i--)
        {
            // xorshift32
            state ^= state << 13;
            state ^= state >> 17;
            state ^= state << 5;
            var j = (int)(state % (uint)(i + 1));
            (perm[i], perm[j]) = (perm[j], perm[i]);
        }
        return perm;
    }

    private static TimeZoneInfo ResolveBerlin()
    {
        try { return TimeZoneInfo.FindSystemTimeZoneById("Europe/Berlin"); }
        catch (TimeZoneNotFoundException)
        {
            // Fallback: CET/CEST without a tz database. EU DST rule: last Sunday of March to last Sunday of October.
            var rule = TimeZoneInfo.AdjustmentRule.CreateAdjustmentRule(
                DateTime.MinValue.Date, DateTime.MaxValue.Date, TimeSpan.FromHours(1),
                TimeZoneInfo.TransitionTime.CreateFloatingDateRule(new DateTime(1, 1, 1, 2, 0, 0), 3, 5, DayOfWeek.Sunday),
                TimeZoneInfo.TransitionTime.CreateFloatingDateRule(new DateTime(1, 1, 1, 3, 0, 0), 10, 5, DayOfWeek.Sunday));
            return TimeZoneInfo.CreateCustomTimeZone("Europe/Berlin", TimeSpan.FromHours(1), "Central European Time", "CET", "CEST", [rule]);
        }
    }
}
