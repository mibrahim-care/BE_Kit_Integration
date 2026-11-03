using Fakeeh.MedicalKit.Api.Fhir;
using Fakeeh.MedicalKit.Api.Fhir.Search;
using Fakeeh.MedicalKit.Application.Readings;

namespace Fakeeh.MedicalKit.UnitTests.Fhir;

public sealed class FhirSearchParserTests
{
    private static readonly TimeSpan Riyadh = TimeSpan.FromHours(3);
    private readonly FhirSearchParser _parser = new(new FhirOptions());

    [Fact]
    public void Date_only_ge_and_le_cover_whole_days()
    {
        var error = _parser.TryParseDates(["ge2026-10-01", "le2026-10-31"], out var range);

        Assert.Null(error);
        Assert.Equal(new DateTimeOffset(2026, 10, 1, 0, 0, 0, Riyadh), range.From);
        Assert.Equal(new DateTimeOffset(2026, 11, 1, 0, 0, 0, Riyadh), range.To);
    }

    [Fact]
    public void Eq_date_is_one_day()
    {
        _parser.TryParseDates(["2026-10-18"], out var range);

        Assert.Equal(new DateTimeOffset(2026, 10, 18, 0, 0, 0, Riyadh), range.From);
        Assert.Equal(new DateTimeOffset(2026, 10, 19, 0, 0, 0, Riyadh), range.To);
    }

    [Fact]
    public void Lt_date_time_keeps_its_offset()
    {
        _parser.TryParseDates(["lt2026-10-18T10:00:00Z"], out var range);

        Assert.Null(range.From);
        Assert.Equal(new DateTimeOffset(2026, 10, 18, 10, 0, 0, TimeSpan.Zero), range.To);
    }

    [Fact]
    public void Invalid_date_returns_an_error() =>
        Assert.NotNull(_parser.TryParseDates(["geyesterday"], out _));

    [Theory]
    [InlineData("BP", CodeFilterSystem.Any, "BP")]
    [InlineData("http://loinc.org|8867-4", CodeFilterSystem.Loinc, "8867-4")]
    [InlineData("http://fakeeh.example/medical-kit/vital-type|BT", CodeFilterSystem.VitalType, "BT")]
    [InlineData("http://fakeeh.example/medical-kit/vital-field|bmi", CodeFilterSystem.VitalField, "bmi")]
    public void Code_tokens_map_to_filters(string token, CodeFilterSystem system, string code)
    {
        Assert.True(_parser.TryParseCodeFilter(token, out var filter));
        Assert.Equal(new CodeFilter(system, code), filter);
    }

    [Fact]
    public void Unknown_code_system_matches_nothing() =>
        Assert.False(_parser.TryParseCodeFilter("http://snomed.info/sct|12345", out _));
}
