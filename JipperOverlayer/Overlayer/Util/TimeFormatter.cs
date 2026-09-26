namespace JipperOverlayer.Overlayer.Util;

internal static class TimeFormatter
{
    public static string Format(float time, bool hour)
    {
        if (float.IsNaN(time) || float.IsInfinity(time) || time < 0) time = 0;
        int t = (int)time;
        return hour ? $"{t / 3600}:{t % 3600 / 60:00}:{t % 60:00}" : $"{t / 60}:{t % 60:00}";
    }

    public static string FormatWithDecimals(float time, bool hour)
    {
        if (float.IsNaN(time) || float.IsInfinity(time) || time < 0) time = 0;
        int t = (int)time;
        return hour ? $"{t / 3600}:{t % 3600 / 60:00}:{time % 60:00.0}" : $"{t / 60}:{time % 60:00.0}";
    }
}
