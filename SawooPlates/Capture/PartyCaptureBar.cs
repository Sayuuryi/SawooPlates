using Dalamud.Interface.Windowing;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace SawooPlates.Capture;

/// <summary>
/// A bar above the party portrait window (duty start) and the MVP vote window with one
/// "copy" button per member.
/// </summary>
public sealed unsafe class PartyCaptureBar : Window
{
    private readonly IFramework _framework;
    private readonly TextService _textService;
    private readonly CaptureService _captureService;
    private readonly BannerPartySource _source;
    private readonly PortraitActions _actions;

    private CaptureSource _shown;
    private List<BannerPartySource.Member> _members = [];

    public PartyCaptureBar(
        IFramework framework,
        TextService textService,
        CaptureService captureService,
        BannerPartySource source,
        PortraitActions actions)
        : base("##SawooPlatesPartyBar",
            ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoSavedSettings |
            ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoFocusOnAppearing | ImGuiWindowFlags.NoNav)
    {
        _framework = framework;
        _textService = textService;
        _captureService = captureService;
        _source = source;
        _actions = actions;

        IsOpen = true;
        RespectCloseHotkey = false;
        DisableWindowSounds = true;
        BgAlpha = 0.85f;
    }

    public override bool DrawConditions()
    {
        if (!_captureService.Settings.ShowPartyButtons)
            return false;

        foreach (var source in new[] { CaptureSource.MvpVote, CaptureSource.PartyPortrait })
        {
            var addon = AddonUtil.GetAddon<AtkUnitBase>(BannerPartySource.GetAgentId(source));
            if (addon == null || !addon->IsVisible)
                continue;

            _shown = source;

            var height = ImGui.GetFrameHeight() + ImGui.GetStyle().WindowPadding.Y * 2;
            Position = ImGui.GetMainViewport().Pos + new Vector2(addon->X, addon->Y - height - 2);
            PositionCondition = ImGuiCond.Always;
            return true;
        }

        return false;
    }

    public override void PreDraw()
    {
        // member list is read on the framework thread; draw uses the last copy
        var source = _shown;
        _framework.RunOnFrameworkThread(() => _members = _source.GetMembers(source));
    }

    public override void Draw()
    {
        ImGuiUtils.Icon(FontAwesomeIcon.Copy);
        ImGui.SameLine();
        ImGui.Text(_textService.Translate("SawooPlates.PartyBar.Label"));

        foreach (var member in _members)
        {
            ImGui.SameLine();
            using var id = ImRaii.PushId(member.Index);

            var label = string.IsNullOrEmpty(member.Job) ? FirstName(member.Name) : $"{FirstName(member.Name)} ({member.Job})";
            if (ImGui.Button(label))
                Copy(member);

            if (ImGui.IsItemHovered())
                ImGui.SetTooltip(_textService.Translate("SawooPlates.PartyBar.Tooltip", member.Name));
        }

        ImGui.SameLine();
        if (ImGuiUtils.IconButton("SaveAll", FontAwesomeIcon.Download, _textService.Translate("SawooPlates.PartyBar.SaveAll")))
        {
            var source = _shown;
            _framework.RunOnFrameworkThread(() =>
            {
                var count = _source.CaptureAll(source, manual: true);
                _captureService.Notify(_textService.Translate("SawooPlates.Notification.SavedPortraits", count));
            });
        }
    }

    private void Copy(BannerPartySource.Member member)
    {
        var source = _shown;
        _framework.RunOnFrameworkThread(() =>
        {
            var capture = _source.Capture(source, member.Index, manual: true);
            if (capture == null)
            {
                _captureService.Notify(_textService.Translate("SawooPlates.Notification.NothingToCapture"), Dalamud.Interface.ImGuiNotification.NotificationType.Warning);
                return;
            }

            _actions.CopyCode(capture.Preset, capture.Name);
        });
    }

    private static string FirstName(string name)
    {
        var space = name.IndexOf(' ');
        return space > 0 ? name[..space] : name;
    }
}
