namespace Fakeeh.MedicalKit.Application.Readings;

public sealed class ReadingSettings
{
    public const string SectionName = "Readings";

    /// <summary>Offset applied to KIT date-times sent without one (Saudi Arabia, +03:00).</summary>
    public TimeSpan DefaultTimeZoneOffset { get; set; } = TimeSpan.FromHours(3);

    public int MaxVitalsPerRequest { get; set; } = 100;

    public int MaxRecordsPerType { get; set; } = 50;
}
