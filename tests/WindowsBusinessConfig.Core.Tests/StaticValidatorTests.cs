using WindowsBusinessConfig.Core;
using WindowsBusinessConfig.Core.Yaml;
using Xunit;

namespace WindowsBusinessConfig.Core.Tests;

public class StaticValidatorTests
{
    private const string MinimalDoc =
        "$schema: https://example/dsc.json\n" +
        "resources:\n" +
        "  - name: A\n" +
        "    type: Microsoft.Windows/Registry\n" +
        "  - name: B\n" +
        "    type: Microsoft.Windows/Registry\n" +
        "    dependsOn:\n" +
        "      - A\n";

    [Fact]
    public void Minimal_valid_document_has_no_errors()
    {
        var doc = YamlDocumentLoader.LoadFromString(MinimalDoc);
        var issues = StaticValidator.Validate(doc);
        Assert.DoesNotContain(issues, i => i.Severity == IssueSeverity.Error);
    }

    [Fact]
    public void Missing_schema_warns()
    {
        var yaml = "resources:\n  - name: A\n    type: Microsoft.Windows/Registry\n";
        var doc = YamlDocumentLoader.LoadFromString(yaml);
        var issues = StaticValidator.Validate(doc);
        Assert.Contains(issues, i => i.Severity == IssueSeverity.Warning && i.Message.Contains("$schema"));
    }

    [Fact]
    public void Duplicate_names_are_errors()
    {
        var yaml =
            "resources:\n" +
            "  - name: A\n" +
            "    type: Microsoft.Windows/Registry\n" +
            "  - name: A\n" +
            "    type: Microsoft.Windows/Registry\n";
        var doc = YamlDocumentLoader.LoadFromString(yaml);
        var issues = StaticValidator.Validate(doc);
        Assert.Contains(issues, i => i.Severity == IssueSeverity.Error && i.Message.Contains("Duplicate"));
    }

    [Fact]
    public void Dangling_dependsOn_is_an_error()
    {
        var yaml =
            "resources:\n" +
            "  - name: A\n" +
            "    type: Microsoft.Windows/Registry\n" +
            "    dependsOn:\n" +
            "      - Ghost\n";
        var doc = YamlDocumentLoader.LoadFromString(yaml);
        var issues = StaticValidator.Validate(doc);
        Assert.Contains(issues, i => i.Severity == IssueSeverity.Error && i.Message.Contains("Ghost"));
    }

    [Fact]
    public void Missing_type_is_an_error()
    {
        var yaml = "resources:\n  - name: A\n";
        var doc = YamlDocumentLoader.LoadFromString(yaml);
        var issues = StaticValidator.Validate(doc);
        Assert.Contains(issues, i => i.Severity == IssueSeverity.Error && i.Message.Contains("missing a type"));
    }
}
