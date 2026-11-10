using System.Globalization;
using System.Text.RegularExpressions;
using LogicGate.SiteFactory.Application.Configuration;
using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;

namespace LogicGate.SiteFactory.Infrastructure.Configuration;

// Only the known V1 shape is mapped; no runtime type resolution or arbitrary property bag.
internal static partial class ClientConfigurationYamlReader
{
    public static ClientConfigurationV1 Read(YamlNode node)
    {
        YamlMappingNode root = ReadMapping(node, null, "schemaVersion", "client", "site", "modules", "deployment", "git");
        int schemaVersion = ReadVersion(root);
        YamlMappingNode client = ReadMapping(Required(root, "client", "client"), "client", "slug", "displayName");
        YamlMappingNode site = ReadMapping(Required(root, "site", "site"), "site", "product", "template", "defaultLocale", "publicBaseUrl");
        YamlMappingNode modules = ReadMapping(Required(root, "modules", "modules"), "modules", "enabled");
        YamlMappingNode deployment = ReadMapping(Required(root, "deployment", "deployment"), "deployment", "profile");
        YamlMappingNode git = ReadMapping(Required(root, "git", "git"), "git", "initializeLocalRepository", "publishRemote");

        return new ClientConfigurationV1(
            schemaVersion,
            new ClientIdentityConfiguration(
                ReadString(Required(client, "slug", "client.slug"), "client.slug"),
                ReadString(Required(client, "displayName", "client.displayName"), "client.displayName")),
            new SiteConfiguration(
                ReadString(Required(site, "product", "site.product"), "site.product"),
                ReadString(Required(site, "template", "site.template"), "site.template"),
                ReadString(Required(site, "defaultLocale", "site.defaultLocale"), "site.defaultLocale"),
                ReadString(Required(site, "publicBaseUrl", "site.publicBaseUrl"), "site.publicBaseUrl")),
            ReadModules(Required(modules, "enabled", "modules.enabled")),
            new DeploymentConfiguration(ReadString(Required(deployment, "profile", "deployment.profile"), "deployment.profile")),
            new GitConfiguration(
                ReadBoolean(Required(git, "initializeLocalRepository", "git.initializeLocalRepository"), "git.initializeLocalRepository"),
                ReadBoolean(Required(git, "publishRemote", "git.publishRemote"), "git.publishRemote")));
    }

    private static YamlMappingNode ReadMapping(YamlNode node, string? path, params string[] properties)
    {
        if (node is not YamlMappingNode mapping)
            throw InvalidType(path);

        foreach (YamlNode key in mapping.Children.Keys)
        {
            if (key is not YamlScalarNode scalar || !properties.Contains(scalar.Value, StringComparer.Ordinal))
                throw new ConfigurationYamlException("SF-CONFIG-SCHEMA-004", "Une propriété inconnue est présente dans la configuration.", path);
        }

        return mapping;
    }

    private static YamlNode Required(YamlMappingNode mapping, string key, string path)
    {
        if (!mapping.Children.TryGetValue(new YamlScalarNode(key), out YamlNode? node))
            throw new ConfigurationYamlException(
                path == "schemaVersion" ? "SF-CONFIG-SCHEMA-001" : "SF-CONFIG-SCHEMA-003",
                "Une propriété obligatoire est absente.", path);

        return node;
    }

    private static string ReadString(YamlNode node, string path)
    {
        if (node is not YamlScalarNode { Value: not null } scalar)
            throw InvalidType(path);

        if (scalar.Style == ScalarStyle.Plain &&
            (scalar.Value.Length == 0 || scalar.Value == "~" || scalar.Value.Equals("null", StringComparison.OrdinalIgnoreCase) ||
                bool.TryParse(scalar.Value, out _) || NumberOrDateLikeScalar().IsMatch(scalar.Value)))
            throw InvalidType(path);

        return scalar.Value;
    }

    // YAML 1.2 core numbers, plus date/timestamp-shaped text required to be explicit by ADR-0002 §23.6.
    // Lexical matching avoids overflow, culture-dependent conversions and calendar validation.
    // Quoted and block string styles never reach this check (ADR-0002 §24).
    [GeneratedRegex(
        @"\A(?:[-+]?(?:[0-9]+(?:\.[0-9]*)?|\.[0-9]+)(?:[eE][-+]?[0-9]+)?|" +
        @"0o[0-7]+|0x[0-9a-fA-F]+|[-+]?\.(?:inf|Inf|INF)|\.(?:nan|NaN|NAN)|" +
        @"[0-9]{4}-[0-9]{1,2}-[0-9]{1,2}" +
        @"(?:(?:[Tt]|[ \t]+)[0-9]{1,2}:[0-9]{2}:[0-9]{2}(?:\.[0-9]+)?" +
        @"(?:[ \t]*(?:[Zz]|[-+][0-9]{1,2}(?::[0-9]{2})?))?)?)\z",
        RegexOptions.CultureInvariant | RegexOptions.NonBacktracking)]
    private static partial Regex NumberOrDateLikeScalar();

    private static int ReadVersion(YamlMappingNode root)
    {
        YamlNode node = Required(root, "schemaVersion", "schemaVersion");

        if (node is not YamlScalarNode { Style: ScalarStyle.Plain } scalar ||
            !int.TryParse(scalar.Value, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out int version))
            throw InvalidType("schemaVersion");

        if (version != 1)
            throw new ConfigurationYamlException("SF-CONFIG-SCHEMA-002", "Seule la version 1 du schéma est prise en charge.", "schemaVersion");

        return version;
    }

    private static bool ReadBoolean(YamlNode node, string path)
    {
        if (node is YamlScalarNode { Style: ScalarStyle.Plain, Value: "true" or "false" } scalar)
            return scalar.Value == "true";

        throw InvalidType(path);
    }

    private static ModulesConfiguration ReadModules(YamlNode node)
    {
        if (node is not YamlSequenceNode sequence)
            throw InvalidType("modules.enabled");

        string[] enabled = new string[sequence.Children.Count];

        for (int index = 0; index < enabled.Length; index++)
            enabled[index] = ReadString(sequence.Children[index], $"modules.enabled[{index.ToString(CultureInfo.InvariantCulture)}]");

        return new ModulesConfiguration(enabled);
    }

    private static ConfigurationYamlException InvalidType(string? path) =>
        new("SF-CONFIG-SCHEMA-005", "Le type de valeur ne correspond pas au contrat de configuration V1.", path);
}
