using System.Buffers.Binary;
using Dalamud.Interface.Colors;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace SawooPlates.Common;

public enum ColorFormat
{
    /// <summary>
    /// RGBA | 0xAABBGGRR<br/>
    /// Used by ImGui.
    /// </summary>
    RGBA,

    /// <summary>
    /// BGRA | 0xAARRGGBB<br/>
    /// Used by SeStrings.
    /// </summary>
    BGRA,

    /// <summary>
    /// ARGB | 0xBBGGRRAA
    /// </summary>
    ARGB,

    /// <summary>
    /// ABGR | 0xRRGGBBAA
    /// </summary>
    ABGR,
}

public struct Color(float r, float g, float b, float a = 1)
{
    public float R = r;
    public float G = g;
    public float B = b;
    public float A = a;

    public static Color Lerp(Color colorA, Color colorB, float fraction)
    {
        return new Color(
            float.Lerp(colorA.R, colorB.R, fraction),
            float.Lerp(colorA.G, colorB.G, fraction),
            float.Lerp(colorA.B, colorB.B, fraction),
            float.Lerp(colorA.A, colorB.A, fraction));
    }

    public static Color FromVector4(Vector4 vec, ColorFormat format = ColorFormat.RGBA)
        => format switch
        {
            ColorFormat.RGBA => new() { R = vec.X, G = vec.Y, B = vec.Z, A = vec.W },
            ColorFormat.BGRA => new() { B = vec.X, G = vec.Y, R = vec.Z, A = vec.W },
            ColorFormat.ARGB => new() { A = vec.X, R = vec.Y, G = vec.Z, B = vec.W },
            ColorFormat.ABGR => new() { A = vec.X, B = vec.Y, G = vec.Z, R = vec.W },
            _ => throw new ArgumentOutOfRangeException(nameof(format))
        };

    public static Color From(ImGuiCol col)
        => FromRGBA(ImGui.GetColorU32(col));

    public static unsafe Color FromUIColor(uint id, bool useThemeColor = true, Color? fallback = null)
    {
        var atkStage = AtkStage.Instance();
        if (atkStage == null || atkStage->AtkUIColorHolder == null)
            return fallback ?? Black;

        var color = atkStage->AtkUIColorHolder->GetColor(useThemeColor, id);
        if (color == 0)
            return fallback ?? Black;

        return FromRGBA(color);
    }

    public static Color FromRGBA(uint rgba)
        => FromVector4(ImGui.ColorConvertU32ToFloat4(rgba));

    public static Color FromBGRA(uint bgra)
        => FromRGBA((bgra & 0xFF00FF00) | ((bgra & 0x000000FF) << 16) | ((bgra & 0x00FF0000) >> 16)); // swap red and blue channel

    public static Color FromABGR(uint abgr)
        => FromRGBA(BinaryPrimitives.ReverseEndianness(abgr));

    public static Color FromARGB(uint argb)
        => FromRGBA(BinaryPrimitives.ReverseEndianness((argb & 0xFF00FF00) | ((argb & 0x000000FF) << 16) | ((argb & 0x00FF0000) >> 16))); // swap red and blue channel, then endianness

    public ImRaii.ColorDisposable Push(ImGuiCol idx, bool condition = true)
        => ImRaii.PushColor(idx, ToUInt(), condition);

    public uint ToUInt(ColorFormat format = ColorFormat.RGBA)
    {
        var vec = ToVector(format);

        static uint Pack(float value, byte shift)
            => (uint)(Math.Clamp(value, 0f, 1f) * 255f + 0.5f) << shift;

        return Pack(vec.X, 0) | Pack(vec.Y, 8) | Pack(vec.Z, 16) | Pack(vec.W, 24);
    }

    public Vector4 ToVector(ColorFormat format = ColorFormat.RGBA)
        => format switch
        {
            ColorFormat.RGBA => new Vector4(R, G, B, A),
            ColorFormat.BGRA => new Vector4(B, G, R, A),
            ColorFormat.ARGB => new Vector4(A, R, G, B),
            ColorFormat.ABGR => new Vector4(A, B, G, R),
            _ => throw new ArgumentOutOfRangeException(nameof(format))
        };

    public static implicit operator Vector4(Color col)
        => new(col.R, col.G, col.B, col.A);

    #region HSV

    public static Color FromHSV(float h, float s, float v, float a = 1.0f)
    {
        var color = new Color() { A = a };
        ImGui.ColorConvertHSVtoRGB(h, s, v, ref color.R, ref color.G, ref color.B);
        return color;
    }

    public readonly (float H, float S, float V) ToHSV()
    {
        var h = 0f; // hue
        var s = 0f; // saturation
        var v = 0f; // value

        ImGui.ColorConvertRGBtoHSV(R, G, B, ref h, ref s, ref v);

        return (h, s, v);
    }

    public float Brightness // aka. Value
    {
        readonly get => ToHSV().V;
        set
        {
            var (h, s, _) = ToHSV();
            ImGui.ColorConvertHSVtoRGB(h, s, value, ref R, ref G, ref B);
        }
    }

    #endregion

    #region Presets

    public static Color White { get; } = new(1, 1, 1);
    public static Color Black { get; } = new(0, 0, 0);

    public static Color Gold { get; } = new(0.847f, 0.733f, 0.49f);

    public static Color Text => From(ImGuiCol.Text);
    public static Color Text700 => From(ImGuiCol.Text) with { A = 0.7f };

    #endregion

    #region Dalamud Colors

    /// <inheritdoc cref="ImGuiColors.InfoForeground"/>
    public static Color InfoForeground => FromVector4(ImGuiColors.InfoForeground);

    /// <inheritdoc cref="ImGuiColors.InfoBackground"/>
    public static Color InfoBackground => FromVector4(ImGuiColors.InfoBackground);

    /// <inheritdoc cref="ImGuiColors.SuccessForeground"/>
    public static Color SuccessForeground => FromVector4(ImGuiColors.SuccessForeground);

    /// <inheritdoc cref="ImGuiColors.SuccessBackground"/>
    public static Color SuccessBackground => FromVector4(ImGuiColors.SuccessBackground);

    /// <inheritdoc cref="ImGuiColors.WarningForeground"/>
    public static Color WarningForeground => FromVector4(ImGuiColors.WarningForeground);

    /// <inheritdoc cref="ImGuiColors.WarningBackground"/>
    public static Color WarningBackground => FromVector4(ImGuiColors.WarningBackground);

    /// <inheritdoc cref="ImGuiColors.ErrorForeground"/>
    public static Color ErrorForeground => FromVector4(ImGuiColors.ErrorForeground);

    /// <inheritdoc cref="ImGuiColors.ErrorBackground"/>
    public static Color ErrorBackground => FromVector4(ImGuiColors.ErrorBackground);

    /// <inheritdoc cref="ImGuiColors.AttentionForeground"/>
    public static Color AttentionForeground => FromVector4(ImGuiColors.AttentionForeground);

    /// <inheritdoc cref="ImGuiColors.AttentionBackground"/>
    public static Color AttentionBackground => FromVector4(ImGuiColors.AttentionBackground);

    #endregion
}
