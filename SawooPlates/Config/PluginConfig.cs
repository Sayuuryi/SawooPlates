using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using Dalamud.Configuration;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Dalamud.Utility;

namespace SawooPlates.Config;

/// <summary>
/// Plugin settings. Stored as JSON in the Dalamud config file. Migrations run on the raw JSON
/// before deserializing, keyed by <see cref="Version"/>.
/// </summary>
public class PluginConfig : IPluginConfiguration
{
    public const int CurrentVersion = 2;

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        IncludeFields = true,
        WriteIndented = true,
    };

    private IDalamudPluginInterface? _pluginInterface;
    private IPluginLog? _log;

    public int Version { get; set; } = CurrentVersion;

    /// <summary>Portrait editor settings and preset library.</summary>
    public PortraitEditorSettings PortraitEditor { get; set; } = new();

    public CaptureSettings Capture { get; set; } = new();

    /// <summary>Show the menu bar and overlays in the Portrait Editor.</summary>
    public bool EnablePortraitEditorTools { get; set; } = true;

    public static PluginConfig Load(IDalamudPluginInterface pluginInterface, IPluginLog log)
    {
        var config = LoadFile(pluginInterface.ConfigFile, log) ?? new PluginConfig();
        config._pluginInterface = pluginInterface;
        config._log = log;
        return config;
    }

    private static PluginConfig? LoadFile(FileInfo file, IPluginLog log)
    {
        if (!file.Exists || file.Length < 2)
            return null;

        try
        {
            if (JsonNode.Parse(File.ReadAllText(file.FullName)) is not JsonObject json)
                return null;

            var version = json[nameof(Version)]?.GetValue<int>() ?? 0;
            if (version > CurrentVersion)
                log.Warning("Config version {version} is newer than this plugin ({current}), loading anyway.", version, CurrentVersion);

            // 2: the editor settings and preset library moved from "PortraitHelper" to "PortraitEditor"
            if (version < 2)
            {
                if (json["PortraitHelper"] is JsonNode editor && json["PortraitEditor"] == null)
                {
                    json.Remove("PortraitHelper");
                    json["PortraitEditor"] = editor;
                }

                log.Information("Migrated config to version 2");
                version = 2;
            }

            json[nameof(Version)] = Math.Max(version, CurrentVersion);

            return json.Deserialize<PluginConfig>(SerializerOptions);
        }
        catch (Exception ex)
        {
            var backup = file.FullName + ".broken";
            log.Error(ex, "Could not read config, starting with defaults. Old file kept as {backup}", backup);
            try { File.Copy(file.FullName, backup, true); } catch { /* best effort */ }
            return null;
        }
    }

    public void Save()
    {
        if (_pluginInterface == null)
            return;

        try
        {
            FilesystemUtil.WriteAllTextSafe(_pluginInterface.ConfigFile.FullName, JsonSerializer.Serialize(this, SerializerOptions));
        }
        catch (Exception ex)
        {
            _log?.Error(ex, "Error saving config");
        }
    }
}
