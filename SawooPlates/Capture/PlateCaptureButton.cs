using Dalamud.Interface.Windowing;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace SawooPlates.Capture;

/// <summary>"Copy portrait" button drawn above an open Adventurer Plate.</summary>
public sealed unsafe class PlateCaptureButton : Window
{
    private readonly IFramework _framework;
    private readonly TextService _textService;
    private readonly CaptureService _captureService;
    private readonly CharaCardSource _source;
    private readonly PortraitActions _actions;

    public PlateCaptureButton(
        IFramework framework,
        TextService textService,
        CaptureService captureService,
        CharaCardSource source,
        PortraitActions actions)
        : base("##SawooPlatesPlateButton",
            ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoSavedSettings |
            ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoFocusOnAppearing | ImGuiWindowFlags.NoNav |
            ImGuiWindowFlags.NoBackground)
    {
        _framework = framework;
        _textService = textService;
        _captureService = captureService;
        _source = source;
        _actions = actions;

        IsOpen = true;
        RespectCloseHotkey = false;
        DisableWindowSounds = true;
    }

    public override bool DrawConditions()
    {
        if (!_captureService.Settings.ShowPlateButton)
            return false;

        var addon = AddonUtil.GetAddon<AtkUnitBase>(CharaCardSource.AddonName);
        if (addon == null || !addon->IsVisible)
            return false;

        // place it right above the top-right corner of the plate
        var height = ImGui.GetFrameHeight() + ImGui.GetStyle().WindowPadding.Y * 2;
        var width = addon->GetScaledWidth(true);
        Position = ImGui.GetMainViewport().Pos + new Vector2(addon->X + width - 180 * ImGuiHelpers.GlobalScale, addon->Y - height);
        PositionCondition = ImGuiCond.Always;
        return true;
    }

    public override void Draw()
    {
        if (ImGuiUtils.IconButton("CopyPortrait", FontAwesomeIcon.Copy, null))
            CopyPortrait();

        ImGui.SameLine(0, ImGui.GetStyle().ItemInnerSpacing.X);
        if (ImGui.Button(_textService.Translate("SawooPlates.PlateButton.Label")))
            CopyPortrait();

        if (ImGui.IsItemHovered())
            ImGui.SetTooltip(_textService.Translate("SawooPlates.PlateButton.Tooltip"));
    }

    private void CopyPortrait()
    {
        _framework.RunOnFrameworkThread(() =>
        {
            var capture = _source.TryCapture(manual: true);
            if (capture == null)
            {
                _captureService.Notify(_textService.Translate("SawooPlates.Notification.NothingToCapture"), Dalamud.Interface.ImGuiNotification.NotificationType.Warning);
                return;
            }

            _actions.CopyCode(capture.Preset, capture.Name);
        });
    }
}
