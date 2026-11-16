namespace LogicGate.SiteFactory.ModuleSdk;

/// <summary>A complete captured catalog (possibly empty), or a closed failure with no partial declarations.</summary>
public sealed class TemplateCatalogResult
{
    private TemplateCatalogResult(IReadOnlyList<TemplateDefinition> templates, TemplateReadFailure? error)
    {
        Templates = templates;
        Error = error;
    }

    public IReadOnlyList<TemplateDefinition> Templates { get; }
    public TemplateReadFailure? Error { get; }
    public bool IsSuccess => Error is null;

    public static TemplateCatalogResult Success(IEnumerable<TemplateDefinition> templates)
    {
        ArgumentNullException.ThrowIfNull(templates);
        TemplateDefinition[] snapshot = templates.ToArray();
        if (snapshot.Any(static template => template is null))
            throw new ArgumentException("Catalog entries cannot be null.", nameof(templates));
        return new(Array.AsReadOnly(snapshot), null);
    }

    public static TemplateCatalogResult Failure(TemplateReadFailure error)
    {
        if (!Enum.IsDefined(error)) throw new ArgumentOutOfRangeException(nameof(error));
        return new(Array.AsReadOnly(Array.Empty<TemplateDefinition>()), error);
    }
}
