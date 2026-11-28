using System.Text.Json;
using Deosrc.Tammi.Core.Settings;

namespace Deosrc.Tammi.Core.Tests.Settings;

public class GeneralSettingsSlimWindowTests
{
    [Fact]
    public void SlimWindow_defaults_to_on() =>
        Assert.True(new GeneralSettings().SlimWindow);

    [Fact]
    public void SlimWindow_is_on_when_the_settings_file_predates_it()
    {
        var settings = JsonSerializer.Deserialize<GeneralSettings>("""{"Theme":"Dark"}""", JsonSerializerOptions.Web);
        Assert.True(settings!.SlimWindow);
    }

    [Fact]
    public void SlimWindow_is_read_when_turned_off()
    {
        var settings = JsonSerializer.Deserialize<GeneralSettings>("""{"SlimWindow":false}""", JsonSerializerOptions.Web);
        Assert.False(settings!.SlimWindow);
    }

    [Fact]
    public void Settings_differing_only_in_SlimWindow_are_not_equal() =>
        Assert.NotEqual(new GeneralSettings(), new GeneralSettings { SlimWindow = false });
}
