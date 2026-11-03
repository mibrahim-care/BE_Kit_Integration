using Fakeeh.MedicalKit.Domain.Common;

namespace Fakeeh.MedicalKit.Application.Patients;

public static class PatientErrors
{
    public static Error NotFound(string nationalId) =>
        Error.NotFound("patient.not_found", $"No patient with national ID / Iqama '{nationalId}' was found in HIS.");
}
