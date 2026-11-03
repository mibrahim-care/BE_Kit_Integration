using Fakeeh.MedicalKit.Domain.Entities;

namespace Fakeeh.MedicalKit.Application.Readings;

internal static class ReadingMappings
{
    public static VitalReadingDto ToDto(this Reading reading, VitalType? type) => new(
        reading.Id,
        reading.ExternalId,
        reading.EffectiveAt,
        ValuesOf(reading, type),
        reading.Interpretation switch
        {
            null or "" => null,
            "N" => false,
            _ => true
        },
        reading.Note,
        reading.Address,
        reading.DeviceRef,
        reading.PerformerRef,
        reading.ReceivedAt);

    private static Dictionary<string, decimal> ValuesOf(Reading reading, VitalType? type)
    {
        if (reading.Components.Count > 0)
        {
            return reading.Components.ToDictionary(c => c.FieldKey, c => Trim(c.Value));
        }

        // Readings stored before the KIT format was adopted kept a single value on the reading itself.
        if (reading.Value is { } value)
        {
            var key = reading.FieldKey ?? type?.Fields.OrderBy(f => f.SortOrder).FirstOrDefault()?.Key ?? "Value";
            return new Dictionary<string, decimal> { [key] = Trim(value) };
        }

        return [];
    }

    // Values come back from decimal(18,4) columns as e.g. 13.0000; drop the trailing zeros.
    private static decimal Trim(decimal value) => value / 1.000000000000000000000000000000000m;
}
