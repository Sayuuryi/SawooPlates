using System.Runtime.CompilerServices;
using System.Text;
using Dalamud.Game.Agent;
using Dalamud.Game.Agent.AgentArgTypes;
using FFXIVClientStructs.FFXIV.Client.Graphics.Render;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using FFXIVClientStructs.FFXIV.Component.GUI;
using SawooPlates.PortraitEditor;
using SawooPlates.PortraitEditor.Utils;
using TerraFX.Interop.DirectX;
using DAgentId = Dalamud.Game.Agent.AgentId;
using GameAgentId = FFXIVClientStructs.FFXIV.Client.UI.Agent.AgentId;

namespace SawooPlates.Capture;

/// <summary>
/// Reads the party portraits shown at duty start (BannerParty) and on the MVP vote (BannerMIP)
/// from AgentBannerInterface.Data.Characters. Frame and accent are not part of that data, so
/// those captures are marked as partial.
/// </summary>
public sealed unsafe class BannerPartySource : IDisposable
{
    private static readonly TimeSpan[] ReadDelays = [TimeSpan.FromMilliseconds(500), TimeSpan.FromSeconds(1.5), TimeSpan.FromSeconds(3)];

    private readonly IPluginLog _log;
    private readonly IFramework _framework;
    private readonly IAgentLifecycle _agentLifecycle;
    private readonly IPlayerState _playerState;
    private readonly ExcelService _excelService;
    private readonly CaptureService _captureService;
    private readonly GearReader _gearReader;

    public BannerPartySource(
        IPluginLog log,
        IFramework framework,
        IAgentLifecycle agentLifecycle,
        IPlayerState playerState,
        ExcelService excelService,
        CaptureService captureService,
        GearReader gearReader)
    {
        _log = log;
        _framework = framework;
        _agentLifecycle = agentLifecycle;
        _playerState = playerState;
        _excelService = excelService;
        _captureService = captureService;
        _gearReader = gearReader;

        _agentLifecycle.RegisterListener(AgentEvent.PostShow, (DAgentId)(uint)GameAgentId.BannerParty, OnPartyShown);
        _agentLifecycle.RegisterListener(AgentEvent.PostShow, (DAgentId)(uint)GameAgentId.BannerMIP, OnMvpShown);
    }

    public void Dispose()
    {
        _agentLifecycle.UnregisterListener(AgentEvent.PostShow, (DAgentId)(uint)GameAgentId.BannerParty, OnPartyShown);
        _agentLifecycle.UnregisterListener(AgentEvent.PostShow, (DAgentId)(uint)GameAgentId.BannerMIP, OnMvpShown);
    }

    public record struct Member(int Index, string Name, string Job, ushort WorldId);

    private void OnPartyShown(AgentEvent type, AgentArgs args)
    {
        if (_captureService.Settings.AutoCapturePartyPortraits)
            ScheduleReads(CaptureSource.PartyPortrait);
    }

    private void OnMvpShown(AgentEvent type, AgentArgs args)
    {
        if (_captureService.Settings.AutoCaptureMvpVote)
            ScheduleReads(CaptureSource.MvpVote);
    }

    // portraits finish loading a bit after the window shows; dedupe in the history keeps this to one entry each
    private void ScheduleReads(CaptureSource source)
    {
        foreach (var delay in ReadDelays)
            _ = _framework.RunOnTick(() => CaptureAll(source, manual: false), delay);
    }

    public static GameAgentId GetAgentId(CaptureSource source)
        => source == CaptureSource.MvpVote ? GameAgentId.BannerMIP : GameAgentId.BannerParty;

    private static AgentBannerInterface.Storage* GetStorage(CaptureSource source)
    {
        var agentModule = AgentModule.Instance();
        if (agentModule == null)
            return null;

        var agent = (AgentBannerInterface*)agentModule->GetAgentByInternalId(GetAgentId(source));
        if (agent == null || !agent->IsAgentActive() || agent->Data == null)
            return null;

        return agent->Data;
    }

    /// <summary>True when the party or MVP portrait window is open.</summary>
    public static bool IsOpen(CaptureSource source)
        => GetStorage(source) != null && AddonUtil.IsAddonOpen(GetAgentId(source));

    /// <summary>Members shown right now. Must be called on the framework thread.</summary>
    public List<Member> GetMembers(CaptureSource source)
    {
        var list = new List<Member>();
        var storage = GetStorage(source);
        if (storage == null)
            return list;

        for (var i = 0; i < AgentBannerInterface.Storage.NumCharacters; i++)
        {
            ref var character = ref storage->Characters[i];
            var name = character.Name1.ToString();
            if (string.IsNullOrEmpty(name))
                continue;

            list.Add(new Member(i, name, character.Job.ToString(), (ushort)character.WorldId));
        }

        return list;
    }

    /// <summary>Captures every member shown. Must be called on the framework thread.</summary>
    public int CaptureAll(CaptureSource source, bool manual)
    {
        var count = 0;
        foreach (var member in GetMembers(source))
        {
            if (Capture(source, member.Index, manual) != null)
                count++;
        }

        return count;
    }

    /// <summary>Captures one member. Must be called on the framework thread.</summary>
    public CapturedPortrait? Capture(CaptureSource source, int index, bool manual)
    {
        try
        {
            var storage = GetStorage(source);
            if (storage == null || index < 0 || index >= AgentBannerInterface.Storage.NumCharacters)
                return null;

            ref var character = ref storage->Characters[index];
            var name = character.Name1.ToString();
            if (string.IsNullOrEmpty(name))
                return null;

            var worldId = (ushort)character.WorldId;
            if (!manual && _captureService.Settings.SkipOwnCharacter && IsLocalPlayer(name, worldId))
                return null;

            var charaView = (CharaViewPortrait*)Unsafe.AsPointer(ref character.CharaView);

            var exported = stackalloc ExportedPortraitData[1];
            charaView->ExportPortraitData(exported);

            var preset = new PortraitPresetData();
            preset.ReadExportedPortraitData(exported);

            if (preset.CameraPosition == default && preset.CameraTarget == default && preset.BannerTimeline == 0)
                return null; // not loaded yet

            var capture = new CapturedPortrait
            {
                Source = source,
                Name = name,
                WorldId = worldId,
                WorldName = _excelService.TryGetRow<World>(worldId, out var world) ? world.Name.ToString() : string.Empty,
                Job = character.Job.ToString(),
                Title = character.Title.ToString(),
                Preset = preset,
                JobId = FindJobId(character.Job.ToString()),
                Gear = _gearReader.Read(charaView),
                FrameKnown = false,
                DecorationKnown = false,
            };

            return _captureService.Add(capture, () => GrabImage(source, index, name), manual);
        }
        catch (Exception ex)
        {
            _log.Error(ex, "Error while reading the party portraits");
            return null;
        }
    }

    /// <summary>The party screen only has the job's display name; look up its id for the icon.</summary>
    private byte FindJobId(string job)
    {
        if (string.IsNullOrWhiteSpace(job))
            return 0;

        foreach (var row in _excelService.GetSheet<ClassJob>())
        {
            if (row.RowId != 0 && (
                string.Equals(row.Name.ToString(), job, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(row.NameEnglish.ToString(), job, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(row.Abbreviation.ToString(), job, StringComparison.OrdinalIgnoreCase)))
            {
                return (byte)row.RowId;
            }
        }

        return 0;
    }

    private bool IsLocalPlayer(string name, ushort worldId)
        => _playerState.IsLoaded && _playerState.CharacterName == name && _playerState.HomeWorld.RowId == worldId;

    /// <summary>Called on the draw thread. Returns null while the portrait is not rendered yet.</summary>
    private static BgraImage? GrabImage(CaptureSource source, int index, string name)
    {
        var storage = GetStorage(source);
        if (storage == null)
            return null;

        ref var character = ref storage->Characters[index];
        if (character.Name1.ToString() != name || !character.CharaView.CharaViewPortraitCharacterLoaded)
            return null;

        var manager = RenderTargetManager.Instance();
        if (manager == null)
            return null;

        var texture = manager->GetCharaViewTexture(character.CharaView.ClientObjectIndex);
        if (texture == null || texture->D3D11Texture2D == null)
            return null;

        return BgraImage.FromTexture2D((ID3D11Texture2D*)texture->D3D11Texture2D);
    }

    /// <summary>
    /// Writes the node tree of the open window to the log (node ids, positions, sizes, textures).
    /// Used to find where each portrait sits and whether frame/accent textures can be read from it.
    /// </summary>
    public string DumpAddon(CaptureSource source)
    {
        var addon = AddonUtil.GetAddon<AtkUnitBase>(GetAgentId(source));
        if (addon == null)
            return "Window not open.";

        var sb = new StringBuilder();
        sb.AppendLine($"{addon->NameString} at {addon->X},{addon->Y} scale {addon->Scale} size {addon->GetScaledWidth(true)}x{addon->GetScaledHeight(true)}");

        for (var i = 0; i < addon->UldManager.NodeListCount; i++)
            DumpNode(sb, addon->UldManager.NodeList[i], 1);

        foreach (var member in GetMembers(source))
            sb.AppendLine($"member {member.Index}: {member.Name} {member.Job} world {member.WorldId}");

        var text = sb.ToString();
        _log.Information("[Dump {source}]\n{text}", source.ToString(), text);
        return text;
    }

    private static void DumpNode(StringBuilder sb, AtkResNode* node, int depth)
    {
        if (node == null)
            return;

        var line = $"{new string(' ', depth * 2)}#{node->NodeId} {node->Type} pos {node->X},{node->Y} size {node->Width}x{node->Height} visible {node->IsVisible()}";

        if (node->Type == NodeType.Image)
        {
            var image = (AtkImageNode*)node;
            if (image->PartsList != null && image->PartId < image->PartsList->PartCount)
            {
                var asset = image->PartsList->Parts[image->PartId].UldAsset;
                if (asset != null && asset->AtkTexture.Resource != null)
                {
                    var resource = asset->AtkTexture.Resource;
                    line += $" icon {resource->IconId}";
                    if (resource->TexFileResourceHandle != null)
                        line += $" tex {resource->TexFileResourceHandle->ResourceHandle.FileName}";
                }
            }
        }

        sb.AppendLine(line);

        if ((int)node->Type >= 1000)
        {
            var component = ((AtkComponentNode*)node)->Component;
            if (component != null)
            {
                for (var i = 0; i < component->UldManager.NodeListCount; i++)
                    DumpNode(sb, component->UldManager.NodeList[i], depth + 1);
            }
        }
    }
}
