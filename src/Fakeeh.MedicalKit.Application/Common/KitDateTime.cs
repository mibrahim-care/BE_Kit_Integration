using System.Globalization;
using System.Text.RegularExpressions;

namespace Fakeeh.MedicalKit.Application.Common;

internal static partial class KitDateTime
{
    /// <summary>
    /// Parses an ISO 8601 date-time such as 2026-10-18T07:58:42.63689 or 2026-10-18T07:58:42+03:00.
    /// When no offset is given, <paramref name="defaultOffset"/> is assumed.
    /// </summary>
    public static bool TryParse(string? value, TimeSpan defaultOffset, out DateTimeOffset result)
    {
        result = default;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var match = Pattern().Match(value.Trim());
        if (!match.Success)
        {
            return false;
        }

        if (match.Groups["offset"].Success)
        {
            return DateTimeOffset.TryParse(value.Trim(), CultureInfo.InvariantCulture, DateTimeStyles.None, out result);
        }

        if (!DateTime.TryParse(value.Trim(), CultureInfo.InvariantCulture, DateTimeStyles.None, out var local))
        {
            return false;
        }

        result = new DateTimeOffset(DateTime.SpecifyKind(local, DateTimeKind.Unspecified), defaultOffset);
        return true;
    }

    [GeneratedRegex(@"^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}(:\d{2}(\.\d+)?)?(?<offset>Z|[+-]\d{2}:\d{2})?$")]
    private static partial Regex Pattern();
}
