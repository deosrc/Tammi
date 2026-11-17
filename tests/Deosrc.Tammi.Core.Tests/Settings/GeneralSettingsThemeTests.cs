using System.Text.Json;
using Deosrc.Tammi.Core.Settings;

namespace Deosrc.Tammi.Core.Tests.Settings;

public class GeneralSettingsThemeTests
{
    [Fact]
    public void Theme_defaults_to_auto() =>
        Assert.Equal(ThemePreference.Auto, new GeneralSettings().Theme);

    [Fact]
    public void Normalize_replaces_an_unknown_theme_with_auto() =>
        Assert.Equal(ThemePreference.Auto, (new GeneralSettings { Theme = (ThemePreference)99 }).Normalize().Theme);

    [Fact]
    public void Theme_is_read_from_its_name()
    {
        var settings = JsonSerializer.Deserialize<GeneralSettings>("""{"Theme":"Dark"}""", JsonSerializerOptions.Web);
        Assert.Equal(ThemePreference.Dark, settings!.Theme);
    }
}
