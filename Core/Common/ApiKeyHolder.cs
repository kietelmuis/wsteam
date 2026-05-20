using System.Collections.Generic;

namespace wsteam.Core.Common;

public class ApiKeyHolder
{
    private readonly Dictionary<ManifestSource, string> _apiKeys = [];

    public void AddApiKey(ManifestSource src, string key)
        => _apiKeys[src] = key;

    public string? GetApiKey(ManifestSource src)
        => _apiKeys.TryGetValue(src, out var key) ? key : null;
}
