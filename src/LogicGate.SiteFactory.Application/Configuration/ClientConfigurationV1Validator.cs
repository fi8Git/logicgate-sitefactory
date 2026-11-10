using System.Globalization;

namespace LogicGate.SiteFactory.Application.Configuration;

/// <summary>Pure validation of the initial V1 configuration, without catalogue resolution or side effects.</summary>
public static class ClientConfigurationV1Validator
{
    private const int MinimumSlugLength = 3;
    private const int MaximumIdentifierLength = 80;
    private const string SupportedDeploymentProfile = "docker-compose-vps";

    public static ConfigurationValidationResult Validate(ClientConfigurationV1 configuration,
        IConfigurationSelectionValidator? selectionValidator = null)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        List<ConfigurationDiagnostic> diagnostics = [];

        if (configuration.SchemaVersion != 1)
            AddError(diagnostics, "SF-CONFIG-SCHEMA-002", "schemaVersion", "Seule la version 1 du schéma est prise en charge.");

        ValidateClient(configuration.Client, diagnostics);
        ValidateSite(configuration.Site, configuration.Deployment, diagnostics);
        if (selectionValidator is not null)
            diagnostics.AddRange(selectionValidator.ValidateSelection(configuration.Site));
        ValidateModules(configuration.Modules, diagnostics);

        if (Require(configuration.Deployment.Profile, "deployment.profile", diagnostics) &&
            configuration.Deployment.Profile != SupportedDeploymentProfile)
            AddError(diagnostics, "SF-CONFIG-SEMANTIC-008", "deployment.profile", "Le seul profil pris en charge est docker-compose-vps.");

        // ADR-0013: the initial baseline is local; publishing is a separate, explicitly approved operation.
        if (!configuration.Git.InitializeLocalRepository)
            AddError(diagnostics, "SF-CONFIG-SEMANTIC-014", "git.initializeLocalRepository", "L'initialisation du repository Git local doit être activée pour la configuration V1 initiale.");

        if (configuration.Git.PublishRemote)
            AddError(diagnostics, "SF-CONFIG-SEMANTIC-015", "git.publishRemote", "La publication distante doit rester désactivée dans la configuration ; elle exige une opération séparée et explicitement confirmée.");

        return new ConfigurationValidationResult(diagnostics);
    }

    private static void ValidateClient(ClientIdentityConfiguration client, List<ConfigurationDiagnostic> diagnostics)
    {
        if (Require(client.Slug, "client.slug", diagnostics) &&
            (!IsIdentifier(client.Slug, MinimumSlugLength) || IsReservedSlug(client.Slug)))
            AddError(diagnostics, "SF-CONFIG-SEMANTIC-001", "client.slug", "Le slug doit contenir de 3 à 80 caractères ASCII en kebab-case, sans nom réservé.");

        Require(client.DisplayName, "client.displayName", diagnostics);
    }

    private static void ValidateSite(SiteConfiguration site, DeploymentConfiguration deployment, List<ConfigurationDiagnostic> diagnostics)
    {
        Require(site.Product, "site.product", diagnostics);
        Require(site.Template, "site.template", diagnostics);

        if (Require(site.DefaultLocale, "site.defaultLocale", diagnostics) && !IsLocaleFormat(site.DefaultLocale))
            AddError(diagnostics, "SF-CONFIG-SEMANTIC-010", "site.defaultLocale", "La locale doit suivre la forme langue[-écriture][-région], par exemple fr-FR, zh-Hant-TW ou es-419.");

        if (!Require(site.PublicBaseUrl, "site.publicBaseUrl", diagnostics))
            return;

        // System.Uri tolerates surrounding whitespace; configuration must not be silently repaired.
        if (site.PublicBaseUrl.Any(static c => char.IsWhiteSpace(c) || char.IsControl(c)) ||
            !Uri.TryCreate(site.PublicBaseUrl, UriKind.Absolute, out Uri? uri) ||
            !uri.IsWellFormedOriginalString() || string.IsNullOrEmpty(uri.Host) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps) ||
            uri.UserInfo.Length != 0)
        {
            AddError(diagnostics, "SF-CONFIG-SEMANTIC-011", "site.publicBaseUrl", "L'URL publique doit être une URI HTTP ou HTTPS absolue, bien formée, avec un hôte et sans identifiants intégrés.");
        }
        else if (deployment.Profile == SupportedDeploymentProfile && uri.Scheme != Uri.UriSchemeHttps)
        {
            AddError(diagnostics, "SF-CONFIG-SEMANTIC-012", "site.publicBaseUrl", "Le profil docker-compose-vps exige une URL publique HTTPS.");
        }
    }

    private static void ValidateModules(ModulesConfiguration modules, List<ConfigurationDiagnostic> diagnostics)
    {
        HashSet<string> seen = new(StringComparer.Ordinal);

        for (int index = 0; index < modules.Enabled.Count; index++)
        {
            string module = modules.Enabled[index];
            string path = $"modules.enabled[{index.ToString(CultureInfo.InvariantCulture)}]";

            if (Require(module, path, diagnostics) && !IsIdentifier(module, 1))
                AddError(diagnostics, "SF-CONFIG-SEMANTIC-001", path, "L'identifiant de module doit contenir de 1 à 80 caractères ASCII en kebab-case.");

            if (!seen.Add(module))
                AddError(diagnostics, "SF-CONFIG-SEMANTIC-013", path, "Ce module est déjà déclaré dans modules.enabled.");
        }
    }

    private static bool Require(string value, string path, List<ConfigurationDiagnostic> diagnostics)
    {
        if (!string.IsNullOrWhiteSpace(value))
            return true;

        AddError(diagnostics, "SF-CONFIG-SCHEMA-003", path, "Une valeur non vide est obligatoire.");
        return false;
    }

    private static bool IsIdentifier(string value, int minimumLength)
    {
        if (value.Length < minimumLength || value.Length > MaximumIdentifierLength)
            return false;

        for (int index = 0; index < value.Length; index++)
        {
            char character = value[index];

            if (character is >= 'a' and <= 'z' or >= '0' and <= '9')
                continue;

            if (character != '-' || index == 0 || index == value.Length - 1 || value[index - 1] == '-')
                return false;
        }

        return true;
    }

    // This explicit, platform-independent set implements the schema's reserved-name rule.
    private static bool IsReservedSlug(string slug) =>
        slug is "con" or "prn" or "aux" or "nul" ||
        (slug.Length == 4 && (slug.StartsWith("com", StringComparison.Ordinal) || slug.StartsWith("lpt", StringComparison.Ordinal)) &&
            slug[3] is >= '1' and <= '9');

    private static bool IsLocaleFormat(string locale)
    {
        // V1 supports language[-script][-region], without OS culture data or a language registry.
        if (locale.Length is < 2 or > 12)
            return false;

        string[] parts = locale.Split('-');

        if (parts[0].Length is < 2 or > 3 || !parts[0].All(char.IsAsciiLetter))
            return false;

        int index = 1;

        if (index < parts.Length && parts[index].Length == 4 && parts[index].All(char.IsAsciiLetter))
            index++;

        if (index < parts.Length &&
            ((parts[index].Length == 2 && parts[index].All(char.IsAsciiLetter)) ||
                (parts[index].Length == 3 && parts[index].All(char.IsAsciiDigit))))
            index++;

        return index == parts.Length;
    }

    private static void AddError(List<ConfigurationDiagnostic> diagnostics, string code, string path, string message) =>
        diagnostics.Add(new(new DiagnosticCode(code), DiagnosticSeverity.Error, message, path));
}
