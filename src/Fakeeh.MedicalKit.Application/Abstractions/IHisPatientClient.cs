using Fakeeh.MedicalKit.Application.Patients;

namespace Fakeeh.MedicalKit.Application.Abstractions;

public interface IHisPatientClient
{
    /// <summary>Looks a patient up in HIS by national ID or Iqama number. Returns null when HIS has no such patient.</summary>
    Task<PatientDto?> FindByNationalIdAsync(string nationalId, CancellationToken cancellationToken);
}
