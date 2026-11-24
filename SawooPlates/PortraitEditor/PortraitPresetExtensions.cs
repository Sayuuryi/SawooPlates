using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using FFXIVClientStructs.FFXIV.Common.Math;

namespace SawooPlates.PortraitEditor;

public static unsafe class PortraitPresetExtensions
{
    public static HalfVector2 ToGame(this HalfVec2 v) => new(v.X, v.Y);
    public static HalfVector4 ToGame(this HalfVec4 v) => new(v.X, v.Y, v.Z, v.W);
    public static HalfVec2 ToCore(this HalfVector2 v) => new(v.X, v.Y);
    public static HalfVec4 ToCore(this HalfVector4 v) => new(v.X, v.Y, v.Z, v.W);

    public static bool ReadExportedPortraitData(this PortraitPresetData preset, ExportedPortraitData* portraitData)
    {
        if (portraitData == null)
            return false;

        preset.CameraPosition = portraitData->CameraPosition.ToCore();
        preset.CameraTarget = portraitData->CameraTarget.ToCore();
        preset.ImageRotation = portraitData->ImageRotation;
        preset.CameraZoom = portraitData->CameraZoom;
        preset.BannerTimeline = portraitData->BannerTimeline;
        preset.AnimationProgress = portraitData->AnimationProgress;
        preset.Expression = portraitData->Expression;
        preset.HeadDirection = portraitData->HeadDirection.ToCore();
        preset.EyeDirection = portraitData->EyeDirection.ToCore();
        preset.DirectionalLightingColorRed = portraitData->DirectionalLightingColorRed;
        preset.DirectionalLightingColorGreen = portraitData->DirectionalLightingColorGreen;
        preset.DirectionalLightingColorBlue = portraitData->DirectionalLightingColorBlue;
        preset.DirectionalLightingBrightness = portraitData->DirectionalLightingBrightness;
        preset.DirectionalLightingVerticalAngle = portraitData->DirectionalLightingVerticalAngle;
        preset.DirectionalLightingHorizontalAngle = portraitData->DirectionalLightingHorizontalAngle;
        preset.AmbientLightingColorRed = portraitData->AmbientLightingColorRed;
        preset.AmbientLightingColorGreen = portraitData->AmbientLightingColorGreen;
        preset.AmbientLightingColorBlue = portraitData->AmbientLightingColorBlue;
        preset.AmbientLightingBrightness = portraitData->AmbientLightingBrightness;
        preset.BannerBg = portraitData->BannerBg;

        return true;
    }

    public static bool WriteExportedPortraitData(this PortraitPresetData preset, ExportedPortraitData* portraitData)
    {
        if (portraitData == null)
            return false;

        portraitData->CameraPosition = preset.CameraPosition.ToGame();
        portraitData->CameraTarget = preset.CameraTarget.ToGame();
        portraitData->ImageRotation = preset.ImageRotation;
        portraitData->CameraZoom = preset.CameraZoom;
        portraitData->BannerTimeline = preset.BannerTimeline;
        portraitData->AnimationProgress = preset.AnimationProgress;
        portraitData->Expression = preset.Expression;
        portraitData->HeadDirection = preset.HeadDirection.ToGame();
        portraitData->EyeDirection = preset.EyeDirection.ToGame();
        portraitData->DirectionalLightingColorRed = preset.DirectionalLightingColorRed;
        portraitData->DirectionalLightingColorGreen = preset.DirectionalLightingColorGreen;
        portraitData->DirectionalLightingColorBlue = preset.DirectionalLightingColorBlue;
        portraitData->DirectionalLightingBrightness = preset.DirectionalLightingBrightness;
        portraitData->DirectionalLightingVerticalAngle = preset.DirectionalLightingVerticalAngle;
        portraitData->DirectionalLightingHorizontalAngle = preset.DirectionalLightingHorizontalAngle;
        portraitData->AmbientLightingColorRed = preset.AmbientLightingColorRed;
        portraitData->AmbientLightingColorGreen = preset.AmbientLightingColorGreen;
        portraitData->AmbientLightingColorBlue = preset.AmbientLightingColorBlue;
        portraitData->AmbientLightingBrightness = preset.AmbientLightingBrightness;
        portraitData->BannerBg = preset.BannerBg;

        return true;
    }

    /// <summary>Reads the current portrait from the open Portrait Editor.</summary>
    public static PortraitPresetData FromState()
    {
        var state = AgentBannerEditor.Instance()->EditorState;
        var preset = new PortraitPresetData();

        var portraitData = stackalloc ExportedPortraitData[1];
        state->CharaView->ExportPortraitData(portraitData);
        preset.ReadExportedPortraitData(portraitData);

        preset.BannerFrame = state->BannerEntry.BannerFrame;
        preset.BannerDecoration = state->BannerEntry.BannerDecoration;

        return preset;
    }
}
