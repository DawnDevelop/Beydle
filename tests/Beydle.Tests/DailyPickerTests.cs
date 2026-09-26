using Beydle.Models;
using Beydle.Services;

namespace Beydle.Tests;

public class DailyPickerTests
{
    private static Blade Make(string id) =>
        new(id, id, [], "BX-01", "BX", "Attack", "Right", 30, 50, 20, 20, "3-60", "Flat", "F", "Attack", "", 2023, "Ekusu Kurosu");

    private static readonly IReadOnlyList<Blade> Pool = Enumerable.Range(0, 75).Select(i => Make($"b{i}")).ToList();

    [Fact]
    public void SameDayAlwaysGivesSameBlade()
    {
        var day = new DateOnly(2026, 9, 19);
        Assert.Same(DailyPicker.Pick(Pool, day), DailyPicker.Pick(Pool, day));
    }

    [Fact]
    public void ConsecutiveDaysNeverRepeat()
    {
        var day = new DateOnly(2026, 1, 1);
        for (var i = 0; i < 400; i++, day = day.AddDays(1))
            Assert.NotSame(DailyPicker.Pick(Pool, day), DailyPicker.Pick(Pool, day.AddDays(1)));
    }

    [Fact]
    public void EveryBladeAppearsOncePerCycle()
    {
        foreach (var start in new[] { 0, 75, 150, 3000 })
        {
            var day = new DateOnly(2026, 1, 1).AddDays(start);
            var seen = new HashSet<string>();
            for (var i = 0; i < Pool.Count; i++, day = day.AddDays(1))
                seen.Add(DailyPicker.Pick(Pool, day).Id);
            Assert.Equal(Pool.Count, seen.Count);
        }
    }

    [Fact]
    public void ImageBladeIsDeterministicAndDiffersFromTheDailyBlade()
    {
        var day = new DateOnly(2026, 1, 1);
        for (var i = 0; i < 400; i++, day = day.AddDays(1))
        {
            var image = DailyPicker.PickImage(Pool, day);
            Assert.Same(image, DailyPicker.PickImage(Pool, day));
            Assert.NotSame(DailyPicker.Pick(Pool, day), image);
        }
    }

    [Theory]
    [InlineData(10)]
    [InlineData(75)]
    [InlineData(79)]
    public void ImageBladeNeverRepeatsOnConsecutiveDays(int size)
    {
        var pool = Pool.Concat(Enumerable.Range(75, 10).Select(i => Make($"b{i}"))).Take(size).ToList();
        var day = new DateOnly(2026, 1, 1);
        var previous = DailyPicker.PickImage(pool, day.AddDays(-1));
        for (var i = 0; i < 365 * 30; i++, day = day.AddDays(1))
        {
            var image = DailyPicker.PickImage(pool, day);
            Assert.NotSame(previous, image);
            previous = image;
        }
    }

    [Fact]
    public void ImageBladeWithTwoBladePoolAlwaysTakesTheOtherOne()
    {
        var two = Pool.Take(2).ToList();
        var day = new DateOnly(2026, 3, 1);
        for (var i = 0; i < 10; i++, day = day.AddDays(1))
            Assert.NotSame(DailyPicker.Pick(two, day), DailyPicker.PickImage(two, day));
    }

    [Fact]
    public void WorksForDatesBeforeTheEpoch()
    {
        var day = new DateOnly(2025, 12, 31);
        Assert.NotNull(DailyPicker.Pick(Pool, day));
        Assert.NotSame(DailyPicker.Pick(Pool, day), DailyPicker.Pick(Pool, day.AddDays(1)));
    }

    [Fact]
    public void DayNumberCountsFromEpoch()
    {
        Assert.Equal(0, DailyPicker.DayNumber(new DateOnly(2026, 1, 1)));
        Assert.Equal(261, DailyPicker.DayNumber(new DateOnly(2026, 9, 19)));
    }

    [Fact]
    public void CountdownIsWithinOneDay()
    {
        var t = DailyPicker.UntilNextDay();
        Assert.InRange(t, TimeSpan.Zero, TimeSpan.FromHours(24));
    }

    [Fact]
    public void CountdownIsExactAcrossDaylightSavingChanges()
    {
        // 2026-03-29 is the spring-forward day in Berlin (CET -> CEST at 02:00). 2026-03-28 22:00 UTC is 23:00 CET;
        // the next Berlin midnight (00:00 CET, 2026-03-29) is 2026-03-28 23:00 UTC, so exactly one hour remains.
        Assert.Equal(TimeSpan.FromHours(1), DailyPicker.UntilNextDay(new DateTime(2026, 3, 28, 22, 0, 0, DateTimeKind.Utc)));
        // During the 23-hour day itself: 2026-03-29 12:00 UTC is 14:00 CEST; midnight CEST is 2026-03-29 22:00 UTC.
        Assert.Equal(TimeSpan.FromHours(10), DailyPicker.UntilNextDay(new DateTime(2026, 3, 29, 12, 0, 0, DateTimeKind.Utc)));
        // Fall back: 2026-10-25 (CEST -> CET at 03:00, a 25-hour day). 2026-10-25 12:00 UTC is 13:00 CET; midnight CET is 23:00 UTC.
        Assert.Equal(TimeSpan.FromHours(11), DailyPicker.UntilNextDay(new DateTime(2026, 10, 25, 12, 0, 0, DateTimeKind.Utc)));
    }

    [Fact]
    public void BerlinDateRollsOverAtBerlinMidnightNotUtc()
    {
        Assert.Equal(new DateOnly(2026, 9, 19), DailyPicker.Today(new DateTime(2026, 9, 19, 21, 59, 0, DateTimeKind.Utc)));
        Assert.Equal(new DateOnly(2026, 9, 20), DailyPicker.Today(new DateTime(2026, 9, 19, 22, 0, 0, DateTimeKind.Utc)));
    }

    [Fact]
    public void ScheduleHashIsStableAndDiffersPerDayAndBlade()
    {
        var d = new DateOnly(2026, 9, 19);
        Assert.Equal(ScheduleHash.For(d, "dran-sword"), ScheduleHash.For(d, "dran-sword"));
        Assert.Equal(32, ScheduleHash.For(d, "dran-sword").Length);
        Assert.NotEqual(ScheduleHash.For(d, "dran-sword"), ScheduleHash.For(d.AddDays(1), "dran-sword"));
        Assert.NotEqual(ScheduleHash.For(d, "dran-sword"), ScheduleHash.For(d, "dran-buster"));
    }

    [Fact]
    public void ScheduleOverridesTheGeneratorAndSurvivesCatalogueGrowth()
    {
        var day = new DateOnly(2026, 9, 19);
        var schedule = new Dictionary<string, string[]> { ["2026-09-19"] = [ScheduleHash.For(day, "b5"), ScheduleHash.For(day, "b9")] };
        var (first, image) = DailyPicker.PickDay(Pool, day, schedule);
        Assert.Equal("b5", first.Id);
        Assert.Equal("b9", image.Id);

        var grown = Pool.Concat([Make("brand-new")]).ToList();
        var (first2, image2) = DailyPicker.PickDay(grown, day, schedule);
        Assert.Equal("b5", first2.Id);
        Assert.Equal("b9", image2.Id);
    }

    [Fact]
    public void ScheduleFallsBackToGeneratorForUnknownDaysOrBrokenEntries()
    {
        var day = new DateOnly(2026, 9, 19);
        var expected = (DailyPicker.Pick(Pool, day), DailyPicker.PickImage(Pool, day));
        Assert.Equal(expected, DailyPicker.PickDay(Pool, day, null));
        Assert.Equal(expected, DailyPicker.PickDay(Pool, day, new Dictionary<string, string[]>()));
        Assert.Equal(expected, DailyPicker.PickDay(Pool, day, new Dictionary<string, string[]> { ["2026-09-19"] = [ScheduleHash.For(day, "b5"), ScheduleHash.For(day, "gone")] }));
        Assert.Equal(expected, DailyPicker.PickDay(Pool, day, new Dictionary<string, string[]> { ["2026-09-19"] = [ScheduleHash.For(day, "b5"), ScheduleHash.For(day, "b5")] }));
        // Plain ids (the old file format) no longer resolve.
        Assert.Equal(expected, DailyPicker.PickDay(Pool, day, new Dictionary<string, string[]> { ["2026-09-19"] = ["b5", "b9"] }));
    }

    [Fact]
    public void CommittedScheduleMatchesTheGeneratorForTheCurrentCatalogue()
    {
        // Guards the JS port in tools/build-schedule.js against drifting from the C# generator. Only the tail of the
        // file is checked: build-schedule.js keeps past days as they were, so they reflect an older catalogue once a
        // blade is added.
        var root = FindRepoRoot();
        var json = new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web);
        var blades = System.Text.Json.JsonSerializer.Deserialize<List<Blade>>(File.ReadAllText(Path.Combine(root, "wwwroot", "data", "blades.json")), json)!;
        var schedule = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string[]>>(File.ReadAllText(Path.Combine(root, "wwwroot", "data", "schedule.json")))!;
        Assert.True(schedule.Count > 365);
        foreach (var (k, v) in schedule.TakeLast(400))
        {
            var day = DateOnly.Parse(k);
            Assert.Equal(ScheduleHash.For(day, DailyPicker.Pick(blades, day).Id), v[0]);
            Assert.Equal(ScheduleHash.For(day, DailyPicker.PickImage(blades, day).Id), v[1]);
            Assert.DoesNotContain(blades, b => b.Id == v[0]); // no plain ids in the file
        }
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Beydle.csproj"))) dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("repo root not found");
    }
}
