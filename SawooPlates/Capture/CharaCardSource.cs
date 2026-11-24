using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using SawooPlates.PortraitEditor;
using SawooPlates.PortraitEditor.Utils;
using TerraFX.Interop.DirectX;

namespace SawooPlates.Capture;

/// <summary>
/// Reads the portrait shown on an Adventurer Plate (CharaCard addon) from AgentCharaCard.Data.
/// Only reads what the client already received when the player opened the plate.
/// </summary>
public sealed unsafe class CharaCardSource : IDisposable
{
    public const string AddonName = "CharaCard";

    private readonly IPluginLog _log;
    private readonly IFramework _framework;
    private readonly IAddonLifecycle _addonLifecycle;
    private readonly IPlayerState _playerState;
    private readonly ExcelService _excelService;
    private readonly CaptureService _captureService;
    private readonly GearReader _gearReader;

    public CharaCardSource(
        IPluginLog log,
        IFramework framework,
        IAddonLifecycle addonLifecycle,
        IPlayerState playerState,
        ExcelService excelService,
        CaptureService captureService,
        GearReader gearReader)
    {
        _log = log;
        _framework = framework;
        _addonLifecycle = addonLifecycle;
        _playerState = playerState;
        _excelService = excelService;
        _captureService = captureService;
        _gearReader = gearReader;

        _addonLifecycle.RegisterListener(AddonEvent.PostSetup, AddonName, OnPlateShown);
        _addonLifecycle.RegisterListener(AddonEvent.PostRefresh, AddonName, OnPlateShown);
    }

    public void Dispose()
    {
        _addonLifecycle.UnregisterListener(AddonEvent.PostSetup, AddonName, OnPlateShown);
        _addonLifecycle.UnregisterListener(AddonEvent.PostRefresh, AddonName, OnPlateShown);
    }

    private void OnPlateShown(AddonEvent type, AddonArgs args)
    {
        if (!_captureService.Settings.AutoCaptureAdventurerPlate)
            return;

        // give the agent a moment to finish filling its data
        _ = _framework.RunOnTick(() => TryCapture(manual: false), TimeSpan.FromMilliseconds(250));
    }

    /// <summary>True when an Adventurer Plate is open and its data can be read.</summary>
    public bool IsPlateOpen => AddonUtil.IsAddonOpen(AddonName) && TryRead(out _);

    /// <summary>Content id of the plate that is open right now, or 0.</summary>
    public ulong OpenPlateContentId
    {
        get
        {
            var agent = AgentCharaCard.Instance();
            return agent != null && agent->Data != null && AddonUtil.IsAddonOpen(AddonName) ? agent->Data->ContentId : 0;
        }
    }

    /// <summary>Must be called on the framework thread.</summary>
    public CapturedPortrait? TryCapture(bool manual)
    {
        try
        {
            if (!AddonUtil.IsAddonOpen(AddonName) || !TryRead(out var capture))
                return null;

            if (!manual && _captureService.Settings.SkipOwnCharacter && capture.ContentId != 0 && capture.ContentId == _playerState.ContentId)
                return null;

            var contentId = capture.ContentId;
            var entry = _captureService.Add(capture, () => GrabImage(contentId), manual);

            // the model (and so the gear list) can finish loading after the plate opens
            if (entry.Gear.Count == 0)
                ScheduleGearRead(entry.Id, contentId);

            return entry;
        }
        catch (Exception ex)
        {
            _log.Error(ex, "Error while reading the Adventurer Plate");
            return null;
        }
    }

    private bool TryRead(out CapturedPortrait capture)
    {
        capture = null!;

        var agent = AgentCharaCard.Instance();
        if (agent == null || agent->Data == null)
            return false;

        var data = agent->Data;
        if (data->IsNotCreated)
            return false;

        var name = data->Name.ToString();
        if (string.IsNullOrEmpty(name))
            return false;

        var preset = new PortraitPresetData();
        preset.ReadExportedPortraitData(&data->PortraitData);

        if (preset.BannerBg == 0)
            preset.BannerBg = data->BannerBg;
        else if (preset.BannerBg != data->BannerBg)
            _log.Debug("CharaCard BannerBg differs: PortraitData {a}, Storage {b}", preset.BannerBg, data->BannerBg);

        preset.BannerFrame = data->BannerFrame;
        preset.BannerDecoration = data->BannerDecoration;

        // all-zero camera means the portrait data was not filled yet
        if (preset.CameraPosition == default && preset.CameraTarget == default && preset.BannerTimeline == 0)
            return false;

        var worldName = _excelService.TryGetRow<World>(data->WorldId, out var world) ? world.Name.ToString() : string.Empty;
        var job = _excelService.TryGetRow<ClassJob>(data->ClassJobId, out var classJob) ? classJob.Abbreviation.ToString() : string.Empty;
        var title = string.Empty;
        if (data->TitleId != 0 && _excelService.TryGetRow<Title>(data->TitleId, out var titleRow))
            title = (data->Sex == 1 ? titleRow.Feminine : titleRow.Masculine).ToString();

        capture = new CapturedPortrait
        {
            Source = CaptureSource.AdventurerPlate,
            ContentId = data->ContentId,
            Name = name,
            WorldId = data->WorldId,
            WorldName = worldName,
            Job = job,
            Title = title,
            Preset = preset,
            JobId = data->ClassJobId,
            Plate = new PlateDesignData
            {
                BasePlate = data->PlateDesign.BasePlate,
                TopBorder = data->PlateDesign.TopBorder,
                BottomBorder = data->PlateDesign.BottomBorder,
                Decorations = data->PlateDesign.Decorations[..Math.Min((int)data->PlateDesign.NumDecorations, data->PlateDesign.Decorations.Length)].ToArray(),
                PortraitOnRight = data->InvertPortraitPlacement,
            },
            Gear = _gearReader.Read(&data->CharaView),
        };

        return true;
    }

    private void ScheduleGearRead(Guid id, ulong contentId)
    {
        foreach (var delay in new[] { TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(3) })
        {
            _ = _framework.RunOnTick(() =>
            {
                var agent = AgentCharaCard.Instance();
                if (agent == null || agent->Data == null || agent->Data->ContentId != contentId)
                    return;

                var gear = _gearReader.Read(&agent->Data->CharaView);
                if (gear.Count > 0)
                    _captureService.SetGear(id, gear);
            }, delay);
        }
    }

    /// <summary>Called on the draw thread. Returns null while the texture is not loaded yet.</summary>
    private BgraImage? GrabImage(ulong contentId)
    {
        var agent = AgentCharaCard.Instance();
        if (agent == null || agent->Data == null || agent->Data->ContentId != contentId)
            return null;

        var texture = agent->Data->PortraitTexture;
        if (texture == null || texture->D3D11Texture2D == null)
            return null;

        return BgraImage.FromTexture2D((ID3D11Texture2D*)texture->D3D11Texture2D);
    }
}
