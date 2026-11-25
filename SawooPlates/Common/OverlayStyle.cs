using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace SawooPlates.Common;

/// <summary>Helpers for small windows drawn on top of game windows.</summary>
public static unsafe class OverlayStyle
{
    /// <summary>
    /// True when another game window is drawn in front of <paramref name="owner"/> somewhere inside the given screen rectangle,
    /// so an overlay there should hide instead of floating over that window.
    /// </summary>
    public static bool IsCovered(AtkUnitBase* owner, Vector2 min, Vector2 max)
    {
        var module = RaptureAtkModule.Instance();
        if (module == null || owner == null)
            return false;

        var ownerLayer = owner->DepthLayer;
        var ownerOrder = owner->DrawOrderIndex;

        ref var list = ref module->RaptureAtkUnitManager.AtkUnitManager.AllLoadedUnitsList;
        for (var i = 0; i < list.Count; i++)
        {
            var unit = list.Entries[i].Value;
            if (unit == null || unit == owner || !unit->IsVisible)
                continue;

            // HUD parts start with "_" (party list, duty list, nameplates...); they sit in front of everything in duties
            var name = unit->NameString;
            if (name.Length == 0 || name[0] == '_' || name == "NamePlate" || name.StartsWith("Fade", StringComparison.Ordinal))
                continue;

            var inFront = unit->DepthLayer > ownerLayer || (unit->DepthLayer == ownerLayer && unit->DrawOrderIndex > ownerOrder);
            if (!inFront)
                continue;

            var unitMin = new Vector2(unit->X, unit->Y);
            var unitMax = unitMin + new Vector2(unit->GetScaledWidth(true), unit->GetScaledHeight(true));
            if (unitMax.X - unitMin.X < 1 || unitMax.Y - unitMin.Y < 1)
                continue;

            // full-screen layers (screen effects, cutscene bars) aren't windows either
            var screen = ImGui.GetMainViewport().Size;
            if (unitMax.X - unitMin.X >= screen.X * 0.9f && unitMax.Y - unitMin.Y >= screen.Y * 0.9f)
                continue;

            if (unitMin.X < max.X && unitMax.X > min.X && unitMin.Y < max.Y && unitMax.Y > min.Y)
                return true;
        }

        return false;
    }

    /// <summary>
    /// Puts an overlay behind every other plugin window. Overlays sit on game windows, and game windows are
    /// always drawn under plugin windows, so an overlay in front of a plugin window would float over it.
    /// Call it after the window has ended (PostDraw).
    /// </summary>
    public static void SendToBack(string windowName)
    {
        var window = ImGuiP.FindWindowByName(windowName);
        if (!window.IsNull)
            ImGuiP.BringWindowToDisplayBack(window);
    }

    /// <summary>Dark rounded panel with a thin gold border, so buttons stay readable over any plate art.</summary>
    public static IDisposable PushPanel()
    {
        var style = ImRaii.PushStyle(ImGuiStyleVar.WindowRounding, 6f * ImGuiHelpers.GlobalScale)
            .Push(ImGuiStyleVar.WindowBorderSize, 1f)
            .Push(ImGuiStyleVar.WindowPadding, new Vector2(6, 5) * ImGuiHelpers.GlobalScale)
            .Push(ImGuiStyleVar.FrameRounding, 4f * ImGuiHelpers.GlobalScale);

        var color = ImRaii.PushColor(ImGuiCol.WindowBg, new Vector4(0.07f, 0.07f, 0.09f, 0.94f))
            .Push(ImGuiCol.Border, new Vector4(0.85f, 0.73f, 0.49f, 0.55f))
            .Push(ImGuiCol.Button, new Vector4(0.22f, 0.22f, 0.27f, 1f))
            .Push(ImGuiCol.ButtonHovered, new Vector4(0.36f, 0.32f, 0.22f, 1f))
            .Push(ImGuiCol.ButtonActive, new Vector4(0.50f, 0.43f, 0.26f, 1f))
            .Push(ImGuiCol.Text, new Vector4(1f, 1f, 1f, 1f));

        return new Both(style, color);
    }

    private sealed class Both(IDisposable a, IDisposable b) : IDisposable
    {
        public void Dispose()
        {
            b.Dispose();
            a.Dispose();
        }
    }
}
