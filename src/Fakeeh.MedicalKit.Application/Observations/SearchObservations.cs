using Fakeeh.MedicalKit.Application.Abstractions;
using Fakeeh.MedicalKit.Application.Common;
using Fakeeh.MedicalKit.Domain.Common;
using FluentValidation;
using MediatR;

namespace Fakeeh.MedicalKit.Application.Observations;

/// <summary>FHIR Observation search. <see cref="PatientId"/> is required unless <see cref="Id"/> is given.</summary>
public sealed record SearchObservationsQuery : IRequest<Result<PagedList<ObservationDto>>>
{
    public const int DefaultCount = 20;
    public const int MaxCount = 100;

    public string? PatientId { get; init; }

    public Guid? Id { get; init; }

    public CodeFilter? Code { get; init; }

    public string? Category { get; init; }

    public DateTimeOffset? From { get; init; }

    public DateTimeOffset? To { get; init; }

    public bool SortDescending { get; init; } = true;

    public int Offset { get; init; }

    public int Count { get; init; } = DefaultCount;
}

internal sealed class SearchObservationsQueryValidator : AbstractValidator<SearchObservationsQuery>
{
    public SearchObservationsQueryValidator()
    {
        RuleFor(q => q.PatientId)
            .NotEmpty().When(q => q.Id is null).WithErrorCode("search.patient_required")
            .WithMessage("The 'patient' search parameter is required, e.g. patient=Patient/1234567890.")
            .OverridePropertyName("patient");
        RuleFor(q => q.Count).InclusiveBetween(1, SearchObservationsQuery.MaxCount)
            .WithErrorCode("search.count_invalid")
            .WithMessage($"_count must be between 1 and {SearchObservationsQuery.MaxCount}.")
            .OverridePropertyName("_count");
        RuleFor(q => q.Offset).GreaterThanOrEqualTo(0)
            .WithErrorCode("search.offset_invalid")
            .WithMessage("_offset must be 0 or more.")
            .OverridePropertyName("_offset");
    }
}

internal sealed class SearchObservationsQueryHandler(
    IReadingRepository readings,
    IVitalTypeRepository vitalTypes,
    ObservationResolver resolver)
    : IRequestHandler<SearchObservationsQuery, Result<PagedList<ObservationDto>>>
{
    public async Task<Result<PagedList<ObservationDto>>> Handle(SearchObservationsQuery request, CancellationToken cancellationToken)
    {
        var codes = request.Code is null
            ? null
            : await resolver.ResolveCodeFilterAsync(request.Code, cancellationToken);

        var page = await readings.SearchAsync(
            new ReadingSearchCriteria
            {
                PatientId = string.IsNullOrWhiteSpace(request.PatientId) ? null : request.PatientId.Trim(),
                Id = request.Id,
                Codes = codes,
                Category = request.Category,
                From = request.From,
                To = request.To,
                SortDescending = request.SortDescending,
                Offset = request.Offset,
                Count = request.Count
            },
            cancellationToken);

        var types = (await vitalTypes.GetAllActiveAsync(cancellationToken))
            .ToDictionary(t => t.Code, StringComparer.OrdinalIgnoreCase);

        return page.Map(r => r.ToObservationDto(types.GetValueOrDefault(r.VitalTypeCode)));
    }
}
