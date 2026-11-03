using Fakeeh.MedicalKit.Domain.Entities;

namespace Fakeeh.MedicalKit.Application.Readings;

internal static class ReadingMappings
{
    public static ReadingDto ToDto(this Reading reading, VitalType? type)
    {
        var field = reading.FieldKey is null ? null : type?.FindField(reading.FieldKey);

        return new ReadingDto(
            reading.Id,
            reading.ExternalId,
            reading.PatientId,
            reading.NationalId,
            reading.VitalTypeCode,
            type?.Name ?? reading.VitalTypeCode,
            type?.LoincCode,
            reading.FieldKey,
            field?.Label,
            field?.LoincCode,
            reading.Category,
            reading.Status,
            reading.EffectiveAt,
            reading.Value,
            reading.Unit,
            reading.UcumCode,
            reading.Components
                .Select(c => new ReadingComponentDto(
                    c.FieldKey,
                    type?.FindField(c.FieldKey)?.Label ?? c.FieldKey,
                    c.LoincCode,
                    c.Value,
                    c.Unit,
                    c.UcumCode))
                .ToList(),
            reading.Interpretation,
            reading.Note,
            reading.Address,
            reading.PerformerRef,
            reading.DeviceRef,
            reading.ReceivedAt);
    }
}
