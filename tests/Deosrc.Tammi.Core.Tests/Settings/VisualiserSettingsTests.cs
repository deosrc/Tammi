using Deosrc.Tammi.Core.Settings;

namespace Deosrc.Tammi.Core.Tests.Settings;

public sealed class VisualiserSettingsTests
{
    [Fact]
    public void WorkspaceVisualisation_DefaultsToPlasma()
    {
        Assert.Equal(WorkspaceVisualisationType.Plasma, new VisualiserSettings().WorkspaceVisualisation);
        Assert.Equal(WorkspaceVisualisationType.Plasma, new AppSettings().Visualiser.WorkspaceVisualisation);
    }

    [Fact]
    public void BurstWhenWorkingFinishes_DefaultsToOn()
    {
        Assert.True(new VisualiserSettings().BurstWhenWorkingFinishes);
    }

    [Fact]
    public void AlwaysOnTop_DefaultsToOn()
    {
        Assert.True(new VisualiserSettings().AlwaysOnTop);
    }

    [Fact]
    public void Normalize_KeepsAlwaysOnTopOff()
    {
        Assert.False(new VisualiserSettings { AlwaysOnTop = false }.Normalize().AlwaysOnTop);
    }

    [Fact]
    public void Normalize_KeepsBurstWhenWorkingFinishesOff()
    {
        Assert.False(new VisualiserSettings { BurstWhenWorkingFinishes = false }.Normalize().BurstWhenWorkingFinishes);
    }

    [Fact]
    public void Normalize_UnknownWorkspaceVisualisation_IsPlasma()
    {
        var settings = new VisualiserSettings { WorkspaceVisualisation = (WorkspaceVisualisationType)42 }.Normalize();

        Assert.Equal(WorkspaceVisualisationType.Plasma, settings.WorkspaceVisualisation);
    }

    [Fact]
    public void Normalize_Glow_IsKept()
    {
        var settings = new VisualiserSettings { WorkspaceVisualisation = WorkspaceVisualisationType.Glow }.Normalize();

        Assert.Equal(WorkspaceVisualisationType.Glow, settings.WorkspaceVisualisation);
    }

    [Fact]
    public void AppSettingsNormalize_NormalizesTheVisualiserSettings()
    {
        var settings = new AppSettings
        {
            Visualiser = new VisualiserSettings { WorkspaceVisualisation = (WorkspaceVisualisationType)42 },
        }.Normalize();

        Assert.Equal(WorkspaceVisualisationType.Plasma, settings.Visualiser.WorkspaceVisualisation);
    }
}
