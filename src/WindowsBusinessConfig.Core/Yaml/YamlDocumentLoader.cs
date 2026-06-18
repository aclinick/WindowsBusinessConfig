using System;
using System.Collections.Generic;
using System.IO;
using WindowsBusinessConfig.Core.Model;
using YamlDotNet.RepresentationModel;

namespace WindowsBusinessConfig.Core.Yaml;

/// <summary>
/// Loads and saves <see cref="BusinessConfigDocument"/> instances by
/// holding onto the underlying <see cref="YamlStream"/>. Round-tripping
/// through YamlDotNet's representation model preserves key ordering and
/// is the closest thing the library provides to a lossless edit.
/// Inline comments survive because emit reads them off the original
/// nodes; comment-only lines between resources are lost (a known
/// YamlDotNet limitation, documented in the README).
/// </summary>
public static class YamlDocumentLoader
{
    public static BusinessConfigDocument Load(string path)
    {
        using var reader = new StreamReader(path);
        return LoadFromReader(reader);
    }

    public static BusinessConfigDocument LoadFromString(string yaml)
    {
        using var reader = new StringReader(yaml);
        return LoadFromReader(reader);
    }

    private static BusinessConfigDocument LoadFromReader(TextReader reader)
    {
        var stream = new YamlStream();
        stream.Load(reader);

        if (stream.Documents.Count == 0)
        {
            throw new InvalidDataException("YAML stream contained no documents.");
        }

        if (stream.Documents[0].RootNode is not YamlMappingNode root)
        {
            throw new InvalidDataException("Document root must be a mapping.");
        }

        TryGetScalar(root, "$schema", out var schema);
        TryGetMappingOrNull(root, "metadata",   out var metadata);
        TryGetMappingOrNull(root, "parameters", out var parameters);
        TryGetMappingOrNull(root, "variables",  out var variables);

        var doc = new BusinessConfigDocument
        {
            Root       = root,
            Schema     = string.IsNullOrEmpty(schema) ? null : schema,
            Metadata   = metadata,
            Parameters = parameters,
            Variables  = variables,
        };

        if (TryGetSequence(root, "resources", out var resources))
        {
            foreach (var item in resources)
            {
                if (item is YamlMappingNode resourceNode)
                {
                    doc.Resources.Add(BuildResource(resourceNode));
                }
            }
        }

        return doc;
    }

    public static void Save(BusinessConfigDocument doc, string path)
    {
        using var writer = new StreamWriter(path);
        Save(doc, writer);
    }

    public static string SaveToString(BusinessConfigDocument doc)
    {
        using var writer = new StringWriter();
        Save(doc, writer);
        return writer.ToString();
    }

    private static void Save(BusinessConfigDocument doc, TextWriter writer)
    {
        var stream = new YamlStream(new YamlDocument(doc.Root));
        stream.Save(writer, assignAnchors: false);
    }

    private static ConfigResource BuildResource(YamlMappingNode node)
    {
        TryGetScalar(node, "name", out var name);
        TryGetScalar(node, "type", out var type);
        TryGetMappingOrNull(node, "properties", out var props);
        TryGetMappingOrNull(node, "metadata",   out var meta);

        var typeStr = string.IsNullOrEmpty(type) ? null : type;
        var nameStr = string.IsNullOrEmpty(name) ? null : name;

        var resource = new ConfigResource
        {
            Node                = node,
            Name                = nameStr,
            Type                = typeStr,
            Properties          = props,
            Metadata            = meta,
            Category            = ResourceCatalog.CategorizeByType(typeStr, nameStr),
            FriendlyDescription = ResourceCatalog.DescribeBy(typeStr, nameStr),
        };

        if (TryGetSequence(node, "dependsOn", out var depends))
        {
            foreach (var dep in depends)
            {
                if (dep is YamlScalarNode scalar && scalar.Value is not null)
                {
                    resource.DependsOn.Add(scalar.Value);
                }
            }
        }

        return resource;
    }

    private static bool TryGetMappingOrNull(YamlMappingNode map, string key, out YamlMappingNode? value)
    {
        if (map.Children.TryGetValue(new YamlScalarNode(key), out var node) &&
            node is YamlMappingNode mapping)
        {
            value = mapping;
            return true;
        }

        value = null;
        return false;
    }

    private static bool TryGetScalar(YamlMappingNode map, string key, out string value)
    {
        if (map.Children.TryGetValue(new YamlScalarNode(key), out var node) &&
            node is YamlScalarNode scalar &&
            scalar.Value is not null)
        {
            value = scalar.Value;
            return true;
        }

        value = string.Empty;
        return false;
    }

    private static bool TryGetMapping(YamlMappingNode map, string key, out YamlMappingNode value)
    {
        if (map.Children.TryGetValue(new YamlScalarNode(key), out var node) &&
            node is YamlMappingNode mapping)
        {
            value = mapping;
            return true;
        }

        // Empty mapping is a sentinel; callers must check the bool, not the
        // returned node. Mutating the returned value will NOT affect the
        // document.
        value = new YamlMappingNode();
        return false;
    }

    private static bool TryGetSequence(YamlMappingNode map, string key, out YamlSequenceNode value)
    {
        if (map.Children.TryGetValue(new YamlScalarNode(key), out var node) &&
            node is YamlSequenceNode sequence)
        {
            value = sequence;
            return true;
        }

        value = new YamlSequenceNode();
        return false;
    }
}
