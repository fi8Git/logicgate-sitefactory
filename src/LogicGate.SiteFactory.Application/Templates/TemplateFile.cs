using LogicGate.SiteFactory.Application.Generation;

namespace LogicGate.SiteFactory.Application.Templates;

/// <summary>A logical file declaration, without content, physical source resolution or a planned action.</summary>
public sealed record TemplateFile
{
    public TemplateFile(PlannedFileId id, string relativePath, string source, FileGenerationStrategy strategy)
    {
        ArgumentNullException.ThrowIfNull(id);
        LogicalFilePath.Validate(relativePath, nameof(relativePath));
        LogicalFilePath.Validate(source, nameof(source));

        if (!Enum.IsDefined(strategy))
        {
            throw new ArgumentOutOfRangeException(nameof(strategy));
        }

        Id = id;
        RelativePath = relativePath;
        Source = source;
        Strategy = strategy;
    }

    public PlannedFileId Id { get; }

    public string RelativePath { get; }

    public string Source { get; }

    public FileGenerationStrategy Strategy { get; }
}
