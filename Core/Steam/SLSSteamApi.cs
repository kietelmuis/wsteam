using System;
using System.IO;
using System.Linq;
using YamlDotNet.Serialization;

class SLSConfig
{
    public uint[] AppIds { get; set; }
}

public class SLSSteamApi
{
    private readonly Deserializer YamlDeserializer = new();
    private readonly Serializer YamlSerializer = new();

    private readonly string SLSConfigFile
        = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config", "SLSsteam", "config.yaml");

    public void AddAppIdToAppList(uint newAppId)
    {
        if (!File.Exists(SLSConfigFile))
            return;

        var slsConfig = YamlDeserializer.Deserialize<SLSConfig>(File.ReadAllText(SLSConfigFile));

        if (slsConfig.AppIds is null)
            slsConfig.AppIds = [newAppId];
        else if (!slsConfig.AppIds.Contains(newAppId))
            slsConfig.AppIds = [.. slsConfig.AppIds, newAppId];

        File.WriteAllText(SLSConfigFile, YamlSerializer.Serialize(slsConfig));
    }
}
