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

    public MainWindow(IDalamudPluginInterface pluginInterface, RecentTab recentTab, PresetsTab presetsTab, SettingsTab settingsTab)
        : base("Sawoo Plates##SawooPlatesMain")
    {
        _pluginInterface = pluginInterface;
        _recentTab = recentTab;
        _presetsTab = presetsTab;
        _settingsTab = settingsTab;

        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(700, 450),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue),
        };
    }

    public void Open(Tab tab)
    {
        _requestedTab = tab;
        IsOpen = true;
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

        using var child = ImRaii.Child($"##{tab}Content");
        if (child)
            draw();
    }

    private void DrawAbout()
    {
        ImGui.Text($"Sawoo Plates {_pluginInterface.Manifest.AssemblyVersion}");
        ImGui.Text("by Sayuuryi");
        ImGui.Spacing();

        ImGui.TextWrapped("Licensed under AGPL-3.0. The source is on GitHub:");
        if (ImGui.Button("Sawoo Plates on GitHub"))
            Util.OpenLink("https://github.com/Sayuuryi/SawooPlates");
    }
}
