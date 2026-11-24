namespace SawooPlates.Common;

public class ConfirmationButton(string label, ConfirmationButton.ClickCallbackDelegate clickCallback)
{
    public delegate void ClickCallbackDelegate();

    public string Label = label;
    public ClickCallbackDelegate ClickCallback = clickCallback;

    public bool Disabled { get; set; }

    internal bool Draw(int buttonIndex, float buttonWidth)
    {
        using var id = ImRaii.PushId(buttonIndex);
        using var disabled = ImRaii.Disabled(Disabled);

        if (ImGui.Button(Label, new Vector2(buttonWidth, ImStyle.FrameHeight)))
        {
            ClickCallback();
            return true;
        }

        return false;
    }
}

public abstract class ConfirmationDialog
{
    public string WindowName { get; set; } = string.Empty;

    private readonly List<ConfirmationButton> _buttons = [];
    private bool _shouldDraw;
    private bool _openCalled = true;

    public void Show()
    {
        _shouldDraw = true;
        _openCalled = false;
    }

    public void Hide()
        => _shouldDraw = false;

    public void AddButton(ConfirmationButton button)
        => _buttons.Add(button);

    public virtual bool DrawCondition()
        => _shouldDraw;

    public abstract void InnerDraw();

    public void Draw()
    {
        if (!DrawCondition()) return;

        if (!_openCalled)
        {
            ImGui.OpenPopup(WindowName);
            _openCalled = true;
        }

        ImGui.SetNextWindowPos(ImGui.GetMainViewport().GetCenter(), ImGuiCond.Appearing, new Vector2(0.5f));

        using var windowPadding = ImRaii.PushStyle(ImGuiStyleVar.WindowPadding, new Vector2(14, 10));

        if (!ImGui.BeginPopupModal(WindowName, ref _shouldDraw, ImGuiWindowFlags.NoSavedSettings | ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoScrollbar))
            return;

        try
        {
            InnerDraw();
        }
        catch (Exception ex)
        {
            ImGui.TextColored(Color.ErrorForeground, ex.Message);
        }

        ImGui.Spacing();

        var buttonSpacing = ImStyle.ItemSpacing.X;
        var buttonWidth = (int)Math.Max(80f, (ImStyle.ContentRegionAvail.X - buttonSpacing * (_buttons.Count - 1)) / _buttons.Count - 80);
        var totalWidth = _buttons.Count * buttonWidth + buttonSpacing * (_buttons.Count - 1);

        ImCursor.X = (ImGui.GetWindowWidth() - totalWidth) / 2f;

        foreach (var (index, button) in _buttons.Index())
        {
            if (button.Draw(index, buttonWidth))
            {
                _shouldDraw = false;
                ImGui.CloseCurrentPopup();
            }

            if (index < _buttons.Count)
                ImGui.SameLine();
        }

        ImGui.EndPopup();
    }
}
