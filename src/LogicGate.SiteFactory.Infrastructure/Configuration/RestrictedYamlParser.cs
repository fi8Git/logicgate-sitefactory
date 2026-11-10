using YamlDotNet.Core;
using YamlDotNet.Core.Events;

namespace LogicGate.SiteFactory.Infrastructure.Configuration;

// Restricts library-produced events before YamlDotNet builds its representation tree.
internal sealed class RestrictedYamlParser(IParser parser, CancellationToken cancellationToken) : IParser
{
    private readonly Stack<Container> containers = new();
    private int documentCount;
    private int nodeCount;

    public ParsingEvent? Current => parser.Current;

    public bool MoveNext()
    {
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            if (!parser.MoveNext())
                return false;
        }
        catch (InvalidOperationException)
        {
            // YamlDotNet 18.1.0 also uses this exception for malformed flow indentation.
            // Keep the translation at the provider boundary, not around our mapping code.
            throw new ConfigurationYamlException("SF-CONFIG-YAML-004", "La syntaxe du document YAML est invalide.");
        }
        catch (ArgumentOutOfRangeException exception) when (exception.ParamName == "utf32")
        {
            // YamlDotNet 18.1.0 can overflow an eight-digit Unicode escape before
            // calling char.ConvertFromUtf32. Do not absorb unrelated range errors.
            throw new ConfigurationYamlException("SF-CONFIG-YAML-004", "La syntaxe du document YAML est invalide.");
        }

        switch (Current)
        {
            case DocumentStart:
                if (++documentCount > 1)
                    throw new ConfigurationYamlException("SF-CONFIG-YAML-005", "Un seul document YAML est autorisé.");
                break;
            case AnchorAlias:
                throw new ConfigurationYamlException("SF-CONFIG-YAML-007", "Les anchors et aliases YAML sont interdits.");
            case NodeEvent node:
                CheckNode(node);
                break;
            case MappingEnd or SequenceEnd:
                containers.Pop();
                break;
        }

        return true;
    }

    private void CheckNode(NodeEvent node)
    {
        if (!node.Anchor.IsEmpty)
            throw new ConfigurationYamlException("SF-CONFIG-YAML-007", "Les anchors et aliases YAML sont interdits.");

        if (!node.Tag.IsEmpty)
            throw new ConfigurationYamlException("SF-CONFIG-YAML-008", "Les tags YAML sont interdits.");

        if (++nodeCount > YamlConfigurationLoader.MaximumNodeCount)
            throw new ConfigurationYamlException("SF-CONFIG-SCHEMA-006", "Le document dépasse la limite de nœuds YAML.");

        if (containers.TryPeek(out Container? parent) && parent.IsMapping)
        {
            if (parent.ExpectsKey)
            {
                if (node is not Scalar key)
                    throw new ConfigurationYamlException("SF-CONFIG-SCHEMA-005", "Une clé YAML doit être une chaîne simple.");

                if (key.Value == "<<")
                    throw new ConfigurationYamlException("SF-CONFIG-YAML-009", "Les merge keys YAML sont interdites.");

                if (!parent.Keys.Add(key.Value))
                    throw new ConfigurationYamlException("SF-CONFIG-YAML-006", "Une propriété YAML est déclarée plusieurs fois.");
            }

            parent.ExpectsKey = !parent.ExpectsKey;
        }

        if (node is MappingStart or SequenceStart)
        {
            if (containers.Count >= YamlConfigurationLoader.MaximumDepth)
                throw new ConfigurationYamlException("SF-CONFIG-YAML-010", "Le document dépasse la profondeur YAML autorisée.");

            containers.Push(new Container(node is MappingStart));
        }
    }

    private sealed class Container(bool isMapping)
    {
        public bool IsMapping { get; } = isMapping;

        public bool ExpectsKey { get; set; } = true;

        public HashSet<string> Keys { get; } = new(StringComparer.Ordinal);
    }
}
