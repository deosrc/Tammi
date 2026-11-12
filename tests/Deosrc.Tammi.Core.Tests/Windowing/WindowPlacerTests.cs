using Deosrc.Tammi.Core.Windowing;

namespace Deosrc.Tammi.Core.Tests.Windowing;

public sealed class WindowPlacerTests
{
    private static readonly ScreenRect Left = new(0, 0, 1920, 1080);
    private static readonly ScreenRect Right = new(1920, 0, 1920, 1080);
    private static readonly ScreenRect Main = new(100, 100, 1280, 800);

    private static WindowPlacement Place(VisualiserWindowState? saved, params ScreenRect[] screens) =>
        WindowPlacer.Place(saved, 900, 700, screens.Length == 0 ? [Left] : screens, Main);

    [Fact]
    public void WithoutSavedState_IsTheDefaultSizeCentredOverTheMainWindow()
    {
        var placement = Place(saved: null);

        Assert.Equal(new ScreenRect(290, 150, 900, 700), placement.Bounds);
        Assert.False(placement.IsMaximized);
    }

    [Fact]
    public void WithAPositionOnAScreen_IsRestoredAsItWas()
    {
        var placement = Place(new VisualiserWindowState(1000, 600, 300, 200, IsMaximized: false));

        Assert.Equal(new ScreenRect(300, 200, 1000, 600), placement.Bounds);
    }

    [Fact]
    public void WhenItWasMaximised_OpensMaximisedWithItsNormalBounds()
    {
        var placement = Place(new VisualiserWindowState(1000, 600, 300, 200, IsMaximized: true));

        Assert.True(placement.IsMaximized);
        Assert.Equal(new ScreenRect(300, 200, 1000, 600), placement.Bounds);
    }

    [Fact]
    public void OnASecondScreen_IsRestoredThere()
    {
        var placement = Place(new VisualiserWindowState(800, 600, 2500, 100, IsMaximized: false), Left, Right);

        Assert.Equal(new ScreenRect(2500, 100, 800, 600), placement.Bounds);
    }

    [Fact]
    public void WhenItsScreenIsGone_IsCentredOverTheMainWindowAtItsSavedSize()
    {
        var placement = Place(new VisualiserWindowState(800, 600, 2500, 100, IsMaximized: false), Left);

        Assert.Equal(new ScreenRect(340, 200, 800, 600), placement.Bounds);
    }

    [Fact]
    public void WithNoSavedPosition_IsCentredOverTheMainWindowAtItsSavedSize()
    {
        var placement = Place(new VisualiserWindowState(800, 600, X: null, Y: null, IsMaximized: false));

        Assert.Equal(new ScreenRect(340, 200, 800, 600), placement.Bounds);
    }

    [Fact]
    public void WhenItWouldGoOffTheScreen_ReturnsToTheDefaultSizeWhereItWas()
    {
        var placement = Place(new VisualiserWindowState(1800, 1000, 500, 300, IsMaximized: false));

        Assert.Equal(new ScreenRect(500, 300, 900, 700), placement.Bounds);
    }

    [Fact]
    public void WhenEvenTheDefaultSizeWouldGoOffTheScreen_IsCentredOverTheMainWindow()
    {
        var placement = Place(new VisualiserWindowState(1800, 1000, 1500, 600, IsMaximized: false));

        Assert.Equal(new ScreenRect(290, 150, 900, 700), placement.Bounds);
    }

    [Fact]
    public void WhenCentredButTooBig_ReturnsToTheDefaultSizeCentredOverTheMainWindow()
    {
        // Centred over the main window, 2200 wide would go past the screen's right edge.
        var placement = Place(new VisualiserWindowState(2200, 600, 5000, 100, IsMaximized: false));

        Assert.Equal(new ScreenRect(290, 150, 900, 700), placement.Bounds);
    }

    [Fact]
    public void WhenTheScreenIsSmallerThanTheDefaultSize_IsMovedSoAsMuchAsPossibleIsOnIt()
    {
        var placement = WindowPlacer.Place(null, 900, 700, [new ScreenRect(0, 0, 800, 600)], new ScreenRect(0, 0, 800, 600));

        Assert.Equal(new ScreenRect(0, 0, 900, 700), placement.Bounds);
    }
}
