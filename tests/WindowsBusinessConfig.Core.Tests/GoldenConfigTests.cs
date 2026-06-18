using System.IO;
using System.Linq;
using WindowsBusinessConfig.Core;
using WindowsBusinessConfig.Core.Model;
using WindowsBusinessConfig.Core.Yaml;
using Xunit;

namespace WindowsBusinessConfig.Core.Tests;

public class GoldenConfigTests
{
    private const string GoldenPath = @"..\..\..\..\..\business-config\business-config.winget";

    [Fact]
    public void Loads_canonical_business_config()
    {
        var path = Path.GetFullPath(GoldenPath);
        Assert.True(File.Exists(path), $"Golden file missing: {path}");

        var doc = YamlDocumentLoader.Load(path);

        Assert.NotNull(doc.Schema);
        Assert.Contains("dsc", doc.Schema!, System.StringComparison.OrdinalIgnoreCase);
        Assert.True(doc.Resources.Count >= 30, $"Expected ~38 resources, got {doc.Resources.Count}");
        Assert.All(doc.Resources, r => Assert.False(string.IsNullOrWhiteSpace(r.Type)));
        Assert.All(doc.Resources, r => Assert.False(string.IsNullOrWhiteSpace(r.Name)));
    }

    [Fact]
    public void Round_trips_resource_names_and_types()
    {
        var path = Path.GetFullPath(GoldenPath);
        var original = YamlDocumentLoader.Load(path);
        var yaml = YamlDocumentLoader.SaveToString(original);
        var roundtripped = YamlDocumentLoader.LoadFromString(yaml);

        Assert.Equal(original.Resources.Count, roundtripped.Resources.Count);
        var origNames = original.Resources.Select(r => r.Name).ToArray();
        var rtNames = roundtripped.Resources.Select(r => r.Name).ToArray();
        Assert.Equal(origNames, rtNames);
        var origTypes = original.Resources.Select(r => r.Type).ToArray();
        var rtTypes = roundtripped.Resources.Select(r => r.Type).ToArray();
        Assert.Equal(origTypes, rtTypes);
    }

    [Fact]
    public void Static_validator_passes_on_canonical_config()
    {
        var path = Path.GetFullPath(GoldenPath);
        var doc = YamlDocumentLoader.Load(path);
        var issues = StaticValidator.Validate(doc);

        var errors = issues.Where(i => i.Severity == IssueSeverity.Error).ToArray();
        Assert.True(errors.Length == 0, "Unexpected errors: " + string.Join("; ", errors.Select(e => e.Message)));
    }

    [Fact]
    public void Install_marker_is_present()
    {
        var path = Path.GetFullPath(GoldenPath);
        var doc = YamlDocumentLoader.Load(path);
        Assert.Contains(doc.Resources, r => r.Category == ResourceCategory.InstallMarker);
    }

    [Fact]
    public void App_resources_use_winget_package_type()
    {
        var path = Path.GetFullPath(GoldenPath);
        var doc = YamlDocumentLoader.Load(path);
        var apps = doc.Resources.Where(r => r.Category == ResourceCategory.Apps).ToArray();
        Assert.True(apps.Length >= 5, $"Expected >=5 app installs, got {apps.Length}");
        Assert.All(apps, a => Assert.StartsWith("Microsoft.WinGet/Package", a.Type, System.StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Mutating_resource_node_survives_round_trip()
    {
        var path = Path.GetFullPath(GoldenPath);
        var doc = YamlDocumentLoader.Load(path);

        var terminal = doc.Resources.Single(r => r.Name == "Terminal");
        terminal.Node.Children[new YamlDotNet.RepresentationModel.YamlScalarNode("name")] =
            new YamlDotNet.RepresentationModel.YamlScalarNode("Terminal_renamed");

        var yaml = YamlDocumentLoader.SaveToString(doc);
        var reloaded = YamlDocumentLoader.LoadFromString(yaml);

        Assert.DoesNotContain(reloaded.Resources, r => r.Name == "Terminal");
        Assert.Contains(reloaded.Resources, r => r.Name == "Terminal_renamed");
    }
}
