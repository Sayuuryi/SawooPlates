using Dalamud.Interface.Windowing;
using SawooPlates.Capture;
using SawooPlates.Config;

namespace SawooPlates.Windows;

/// <summary>Asks once, after login, whether to bring in presets saved by another portrait plugin.</summary>
public sealed class ImportPromptWindow : Window, IDisposable
{
    private readonly IFramework _framework;
    private readonly IClientState _clientState;
    private readonly IPluginLog _log;
    private readonly PluginConfig _pluginConfig;
    private readonly PresetImporter _importer;

    private int _newCount;
    private string? _result;
    private bool _checked;

    public ImportPromptWindow(IFramework framework, IClientState clientState, IPluginLog log, PluginConfig pluginConfig, PresetImporter importer)
        : base("Sawoo Plates##ImportPrompt", ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoSavedSettings)
    {
        _framework = framework;
        _clientState = clientState;
        _log = log;
        _pluginConfig = pluginConfig;
        _importer = importer;

        PositionCondition = ImGuiCond.Appearing;

        _clientState.Login += ScheduleCheck;
        if (_clientState.IsLoggedIn)
            ScheduleCheck();
    }

    public void Dispose()
    {
        _clientState.Login -= ScheduleCheck;
    }

    private void ScheduleCheck()
    {
        if (_checked || _pluginConfig.PresetImportOffered)
            return;

        // wait a little so it doesn't pop up during the login screen fade
        _ = _framework.RunOnTick(() =>
        {
            _checked = true;
            _newCount = _importer.CountNew();
            if (_newCount > 0)
            {
                _result = null;
                IsOpen = true;
            }
        }, TimeSpan.FromSeconds(8));
    }

    public override void PreDraw()
    {
        Position = ImGui.GetMainViewport().GetCenter() - new Vector2(200, 80) * Ui.Scale;
    }

    public override void Draw()
    {
        using var wrap = ImRaii.TextWrapPos(ImGui.GetFontSize() * 26);

        if (_result != null)
        {
            Ui.Icon(FontAwesomeIcon.CheckCircle, Ui.Good);
            ImGui.SameLine();
            ImGui.TextUnformatted(_result);
            ImGui.Spacing();
            if (ImGui.Button("Close", new Vector2(120 * Ui.Scale, 0)))
                IsOpen = false;
            return;
        }

        ImGui.TextColored(Ui.Accent, "Import your presets?");
        ImGui.Spacing();
        ImGui.TextUnformatted(_newCount == 1
            ? $"You have 1 portrait preset saved in {PresetImporter.SourcePlugin}. Copy it into Sawoo Plates?"
            : $"You have {_newCount} portrait presets saved in {PresetImporter.SourcePlugin}. Copy them into Sawoo Plates?");
        ImGui.TextColored(Ui.Dim, $"Tags and thumbnails come along. {PresetImporter.SourcePlugin} itself isn't changed.");

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        if (Ui.Button(FontAwesomeIcon.FileImport, "Import"))
        {
            try
            {
                var result = _importer.ImportAll();
                _result = $"Imported {result.Imported} presets. You'll find them in /splates → Presets and in the Portrait Editor's Preset Browser.";
            }
            catch (Exception ex)
            {
                _log.Error(ex, "Import failed");
                _result = $"Couldn't import: {ex.Message}";
            }

            _pluginConfig.PresetImportOffered = true;
            _pluginConfig.Save();
        }

        ImGui.SameLine();
        if (ImGui.Button("Not now"))
            IsOpen = false;

        ImGui.SameLine();
        if (ImGui.Button("Don't ask again"))
        {
            _pluginConfig.PresetImportOffered = true;
            _pluginConfig.Save();
            IsOpen = false;
        }
    }
}
