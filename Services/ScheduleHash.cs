using System.Security.Cryptography;
using System.Text;

namespace Beydle.Services;

/// <summary>
/// The committed schedule stores hashes rather than blade ids, so the file alone does not spell out the day's
/// answers. The app hashes every candidate for the day and takes the one that matches.
/// tools/build-schedule.js must use the same salt and format.
/// </summary>
public static class ScheduleHash
{
    public const string Salt = "beydle-schedule-2026";

    public static string For(DateOnly day, string bladeId)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes($"{Salt}|{DailyPicker.DayKey(day)}|{bladeId}"));
        return Convert.ToHexStringLower(bytes)[..32];
    }
}
