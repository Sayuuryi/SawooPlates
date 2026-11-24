using SawooPlates.PortraitEditor.Services;

namespace SawooPlates.Windows;

/// <summary>Shows a portrait preset: image with frame/accent and the decoded values.</summary>
public sealed class PresetDetailPanel(
    ITextureProvider textureProvider,
    TextService textService,
    ExcelService excelService)
{
    public static readonly Vector2 PortraitSize = new(576, 960); // native portrait texture size

    /// <summary>Draws the portrait image layers at the cursor with the given height.</summary>
    public void DrawPortrait(PortraitPresetData preset, ThumbnailService thumbnails, Guid imageId, float height, bool frameKnown = true, bool decorationKnown = true)
    {
        var size = new Vector2(height * PortraitSize.X / PortraitSize.Y, height);
        var start = ImCursor.Position;

        // background first, the captured image may or may not include it
        if (excelService.TryGetRow<BannerBg>(preset.BannerBg, out var bg) && bg.Image != 0)
            textureProvider.DrawIcon((uint)bg.Image, size);
        else
            textureProvider.DrawIcon(190009, size);

        if (thumbnails.TryGetThumbnail(imageId, (int)size.X, (int)size.Y, out _, out var wrap, out _) && wrap != null)
        {
            ImCursor.Position = start;
            ImGui.Image(wrap.Handle, size);
        }

        if (frameKnown && excelService.TryGetRow<BannerFrame>(preset.BannerFrame, out var frame) && frame.Image != 0)
        {
            ImCursor.Position = start;
            textureProvider.DrawIcon((uint)frame.Image, size);
        }

        if (decorationKnown && excelService.TryGetRow<BannerDecoration>(preset.BannerDecoration, out var decoration) && decoration.Image != 0)
        {
            ImCursor.Position = start;
            textureProvider.DrawIcon((uint)decoration.Image, size);
        }

        ImCursor.Position = start;
        ImGui.Dummy(size);
    }

    /// <summary>Camera, animation and lighting values (the parts are listed by PlateItems).</summary>
    public void DrawValues(PortraitPresetData preset)
    {
        var brightness = textService.GetAddonText(14694).OrDefault("Brightness");
        var animation = textService.Translate("EditorWindows.Setting.AnimationTimestamp.Label");
        var zoom = textService.GetAddonText(14711).OrDefault("Zoom");
        var rotation = textService.GetAddonText(14712).OrDefault("Rotation");
        var position = textService.GetAddonText(5972).OrDefault("Camera Position");
        var target = textService.Translate("EditorWindows.Setting.CameraTarget.Label");
        var head = textService.Translate("EditorWindows.Setting.HeadDirection.Label");
        var eyes = textService.Translate("EditorWindows.Setting.EyeDirection.Label");
        var ambient = textService.GetAddonText(14692).OrDefault("Ambient Lighting");
        var directional = textService.GetAddonText(14693).OrDefault("Directional Lighting");

        // label column as wide as the longest label, so nothing gets cut
        var labelWidth = new[] { animation, zoom, rotation, position, target, head, eyes, ambient, directional }
            .Max(label => ImGui.CalcTextSize(label).X) + ImGui.GetStyle().CellPadding.X * 2;

        using var table = ImRaii.Table("##PresetValues", 2, ImGuiTableFlags.SizingStretchProp | ImGuiTableFlags.RowBg);
        if (!table)
            return;

        ImGui.TableSetupColumn("Label", ImGuiTableColumnFlags.WidthFixed, labelWidth);
        ImGui.TableSetupColumn("Value", ImGuiTableColumnFlags.WidthStretch);

        Row(animation, preset.AnimationProgress.ToString("0.000"));
        Row(zoom, preset.CameraZoom.ToString());
        Row(rotation, preset.ImageRotation.ToString());
        Row(position, Format(preset.CameraPosition));
        Row(target, Format(preset.CameraTarget));
        Row(head, Format(preset.HeadDirection));
        Row(eyes, Format(preset.EyeDirection));

        ColorRow(ambient,
            preset.AmbientLightingColorRed, preset.AmbientLightingColorGreen, preset.AmbientLightingColorBlue,
            $"{brightness} {preset.AmbientLightingBrightness}");

        ColorRow(directional,
            preset.DirectionalLightingColorRed, preset.DirectionalLightingColorGreen, preset.DirectionalLightingColorBlue,
            $"{brightness} {preset.DirectionalLightingBrightness}, {textService.GetAddonText(14696).OrDefault("Vertical Angle")} {preset.DirectionalLightingVerticalAngle}, {textService.GetAddonText(14695).OrDefault("Horizontal Angle")} {preset.DirectionalLightingHorizontalAngle}");
    }

    private static string Format(HalfVec2 v) => $"{(float)v.X:0.000}, {(float)v.Y:0.000}";
    private static string Format(HalfVec4 v) => $"{(float)v.X:0.000}, {(float)v.Y:0.000}, {(float)v.Z:0.000}, {(float)v.W:0.000}";

    private static void Row(string label, string value, bool dim = false)
    {
        ImGui.TableNextRow();
        ImGui.TableNextColumn();
        ImGui.TextColored(Color.Gold, label);
        ImGui.TableNextColumn();
        if (dim)
            ImGui.TextDisabled(value);
        else
            ImGui.TextWrapped(value);
    }

    private static void ColorRow(string label, byte r, byte g, byte b, string extra)
    {
        ImGui.TableNextRow();
        ImGui.TableNextColumn();
        ImGui.TextColored(Color.Gold, label);
        ImGui.TableNextColumn();
        var color = new Vector4(r / 255f, g / 255f, b / 255f, 1f);
        ImGui.ColorButton("##Swatch", color, ImGuiColorEditFlags.NoTooltip | ImGuiColorEditFlags.NoPicker, new Vector2(ImGui.GetTextLineHeight()));
        ImGui.SameLine();
        ImGui.TextWrapped($"{r}, {g}, {b} · {extra}");
    }
}
