namespace LogicGate.SiteFactory.ModuleSdk;

/// <summary>Minimal immutable module metadata; not a complete manifest or a contribution executor.</summary>
public sealed class ModuleDescriptor
{
    public ModuleDescriptor(ModuleId id, ModuleVersion version, IEnumerable<ModuleCapability> capabilities)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(version);
        ArgumentNullException.ThrowIfNull(capabilities);

        ModuleCapability[] snapshot = capabilities.ToArray();
        HashSet<string> seen = new(StringComparer.Ordinal);
        foreach (ModuleCapability capability in snapshot)
        {
            if (capability is null)
                throw new ArgumentException("Capabilities cannot contain null entries.", nameof(capabilities));
            if (!capability.Value.StartsWith(id.Value + ".", StringComparison.Ordinal))
                throw new ArgumentException("A capability must belong to the described module.", nameof(capabilities));
            if (!seen.Add(capability.Value))
                throw new ArgumentException("A capability cannot be declared twice.", nameof(capabilities));
        }

        Id = id;
        Version = version;
        Capabilities = Array.AsReadOnly(snapshot.OrderBy(static capability => capability.Value, StringComparer.Ordinal).ToArray());
    }

    public ModuleId Id { get; }

    public ModuleVersion Version { get; }

    public IReadOnlyList<ModuleCapability> Capabilities { get; }
}
