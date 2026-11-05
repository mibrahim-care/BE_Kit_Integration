using Fakeeh.MedicalKit.Domain.Entities;
using Fakeeh.MedicalKit.Domain.Enums;

namespace Fakeeh.MedicalKit.Application.Observations;

internal static class ObservationMappings
{
    /// <summary>
    /// Shapes a stored reading as one FHIR Observation. Readings are stored with their values as components
    /// (whether they came from AddVitals or from FHIR); single-value and per-field types are turned back into
    /// one valueQuantity here. Older rows that kept the value on the reading itself are handled too.
    /// </summary>
    public static ObservationDto ToObservationDto(this Reading reading, VitalType? type)
    {
        var model = type?.ObservationModel;
        var fieldKey = reading.FieldKey;
        decimal? value = reading.Value;
        var unit = reading.Unit;
        var ucum = reading.UcumCode;
        var components = reading.Components;

        if (value is null && (model is ObservationModel.SingleValue or ObservationModel.PerField) && components.Count == 1)
        {
            var only = components[0];
            value = only.Value;
            unit = only.Unit;
            ucum = only.UcumCode;
            fieldKey = model == ObservationModel.PerField ? only.FieldKey : null;
            components = [];
        }
        else if (model == ObservationModel.SingleValue)
        {
            fieldKey = null;
        }

        var field = fieldKey is null ? null : type?.FindField(fieldKey);

        return new ObservationDto(
            reading.Id,
            reading.ExternalId,
            reading.PatientId,
            reading.NationalId,
            reading.VitalTypeCode,
            type?.Name ?? reading.VitalTypeCode,
            type?.LoincCode,
            model,
            fieldKey,
            field?.Label,
            field?.LoincCode,
            reading.Category,
            reading.Status,
            reading.EffectiveAt,
            value is null ? null : Trim(value.Value),
            unit,
            ucum,
            components
                .Select(c => new ObservationComponentDto(
                    c.FieldKey,
                    type?.FindField(c.FieldKey)?.Label ?? c.FieldKey,
                    c.LoincCode,
                    Trim(c.Value),
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

    // Values come back from decimal(18,4) columns as e.g. 13.0000; drop the trailing zeros.
    private static decimal Trim(decimal value) => value / 1.000000000000000000000000000000000m;
}
