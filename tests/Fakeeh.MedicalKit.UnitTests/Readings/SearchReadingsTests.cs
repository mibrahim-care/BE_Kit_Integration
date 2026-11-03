using Fakeeh.MedicalKit.Application.Readings;

namespace Fakeeh.MedicalKit.UnitTests.Readings;

public sealed class SearchReadingsTests : IAsyncLifetime
{
    private readonly TestHost _host = new();

    public async Task InitializeAsync()
    {
        await Record("S-hr-1", "HR", 70, "2026-10-01T08:00:00+03:00");
        await Record("S-hr-2", "HR", 75, "2026-10-05T08:00:00+03:00");
        await Record("S-spo2", "SPO2", 98, "2026-10-03T08:00:00+03:00", "%");
        await _host.SendAsync(new RecordReadingCommand(new ReadingInputDto
        {
            ExternalId = "S-hb",
            PatientId = "1001",
            VitalTypeCode = "BT",
            FieldKey = "hb_gdL",
            Value = 13,
            UcumCode = "g/dL",
            EffectiveDateTime = "2026-10-04T08:00:00+03:00"
        }));
    }

    public async Task DisposeAsync() => await _host.DisposeAsync();

    [Fact]
    public async Task Results_are_newest_first_and_paged()
    {
        var page = await _host.SendAsync(new SearchReadingsQuery { PatientId = "1001", Count = 2 });

        Assert.Equal(4, page.Value.Total);
        Assert.Equal(["S-hr-2", "S-hb"], page.Value.Items.Select(r => r.ExternalId));
        Assert.True(page.Value.HasNext);
    }

    [Theory]
    [InlineData(CodeFilterSystem.Any, "HR", 2)]
    [InlineData(CodeFilterSystem.Loinc, "8867-4", 2)]
    [InlineData(CodeFilterSystem.Loinc, "718-7", 1)]
    [InlineData(CodeFilterSystem.VitalField, "hb_gdL", 1)]
    [InlineData(CodeFilterSystem.VitalType, "BT", 1)]
    [InlineData(CodeFilterSystem.Any, "NOPE", 0)]
    public async Task Code_filter_matches_local_and_loinc_codes(CodeFilterSystem system, string code, int expected)
    {
        var page = await _host.SendAsync(new SearchReadingsQuery { PatientId = "1001", Code = new CodeFilter(system, code) });

        Assert.Equal(expected, page.Value.Total);
    }

    [Fact]
    public async Task Date_range_and_category_filter()
    {
        var page = await _host.SendAsync(new SearchReadingsQuery
        {
            PatientId = "1001",
            Category = "vital-signs",
            From = new DateTimeOffset(2026, 10, 2, 0, 0, 0, TimeSpan.FromHours(3)),
            To = new DateTimeOffset(2026, 10, 6, 0, 0, 0, TimeSpan.FromHours(3))
        });

        Assert.Equal(["S-hr-2", "S-spo2"], page.Value.Items.Select(r => r.ExternalId));
    }

    [Fact]
    public async Task Count_over_the_maximum_is_rejected()
    {
        var result = await _host.SendAsync(new SearchReadingsQuery { PatientId = "1001", Count = 500 });

        Assert.Equal("search.count_invalid", result.Error.Code);
    }

    private Task Record(string id, string type, decimal value, string effective, string? ucum = null) =>
        _host.SendAsync(new RecordReadingCommand(new ReadingInputDto
        {
            ExternalId = id,
            PatientId = "1001",
            VitalTypeCode = type,
            Value = value,
            UcumCode = ucum,
            EffectiveDateTime = effective
        }));
}
