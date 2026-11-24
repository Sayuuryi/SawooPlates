using System.Text.Json.Serialization;

namespace SawooPlates.Core.Presets;

[JsonConverter(typeof(PortraitPresetDataConverter))]
public sealed record PortraitPresetData
{
    public const int Magic = 0x53505448;
    public const ushort CurrentVersion = 1;

    public ushort Version = CurrentVersion;

    public HalfVec4 CameraPosition;
    public HalfVec4 CameraTarget;
    public short ImageRotation;
    public byte CameraZoom;
    public ushort BannerTimeline;
    public float AnimationProgress;
    public byte Expression;
    public HalfVec2 HeadDirection;
    public HalfVec2 EyeDirection;
    public byte DirectionalLightingColorRed;
    public byte DirectionalLightingColorGreen;
    public byte DirectionalLightingColorBlue;
    public byte DirectionalLightingBrightness;
    public short DirectionalLightingVerticalAngle;
    public short DirectionalLightingHorizontalAngle;
    public byte AmbientLightingColorRed;
    public byte AmbientLightingColorGreen;
    public byte AmbientLightingColorBlue;
    public byte AmbientLightingBrightness;
    public ushort BannerBg;

    public ushort BannerFrame;
    public ushort BannerDecoration;

    public byte[] ToBytes()
    {
        using var outputStream = new MemoryStream();
        using var writer = new BinaryWriter(outputStream);

        writer.Write(Magic);
        writer.Write(Version);
        Write(writer, CameraPosition);
        Write(writer, CameraTarget);
        writer.Write(ImageRotation);
        writer.Write(CameraZoom);
        writer.Write(BannerTimeline);
        writer.Write(AnimationProgress);
        writer.Write(Expression);
        Write(writer, HeadDirection);
        Write(writer, EyeDirection);
        writer.Write(DirectionalLightingColorRed);
        writer.Write(DirectionalLightingColorGreen);
        writer.Write(DirectionalLightingColorBlue);
        writer.Write(DirectionalLightingBrightness);
        writer.Write(DirectionalLightingVerticalAngle);
        writer.Write(DirectionalLightingHorizontalAngle);
        writer.Write(AmbientLightingColorRed);
        writer.Write(AmbientLightingColorGreen);
        writer.Write(AmbientLightingColorBlue);
        writer.Write(AmbientLightingBrightness);
        writer.Write(BannerBg);
        writer.Write(BannerFrame);
        writer.Write(BannerDecoration);
        writer.Flush();

        return outputStream.ToArray();
    }

    public string ToExportedString() => Convert.ToBase64String(ToBytes());

    /// <summary>
    /// Returns null for empty, non-Base64, too short or wrong-magic input,
    /// throws for an unknown version.
    /// </summary>
    public static PortraitPresetData? FromExportedString(string? input)
    {
        if (string.IsNullOrEmpty(input))
            return null;

        byte[] rawInput;
        try
        {
            rawInput = Convert.FromBase64String(input);
        }
        catch
        {
            return null;
        }

        if (rawInput.Length < 8)
            return null;

        using var inputStream = new MemoryStream(rawInput);
        using var reader = new BinaryReader(inputStream);

        var magic = reader.ReadInt32();
        if (magic != Magic)
            return null;

        var preset = new PortraitPresetData
        {
            Version = reader.ReadUInt16()
        };

        switch (preset.Version)
        {
            case 1:
                preset.CameraPosition = ReadHalfVec4(reader);
                preset.CameraTarget = ReadHalfVec4(reader);
                preset.ImageRotation = reader.ReadInt16();
                preset.CameraZoom = reader.ReadByte();
                preset.BannerTimeline = reader.ReadUInt16();
                preset.AnimationProgress = reader.ReadSingle();
                preset.Expression = reader.ReadByte();
                preset.HeadDirection = ReadHalfVec2(reader);
                preset.EyeDirection = ReadHalfVec2(reader);
                preset.DirectionalLightingColorRed = reader.ReadByte();
                preset.DirectionalLightingColorGreen = reader.ReadByte();
                preset.DirectionalLightingColorBlue = reader.ReadByte();
                preset.DirectionalLightingBrightness = reader.ReadByte();
                preset.DirectionalLightingVerticalAngle = reader.ReadInt16();
                preset.DirectionalLightingHorizontalAngle = reader.ReadInt16();
                preset.AmbientLightingColorRed = reader.ReadByte();
                preset.AmbientLightingColorGreen = reader.ReadByte();
                preset.AmbientLightingColorBlue = reader.ReadByte();
                preset.AmbientLightingBrightness = reader.ReadByte();
                preset.BannerBg = reader.ReadUInt16();
                preset.BannerFrame = reader.ReadUInt16();
                preset.BannerDecoration = reader.ReadUInt16();
                break;

            default:
                throw new Exception($"Unknown Preset version {preset.Version}");
        }

        return preset;
    }

    /// <summary>Non-throwing variant of <see cref="FromExportedString"/>.</summary>
    public static bool TryParse(string? input, out PortraitPresetData? preset)
    {
        try
        {
            preset = FromExportedString(input?.Trim());
        }
        catch
        {
            preset = null;
        }

        return preset != null;
    }

    private static void Write(BinaryWriter writer, HalfVec2 vec)
    {
        writer.Write(vec.X);
        writer.Write(vec.Y);
    }

    private static void Write(BinaryWriter writer, HalfVec4 vec)
    {
        writer.Write(vec.X);
        writer.Write(vec.Y);
        writer.Write(vec.Z);
        writer.Write(vec.W);
    }

    private static HalfVec2 ReadHalfVec2(BinaryReader reader)
        => new(reader.ReadHalf(), reader.ReadHalf());

    private static HalfVec4 ReadHalfVec4(BinaryReader reader)
        => new(reader.ReadHalf(), reader.ReadHalf(), reader.ReadHalf(), reader.ReadHalf());
}
