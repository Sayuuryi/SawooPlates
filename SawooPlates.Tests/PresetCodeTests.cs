using System.Text.Json;
using SawooPlates.Core.Presets;

namespace SawooPlates.Tests;

public class PresetCodeTests
{
    private const string FirstCode = "SFRQUwEA2DMiNJM7ADyeKD4o8igAPPr/yAEAbxKDOg0AAAAAcrGCLf///xqFALP/MzMzf3UBSgBJAQ==";

    // Real preset codes.
    public static TheoryData<string> RealCodes =>
    [
        "SFRQUwEA2DMiNJM7ADyeKD4o8igAPPr/yAEAbxKDOg0AAAAAcrGCLf///xqFALP/MzMzf3UBSgBJAQ==",
        "SFRQUwEADjufrNs1ADxfrCsqba0APPr/yAMAwcrEQQGwsh8zk7UQKv///xqFALP/FBUPOHUBIQFHAQ==",
        "SFRQUwEAGTkdKC0/ADyUrKSut6MAPAAAvVwA7NH8QgEAAAAAeLGKLf///xqFALP/FBUPOAEA5wABAA==",
        "SFRQUwEAeC1FNAs7ADy5smWrLagAPPr/yFwASsyQQgEAAAAAebGJLf///xqFALP/FBUPOAEA5wABAA==",
        "SFRQUwEACzwfOuI5ADx4qvO0WiUAPPv/oGEAx8ucQhkAuwE0lbWULf///3+FALP/MzMzf+YAAgADAA==",
        "SFRQUwEAdzKtM7U7ADxAovEk5ioAPPr/yAEAbxKDOg0AAAAAf7GNLf///xqFALP/MzMzf6MASwBQAA==",
        "SFRQUwEAqDjcOV82ADwcLT2p5C8APO//olcA4rEzQwNWqGk8MCgxpoye0GU+AJX/g2NsWn4ACgADAA==",
        "SFRQUwEA6iI8Od0mADziJPa1ACEAPPv/oGEAff+cQhkdMao7Q7EfKf///3+FALP/MzMzf34ACgADAA==",
    ];

    [Theory]
    [MemberData(nameof(RealCodes))]
    public void RoundTripsRealCodes(string code)
    {
        var preset = PortraitPresetData.FromExportedString(code);

        Assert.NotNull(preset);
        Assert.Equal(1, preset.Version);
        Assert.Equal(code, preset.ToExportedString());
    }

    [Fact]
    public void PayloadIs58Bytes()
    {
        var bytes = new PortraitPresetData().ToBytes();

        Assert.Equal(58, bytes.Length);
        Assert.Equal(BitConverter.GetBytes(PortraitPresetData.Magic), bytes[..4]);
        Assert.Equal(new byte[] { 1, 0 }, bytes[4..6]);
    }

    [Fact]
    public void DecodesKnownValues()
    {
        // first code above
        var preset = PortraitPresetData.FromExportedString(FirstCode)!;

        // last 6 bytes: BannerBg, BannerFrame, BannerDecoration
        Assert.Equal((ushort)0x0175, preset.BannerBg);
        Assert.Equal((ushort)0x004A, preset.BannerFrame);
        Assert.Equal((ushort)0x0149, preset.BannerDecoration);
        Assert.Equal((Half)1, preset.CameraPosition.W);
    }

    [Fact]
    public void WritesFieldsInOrder()
    {
        var preset = new PortraitPresetData
        {
            CameraPosition = new((Half)1, (Half)2, (Half)3, (Half)4),
            CameraTarget = new((Half)5, (Half)6, (Half)7, (Half)8),
            ImageRotation = -45,
            CameraZoom = 200,
            BannerTimeline = 0x1234,
            AnimationProgress = 0.5f,
            Expression = 9,
            HeadDirection = new((Half)0.25, (Half)(-0.25)),
            EyeDirection = new((Half)0.125, (Half)(-0.125)),
            DirectionalLightingColorRed = 10,
            DirectionalLightingColorGreen = 11,
            DirectionalLightingColorBlue = 12,
            DirectionalLightingBrightness = 13,
            DirectionalLightingVerticalAngle = -180,
            DirectionalLightingHorizontalAngle = 180,
            AmbientLightingColorRed = 14,
            AmbientLightingColorGreen = 15,
            AmbientLightingColorBlue = 16,
            AmbientLightingBrightness = 17,
            BannerBg = 0x0102,
            BannerFrame = 0x0304,
            BannerDecoration = 0x0506,
        };

        using var ms = new MemoryStream();
        using (var w = new BinaryWriter(ms))
        {
            w.Write(PortraitPresetData.Magic);
            w.Write((ushort)1);
            foreach (var h in new[] { 1, 2, 3, 4, 5, 6, 7, 8 }) w.Write((Half)h);
            w.Write((short)-45);
            w.Write((byte)200);
            w.Write((ushort)0x1234);
            w.Write(0.5f);
            w.Write((byte)9);
            w.Write((Half)0.25); w.Write((Half)(-0.25));
            w.Write((Half)0.125); w.Write((Half)(-0.125));
            w.Write((byte)10); w.Write((byte)11); w.Write((byte)12); w.Write((byte)13);
            w.Write((short)-180); w.Write((short)180);
            w.Write((byte)14); w.Write((byte)15); w.Write((byte)16); w.Write((byte)17);
            w.Write((ushort)0x0102); w.Write((ushort)0x0304); w.Write((ushort)0x0506);
        }

        Assert.Equal(ms.ToArray(), preset.ToBytes());
        Assert.Equal(preset, PortraitPresetData.FromExportedString(preset.ToExportedString()));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not base64!")]
    [InlineData("SFRQUw==")] // too short
    [InlineData("QUJDRAEAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA")] // wrong magic
    public void ReturnsNullForInvalidInput(string? input)
    {
        Assert.Null(PortraitPresetData.FromExportedString(input));
        Assert.False(PortraitPresetData.TryParse(input, out _));
    }

    [Fact]
    public void UnknownVersionThrowsButTryParseDoesNot()
    {
        var bytes = new PortraitPresetData().ToBytes();
        bytes[4] = 2;
        var code = Convert.ToBase64String(bytes);

        Assert.ThrowsAny<Exception>(() => PortraitPresetData.FromExportedString(code));
        Assert.False(PortraitPresetData.TryParse(code, out _));
    }

    [Fact]
    public void TryParseTrimsWhitespace()
    {
        var code = FirstCode;
        Assert.True(PortraitPresetData.TryParse($"  {code}\r\n", out var preset));
        Assert.Equal(code, preset!.ToExportedString());
    }

    [Fact]
    public void JsonStoresPresetAsCode()
    {
        var code = FirstCode;
        var preset = PortraitPresetData.FromExportedString(code)!;

        var json = JsonSerializer.Serialize(preset);
        Assert.Equal($"\"{code}\"", json);
        Assert.Equal(preset, JsonSerializer.Deserialize<PortraitPresetData>(json));
    }
}
