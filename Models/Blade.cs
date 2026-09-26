namespace Beydle.Models;

public sealed record Blade(
    string Id,
    string Name,
    string[] Aliases,
    string Code,
    string Line,
    string Type,
    string Spin,
    double Weight,
    int Atk,
    int Def,
    int Sta,
    string Ratchet,
    string Bit,
    string BitAbbr,
    string BitType,
    string Image,
    int Year,
    string? Owner,
    string? Released = null)
{
    public bool HasIntegratedRatchet => Ratchet == "INT";
    public string RatchetLabel => HasIntegratedRatchet ? "Integrated" : Ratchet;
    public string OwnerLabel => Owner ?? "None";
    /// <summary>The stock combo as sold, e.g. "Dran Sword 3-60F"; an integrated ratchet is left out.</summary>
    public string StockCombo => $"{Name} {(HasIntegratedRatchet ? "" : Ratchet)}{BitAbbr}";

    public bool Matches(string query)
    {
        if (Name.Contains(query, StringComparison.OrdinalIgnoreCase)) return true;
        foreach (var a in Aliases)
            if (a.Contains(query, StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }
}
