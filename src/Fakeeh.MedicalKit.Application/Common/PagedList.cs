namespace Fakeeh.MedicalKit.Application.Common;

public sealed record PagedList<T>(IReadOnlyList<T> Items, int Total, int Offset, int Count)
{
    /// <summary>
    /// Overrides the default next-page check, for paging where one page is not a single window of <see cref="Total"/>
    /// (e.g. a page per vital type).
    /// </summary>
    public bool? HasMore { get; init; }

    public bool HasNext => HasMore ?? Offset + Items.Count < Total;

    public bool HasPrevious => Offset > 0;

    public PagedList<TOut> Map<TOut>(Func<T, TOut> map) =>
        new(Items.Select(map).ToList(), Total, Offset, Count) { HasMore = HasMore };
}
