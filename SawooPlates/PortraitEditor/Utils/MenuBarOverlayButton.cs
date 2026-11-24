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

namespace SawooPlates.PortraitEditor.Utils;

public class MenuBarOverlayButton<T> : MenuBarButton where T : IOverlay
{
    private readonly MenuBarState _state;

    public MenuBarOverlayButton(MenuBarState state)
    {
        _state = state;
    }


    public override bool IsActive => _state.Overlay is T;

    public override void OnClick()
    {
        if (IsActive)
        {
            _state.CloseOverlay();
        }
        else
        {
            _state.OpenOverlay<T>();
        }
    }
}
