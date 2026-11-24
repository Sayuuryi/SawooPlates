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

public class RenameTagDialog : ConfirmationDialog
{
    private readonly PluginConfig _pluginConfig;
    private readonly TextService _textService;

    private ConfirmationButton _saveButton = null!;
    private SavedPresetTag? _tag = null;
    private string? _name;

    public RenameTagDialog(PluginConfig pluginConfig, TextService textService)
    {
        _pluginConfig = pluginConfig;
        _textService = textService;
        Initialize();
    }


    public void Initialize()
    {
        WindowName = _textService.Translate("EditorWindows.RenameTagDialog.Title");

        AddButton(_saveButton = new ConfirmationButton(_textService.Translate("ConfirmationButtonWindow.Save"), OnSave));
        AddButton(new ConfirmationButton(_textService.Translate("ConfirmationButtonWindow.Cancel"), Close));
    }

    public void Open(SavedPresetTag tag)
    {
        _tag = tag;
        _name = tag.Name;
        Show();
    }

    public void Close()
    {
        Hide();
        _tag = null;
        _name = null;
    }

    public override bool DrawCondition()
        => base.DrawCondition() && _tag != null && _name != null;

    public override void InnerDraw()
    {
        ImGui.Text(_textService.Translate("EditorWindows.RenameTagDialog.Name.Label", _tag!.Name));

        ImGui.Spacing();

        var name = _name ?? string.Empty;
        ImGui.InputText("##TagName", ref name, 30);
        _name = name;

        var disabled = string.IsNullOrEmpty(_name.Trim()) && _name.Trim() != _tag!.Name.Trim();

        _saveButton.Disabled = disabled;

        if (!disabled && (ImGui.IsKeyPressed(ImGuiKey.Enter) || ImGui.IsKeyPressed(ImGuiKey.KeypadEnter)))
        {
            OnSave();
        }
    }

    private void OnSave()
    {
        if (_tag == null || string.IsNullOrEmpty(_name?.Trim()))
        {
            Close();
            return;
        }

        _tag.Name = _name.Trim();
        _pluginConfig.Save();

        Close();
    }
}
