namespace Fakeeh.MedicalKit.Application.Common;

public sealed record PagedList<T>(IReadOnlyList<T> Items, int Total, int Offset, int Count)
{
    public bool HasNext => Offset + Items.Count < Total;

    public bool HasPrevious => Offset > 0;

    public PagedList<TOut> Map<TOut>(Func<T, TOut> map) =>
        new(Items.Select(map).ToList(), Total, Offset, Count);
}
