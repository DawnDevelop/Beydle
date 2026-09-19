namespace Beydle.Models;

public enum Hit { Exact, Partial, Miss }

public enum Direction { None, Higher, Lower }

/// <summary>One compared cell of a guess row.</summary>
public sealed record Cell(string Label, string Value, Hit Hit, Direction Direction = Direction.None)
{
    public string Emoji => Hit switch { Hit.Exact => "🟩", Hit.Partial => "🟨", _ => "⬛" };
}

public sealed record GuessResult(Blade Guess, Cell[] Cells, bool IsCorrect)
{
    public static readonly string[] Labels = ["Type", "Spin", "Weight", "ATK", "DEF", "STA", "Ratchet", "Bit", "Year", "Owner"];

    public static GuessResult Compare(Blade guess, Blade answer)
    {
        var cells = new[]
        {
            new Cell("Type", guess.Type, guess.Type == answer.Type ? Hit.Exact : Hit.Miss),
            new Cell("Spin", guess.Spin, guess.Spin == answer.Spin ? Hit.Exact : Hit.Miss),
            Numeric("Weight", guess.Weight, answer.Weight, closeWithin: 1.5, format: v => $"{v:0.0} g"),
            Numeric("ATK", guess.Atk, answer.Atk, closeWithin: 10, format: v => $"{v:0}"),
            Numeric("DEF", guess.Def, answer.Def, closeWithin: 10, format: v => $"{v:0}"),
            Numeric("STA", guess.Sta, answer.Sta, closeWithin: 10, format: v => $"{v:0}"),
            new Cell("Ratchet", guess.RatchetLabel, RatchetHit(guess, answer)),
            new Cell("Bit", guess.Bit, guess.Bit == answer.Bit ? Hit.Exact : guess.BitType == answer.BitType ? Hit.Partial : Hit.Miss),
            Numeric("Year", guess.Year, answer.Year, closeWithin: 0, format: v => $"{v:0}"),
            new Cell("Owner", guess.OwnerLabel, guess.Owner == answer.Owner ? Hit.Exact : Hit.Miss),
        };
        return new GuessResult(guess, cells, guess.Id == answer.Id);
    }

    private static Cell Numeric(string label, double guess, double answer, double closeWithin, Func<double, string> format)
    {
        var diff = guess - answer;
        if (Math.Abs(diff) < 0.001) return new Cell(label, format(guess), Hit.Exact);
        var hit = closeWithin > 0 && Math.Abs(diff) <= closeWithin ? Hit.Partial : Hit.Miss;
        // Arrow points toward the answer: guess too high -> answer is lower.
        return new Cell(label, format(guess), hit, diff > 0 ? Direction.Lower : Direction.Higher);
    }

    private static Hit RatchetHit(Blade guess, Blade answer)
    {
        if (guess.Ratchet == answer.Ratchet) return Hit.Exact;
        if (guess.HasIntegratedRatchet || answer.HasIntegratedRatchet) return Hit.Miss;
        var g = guess.Ratchet.Split('-');
        var a = answer.Ratchet.Split('-');
        return g[0] == a[0] || g[1] == a[1] ? Hit.Partial : Hit.Miss;
    }
}
