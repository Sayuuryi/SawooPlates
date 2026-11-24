using SawooPlates.PortraitEditor.Services;

namespace SawooPlates.Capture;

/// <summary>Thumbnails of captured portraits, stored in the "Captures" folder.</summary>
public sealed class CaptureThumbnailService(IDalamudPluginInterface pluginInterface, ITextureProvider textureProvider)
    : ThumbnailService(pluginInterface, textureProvider)
{
    protected override string FolderName => "Captures";
}
