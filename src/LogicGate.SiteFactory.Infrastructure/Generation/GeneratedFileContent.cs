using System.Security.Cryptography;
using System.Text;
using LogicGate.SiteFactory.Application.Generation;

namespace LogicGate.SiteFactory.Infrastructure.Generation;

/// <summary>Canonical bytes prepared from supplied text, not a template renderer or a content-source resolver.</summary>
public sealed class GeneratedFileContent
{
    public GeneratedFileContent(string text)
    {
        Bytes = Encode(text);
        Hash = new(Convert.ToHexString(SHA256.HashData(Bytes.Span)));
    }

    public GeneratedContentHash Hash { get; }

    internal ReadOnlyMemory<byte> Bytes { get; }

    internal static byte[] Encode(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return new UTF8Encoding(false, true).GetBytes(text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n'));
    }
}
