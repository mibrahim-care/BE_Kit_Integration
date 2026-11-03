using Fakeeh.MedicalKit.Domain.Common;

namespace Fakeeh.MedicalKit.Application.Patients;

public static class PatientErrors
{
    public static Error NotFound(string patientId) =>
        Error.NotFound("patient.not_found", $"Patient '{patientId}' was not found in HIS.");
}
