namespace Fakeeh.MedicalKit.Api.Fhir.Search;

/// <summary>The name/value pairs of a FHIR search, in order. A name can repeat (e.g. date=ge…&amp;date=le…).</summary>
public sealed class SearchParameters(IReadOnlyList<KeyValuePair<string, string>> pairs)
{
    public IReadOnlyList<KeyValuePair<string, string>> Pairs { get; } = pairs;

    /// <summary>The first non-empty value of <paramref name="name"/>; FHIR ignores parameters with an empty value.</summary>
    public string? First(string name) => All(name).FirstOrDefault();

    public IReadOnlyList<string> All(string name) =>
        Pairs.Where(p => p.Key == name && !string.IsNullOrWhiteSpace(p.Value)).Select(p => p.Value.Trim()).ToList();

    public bool Has(string name) => All(name).Count > 0;

    public IEnumerable<string> Names => Pairs.Where(p => !string.IsNullOrWhiteSpace(p.Value)).Select(p => p.Key).Distinct();

    public SearchParameters Without(params string[] names) =>
        new(Pairs.Where(p => !names.Contains(p.Key)).ToList());

    public SearchParameters With(string name, string value) =>
        new([.. Pairs, new KeyValuePair<string, string>(name, value)]);
}
