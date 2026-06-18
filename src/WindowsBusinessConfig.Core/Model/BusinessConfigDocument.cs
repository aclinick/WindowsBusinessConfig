using System.Collections.Generic;
using YamlDotNet.RepresentationModel;

namespace WindowsBusinessConfig.Core.Model;

/// <summary>
/// Strongly-typed projection of a DSC v3 configuration document
/// (as consumed by <c>winget configure</c>). The original YAML node tree
/// is kept on <see cref="Root"/> so edits can be written back without
/// losing comments, ordering, or unknown fields.
/// </summary>
/// <remarks>
/// The scalar properties are read-only snapshots taken at load time.
/// To mutate the document, edit <see cref="Root"/> (or a nested
/// <see cref="YamlMappingNode"/> on a <see cref="ConfigResource"/>)
/// directly. <see cref="Yaml.YamlDocumentLoader.Save"/> serializes only
/// the YAML node tree, so changes made through these mirror properties
/// would otherwise be silently dropped.
/// </remarks>
public sealed class BusinessConfigDocument
{
    public required YamlMappingNode Root { get; init; }

    public string? Schema { get; init; }

    public List<ConfigResource> Resources { get; } = new();

    public YamlMappingNode? Metadata { get; init; }

    public YamlMappingNode? Parameters { get; init; }

    public YamlMappingNode? Variables { get; init; }
}

