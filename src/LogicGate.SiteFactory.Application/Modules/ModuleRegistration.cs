using LogicGate.SiteFactory.ModuleSdk;

namespace LogicGate.SiteFactory.Application.Modules;

/// <summary>
/// An explicit composition declaration, not yet validated. Expected identity/version are pins
/// checked against the supplied module, never inferred from its class or assembly name.
/// </summary>
public sealed record ModuleRegistration(string? ExpectedId, string? ExpectedVersion, ISiteFactoryModule? Module);
