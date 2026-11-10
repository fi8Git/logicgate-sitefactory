using LogicGate.SiteFactory.Application.Configuration;
using LogicGate.SiteFactory.Application.Modules;
using LogicGate.SiteFactory.Application.Templates;

namespace LogicGate.SiteFactory.Application.Generation;

/// <summary>Explicit, ordinal composition of trusted contributions backed by the module catalog.</summary>
public sealed class GenerationComposition : ITemplateCatalog, ITemplateContentProvider
{
    private readonly GenerationContribution[] contributions;

    public GenerationComposition(IModuleCatalog modules, IEnumerable<GenerationContribution> contributions,
        IConfigurationSelectionValidator? selectionValidator = null)
    {
        ArgumentNullException.ThrowIfNull(modules);
        ArgumentNullException.ThrowIfNull(contributions);
        GenerationContribution[] snapshot = contributions.ToArray();
        if (snapshot.Any(static contribution => contribution is null))
            throw new ArgumentException("Contributions cannot contain null entries.", nameof(contributions));

        this.contributions = snapshot.OrderBy(static contribution => contribution.ModuleId.Value, StringComparer.Ordinal)
            .ThenBy(static contribution => contribution.TemplateId.Value, StringComparer.Ordinal).ToArray();
        SelectionValidator = selectionValidator;
        List<ConfigurationDiagnostic> diagnostics = [.. modules.Diagnostics];
        foreach (GenerationContribution contribution in this.contributions)
        {
            ModuleResolutionResult resolved = modules.Resolve(contribution.ModuleId);
            if (!resolved.IsSuccess)
                diagnostics.AddRange(resolved.Diagnostics);
            else if (resolved.Module!.Version != contribution.ModuleVersion)
                diagnostics.Add(ModuleDiagnostics.InvalidRegistration(contribution.ModuleId));
        }

        if (snapshot.Select(static contribution => contribution.TemplateId).Distinct().Count() != snapshot.Length)
        {
            GenerationPlanDiagnostic diagnostic = TemplateDiagnostics.AmbiguousTemplate;
            diagnostics.Add(new(diagnostic.Code, diagnostic.Severity, diagnostic.Message, diagnostic.PropertyPath));
        }
        Diagnostics = new ConfigurationValidationResult(diagnostics.Distinct()).Diagnostics;
    }

    public IConfigurationSelectionValidator? SelectionValidator { get; }
    public IReadOnlyList<ConfigurationDiagnostic> Diagnostics { get; }
    public bool IsValid => Diagnostics.Count == 0;

    public async Task<TemplateCatalogReadResult> ReadAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!IsValid)
        {
            ConfigurationDiagnostic diagnostic = Diagnostics[0];
            return TemplateCatalogReadResult.Failure(new(diagnostic.Code, diagnostic.Severity,
                diagnostic.Message, diagnostic.PropertyPath));
        }

        List<TemplateManifest> manifests = [];
        foreach (GenerationContribution contribution in contributions)
        {
            TemplateCatalogReadResult result = await contribution.Templates.ReadAsync(cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            if (!result.IsSuccess) return result;
            if (result.Manifests.Any(manifest => manifest.TemplateId != contribution.TemplateId))
                return TemplateCatalogReadResult.Failure(TemplateDiagnostics.InvalidManifest);
            manifests.AddRange(result.Manifests);
        }
        return TemplateCatalogReadResult.Success(manifests);
    }

    public Task<string?> ReadAsync(TemplateManifest manifest, ClientConfigurationV1 configuration,
        string source, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(source);
        cancellationToken.ThrowIfCancellationRequested();
        GenerationContribution? contribution = IsValid
            ? contributions.SingleOrDefault(contribution => contribution.TemplateId == manifest.TemplateId) : null;
        return contribution is null
            ? Task.FromResult<string?>(null)
            : contribution.Content.ReadAsync(manifest, configuration, source, cancellationToken);
    }
}
