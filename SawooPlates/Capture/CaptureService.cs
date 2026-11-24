using System.Collections.Concurrent;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Dalamud.Interface.ImGuiNotification;
using SawooPlates.Config;
using SawooPlates.Core.History;
using SawooPlates.PortraitEditor.Utils;

namespace SawooPlates.Capture;

/// <summary>Holds the Recent history: adds captures, stores their images and saves history.json.</summary>
public sealed class CaptureService : IDisposable
{
    private const string HistoryFile = "history.json";

    private readonly IDalamudPluginInterface _pluginInterface;
    private readonly IPluginLog _log;
    private readonly INotificationManager _notificationManager;
    private readonly PluginConfig _pluginConfig;
    private readonly TextService _textService;
    private readonly CaptureThumbnailService _thumbnails;

    // image grabs run in UiBuilder.Draw, where using the D3D11 immediate context is safe
    private readonly ConcurrentQueue<ImageRequest> _imageRequests = new();

    // one image job per entry at a time; a plate opening fires more than one event
    private readonly ConcurrentDictionary<Guid, byte> _imagesInFlight = new();
    private readonly Lock _saveLock = new();
    private CancellationTokenSource? _saveCts;

    private sealed record ImageRequest(Guid Id, Func<BgraImage?> Grab, string PresetCode, DateTime GiveUpAt);

    public CaptureService(
        IDalamudPluginInterface pluginInterface,
        IPluginLog log,
        INotificationManager notificationManager,
        PluginConfig pluginConfig,
        TextService textService,
        CaptureThumbnailService thumbnails)
    {
        _pluginInterface = pluginInterface;
        _log = log;
        _notificationManager = notificationManager;
        _pluginConfig = pluginConfig;
        _textService = textService;
        _thumbnails = thumbnails;

        History = LoadHistory();
        _pluginInterface.UiBuilder.Draw += ProcessImageRequests;
    }

    public CaptureHistory History { get; }

    public CaptureSettings Settings => _pluginConfig.Capture;

    /// <summary>Raised after a capture was added or moved to the top. Argument: the history entry.</summary>
    public event Action<CapturedPortrait>? Captured;

    public void Dispose()
    {
        _pluginInterface.UiBuilder.Draw -= ProcessImageRequests;
        SaveNow();
    }

    public string GetImagePath(Guid id) => _thumbnails.GetPortraitThumbnailPath(id);

    /// <summary>
    /// Adds a capture to the history. <paramref name="grabImage"/> is called on the draw thread
    /// (retried for a few seconds while it returns null) to store the portrait image.
    /// </summary>
    public CapturedPortrait Add(CapturedPortrait capture, Func<BgraImage?>? grabImage, bool manual)
    {
        var (result, entry, removed) = History.Add(capture, Settings.DedupePerPlayer, Settings.MaxHistorySize);

        foreach (var old in removed)
            DeleteImage(old.Id);

        if (grabImage != null && (result == AddResult.Added || !entry.HasImage) && _imagesInFlight.TryAdd(entry.Id, 0))
            _imageRequests.Enqueue(new ImageRequest(entry.Id, grabImage, entry.Preset.ToExportedString(), DateTime.UtcNow.AddSeconds(5)));

        _log.Debug("Captured portrait of {name} ({source}, {result})", entry.Name, entry.Source.ToString(), result.ToString());

        if (Settings.NotifyOnCapture && (result == AddResult.Added || manual))
        {
            _notificationManager.AddNotification(new Notification
            {
                Title = "Sawoo Plates",
                Content = _textService.Translate("SawooPlates.Capture.Notification.Captured", entry.Name),
                Type = NotificationType.Info,
            });
        }

        ScheduleSave();
        Captured?.Invoke(entry);
        return entry;
    }

    public void Remove(Guid id)
    {
        if (History.Remove(id))
        {
            DeleteImage(id);
            ScheduleSave();
        }
    }

    public void ClearAll()
    {
        foreach (var entry in History.Entries)
            DeleteImage(entry.Id);

        History.Clear();
        ScheduleSave();
    }

    /// <summary>Applies a changed max history size.</summary>
    public void TrimHistory()
    {
        foreach (var old in History.Trim(Settings.MaxHistorySize))
            DeleteImage(old.Id);

        ScheduleSave();
    }

    public void Notify(string text, NotificationType type = NotificationType.Success)
    {
        _notificationManager.AddNotification(new Notification
        {
            Title = "Sawoo Plates",
            Content = text,
            Type = type,
        });
    }

    private void ProcessImageRequests()
    {
        var count = _imageRequests.Count;
        for (var i = 0; i < count && _imageRequests.TryDequeue(out var request); i++)
        {
            BgraImage? image = null;
            try
            {
                image = request.Grab();
            }
            catch (Exception ex)
            {
                _log.Warning(ex, "Could not read portrait image");
                _imagesInFlight.TryRemove(request.Id, out _);
                continue;
            }

            if (image == null)
            {
                if (DateTime.UtcNow < request.GiveUpAt)
                    _imageRequests.Enqueue(request); // texture not loaded yet, try again next frame
                else
                    _imagesInFlight.TryRemove(request.Id, out _);
                continue;
            }

            var path = GetImagePath(request.Id);
            var embed = _pluginConfig.PortraitEditor.EmbedPresetStringInThumbnails ? request.PresetCode : null;

            _ = Task.Run(() =>
            {
                var temp = path + ".tmp";
                try
                {
                    // write next to the target and swap it in, so a thumbnail being read never blocks the save
                    image.SaveAsPng(temp, embed);
                    File.Move(temp, path, overwrite: true);
                    _thumbnails.Invalidate(request.Id);

                    var entry = History.Entries.FirstOrDefault(e => e.Id == request.Id);
                    if (entry != null)
                    {
                        entry.HasImage = true;
                        ScheduleSave();
                    }
                }
                catch (Exception ex)
                {
                    _log.Warning(ex, "Could not save portrait image");
                    try { File.Delete(temp); } catch { /* best effort */ }
                }
                finally
                {
                    image.Dispose();
                    _imagesInFlight.TryRemove(request.Id, out _);
                }
            });
        }
    }

    private void DeleteImage(Guid id)
    {
        try
        {
            var path = GetImagePath(id);
            if (File.Exists(path))
                File.Delete(path);
            _thumbnails.Invalidate(id);
        }
        catch (Exception ex)
        {
            _log.Warning(ex, "Could not delete capture image {id}", id);
        }
    }

    private string HistoryPath => Path.Join(_pluginInterface.ConfigDirectory.FullName, HistoryFile);

    private CaptureHistory LoadHistory()
    {
        try
        {
            if (File.Exists(HistoryPath))
                return CaptureHistory.FromJson(File.ReadAllText(HistoryPath));
        }
        catch (Exception ex)
        {
            _log.Error(ex, "Could not read {file}, starting with an empty history", HistoryFile);
            try { File.Copy(HistoryPath, HistoryPath + ".broken", true); } catch { /* best effort */ }
        }

        return new CaptureHistory();
    }

    private void ScheduleSave()
    {
        lock (_saveLock)
        {
            _saveCts?.Cancel();
            _saveCts = new CancellationTokenSource();
            var token = _saveCts.Token;

            _ = Task.Delay(1000, token).ContinueWith(t =>
            {
                if (!t.IsCanceled)
                    SaveNow();
            }, TaskScheduler.Default);
        }
    }

    private void SaveNow()
    {
        lock (_saveLock)
        {
            try
            {
                Directory.CreateDirectory(_pluginInterface.ConfigDirectory.FullName);
                Dalamud.Utility.FilesystemUtil.WriteAllTextSafe(HistoryPath, History.ToJson());
            }
            catch (Exception ex)
            {
                _log.Error(ex, "Could not save {file}", HistoryFile);
            }
        }
    }
}
