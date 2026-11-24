using TerraFX.Interop.Windows;
using static TerraFX.Interop.Windows.Windows;

namespace SawooPlates.PortraitEditor.Utils;

public static class ClipboardFormat
{
    public const uint CF_TEXT = 1;
    public const uint CF_DIB = 8;
    public const uint CF_DIBV5 = 17;
}

public unsafe struct ClipboardWriter : IDisposable
{
    private const uint GMEM_MOVEABLE = 0x0002;
    private const uint GMEM_ZEROINIT = 0x0040;

    private HGLOBAL _hMem = HGLOBAL.NULL;
    private readonly uint _format;

    public ClipboardWriter()
    {
        throw new InvalidOperationException("Format required.");
    }

    public ClipboardWriter(uint format)
    {
        ArgumentOutOfRangeException.ThrowIfZero(format);
        _format = format;
    }

    public bool TryAllocMemory(int length, out void* address)
    {
        var hMem = GlobalAlloc(GMEM_MOVEABLE | GMEM_ZEROINIT, (nuint)length + 1); // + 1 for null terminator
        if (hMem == HGLOBAL.NULL)
        {
            address = null;
            return false;
        }

        var lockedMem = GlobalLock(hMem);
        if (lockedMem == null)
        {
            GlobalFree(hMem);
            address = null;
            return false;
        }

        address = lockedMem;
        _hMem = hMem;
        return true;
    }

    public bool TryAllocMemorySpan(int length, out Span<byte> span)
    {
        if (TryAllocMemory(length, out var address))
        {
            span = new Span<byte>(address, length);
            return true;
        }

        span = null;
        return false;
    }

    /// <summary>Sets the clipboard data.</summary>
    /// <returns><see langword="true"/> on success, <see langword="false"/> otherwise.</returns>
    public bool End()
    {
        if (_hMem == HGLOBAL.NULL)
            return false;

        GlobalUnlock(_hMem);

        if (SetClipboardData(_format, (HANDLE)_hMem.Value) == HANDLE.NULL)
        {
            GlobalFree(_hMem);
            _hMem = HGLOBAL.NULL;
            return false;
        }

        _hMem = HGLOBAL.NULL;
        return true;
    }

    public void Dispose()
    {
        End();
    }
}
