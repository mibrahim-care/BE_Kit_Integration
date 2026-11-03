using System.Net;
using Fakeeh.MedicalKit.Domain.Common;
using Hl7.Fhir.Model;

namespace Fakeeh.MedicalKit.Api.Fhir;

public static class OperationOutcomes
{
    public static OperationOutcome FromErrors(IEnumerable<Error> errors) => new()
    {
        Issue = errors.Select(ToIssue).ToList()
    };

    public static OperationOutcome Single(
        OperationOutcome.IssueType type,
        string message,
        OperationOutcome.IssueSeverity severity = OperationOutcome.IssueSeverity.Error,
        string? code = null) => new()
    {
        Issue =
        [
            new OperationOutcome.IssueComponent
            {
                Severity = severity,
                Code = type,
                Diagnostics = message,
                Details = code is null ? null : new CodeableConcept { Text = code }
            }
        ]
    };

    public static HttpStatusCode StatusFor(Error error) => error.Type switch
    {
        ErrorType.Validation => HttpStatusCode.BadRequest,
        ErrorType.NotFound => HttpStatusCode.NotFound,
        ErrorType.Conflict => HttpStatusCode.Conflict,
        ErrorType.Unauthorized => HttpStatusCode.Unauthorized,
        _ => HttpStatusCode.InternalServerError
    };

    private static OperationOutcome.IssueComponent ToIssue(Error error) => new()
    {
        Severity = OperationOutcome.IssueSeverity.Error,
        Code = error.Type switch
        {
            ErrorType.Validation => OperationOutcome.IssueType.Invalid,
            ErrorType.NotFound => OperationOutcome.IssueType.NotFound,
            ErrorType.Conflict => OperationOutcome.IssueType.Conflict,
            ErrorType.Unauthorized => OperationOutcome.IssueType.Security,
            _ => OperationOutcome.IssueType.Exception
        },
        Details = new CodeableConcept { Text = error.Code },
        Diagnostics = error.Message,
        Expression = string.IsNullOrWhiteSpace(error.Target) ? null : [error.Target]
    };
}
