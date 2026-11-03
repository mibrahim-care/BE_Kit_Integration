using Fakeeh.MedicalKit.Application.Patients;

namespace Fakeeh.MedicalKit.Application.Abstractions;

public interface IHisPatientClient
{
    Task<PatientDto?> FindByIdentifierAsync(PatientIdentifierType type, string value, CancellationToken cancellationToken);

    Task<PatientDto?> GetByIdAsync(string patientId, CancellationToken cancellationToken);
}
