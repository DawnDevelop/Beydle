using System.Net.Http.Json;
using Beydle.Models;

namespace Beydle.Services;

/// <summary>
/// Sends one anonymous event per guess to the Worker (worker/index.js) for aggregate stats. Best effort: a failed
/// request is ignored.
/// </summary>
public sealed class GuessReporter(HttpClient http)
{
    /// <param name="game">The game after it accepted <paramref name="blade"/>.</param>
    public async Task ReportAsync(DateOnly day, bool practice, GameSession game, Blade blade, bool inImageRound)
    {
        try
        {
            await http.PostAsJsonAsync("api/event", new
            {
                date = DailyPicker.DayKey(day),
                mode = practice ? "practice" : "daily",
                round = inImageRound ? 2 : 1,
                guess = blade.Id,
                number = inImageRound ? game.Image.Guesses.Count : game.Blade.Guesses.Count,
                solved = inImageRound ? game.Image.Won : game.Blade.Won,
                xtreme = game.XtremeMode,
            });
        }
        catch (HttpRequestException) { }
    }
}
