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

public class PresetBrowserButton : MenuBarOverlayButton<PresetBrowserOverlay>
{
    private readonly TextService _textService;

    public PresetBrowserButton(TextService textService, MenuBarState state)
        : base(state)
    {
        _textService = textService;
        Initialize();
    }


    private void Initialize()
    {
        Key = "TogglePresetBrowser";
        Icon = FontAwesomeIcon.List;
        TooltipText = _textService.Translate("EditorWindows.MenuBar.TogglePresetBrowser.Label");
    }
}
