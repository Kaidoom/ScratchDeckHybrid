namespace Scratchdeck.Services;

public sealed class ScratchUndoHistory
{
    public const int MaximumActionsPerTab = 20;

    private readonly Dictionary<Guid, List<string>> _history = [];

    public void Record(Guid tabId, string snapshot)
    {
        if (!_history.TryGetValue(tabId, out var actions))
        {
            actions = [];
            _history[tabId] = actions;
        }

        actions.Add(snapshot ?? string.Empty);
        if (actions.Count > MaximumActionsPerTab)
        {
            actions.RemoveAt(0);
        }
    }

    public bool TryPop(Guid tabId, out string snapshot)
    {
        if (!_history.TryGetValue(tabId, out var actions) || actions.Count == 0)
        {
            snapshot = string.Empty;
            return false;
        }

        var index = actions.Count - 1;
        snapshot = actions[index];
        actions.RemoveAt(index);
        if (actions.Count == 0)
        {
            _history.Remove(tabId);
        }
        return true;
    }

    public void Forget(Guid tabId) => _history.Remove(tabId);
}
