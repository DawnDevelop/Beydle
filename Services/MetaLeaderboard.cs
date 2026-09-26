using Beydle.Models;

namespace Beydle.Services;

public enum MetaView { Blades, Combos, Ratchets, Bits, Decks }

/// <summary>How often <see cref="Name"/> finished 1st, 2nd and 3rd in a period.</summary>
public sealed record MetaRow(string Name, int First, int Second, int Third)
{
    public int Total => First + Second + Third;
    public int Score => 3 * First + 2 * Second + Third;
    public int Rank { get; init; }
    /// <summary>Rank in the previous period minus rank now, so positive means it climbed; null without a previous rank.</summary>
    public int? Delta { get; init; }
    /// <summary>True when the previous period exists and this entry is not in it.</summary>
    public bool IsNew { get; init; }

    /// <summary>"up", "down", "new" or "" (unchanged or unknown), for styling the movement.</summary>
    public string Trend => IsNew ? "new" : Delta > 0 ? "up" : Delta < 0 ? "down" : "";
    public string TrendText => IsNew ? "new" : Delta switch { > 0 and var d => $"▲{d}", < 0 and var d => $"▼{-d}", _ => "–" };
    /// <summary>The movement for screen readers ("up 3"); null when there is none to tell.</summary>
    public string? TrendSpoken => IsNew ? "new" : Delta switch { > 0 and var d => $"up {d}", < 0 and var d => $"down {-d}", _ => null };
}

/// <param name="Cutoff">First day of the period ending on the given day; null means all time.</param>
public sealed record MetaWindow(string Key, string Label, Func<DateOnly, DateOnly?> Cutoff);

/// <summary>
/// Ranks blades, combos, ratchets, bits and three-blade decks by their top-3 finishes. Periods end at the newest event
/// in the data rather than today, so "last 4 weeks" stays filled as the data ages, and movement compares a period with
/// the one of the same length before it.
/// </summary>
public sealed class MetaLeaderboard
{
    public static readonly IReadOnlyList<MetaWindow> Windows =
    [
        new("all", "All time", _ => null),
        new("1w", "Last week", t => t.AddDays(-7)),
        new("2w", "Last 2 weeks", t => t.AddDays(-14)),
        new("4w", "Last 4 weeks", t => t.AddDays(-28)),
        new("3m", "Last 3 months", t => t.AddMonths(-3)),
        new("6m", "Last 6 months", t => t.AddMonths(-6)),
    ];

    public const string DefaultWindow = "4w";

    /// <summary>One player's three combos in one finish. Names are sorted so the same line-up always reads the same.</summary>
    private sealed record Deck(int Placement, DateOnly? On, string[] Members, string Blades, string Combos);

    private readonly IReadOnlyList<MetaAppearance> appearances;
    private readonly List<Deck> decks;

    /// <summary>The newest event date in the data; null when no appearance is dated.</summary>
    public DateOnly? Anchor { get; }

    public MetaLeaderboard(IReadOnlyList<MetaAppearance> appearances)
    {
        this.appearances = appearances;
        Anchor = appearances.Max(a => a.On);
        decks = appearances
            .Where(a => a.Deck is not null)
            .GroupBy(a => a.Deck!.Value)
            .Where(g => g.Count() == 3) // standard 3-on-3 decks
            .Select(g =>
            {
                var beys = g.ToList();
                var members = beys.Select(b => b.Blade).ToArray();
                return new Deck(beys[0].Placement, beys[0].On, members, JoinSorted(members), JoinSorted(beys.Select(b => b.Display)));
            })
            .ToList();
    }

    /// <param name="blade">Counts only this blade's finishes, for its page: its ratchets, bits, combos and the decks it was in.</param>
    public IReadOnlyList<MetaRow> Rank(MetaView view, string window, string? blade = null)
    {
        if (view == MetaView.Decks)
        {
            var (current, prior) = Split(decks.Where(d => blade is null || d.Members.Contains(blade)), d => d.On, window);
            return RankPairs(current.Select(d => (d.Blades, d.Placement)), prior?.Select(d => (d.Blades, d.Placement)));
        }
        var key = KeyOf(view);
        var (now, before) = Split(appearances.Where(a => blade is null || a.Blade == blade), a => a.On, window);
        return RankPairs(Pairs(now, key), before is null ? null : Pairs(before, key));
    }

    /// <summary>
    /// The combos behind one entry of <see cref="Rank"/>, best first: the combos using a blade, ratchet or bit, or the
    /// exact three-combo line-ups of a deck. Combos have nothing below them.
    /// </summary>
    public IReadOnlyList<MetaRow> CombosFor(MetaView view, string window, string name)
    {
        if (view == MetaView.Decks)
        {
            var (current, _) = Split(decks, d => d.On, window);
            return RankPairs(current.Where(d => d.Blades == name).Select(d => (d.Combos, d.Placement)), null);
        }
        var key = KeyOf(view);
        var (now, _) = Split(appearances, a => a.On, window);
        return RankPairs(now.Where(a => key(a) == name).Select(a => (a.Display, a.Placement)), null);
    }

    private (List<T> Current, List<T>? Prior) Split<T>(IEnumerable<T> items, Func<T, DateOnly?> on, string window)
    {
        var cutoff = Anchor is { } end ? Windows.First(w => w.Key == window).Cutoff(end) : null;
        if (cutoff is null) return (items.ToList(), null);
        var priorStart = cutoff.Value.AddDays(-(Anchor!.Value.DayNumber - cutoff.Value.DayNumber));
        return (items.Where(i => on(i) is { } d && d >= cutoff).ToList(),
                items.Where(i => on(i) is { } d && d >= priorStart && d < cutoff).ToList());
    }

    private static Func<MetaAppearance, string?> KeyOf(MetaView view) => view switch
    {
        MetaView.Combos => a => a.Display,
        MetaView.Ratchets => a => a.Ratchet,
        MetaView.Bits => a => a.Bit,
        _ => a => a.Blade,
    };

    private static IEnumerable<(string, int)> Pairs(IEnumerable<MetaAppearance> items, Func<MetaAppearance, string?> key) =>
        items.Where(a => !string.IsNullOrEmpty(key(a))).Select(a => (key(a)!, a.Placement));

    private static string JoinSorted(IEnumerable<string> names) => string.Join(" + ", names.OrderBy(n => n, StringComparer.Ordinal));

    private static List<MetaRow> RankPairs(IEnumerable<(string Key, int Placement)> current, IEnumerable<(string Key, int Placement)>? prior)
    {
        var priorRanks = prior is null ? null : Ordered(Group(prior)).Select((r, i) => (r.Name, Rank: i + 1)).ToDictionary(x => x.Name, x => x.Rank);
        return Ordered(Group(current))
            .Select((r, i) =>
            {
                var rank = i + 1;
                int? delta = priorRanks is not null && priorRanks.TryGetValue(r.Name, out var was) ? was - rank : null;
                return r with { Rank = rank, Delta = delta, IsNew = priorRanks is not null && !priorRanks.ContainsKey(r.Name) };
            })
            .ToList();
    }

    private static IEnumerable<MetaRow> Group(IEnumerable<(string Key, int Placement)> pairs) =>
        pairs.GroupBy(p => p.Key).Select(g => new MetaRow(g.Key, g.Count(p => p.Placement == 1), g.Count(p => p.Placement == 2), g.Count(p => p.Placement == 3)));

    // Score, then total, then name: ties rank the same in every period, so they never show as movement.
    private static IOrderedEnumerable<MetaRow> Ordered(IEnumerable<MetaRow> rows) =>
        rows.OrderByDescending(r => r.Score).ThenByDescending(r => r.Total).ThenBy(r => r.Name, StringComparer.Ordinal);
}
