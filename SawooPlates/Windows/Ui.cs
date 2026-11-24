using Dalamud.Interface.Components;
using Dalamud.Interface.Textures;
using SawooPlates.Capture;

namespace SawooPlates.Windows;

/// <summary>Small drawing helpers shared by the tabs, so everything looks the same.</summary>
public static class Ui
{
    public static readonly Vector4 Good = new(0.47f, 0.84f, 0.47f, 1f);
    public static readonly Vector4 Bad = new(0.96f, 0.50f, 0.42f, 1f);
    public static readonly Vector4 Accent = new(0.85f, 0.73f, 0.49f, 1f);
    public static readonly Vector4 Warn = new(0.95f, 0.78f, 0.35f, 1f);

    public static float Scale => ImGuiHelpers.GlobalScale;

    public static Vector4 Dim => ImGui.GetStyle().Colors[(int)ImGuiCol.TextDisabled];

    /// <summary>Gold section title with a count, followed by a separator.</summary>
    public static void Section(string title, string? right = null)
    {
        ImGui.Spacing();
        ImGui.TextColored(Accent, title);
        if (right != null)
        {
            // count or note at the right edge, away from the title
            ImGui.SameLine(ImGui.GetContentRegionAvail().X + ImGui.GetCursorPosX() - ImGui.CalcTextSize(right).X);
            ImGui.TextColored(Dim, right);
        }

        ImGui.Separator();
        ImGui.Spacing();
    }

    /// <summary>
    /// Picture on the left at a fixed width, parts on the right taking the rest. Always side by side;
    /// resizing the window only changes the right side.
    /// </summary>
    public static void DetailColumns(string id, float visualWidth, Action visual, Action parts)
    {
        var gap = 14 * Scale;

        using (var child = ImRaii.Child($"##{id}Visual", new Vector2(visualWidth, -1), false))
        {
            if (child)
                visual();
        }

        ImGui.SameLine(0, gap);
        using (var child = ImRaii.Child($"##{id}Parts", new Vector2(-1, -1), false))
        {
            if (child)
                parts();
        }
    }

    /// <summary>Width of the picture column: a fixed size, so it doesn't change when the window is resized.</summary>
    public static float VisualWidth(bool hasPlate) => (hasPlate ? 420 : 240) * Scale;

    public static bool Button(FontAwesomeIcon icon, string text, bool disabled = false, string? tooltip = null)
    {
        bool clicked;
        using (ImRaii.Disabled(disabled))
            clicked = ImGuiComponents.IconButtonWithText(icon, text);

        if (tooltip != null && ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
            ImGui.SetTooltip(tooltip);

        return clicked && !disabled;
    }

    public static bool IconButton(string id, FontAwesomeIcon icon, string tooltip, bool disabled = false)
    {
        bool clicked;
        using (ImRaii.PushId(id))
        using (ImRaii.Disabled(disabled))
            clicked = ImGuiComponents.IconButton(icon);

        if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
            ImGui.SetTooltip(tooltip);

        return clicked && !disabled;
    }

    /// <summary>
    /// Trash button that only works while Ctrl is held, so nothing gets deleted by a stray click.
    /// Turns red while Ctrl is down.
    /// </summary>
    public static bool CtrlDeleteButton(string id, string what, string? text = null)
    {
        var ctrl = ImGui.GetIO().KeyCtrl;
        bool clicked;

        using (ImRaii.PushId(id))
        using (ImRaii.PushColor(ImGuiCol.Text, Bad, !ctrl))
        {
            clicked = text == null
                ? ImGuiComponents.IconButton(FontAwesomeIcon.Trash)
                : ImGuiComponents.IconButtonWithText(FontAwesomeIcon.Trash, text);
        }

        if (ImGui.IsItemHovered())
            ImGui.SetTooltip(ctrl ? what : $"{what}\nHold Ctrl and click.");

        return clicked && ctrl;
    }

    public static void Icon(FontAwesomeIcon icon, Vector4? color = null)
    {
        using (ImRaii.PushColor(ImGuiCol.Text, color ?? Vector4.One, color != null))
        using (ImRaii.PushFont(UiBuilder.IconFont))
            ImGui.Text(icon.ToIconString());
    }

    /// <summary>Check, cross or question mark, with a tooltip saying where it is or how to get it.</summary>
    public static void OwnBadge(Ownership ownership, bool tooltip = true)
    {
        switch (ownership.State)
        {
            case OwnState.Have:
                Icon(FontAwesomeIcon.Check, Good);
                if (tooltip && ImGui.IsItemHovered())
                    ImGui.SetTooltip(ownership.Where is { } where ? $"You have this ({where})" : "You have this");
                break;

            case OwnState.Missing:
                Icon(FontAwesomeIcon.Times, Bad);
                if (tooltip && ImGui.IsItemHovered())
                {
                    using var tip = ImRaii.Tooltip();
                    ImGui.TextColored(Bad, "You don't have this");
                    if (ownership.Hint != null)
                    {
                        using (ImRaii.TextWrapPos(ImGui.GetFontSize() * 22))
                            ImGui.TextUnformatted(ownership.Hint);
                    }
                }
                break;

            default:
                Icon(FontAwesomeIcon.Question, Dim);
                if (tooltip && ImGui.IsItemHovered() && ownership.Hint != null)
                {
                    using var tip = ImRaii.Tooltip();
                    using (ImRaii.TextWrapPos(ImGui.GetFontSize() * 22))
                        ImGui.TextUnformatted(ownership.Hint);
                }
                break;
        }
    }

    /// <summary>Draws a game texture scaled to fit inside a square box, centered, keeping its aspect ratio.</summary>
    /// <summary>Draws a game image as large as fits in the box, keeping its shape.</summary>
    public static void ImageFit(ITextureProvider textures, uint iconId, Vector2 box)
    {
        if (iconId == 0
            || !textures.TryGetFromGameIcon(new GameIconLookup(iconId), out var texture)
            || !texture.TryGetWrap(out var wrap, out _))
        {
            ImGui.Dummy(box);
            return;
        }

        var scale = Math.Min(box.X / wrap.Width, box.Y / wrap.Height);
        ImGui.Image(wrap.Handle, new Vector2(wrap.Width, wrap.Height) * scale);
    }

    public static void IconFit(ITextureProvider textures, uint iconId, float box)
    {
        var start = ImGui.GetCursorPos();
        if (iconId != 0
            && textures.TryGetFromGameIcon(new GameIconLookup(iconId), out var texture)
            && texture.TryGetWrap(out var wrap, out _))
        {
            var w = (float)wrap.Width;
            var h = (float)wrap.Height;
            var scale = box / Math.Max(w, h);
            var size = new Vector2(w * scale, h * scale);
            ImGui.SetCursorPos(start + (new Vector2(box) - size) / 2f);
            ImGui.Image(wrap.Handle, size);
        }

        ImGui.SetCursorPos(start);
        ImGui.Dummy(new Vector2(box));
    }

    public static void JobIcon(ITextureProvider textures, byte jobId, float size)
    {
        if (jobId == 0)
        {
            ImGui.Dummy(new Vector2(size));
            return;
        }

        textures.DrawIcon(62100u + jobId, size);
    }

    /// <summary>A small colored square for a dye, with its name next to it.</summary>
    public static void DyeChip(uint rgb, string name)
    {
        var size = ImGui.GetTextLineHeight() * 0.8f;
        var pos = ImGui.GetCursorScreenPos() + new Vector2(0, (ImGui.GetTextLineHeight() - size) / 2f);
        var color = new Vector4(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f, 1f);
        var draw = ImGui.GetWindowDrawList();
        draw.AddRectFilled(pos, pos + new Vector2(size), ImGui.ColorConvertFloat4ToU32(color), 2f * Scale);
        draw.AddRect(pos, pos + new Vector2(size), ImGui.GetColorU32(ImGuiCol.Border), 2f * Scale);
        ImGui.Dummy(new Vector2(size, ImGui.GetTextLineHeight()));
        ImGui.SameLine(0, 4 * Scale);
        ImGui.TextColored(Dim, name);
    }

    /// <summary>Centered icon, title and one line of text for empty lists.</summary>
    public static void EmptyState(FontAwesomeIcon icon, string title, string text)
    {
        var avail = ImGui.GetContentRegionAvail();
        var wrap = Math.Min(avail.X - 40 * Scale, 360 * Scale);
        ImGui.SetCursorPosY(ImGui.GetCursorPosY() + Math.Max(0, avail.Y * 0.3f));

        using (ImRaii.PushFont(UiBuilder.IconFont))
        {
            var iconSize = ImGui.CalcTextSize(icon.ToIconString());
            ImGui.SetCursorPosX((avail.X - iconSize.X) / 2f);
            ImGui.TextColored(Dim, icon.ToIconString());
        }

        ImGui.Spacing();
        var titleSize = ImGui.CalcTextSize(title);
        ImGui.SetCursorPosX((avail.X - titleSize.X) / 2f);
        ImGui.TextUnformatted(title);

        var textSize = ImGui.CalcTextSize(text, wrapWidth: wrap);
        ImGui.SetCursorPosX((avail.X - textSize.X) / 2f);
        using (ImRaii.PushColor(ImGuiCol.Text, Dim))
        using (ImRaii.TextWrapPos(ImGui.GetCursorPosX() + wrap))
            ImGui.TextUnformatted(text);
    }

    public static string TimeAgo(DateTimeOffset time)
    {
        var span = DateTimeOffset.UtcNow - time;
        if (span.TotalMinutes < 1) return "just now";
        if (span.TotalHours < 1) return $"{(int)span.TotalMinutes}m ago";
        if (span.TotalDays < 1) return $"{(int)span.TotalHours}h ago";
        if (span.TotalDays < 30) return $"{(int)span.TotalDays}d ago";
        return time.ToLocalTime().ToString("d MMM yyyy");
    }

    public static FontAwesomeIcon SourceIcon(CaptureSource source) => source switch
    {
        CaptureSource.PartyPortrait => FontAwesomeIcon.Users,
        CaptureSource.MvpVote => FontAwesomeIcon.Trophy,
        _ => FontAwesomeIcon.IdCard,
    };

    public static string SourceName(CaptureSource source) => source switch
    {
        CaptureSource.PartyPortrait => "Party portrait",
        CaptureSource.MvpVote => "MVP vote",
        _ => "Adventurer Plate",
    };
}
