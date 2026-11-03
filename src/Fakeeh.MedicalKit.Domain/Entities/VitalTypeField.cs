namespace Fakeeh.MedicalKit.Domain.Entities;

public class VitalTypeField
{
    public int Id { get; set; }

    public string VitalTypeCode { get; set; } = null!;

    public string Key { get; set; } = null!;

    public string Label { get; set; } = null!;

    public string Unit { get; set; } = null!;

    public string UcumCode { get; set; } = null!;

    public string? LoincCode { get; set; }

    public bool Required { get; set; }

    public int SortOrder { get; set; }
}
