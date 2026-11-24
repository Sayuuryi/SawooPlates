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

        Size = new Vector2(900, 600);
        SizeCondition = ImGuiCond.FirstUseEver;
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(720, 460),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue),
        };
    }

    public void Open(Tab tab)
    {
        _requestedTab = tab;
        IsOpen = true;
        BringToFront();
    }

    /// <summary>Opens Recent with one portrait selected.</summary>
    public void ShowCapture(Guid id)
    {
        _recentTab.Select(id);
        Open(Tab.Recent);
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

        ImGui.Spacing();
        using (ImRaii.TextWrapPos(ImGui.GetFontSize() * 34))
        {
            ImGui.TextUnformatted("Keeps the portraits you come across, shows what's on them and what you're missing, and adds a few tools to the Portrait Editor.");
        }

        Ui.Section("Commands");
        Command("/splates", "Open this window.");
        Command("/splates dump party", "Write the party portrait window's layout to /xllog, for bug reports.");
        Command("/splates dump mvp", "Same for the MVP vote window.");

        Ui.Section("Where things are");
        Bullet("Recent: portraits you've seen, with everything on them.");
        Bullet("Presets: your saved portraits. Also in the Portrait Editor's menu bar.");
        Bullet("Hover any item to see more about it.");

        ImGui.Spacing();
        ImGui.Spacing();
        if (Ui.Button(FontAwesomeIcon.Globe, "Source on GitHub"))
            Util.OpenLink("https://github.com/Sayuuryi/SawooPlates");

        ImGui.SameLine();
        ImGui.TextColored(Ui.Dim, "AGPL-3.0");
    }

    private static void Command(string command, string text)
    {
        ImGui.TextColored(Ui.Accent, command);
        ImGui.SameLine(170 * Ui.Scale);
        ImGui.TextUnformatted(text);
    }

    private static void Bullet(string text)
    {
        ImGui.Bullet();
        ImGui.SameLine();
        ImGui.TextUnformatted(text);
    }
}
