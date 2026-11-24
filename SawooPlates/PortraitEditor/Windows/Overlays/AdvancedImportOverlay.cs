using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Common.Math;

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

public unsafe class AdvancedImportOverlay : Overlay
{
    private readonly MenuBarState _state;
    private readonly TextService _textService;
    private readonly ExcelService _excelService;
    private readonly BannerService _bannerService;
    private readonly ClipboardService _clipboardService;

    public AdvancedImportOverlay(MenuBarState state, TextService textService, ExcelService excelService, BannerService bannerService, ClipboardService clipboardService, WindowSystem windowSystem, PluginConfig pluginConfig)
        : base(windowSystem, textService, pluginConfig, excelService, state)
    {
        _state = state;
        _textService = textService;
        _excelService = excelService;
        _bannerService = bannerService;
        _clipboardService = clipboardService;
    }


    public override void Draw()
    {
        base.Draw();

        if (_clipboardService.ClipboardPreset == null)
        {
            Close();
            return;
        }

        if (IsWindow)
            ImCursor.Y += ImStyle.ItemSpacing.Y * 2;

        var state = AgentBannerEditor.Instance()->EditorState;
        var unknown = _textService.GetAddonText(624).OrDefault("Unknown");

        if (ImGui.Button(_textService.GetAddonText(14923).OrDefault("Select All")))
            _clipboardService.CurrentImportFlags = ImportFlags.All;

        ImGui.SameLine();

        if (ImGui.Button(_textService.GetAddonText(14924).OrDefault("Deselect All")))
            _clipboardService.CurrentImportFlags = ImportFlags.None;

        ImGui.SameLine();

        if (ImGui.Button(_textService.Translate("EditorWindows.AdvancedImportOverlay.ImportSelectedSettingsButton.Label")))
        {
            _bannerService.ImportPresetToState(_clipboardService.ClipboardPreset, _clipboardService.CurrentImportFlags);
            _state.CloseOverlay();
        }

        ImGuiUtils.DrawSection(_textService.GetAddonText(14684).OrDefault("Design"), true, GetSectionColor());

        var isBannerBgUnlocked = _bannerService.IsBannerBgUnlocked(_clipboardService.ClipboardPreset.BannerBg);
        DrawImportSetting(
            _textService.GetAddonText(14687).OrDefault("Background"),
            ImportFlags.BannerBg,
            () =>
            {
                if (_excelService.TryGetRow<BannerBg>(_clipboardService.ClipboardPreset.BannerBg, out var bannerBgRow))
                    ImGui.Text(bannerBgRow.Name.ToString());
                else
                    ImGui.Text(unknown);

                if (!isBannerBgUnlocked)
                {
                    ImGui.TextColored(Color.ErrorForeground, _textService.Translate("EditorWindows.AdvancedImportOverlay.NotUnlocked"));
                }
            },
            isBannerBgUnlocked
        );

        var isBannerFrameUnlocked = _bannerService.IsBannerFrameUnlocked(_clipboardService.ClipboardPreset.BannerFrame);
        DrawImportSetting(
            _textService.GetAddonText(14688).OrDefault("Frame"),
            ImportFlags.BannerFrame,
            () =>
            {
                if (_excelService.TryGetRow<BannerFrame>(_clipboardService.ClipboardPreset.BannerFrame, out var bannerFrameRow))
                    ImGui.Text(bannerFrameRow.Name.ToString());
                else
                    ImGui.Text(unknown);

                if (!isBannerFrameUnlocked)
                {
                    ImGui.TextColored(Color.ErrorForeground, _textService.Translate("EditorWindows.AdvancedImportOverlay.NotUnlocked"));
                }
            },
            isBannerFrameUnlocked
        );

        var isBannerDecorationUnlocked = _bannerService.IsBannerDecorationUnlocked(_clipboardService.ClipboardPreset.BannerDecoration);
        DrawImportSetting(
            _textService.GetAddonText(14689).OrDefault("Accent"),
            ImportFlags.BannerDecoration,
            () =>
            {
                if (_excelService.TryGetRow<BannerDecoration>(_clipboardService.ClipboardPreset.BannerDecoration, out var bannerDecorationRow))
                    ImGui.Text(bannerDecorationRow.Name.ToString());
                else
                    ImGui.Text(unknown);

                if (!isBannerDecorationUnlocked)
                {
                    ImGui.TextColored(Color.ErrorForeground, _textService.Translate("EditorWindows.AdvancedImportOverlay.NotUnlocked"));
                }
            },
            isBannerDecorationUnlocked
        );

        DrawImportSetting(
            _textService.GetAddonText(14711).OrDefault("Zoom"),
            ImportFlags.CameraZoom,
            () => ImGui.Text(_clipboardService.ClipboardPreset.CameraZoom.ToString())
        );

        DrawImportSetting(
            _textService.GetAddonText(14712).OrDefault("Rotation"),
            ImportFlags.ImageRotation,
            () => ImGui.Text(_clipboardService.ClipboardPreset.ImageRotation.ToString())
        );

        ImGuiUtils.DrawSection(_textService.GetAddonText(14685).OrDefault("Character"), true, GetSectionColor());

        var isBannerTimelineUnlocked = _bannerService.IsBannerTimelineUnlocked(_clipboardService.ClipboardPreset.BannerTimeline);
        DrawImportSetting(
            _textService.GetAddonText(14690).OrDefault("Pose"),
            ImportFlags.BannerTimeline,
            () =>
            {
                ImGui.Text(_bannerService.GetBannerTimelineName(_clipboardService.ClipboardPreset.BannerTimeline));

                if (!isBannerTimelineUnlocked)
                {
                    ImGui.TextColored(Color.ErrorForeground, _textService.Translate("EditorWindows.AdvancedImportOverlay.NotUnlocked"));
                }
            },
            isBannerTimelineUnlocked
        );

        DrawImportSetting(
            _textService.GetAddonText(14691).OrDefault("Expression"),
            ImportFlags.Expression,
            () =>
            {
                var id = _clipboardService.ClipboardPreset.Expression;
                var expressionName = unknown;

                if (id == 0)
                {
                    expressionName = _textService.GetAddonText(14727).OrDefault("None");
                }
                else
                {
                    for (var i = 0; i < state->Expressions.SortedEntriesCount; i++)
                    {
                        var entry = state->Expressions.SortedEntries[i];
                        if (entry->RowId == id
                        && entry->SupplementalRow != 0
                        && _excelService.TryGetRow<BannerFacial>(entry->RowId, out var bannerFacialRow)
                        && _excelService.TryGetRow<Emote>(bannerFacialRow.Emote.RowId, out var emoteRow))
                        {
                            expressionName = emoteRow.Name.ToString();
                            break;
                        }
                    }
                }

                ImGui.Text(expressionName);
            }
        );

        DrawImportSetting(
            _textService.Translate("EditorWindows.Setting.AnimationTimestamp.Label"),
            ImportFlags.AnimationProgress,
            () => ImGui.Text(_textService.Translate("EditorWindows.Setting.AnimationTimestamp.ValueFormat", _clipboardService.ClipboardPreset.AnimationProgress))
        );

        DrawImportSetting(
            _textService.GetAddonText(5972).OrDefault("Camera Position"),
            ImportFlags.CameraPosition,
            () => DrawHalfVector4(_clipboardService.ClipboardPreset.CameraPosition)
        );

        DrawImportSetting(
            _textService.Translate("EditorWindows.Setting.CameraTarget.Label"),
            ImportFlags.CameraTarget,
            () => DrawHalfVector4(_clipboardService.ClipboardPreset.CameraTarget)
        );

        DrawImportSetting(
            _textService.Translate("EditorWindows.Setting.HeadDirection.Label"),
            ImportFlags.HeadDirection,
            () => DrawHalfVector2(_clipboardService.ClipboardPreset.HeadDirection)
        );

        DrawImportSetting(
            _textService.Translate("EditorWindows.Setting.EyeDirection.Label"),
            ImportFlags.EyeDirection,
            () => DrawHalfVector2(_clipboardService.ClipboardPreset.EyeDirection)
        );

        ImGuiUtils.DrawSection(_textService.GetAddonText(14692).OrDefault("Ambient Lighting"), true, GetSectionColor());

        var labelBrightness = _textService.GetAddonText(14694).OrDefault("Brightness");
        var labelColor = _textService.GetAddonText(7008).OrDefault("Color");

        DrawImportSetting(
            labelBrightness,
            ImportFlags.AmbientLightingBrightness,
            () => ImGui.Text(_clipboardService.ClipboardPreset.AmbientLightingBrightness.ToString())
        );

        DrawImportSetting(
            labelColor,
            ImportFlags.AmbientLightingColor,
            () => DrawColor(
                _clipboardService.ClipboardPreset.AmbientLightingColorRed,
                _clipboardService.ClipboardPreset.AmbientLightingColorGreen,
                _clipboardService.ClipboardPreset.AmbientLightingColorBlue
            )
        );

        ImGuiUtils.DrawSection(_textService.GetAddonText(14693).OrDefault("Directional Lighting"), true, GetSectionColor());

        DrawImportSetting(
            labelBrightness,
            ImportFlags.DirectionalLightingBrightness,
            () => ImGui.Text(_clipboardService.ClipboardPreset.DirectionalLightingBrightness.ToString())
        );

        DrawImportSetting(
            labelColor,
            ImportFlags.DirectionalLightingColor,
            () => DrawColor(
                _clipboardService.ClipboardPreset.DirectionalLightingColorRed,
                _clipboardService.ClipboardPreset.DirectionalLightingColorGreen,
                _clipboardService.ClipboardPreset.DirectionalLightingColorBlue
            )
        );

        DrawImportSetting(
            _textService.GetAddonText(14696).OrDefault("Vertical Angle"),
            ImportFlags.DirectionalLightingVerticalAngle,
            () => ImGui.Text(_clipboardService.ClipboardPreset.DirectionalLightingVerticalAngle.ToString())
        );

        DrawImportSetting(
            _textService.GetAddonText(14695).OrDefault("Horizontal Angle"),
            ImportFlags.DirectionalLightingHorizontalAngle,
            () => ImGui.Text(_clipboardService.ClipboardPreset.DirectionalLightingHorizontalAngle.ToString())
        );

        if (IsWindow)
            ImCursor.Y += ImStyle.ItemSpacing.Y;
    }

    private void DrawImportSetting(string label, ImportFlags flag, Action drawFn, bool isUnlocked = true)
    {
        using var id = ImRaii.PushId(flag.ToString());

        ImGui.Columns(2, "##Columns", false);

        var isEnabled = isUnlocked && _clipboardService.CurrentImportFlags.HasFlag(flag);
        using var textColor = GetLabelColor(isEnabled).Push(ImGuiCol.Text);
        using var disabled = ImRaii.Disabled(!isUnlocked);

        if (ImGui.Checkbox(label + "##Checkbox", ref isEnabled))
        {
            if (isEnabled)
                _clipboardService.CurrentImportFlags |= flag;
            else
                _clipboardService.CurrentImportFlags &= ~flag;
        }

        disabled?.Dispose();
        textColor?.Dispose();

        using (ImRaii.Disabled(!isEnabled))
        {
            ImGui.NextColumn();
            drawFn();
        }

        ImGui.Columns(1);
    }

    private void DrawColor(byte r, byte g, byte b)
    {
        var vec = new System.Numerics.Vector3(r / 255f, g / 255f, b / 255f);
        using var table = ImRaii.Table("##Table", 4);
        if (!table)
            return;

        var scale = ImStyle.Scale;
        ImGui.TableSetupColumn("Preview", ImGuiTableColumnFlags.WidthFixed, 26 * scale);
        ImGui.TableSetupColumn("R", ImGuiTableColumnFlags.WidthFixed, 40 * scale);
        ImGui.TableSetupColumn("G", ImGuiTableColumnFlags.WidthFixed, 40 * scale);
        ImGui.TableSetupColumn("B", ImGuiTableColumnFlags.WidthFixed, 40 * scale);

        ImGui.TableNextRow();
        ImGui.TableNextColumn();
        ImGui.ColorEdit3("##ColorEdit3", ref vec, ImGuiColorEditFlags.NoPicker | ImGuiColorEditFlags.NoOptions | ImGuiColorEditFlags.NoLabel | ImGuiColorEditFlags.NoInputs);

        void drawColumn(string label, Half value)
        {
            ImGui.TableNextColumn();
            label = _textService.Translate("EditorWindows.AdvancedImportOverlay.ColorInput." + label);
            var labelWidth = ImGui.CalcTextSize(label).X;
            ImGui.Text(label);

            var valueStr = _textService.Translate("EditorWindows.AdvancedImportOverlay.ColorInput.ValueFormat", value);
            ImGui.SameLine(0, ImStyle.ContentRegionAvail.X - labelWidth - ImGui.CalcTextSize(valueStr).X);
            ImGui.Text(valueStr);
        }

        drawColumn("R", r);
        drawColumn("G", g);
        drawColumn("B", b);
    }

    private void DrawHalfVector2(HalfVec2 vec)
    {
        using var table = ImRaii.Table("##Table", 2);
        if (!table)
            return;

        var scale = ImStyle.Scale;
        ImGui.TableSetupColumn("X", ImGuiTableColumnFlags.WidthFixed, 50 * scale);
        ImGui.TableSetupColumn("Y", ImGuiTableColumnFlags.WidthFixed, 50 * scale);

        ImGui.TableNextRow();

        void drawColumn(string label, Half value)
        {
            ImGui.TableNextColumn();
            label = _textService.Translate("EditorWindows.AdvancedImportOverlay.VectorInput." + label);
            var labelWidth = ImGui.CalcTextSize(label).X;
            ImGui.Text(label);

            var valueStr = _textService.Translate("EditorWindows.AdvancedImportOverlay.VectorInput.ValueFormat", value);
            ImGui.SameLine(0, ImStyle.ContentRegionAvail.X - labelWidth - ImGui.CalcTextSize(valueStr).X);
            ImGui.Text(valueStr);
        }

        drawColumn("X", vec.X);
        drawColumn("Y", vec.Y);
    }

    private void DrawHalfVector4(HalfVec4 vec)
    {
        using var table = ImRaii.Table("##Table", 4);
        if (!table)
            return;

        var scale = ImStyle.Scale;
        ImGui.TableSetupColumn("X", ImGuiTableColumnFlags.WidthFixed, 50 * scale);
        ImGui.TableSetupColumn("Y", ImGuiTableColumnFlags.WidthFixed, 50 * scale);
        ImGui.TableSetupColumn("Z", ImGuiTableColumnFlags.WidthFixed, 50 * scale);
        ImGui.TableSetupColumn("W", ImGuiTableColumnFlags.WidthFixed, 50 * scale);

        ImGui.TableNextRow();

        void drawColumn(string label, Half value)
        {
            ImGui.TableNextColumn();
            label = _textService.Translate("EditorWindows.AdvancedImportOverlay.VectorInput." + label);
            var labelWidth = ImGui.CalcTextSize(label).X;
            ImGui.Text(label);

            var valueStr = _textService.Translate("EditorWindows.AdvancedImportOverlay.VectorInput.ValueFormat", value);
            ImGui.SameLine(0, ImStyle.ContentRegionAvail.X - labelWidth - ImGui.CalcTextSize(valueStr).X);
            ImGui.Text(valueStr);
        }

        drawColumn("X", vec.X);
        drawColumn("Y", vec.Y);
        drawColumn("Z", vec.Z);
        drawColumn("W", vec.W);
    }
}
