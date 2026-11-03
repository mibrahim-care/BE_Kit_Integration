using Fakeeh.MedicalKit.Domain.Entities;
using Fakeeh.MedicalKit.Domain.Enums;

namespace Fakeeh.MedicalKit.Application.VitalTypes;

public sealed record VitalTypeDto(
    string Code,
    string Name,
    string Category,
    string? LoincCode,
    ObservationModel ObservationModel,
    IReadOnlyList<VitalTypeFieldDto> Fields)
{
    public static VitalTypeDto From(VitalType type) => new(
        type.Code,
        type.Name,
        type.Category,
        type.LoincCode,
        type.ObservationModel,
        type.Fields
            .OrderBy(f => f.SortOrder)
            .Select(f => new VitalTypeFieldDto(f.Key, f.Label, f.Unit, f.UcumCode, f.LoincCode, f.Required))
            .ToList());
}

public sealed record VitalTypeFieldDto(
    string Key,
    string Label,
    string Unit,
    string UcumCode,
    string? LoincCode,
    bool Required);
