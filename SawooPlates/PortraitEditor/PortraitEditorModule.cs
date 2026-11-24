using System.Threading;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Hooking;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Game.Control;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using SawooPlates.Config;
using SawooPlates.PortraitEditor.Windows;
using GameAgentId = FFXIVClientStructs.FFXIV.Client.UI.Agent.AgentId;

namespace SawooPlates.PortraitEditor;

public sealed unsafe class PortraitEditorModule : IDisposable
{
    private static readonly TimeSpan CheckDelay = TimeSpan.FromMilliseconds(500);

    private readonly IPluginLog _logger;
    private readonly IChatGui _chatGui;
    private readonly TextService _textService;
    private readonly ExcelService _excelService;
    private readonly IAddonLifecycle _addonLifecycle;
    private readonly IFramework _framework;
    private readonly IClientState _clientState;
    private readonly PluginConfig _pluginConfig;
    private readonly MenuBar _menuBar;

    private CancellationTokenSource? _mismatchCheckCTS;
    private DalamudLinkPayload? _openPortraitEditPayload;

    private Hook<RaptureGearsetModule.Delegates.UpdateGearset>? _updateGearsetHook;
    private Hook<UpdateGearVisibilityDelegate>? _updateGearVisibility;
    private bool _wasBoundByDuty;
    private bool _classJobChanged;

    private delegate void UpdateGearVisibilityDelegate(uint entityId, nint packet);

    private PortraitEditorSettings Config => _pluginConfig.PortraitEditor;

    public PortraitEditorModule(
        IPluginLog logger,
        IChatGui chatGui,
        TextService textService,
        ExcelService excelService,
        IGameInteropProvider gameInteropProvider,
        IAddonLifecycle addonLifecycle,
        IFramework framework,
        IClientState clientState,
        PluginConfig pluginConfig,
        MenuBar menuBar)
    {
        _logger = logger;
        _chatGui = chatGui;
        _textService = textService;
        _excelService = excelService;
        _addonLifecycle = addonLifecycle;
        _framework = framework;
        _clientState = clientState;
        _pluginConfig = pluginConfig;
        _menuBar = menuBar;

        _framework.RunOnFrameworkThread(() =>
        {
            _updateGearsetHook = gameInteropProvider.HookFromAddress<RaptureGearsetModule.Delegates.UpdateGearset>(
                RaptureGearsetModule.MemberFunctionPointers.UpdateGearset,
                UpdateGearsetDetour);
            _updateGearsetHook.Enable();

            try
            {
                _updateGearVisibility = gameInteropProvider.HookFromSignature<UpdateGearVisibilityDelegate>(
                    "48 89 74 24 ?? 57 48 83 EC ?? 48 8B FA 8B D1",
                    UpdateGearVisibilityDetour);
                _updateGearVisibility.Enable();
            }
            catch (Exception ex)
            {
                // signature may break after a game patch; gear checks after job changes then rely on OnClassJobChange alone
                _logger.Warning(ex, "Could not hook UpdateGearVisibility");
            }

            _addonLifecycle.RegisterListener(AddonEvent.PostSetup, "BannerEditor", OnBannerEditorSetup);
            _addonLifecycle.RegisterListener(AddonEvent.PreFinalize, "BannerEditor", OnBannerEditorFinalize);

            _clientState.TerritoryChanged += OnTerritoryChanged;
            _clientState.ClassJobChanged += OnClassJobChange;

            _openPortraitEditPayload = _chatGui.AddChatLinkHandler(1, OpenPortraitEditChatHandler);

            if (AddonUtil.IsAddonOpen(GameAgentId.BannerEditor) && _pluginConfig.EnablePortraitEditorTools)
                _menuBar.Open();
        });
    }

    public void Dispose()
    {
        _addonLifecycle.UnregisterListener(AddonEvent.PostSetup, "BannerEditor", OnBannerEditorSetup);
        _addonLifecycle.UnregisterListener(AddonEvent.PreFinalize, "BannerEditor", OnBannerEditorFinalize);

        _clientState.TerritoryChanged -= OnTerritoryChanged;
        _clientState.ClassJobChanged -= OnClassJobChange;

        _updateGearsetHook?.Dispose();
        _updateGearVisibility?.Dispose();

        if (_openPortraitEditPayload != null)
            _chatGui.RemoveChatLinkHandler(_openPortraitEditPayload.CommandId);

        _mismatchCheckCTS?.Cancel();
        _mismatchCheckCTS?.Dispose();
        _mismatchCheckCTS = null;

        _menuBar.Close();
    }

    /// <summary>Opens or closes the menu bar when the setting changes while the editor is open.</summary>
    public void RefreshMenuBar()
    {
        if (_pluginConfig.EnablePortraitEditorTools && AddonUtil.IsAddonOpen(GameAgentId.BannerEditor))
            _menuBar.Open();
        else
            _menuBar.Close();
    }

    private void OpenPortraitEditChatHandler(uint commandId, SeString message)
    {
        var raptureGearsetModule = RaptureGearsetModule.Instance();
        var gearsetId = raptureGearsetModule->CurrentGearsetIndex;
        if (!raptureGearsetModule->IsValidGearset(gearsetId))
            return;

        AgentGearSet.Instance()->OpenBannerEditorForGearset(gearsetId);
    }

    private void OnBannerEditorSetup(AddonEvent type, AddonArgs args)
    {
        if (!_pluginConfig.EnablePortraitEditorTools)
            return;

        _menuBar.Open();
    }

    private void OnBannerEditorFinalize(AddonEvent type, AddonArgs args)
    {
        _menuBar.Close();
    }

    private void RestartCheck()
    {
        _mismatchCheckCTS?.Cancel();
        _mismatchCheckCTS = new();

        _ = _framework.RunOnTick(
            CheckForGearChecksumMismatch,
            CheckDelay,
            cancellationToken: _mismatchCheckCTS.Token);
    }

    private void OnTerritoryChanged(uint territoryTypeId)
    {
        if (_wasBoundByDuty && !Conditions.Instance()->BoundByDuty56)
        {
            _wasBoundByDuty = false;
            RestartCheck();
        }
    }

    private int UpdateGearsetDetour(RaptureGearsetModule* raptureGearsetModule, int gearsetId)
    {
        _logger.Verbose("[UpdateGearsetDetour] Changed to {gearsetId}", gearsetId);

        var ret = _updateGearsetHook!.Original(raptureGearsetModule, gearsetId);

        RestartCheck();

        return ret;
    }

    private void UpdateGearVisibilityDetour(uint entityId, nint packet)
    {
        _updateGearVisibility!.Original(entityId, packet);

        if (!_classJobChanged || entityId != Control.Instance()->LocalPlayerEntityId)
            return;

        _logger.Verbose("[UpdateGearVisibilityDetour] Updated LocalPlayer's gear visibility after Class/Job change");

        RestartCheck();
    }

    private void OnClassJobChange(uint classJobId)
    {
        _classJobChanged |= true;

        _logger.Verbose("[OnClassJobChange] Changed to {classJobId}", classJobId);

        RestartCheck();
    }

    private void CheckForGearChecksumMismatch()
    {
        if (Conditions.Instance()->DutyRecorderPlayback)
            return;

        if (Conditions.Instance()->BoundByDuty56) // delay when bound by duty
        {
            _wasBoundByDuty = true;
            return;
        }

        if (Conditions.Instance()->BetweenAreas) // requeue when moving
        {
            RestartCheck();
            return;
        }

        var raptureGearsetModule = RaptureGearsetModule.Instance();
        var gearsetId = raptureGearsetModule->CurrentGearsetIndex;

        if (!raptureGearsetModule->IsValidGearset(gearsetId))
            return;

        var gearset = raptureGearsetModule->GetGearset(gearsetId);
        if (gearset == null)
            return;

        if (Config.IgnoreDoHDoL && (!_excelService.TryGetRow<ClassJob>(gearset->ClassJob, out var classJobRow) || classJobRow.DohDolJobIndex != -1))
            return;

        var bannerIndex = gearset->BannerIndex;
        if (bannerIndex == 0) // no banner linked
            return;

        var bannerModule = BannerModule.Instance();
        var bannerId = bannerModule->GetBannerIdByBannerIndex(bannerIndex - 1);
        if (bannerId < 0) // banner not found
            return;

        var banner = bannerModule->GetBannerById(bannerId);
        if (banner == null) // banner not found
            return;

        var checksum = UIGlobals.GenerateEquippedItemsChecksum();
        Interlocked.Exchange(ref _classJobChanged, false);

        if (banner->Checksum == checksum)
            return;

        if (Config.NotifyGearChecksumMismatch)
            NotifyMismatch();
    }

    private void NotifyMismatch()
    {
        var text = _textService.Translate("Editor.GearChecksumMismatch"); // based on LogMessage#5876

        var sb = new SeStringBuilder()
            .AddUiForeground("[Sawoo Plates] ", 32);

        var raptureGearsetModule = RaptureGearsetModule.Instance();
        if (raptureGearsetModule->IsValidGearset(raptureGearsetModule->CurrentGearsetIndex) && _openPortraitEditPayload != null)
        {
            sb.Add(_openPortraitEditPayload)
              .AddText(text)
              .Add(RawPayload.LinkTerminator);
        }
        else
        {
            sb.AddText(text);
        }

        UIModule.Instance()->ShowErrorText(text, false);

        _chatGui.PrintError(sb.Build());
    }


}
