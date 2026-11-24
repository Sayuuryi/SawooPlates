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

public class CreateTagDialog : ConfirmationDialog
{
    private readonly PluginConfig _pluginConfig;
    private readonly TextService _textService;
    private ConfirmationButton _saveButton = null!;
    private string _name = string.Empty;

    public CreateTagDialog(PluginConfig pluginConfig, TextService textService)
    {
        _pluginConfig = pluginConfig;
        _textService = textService;
        Initialize();
    }


    private void Initialize()
    {
        WindowName = _textService.Translate("EditorWindows.CreateTagDialog.Title");

        AddButton(_saveButton = new ConfirmationButton(_textService.Translate("ConfirmationButtonWindow.Save"), OnSave));
        AddButton(new ConfirmationButton(_textService.Translate("ConfirmationButtonWindow.Cancel"), Close));
    }

    public void Open()
    {
        _name = string.Empty;
        Show();
    }

    public void Close()
    {
        Hide();
        _name = string.Empty;
    }

    public override bool DrawCondition()
        => base.DrawCondition() && _name != null;

    public override void InnerDraw()
    {
        ImGui.Text(_textService.Translate("EditorWindows.CreateTagDialog.Name.Label"));

        ImGui.Spacing();

        ImGui.InputText("##TagName", ref _name, 30);

        var disabled = string.IsNullOrEmpty(_name.Trim());

        _saveButton.Disabled = disabled;

        if (!disabled && (ImGui.IsKeyPressed(ImGuiKey.Enter) || ImGui.IsKeyPressed(ImGuiKey.KeypadEnter)))
        {
            OnSave();
        }
    }

    private void OnSave()
    {
        if (string.IsNullOrEmpty(_name?.Trim()))
        {
            Close();
            return;
        }

        _pluginConfig.PortraitEditor.PresetTags.Add(new(_name.Trim()));
        _pluginConfig.Save();

        Close();
    }
}
