using Dalamud.Game.Command;
using Dalamud.Interface.ImGuiNotification;
using Dalamud.Interface.Windowing;
using SawooPlates.Capture;
using SawooPlates.Config;
using SawooPlates.PortraitEditor;
using SawooPlates.PortraitEditor.Services;
using SawooPlates.PortraitEditor.Utils;
using SawooPlates.PortraitEditor.Windows;
using SawooPlates.PortraitEditor.Windows.Dialogs;
using SawooPlates.PortraitEditor.Windows.MenuBarButtons;
using SawooPlates.Windows;

namespace SawooPlates;

public sealed class Plugin : IDalamudPlugin
{
    private const string Command = "/sawooplates";
    private const string CommandAlias = "/splates";

    private readonly IDalamudPluginInterface _pluginInterface;
    private readonly ICommandManager _commandManager;
    private readonly ServiceProvider _services;
    private readonly WindowSystem _windowSystem = new("SawooPlates");
    private readonly MainWindow _mainWindow;

    public Plugin(
        IDalamudPluginInterface pluginInterface,
        ICommandManager commandManager,
        IPluginLog log,
        IFramework framework,
        IChatGui chatGui,
        IClientState clientState,
        IDataManager dataManager,
        ITextureProvider textureProvider,
        IGameInteropProvider gameInteropProvider,
        IAddonLifecycle addonLifecycle,
        IAgentLifecycle agentLifecycle,
        INotificationManager notificationManager,
        IPlayerState playerState,
        IContextMenu contextMenu)
    {
        _pluginInterface = pluginInterface;
        _commandManager = commandManager;

        BgraImage.Device = pluginInterface.UiBuilder.DeviceHandle;

        _services = new ServiceCollection()
            // Dalamud
            .AddSingleton(pluginInterface)
            .AddSingleton(log)
            .AddSingleton(framework)
            .AddSingleton(chatGui)
            .AddSingleton(clientState)
            .AddSingleton(dataManager)
            .AddSingleton(textureProvider)
            .AddSingleton(gameInteropProvider)
            .AddSingleton(addonLifecycle)
            .AddSingleton(agentLifecycle)
            .AddSingleton(notificationManager)
            .AddSingleton(playerState)
            .AddSingleton(contextMenu)
            .AddSingleton(_windowSystem)
            // Common
            .AddSingleton(sp => PluginConfig.Load(sp.GetRequiredService<IDalamudPluginInterface>(), sp.GetRequiredService<IPluginLog>()))
            .AddSingleton<ExcelService>()
            .AddSingleton<TextService>()
            // Portrait editor
            .AddSingleton<BannerService>()
            .AddSingleton<ClipboardService>()
            .AddSingleton<ThumbnailService>()
            .AddSingleton<MenuBarState>()
            .AddSingleton<AlignmentToolRenderer>()
            .AddSingleton<CreatePresetDialog>()
            .AddSingleton<CreateTagDialog>()
            .AddSingleton<DeletePresetDialog>()
            .AddSingleton<DeleteTagDialog>()
            .AddSingleton<EditPresetDialog>()
            .AddSingleton<RenameTagDialog>()
            .AddSingleton<ResetButton>()
            .AddSingleton<CopyButton>()
            .AddSingleton<PasteButton>()
            .AddSingleton<AdvancedImportButton>()
            .AddSingleton<AdvancedEditButton>()
            .AddSingleton<SaveAsPresetButton>()
            .AddSingleton<PresetBrowserButton>()
            .AddSingleton<AlignmentToolButton>()
            .AddSingleton<MenuBar>()
            .AddSingleton<PortraitEditorModule>()
            // Capture
            .AddSingleton<CaptureThumbnailService>()
            .AddSingleton<CaptureService>()
            .AddSingleton<GearReader>()
            .AddSingleton<CollectionService>()
            .AddSingleton<CharaCardSource>()
            .AddSingleton<BannerPartySource>()
            .AddSingleton<PartyCaptureBar>()
            .AddSingleton<PortraitActions>()
            .AddSingleton<PlateCaptureButton>()
            .AddSingleton<PlateContextMenu>()
            // UI
            .AddSingleton<PresetDetailPanel>()
            .AddSingleton<SawooPlates.Windows.Tabs.RecentTab>()
            .AddSingleton<SawooPlates.Windows.Tabs.PresetsTab>()
            .AddSingleton<SawooPlates.Windows.Tabs.SettingsTab>()
            .AddSingleton<SawooPlates.Windows.Tabs.SettingsTab>()
            .AddSingleton<MainWindow>()
            .BuildServiceProvider();

        _mainWindow = _services.GetRequiredService<MainWindow>();
        _windowSystem.AddWindow(_mainWindow);

        _services.GetRequiredService<PortraitEditorModule>();
        _services.GetRequiredService<CharaCardSource>();
        _services.GetRequiredService<BannerPartySource>();
        _windowSystem.AddWindow(_services.GetRequiredService<PartyCaptureBar>());
        _services.GetRequiredService<PlateContextMenu>();
        _windowSystem.AddWindow(_services.GetRequiredService<PlateCaptureButton>());

        _commandManager.AddHandler(Command, new CommandInfo(OnCommand) { HelpMessage = "Open the Sawoo Plates window. \"/splates dump party\" or \"dump mvp\" writes the open portrait window's layout to /xllog." });
        _commandManager.AddHandler(CommandAlias, new CommandInfo(OnCommand) { ShowInHelp = false });

        _pluginInterface.UiBuilder.Draw += _windowSystem.Draw;
        _pluginInterface.UiBuilder.OpenMainUi += _mainWindow.Toggle;
        _pluginInterface.UiBuilder.OpenConfigUi += OpenSettings;
    }

    public void Dispose()
    {
        _pluginInterface.UiBuilder.Draw -= _windowSystem.Draw;
        _pluginInterface.UiBuilder.OpenMainUi -= _mainWindow.Toggle;
        _pluginInterface.UiBuilder.OpenConfigUi -= OpenSettings;

        _commandManager.RemoveHandler(Command);
        _commandManager.RemoveHandler(CommandAlias);

        _services.Dispose();
        _windowSystem.RemoveAllWindows();
    }

    private void OnCommand(string command, string args)
    {
        var parts = args.Trim().ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 2 && parts[0] == "dump" && parts[1] is "party" or "mvp")
        {
            var source = parts[1] == "mvp" ? CaptureSource.MvpVote : CaptureSource.PartyPortrait;
            var framework = _services.GetRequiredService<IFramework>();
            framework.RunOnFrameworkThread(() =>
            {
                _services.GetRequiredService<BannerPartySource>().DumpAddon(source);
                _services.GetRequiredService<IChatGui>().Print("[Sawoo Plates] Window layout written to /xllog.");
            });
            return;
        }

        _mainWindow.Toggle();
    }

    private void OpenSettings() => _mainWindow.Open(MainWindow.Tab.Settings);
}
