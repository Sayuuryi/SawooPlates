using System.IO;
using System.Text.Json;
using Dalamud.Utility;
using Dalamud.Interface.ImGuiNotification;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.UI;

namespace SawooPlates.Capture;

/// <summary>
/// Remembers what is in your glamour dresser and armoire, per character, in Collections/&lt;id&gt;.json.
/// The game only knows their contents after you open them once per session; with this, the plugin can
/// still tell which gear you own later. Each list is replaced whenever the game has it loaded again,
/// so new items show up and removed ones go away.
/// </summary>
public sealed unsafe class GearCollectionStore : IDisposable
{
    private static readonly TimeSpan CheckEvery = TimeSpan.FromSeconds(2);
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = false };

    private readonly IDalamudPluginInterface _pluginInterface;
    private readonly IPluginLog _log;
    private readonly IFramework _framework;
    private readonly IClientState _clientState;
    private readonly IPlayerState _playerState;
    private readonly INotificationManager _notifications;
    private readonly ExcelService _excel;

    private Snapshot? _snapshot;
    private ulong _contentId;
    private DateTime _nextCheck;
    private bool _askedThisLogin;

    public GearCollectionStore(
        IDalamudPluginInterface pluginInterface,
        IPluginLog log,
        IFramework framework,
        IClientState clientState,
        IPlayerState playerState,
        INotificationManager notifications,
        ExcelService excel)
    {
        _pluginInterface = pluginInterface;
        _log = log;
        _framework = framework;
        _clientState = clientState;
        _playerState = playerState;
        _notifications = notifications;
        _excel = excel;

        _framework.Update += OnUpdate;
        _clientState.Login += OnLogin;
    }

    public void Dispose()
    {
        _framework.Update -= OnUpdate;
        _clientState.Login -= OnLogin;
    }

    private sealed class Snapshot
    {
        public HashSet<uint> Dresser { get; set; } = [];
        public DateTimeOffset? DresserSaved { get; set; }
        public HashSet<uint> Armoire { get; set; } = [];
        public DateTimeOffset? ArmoireSaved { get; set; }
    }

    /// <summary>True/false from the saved dresser list; null when it was never opened on this character.</summary>
    public bool? InDresser(uint itemId) => _snapshot?.DresserSaved == null ? null : _snapshot.Dresser.Contains(itemId);

    /// <summary>True/false from the saved armoire list; null when it was never opened on this character.</summary>
    public bool? InArmoire(uint itemId) => _snapshot?.ArmoireSaved == null ? null : _snapshot.Armoire.Contains(itemId);

    private string FilePath(ulong contentId)
        => Path.Join(_pluginInterface.ConfigDirectory.FullName, "Collections", $"{contentId:X16}.json");

    private void OnLogin() => _askedThisLogin = false;

    private void OnUpdate(IFramework framework)
    {
        if (DateTime.UtcNow < _nextCheck)
            return;
        _nextCheck = DateTime.UtcNow + CheckEvery;

        var contentId = _playerState.ContentId;
        if (contentId == 0)
            return;

        if (contentId != _contentId)
        {
            _contentId = contentId;
            _snapshot = Load(contentId);
        }

        var snapshot = _snapshot ??= new Snapshot();
        var changed = false;

        var mirage = MirageManager.Instance();
        if (mirage != null && mirage->PrismBoxLoaded)
        {
            var dresser = new HashSet<uint>();
            foreach (var id in mirage->PrismBoxItemIds)
            {
                if (id != 0)
                    dresser.Add(id % 1_000_000); // high quality items are stored with +1,000,000
            }

            if (snapshot.DresserSaved == null || !dresser.SetEquals(snapshot.Dresser))
            {
                snapshot.Dresser = dresser;
                snapshot.DresserSaved = DateTimeOffset.UtcNow;
                changed = true;
            }
        }

        var uiState = UIState.Instance();
        if (uiState != null && uiState->Cabinet.IsCabinetLoaded())
        {
            var armoire = new HashSet<uint>();
            foreach (var row in _excel.GetSheet<Lumina.Excel.Sheets.Cabinet>())
            {
                if (row.Item.RowId != 0 && uiState->Cabinet.IsItemInCabinet(row.RowId))
                    armoire.Add(row.Item.RowId);
            }

            if (snapshot.ArmoireSaved == null || !armoire.SetEquals(snapshot.Armoire))
            {
                snapshot.Armoire = armoire;
                snapshot.ArmoireSaved = DateTimeOffset.UtcNow;
                changed = true;
            }
        }

        if (changed)
            Save(contentId, snapshot);

        AskOnce(snapshot);
    }

    /// <summary>Once per login, while the dresser or armoire was never opened on this character.</summary>
    private void AskOnce(Snapshot snapshot)
    {
        if (_askedThisLogin || (snapshot.DresserSaved != null && snapshot.ArmoireSaved != null))
            return;

        _askedThisLogin = true;

        var missing = snapshot.DresserSaved == null && snapshot.ArmoireSaved == null
            ? "your glamour dresser and armoire"
            : snapshot.DresserSaved == null ? "your glamour dresser" : "your armoire";

        _notifications.AddNotification(new Notification
        {
            Title = "Sawoo Plates",
            Content = $"Open {missing} once so Sawoo Plates can tell which gear you own. It remembers them after that.",
            Type = NotificationType.Info,
            InitialDuration = TimeSpan.FromSeconds(12),
        });
    }

    private Snapshot? Load(ulong contentId)
    {
        var path = FilePath(contentId);
        if (!File.Exists(path))
            return null;

        try
        {
            return JsonSerializer.Deserialize<Snapshot>(File.ReadAllText(path), JsonOptions);
        }
        catch (Exception ex)
        {
            _log.Warning(ex, "Could not read the saved gear collection");
            return null;
        }
    }

    private void Save(ulong contentId, Snapshot snapshot)
    {
        try
        {
            var path = FilePath(contentId);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            FilesystemUtil.WriteAllTextSafe(path, JsonSerializer.Serialize(snapshot, JsonOptions));
        }
        catch (Exception ex)
        {
            _log.Warning(ex, "Could not save the gear collection");
        }
    }
}
