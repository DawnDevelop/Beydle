using System.Net.Http.Json;
using Beydle.Models;

namespace Beydle.Services;

public sealed class BladeService(HttpClient http)
{
    private IReadOnlyList<Blade>? _blades;
    private IReadOnlyDictionary<string, string[]>? _schedule;

    public async Task<IReadOnlyList<Blade>> GetAllAsync()
    {
        _blades ??= await http.GetFromJsonAsync<List<Blade>>("data/blades.json")
                    ?? throw new InvalidOperationException("blades.json is empty");
        return _blades;
    }

    /// <summary>Date (yyyy-MM-dd) to [round-1 blade id, round-2 blade id]. Missing file means "use the generator".</summary>
    public async Task<IReadOnlyDictionary<string, string[]>?> GetScheduleAsync()
    {
        if (_schedule is not null) return _schedule;
        try
        {
            _schedule = await http.GetFromJsonAsync<Dictionary<string, string[]>>("data/schedule.json");
        }
        catch (HttpRequestException)
        {
            _schedule = null;
        }
        return _schedule;
    }
}
