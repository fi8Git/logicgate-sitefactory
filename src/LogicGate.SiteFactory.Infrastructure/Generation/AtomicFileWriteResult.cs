namespace LogicGate.SiteFactory.Infrastructure.Generation;

/// <summary>A complete file was promoted. No ownership, generation report or durability guarantee is implied.</summary>
public sealed record AtomicFileWriteResult
{
    internal AtomicFileWriteResult(string relativePath, long bytesWritten)
    {
        RelativePath = relativePath;
        BytesWritten = bytesWritten;
    }

    public string RelativePath { get; }

    public long BytesWritten { get; }
}
