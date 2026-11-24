using System.Text.Json;
using System.Text.Json.Serialization;
using SawooPlates.Core.Models;

namespace SawooPlates.Core.History;

public enum AddResult
{
    Added,
    /// <summary>Same player with the same preset already in the history; its entry was moved to the top.</summary>
    Duplicate,
}

/// <summary>Recent captures, newest first.</summary>
public sealed class CaptureHistory
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        IncludeFields = true,
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly List<CapturedPortrait> _entries = [];
    private readonly Lock _lock = new();

    public IReadOnlyList<CapturedPortrait> Entries
    {
        get
        {
            lock (_lock)
                return _entries.ToArray();
        }
    }

    /// <summary>
    /// Adds a capture. With <paramref name="dedupe"/>, an existing entry for the same player and the same
    /// preset bytes is moved to the top (keeping its id and image) instead of adding a new one.
    /// Returns the entries that were dropped because the history is over <paramref name="maxSize"/>.
    /// </summary>
    public (AddResult Result, CapturedPortrait Entry, IReadOnlyList<CapturedPortrait> Removed) Add(CapturedPortrait capture, bool dedupe, int maxSize)
    {
        lock (_lock)
        {
            if (dedupe)
            {
                var bytes = capture.Preset.ToBytes();
                var index = _entries.FindIndex(e => e.PlayerKey == capture.PlayerKey && e.Preset.ToBytes().AsSpan().SequenceEqual(bytes));
                if (index >= 0)
                {
                    var existing = _entries[index];
                    var updated = existing with
                    {
                        Time = capture.Time,
                        Source = capture.Source,
                        Job = string.IsNullOrEmpty(capture.Job) ? existing.Job : capture.Job,
                        Title = string.IsNullOrEmpty(capture.Title) ? existing.Title : capture.Title,
                        JobId = capture.JobId != 0 ? capture.JobId : existing.JobId,
                        Plate = capture.Plate ?? existing.Plate,
                        Gear = capture.Gear.Count > 0 ? capture.Gear : existing.Gear,
                    };
                    _entries.RemoveAt(index);
                    _entries.Insert(0, updated);
                    return (AddResult.Duplicate, updated, []);
                }
            }

            _entries.Insert(0, capture);
            return (AddResult.Added, capture, Trim(maxSize));
        }
    }

    /// <summary>Sets the gear of an entry (read a moment after the capture, once the portrait has loaded).</summary>
    public bool SetGear(Guid id, List<GearPiece> gear)
    {
        lock (_lock)
        {
            var entry = _entries.FirstOrDefault(e => e.Id == id);
            if (entry == null || gear.Count == 0)
                return false;

            entry.Gear = gear;
            return true;
        }
    }

    public bool Remove(Guid id)
    {
        lock (_lock)
            return _entries.RemoveAll(e => e.Id == id) > 0;
    }

    public IReadOnlyList<CapturedPortrait> Trim(int maxSize)
    {
        lock (_lock)
        {
            if (maxSize < 1 || _entries.Count <= maxSize)
                return [];

            var removed = _entries.GetRange(maxSize, _entries.Count - maxSize);
            _entries.RemoveRange(maxSize, _entries.Count - maxSize);
            return removed;
        }
    }

    public void Clear()
    {
        lock (_lock)
            _entries.Clear();
    }

    public string ToJson()
    {
        lock (_lock)
            return JsonSerializer.Serialize(_entries, JsonOptions);
    }

    public static CaptureHistory FromJson(string json)
    {
        var history = new CaptureHistory();
        var entries = JsonSerializer.Deserialize<List<CapturedPortrait>>(json, JsonOptions);
        if (entries != null)
            history._entries.AddRange(entries.Where(e => e.Preset != null));
        return history;
    }
}
