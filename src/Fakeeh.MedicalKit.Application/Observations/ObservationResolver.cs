using Fakeeh.MedicalKit.Application.Abstractions;
using Fakeeh.MedicalKit.Domain.Common;
using Fakeeh.MedicalKit.Domain.Entities;
using Fakeeh.MedicalKit.Domain.Enums;

namespace Fakeeh.MedicalKit.Application.Observations;

public sealed record ResolvedObservation(
    VitalType VitalType,
    VitalTypeField? Field,
    IReadOnlyList<ResolvedComponent> Components);

public sealed record ResolvedComponent(VitalTypeField Field, decimal Value);

public enum CodeFilterSystem
{
    Any,
    Loinc,
    VitalType,
    VitalField
}

/// <summary>A parsed FHIR token for the Observation <c>code</c> search parameter.</summary>
public sealed record CodeFilter(CodeFilterSystem System, string Code)
{
    public bool Matches(CodeFilterSystem system, string code) =>
        (System == CodeFilterSystem.Any || System == system)
        && string.Equals(Code, code, StringComparison.OrdinalIgnoreCase);
}

/// <summary>Matches FHIR Observation codes to the configured vital types and validates values and units.</summary>
public sealed class ObservationResolver(IVitalTypeRepository vitalTypes)
{
    public async Task<Result<ResolvedObservation>> ResolveAsync(ObservationInputDto input, CancellationToken cancellationToken)
    {
        var types = await vitalTypes.GetAllActiveAsync(cancellationToken);

        var (type, fieldFromLoinc) = FindType(types, input);
        if (type is null)
        {
            return string.IsNullOrWhiteSpace(input.VitalTypeCode)
                ? ObservationErrors.VitalTypeNotIdentified()
                : ObservationErrors.UnknownVitalType(input.VitalTypeCode);
        }

        return type.ObservationModel switch
        {
            ObservationModel.SingleValue => ResolveSingleValue(type, input),
            ObservationModel.PerField => ResolvePerField(type, fieldFromLoinc, input),
            ObservationModel.Components => ResolveComponents(type, input),
            _ => ObservationErrors.UnknownVitalType(type.Code)
        };
    }

    public async Task<IReadOnlyList<CodeMatch>> ResolveCodeFilterAsync(CodeFilter filter, CancellationToken cancellationToken)
    {
        var types = await vitalTypes.GetAllActiveAsync(cancellationToken);
        var matches = new HashSet<CodeMatch>();

        foreach (var type in types)
        {
            if (filter.Matches(CodeFilterSystem.VitalType, type.Code)
                || (type.LoincCode is not null && filter.Matches(CodeFilterSystem.Loinc, type.LoincCode)))
            {
                matches.Add(new CodeMatch(type.Code, null));
                continue;
            }

            foreach (var field in type.Fields)
            {
                var fieldMatches = filter.Matches(CodeFilterSystem.VitalField, field.Key)
                                   || (field.LoincCode is not null && filter.Matches(CodeFilterSystem.Loinc, field.LoincCode));
                if (fieldMatches)
                {
                    matches.Add(type.ObservationModel == ObservationModel.PerField
                        ? new CodeMatch(type.Code, field.Key)
                        : new CodeMatch(type.Code, null));
                }
            }
        }

        return matches.ToList();
    }

    private static (VitalType? Type, VitalTypeField? Field) FindType(IReadOnlyList<VitalType> types, ObservationInputDto input)
    {
        if (!string.IsNullOrWhiteSpace(input.VitalTypeCode))
        {
            var byCode = types.FirstOrDefault(t => string.Equals(t.Code, input.VitalTypeCode, StringComparison.OrdinalIgnoreCase));
            return (byCode, null);
        }

        if (string.IsNullOrWhiteSpace(input.LoincCode))
        {
            return (null, null);
        }

        var byLoinc = types.FirstOrDefault(t => t.LoincCode == input.LoincCode);
        if (byLoinc is not null)
        {
            return (byLoinc, null);
        }

        foreach (var type in types.Where(t => t.ObservationModel != ObservationModel.Components))
        {
            var field = type.Fields.FirstOrDefault(f => f.LoincCode == input.LoincCode);
            if (field is not null)
            {
                return (type, field);
            }
        }

        return (null, null);
    }

    private static Result<ResolvedObservation> ResolveSingleValue(VitalType type, ObservationInputDto input)
    {
        if (input.Components.Count > 0)
        {
            return ObservationErrors.ComponentsNotAllowed(type.Code);
        }

        var field = type.Fields.OrderBy(f => f.SortOrder).First();
        return ResolveValue(type, field, input);
    }

    private static Result<ResolvedObservation> ResolvePerField(VitalType type, VitalTypeField? fieldFromLoinc, ObservationInputDto input)
    {
        if (input.Components.Count > 0)
        {
            return ObservationErrors.ComponentsNotAllowed(type.Code);
        }

        var field = fieldFromLoinc;
        if (!string.IsNullOrWhiteSpace(input.FieldKey))
        {
            field = type.FindField(input.FieldKey);
        }
        else if (field is null && !string.IsNullOrWhiteSpace(input.LoincCode))
        {
            field = type.Fields.FirstOrDefault(f => f.LoincCode == input.LoincCode);
        }

        if (field is null)
        {
            return string.IsNullOrWhiteSpace(input.FieldKey)
                ? ObservationErrors.FieldRequired(type.Code)
                : ObservationErrors.UnknownField(type.Code, input.FieldKey);
        }

        return ResolveValue(type, field, input);
    }

    private static Result<ResolvedObservation> ResolveValue(VitalType type, VitalTypeField field, ObservationInputDto input)
    {
        if (input.Value is null)
        {
            return ObservationErrors.ValueRequired("valueQuantity");
        }

        if (!UnitMatches(field, input.Unit, input.UcumCode))
        {
            return ObservationErrors.UnitMismatch(field.Key, field.UcumCode, input.UcumCode ?? input.Unit);
        }

        return new ResolvedObservation(type, field, [new ResolvedComponent(field, input.Value.Value)]);
    }

    private static Result<ResolvedObservation> ResolveComponents(VitalType type, ObservationInputDto input)
    {
        if (input.Components.Count == 0)
        {
            return ObservationErrors.ComponentsRequired(type.Code);
        }

        var errors = new List<Error>();
        var resolved = new List<ResolvedComponent>();

        foreach (var component in input.Components)
        {
            var field = !string.IsNullOrWhiteSpace(component.FieldKey)
                ? type.FindField(component.FieldKey)
                : type.Fields.FirstOrDefault(f => f.LoincCode is not null && f.LoincCode == component.LoincCode);

            if (field is null)
            {
                errors.Add(ObservationErrors.UnknownField(type.Code, component.FieldKey ?? component.LoincCode));
                continue;
            }

            if (resolved.Any(r => r.Field.Key == field.Key))
            {
                errors.Add(ObservationErrors.DuplicateComponent(field.Key));
                continue;
            }

            if (component.Value is null)
            {
                errors.Add(ObservationErrors.ValueRequired($"component[{field.Key}]"));
                continue;
            }

            if (!UnitMatches(field, component.Unit, component.UcumCode))
            {
                errors.Add(ObservationErrors.UnitMismatch(field.Key, field.UcumCode, component.UcumCode ?? component.Unit));
                continue;
            }

            resolved.Add(new ResolvedComponent(field, component.Value.Value));
        }

        errors.AddRange(type.Fields
            .Where(f => f.Required && resolved.All(r => r.Field.Key != f.Key))
            .Select(f => ObservationErrors.MissingRequiredComponent(type.Code, f.Key)));

        if (errors.Count > 0)
        {
            return Result.Failure<ResolvedObservation>(errors);
        }

        return new ResolvedObservation(type, null, resolved.OrderBy(r => r.Field.SortOrder).ToList());
    }

    private static bool UnitMatches(VitalTypeField field, string? unit, string? ucumCode)
    {
        if (!string.IsNullOrWhiteSpace(ucumCode))
        {
            return string.Equals(ucumCode, field.UcumCode, StringComparison.Ordinal);
        }

        if (!string.IsNullOrWhiteSpace(unit))
        {
            return string.Equals(unit, field.Unit, StringComparison.OrdinalIgnoreCase)
                   || string.Equals(unit, field.UcumCode, StringComparison.Ordinal);
        }

        return true;
    }
}
