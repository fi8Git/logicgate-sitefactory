using System.Diagnostics.CodeAnalysis;

namespace LogicGate.SiteFactory.Runtime.Contracts;

/// <summary>An immutable validated snapshot. No provider, secret, I/O or reload semantics.</summary>
public sealed class RuntimeConfiguration
{
    private RuntimeConfiguration(RuntimeIdentity identity, Uri publicBaseUrl)
    {
        Identity = identity;
        PublicBaseUrl = publicBaseUrl;
    }

    public RuntimeIdentity Identity { get; }
    public Uri PublicBaseUrl { get; }

    public static bool TryCreate(string? clientSlug, string? environment, RuntimeComponent component, string? publicBaseUrl,
        [NotNullWhen(true)] out RuntimeConfiguration? configuration, out IReadOnlyList<StartupValidationResult> diagnostics)
    {
        RuntimeIdentity.TryCreate(clientSlug, environment, component, out RuntimeIdentity? identity,
            out IReadOnlyList<StartupValidationResult> identityDiagnostics);
        List<StartupValidationResult> errors = [.. identityDiagnostics];
        // Still collect URL diagnostics when identity fails, using the explicitly supplied environment.
        Uri? uri = ValidatePublicBaseUrl(publicBaseUrl, RuntimeIdentity.ParseEnvironment(environment), errors);
        diagnostics = errors.AsReadOnly();
        configuration = errors.Count == 0 ? new(identity!, uri!) : null;
        return configuration is not null;
    }

    public static bool TryCreate(RuntimeIdentity identity, string? publicBaseUrl,
        [NotNullWhen(true)] out RuntimeConfiguration? configuration, out IReadOnlyList<StartupValidationResult> diagnostics)
    {
        ArgumentNullException.ThrowIfNull(identity);
        List<StartupValidationResult> errors = [];
        Uri? uri = ValidatePublicBaseUrl(publicBaseUrl, identity.Environment, errors);
        diagnostics = errors.AsReadOnly();
        configuration = errors.Count == 0 ? new(identity, uri!) : null;
        return configuration is not null;
    }

    private static Uri? ValidatePublicBaseUrl(string? publicBaseUrl, RuntimeEnvironment? environment,
        List<StartupValidationResult> errors)
    {
        bool validUrl = Uri.TryCreate(publicBaseUrl, UriKind.Absolute, out Uri? uri)
            && uri.IsWellFormedOriginalString()
            && uri.Scheme is "http" or "https"
            && uri.HostNameType is not (UriHostNameType.Unknown or UriHostNameType.Basic)
            && uri.Port > 0
            && uri.UserInfo.Length == 0
            && uri.Fragment.Length == 0
            && !publicBaseUrl!.Contains('#', StringComparison.Ordinal)
            && !publicBaseUrl[(publicBaseUrl.IndexOf("://", StringComparison.Ordinal) + 3)..].Split('/', '?', '#')[0].Contains('@', StringComparison.Ordinal)
            && !publicBaseUrl.Any(char.IsWhiteSpace);
        if (validUrl && environment == RuntimeEnvironment.Production)
        {
            // Preserve the current deployment profile: non-local HTTPS on port 443.
            validUrl = uri!.Scheme == "https" && uri.Port == 443 && !uri.IsLoopback
                && !uri.Host.TrimEnd('.').EndsWith(".localhost", StringComparison.OrdinalIgnoreCase)
                && !uri.Host.TrimEnd('.').Equals("localhost", StringComparison.OrdinalIgnoreCase)
                && uri.Host is not ("0.0.0.0" or "[::]");
        }

        if (!validUrl)
        {
            errors.Add(new(RuntimeIdentity.ValidationId, StartupValidationSeverity.BlockingFailure, "SF-CONFIG-STARTUP-005",
                "An absolute HTTP(S) URL without credentials or fragment is required; Production requires non-local HTTPS on port 443.", "PublicBaseUrl"));
        }

        return validUrl ? uri : null;
    }
}
