using Dalamud.Interface.Windowing;
using SawooPlates.Windows;
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
    private readonly IServiceProvider _services;

    public PlateCaptureButton(
        IFramework framework,
        TextService textService,
        CaptureService captureService,
        CharaCardSource source,
        PortraitActions actions,
        IServiceProvider services)
        : base("##SawooPlatesPlateButton",
            ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoSavedSettings |
            ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoFocusOnAppearing | ImGuiWindowFlags.NoNav)
    {
        _framework = framework;
        _textService = textService;
        _captureService = captureService;
        _source = source;
        _actions = actions;
        _services = services;

        IsOpen = true;
        RespectCloseHotkey = false;
        DisableWindowSounds = true;
    }

    // size of the panel last frame, used to place it and to test whether a game window covers it
    private Vector2 _size = new(250, 34);
    private IDisposable? _style;

    public override bool DrawConditions()
    {
        if (!_captureService.Settings.ShowButtons)
            return false;

        var addon = AddonUtil.GetAddon<AtkUnitBase>(CharaCardSource.AddonName);
        if (addon == null || !addon->IsVisible)
            return false;

        // right above the top-right corner of the plate
        var topLeft = new Vector2(addon->X + addon->GetScaledWidth(true) - _size.X, addon->Y - _size.Y - 2);

        // hide while another game window is in front of the plate here
        if (OverlayStyle.IsCovered(addon, topLeft, topLeft + _size))
            return false;

        Position = ImGui.GetMainViewport().Pos + topLeft;
        PositionCondition = ImGuiCond.Always;
        return true;
    }

    public override void PreDraw() => _style = OverlayStyle.PushPanel();

    public override void PostDraw()
    {
        _style?.Dispose();
        _style = null;
        OverlayStyle.SendToBack(WindowName);
    }

    public override void Draw()
    {
        _size = ImGui.GetWindowSize();

        if (Ui.Button(FontAwesomeIcon.Save, "Save plate", tooltip: "Save this portrait and plate design as a preset."))
            SavePlate();

        ImGui.SameLine();
        if (Ui.Button(FontAwesomeIcon.Copy, "Copy code", tooltip: "Keep this plate in Recent and copy its portrait code."))
            CopyPortrait();
    }

    private void SavePlate()
    {
        _framework.RunOnFrameworkThread(() =>
        {
            var capture = _source.TryCapture(manual: true);
            if (capture == null)
            {
                _captureService.Notify(_textService.Translate("SawooPlates.Notification.NothingToCapture"), Dalamud.Interface.ImGuiNotification.NotificationType.Warning);
                return;
            }

            var preset = _actions.SaveAsPreset(capture, capture.DefaultPresetName);
            _services.GetRequiredService<MainWindow>().ShowPreset(preset.Id);
        });
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
