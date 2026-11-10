using LogicGate.SiteFactory.Application.Configuration;
using LogicGate.SiteFactory.Application.Generation;
using LogicGate.SiteFactory.Application.Templates;

namespace LogicGate.SiteFactory.Cli.Commands.Validate;

internal static class ValidateCommand
{
    public static async Task<int> RunAsync(
        string configurationPath,
        ITemplateCatalog catalog,
        IConfigurationSelectionValidator? selectionValidator,
        TextWriter standardOutput,
        TextWriter standardError,
        CancellationToken cancellationToken)
    {
        (ClientConfigurationV1? configuration, int exitCode) = await ConfigurationCommandInput.LoadAndValidateAsync(
            configurationPath, selectionValidator, standardError, cancellationToken);

        if (configuration is null)
            return exitCode;

        TemplateResolutionResult resolved = await new TemplateResolver(catalog).ResolveAsync(configuration, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        if (resolved.Template is null)
        {
            GenerationPlanDiagnostic diagnostic = resolved.Diagnostic!;
            await standardError.WriteLineAsync(
                $"{diagnostic.Code.Value} {diagnostic.PropertyPath ?? "-"} {diagnostic.Message}".AsMemory(), cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            return CliExitCode.InvalidCatalog;
        }

        await standardOutput.WriteLineAsync("La configuration SiteFactory est valide.".AsMemory(), cancellationToken);
        return CliExitCode.Success;
    }
}
