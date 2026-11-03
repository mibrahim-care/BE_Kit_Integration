using Fakeeh.MedicalKit.Application.Readings;
using Fakeeh.MedicalKit.Domain.Common;

namespace Fakeeh.MedicalKit.UnitTests.Readings;

public sealed class RecordReadingTests : IAsyncLifetime
{
    private readonly TestHost _host = new();

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync() => await _host.DisposeAsync();

    [Fact]
    public async Task Single_value_reading_is_created()
    {
        var result = await _host.SendAsync(new RecordReadingCommand(HeartRate("R-1", 72)));

        Assert.True(result.IsSuccess);
        Assert.Equal(RecordReadingOutcome.Created, result.Value.Outcome);

        var saved = await _host.SendAsync(new GetReadingByIdQuery(result.Value.Id));
        Assert.Equal("HR", saved.Value.VitalTypeCode);
        Assert.Equal(72m, saved.Value.Value);
        Assert.Equal("/min", saved.Value.UcumCode);
        Assert.Equal("1123456781", saved.Value.NationalId);
        Assert.Equal(new DateTimeOffset(2026, 10, 18, 7, 58, 42, TimeSpan.FromHours(3)), saved.Value.EffectiveAt);
    }

    [Fact]
    public async Task Same_external_id_is_not_stored_twice()
    {
        var first = await _host.SendAsync(new RecordReadingCommand(HeartRate("R-dup", 72)));
        var second = await _host.SendAsync(new RecordReadingCommand(HeartRate("R-dup", 72)));

        Assert.Equal(RecordReadingOutcome.Created, first.Value.Outcome);
        Assert.Equal(RecordReadingOutcome.AlreadyExists, second.Value.Outcome);
        Assert.Equal(first.Value.Id, second.Value.Id);

        var search = await _host.SendAsync(new SearchReadingsQuery { PatientId = "1001" });
        Assert.Equal(1, search.Value.Total);
    }

    [Fact]
    public async Task Vital_type_can_be_identified_by_loinc_only()
    {
        var input = HeartRate("R-loinc", 80) with { VitalTypeCode = null, LoincCode = "8867-4" };

        var result = await _host.SendAsync(new RecordReadingCommand(input));

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task Per_field_reading_needs_a_known_field()
    {
        var input = Base("R-bt") with { VitalTypeCode = "BT", FieldKey = "cholesterol", Value = 5, UcumCode = "mmol/L" };

        var result = await _host.SendAsync(new RecordReadingCommand(input));

        Assert.True(result.IsFailure);
        Assert.Equal("reading.unknown_field", result.Error.Code);
    }

    [Fact]
    public async Task Per_field_reading_is_stored_with_its_field()
    {
        var input = Base("R-hb") with { VitalTypeCode = "BT", FieldKey = "hb_gdL", Value = 13, UcumCode = "g/dL" };

        var result = await _host.SendAsync(new RecordReadingCommand(input));
        var saved = await _host.SendAsync(new GetReadingByIdQuery(result.Value.Id));

        Assert.Equal("hb_gdL", saved.Value.FieldKey);
        Assert.Equal("Hemoglobin", saved.Value.FieldLabel);
        Assert.Equal("718-7", saved.Value.FieldLoincCode);
        Assert.Equal("laboratory", saved.Value.Category);
    }

    [Fact]
    public async Task Components_reading_requires_required_components()
    {
        var input = Base("R-pf") with
        {
            VitalTypeCode = "PF",
            Components = [new ReadingComponentInputDto("fev1_l", null, 3, "L", "L")]
        };

        var result = await _host.SendAsync(new RecordReadingCommand(input));

        Assert.True(result.IsFailure);
        Assert.Contains(result.Errors, e => e.Code == "reading.required_component_missing");
    }

    [Fact]
    public async Task Blood_pressure_components_resolve_by_loinc()
    {
        var input = Base("R-bp") with
        {
            VitalTypeCode = "BP",
            Components =
            [
                new ReadingComponentInputDto(null, "8462-4", 81, "mmHg", "mm[Hg]"),
                new ReadingComponentInputDto(null, "8480-6", 121, "mmHg", "mm[Hg]")
            ]
        };

        var result = await _host.SendAsync(new RecordReadingCommand(input));
        var saved = await _host.SendAsync(new GetReadingByIdQuery(result.Value.Id));

        Assert.Equal(["systolic", "diastolic"], saved.Value.Components.Select(c => c.FieldKey));
        Assert.Equal(121m, saved.Value.Components[0].Value);
    }

    [Fact]
    public async Task Wrong_unit_is_rejected()
    {
        var input = HeartRate("R-unit", 72) with { UcumCode = "Hz" };

        var result = await _host.SendAsync(new RecordReadingCommand(input));

        Assert.Equal("reading.unit_mismatch", result.Error.Code);
    }

    [Fact]
    public async Task Unknown_patient_is_rejected()
    {
        var input = HeartRate("R-patient", 72) with { PatientId = "9999" };

        var result = await _host.SendAsync(new RecordReadingCommand(input));

        Assert.Equal("reading.patient_not_found", result.Error.Code);
        Assert.Equal(ErrorType.Validation, result.Error.Type);
    }

    [Theory]
    [InlineData("2026-10-18T07:58:42.63689")]
    [InlineData("2026-10-18")]
    [InlineData("not a date")]
    [InlineData(null)]
    public async Task Effective_date_time_must_have_a_time_zone(string? effective)
    {
        var input = HeartRate("R-tz", 72) with { EffectiveDateTime = effective };

        var result = await _host.SendAsync(new RecordReadingCommand(input));

        Assert.True(result.IsFailure);
        Assert.Contains(result.Errors, e => e.Code.StartsWith("reading.effective_"));
    }

    [Theory]
    [InlineData("2026-10-18T07:58:42+03:00")]
    [InlineData("2026-10-18T04:58:42Z")]
    [InlineData("2026-10-18T07:58:42.123+03:00")]
    public void Valid_time_zones_are_accepted(string effective) =>
        Assert.True(RecordReadingCommandValidator.HaveTimeZone(effective));

    [Fact]
    public async Task External_id_is_required()
    {
        var result = await _host.SendAsync(new RecordReadingCommand(HeartRate("", 72)));

        Assert.Contains(result.Errors, e => e.Code == "reading.identifier_required");
    }

    private static ReadingInputDto Base(string externalId) => new()
    {
        ExternalId = externalId,
        PatientId = TestHost.Ahmed.Id,
        EffectiveDateTime = "2026-10-18T07:58:42+03:00"
    };

    private static ReadingInputDto HeartRate(string externalId, decimal value) =>
        Base(externalId) with { VitalTypeCode = "HR", Value = value, Unit = "bpm", UcumCode = "/min" };
}
