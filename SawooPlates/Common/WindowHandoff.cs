namespace SawooPlates.Common;

/// <summary>
/// Moves the Sawoo Plates window out of the way while a game editor it opened is in use,
/// so the two don't sit on top of each other. The window comes back when that editor closes.
/// </summary>
public sealed class WindowHandoff(IFramework framework)
{
    private static readonly TimeSpan CheckEvery = TimeSpan.FromMilliseconds(500);
    private static readonly TimeSpan OpenTimeout = TimeSpan.FromSeconds(15);

    private int _generation;

    /// <summary>Raised with false to hide the window and with true to bring it back.</summary>
    public event Action<bool>? Visibility;

    /// <summary>Hides the window until <paramref name="editorOpen"/> has been true and turns false again.</summary>
    public void StepAside(Func<bool> editorOpen)
    {
        var generation = ++_generation;
        Visibility?.Invoke(false);
        Check(generation, editorOpen, seen: false, giveUpAt: DateTime.UtcNow + OpenTimeout);
    }

    private void Check(int generation, Func<bool> editorOpen, bool seen, DateTime giveUpAt)
    {
        if (generation != _generation)
            return; // a newer hand-off took over

        var open = editorOpen();
        if ((seen && !open) || (!seen && !open && DateTime.UtcNow > giveUpAt))
        {
            Visibility?.Invoke(true);
            return;
        }

        _ = framework.RunOnTick(() => Check(generation, editorOpen, seen || open, giveUpAt), CheckEvery);
    }
}
