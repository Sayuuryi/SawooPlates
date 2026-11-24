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

namespace SawooPlates.PortraitEditor.Windows.Overlays;

public class AlignmentToolSettingsOverlay : Overlay
{
    private readonly TextService _textService;
    private readonly PluginConfig _pluginConfig;

    public AlignmentToolSettingsOverlay(TextService textService, PluginConfig pluginConfig, WindowSystem windowSystem, ExcelService excelService, MenuBarState state)
        : base(windowSystem, textService, pluginConfig, excelService, state)
    {
        _textService = textService;
        _pluginConfig = pluginConfig;
    }


    public override OverlayType Type => OverlayType.LeftPane;

    public override void Draw()
    {
        base.Draw();

        var config = _pluginConfig.PortraitEditor;

        ImGuiUtils.DrawSection(
            _textService.Translate("EditorWindows.AlignmentToolSettingsOverlay.Title.Inner"),
            pushDown: false,
            GetSectionColor());

        var changed = false;

        changed |= ImGui.Checkbox(_textService.Translate("EditorWindows.AlignmentToolSettingsOverlay.ShowAlignmentTool.Label"), ref config.ShowAlignmentTool);

        using var _ = ImRaii.Disabled(!config.ShowAlignmentTool);

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        ImGui.Text(_textService.Translate("EditorWindows.AlignmentToolSettingsOverlay.VerticalLines.Label"));
        ImGui.Indent();

        changed |= ImGui.SliderInt("##Vertical Lines", ref config.AlignmentToolVerticalLines, 0, 10);
        changed |= ImGui.ColorEdit4("##Vertical Color", ref config.AlignmentToolVerticalColor);

        ImGui.Unindent();
        ImGui.Text(_textService.Translate("EditorWindows.AlignmentToolSettingsOverlay.HorizontalLines.Label"));
        ImGui.Indent();

        changed |= ImGui.SliderInt("##Horizontal Lines", ref config.AlignmentToolHorizontalLines, 0, 10);
        changed |= ImGui.ColorEdit4("##Horizontal Color", ref config.AlignmentToolHorizontalColor);

        ImGui.Unindent();

        if (changed)
        {
            _pluginConfig.Save();
        }
    }
}
