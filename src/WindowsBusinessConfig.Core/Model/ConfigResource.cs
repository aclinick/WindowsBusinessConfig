using System.Collections.Generic;
using YamlDotNet.RepresentationModel;

namespace WindowsBusinessConfig.Core.Model;

/// <summary>
/// One resource entry from <c>resources:</c> in a DSC v3 document.
/// Carries the original mapping node so callers can mutate scalar
/// fields without rewriting the whole document.
/// </summary>
/// <remarks>
/// Scalar properties are read-only snapshots taken at load. To mutate
/// the document, edit <see cref="Node"/> (or the nested
/// <see cref="Properties"/> / <see cref="Metadata"/> mappings)
/// directly so the changes survive a save.
/// </remarks>
public sealed class ConfigResource
{
    public required YamlMappingNode Node { get; init; }

    public string? Name { get; init; }

    public string? Type { get; init; }

    public YamlMappingNode? Properties { get; init; }

    public YamlMappingNode? Metadata { get; init; }

    public List<string> DependsOn { get; } = new();

    public ResourceCategory Category { get; init; } = ResourceCategory.Other;

    public string? FriendlyDescription { get; init; }
}

public enum ResourceCategory
{
    Other,
    Appearance,
    Explorer,
    Taskbar,
    StartAndSearch,
    Edge,
    Privacy,
    BloatRemoval,
    Power,
    Security,
    Apps,
    InstallMarker,
}

