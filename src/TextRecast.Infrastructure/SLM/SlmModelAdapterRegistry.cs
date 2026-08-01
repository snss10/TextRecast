namespace TextRecast.Infrastructure.SLM;

public sealed class SlmModelAdapterRegistry
{
    private readonly IReadOnlyDictionary<string, Func<SlmModelProfile, ISlmModelAdapter>> _factories;

    private SlmModelAdapterRegistry(
        IReadOnlyDictionary<string, Func<SlmModelProfile, ISlmModelAdapter>> factories)
    {
        _factories = factories;
    }

    public static SlmModelAdapterRegistry Default { get; } = new(
        new Dictionary<string, Func<SlmModelProfile, ISlmModelAdapter>>(StringComparer.Ordinal)
        {
            [Qwen25ModelAdapter.AdapterId] = _ => new Qwen25ModelAdapter(),
            [Qwen35ModelAdapter.AdapterId] = profile => new Qwen35ModelAdapter(profile),
            [Granite41ModelAdapter.AdapterId] = profile => new Granite41ModelAdapter(profile)
        });

    public ISlmModelAdapter Resolve(SlmModelProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        if (_factories.TryGetValue(profile.AdapterId, out var factory))
        {
            return factory(profile);
        }

        throw new InvalidOperationException(
            $"Model profile '{profile.Id}' references unsupported adapter '{profile.AdapterId}'.");
    }
}
