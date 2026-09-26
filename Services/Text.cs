namespace Beydle.Services;

/// <summary>Small wording helpers shared by the page and the share texts.</summary>
public static class Text
{
    /// <summary><paramref name="singular"/> for exactly one, otherwise <paramref name="plural"/>.</summary>
    public static string Plural(int n, string singular, string plural) => n == 1 ? singular : plural;

    /// <summary>The number followed by the right word, e.g. "1 guess" or "3 guesses".</summary>
    public static string Count(int n, string singular, string plural) => $"{n} {Plural(n, singular, plural)}";
}
