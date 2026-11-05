namespace Fakeeh.MedicalKit.Api.Fhir;

public sealed class FhirOptions
{
    public const string SectionName = "Fhir";

    public string NationalIdSystem { get; set; } = "http://nphies.sa/identifier/nationalid";

    public string IqamaSystem { get; set; } = "http://nphies.sa/identifier/iqama";

    public string MrnSystem { get; set; } = "http://fakeeh.example/his/mrn";

    public string ReadingIdSystem { get; set; } = "http://fakeeh.example/medical-kit/reading-id";

    public string VitalTypeSystem { get; set; } = "http://fakeeh.example/medical-kit/vital-type";

    public string VitalFieldSystem { get; set; } = "http://fakeeh.example/medical-kit/vital-field";

    public string AddressExtensionUrl { get; set; } = "http://fakeeh.example/fhir/StructureDefinition/reading-address";

    /// <summary>How long the opaque paging links (next / previous / self) of a search stay valid.</summary>
    public TimeSpan PageLinkLifetime { get; set; } = TimeSpan.FromHours(1);
}

public static class FhirConstants
{
    public const string RoutePrefix = "fhir";
    public const string ContentType = "application/fhir+json";
    public const string LoincSystem = "http://loinc.org";
    public const string UcumSystem = "http://unitsofmeasure.org";
    public const string ObservationCategorySystem = "http://terminology.hl7.org/CodeSystem/observation-category";
    public const string InterpretationSystem = "http://terminology.hl7.org/CodeSystem/v3-ObservationInterpretation";
    public const string VitalTypeCodeSystemId = "medical-kit-vital-type";

    /// <summary>True for every request under /fhir: those get FHIR (OperationOutcome) error bodies.</summary>
    public static bool IsFhirRequest(HttpRequest request) => request.Path.StartsWithSegments("/" + RoutePrefix);

    public static Hl7.Fhir.Model.CodeableConcept Concept(string system, string code, string? display) => new()
    {
        Coding = [new Hl7.Fhir.Model.Coding(system, code, display)]
    };
}
