using Fakeeh.MedicalKit.Api.Fhir;
using Hl7.Fhir.Model;
using Microsoft.Extensions.DependencyInjection;

namespace Fakeeh.MedicalKit.UnitTests.Fhir;

public sealed class BatchProcessorTests : IAsyncLifetime
{
    private readonly TestHost _host = new();

    public System.Threading.Tasks.Task InitializeAsync() => System.Threading.Tasks.Task.CompletedTask;

    public async System.Threading.Tasks.Task DisposeAsync() => await _host.DisposeAsync();

    [Fact]
    public async System.Threading.Tasks.Task Each_entry_succeeds_or_fails_on_its_own()
    {
        var batch = new Bundle { Type = Bundle.BundleType.Batch };
        batch.Entry.Add(Entry(HeartRate("B-1", "2026-10-18T07:58:42+03:00")));
        batch.Entry.Add(Entry(HeartRate("B-2", "2026-10-18T07:58:42")));
        batch.Entry.Add(Entry(HeartRate("B-1", "2026-10-18T07:58:42+03:00")));
        batch.Entry.Add(new Bundle.EntryComponent
        {
            Resource = new Patient(),
            Request = new Bundle.RequestComponent { Method = Bundle.HTTPVerb.PUT, Url = "Patient/1" }
        });

        var response = await Process(batch);

        Assert.Equal(Bundle.BundleType.BatchResponse, response.Type);
        Assert.Equal(["201 Created", "400 Bad Request", "200 OK", "405 Method Not Allowed"], response.Entry.Select(e => e.Response.Status));
        Assert.Equal(response.Entry[0].Response.Location, response.Entry[2].Response.Location);

        var outcome = Assert.IsType<OperationOutcome>(response.Entry[1].Response.Outcome);
        Assert.Contains(outcome.Issue, i => i.Details.Text == "reading.effective_timezone");
    }

    [Theory]
    [InlineData("identifier=http://fakeeh.example/medical-kit/reading-id|KIT-1", "KIT-1")]
    [InlineData("identifier=KIT-2", "KIT-2")]
    [InlineData("?identifier=http%3A%2F%2Fx%7CKIT-3", "KIT-3")]
    [InlineData("status=final", null)]
    [InlineData(null, null)]
    public void If_none_exist_identifier_is_parsed(string? ifNoneExist, string? expected) =>
        Assert.Equal(expected, BatchProcessor.ExternalIdFromIfNoneExist(ifNoneExist));

    private Task<Bundle> Process(Bundle batch) =>
        _host.InScopeAsync(sp => sp.GetRequiredService<BatchProcessor>().ProcessAsync(batch, CancellationToken.None));

    private Observation HeartRate(string id, string effective)
    {
        var observation = new Observation
        {
            Status = ObservationStatus.Final,
            Code = new CodeableConcept(_host.Options.VitalTypeSystem, "HR"),
            Subject = new ResourceReference("Patient/1001"),
            Effective = new FhirDateTime(effective),
            Value = new Quantity(72, "/min", FhirConstants.UcumSystem)
        };
        observation.Identifier.Add(new Identifier(_host.Options.ReadingIdSystem, id));
        return observation;
    }

    private static Bundle.EntryComponent Entry(Observation observation) => new()
    {
        Resource = observation,
        Request = new Bundle.RequestComponent { Method = Bundle.HTTPVerb.POST, Url = "Observation" }
    };
}
