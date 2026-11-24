using FFXIVClientStructs.FFXIV.Client.UI.Agent;

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

public unsafe class ResetButton : MenuBarButton
{
    private readonly MenuBarState _state;
    private readonly TextService _textService;
    private readonly BannerService _bannerService;

    public ResetButton(MenuBarState state, TextService textService, BannerService bannerService)
    {
        _state = state;
        _textService = textService;
        _bannerService = bannerService;
        Initialize();
    }


    private void Initialize()
    {
        Key = "Reset";
        Icon = FontAwesomeIcon.Undo;
        TooltipText = _textService.GetAddonText(4830); // Reset
    }

    public override bool IsDisabled
    {
        get
        {
            var agent = AgentBannerEditor.Instance();
            return agent == null || agent->EditorState == null || !agent->EditorState->HasDataChanged;
        }
    }

    public override void OnClick()
    {
        _bannerService.ImportPresetToState(_state.InitialPreset);

        AgentBannerEditor.Instance()->EditorState->SetHasChanged(false);
    }
}
