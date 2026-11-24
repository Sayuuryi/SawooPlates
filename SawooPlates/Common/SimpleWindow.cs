using Dalamud.Interface.Windowing;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace SawooPlates.Common;

public abstract class SimpleWindow : Window, IDisposable
{
    private readonly WindowSystem _windowSystem;
    private readonly TextService _textService;
    private bool _registered;

    public readonly ImRaii.StyleDisposable WindowStyle = new();
    public readonly ImRaii.ColorDisposable WindowColor = new();

    protected SimpleWindow(WindowSystem windowSystem, TextService textService, string? windowNameKey = null)
        : base($"SimpleWindow###{Guid.NewGuid()}", ImGuiWindowFlags.NoFocusOnAppearing, false)
    {
        _windowSystem = windowSystem;
        _textService = textService;

        var key = windowNameKey ?? $"{GetType().Name}.Title";
        WindowName = $"{_textService.Translate(key)}###{GetType().FullName}";

        _windowSystem.AddWindow(this);
        _registered = true;
    }

    public virtual void Dispose()
    {
        WindowStyle.Dispose();
        WindowColor.Dispose();
        IsOpen = false;

        if (_registered)
        {
            _windowSystem.RemoveWindow(this);
            _registered = false;
        }
    }

    public void Open(bool focus = true)
    {
        IsOpen = true;
        Collapsed = false;

        if (focus)
            BringToFront();
    }

    public void Close()
    {
        IsOpen = false;
    }

    public override void OnClose()
    {
        WindowStyle.Dispose();
        WindowColor.Dispose();
        base.OnClose();
    }

    public override unsafe bool DrawConditions()
    {
        return AtkStage.Instance()->Filter.NumActiveFilters == 0;
    }

    public override void PostDraw()
    {
        WindowStyle.Dispose();
        WindowColor.Dispose();

        base.PostDraw();

        if (Collapsed != null)
            Collapsed = null;
    }
}
