using Fakeeh.MedicalKit.Application.Patients;
using Hl7.Fhir.Model;

namespace Fakeeh.MedicalKit.Api.Fhir.Mappers;

public sealed class PatientFhirMapper(FhirOptions options)
{
    public Patient ToFhir(PatientDto patient)
    {
        var resource = new Patient
        {
            Id = patient.Id,
            Active = true,
            Gender = patient.Gender.ToLowerInvariant() switch
            {
                "male" => AdministrativeGender.Male,
                "female" => AdministrativeGender.Female,
                "other" => AdministrativeGender.Other,
                _ => AdministrativeGender.Unknown
            },
            BirthDate = patient.BirthDate?.ToString("yyyy-MM-dd")
        };

        resource.Identifier.Add(new Identifier(IdentifierSystem(patient.IdentifierType), patient.IdentifierValue)
        {
            Type = FhirConstants.Concept(
                "http://terminology.hl7.org/CodeSystem/v2-0203",
                patient.IdentifierType == PatientIdentifierType.NationalId ? "NI" : "PRC",
                patient.IdentifierType == PatientIdentifierType.NationalId ? "National unique individual identifier" : "Permanent Resident Card Number")
        });

        if (!string.IsNullOrWhiteSpace(patient.Mrn))
        {
            resource.Identifier.Add(new Identifier(options.MrnSystem, patient.Mrn)
            {
                Type = FhirConstants.Concept("http://terminology.hl7.org/CodeSystem/v2-0203", "MR", "Medical record number")
            });
        }

        var given = new[] { patient.FirstName, patient.MiddleName }.Where(n => !string.IsNullOrWhiteSpace(n)).ToList();
        var family = string.IsNullOrWhiteSpace(patient.FamilyName) ? null : patient.FamilyName;
        var fullName = string.Join(' ', family is null ? given : given.Append(family));
        if (!string.IsNullOrWhiteSpace(fullName))
        {
            resource.Name.Add(new HumanName
            {
                Use = HumanName.NameUse.Official,
                Family = family,
                Given = given!,
                Text = fullName
            });
        }

        if (!string.IsNullOrWhiteSpace(patient.FullNameArabic))
        {
            resource.Name.Add(new HumanName { Use = HumanName.NameUse.Usual, Text = patient.FullNameArabic });
        }

        if (!string.IsNullOrWhiteSpace(patient.Phone))
        {
            resource.Telecom.Add(new ContactPoint(ContactPoint.ContactPointSystem.Phone, ContactPoint.ContactPointUse.Mobile, patient.Phone));
        }

        if (!string.IsNullOrWhiteSpace(patient.Email))
        {
            resource.Telecom.Add(new ContactPoint(ContactPoint.ContactPointSystem.Email, null, patient.Email));
        }

        if (!string.IsNullOrWhiteSpace(patient.City))
        {
            resource.Address.Add(new Address { City = patient.City, Country = "SA" });
        }

        if (!string.IsNullOrWhiteSpace(patient.Nationality))
        {
            var nationality = new Extension { Url = "http://hl7.org/fhir/StructureDefinition/patient-nationality" };
            nationality.Extension.Add(new Extension("code", new CodeableConcept { Text = patient.Nationality }));
            resource.Extension.Add(nationality);
        }

        return resource;
    }

    public string IdentifierSystem(PatientIdentifierType type) =>
        type == PatientIdentifierType.NationalId ? options.NationalIdSystem : options.IqamaSystem;

    public PatientIdentifierType? IdentifierTypeFor(string? system, string value)
    {
        if (string.IsNullOrEmpty(system))
        {
            return value.StartsWith('2') ? PatientIdentifierType.Iqama : PatientIdentifierType.NationalId;
        }

        if (string.Equals(system, options.NationalIdSystem, StringComparison.OrdinalIgnoreCase))
        {
            return PatientIdentifierType.NationalId;
        }

        if (string.Equals(system, options.IqamaSystem, StringComparison.OrdinalIgnoreCase))
        {
            return PatientIdentifierType.Iqama;
        }

        return null;
    }
}
