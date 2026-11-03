using Fakeeh.MedicalKit.Domain.Enums;

namespace Fakeeh.MedicalKit.Domain.Entities;

public class VitalType
{
    public const string VitalSignsCategory = "vital-signs";
    public const string LaboratoryCategory = "laboratory";

    public string Code { get; set; } = null!;

    public string Name { get; set; } = null!;

    public bool IsActive { get; set; } = true;

    public string Category { get; set; } = VitalSignsCategory;

    public string? LoincCode { get; set; }

    public ObservationModel ObservationModel { get; set; }

    public List<VitalTypeField> Fields { get; set; } = [];

    public VitalTypeField? FindField(string key) =>
        Fields.FirstOrDefault(f => string.Equals(f.Key, key, StringComparison.OrdinalIgnoreCase));
}
