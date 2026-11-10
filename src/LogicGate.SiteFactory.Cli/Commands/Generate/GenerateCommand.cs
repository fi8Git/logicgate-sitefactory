using LogicGate.SiteFactory.Application.Configuration;
using LogicGate.SiteFactory.Application.Generation;
using LogicGate.SiteFactory.Application.Templates;
using LogicGate.SiteFactory.Infrastructure.Generation;

namespace LogicGate.SiteFactory.Cli.Commands.Generate;

internal static class GenerateCommand
{
    public static async Task<int> RunAsync(string configurationPath, string outputPath, ITemplateCatalog catalog,
        ITemplateContentProvider contentProvider, IConfigurationSelectionValidator? selectionValidator,
        TextWriter standardOutput, TextWriter standardError, CancellationToken cancellationToken)
    {
        (ClientConfigurationV1? configuration, int exitCode) = await ConfigurationCommandInput.LoadAndValidateAsync(
            configurationPath, selectionValidator, standardError, cancellationToken);
        if (configuration is null) return exitCode;

        TemplateResolutionResult resolved = await new TemplateResolver(catalog).ResolveAsync(configuration, cancellationToken);
        if (resolved.Template is not { } template)
        {
            await WriteDiagnosticAsync(resolved.Diagnostic!, standardError, cancellationToken);
            return CliExitCode.InvalidCatalog;
        }

        GenerationPlanId id = new($"plan:{configuration.Client.Slug}:{configuration.Site.Product}:{configuration.Site.Template}");
        GenerationPlan plan = GenerationPlanBuilder.Build(id, configuration, template);
        if (plan.HasErrors)
        {
            foreach (GenerationPlanDiagnostic diagnostic in plan.Diagnostics)
                await WriteDiagnosticAsync(diagnostic, standardError, cancellationToken);
            return CliExitCode.NonExecutablePlan;
        }

        GenerationExecutionRequest request = new(plan, plan.ClientSlug.Value);
        InitialGenerationPreparation preparation = await InitialGenerationExecutor.PrepareAsync(
            request, configuration, template, outputPath, contentProvider, cancellationToken);
        if (preparation.Executor is not { } executor)
        {
            foreach (GenerationPlanDiagnostic diagnostic in preparation.Diagnostics)
                await WriteDiagnosticAsync(diagnostic, standardError, cancellationToken);
            return preparation.Status switch
            {
                InitialGenerationPreparationStatus.InvalidPlan => CliExitCode.NonExecutablePlan,
                InitialGenerationPreparationStatus.InvalidTemplate => CliExitCode.InvalidCatalog,
                InitialGenerationPreparationStatus.InvalidRoot => CliExitCode.EnvironmentInvalid,
                InitialGenerationPreparationStatus.Conflict => CliExitCode.FileConflict,
                _ => throw new InvalidOperationException("Unexpected preparation outcome."),
            };
        }

        GenerationExecutionResult result = await executor.ExecuteAsync(request, cancellationToken);
        if (result.Status != GenerationExecutionStatus.Succeeded)
        {
            // The existing report projection removes raw provider messages and unrecognized identifiers.
            GenerationReport safeReport = GenerationReportWriter.CreateReport(result);
            foreach (GenerationReportDiagnostic diagnostic in safeReport.Diagnostics)
                await standardError.WriteLineAsync(
                    $"{diagnostic.Code ?? "SF-GEN-INITIAL-004"} - {diagnostic.Message}".AsMemory(), cancellationToken);
            return result.Status == GenerationExecutionStatus.Conflict ? CliExitCode.FileConflict : CliExitCode.GenerationFailed;
        }

        await standardOutput.WriteLineAsync(
            "SiteFactory minimal generation completed (no Git; client quality gates are separate).".AsMemory(), cancellationToken);
        return CliExitCode.Success;
    }

    private static Task WriteDiagnosticAsync(GenerationPlanDiagnostic diagnostic, TextWriter standardError, CancellationToken cancellationToken) =>
        standardError.WriteLineAsync(
            $"{diagnostic.Code.Value} {diagnostic.PropertyPath ?? "-"} {diagnostic.Message}".AsMemory(), cancellationToken);
}
