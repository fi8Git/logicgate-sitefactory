namespace LogicGate.SiteFactory.ModuleSdk;

/// <summary>A statically supplied module; describing it must not perform I/O or registration side effects.</summary>
public interface ISiteFactoryModule
{
    /// <summary>Stable metadata for this module instance, independent of the environment.</summary>
    ModuleDescriptor Descriptor { get; }
}
