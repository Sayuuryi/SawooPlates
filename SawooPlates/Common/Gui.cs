using FFXIVClientStructs.FFXIV.Client.UI;

namespace SawooPlates.Common;

public static class ImCursor
{
    public static Vector2 Position
    {
        get => ImGui.GetCursorPos();
        set => ImGui.SetCursorPos(value);
    }

    public static float X
    {
        get => ImGui.GetCursorPosX();
        set => ImGui.SetCursorPosX(value);
    }

    public static float Y
    {
        get => ImGui.GetCursorPosY();
        set => ImGui.SetCursorPosY(value);
    }

    public static Vector2 ScreenPosition
    {
        get => ImGui.GetCursorScreenPos();
        set => ImGui.SetCursorScreenPos(value);
    }
}

public static class ImStyle
{
    public static float Scale => ImGuiHelpers.GlobalScale;
    public static float FrameHeight => ImGui.GetFrameHeight();
    public static float TextLineHeight => ImGui.GetTextLineHeight();
    public static Vector2 ContentRegionAvail => ImGui.GetContentRegionAvail();
    public static Vector2 WindowPadding => ImGui.GetStyle().WindowPadding;
    public static Vector2 FramePadding => ImGui.GetStyle().FramePadding;
    public static Vector2 ItemSpacing => ImGui.GetStyle().ItemSpacing;
    public static Vector2 ItemInnerSpacing => ImGui.GetStyle().ItemInnerSpacing;
}

public readonly ref struct ImRaiiListClipper
{
    public readonly ImGuiListClipperPtr ClipperPtr = ImGui.ImGuiListClipper();

    public ImRaiiListClipper(int itemsCount, float itemsHeight = -1.0f)
    {
        ClipperPtr.Begin(itemsCount, itemsHeight);
    }

    public void Dispose()
    {
        ClipperPtr.End();
    }

    public Enumerator GetEnumerator() => new(ClipperPtr);

    public ref struct Enumerator(ImGuiListClipperPtr clipper)
    {
        public int Current { get; private set; } = -1;

        public bool MoveNext()
        {
            if (Current != -1 && Current < clipper.DisplayEnd - 1)
            {
                Current++;
                return true;
            }

            if (!clipper.Step())
                return false;

            Current = clipper.DisplayStart;

            if (Current >= clipper.DisplayEnd)
                return MoveNext();

            return true;
        }
    }
}

public static class ImGuiUtils
{
    public static void DrawSection(string label, bool pushDown = true, Color? color = null)
    {
        var itemSpacingHeight = ImStyle.ItemSpacing.Y;

        // push down a bit
        if (pushDown)
            ImCursor.Y += itemSpacingHeight * 2;

        ImGui.TextColored(color ?? Color.Gold, label);

        // pull up the separator
        ImCursor.Y += -itemSpacingHeight + 3;
        ImGui.Separator();
        ImCursor.Y += itemSpacingHeight * 2 - 1;
    }

    public static void VerticalSeparator(uint color)
    {
        ImGui.SameLine();

        var height = ImStyle.FrameHeight;
        var pos = ImCursor.ScreenPosition;

        ImGui.GetWindowDrawList().AddLine(
            pos + new Vector2(3f, 0f),
            pos + new Vector2(3f, height),
            color);

        ImGui.Dummy(new(7, height));
    }

    public static void VerticalSeparator()
        => VerticalSeparator(ImGui.GetColorU32(ImGuiCol.Separator));

    public static void Icon(FontAwesomeIcon icon, uint? color = null)
    {
        using (ImRaii.PushColor(ImGuiCol.Text, color ?? 0u, color != null))
        using (ImRaii.PushFont(UiBuilder.IconFont))
            ImGui.Text(icon.ToIconString());
    }

    public static bool IconButton(string key, FontAwesomeIcon icon, string? tooltip = null, Vector2 size = default, bool disabled = false, bool active = false)
    {
        using var iconFont = ImRaii.PushFont(UiBuilder.IconFont);
        if (!key.StartsWith("##")) key = "##" + key;

        using var color = new ImRaii.ColorDisposable();

        if (disabled)
        {
            color.Push(ImGuiCol.Text, ImGui.GetColorU32(ImGuiCol.TextDisabled))
                 .Push(ImGuiCol.ButtonActive, ImGui.GetColorU32(ImGuiCol.Button))
                 .Push(ImGuiCol.ButtonHovered, ImGui.GetColorU32(ImGuiCol.Button));
        }
        else if (active)
        {
            color.Push(ImGuiCol.Button, ImGui.GetColorU32(ImGuiCol.ButtonActive));
        }

        var pressed = ImGui.Button(icon.ToIconString() + key, size);

        color.Dispose();
        iconFont.Dispose();

        if (tooltip != null && ImGui.IsItemHovered())
        {
            ImGui.BeginTooltip();
            ImGui.Text(tooltip);
            ImGui.EndTooltip();
        }

        return pressed;
    }
}

public static class GameMisc
{
    public static unsafe bool IsLightTheme
        => RaptureAtkModule.Instance()->AtkUIColorHolder.ActiveColorThemeType is 1 or 4;
}
