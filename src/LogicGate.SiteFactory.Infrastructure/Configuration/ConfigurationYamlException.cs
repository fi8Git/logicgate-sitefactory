using LogicGate.SiteFactory.Application.Configuration;
using YamlDotNet.Core;

namespace LogicGate.SiteFactory.Infrastructure.Configuration;

// Carries a controlled diagnostic across YamlDotNet's exception-based parsing boundary.
internal sealed class ConfigurationYamlException(string code, string message, string? propertyPath = null)
    : YamlException(message)
{
    public ConfigurationDiagnostic Diagnostic { get; } = new(
        new DiagnosticCode(code), DiagnosticSeverity.Error, message, propertyPath);
}
