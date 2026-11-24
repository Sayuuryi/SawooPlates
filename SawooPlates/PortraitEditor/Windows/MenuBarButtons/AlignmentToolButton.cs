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

namespace SawooPlates.PortraitEditor.Windows.MenuBarButtons;

public class AlignmentToolButton : MenuBarOverlayButton<AlignmentToolSettingsOverlay>
{
    private readonly TextService _textService;
    private readonly PluginConfig _pluginConfig;
    private readonly MenuBarState _state;

    public AlignmentToolButton(TextService textService, PluginConfig pluginConfig, MenuBarState state)
        : base(state)
    {
        _textService = textService;
        _pluginConfig = pluginConfig;
        _state = state;
        Initialize();
    }


    private void Initialize()
    {
        Key = "ToggleAlignmentTool";
        Icon = FontAwesomeIcon.Hashtag;
        TooltipText = _textService.Translate("EditorWindows.MenuBar.ToggleAlignmentTool.Label");
    }

    public override bool IsActive => _pluginConfig.PortraitEditor.ShowAlignmentTool;

    public override void OnClick()
    {
        if (ImGui.IsKeyDown(ImGuiKey.LeftShift) || ImGui.IsKeyDown(ImGuiKey.RightShift))
        {
            if (_state.Overlay is AlignmentToolSettingsOverlay)
            {
                _state.CloseOverlay();
            }
            else
            {
                _state.OpenOverlay<AlignmentToolSettingsOverlay>();
            }
        }
        else
        {
            _pluginConfig.PortraitEditor.ShowAlignmentTool = !_pluginConfig.PortraitEditor.ShowAlignmentTool;
            _pluginConfig.Save();
        }
    }
}
