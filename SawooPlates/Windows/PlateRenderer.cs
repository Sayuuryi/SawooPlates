using Dalamud.Interface.Textures;
using SawooPlates.PortraitEditor.Services;

namespace SawooPlates.Windows;

/// <summary>
/// Draws a whole Adventurer Plate: backing, base, pattern, borders, frames, accent and the portrait.
/// Positions come from the game's CharacterCard layout (plate area 740x420, portrait 256x420 on the right).
/// The drawn area grows to fit every part the plate uses, so nothing is cut off.
/// </summary>
public sealed class PlateRenderer(ITextureProvider textures, ExcelService excel)
{
    private const float PlateW = 740, PlateH = 420;

    private readonly record struct Layer(uint IconId, float X, float Y, float W, float H, bool IsPortrait = false);

    public void Draw(PlateDesignData plate, PortraitPresetData preset, ThumbnailService thumbnails, Guid imageId,
        float width, bool frameKnown = true, bool decorationKnown = true)
    {
        var layers = BuildLayers(plate);

        // bounds: the plate itself plus every part that is drawn
        float minX = 0, minY = 0, maxX = PlateW, maxY = PlateH;
        foreach (var l in layers)
        {
            minX = Math.Min(minX, l.X);
            minY = Math.Min(minY, l.Y);
            maxX = Math.Max(maxX, l.X + l.W);
            maxY = Math.Max(maxY, l.Y + l.H);
        }

        var scale = width / (maxX - minX);
        var size = new Vector2(maxX - minX, maxY - minY) * scale;
        var origin = ImGui.GetCursorScreenPos();
        var draw = ImGui.GetWindowDrawList();

        Vector2 At(float x, float y) => origin + new Vector2(x - minX, y - minY) * scale;

        draw.AddRectFilled(origin, origin + size, ImGui.GetColorU32(ImGuiCol.FrameBg), 4 * Ui.Scale);

        foreach (var l in layers)
        {
            if (l.IsPortrait)
            {
                DrawPortrait(draw, preset, thumbnails, imageId, At(l.X, l.Y), At(l.X + l.W, l.Y + l.H), frameKnown, decorationKnown);
                continue;
            }

            if (textures.TryGetFromGameIcon(new GameIconLookup(l.IconId), out var tex) && tex.TryGetWrap(out var wrap, out _))
                draw.AddImage(wrap.Handle, At(l.X, l.Y), At(l.X + l.W, l.Y + l.H));
        }

        ImGui.Dummy(size);
    }

    /// <summary>Parts back to front, in the same order as the game.</summary>
    private List<Layer> BuildLayers(PlateDesignData plate)
    {
        var layers = new List<Layer>();

        // the game's "invert" flag puts the portrait on the left and mirrors what follows it
        var inverted = plate.InvertPortraitPlacement;
        float Mirror(float x, float w) => inverted ? PlateW - x - w : x;

        var decorations = plate.Decorations
            .Where(id => id != 0)
            .Select(id => excel.TryGetRow<CharaCardDecoration>(id, out var row) ? row : (CharaCardDecoration?)null)
            .Where(row => row != null)
            .Select(row => row!.Value)
            .ToList();

        uint Decoration(byte component) => decorations.Where(d => d.Component == component).Select(d => (uint)d.Image).FirstOrDefault();

        void Add(uint iconId, float x, float y, float w, float h)
        {
            if (iconId != 0)
                layers.Add(new Layer(iconId, x, y, w, h));
        }

        Add(Decoration(1), -270, -150, 1280, 720);                                     // backing
        if (excel.TryGetRow<CharaCardBase>(plate.BasePlate, out var basePlate))
            Add((uint)basePlate.Image, 0, 0, PlateW, PlateH);                          // base plate
        Add(Decoration(2), 0, 0, PlateW, PlateH);                                      // pattern overlay

        layers.Add(new Layer(0, Mirror(452, 256), 0, 256, PlateH, IsPortrait: true));  // portrait

        if (plate.BottomBorder != 0 && excel.TryGetRow<CharaCardHeader>(plate.BottomBorder, out var bottom))
            Add((uint)bottom.BottomImage, -110, 328, 960, 128);                        // bottom border
        if (plate.TopBorder != 0 && excel.TryGetRow<CharaCardHeader>(plate.TopBorder, out var top))
            Add((uint)top.TopImage, -110, -64, 960, 128);                              // top border

        Add(Decoration(4), -110, -64, 960, 520);                                       // plate frame
        Add(Decoration(3), Mirror(360, 440), -150, 440, 720);                          // portrait frame
        Add(Decoration(5), Mirror(580, 256), 230, 256, 256);                           // accent

        return layers;
    }

    private void DrawPortrait(ImDrawListPtr draw, PortraitPresetData preset, ThumbnailService thumbnails, Guid imageId,
        Vector2 min, Vector2 max, bool frameKnown, bool decorationKnown)
    {
        void Icon(uint iconId)
        {
            if (iconId != 0 && textures.TryGetFromGameIcon(new GameIconLookup(iconId), out var tex) && tex.TryGetWrap(out var wrap, out _))
                draw.AddImage(wrap.Handle, min, max);
        }

        if (excel.TryGetRow<BannerBg>(preset.BannerBg, out var bg))
            Icon((uint)bg.Image);

        var size = max - min;
        if (thumbnails.TryGetThumbnail(imageId, (int)size.X, (int)size.Y, out _, out var wrap, out _) && wrap != null)
            draw.AddImage(wrap.Handle, min, max);

        if (frameKnown && excel.TryGetRow<BannerFrame>(preset.BannerFrame, out var frame))
            Icon((uint)frame.Image);

        if (decorationKnown && excel.TryGetRow<BannerDecoration>(preset.BannerDecoration, out var decoration))
            Icon((uint)decoration.Image);
    }
}
