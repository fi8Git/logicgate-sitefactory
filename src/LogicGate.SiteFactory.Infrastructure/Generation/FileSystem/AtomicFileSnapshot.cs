using LogicGate.SiteFactory.Application.Generation;
using Microsoft.Win32.SafeHandles;

namespace LogicGate.SiteFactory.Infrastructure.Generation.FileSystem;

/// <summary>Keeps the observed inode/file ID alive until application ends; never persisted.</summary>
internal sealed class AtomicFileSnapshot(SafeFileHandle handle, string identity, GeneratedContentHash hash) : IDisposable
{
    internal GeneratedContentHash Hash { get; } = hash;

    private string Identity { get; } = identity;

    internal static bool Matches(AtomicFileSnapshot? first, AtomicFileSnapshot? second) =>
        first is null ? second is null : second is not null && first.Identity == second.Identity && first.Hash == second.Hash;

    public void Dispose() => handle.Dispose();
}
