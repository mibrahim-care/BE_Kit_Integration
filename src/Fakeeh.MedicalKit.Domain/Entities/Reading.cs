namespace Fakeeh.MedicalKit.Domain.Entities;

public class Reading
{
    public Guid Id { get; set; }

    public string ExternalId { get; set; } = null!;

    public string PatientId { get; set; } = null!;

    public string? NationalId { get; set; }

    public string VitalTypeCode { get; set; } = null!;

    public string? FieldKey { get; set; }

    public string Category { get; set; } = null!;

    public string Status { get; set; } = "final";

    public DateTimeOffset EffectiveAt { get; set; }

    public decimal? Value { get; set; }

    public string? Unit { get; set; }

    public string? UcumCode { get; set; }

    public List<ReadingComponent> Components { get; set; } = [];

    public string? Interpretation { get; set; }

    public string? Note { get; set; }

    public string? Address { get; set; }

    public string? PerformerRef { get; set; }

    public string? DeviceRef { get; set; }

    public DateTimeOffset ReceivedAt { get; set; }
}
