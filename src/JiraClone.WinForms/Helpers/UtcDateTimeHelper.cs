namespace JiraClone.WinForms.Helpers;

public static class UtcDateTimeHelper
{
    public static DateTime NormalizeUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
    };

    public static DateTime? NormalizeUtc(DateTime? value) => value.HasValue ? NormalizeUtc(value.Value) : null;

    public static DateTime ToLocal(DateTime value) => NormalizeUtc(value).ToLocalTime();

    public static DateTime? ToLocal(DateTime? value) => value.HasValue ? ToLocal(value.Value) : null;

    public static string FormatLocal(DateTime value, string format = "g") => ToLocal(value).ToString(format);

    public static string FormatLocal(DateTime? value, string format, string fallback) =>
        value.HasValue ? FormatLocal(value.Value, format) : fallback;

    public static TimeSpan GetElapsedSinceUtc(DateTime value, DateTime? nowUtc = null)
    {
        var referenceUtc = nowUtc.HasValue ? NormalizeUtc(nowUtc.Value) : DateTime.UtcNow;
        return referenceUtc - NormalizeUtc(value);
    }
}
