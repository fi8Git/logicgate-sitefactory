namespace LogicGate.SiteFactory.Modules.Documentation.Scanning;

public enum DocumentationSourceType { RootReadme, DocumentationPage }

/// <summary>Logical ID is the exact slash-separated relative path, including its extension. Never a physical path.</summary>
public sealed record RepositoryDocument(string RelativePath, string LogicalId, string TitleCandidate, DocumentationSourceType SourceType);

public sealed record DocumentationScanDiagnostic(string Code, string Message);

public sealed class DocumentationScanResult
{
    internal DocumentationScanResult(IEnumerable<RepositoryDocument> documents, DocumentationScanDiagnostic? diagnostic = null)
    {
        Documents = Array.AsReadOnly(documents.OrderBy(static document => document.RelativePath, StringComparer.Ordinal).ToArray());
        Diagnostic = diagnostic;
    }

    public IReadOnlyList<RepositoryDocument> Documents { get; }
    public DocumentationScanDiagnostic? Diagnostic { get; }
    public bool IsSuccess => Diagnostic is null;
}
