using LogicGate.SiteFactory.Application.Generation;
using LogicGate.SiteFactory.ModuleSdk;

namespace LogicGate.SiteFactory.Infrastructure.Generation.V2;

/// <summary>Host-supplied identity and artifact digest, not a DLL measurement or a trust attestation.</summary>
internal sealed record TemplateProviderIdentity
{
    internal TemplateProviderIdentity(ModuleId moduleId, ModuleVersion moduleVersion, string type, GeneratedContentHash artifactHash)
    {
        ArgumentNullException.ThrowIfNull(moduleId);
        ArgumentNullException.ThrowIfNull(moduleVersion);
        ArgumentNullException.ThrowIfNull(artifactHash);
        GenerationV2Json.RequireText(moduleVersion.Value, GenerationV2Json.MaximumLockLength);
        GenerationV2Json.RequireText(type, GenerationV2Json.MaximumLockLength);
        ModuleId = moduleId;
        ModuleVersion = moduleVersion;
        Type = type;
        ArtifactHash = artifactHash;
    }

    internal ModuleId ModuleId { get; }
    internal ModuleVersion ModuleVersion { get; }
    internal string Type { get; }
    internal GeneratedContentHash ArtifactHash { get; }
}
