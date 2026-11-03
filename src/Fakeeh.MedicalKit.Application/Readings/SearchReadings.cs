using Fakeeh.MedicalKit.Application.Abstractions;
using Fakeeh.MedicalKit.Application.Common;
using Fakeeh.MedicalKit.Domain.Common;
using FluentValidation;
using MediatR;

namespace Fakeeh.MedicalKit.Application.Readings;

public sealed record SearchReadingsQuery : IRequest<Result<PagedList<ReadingDto>>>
{
    public const int DefaultCount = 20;
    public const int MaxCount = 100;

    public required string PatientId { get; init; }

    public CodeFilter? Code { get; init; }

    public string? Category { get; init; }

    public DateTimeOffset? From { get; init; }

    public DateTimeOffset? To { get; init; }

    public bool SortDescending { get; init; } = true;

    public int Offset { get; init; }

    public int Count { get; init; } = DefaultCount;
}

internal sealed class SearchReadingsQueryValidator : AbstractValidator<SearchReadingsQuery>
{
    public SearchReadingsQueryValidator()
    {
        RuleFor(q => q.PatientId).NotEmpty().WithErrorCode("search.patient_required")
            .WithMessage("The 'patient' search parameter is required.")
            .OverridePropertyName("patient");
        RuleFor(q => q.Count).InclusiveBetween(1, SearchReadingsQuery.MaxCount)
            .WithErrorCode("search.count_invalid").OverridePropertyName("_count");
        RuleFor(q => q.Offset).GreaterThanOrEqualTo(0)
            .WithErrorCode("search.offset_invalid").OverridePropertyName("_offset");
    }
}

internal sealed class SearchReadingsQueryHandler(
    IReadingRepository readings,
    IVitalTypeRepository vitalTypes,
    VitalTypeResolver resolver)
    : IRequestHandler<SearchReadingsQuery, Result<PagedList<ReadingDto>>>
{
    public async Task<Result<PagedList<ReadingDto>>> Handle(SearchReadingsQuery request, CancellationToken cancellationToken)
    {
        var codes = request.Code is null
            ? null
            : await resolver.ResolveCodeFilterAsync(request.Code, cancellationToken);

        var criteria = new ReadingSearchCriteria
        {
            PatientId = request.PatientId,
            Codes = codes,
            Category = request.Category,
            From = request.From,
            To = request.To,
            SortDescending = request.SortDescending,
            Offset = request.Offset,
            Count = request.Count
        };

        var page = await readings.SearchAsync(criteria, cancellationToken);
        var types = (await vitalTypes.GetAllActiveAsync(cancellationToken)).ToDictionary(t => t.Code);

        return page.Map(r => r.ToDto(types.GetValueOrDefault(r.VitalTypeCode)));
    }
}
