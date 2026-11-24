using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Dalamud.Hooking;
using FFXIVClientStructs.FFXIV.Client.UI;
using SawooPlates.PortraitEditor.Enums;
using SawooPlates.PortraitEditor.Utils;
using TerraFX.Interop.Windows;
using static TerraFX.Interop.Windows.Windows;

namespace SawooPlates.PortraitEditor.Services;

public sealed class ClipboardService : IDisposable
{
    private readonly IPluginLog _logger;
    private readonly IFramework _framework;

    private Hook<UIClipboard.Delegates.OnClipboardDataChanged>? _onClipboardDataChangedHook;

    public ImportFlags CurrentImportFlags { get; set; } = ImportFlags.All;
    public PortraitPresetData? ClipboardPreset { get; set; }

    /// <summary>Raised on the framework thread when the system clipboard holds a new preset code.</summary>
    public event Action<PortraitPresetData>? PresetCopied;

    public unsafe ClipboardService(IPluginLog logger, IFramework framework, IGameInteropProvider gameInteropProvider)
    {
        _logger = logger;
        _framework = framework;

        _framework.RunOnFrameworkThread(() =>
        {
            _onClipboardDataChangedHook = gameInteropProvider.HookFromAddress<UIClipboard.Delegates.OnClipboardDataChanged>(
                UIClipboard.MemberFunctionPointers.OnClipboardDataChanged,
                OnClipboardDataChangedDetour);
            _onClipboardDataChangedHook.Enable();
        });
    }

    public void Dispose()
    {
        _onClipboardDataChangedHook?.Dispose();
        _onClipboardDataChangedHook = null;
    }

    private unsafe void OnClipboardDataChangedDetour(UIClipboard* uiClipboard)
    {
        _onClipboardDataChangedHook!.Original(uiClipboard);

        try
        {
            if (PortraitPresetData.TryParse(uiClipboard->Data.SystemClipboardText.ToString(), out var preset))
            {
                ClipboardPreset = preset;
                _logger.Debug("Parsed ClipboardPreset: {ClipboardPreset}", preset!);
                PresetCopied?.Invoke(preset!);
            }
            else
            {
                ClipboardPreset = null;
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error reading preset");
        }
    }

    public async Task SetClipboardPortraitPreset(PortraitPresetData? preset)
    {
        if (preset == null)
        {
            ClipboardPreset = null;
            return;
        }

        if (!await OpenClipboard().ConfigureAwait(false))
            return;

        try
        {
            EmptyClipboard();

            var presetString = preset.ToExportedString();
            var length = Encoding.UTF8.GetByteCount(presetString);

            using var writer = new ClipboardWriter(ClipboardFormat.CF_TEXT);

            if (!writer.TryAllocMemorySpan(length, out var span))
                return;

            Encoding.UTF8.GetBytes(presetString, span);

            if (writer.End())
                ClipboardPreset = preset;
        }
        catch (Exception e)
        {
            _logger.Error(e, "Error during PortraitPreset.ToClipboard");
        }
        finally
        {
            CloseClipboard();
        }
    }

    public async Task SetClipboardImage(BgraImage image)
    {
        if (!await OpenClipboard().ConfigureAwait(false))
            return;

        try
        {
            if (!EmptyClipboard())
                return;

            SetDIB(image);
            SetDIBV5(image);
            SetPNG(image);
        }
        finally
        {
            CloseClipboard();
        }
    }

    private async Task<bool> OpenClipboard()
    {
        var start = DateTime.Now;
        HWND hwnd;

        while ((hwnd = GetWindowHandle()) != HWND.NULL)
        {
            if (TerraFX.Interop.Windows.Windows.OpenClipboard(hwnd))
                return true;

            if (DateTime.Now - start > TimeSpan.FromSeconds(2))
            {
                _logger.Error("Clipboard timeout: Could not open clipboard after 2 seconds.");
                return false;
            }

            await Task.Delay(100).ConfigureAwait(false);
        }

        return false;
    }

    private static unsafe HWND GetWindowHandle()
    {
        var framework = FFXIVClientStructs.FFXIV.Client.System.Framework.Framework.Instance();
        if (framework == null)
            return HWND.NULL;

        var uiClipboard = framework->GetUIClipboard();
        if (uiClipboard == null)
            return HWND.NULL;

        return (HWND)uiClipboard->ThisHwnd;
    }

    private static unsafe void SetDIB(BgraImage image)
    {
        using var writer = new ClipboardWriter(ClipboardFormat.CF_DIB);

        var width = (int)image.Width;
        var height = (int)image.Height;

        var length = sizeof(BITMAPINFOHEADER) + width * 4 * height;
        if (!writer.TryAllocMemory(length, out var memory))
            return;

        var header = (BITMAPINFOHEADER*)memory;
        header->biSize = (uint)sizeof(BITMAPINFOHEADER);
        header->biWidth = width;
        header->biHeight = -height;
        header->biPlanes = 1;
        header->biBitCount = 32; // bits per pixel
        header->biCompression = 0; // BI_RGB
        header->biSizeImage = 0;
        header->biXPelsPerMeter = 0;
        header->biYPelsPerMeter = 0;
        header->biClrUsed = 0;
        header->biClrImportant = 0;

        var pixelSpan = new Span<byte>((byte*)memory + header->biSize, width * 4 * height);
        image.CopyPixelDataTo(pixelSpan);
    }

    private static unsafe void SetDIBV5(BgraImage image)
    {
        using var writer = new ClipboardWriter(ClipboardFormat.CF_DIBV5);

        var width = (int)image.Width;
        var height = (int)image.Height;

        var length = sizeof(BITMAPV5HEADER) + width * 4 * height;
        if (!writer.TryAllocMemory(length, out var memory))
            return;

        var bitmapInfo = (BITMAPV5HEADER*)memory;
        bitmapInfo->bV5Size = (uint)sizeof(BITMAPV5HEADER);
        bitmapInfo->bV5Width = width;
        bitmapInfo->bV5Height = -height; // negative height for top-down image
        bitmapInfo->bV5Planes = 1;
        bitmapInfo->bV5BitCount = 32; // 4 bytes per pixel
        bitmapInfo->bV5Compression = 0; // BI_RGB
        bitmapInfo->bV5SizeImage = (uint)(width * 4 * height);
        bitmapInfo->bV5XPelsPerMeter = 0;
        bitmapInfo->bV5YPelsPerMeter = 0;
        bitmapInfo->bV5ClrUsed = 0;
        bitmapInfo->bV5ClrImportant = 0;
        bitmapInfo->bV5RedMask = 0x00FF0000;
        bitmapInfo->bV5GreenMask = 0x0000FF00;
        bitmapInfo->bV5BlueMask = 0x000000FF;
        bitmapInfo->bV5AlphaMask = 0xFF000000;
        bitmapInfo->bV5CSType = 0x73524742; // 'sRGB'
        bitmapInfo->bV5Endpoints = new CIEXYZTRIPLE();
        bitmapInfo->bV5GammaRed = 0;
        bitmapInfo->bV5GammaGreen = 0;
        bitmapInfo->bV5GammaBlue = 0;
        bitmapInfo->bV5Intent = 4; // LCS_COLORIMETRIC
        bitmapInfo->bV5ProfileData = 0;
        bitmapInfo->bV5ProfileSize = 0;
        bitmapInfo->bV5Reserved = 0;

        var pixelSpan = new Span<byte>((byte*)memory + bitmapInfo->bV5Size, width * 4 * height);
        image.CopyPixelDataTo(pixelSpan);
    }

    private static unsafe void SetPNG(BgraImage image)
    {
        uint format;
        fixed (char* name = "PNG")
            format = RegisterClipboardFormatW(name);

        if (format == 0)
            return;

        using var ms = new MemoryStream();

        image.SaveAsPng(ms);

        using var writer = new ClipboardWriter(format);

        if (!writer.TryAllocMemorySpan((int)ms.Length, out var span))
            return;

        if (!ms.TryGetBuffer(out var buffer))
            return;

        buffer.AsSpan(0, (int)ms.Length).CopyTo(span);
    }
}
