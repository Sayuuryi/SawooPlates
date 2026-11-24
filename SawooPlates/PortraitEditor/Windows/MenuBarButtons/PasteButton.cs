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

public class PasteButton : MenuBarButton
{
    private readonly MenuBarState _state;
    private readonly TextService _textService;
    private readonly ClipboardService _clipboardService;
    private readonly BannerService _bannerService;

    public PasteButton(MenuBarState state, TextService textService, ClipboardService clipboardService, BannerService bannerService)
    {
        _state = state;
        _textService = textService;
        _clipboardService = clipboardService;
        _bannerService = bannerService;
        Initialize();
    }


    private void Initialize()
    {
        Key = "Paste";
        Icon = FontAwesomeIcon.Paste;
        TooltipText = _textService.Translate("EditorWindows.MenuBar.ImportFromClipboard.Label");
    }

    public override bool IsDisabled => _clipboardService.ClipboardPreset == null;

    public override void OnClick()
    {
        _bannerService.ImportPresetToState(_clipboardService.ClipboardPreset);
        _state.CloseOverlay();
    }
}
