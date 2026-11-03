using Fakeeh.MedicalKit.Api.Fhir;
using Fakeeh.MedicalKit.Application.Readings;
using Hl7.Fhir.Model;

namespace Fakeeh.MedicalKit.UnitTests.Fhir;

public sealed class ObservationFhirMapperTests : IAsyncLifetime
{
    private readonly TestHost _host = new();

    public System.Threading.Tasks.Task InitializeAsync() => System.Threading.Tasks.Task.CompletedTask;

    public async System.Threading.Tasks.Task DisposeAsync() => await _host.DisposeAsync();

    [Fact]
    public async System.Threading.Tasks.Task Blood_pressure_round_trips_with_components()
    {
        var observation = Observation("RT-BP", "BP");
        observation.Component.Add(Component("8480-6", 121));
        observation.Component.Add(Component("8462-4", 81));
        observation.Note.Add(new Annotation { Text = "note" });
        observation.AddExtension(_host.Options.AddressExtensionUrl, new FhirString("Riyadh"));
        observation.Interpretation.Add(new CodeableConcept(FhirConstants.InterpretationSystem, "H"));

        var result = await RoundTrip(observation);

        Assert.Equal("85354-9", result.Code.Coding.Single(c => c.System == FhirConstants.LoincSystem).Code);
        Assert.Equal([121m, 81m], result.Component.Select(c => ((Quantity)c.Value).Value!.Value));
        Assert.All(result.Component, c => Assert.Equal("mm[Hg]", ((Quantity)c.Value).Code));
        Assert.Equal("note", result.Note.Single().Text);
        Assert.Equal("Riyadh", ((FhirString)result.GetExtension(_host.Options.AddressExtensionUrl).Value).Value);
        Assert.Equal("High", result.Interpretation.Single().Coding.Single().Display);
        Assert.Equal("Patient/1001", result.Subject.Reference);
        Assert.Equal("RT-BP", result.Identifier.Single(i => i.System == _host.Options.ReadingIdSystem).Value);
    }

    [Fact]
    public async System.Threading.Tasks.Task Single_value_round_trips_with_value_quantity()
    {
        var observation = Observation("RT-TEMP", "TEMP");
        observation.Value = new Quantity(37.5m, "Cel", FhirConstants.UcumSystem) { Unit = "°C" };

        var result = await RoundTrip(observation);

        var quantity = Assert.IsType<Quantity>(result.Value);
        Assert.Equal(37.5m, quantity.Value);
        Assert.Equal("Cel", quantity.Code);
        Assert.Equal("Temperature", result.Code.Text);
        Assert.Equal(ObservationStatus.Final, result.Status);
    }

    [Fact]
    public async System.Threading.Tasks.Task Per_field_round_trips_with_field_coding()
    {
        var observation = Observation("RT-BC", "BC");
        observation.Code.Coding.Add(new Coding(_host.Options.VitalFieldSystem, "visceral_fat"));
        observation.Value = new Quantity(20, "{level}", FhirConstants.UcumSystem);

        var result = await RoundTrip(observation);

        Assert.Equal("visceral_fat", result.Code.Coding.Single(c => c.System == _host.Options.VitalFieldSystem).Code);
        Assert.Equal("Body Composition - Visceral fat level", result.Code.Text);
        Assert.DoesNotContain(result.Code.Coding, c => c.System == FhirConstants.LoincSystem);
    }

    [Fact]
    public void Missing_identifier_falls_back_to_if_none_exist()
    {
        var observation = Observation("ignored", "HR");
        observation.Identifier.Clear();

        var input = _host.Mapper.ToInput(
            observation,
            BatchProcessor.ExternalIdFromIfNoneExist("identifier=http://fakeeh.example/medical-kit/reading-id|KIT-9"));

        Assert.Equal("KIT-9", input.ExternalId);
    }

    [Theory]
    [InlineData("Patient/123", "123")]
    [InlineData("https://his.example/fhir/Patient/123", "123")]
    [InlineData("123", "123")]
    [InlineData("Practitioner/3", null)]
    [InlineData(null, null)]
    public void Reference_id_is_extracted(string? reference, string? expected) =>
        Assert.Equal(expected, Api.Fhir.Mappers.ObservationFhirMapper.ReferenceId(reference, "Patient"));

    private async Task<Observation> RoundTrip(Observation observation)
    {
        var recorded = await _host.SendAsync(new RecordReadingCommand(_host.Mapper.ToInput(observation)));
        Assert.True(recorded.IsSuccess, string.Join("; ", recorded.Errors.Select(e => e.Message)));

        var saved = await _host.SendAsync(new GetReadingByIdQuery(recorded.Value.Id));
        return _host.Mapper.ToFhir(saved.Value);
    }

    private Observation Observation(string id, string vitalType)
    {
        var observation = new Observation
        {
            Status = ObservationStatus.Final,
            Code = new CodeableConcept(_host.Options.VitalTypeSystem, vitalType),
            Subject = new ResourceReference("Patient/1001"),
            Effective = new FhirDateTime("2026-10-18T07:58:42+03:00")
        };
        observation.Identifier.Add(new Identifier(_host.Options.ReadingIdSystem, id));
        return observation;
    }

    private static Observation.ComponentComponent Component(string loinc, decimal value) => new()
    {
        Code = new CodeableConcept(FhirConstants.LoincSystem, loinc),
        Value = new Quantity(value, "mm[Hg]", FhirConstants.UcumSystem)
    };
}
