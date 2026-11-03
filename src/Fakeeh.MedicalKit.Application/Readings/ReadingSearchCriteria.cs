namespace Fakeeh.MedicalKit.Application.Readings;

public enum CodeFilterSystem
{
    Any,
    Loinc,
    VitalType,
    VitalField
}

public sealed record CodeFilter(CodeFilterSystem System, string Code)
{
    public bool Matches(CodeFilterSystem system, string code) =>
        (System == CodeFilterSystem.Any || System == system)
        && string.Equals(Code, code, StringComparison.OrdinalIgnoreCase);
}

public sealed record CodeMatch(string VitalTypeCode, string? FieldKey);

public sealed record ReadingSearchCriteria
{
    public required string PatientId { get; init; }

    public IReadOnlyList<CodeMatch>? Codes { get; init; }

    public string? Category { get; init; }

    public DateTimeOffset? From { get; init; }

    public DateTimeOffset? To { get; init; }

    public bool SortDescending { get; init; } = true;

    public int Offset { get; init; }

    public int Count { get; init; } = 20;
}
