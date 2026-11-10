using System.Diagnostics.CodeAnalysis;

namespace LogicGate.SiteFactory.Runtime.Contracts;

public enum RuntimeEnvironment
{
    Development,
    Test,
    Staging,
    Production,
}

public enum RuntimeComponent
{
    Web,
    Worker,
}

/// <summary>An immutable identity validated independently of URLs and configuration providers.</summary>
public sealed class RuntimeIdentity
{
    internal static StartupValidationId ValidationId { get; } = new("startup.configuration.schema");

    private RuntimeIdentity(string clientSlug, RuntimeEnvironment environment, RuntimeComponent component)
    {
        ClientSlug = clientSlug;
        Environment = environment;
        Component = component;
    }

    public string ClientSlug { get; }
    public RuntimeEnvironment Environment { get; }
    public RuntimeComponent Component { get; }

    public static bool TryCreate(string? clientSlug, string? environment, RuntimeComponent component,
        [NotNullWhen(true)] out RuntimeIdentity? identity, out IReadOnlyList<StartupValidationResult> diagnostics)
    {
        List<StartupValidationResult> errors = [];
        if (!IsValidSlug(clientSlug))
        {
            errors.Add(new(ValidationId, StartupValidationSeverity.BlockingFailure, "SF-CONFIG-STARTUP-003",
                "A non-reserved lowercase kebab-case client slug is required (3-80 characters).", "ClientSlug"));
        }

        RuntimeEnvironment? parsedEnvironment = ParseEnvironment(environment);
        if (parsedEnvironment is null)
        {
            errors.Add(new(ValidationId, StartupValidationSeverity.BlockingFailure, "SF-CONFIG-STARTUP-003",
                "An explicit Development, Test, Staging or Production environment is required.", "Environment"));
        }

        if (component is not (RuntimeComponent.Web or RuntimeComponent.Worker))
        {
            errors.Add(new(ValidationId, StartupValidationSeverity.BlockingFailure, "SF-CONFIG-STARTUP-003",
                "Only web and worker are supported.", "Component"));
        }

        diagnostics = errors.AsReadOnly();
        identity = errors.Count == 0 ? new(clientSlug!, parsedEnvironment!.Value, component) : null;
        return identity is not null;
    }

    internal static RuntimeEnvironment? ParseEnvironment(string? environment) => environment?.ToLowerInvariant() switch
    {
        "development" => RuntimeEnvironment.Development,
        "test" => RuntimeEnvironment.Test,
        "staging" => RuntimeEnvironment.Staging,
        "production" => RuntimeEnvironment.Production,
        _ => null,
    };

    // Same lexical contract as structural V1, without referencing the generator.
    private static bool IsValidSlug(string? value) => value is { Length: >= 3 and <= 80 }
        && value[0] != '-' && value[^1] != '-' && !value.Contains("--", StringComparison.Ordinal)
        && value is not ("con" or "prn" or "aux" or "nul")
        && !(value.Length == 4 && (value.StartsWith("com", StringComparison.Ordinal) || value.StartsWith("lpt", StringComparison.Ordinal))
            && value[3] is >= '1' and <= '9')
        && value.All(static character => character is >= 'a' and <= 'z' or >= '0' and <= '9' or '-');
}
