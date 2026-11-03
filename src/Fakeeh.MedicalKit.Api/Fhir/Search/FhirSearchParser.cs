using System.Globalization;
using Fakeeh.MedicalKit.Application.Readings;

namespace Fakeeh.MedicalKit.Api.Fhir.Search;

public sealed record TokenParam(string? System, string Code);

public sealed record DateRange(DateTimeOffset? From, DateTimeOffset? To);

public sealed class FhirSearchParser(FhirOptions options)
{
    private static readonly string[] DatePrefixes = ["eq", "ge", "gt", "le", "lt"];

    public static TokenParam? ParseToken(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var pipe = value.IndexOf('|');
        return pipe < 0
            ? new TokenParam(null, value.Trim())
            : new TokenParam(NullIfEmpty(value[..pipe]), value[(pipe + 1)..].Trim());
    }

    public bool TryParseCodeFilter(string? value, out CodeFilter? filter)
    {
        filter = null;
        var token = ParseToken(value);
        if (token is null)
        {
            return true;
        }

        CodeFilterSystem? system = token.System switch
        {
            null => CodeFilterSystem.Any,
            FhirConstants.LoincSystem => CodeFilterSystem.Loinc,
            var s when s == options.VitalTypeSystem => CodeFilterSystem.VitalType,
            var s when s == options.VitalFieldSystem => CodeFilterSystem.VitalField,
            _ => null
        };

        if (system is null)
        {
            return false;
        }

        filter = new CodeFilter(system.Value, token.Code);
        return true;
    }

    public string? TryParseDates(IEnumerable<string?> values, out DateRange range)
    {
        DateTimeOffset? from = null;
        DateTimeOffset? to = null;
        range = new DateRange(null, null);

        foreach (var raw in values.Where(v => !string.IsNullOrWhiteSpace(v)))
        {
            var prefix = DatePrefixes.FirstOrDefault(p => raw!.StartsWith(p, StringComparison.OrdinalIgnoreCase)) ?? "eq";
            var text = raw!.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) ? raw[prefix.Length..] : raw;

            if (!TryParseDateValue(text, out var start, out var end))
            {
                return $"Invalid date search value '{raw}'. Use e.g. ge2026-10-01 or le2026-10-18T23:59:59+03:00.";
            }

            switch (prefix.ToLowerInvariant())
            {
                case "eq":
                    from = Max(from, start);
                    to = Min(to, end);
                    break;
                case "ge":
                    from = Max(from, start);
                    break;
                case "gt":
                    from = Max(from, end);
                    break;
                case "le":
                    to = Min(to, end);
                    break;
                case "lt":
                    to = Min(to, start);
                    break;
            }
        }

        range = new DateRange(from, to);
        return null;
    }

    private bool TryParseDateValue(string text, out DateTimeOffset start, out DateTimeOffset end)
    {
        start = end = default;

        if (DateOnly.TryParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
        {
            start = new DateTimeOffset(date.ToDateTime(TimeOnly.MinValue), options.DefaultTimeZoneOffset);
            end = start.AddDays(1);
            return true;
        }

        var styles = DateTimeStyles.AllowWhiteSpaces;
        if (!DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, styles, out var value))
        {
            return false;
        }

        if (!HasOffset(text))
        {
            value = new DateTimeOffset(value.DateTime, options.DefaultTimeZoneOffset);
        }

        start = value;
        end = text.Contains('.') ? value.AddTicks(1) : value.AddSeconds(1);
        return true;
    }

    private static bool HasOffset(string text) =>
        text.EndsWith('Z') || (text.Length > 6 && (text[^6] == '+' || text[^6] == '-') && text[^3] == ':');

    private static DateTimeOffset? Max(DateTimeOffset? current, DateTimeOffset candidate) =>
        current is null || candidate > current ? candidate : current;

    private static DateTimeOffset? Min(DateTimeOffset? current, DateTimeOffset candidate) =>
        current is null || candidate < current ? candidate : current;

    private static string? NullIfEmpty(string value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
