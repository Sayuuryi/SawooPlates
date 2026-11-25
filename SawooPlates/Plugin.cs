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

public static class PluginServices
{
    /// <summary>
    /// Registers everything the plugin creates itself. Dalamud services are registered by the constructor.
    /// Kept separate so the container can be checked outside the game.
    /// </summary>
    public static IServiceCollection AddPluginServices(this IServiceCollection services)
        => services
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
            .AddSingleton<PlateDesignApplier>()
            .AddSingleton<WindowHandoff>()
            .AddSingleton<PresetImporter>()
            .AddSingleton<ImportPromptWindow>()
            .AddSingleton<CharaCardSource>()
            .AddSingleton<BannerPartySource>()
            .AddSingleton<PartyCaptureBar>()
            .AddSingleton<PortraitActions>()
            .AddSingleton<PlateCaptureButton>()
            .AddSingleton<PlateContextMenu>()
            // UI
            .AddSingleton<PresetDetailPanel>()
            .AddSingleton<PlateItems>()
            .AddSingleton<GearCollectionStore>()
            .AddSingleton<PlateRenderer>()
            .AddSingleton<BackupService>()
            .AddSingleton<PlatePartsWindow>()
            .AddSingleton<PlateGuideWindow>()
            .AddSingleton<ApplyControls>()
            .AddSingleton<SawooPlates.Windows.Tabs.RecentTab>()
            .AddSingleton<SawooPlates.Windows.Tabs.PresetsTab>()
            .AddSingleton<SawooPlates.Windows.Tabs.SettingsTab>()
            .AddSingleton<MainWindow>();
}

public sealed class Plugin : IDalamudPlugin
{
    private const string Command = "/sawooplates";
    private const string CommandAlias = "/splates";

    private readonly IDalamudPluginInterface _pluginInterface;
    private readonly ICommandManager _commandManager;
    private readonly IPluginLog _log;
    private readonly ServiceProvider _services;
    private readonly WindowSystem _windowSystem = new("SawooPlates");
    private readonly MainWindow? _mainWindow;
    private bool _hooked;

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
        _log = log;

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
            .AddPluginServices()
            .BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });

        try
        {
            // a copy of settings, presets and history as they were before this session touches them
            _services.GetRequiredService<BackupService>().Create("startup");

            _mainWindow = _services.GetRequiredService<MainWindow>();
            _windowSystem.AddWindow(_mainWindow);

            _services.GetRequiredService<PortraitEditorModule>();
            _services.GetRequiredService<CharaCardSource>();
            _services.GetRequiredService<BannerPartySource>();
            _services.GetRequiredService<PlateContextMenu>();
            _services.GetRequiredService<GearCollectionStore>();
            _windowSystem.AddWindow(_services.GetRequiredService<PlateCaptureButton>());
            _windowSystem.AddWindow(_services.GetRequiredService<PartyCaptureBar>());
            _windowSystem.AddWindow(_services.GetRequiredService<ImportPromptWindow>());
            _windowSystem.AddWindow(_services.GetRequiredService<PlatePartsWindow>());
            _windowSystem.AddWindow(_services.GetRequiredService<PlateGuideWindow>());

            _commandManager.AddHandler(Command, new CommandInfo(OnCommand) { HelpMessage = "Open Sawoo Plates. Add \"config\" to open the settings." });
            _commandManager.AddHandler(CommandAlias, new CommandInfo(OnCommand) { ShowInHelp = false });

            _pluginInterface.UiBuilder.Draw += _windowSystem.Draw;
            _pluginInterface.UiBuilder.OpenMainUi += ToggleMain;
            _pluginInterface.UiBuilder.OpenConfigUi += OpenSettings;
            _hooked = true;
        }
        catch (Exception ex)
        {
            // undo whatever was set up, so a failed start never leaves hooks or listeners behind
            _log.Error(ex, "Sawoo Plates failed to start");
            Dispose();
            throw;
        }
    }

    public void Dispose()
    {
        if (_hooked)
        {
            _pluginInterface.UiBuilder.Draw -= _windowSystem.Draw;
            _pluginInterface.UiBuilder.OpenMainUi -= ToggleMain;
            _pluginInterface.UiBuilder.OpenConfigUi -= OpenSettings;
            _hooked = false;
        }

        Try(() => _commandManager.RemoveHandler(Command));
        Try(() => _commandManager.RemoveHandler(CommandAlias));

        // services unhook from the game in their own Dispose; keep going if one of them fails
        Try(_services.Dispose);
        Try(_windowSystem.RemoveAllWindows);
    }

    private void Try(Action action)
    {
        try
        {
            action();
        }
        catch (Exception ex)
        {
            _log.Error(ex, "Error while shutting down");
        }
    }

    private void ToggleMain() => _mainWindow?.Toggle();

    private void OnCommand(string command, string args)
    {
        if (args.Trim().Equals("config", StringComparison.OrdinalIgnoreCase))
            OpenSettings();
        else
            ToggleMain();
    }

    private void OpenSettings() => _mainWindow?.Open(MainWindow.Tab.Settings);
}
