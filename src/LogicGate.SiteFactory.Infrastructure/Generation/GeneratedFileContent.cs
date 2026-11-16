using System.Security.Cryptography;
using System.Text;
using LogicGate.SiteFactory.Application.Generation;

namespace LogicGate.SiteFactory.Infrastructure.Generation;

/// <summary>Owned bytes prepared from canonical text or an exact binary snapshot, without resolving content sources.</summary>
public sealed class GeneratedFileContent
{
    public GeneratedFileContent(string text) : this(Encode(text))
    {
    }

    private GeneratedFileContent(byte[] bytes)
    {
        Bytes = bytes;
        Hash = new(Convert.ToHexString(SHA256.HashData(Bytes.Span)));
    }

    /// <summary>Captures a defensive copy without decoding, adding a BOM or normalizing line endings.</summary>
    internal static GeneratedFileContent FromBytes(ReadOnlySpan<byte> bytes) => new(bytes.ToArray());

    public GeneratedContentHash Hash { get; }

    internal ReadOnlyMemory<byte> Bytes { get; }

    internal static byte[] Encode(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return new UTF8Encoding(false, true).GetBytes(text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n'));
    }
}
