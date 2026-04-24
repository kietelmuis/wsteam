using System;
using System.Collections.Generic;
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

        var yamlContent = YamlDeserializer.Deserialize<Dictionary<object, object>>(File.ReadAllText(SLSConfigFile));

        List<object> appIds;
        if (yamlContent.ContainsKey("AppIds"))
        {
            appIds = (yamlContent["AppIds"] as List<object>) ?? [];
        }
        else
        {
            appIds = [];
            if (!appIds.Contains(newAppId))
            {
                appIds.Add(newAppId);
                yamlContent["AppIds"] = appIds;
                File.WriteAllText(SLSConfigFile, YamlSerializer.Serialize(yamlContent));
            }
        }
    }
