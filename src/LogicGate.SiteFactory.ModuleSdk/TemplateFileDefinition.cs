namespace LogicGate.SiteFactory.ModuleSdk;

/// <summary>A stable file ID, target declaration and separate source key; no owner or physical destination.</summary>
/// <remarks>Portable paths, reserved names, collisions and executable policies are validated by the engine, not the SDK.</remarks>
public sealed record TemplateFileDefinition
{
    public TemplateFileDefinition(string fileId, string relativePath, string contentKey, TemplateFilePolicy policy)
    {
        TemplateContractValidation.RequireText(fileId, nameof(fileId));
        TemplateContractValidation.RequireText(relativePath, nameof(relativePath));
        TemplateContractValidation.RequireText(contentKey, nameof(contentKey));
        if (!Enum.IsDefined(policy)) throw new ArgumentOutOfRangeException(nameof(policy));
        FileId = fileId;
        RelativePath = relativePath;
        ContentKey = contentKey;
        Policy = policy;
    }

    public string FileId { get; }
    public string RelativePath { get; }
    public string ContentKey { get; }
    public TemplateFilePolicy Policy { get; }
}
