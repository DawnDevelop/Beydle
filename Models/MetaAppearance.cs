namespace Beydle.Models;

/// <summary>
/// One combo in a top-3 finish at a WBO event (wwwroot/data/meta/appearances.json, written by meta/BeybladeMeta.Indexer).
/// <see cref="Deck"/> groups the combos one player ran in that finish.
/// </summary>
public sealed record MetaAppearance(string Blade, string Display, string? Ratchet, string Bit, int Placement, string? Date, int? Deck)
{
    /// <summary>The event date, parsed once; null when the post had none.</summary>
    public DateOnly? On { get; } = DateOnly.TryParseExact(Date, "yyyy-MM-dd", out var d) ? d : null;
}
