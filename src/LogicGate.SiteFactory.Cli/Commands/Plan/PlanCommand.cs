using System.Text;
using LogicGate.SiteFactory.Application.Configuration;
using LogicGate.SiteFactory.Application.Generation;
using LogicGate.SiteFactory.Application.Templates;
using LogicGate.SiteFactory.Infrastructure.Generation;

namespace LogicGate.SiteFactory.Cli.Commands.Plan;

internal static class PlanCommand
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
        if (resolved.Template is not { } template)
        {
            await WriteDiagnosticAsync(resolved.Diagnostic!, standardError, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            return CliExitCode.InvalidCatalog;
        }

        // A stable logical label, not an operation ID or a fingerprint of the plan's contents.
        GenerationPlanId id = new($"plan:{configuration.Client.Slug}:{configuration.Site.Product}:{configuration.Site.Template}");
        GenerationPlan plan = GenerationPlanBuilder.Build(id, configuration, template);
        cancellationToken.ThrowIfCancellationRequested();
        if (plan.HasErrors)
        {
            foreach (GenerationPlanDiagnostic diagnostic in plan.Diagnostics)
                await WriteDiagnosticAsync(diagnostic, standardError, cancellationToken);

            cancellationToken.ThrowIfCancellationRequested();
            return CliExitCode.NonExecutablePlan;
        }

        string json = Encoding.UTF8.GetString(GenerationPlanJsonSerializer.Serialize(plan));
        cancellationToken.ThrowIfCancellationRequested();
        // Write, not WriteLine: retain the serializer's exact LF bytes, including its final LF.
        await standardOutput.WriteAsync(json.AsMemory(), cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        return CliExitCode.Success;
    }

    private static Task WriteDiagnosticAsync(
        GenerationPlanDiagnostic diagnostic, TextWriter standardError, CancellationToken cancellationToken) =>
        standardError.WriteLineAsync(
            $"{diagnostic.Code.Value} {diagnostic.PropertyPath ?? "-"} {diagnostic.Message}".AsMemory(), cancellationToken);
}
