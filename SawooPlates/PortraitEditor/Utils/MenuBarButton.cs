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

public class MenuBarButton
{
    public virtual string Key { get; set; } = string.Empty;
    public virtual FontAwesomeIcon Icon { get; set; }
    public virtual string? TooltipText { get; set; } = null;
    public virtual bool IsActive { get; set; }
    public virtual bool IsDisabled { get; set; }

    public void Draw()
    {
        if (Icon == FontAwesomeIcon.None || string.IsNullOrEmpty(Key))
            return;

        var active = IsActive;
        var disabled = IsDisabled;

        using var color = ImRaii
            .PushColor(ImGuiCol.Button, 0xFFE19942, active)
            .Push(ImGuiCol.ButtonActive, 0xFFB06C2B, active)
            .Push(ImGuiCol.ButtonHovered, 0xFFCE8231, active);

        if (ImGuiUtils.IconButton(Key, Icon, TooltipText, disabled: disabled) && !disabled)
        {
            OnClick();
        }
    }

    public virtual void OnClick()
    {

    }
}
