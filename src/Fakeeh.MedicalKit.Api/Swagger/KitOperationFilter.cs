using System.Text.Json.Nodes;
using Fakeeh.MedicalKit.Api.Fhir;
using Fakeeh.MedicalKit.Api.Fhir.Mappers;
using Hl7.Fhir.Model;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Fakeeh.MedicalKit.Api.Swagger;

/// <summary>
/// Describes the FHIR endpoints the way the KIT collection calls them. The actions read their bodies themselves
/// (FHIR JSON or a search form), so request bodies, search fields, responses and examples are added here.
/// Search fields come from the server's own CapabilityStatement, so Swagger and /metadata never disagree.
/// </summary>
public sealed class KitOperationFilter(ConformanceMapper conformance) : IOperationFilter
{
    private const string FormContentType = "application/x-www-form-urlencoded";

    private readonly Lazy<CapabilityStatement> _capabilities = new(() => conformance.BuildCapabilityStatement("/fhir"));

    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        var kit = KitSwaggerContent.Instance.Find(context.ApiDescription);
        if (kit is null)
        {
            return;
        }

        operation.Summary = kit.Summary;
        operation.OperationId = OperationId(kit);

        var spec = Specs.GetValueOrDefault($"{kit.Method} {kit.Path}")
                   ?? throw new InvalidOperationException($"No Swagger spec for {kit.Method} {kit.Path}.");

        if (spec.Public)
        {
            // /metadata needs no X-Api-Key: an empty list overrides the document-wide requirement.
            operation.Security = [];
        }

        DescribePathParameters(operation, kit);
        AddHeaders(operation, kit);

        if (spec.RequestResource is { } requestResource)
        {
            operation.RequestBody = JsonRequestBody(kit, requestResource);
        }
        else if (spec.SearchResource is { } searchResource)
        {
            operation.RequestBody = FormRequestBody(kit, searchResource);
        }

        operation.Responses = Responses(kit, spec);
    }

    private static string OperationId(KitOperation kit) => kit.Path switch
    {
        "/fhir/metadata" => "capabilities",
        "/fhir/Patient/_search" => "searchPatient",
        "/fhir/CodeSystem/medical-kit-vital-type" => "getVitalTypes",
        "/fhir" => "sendReadingsBatch",
        "/fhir/Observation" => "sendReading",
        "/fhir/Observation/_search" => "searchReadings",
        _ => $"{kit.Method.ToLowerInvariant()}{kit.Path.Replace("/", "_")}"
    };

    private static void DescribePathParameters(OpenApiOperation operation, KitOperation kit)
    {
        if (operation.Parameters is null)
        {
            return;
        }

        // GET /CodeSystem/{id}: the only id the collection uses (and the server knows) is the vital-type CodeSystem.
        foreach (var parameter in operation.Parameters.OfType<OpenApiParameter>().Where(p => p.In == ParameterLocation.Path))
        {
            var value = kit.Path.Split('/').Last();
            parameter.Description = $"Always `{value}`.";
            parameter.Schema = new OpenApiSchema
            {
                Type = JsonSchemaType.String,
                Enum = [JsonValue.Create(value)!],
                Default = JsonValue.Create(value)
            };
            parameter.Example = JsonValue.Create(value);
        }
    }

    private static void AddHeaders(OpenApiOperation operation, KitOperation kit)
    {
        foreach (var header in kit.Headers)
        {
            operation.Parameters ??= [];

            // The action may already declare the header ([FromHeader]); describe that parameter instead of adding a second one.
            var existing = operation.Parameters.OfType<OpenApiParameter>()
                .FirstOrDefault(p => p.In == ParameterLocation.Header && string.Equals(p.Name, header.Name, StringComparison.OrdinalIgnoreCase));
            var parameter = existing ?? new OpenApiParameter { Name = header.Name, In = ParameterLocation.Header };

            parameter.Required = false;
            parameter.Description = header.Name == "If-None-Exist"
                ? "Optional. `identifier=<reading id system>|<reading id>`. The reading is not stored twice: " +
                  "if a reading with this id exists the API returns 200 with it instead of 201. " +
                  "Observation.identifier gives the same protection."
                : parameter.Description;
            parameter.Schema = new OpenApiSchema { Type = JsonSchemaType.String };
            parameter.Example = JsonValue.Create(header.Example);

            if (existing is null)
            {
                operation.Parameters.Add(parameter);
            }
        }
    }

    private static OpenApiRequestBody JsonRequestBody(KitOperation kit, string resourceType) => new()
    {
        Required = true,
        Description = $"A FHIR R4 {resourceType} as `{FhirConstants.ContentType}`. Pick an example from the list.",
        Content = new Dictionary<string, OpenApiMediaType>
        {
            [FhirConstants.ContentType] = new()
            {
                Schema = FhirResourceSchema(resourceType),
                Examples = Examples(kit.Requests.Select(r => (r.Name, r.Description, r.Body)))
            }
        }
    };

    private OpenApiRequestBody FormRequestBody(KitOperation kit, string resourceType)
    {
        var searchParams = _capabilities.Value.Rest[0].Resource
            .Single(r => r.Type == resourceType)
            .SearchParam;

        var properties = new Dictionary<string, IOpenApiSchema>();
        var encoding = new Dictionary<string, OpenApiEncoding>();

        foreach (var param in searchParams)
        {
            var description = param.Documentation;
            properties[param.Name] = param.Type switch
            {
                // date is repeatable (ge… and le… for a range): one form field per value.
                SearchParamType.Date => new OpenApiSchema
                {
                    Type = JsonSchemaType.Array,
                    Items = new OpenApiSchema { Type = JsonSchemaType.String },
                    Description = description
                },
                SearchParamType.Number => new OpenApiSchema { Type = JsonSchemaType.Integer, Description = description },
                _ => new OpenApiSchema { Type = JsonSchemaType.String, Description = description }
            };

            if (param.Type == SearchParamType.Date)
            {
                encoding[param.Name] = new OpenApiEncoding { Style = ParameterStyle.Form, Explode = true };
            }
        }

        return new OpenApiRequestBody
        {
            Required = true,
            Description = "Search parameters as an `application/x-www-form-urlencoded` body, so no National ID / Iqama " +
                          "appears in the URL or in server and proxy logs.",
            Content = new Dictionary<string, OpenApiMediaType>
            {
                [FormContentType] = new()
                {
                    Schema = new OpenApiSchema { Type = JsonSchemaType.Object, Properties = properties },
                    Encoding = encoding,
                    Examples = Examples(kit.Requests.Select(r => (r.Name, r.Description, r.Body)))
                }
            }
        };
    }

    private static OpenApiResponses Responses(KitOperation kit, OperationSpec spec)
    {
        var responses = new OpenApiResponses();
        foreach (var (status, (description, resourceType)) in spec.Responses)
        {
            var examples = kit.Responses.Where(r => r.Status == status).Select(r => (r.Name, r.Description, r.Body)).ToList();
            if (examples.Count == 0 && status == "401")
            {
                // The collection has one 401 example (Patient search); the response is the same on every endpoint.
                examples = KitSwaggerContent.Instance.Operations
                    .SelectMany(o => o.Responses)
                    .Where(r => r.Status == "401")
                    .Take(1)
                    .Select(r => ("Missing or wrong X-Api-Key", (string?)null, r.Body))
                    .ToList();
            }

            var response = new OpenApiResponse
            {
                Description = description,
                Content = new Dictionary<string, OpenApiMediaType>
                {
                    [FhirConstants.ContentType] = new()
                    {
                        Schema = FhirResourceSchema(resourceType),
                        Examples = examples.Count > 0 ? Examples(examples) : null
                    }
                }
            };

            if (status == "201")
            {
                response.Headers = new Dictionary<string, IOpenApiHeader>
                {
                    ["Location"] = new OpenApiHeader
                    {
                        Description = "URL of the stored Observation.",
                        Schema = new OpenApiSchema { Type = JsonSchemaType.String }
                    }
                };
            }

            responses[status] = response;
        }

        return responses;
    }

    private static Dictionary<string, IOpenApiExample> Examples(IEnumerable<(string Name, string? Description, JsonNode Body)> examples)
    {
        var result = new Dictionary<string, IOpenApiExample>();
        foreach (var (name, description, body) in examples)
        {
            var key = name;
            for (var i = 2; result.ContainsKey(key); i++)
            {
                key = $"{name} ({i})";
            }

            result[key] = new OpenApiExample { Summary = name, Description = description, Value = body };
        }

        return result;
    }

    private static OpenApiSchema FhirResourceSchema(string resourceType) => new()
    {
        Type = JsonSchemaType.Object,
        Description = $"FHIR R4 {resourceType} (http://hl7.org/fhir/R4/{resourceType.ToLowerInvariant()}.html). See the examples.",
        Required = new HashSet<string> { "resourceType" },
        Properties = new Dictionary<string, IOpenApiSchema>
        {
            ["resourceType"] = new OpenApiSchema { Type = JsonSchemaType.String, Enum = [JsonValue.Create(resourceType)!] }
        },
        AdditionalPropertiesAllowed = true
    };

    private sealed record OperationSpec(
        bool Public,
        string? RequestResource,
        string? SearchResource,
        IReadOnlyList<KeyValuePair<string, (string Description, string ResourceType)>> Responses);

    private static KeyValuePair<string, (string, string)> Status(string code, string description, string resourceType = "OperationOutcome") =>
        new(code, (description, resourceType));

    private static readonly KeyValuePair<string, (string, string)> Unauthorized =
        Status("401", "`X-Api-Key` is missing or wrong.");

    private static readonly Dictionary<string, OperationSpec> Specs = new()
    {
        ["GET /fhir/metadata"] = new(Public: true, null, null,
        [
            Status("200", "The server CapabilityStatement.", "CapabilityStatement")
        ]),
        ["POST /fhir/Patient/_search"] = new(Public: false, null, "Patient",
        [
            Status("200", "`searchset` Bundle with the patient. A patient that does not exist is **200** with `total: 0`, not 404.", "Bundle"),
            Status("400", "Neither `identifier` nor `_id` was sent."),
            Unauthorized,
            Status("502", "Fakeeh HIS could not be reached or did not respond in time. Retry later.")
        ]),
        ["GET /fhir/CodeSystem/medical-kit-vital-type"] = new(Public: false, null, null,
        [
            Status("200", "Every supported vital type with its fields, UCUM units and LOINC codes.", "CodeSystem"),
            Unauthorized,
            Status("404", "Unknown CodeSystem id.")
        ]),
        ["POST /fhir"] = new(Public: false, "Bundle", null,
        [
            Status("200", "`batch-response` Bundle with one entry per request entry, in the same order. " +
                          "`entry[i].response.status`: `201` created, `200` already existed, `4xx` rejected " +
                          "(the reason is in `entry[i].response.outcome`).", "Bundle"),
            Status("400", "The body is not a `batch` Bundle, or it has no entries."),
            Unauthorized
        ]),
        ["POST /fhir/Observation"] = new(Public: false, "Observation", null,
        [
            Status("201", "Reading stored. The body is the stored Observation.", "Observation"),
            Status("200", "A reading with the same KIT reading id already exists. The body is the stored Observation; nothing new is saved.", "Observation"),
            Status("400", "The reading is not valid (unknown patient, unknown vital type, wrong unit, missing timezone, " +
                          "missing required field, …). `issue.diagnostics` has the reason."),
            Unauthorized,
            Status("500", "The reading could not be saved. Retry with the same reading id."),
            Status("502", "Fakeeh HIS could not be reached to check the patient. Retry with the same reading id.")
        ]),
        ["POST /fhir/Observation/_search"] = new(Public: false, null, "Observation",
        [
            Status("200", "`searchset` Bundle with `total` and `self` / `previous` / `next` links. Paging links carry an opaque `_page` token, never the patient id.", "Bundle"),
            Status("400", "`patient` (or `_id`) is missing, or a parameter is not valid."),
            Unauthorized
        ])
    };
}
