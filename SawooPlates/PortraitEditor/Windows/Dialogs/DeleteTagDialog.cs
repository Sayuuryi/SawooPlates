using Dalamud.Interface.Windowing;
using SawooPlates.Config;
using SawooPlates.PortraitEditor;
using SawooPlates.PortraitEditor.Enums;
using SawooPlates.PortraitEditor.Services;
using SawooPlates.PortraitEditor.Utils;
using SawooPlates.PortraitEditor.Windows;
using SawooPlates.PortraitEditor.Windows.Dialogs;
using SawooPlates.PortraitEditor.Windows.MenuBarButtons;
using SawooPlates.PortraitEditor.Windows.Overlays;

namespace SawooPlates.PortraitEditor.Windows.Dialogs;

public class DeleteTagDialog : ConfirmationDialog
{
    private readonly PluginConfig _pluginConfig;
    private readonly TextService _textService;

    private PresetBrowserOverlay? _presetBrowserOverlay;

    private SavedPresetTag? _tag;
    private bool _deletePortraits;

    public DeleteTagDialog(PluginConfig pluginConfig, TextService textService)
    {
        _pluginConfig = pluginConfig;
        _textService = textService;
        Initialize();
    }


    private void Initialize()
    {
        WindowName = _textService.Translate("EditorWindows.DeleteTagDialog.Title");

        AddButton(new ConfirmationButton(_textService.Translate("ConfirmationButtonWindow.Delete"), OnDelete));
        AddButton(new ConfirmationButton(_textService.Translate("ConfirmationButtonWindow.Cancel"), Close));
    }

    public void Open(PresetBrowserOverlay presetBrowserOverlay, SavedPresetTag tag)
    {
        _presetBrowserOverlay = presetBrowserOverlay;
        _tag = tag;
        _deletePortraits = false;
        Show();
    }

    public void Close()
    {
        Hide();
        _tag = null;
    }

    public override bool DrawCondition()
        => base.DrawCondition() && _tag != null;

    public override void InnerDraw()
    {
        ImGui.Text(_textService.Translate("EditorWindows.DeleteTagDialog.Prompt", _tag!.Name));
        ImGui.Spacing();
        ImGui.Checkbox(_textService.Translate("EditorWindows.DeleteTagDialog.DeletePortraitsToo.Label"), ref _deletePortraits);
    }

    private void OnDelete()
    {
        if (_tag == null)
        {
            Close();
            return;
        }

        var config = _pluginConfig.PortraitEditor;

        var presets = config.Presets
            .Where((preset) => preset.Tags.Any((t) => t == _tag.Id))
            .ToArray();

        if (_deletePortraits)
        {
            // remove presets with tag
            foreach (var preset in presets)
            {
                config.Presets.Remove(preset);
            }
        }
        else
        {
            // remove tag from presets
            foreach (var preset in presets)
            {
                preset.Tags.Remove(_tag.Id);
            }
        }

        config.PresetTags.Remove(_tag);
        _pluginConfig.Save();

        if (_presetBrowserOverlay?.SelectedTagId == _tag.Id)
            _presetBrowserOverlay.SelectedTagId = null;

        Close();
    }
}
