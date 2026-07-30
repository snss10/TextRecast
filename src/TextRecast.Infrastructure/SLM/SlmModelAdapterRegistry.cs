namespace TextRecast.Infrastructure.SLM;

public sealed class SlmModelAdapterRegistry
{
    private readonly Dictionary<string, ISlmModelAdapter> _adapters;

    internal SlmModelAdapterRegistry(IEnumerable<ISlmModelAdapter> adapters)
    {
        var adapterMap = new Dictionary<string, ISlmModelAdapter>(StringComparer.Ordinal);
        foreach (var adapter in adapters)
        {
            if (string.IsNullOrWhiteSpace(adapter.Id))
            {
                throw new ArgumentException("A model adapter identifier cannot be empty.", nameof(adapters));
            }

            if (!adapterMap.TryAdd(adapter.Id, adapter))
            {
                throw new ArgumentException(
                    $"A model adapter with identifier '{adapter.Id}' is already registered.",
                    nameof(adapters));
            }
        }

        _adapters = adapterMap;
    }

    public static SlmModelAdapterRegistry Default { get; } = new([new Qwen25ModelAdapter()]);

    public ISlmModelAdapter Resolve(SlmModelProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        if (_adapters.TryGetValue(profile.AdapterId, out var adapter))
        {
            return adapter;
        }

        throw new InvalidOperationException(
            $"Model profile '{profile.Id}' references unsupported adapter '{profile.AdapterId}'.");
    }
}
