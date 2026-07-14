using Scratchdeck.Services;

namespace Scratchdeck.Tests;

public sealed class ScratchUndoHistoryTests
{
    [Fact]
    public void TryPop_ReturnsSnapshotsInReverseActionOrderPerTab()
    {
        var history = new ScratchUndoHistory();
        var firstTab = Guid.NewGuid();
        var secondTab = Guid.NewGuid();
        history.Record(firstTab, "before-draw");
        history.Record(firstTab, "before-clear");
        history.Record(secondTab, "other-tab");

        Assert.True(history.TryPop(firstTab, out var latest));
        Assert.Equal("before-clear", latest);
        Assert.True(history.TryPop(firstTab, out var earlier));
        Assert.Equal("before-draw", earlier);
        Assert.False(history.TryPop(firstTab, out _));
        Assert.True(history.TryPop(secondTab, out var other));
        Assert.Equal("other-tab", other);
    }

    [Fact]
    public void Record_CapsHistoryAndForgetDropsTabState()
    {
        var history = new ScratchUndoHistory();
        var tabId = Guid.NewGuid();
        for (var index = 0; index < ScratchUndoHistory.MaximumActionsPerTab + 3; index++)
        {
            history.Record(tabId, index.ToString());
        }

        var restored = new List<string>();
        while (history.TryPop(tabId, out var snapshot))
        {
            restored.Add(snapshot);
        }

        Assert.Equal(ScratchUndoHistory.MaximumActionsPerTab, restored.Count);
        Assert.Equal((ScratchUndoHistory.MaximumActionsPerTab + 2).ToString(), restored[0]);

        history.Record(tabId, "discarded");
        history.Forget(tabId);
        Assert.False(history.TryPop(tabId, out _));
    }
}
