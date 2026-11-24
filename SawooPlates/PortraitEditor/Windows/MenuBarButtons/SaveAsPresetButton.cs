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

public class SaveAsPresetButton : MenuBarButton
{
    private readonly MenuBarState _state;
    private readonly TextService _textService;
    private readonly BannerService _bannerService;
    private readonly CreatePresetDialog _createPresetDialog;

    public SaveAsPresetButton(MenuBarState state, TextService textService, BannerService bannerService, CreatePresetDialog createPresetDialog)
    {
        _state = state;
        _textService = textService;
        _bannerService = bannerService;
        _createPresetDialog = createPresetDialog;
        Initialize();
    }


    private void Initialize()
    {
        Key = "SaveAsPreset";
        Icon = FontAwesomeIcon.Download;
        TooltipText = _textService.Translate("EditorWindows.MenuBar.SaveAsPreset.Label");
    }

    public override void OnClick()
    {
        _createPresetDialog.Open(_state.PortraitName, PortraitPresetExtensions.FromState(), _bannerService.GetCurrentCharaViewImage());
    }
}
