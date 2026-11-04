using Deosrc.Tammi.Core.Windowing;

namespace Deosrc.Tammi.Core.Tests.Windowing;

public class WindowStateTrackerTests
{
    private static readonly WindowState Initial = new(1280, 800, IsMaximized: false);

    [Fact]
    public void Current_StartsWithInitialState()
    {
        var tracker = new WindowStateTracker(Initial);

        Assert.Equal(Initial, tracker.Current);
    }

    [Fact]
    public void Observe_Normal_RecordsSize()
    {
        var tracker = new WindowStateTracker(Initial);

        tracker.Observe(1000, 700, WindowMode.Normal);

        Assert.Equal(new WindowState(1000, 700, IsMaximized: false), tracker.Current);
    }

    [Fact]
    public void Observe_Maximized_KeepsSizeToRestore()
    {
        var tracker = new WindowStateTracker(Initial);

        tracker.Observe(1920, 1080, WindowMode.Maximized);

        Assert.Equal(new WindowState(1280, 800, IsMaximized: true), tracker.Current);
    }

    [Fact]
    public void Observe_MaximizedSizeReportedBeforeMaximized_RestoresEarlierSize()
    {
        var tracker = new WindowStateTracker(Initial);

        // Some platforms report the new size first, while the window still looks normal.
        tracker.Observe(1920, 1080, WindowMode.Normal);
        tracker.Observe(1920, 1080, WindowMode.Maximized);

        Assert.Equal(new WindowState(1280, 800, IsMaximized: true), tracker.Current);
    }

    [Fact]
    public void Observe_RestoredAfterMaximized_ClearsMaximized()
    {
        var tracker = new WindowStateTracker(Initial);
        tracker.Observe(1920, 1080, WindowMode.Maximized);

        tracker.Observe(1280, 800, WindowMode.Normal);

        Assert.Equal(Initial, tracker.Current);
    }

    [Fact]
    public void Observe_RestoreReportedAtMaximizedSizeFirst_EndsAtRestoredSize()
    {
        var tracker = new WindowStateTracker(Initial);
        tracker.Observe(1920, 1080, WindowMode.Maximized);

        // The restore can be reported before the window shrinks back.
        tracker.Observe(1920, 1080, WindowMode.Normal);
        tracker.Observe(1280, 800, WindowMode.Normal);

        Assert.Equal(Initial, tracker.Current);
    }

    [Fact]
    public void Observe_MaximizedAgainAfterResizing_KeepsNewSize()
    {
        var tracker = new WindowStateTracker(Initial);
        tracker.Observe(1920, 1080, WindowMode.Maximized);
        tracker.Observe(1280, 800, WindowMode.Normal);
        tracker.Observe(1100, 750, WindowMode.Normal);

        tracker.Observe(1920, 1080, WindowMode.Maximized);

        Assert.Equal(new WindowState(1100, 750, IsMaximized: true), tracker.Current);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Observe_Minimized_KeepsStateFromBefore(bool maximized)
    {
        var tracker = new WindowStateTracker(Initial);
        tracker.Observe(maximized ? 1920 : 1000, maximized ? 1080 : 700,
            maximized ? WindowMode.Maximized : WindowMode.Normal);
        var before = tracker.Current;

        tracker.Observe(0, 0, WindowMode.Minimized);

        Assert.Equal(before, tracker.Current);
    }

    [Fact]
    public void Observe_EmptySize_IsIgnored()
    {
        var tracker = new WindowStateTracker(Initial);

        tracker.Observe(0, 0, WindowMode.Normal);

        Assert.Equal(Initial, tracker.Current);
    }
}
