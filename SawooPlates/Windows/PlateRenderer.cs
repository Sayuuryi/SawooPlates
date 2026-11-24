using Dalamud.Interface.Textures;
using SawooPlates.PortraitEditor.Services;

namespace SawooPlates.Windows;

/// <summary>
/// Draws a whole Adventurer Plate: backing, base, pattern, borders, frames, accent and the portrait.
/// Positions come from the game's CharacterCard layout (plate area 740x420, portrait 256x420 on the right).
/// </summary>
public sealed class PlateRenderer(ITextureProvider textures, ExcelService excel)
{
    // visible area around the plate, in plate units: the plate frame's bounds
    private const float AreaX = -110, AreaY = -64, AreaW = 960, AreaH = 520;

    public static float AspectRatio => AreaW / AreaH;

    public void Draw(PlateDesignData plate, PortraitPresetData preset, ThumbnailService thumbnails, Guid imageId,
        float width, bool frameKnown = true, bool decorationKnown = true)
    {
        var scale = width / AreaW;
        var size = new Vector2(AreaW, AreaH) * scale;
        var origin = ImGui.GetCursorScreenPos();
        var draw = ImGui.GetWindowDrawList();

        // plate units -> screen
        Vector2 At(float x, float y) => origin + new Vector2(x - AreaX, y - AreaY) * scale;

        void Image(uint iconId, float x, float y, float w, float h)
        {
            if (iconId == 0 || !textures.TryGetFromGameIcon(new GameIconLookup(iconId), out var tex) || !tex.TryGetWrap(out var wrap, out _))
                return;
            draw.AddImage(wrap.Handle, At(x, y), At(x + w, y + h));
        }

        draw.PushClipRect(origin, origin + size, true);
        draw.AddRectFilled(origin, origin + size, ImGui.GetColorU32(ImGuiCol.FrameBg), 4 * Ui.Scale);

        // the game's "invert" flag puts the portrait on the left and mirrors what follows it
        var inverted = plate.InvertPortraitPlacement;
        float Mirror(float x, float w) => inverted ? 740 - x - w : x;

        var decorations = plate.Decorations
            .Where(id => id != 0)
            .Select(id => excel.TryGetRow<CharaCardDecoration>(id, out var row) ? row : (CharaCardDecoration?)null)
            .Where(row => row != null)
            .Select(row => row!.Value)
            .ToList();

        uint Decoration(byte component) => decorations.Where(d => d.Component == component).Select(d => (uint)d.Image).FirstOrDefault();

        // back to front, same order as the game
        Image(Decoration(1), -270, -150, 1280, 720);                                                    // backing
        if (excel.TryGetRow<CharaCardBase>(plate.BasePlate, out var basePlate))
            Image((uint)basePlate.Image, 0, 0, 740, 420);                                               // base plate
        Image(Decoration(2), 0, 0, 740, 420);                                                           // pattern overlay

        DrawPortrait(draw, preset, thumbnails, imageId, At(Mirror(452, 256), 0), At(Mirror(452, 256) + 256, 420), frameKnown, decorationKnown);

        if (plate.BottomBorder != 0 && excel.TryGetRow<CharaCardHeader>(plate.BottomBorder, out var bottom))
            Image((uint)bottom.BottomImage, -110, 328, 960, 128);                                       // bottom border
        if (plate.TopBorder != 0 && excel.TryGetRow<CharaCardHeader>(plate.TopBorder, out var top))
            Image((uint)top.TopImage, -110, -64, 960, 128);                                             // top border

        Image(Decoration(4), -110, -64, 960, 520);                                                      // plate frame
        Image(Decoration(3), Mirror(360, 440), -150, 440, 720);                                         // portrait frame
        Image(Decoration(5), Mirror(580, 256), 230, 256, 256);                                          // accent

        draw.PopClipRect();
        ImGui.Dummy(size);
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
