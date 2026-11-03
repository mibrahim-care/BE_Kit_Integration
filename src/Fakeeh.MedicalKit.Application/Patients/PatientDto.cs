namespace Fakeeh.MedicalKit.Application.Patients;

public enum PatientIdentifierType
{
    NationalId = 1,

    Iqama = 2
}

public sealed record PatientDto(
    string Id,
    PatientIdentifierType IdentifierType,
    string IdentifierValue,
    string? Mrn,
    string FirstName,
    string? MiddleName,
    string? FamilyName,
    string? FullNameArabic,
    string Gender,
    DateOnly? BirthDate,
    string? Phone,
    string? Email,
    string? Nationality,
    string? City);
