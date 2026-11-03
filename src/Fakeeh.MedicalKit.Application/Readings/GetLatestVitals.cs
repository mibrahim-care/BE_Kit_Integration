using Fakeeh.MedicalKit.Application.Abstractions;
using Fakeeh.MedicalKit.Application.Common;
using Fakeeh.MedicalKit.Domain.Common;
using Fakeeh.MedicalKit.Domain.Entities;
using FluentValidation;
using MediatR;

namespace Fakeeh.MedicalKit.Application.Readings;

/// <summary>The newest <see cref="RecordsPerType"/> readings of each vital type for a patient.</summary>
public sealed record GetLatestVitalsQuery : IRequest<Result<LatestVitalsDto>>
{
    public const int DefaultRecordsPerType = 5;

    public string? NationalId { get; init; }

    public int? RecordsPerType { get; init; }

    /// <summary>Optional: only this vital type.</summary>
    public string? VitalTypeCode { get; init; }

    /// <summary>Optional: readings taken on or after this day.</summary>
    public DateOnly? DateFrom { get; init; }

    /// <summary>Optional: readings taken on or before this day (inclusive).</summary>
    public DateOnly? DateTo { get; init; }
}

internal sealed class GetLatestVitalsQueryValidator : AbstractValidator<GetLatestVitalsQuery>
{
    public GetLatestVitalsQueryValidator(ReadingSettings settings)
    {
        RuleFor(q => q.NationalId).NationalId();

        RuleFor(q => q.RecordsPerType)
            .InclusiveBetween(1, settings.MaxRecordsPerType).WithErrorCode("vitals.records_per_type_invalid")
            .WithMessage($"recordsPerType must be between 1 and {settings.MaxRecordsPerType}.")
            .OverridePropertyName("recordsPerType");

        RuleFor(q => q)
            .Must(q => q.DateFrom is null || q.DateTo is null || q.DateFrom <= q.DateTo)
            .WithErrorCode("vitals.date_range_invalid")
            .WithMessage("dateFrom must be on or before dateTo.")
            .OverridePropertyName("dateFrom");
    }
}

internal sealed class GetLatestVitalsQueryHandler(
    IReadingRepository readings,
    IVitalTypeRepository vitalTypes,
    ReadingSettings settings)
    : IRequestHandler<GetLatestVitalsQuery, Result<LatestVitalsDto>>
{
    public async Task<Result<LatestVitalsDto>> Handle(GetLatestVitalsQuery request, CancellationToken cancellationToken)
    {
        var types = await vitalTypes.GetAllActiveAsync(cancellationToken);

        VitalType? filterType = null;
        if (!string.IsNullOrWhiteSpace(request.VitalTypeCode))
        {
            filterType = types.FirstOrDefault(t => string.Equals(t.Code, request.VitalTypeCode.Trim(), StringComparison.OrdinalIgnoreCase));
            if (filterType is null)
            {
                return ReadingErrors.UnknownVitalType(request.VitalTypeCode);
            }
        }

        var perType = request.RecordsPerType ?? GetLatestVitalsQuery.DefaultRecordsPerType;
        var offset = settings.DefaultTimeZoneOffset;

        var latest = await readings.GetLatestPerTypeAsync(
            new LatestReadingsCriteria(
                request.NationalId!,
                perType,
                filterType?.Code,
                request.DateFrom is { } from ? StartOfDay(from, offset) : null,
                request.DateTo is { } to ? StartOfDay(to.AddDays(1), offset) : null),
            cancellationToken);

        var typesByCode = types.ToDictionary(t => t.Code, StringComparer.OrdinalIgnoreCase);

        var groups = latest
            .GroupBy(r => r.VitalTypeCode, StringComparer.OrdinalIgnoreCase)
            .Select(g =>
            {
                var type = typesByCode.GetValueOrDefault(g.Key);
                return new VitalTypeReadingsDto(
                    type?.Code ?? g.Key,
                    type?.Name ?? g.Key,
                    type?.Unit,
                    type?.Fields.OrderBy(f => f.SortOrder).Select(f => new VitalFieldDto(f.Key, f.Label, f.Unit)).ToList() ?? [],
                    g.OrderByDescending(r => r.EffectiveAt)
                        .ThenByDescending(r => r.ReceivedAt)
                        .Select(r => r.ToDto(type))
                        .ToList());
            })
            .OrderBy(g => g.VitalTypeName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new LatestVitalsDto(request.NationalId!, perType, groups);
    }

    private static DateTimeOffset StartOfDay(DateOnly day, TimeSpan offset) =>
        new(day.ToDateTime(TimeOnly.MinValue), offset);
}
