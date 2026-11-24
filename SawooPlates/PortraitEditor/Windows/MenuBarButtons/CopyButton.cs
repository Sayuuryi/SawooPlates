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

public class CopyButton : MenuBarButton
{
    private readonly TextService _textService;
    private readonly ClipboardService _clipboardService;
    private readonly IFramework _framework;

    public CopyButton(TextService textService, ClipboardService clipboardService, IFramework framework)
    {
        _textService = textService;
        _clipboardService = clipboardService;
        _framework = framework;
        Initialize();
    }


    private void Initialize()
    {
        Key = "Copy";
        Icon = FontAwesomeIcon.Copy;
        TooltipText = _textService.Translate("EditorWindows.MenuBar.ExportToClipboard.Label");
    }

    public override void OnClick()
    {
        _ = _framework.Run(() => _clipboardService.SetClipboardPortraitPreset(PortraitPresetExtensions.FromState()));
    }
}
