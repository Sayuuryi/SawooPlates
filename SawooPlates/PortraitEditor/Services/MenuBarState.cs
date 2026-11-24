using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using SawooPlates.PortraitEditor.Windows;

namespace SawooPlates.PortraitEditor.Services;

public class MenuBarState(IServiceProvider _serviceProvider, TextService _textService)
{

    public PortraitPresetData? InitialPreset { get; private set; }
    public IOverlay? Overlay { get; private set; }
    public string PortraitName { get; private set; } = string.Empty;

    public void Reset()
    {
        InitialPreset = null;
        PortraitName = string.Empty;
        CloseOverlay();
    }

    public void OpenOverlay<T>() where T : IOverlay
    {
        CloseOverlay();

        // created per opening and disposed on close, so not tracked by the container
        Overlay = ActivatorUtilities.CreateInstance<T>(_serviceProvider);
        Overlay.Open();
    }

    public void CloseOverlay()
    {
        if (Overlay != null)
        {
            Overlay.Close();
            Overlay.Dispose();
            Overlay = null;
        }
    }

    public void CloseOverlay<T>() where T : IOverlay
    {
        CloseOverlay(typeof(T));
    }

    public void CloseOverlay(Type type)
    {
        if (Overlay?.GetType() == type)
        {
            CloseOverlay();
        }
    }

    public unsafe void SaveInitialPreset()
    {
        if (InitialPreset != null)
            return;

        var agent = AgentBannerEditor.Instance();
        if (agent == null || agent->EditorState == null || agent->EditorState->CharaView == null)
            return;

        if (!agent->EditorState->CharaView->CharaViewPortraitCharacterLoaded)
            return;

        InitialPreset = PortraitPresetExtensions.FromState();

        if (agent->EditorState->OpenType == AgentBannerEditorState.EditorOpenType.AdventurerPlate)
        {
            PortraitName = _textService.GetAddonText(14761);
        }
        else if (agent->EditorState->OpenerEnabledGearsetIndex > -1)
        {
            var actualGearsetId = RaptureGearsetModule.Instance()->ResolveIdFromEnabledIndex((byte)agent->EditorState->OpenerEnabledGearsetIndex);
            if (actualGearsetId > -1)
            {
                var gearset = RaptureGearsetModule.Instance()->GetGearset(actualGearsetId);
                if (gearset != null)
                    PortraitName = $"{_textService.GetAddonText(756)} #{gearset->Id + 1}: {gearset->NameString}";
            }
        }
    }
}
