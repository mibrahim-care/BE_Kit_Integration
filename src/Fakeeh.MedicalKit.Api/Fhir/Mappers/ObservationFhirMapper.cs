using Fakeeh.MedicalKit.Application.Observations;
using Hl7.Fhir.Model;

namespace Fakeeh.MedicalKit.Api.Fhir.Mappers;

public sealed class ObservationFhirMapper(FhirOptions options)
{
    private static readonly Dictionary<string, string> InterpretationDisplays = new(StringComparer.OrdinalIgnoreCase)
    {
        ["N"] = "Normal",
        ["A"] = "Abnormal",
        ["AA"] = "Critical abnormal",
        ["H"] = "High",
        ["HH"] = "Critical high",
        ["L"] = "Low",
        ["LL"] = "Critical low"
    };

    public ObservationInputDto ToInput(Observation observation, string? fallbackExternalId = null)
    {
        var codings = observation.Code?.Coding ?? [];
        var quantity = observation.Value as Quantity;

        return new ObservationInputDto
        {
            ExternalId = ExternalIdOf(observation) ?? fallbackExternalId ?? string.Empty,
            PatientId = ReferenceId(observation.Subject?.Reference, "Patient") ?? string.Empty,
            VitalTypeCode = CodeFor(codings, options.VitalTypeSystem),
            FieldKey = CodeFor(codings, options.VitalFieldSystem),
            LoincCode = CodeFor(codings, FhirConstants.LoincSystem),
            Status = observation.StatusElement?.ObjectValue as string,
            EffectiveDateTime = observation.Effective switch
            {
                FhirDateTime dateTime => dateTime.Value,
                Instant instant => instant.ObjectValue?.ToString(),
                _ => null
            },
            Value = quantity?.Value,
            Unit = quantity?.Unit,
            UcumCode = UcumCodeOf(quantity),
            Components = observation.Component
                .Select(c =>
                {
                    var componentCodings = c.Code?.Coding ?? [];
                    var componentQuantity = c.Value as Quantity;
                    return new ObservationComponentInputDto(
                        CodeFor(componentCodings, options.VitalFieldSystem),
                        CodeFor(componentCodings, FhirConstants.LoincSystem),
                        componentQuantity?.Value,
                        componentQuantity?.Unit,
                        UcumCodeOf(componentQuantity));
                })
                .ToList(),
            Interpretation = observation.Interpretation
                .SelectMany(i => i.Coding)
                .Select(c => c.Code)
                .FirstOrDefault(c => !string.IsNullOrWhiteSpace(c)),
            Note = observation.Note.Count == 0
                ? null
                : string.Join(Environment.NewLine, observation.Note.Select(n => n.Text).Where(t => !string.IsNullOrWhiteSpace(t))),
            Address = (observation.GetExtension(options.AddressExtensionUrl)?.Value as FhirString)?.Value,
            PerformerRef = observation.Performer.FirstOrDefault()?.Reference,
            DeviceRef = observation.Device?.Reference
        };
    }

    public Observation ToFhir(ObservationDto reading)
    {
        var observation = new Observation
        {
            Id = reading.Id.ToString(),
            Meta = new Meta { LastUpdated = reading.ReceivedAt },
            Status = reading.Status switch
            {
                "amended" => ObservationStatus.Amended,
                "corrected" => ObservationStatus.Corrected,
                "preliminary" => ObservationStatus.Preliminary,
                _ => ObservationStatus.Final
            },
            Subject = new ResourceReference($"Patient/{reading.PatientId}")
            {
                Identifier = reading.NationalId is null ? null : new Identifier { Value = reading.NationalId }
            },
            Effective = new FhirDateTime(reading.EffectiveAt),
            Code = BuildCode(reading)
        };

        observation.Identifier.Add(new Identifier(options.ReadingIdSystem, reading.ExternalId));

        observation.Category.Add(FhirConstants.Concept(
            FhirConstants.ObservationCategorySystem,
            reading.Category,
            reading.Category == "laboratory" ? "Laboratory" : "Vital Signs"));

        if (reading.Value is { } value)
        {
            observation.Value = Quantity(value, reading.Unit, reading.UcumCode);
        }

        foreach (var component in reading.Components)
        {
            var code = new CodeableConcept { Text = component.Label };
            if (component.LoincCode is not null)
            {
                code.Coding.Add(new Coding(FhirConstants.LoincSystem, component.LoincCode, component.Label));
            }

            code.Coding.Add(new Coding(options.VitalFieldSystem, component.FieldKey, component.Label));

            observation.Component.Add(new Observation.ComponentComponent
            {
                Code = code,
                Value = Quantity(component.Value, component.Unit, component.UcumCode)
            });
        }

        if (!string.IsNullOrWhiteSpace(reading.Interpretation))
        {
            observation.Interpretation.Add(FhirConstants.Concept(
                FhirConstants.InterpretationSystem,
                reading.Interpretation,
                InterpretationDisplays.GetValueOrDefault(reading.Interpretation)));
        }

        if (!string.IsNullOrWhiteSpace(reading.Note))
        {
            observation.Note.Add(new Annotation { Text = reading.Note });
        }

        if (!string.IsNullOrWhiteSpace(reading.Address))
        {
            observation.AddExtension(options.AddressExtensionUrl, new FhirString(reading.Address));
        }

        if (!string.IsNullOrWhiteSpace(reading.PerformerRef))
        {
            observation.Performer.Add(new ResourceReference(reading.PerformerRef));
        }

        if (!string.IsNullOrWhiteSpace(reading.DeviceRef))
        {
            observation.Device = new ResourceReference(reading.DeviceRef);
        }

        return observation;
    }

    public static string? ReferenceId(string? reference, string resourceType)
    {
        if (string.IsNullOrWhiteSpace(reference))
        {
            return null;
        }

        var parts = reference.TrimEnd('/').Split('/');
        if (parts.Length >= 2 && parts[^2] == resourceType)
        {
            return parts[^1];
        }

        return parts.Length == 1 ? parts[0] : null;
    }

    private CodeableConcept BuildCode(ObservationDto reading)
    {
        var isPerField = reading.FieldKey is not null;
        var loinc = isPerField ? reading.FieldLoincCode : reading.VitalTypeLoincCode;
        var text = isPerField ? $"{reading.VitalTypeName} - {reading.FieldLabel ?? reading.FieldKey}" : reading.VitalTypeName;

        var code = new CodeableConcept { Text = text };
        if (loinc is not null)
        {
            code.Coding.Add(new Coding(FhirConstants.LoincSystem, loinc, isPerField ? reading.FieldLabel : reading.VitalTypeName));
        }

        code.Coding.Add(new Coding(options.VitalTypeSystem, reading.VitalTypeCode, reading.VitalTypeName));

        if (isPerField)
        {
            code.Coding.Add(new Coding(options.VitalFieldSystem, reading.FieldKey, reading.FieldLabel));
        }

        return code;
    }

    private string? ExternalIdOf(Observation observation)
    {
        var identifier = observation.Identifier.FirstOrDefault(i => i.System == options.ReadingIdSystem)
                         ?? observation.Identifier.FirstOrDefault(i => !string.IsNullOrWhiteSpace(i.Value));
        return identifier?.Value;
    }

    private static string? CodeFor(IEnumerable<Coding> codings, string system) =>
        codings.FirstOrDefault(c => string.Equals(c.System, system, StringComparison.OrdinalIgnoreCase))?.Code;

    private static string? UcumCodeOf(Quantity? quantity) =>
        quantity is null || (quantity.System is not null && quantity.System != FhirConstants.UcumSystem)
            ? null
            : quantity.Code;

    private static Quantity Quantity(decimal value, string? unit, string? ucumCode) => new()
    {
        Value = value / 1.000000000000000000000000000000000m,
        Unit = unit,
        System = ucumCode is null ? null : FhirConstants.UcumSystem,
        Code = ucumCode
    };
}
