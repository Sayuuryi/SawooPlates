using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Dalamud.Interface.Textures;
using HalfVector2 = FFXIVClientStructs.FFXIV.Common.Math.HalfVector2;
using HalfVector4 = FFXIVClientStructs.FFXIV.Common.Math.HalfVector4;
using TerraFX.Interop.Windows;

namespace SawooPlates.Common;

public static class MathExtensions
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsApproximately<T>(this T value, T b, T? margin = default) where T : struct, IFloatingPointIeee754<T>
    {
        if (T.IsNaN(value) && T.IsNaN(b))
            return true;

        var actualMargin = margin ?? T.CreateChecked(0.0001f);
        return T.Abs(value - b) < actualMargin;
    }

    public static bool IsApproximately(this HalfVector2 value, HalfVector2 other)
        => value.X.IsApproximately(other.X)
        && value.Y.IsApproximately(other.Y);

    public static bool IsApproximately(this HalfVector4 value, HalfVector4 other)
        => value.X.IsApproximately(other.X)
        && value.Y.IsApproximately(other.Y)
        && value.Z.IsApproximately(other.Z)
        && value.W.IsApproximately(other.W);
}

public static class HResultExtensions
{
    public static void ThrowOnError(this HRESULT hr)
    {
        if (hr.FAILED)
            Marshal.ThrowExceptionForHR(hr.Value);
    }
}

public static class TextureProviderExtensions
{
    /// <summary>Draws a game icon at the cursor, or reserves the space if it is not loaded yet.</summary>
    public static void DrawIcon(this ITextureProvider textureProvider, uint iconId, Vector2 size)
    {
        if (textureProvider.TryGetFromGameIcon(new GameIconLookup(iconId), out var texture)
            && texture.TryGetWrap(out var textureWrap, out _))
        {
            ImGui.Image(textureWrap.Handle, size);
        }
        else
        {
            ImGui.Dummy(size);
        }
    }

    public static void DrawIcon(this ITextureProvider textureProvider, uint iconId, float size)
        => textureProvider.DrawIcon(iconId, new Vector2(size));

    /// <summary>Draws a game texture by path at the cursor, keeping its aspect ratio for the given width.</summary>
    public static void Draw(this ITextureProvider textureProvider, string path, float width)
    {
        var texture = textureProvider.GetFromGame(path);
        if (texture.TryGetWrap(out var textureWrap, out _))
        {
            var size = new Vector2(width, width * textureWrap.Height / Math.Max(1, textureWrap.Width));
            ImGui.Image(textureWrap.Handle, size);
        }
        else
        {
            ImGui.Dummy(new Vector2(width));
        }
    }
}

public static class EnumerableExtensions
{
    public static int IndexOf<T>(this IEnumerable<T> source, Func<T, bool> predicate)
    {
        var index = 0;
        foreach (var item in source)
        {
            if (predicate(item))
                return index;
            index++;
        }

        return -1;
    }

    public static void DisposeAndNull<T>(ref T? disposable) where T : class, IDisposable
    {
        disposable?.Dispose();
        disposable = null;
    }
}

public static class StringExtensions
{
    /// <summary>Returns <paramref name="fallback"/> when the string is null or empty (e.g. a missing Addon sheet row).</summary>
    public static string OrDefault(this string? value, string fallback)
        => string.IsNullOrEmpty(value) ? fallback : value;
}

// Position and Size helpers for AtkUnitBase and AtkResNode.
public static class AtkExtensions
{
    extension(ref FFXIVClientStructs.FFXIV.Component.GUI.AtkUnitBase addon)
    {
        public Vector2 Position => new(addon.X, addon.Y);
    }

    extension(ref FFXIVClientStructs.FFXIV.Component.GUI.AtkResNode node)
    {
        public Vector2 Position => new(node.X, node.Y);
        public Vector2 Size => new(node.Width, node.Height);
    }
}
