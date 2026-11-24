namespace SawooPlates.Windows;

/// <summary>
/// Selection for the Recent and Presets lists: click selects one, Ctrl+click adds or removes,
/// Shift+click selects a range.
/// </summary>
public sealed class ListSelection
{
    private readonly HashSet<Guid> _marked = [];
    private Guid? _anchor;

    /// <summary>The entry shown on the right when only one is selected.</summary>
    public Guid? Current { get; set; }

    public bool IsMulti => _marked.Count > 1;

    public IReadOnlyCollection<Guid> Marked => _marked;

    public bool IsSelected(Guid id) => IsMulti ? _marked.Contains(id) : Current == id;

    public void Click(Guid id, IReadOnlyList<Guid> order)
    {
        var io = ImGui.GetIO();

        if (io.KeyShift && _anchor is { } anchor)
        {
            var a = IndexOf(order, anchor);
            var b = IndexOf(order, id);
            if (a >= 0 && b >= 0)
            {
                _marked.Clear();
                for (var i = Math.Min(a, b); i <= Math.Max(a, b); i++)
                    _marked.Add(order[i]);
                Current = id;
                return;
            }
        }

        if (io.KeyCtrl)
        {
            if (_marked.Count == 0 && Current is { } current)
                _marked.Add(current);

            if (!_marked.Add(id))
                _marked.Remove(id);

            Current = _marked.Count == 1 ? _marked.First() : id;
            _anchor = id;
            return;
        }

        _marked.Clear();
        Current = id;
        _anchor = id;
    }

    /// <summary>Selects one entry and drops any multi-selection.</summary>
    public void Select(Guid id)
    {
        _marked.Clear();
        Current = id;
        _anchor = id;
    }

    public void ClearMarked() => _marked.Clear();

    /// <summary>Forgets entries that no longer exist.</summary>
    public void Keep(IEnumerable<Guid> existing)
    {
        _marked.IntersectWith(existing);
        if (_marked.Count == 1)
        {
            Current = _marked.First();
            _marked.Clear();
        }
    }

    private static int IndexOf(IReadOnlyList<Guid> order, Guid id)
    {
        for (var i = 0; i < order.Count; i++)
        {
            if (order[i] == id)
                return i;
        }

        return -1;
    }
}
