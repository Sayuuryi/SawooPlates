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

public class AdvancedImportButton : MenuBarOverlayButton<AdvancedImportOverlay>
{
    private readonly TextService _textService;
    private readonly ClipboardService _clipboardService;

    public AdvancedImportButton(TextService textService, ClipboardService clipboardService, MenuBarState state)
        : base(state)
    {
        _textService = textService;
        _clipboardService = clipboardService;
        Initialize();
    }


    private void Initialize()
    {
        Key = "ToggleAdvancedImportMode";
        Icon = FontAwesomeIcon.FileImport;
        TooltipText = _textService.Translate("EditorWindows.MenuBar.ToggleAdvancedImportMode.Label");
    }

    public override bool IsDisabled => _clipboardService.ClipboardPreset == null;
}
