using System.IO;

namespace SawooPlates.Common;

/// <summary>
/// Copies the plugin's settings, presets and Recent history into Backups/ so nothing is lost
/// if a file ever gets damaged. Images aren't copied: they stay where they are and are never deleted by an import.
/// </summary>
public sealed class BackupService(IDalamudPluginInterface pluginInterface, IPluginLog log)
{
    private const int Keep = 10;

    private DirectoryInfo Folder => new(Path.Join(pluginInterface.ConfigDirectory.FullName, "Backups"));

    /// <summary>Makes a backup now. <paramref name="reason"/> ends up in the folder name, e.g. "startup".</summary>
    public string? Create(string reason, params string[] extraFiles)
    {
        try
        {
            var target = Directory.CreateDirectory(Path.Join(Folder.FullName, $"{DateTime.Now:yyyy-MM-dd_HH-mm-ss}_{reason}"));

            var files = new List<string> { pluginInterface.ConfigFile.FullName, Path.Join(pluginInterface.ConfigDirectory.FullName, "history.json") };
            files.AddRange(extraFiles);

            foreach (var file in files.Where(File.Exists))
                File.Copy(file, Path.Join(target.FullName, Path.GetFileName(file)), overwrite: true);

            Prune();
            return target.FullName;
        }
        catch (Exception ex)
        {
            log.Warning(ex, "Could not make a backup ({reason})", reason);
            return null;
        }
    }

    private void Prune()
    {
        foreach (var old in Folder.GetDirectories().OrderByDescending(d => d.Name).Skip(Keep))
        {
            try { old.Delete(true); }
            catch (Exception ex) { log.Warning(ex, "Could not remove old backup {name}", old.Name); }
        }
    }
}
