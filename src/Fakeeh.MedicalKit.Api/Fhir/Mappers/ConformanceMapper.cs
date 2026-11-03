using Fakeeh.MedicalKit.Application.VitalTypes;
using Hl7.Fhir.Model;

namespace Fakeeh.MedicalKit.Api.Fhir.Mappers;

public sealed class ConformanceMapper(FhirOptions options)
{
    public CapabilityStatement BuildCapabilityStatement(string baseUrl)
    {
        var observationParams = new List<CapabilityStatement.SearchParamComponent>
        {
            SearchParam("patient", SearchParamType.Reference, "Required. Patient/{id} from the Patient search."),
            SearchParam("code", SearchParamType.Token, "LOINC, vital-type or vital-field code, with or without system."),
            SearchParam("category", SearchParamType.Token, "vital-signs | laboratory"),
            SearchParam("date", SearchParamType.Date, "effectiveDateTime; repeatable; prefixes eq, ge, gt, le, lt."),
            SearchParam("_count", SearchParamType.Number, "Page size (default 20, max 100)."),
            SearchParam("_offset", SearchParamType.Number, "Number of results to skip."),
            SearchParam("_sort", SearchParamType.String, "-date (default) or date.")
        };

        return new CapabilityStatement
        {
            Id = "fakeeh-medical-kit-integration",
            Url = $"{baseUrl}/metadata",
            Name = "FakeehMedicalKitIntegration",
            Title = "Fakeeh HIS - Medical KIT Integration (POC)",
            Status = PublicationStatus.Active,
            Experimental = true,
            DateElement = new FhirDateTime(DateTimeOffset.UtcNow),
            Publisher = "Fakeeh",
            Kind = CapabilityStatementKind.Instance,
            Software = new CapabilityStatement.SoftwareComponent { Name = "Fakeeh.MedicalKit.Api", Version = "0.1.0" },
            Implementation = new CapabilityStatement.ImplementationComponent
            {
                Description = "FHIR facade over the Medical KIT readings store and the Fakeeh HIS patient API.",
                Url = baseUrl
            },
            FhirVersion = FHIRVersion.N4_0_1,
            Format = ["json"],
            Rest =
            [
                new CapabilityStatement.RestComponent
                {
                    Mode = CapabilityStatement.RestfulCapabilityMode.Server,
                    Security = new CapabilityStatement.SecurityComponent
                    {
                        Description = "Send the API key in the X-Api-Key header. /metadata is public."
                    },
                    Interaction =
                    [
                        new CapabilityStatement.SystemInteractionComponent
                        {
                            Code = CapabilityStatement.SystemRestfulInteraction.Batch,
                            Documentation = "POST [base] with a batch Bundle of Observation creates. Each entry succeeds or fails on its own."
                        }
                    ],
                    Resource =
                    [
                        new CapabilityStatement.ResourceComponent
                        {
                            Type = "Patient",
                            Documentation = "Read-only proxy to Fakeeh HIS.",
                            Interaction = [Interaction(CapabilityStatement.TypeRestfulInteraction.Read), Interaction(CapabilityStatement.TypeRestfulInteraction.SearchType)],
                            SearchParam =
                            [
                                SearchParam("identifier", SearchParamType.Token,
                                    $"{options.NationalIdSystem}|{{id}} or {options.IqamaSystem}|{{id}}")
                            ]
                        },
                        new CapabilityStatement.ResourceComponent
                        {
                            Type = "Observation",
                            Documentation = "Medical KIT device readings.",
                            ConditionalCreate = true,
                            Interaction =
                            [
                                Interaction(CapabilityStatement.TypeRestfulInteraction.Read),
                                Interaction(CapabilityStatement.TypeRestfulInteraction.Create),
                                Interaction(CapabilityStatement.TypeRestfulInteraction.SearchType)
                            ],
                            SearchParam = observationParams
                        },
                        new CapabilityStatement.ResourceComponent
                        {
                            Type = "CodeSystem",
                            Documentation = $"CodeSystem/{FhirConstants.VitalTypeCodeSystemId} lists the vital types and their fields.",
                            Interaction = [Interaction(CapabilityStatement.TypeRestfulInteraction.Read)]
                        }
                    ]
                }
            ]
        };
    }

    public CodeSystem BuildVitalTypeCodeSystem(IReadOnlyList<VitalTypeDto> types)
    {
        var codeSystem = new CodeSystem
        {
            Id = FhirConstants.VitalTypeCodeSystemId,
            Url = options.VitalTypeSystem,
            Name = "MedicalKitVitalType",
            Title = "Medical KIT vital types",
            Status = PublicationStatus.Active,
            Content = CodeSystemContentMode.Complete,
            Description = "Vital types recorded by the Medical KIT. Nested concepts are the fields of each type; " +
                          $"their 'fieldKey' property is the code used with {options.VitalFieldSystem}.",
            Property =
            [
                PropertyDefinition("category", CodeSystem.PropertyType.Code, "FHIR observation category"),
                PropertyDefinition("observationModel", CodeSystem.PropertyType.Code, "SingleValue | Components | PerField"),
                PropertyDefinition("loinc", CodeSystem.PropertyType.Code, "LOINC code, when confirmed"),
                PropertyDefinition("fieldKey", CodeSystem.PropertyType.Code, "Field key as sent by the KIT"),
                PropertyDefinition("unit", CodeSystem.PropertyType.String, "Display unit"),
                PropertyDefinition("ucum", CodeSystem.PropertyType.Code, "UCUM unit code"),
                PropertyDefinition("required", CodeSystem.PropertyType.Boolean, "Field is required")
            ]
        };

        foreach (var type in types)
        {
            var concept = new CodeSystem.ConceptDefinitionComponent { Code = type.Code, Display = type.Name };
            concept.Property.Add(Property("category", new Code(type.Category)));
            concept.Property.Add(Property("observationModel", new Code(type.ObservationModel.ToString())));
            if (type.LoincCode is not null)
            {
                concept.Property.Add(Property("loinc", new Code(type.LoincCode)));
            }

            foreach (var field in type.Fields)
            {
                var child = new CodeSystem.ConceptDefinitionComponent { Code = $"{type.Code}.{field.Key}", Display = field.Label };
                child.Property.Add(Property("fieldKey", new Code(field.Key)));
                child.Property.Add(Property("unit", new FhirString(field.Unit)));
                child.Property.Add(Property("ucum", new Code(field.UcumCode)));
                child.Property.Add(Property("required", new FhirBoolean(field.Required)));
                if (field.LoincCode is not null)
                {
                    child.Property.Add(Property("loinc", new Code(field.LoincCode)));
                }

                concept.Concept.Add(child);
            }

            codeSystem.Concept.Add(concept);
        }

        codeSystem.Count = codeSystem.Concept.Count;
        return codeSystem;
    }

    private static CapabilityStatement.ResourceInteractionComponent Interaction(CapabilityStatement.TypeRestfulInteraction code) =>
        new() { Code = code };

    private static CapabilityStatement.SearchParamComponent SearchParam(string name, SearchParamType type, string documentation) =>
        new() { Name = name, Type = type, Documentation = new Markdown(documentation) };

    private static CodeSystem.PropertyComponent PropertyDefinition(string code, CodeSystem.PropertyType type, string description) =>
        new() { Code = code, Type = type, Description = description };

    private static CodeSystem.ConceptPropertyComponent Property(string code, DataType value) =>
        new() { Code = code, Value = value };
}
