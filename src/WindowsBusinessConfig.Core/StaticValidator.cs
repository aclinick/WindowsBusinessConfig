using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using WindowsBusinessConfig.Core.Model;
using WindowsBusinessConfig.Core.Yaml;

namespace WindowsBusinessConfig.Core;

/// <summary>
/// Static-checks a <see cref="BusinessConfigDocument"/> without invoking
/// winget. Catches the common authoring mistakes (missing required
/// fields, duplicate names, dangling dependsOn references) before
/// shelling out. <c>winget configure validate</c> remains the
/// authoritative check.
/// </summary>
public static class StaticValidator
{
    public static IReadOnlyList<ValidationIssue> Validate(BusinessConfigDocument doc)
    {
        var issues = new List<ValidationIssue>();

        if (string.IsNullOrWhiteSpace(doc.Schema))
        {
            issues.Add(new ValidationIssue(IssueSeverity.Warning, "Document is missing a $schema entry."));
        }

        if (doc.Resources.Count == 0)
        {
            issues.Add(new ValidationIssue(IssueSeverity.Error, "Document declares no resources."));
            return issues;
        }

        var seenNames = new HashSet<string>(StringComparer.Ordinal);
        foreach (var resource in doc.Resources)
        {
            if (string.IsNullOrWhiteSpace(resource.Name))
            {
                issues.Add(new ValidationIssue(IssueSeverity.Error, "Resource is missing a name."));
                continue;
            }

            if (string.IsNullOrWhiteSpace(resource.Type))
            {
                issues.Add(new ValidationIssue(IssueSeverity.Error, $"Resource '{resource.Name}' is missing a type."));
            }

            if (!seenNames.Add(resource.Name))
            {
                issues.Add(new ValidationIssue(IssueSeverity.Error, $"Duplicate resource name '{resource.Name}'."));
            }
        }

        var allNames = doc.Resources
            .Where(r => r.Name is not null)
            .Select(r => r.Name!)
            .ToHashSet(StringComparer.Ordinal);

        foreach (var resource in doc.Resources)
        {
            foreach (var dep in resource.DependsOn)
            {
                if (!allNames.Contains(dep))
                {
                    issues.Add(new ValidationIssue(
                        IssueSeverity.Error,
                        $"Resource '{resource.Name}' depends on '{dep}', which does not exist."));
                }
            }
        }

        return issues;
    }
}

public enum IssueSeverity { Info, Warning, Error }

public sealed record ValidationIssue(IssueSeverity Severity, string Message);
