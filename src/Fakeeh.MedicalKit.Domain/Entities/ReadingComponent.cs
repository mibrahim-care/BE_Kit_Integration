namespace Fakeeh.MedicalKit.Domain.Entities;

public class ReadingComponent
{
    public string FieldKey { get; set; } = null!;

    public string? LoincCode { get; set; }

    public decimal Value { get; set; }

    public string Unit { get; set; } = null!;

    public string UcumCode { get; set; } = null!;
}
