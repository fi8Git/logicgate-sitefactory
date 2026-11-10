using System.Text;
using LogicGate.SiteFactory.Application.Repositories;
using LogicGate.SiteFactory.Modules.Documentation.Scanning;

namespace LogicGate.SiteFactory.Modules.Documentation.Model;

/// <summary>Optional read-only bridge. The caller owns the repository; the builder and model have no I/O.</summary>
public static class DocumentationModelLoader
{
    public static async Task<DocumentationModelResult> LoadAsync(DocumentationScanResult scan, IReadOnlyRepository repository,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(scan);
        ArgumentNullException.ThrowIfNull(repository);
        cancellationToken.ThrowIfCancellationRequested();
        if (!scan.IsSuccess) return DocumentationModelResult.Failure("SF-DOC-MODEL-005", "A successful document inventory is required.");
        List<DocumentationSource> sources = [];
        int totalBytes = 0;
        foreach (RepositoryDocument document in scan.Documents)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!DocumentationSourcePath.IsValid(document))
                return DocumentationModelResult.Failure("SF-DOC-MODEL-001", "A document path or logical identity is invalid.");
            if (sources.Count == RepositoryDocumentationScanner.MaximumDocuments) return DocumentationModelBuilder.LimitExceeded();
            string markdown;
            try
            {
                markdown = await repository.ReadUtf8Async(document.RelativePath, RepositoryDocumentationScanner.MaximumDocumentBytes, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
            {
                return DocumentationModelResult.Failure("SF-DOC-MODEL-006", "A document could not be read safely within the read limits.");
            }
            totalBytes += Encoding.UTF8.GetByteCount(markdown);
            if (totalBytes > RepositoryDocumentationScanner.MaximumTotalBytes) return DocumentationModelBuilder.LimitExceeded();
            sources.Add(new(document, markdown));
        }
        return DocumentationModelBuilder.Build(sources, cancellationToken);
    }
}
