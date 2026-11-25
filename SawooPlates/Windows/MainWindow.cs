using Dalamud.Interface.Windowing;
using Dalamud.Utility;
using SawooPlates.Windows.Tabs;

namespace SawooPlates.Windows;

public sealed class MainWindow : Window
{
    public enum Tab { Recent, Presets, Settings, About }

    private readonly IDalamudPluginInterface _pluginInterface;
    private readonly RecentTab _recentTab;
    private readonly PresetsTab _presetsTab;
    private readonly SettingsTab _settingsTab;
    private Tab? _requestedTab;

    private bool _steppedAside;

    public MainWindow(IDalamudPluginInterface pluginInterface, RecentTab recentTab, PresetsTab presetsTab, SettingsTab settingsTab, WindowHandoff handoff)
        : base("Sawoo Plates##SawooPlatesMain")
    {
        _pluginInterface = pluginInterface;
        _recentTab = recentTab;
        _presetsTab = presetsTab;
        _settingsTab = settingsTab;

        // out of the way while an editor it opened is in use, back when that editor closes
        handoff.Visibility += visible =>
        {
            if (!visible && IsOpen)
            {
                _steppedAside = true;
                IsOpen = false;
            }
            else if (visible && _steppedAside)
            {
                _steppedAside = false;
                IsOpen = true;
            }
        };

        Size = new Vector2(1160, 760);
        SizeCondition = ImGuiCond.FirstUseEver;
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(800, 600),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue),
        };
    }

    public void Open(Tab tab)
    {
        _requestedTab = tab;
        _steppedAside = false;
        IsOpen = true;
        BringToFront();
    }

    /// <summary>Opens Presets with one preset selected.</summary>
    public void ShowPreset(Guid id)
    {
        _presetsTab.Select(id);
        Open(Tab.Presets);
    }

    public override void Draw()
    {
        using var tabBar = ImRaii.TabBar("##Tabs");
        if (!tabBar)
            return;

        DrawTab(Tab.Recent, "Recent", _recentTab.Draw);
        DrawTab(Tab.Presets, "Presets", _presetsTab.Draw);
        DrawTab(Tab.Settings, "Settings", _settingsTab.Draw);
        DrawTab(Tab.About, "About", DrawAbout);

        _requestedTab = null;
    }

    private void DrawTab(Tab tab, string label, Action draw)
    {
        var flags = _requestedTab == tab ? ImGuiTabItemFlags.SetSelected : ImGuiTabItemFlags.None;
        using var item = ImRaii.TabItem(label, flags);
        if (!item)
            return;

        ImGui.Spacing();
        using var child = ImRaii.Child($"##{tab}Content");
        if (child)
            draw();
    }

    private void DrawAbout()
    {
        ImGui.TextColored(Ui.Accent, "Sawoo Plates");
        ImGui.SameLine();
        ImGui.TextColored(Ui.Dim, $"{_pluginInterface.Manifest.AssemblyVersion} · by Sayuuryi");

        Ui.Section("Commands");
        Command("/splates", "Open this window");
        Command("/splates config", "Open the settings");

        ImGui.Spacing();
        ImGui.Spacing();
        if (Ui.Button(FontAwesomeIcon.Globe, "GitHub"))
            Util.OpenLink("https://github.com/Sayuuryi/SawooPlates");
    }

    private static void Command(string command, string text)
    {
        ImGui.TextColored(Ui.Accent, command);
        ImGui.SameLine(170 * Ui.Scale);
        ImGui.TextUnformatted(text);
    }
}
