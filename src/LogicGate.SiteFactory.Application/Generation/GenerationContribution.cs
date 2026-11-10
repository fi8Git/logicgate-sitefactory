using LogicGate.SiteFactory.Application.Templates;
using LogicGate.SiteFactory.Domain;
using LogicGate.SiteFactory.ModuleSdk;

namespace LogicGate.SiteFactory.Application.Generation;

/// <summary>One explicitly owned template contribution; no discovery, destination access or module activation.</summary>
public sealed class GenerationContribution
{
    public GenerationContribution(ModuleId moduleId, ModuleVersion moduleVersion, TemplateId templateId,
        ITemplateCatalog templates, ITemplateContentProvider content)
    {
        ArgumentNullException.ThrowIfNull(moduleId);
        ArgumentNullException.ThrowIfNull(moduleVersion);
        ArgumentNullException.ThrowIfNull(templateId);
        ArgumentNullException.ThrowIfNull(templates);
        ArgumentNullException.ThrowIfNull(content);
        ModuleId = moduleId;
        ModuleVersion = moduleVersion;
        TemplateId = templateId;
        Templates = templates;
        Content = content;
    }

    public ModuleId ModuleId { get; }
    public ModuleVersion ModuleVersion { get; }
    public TemplateId TemplateId { get; }
    public ITemplateCatalog Templates { get; }
    public ITemplateContentProvider Content { get; }
}
