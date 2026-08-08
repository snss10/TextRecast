using System.Net.Http;

namespace TextRecast.Infrastructure.SLM;

public sealed class SlmModelInstallationSet
{
    private readonly Dictionary<string, SlmModelInstaller> installers;

    public SlmModelInstallationSet(
        IEnumerable<SlmModelProfile> profiles,
        HttpClient httpClient,
        string packagedModelDirectory,
        string userModelDirectory)
    {
        ArgumentNullException.ThrowIfNull(profiles);
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentException.ThrowIfNullOrWhiteSpace(packagedModelDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(userModelDirectory);

        installers = profiles.ToDictionary(
            profile => profile.Id,
            profile => new SlmModelInstaller(
                profile,
                httpClient,
                packagedModelDirectory,
                userModelDirectory),
            StringComparer.Ordinal);
    }

    public SlmModelInstaller GetInstaller(string modelId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(modelId);
        return installers.TryGetValue(modelId, out var installer)
            ? installer
            : throw new ArgumentException("The model is not part of this setup.", nameof(modelId));
    }

    public IReadOnlyDictionary<string, string> FindModelCandidatesByExpectedSize()
    {
        return installers
            .Select(pair => (pair.Key, Path: pair.Value.FindCandidateModelByExpectedSize()))
            .Where(item => item.Path is not null)
            .ToDictionary(item => item.Key, item => item.Path!, StringComparer.Ordinal);
    }

    public async Task<IReadOnlyDictionary<string, string>> FindVerifiedInstalledModelsAsync(
        CancellationToken cancellationToken = default)
    {
        var verifiedModels = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var pair in installers)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var path = await pair.Value
                .FindVerifiedInstalledModelAsync(cancellationToken)
                .ConfigureAwait(false);
            if (path is not null)
            {
                verifiedModels.Add(pair.Key, path);
            }
        }

        return verifiedModels;
    }
}
