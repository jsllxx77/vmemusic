namespace VmeMusic.Models;

public static class DurationFormatter
{
    public static string FormatSeconds(int? seconds)
    {
        if (seconds is null or < 0)
        {
            return "";
        }

        var duration = TimeSpan.FromSeconds(seconds.Value);
        if (duration.TotalHours >= 1)
        {
            return $"{(int)duration.TotalHours}:{duration.Minutes:00}:{duration.Seconds:00}";
        }

        return $"{(int)duration.TotalMinutes}:{duration.Seconds:00}";
    }
}
