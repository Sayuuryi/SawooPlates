using Dalamud.Game.Gui.ContextMenu;
using Dalamud.Game.Text.SeStringHandling;

namespace SawooPlates.Capture;

/// <summary>
/// "Copy plate portrait" in a player's context menu. Only shown when that player's plate data is
/// already on this client (their plate is open, or a capture of them is in the history).
/// </summary>
public sealed class PlateContextMenu : IDisposable
{
    private readonly IContextMenu _contextMenu;
    private readonly TextService _textService;
    private readonly CaptureService _captureService;
    private readonly CharaCardSource _source;
    private readonly PortraitActions _actions;

    public PlateContextMenu(
        IContextMenu contextMenu,
        TextService textService,
        CaptureService captureService,
        CharaCardSource source,
        PortraitActions actions)
    {
        _contextMenu = contextMenu;
        _textService = textService;
        _captureService = captureService;
        _source = source;
        _actions = actions;

        _contextMenu.OnMenuOpened += OnMenuOpened;
    }

    public void Dispose()
    {
        _contextMenu.OnMenuOpened -= OnMenuOpened;
    }

    private void OnMenuOpened(IMenuOpenedArgs args)
    {
        if (!_captureService.Settings.ShowButtons)
            return;

        if (args.Target is not MenuTargetDefault target || target.TargetContentId == 0)
            return;

        var contentId = target.TargetContentId;
        var plateOpen = _source.OpenPlateContentId == contentId;
        var known = plateOpen || _captureService.History.Entries.Any(e => e.ContentId == contentId);
        if (!known)
            return;

        args.AddMenuItem(new MenuItem
        {
            Name = new SeStringBuilder().AddText(_textService.Translate("SawooPlates.ContextMenu.CopyPlatePortrait")).Build(),
            PrefixChar = 'S',
            OnClicked = _ =>
            {
                // the plate that is open now is the newest data; otherwise use the newest capture
                var capture = plateOpen ? _source.TryCapture(manual: true) : null;
                capture ??= _captureService.History.Entries.FirstOrDefault(e => e.ContentId == contentId);
                if (capture != null)
                    _actions.CopyCode(capture.Preset, capture.Name);
            },
        });
    }
}
