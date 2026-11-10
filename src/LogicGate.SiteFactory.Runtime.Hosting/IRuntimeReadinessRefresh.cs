namespace LogicGate.SiteFactory.Runtime.Hosting;

/// <summary>A fresh verification whose result belongs to its caller, independently of shared readiness.</summary>
public interface IRuntimeReadinessRefresh
{
    Task<bool> RefreshAsync(CancellationToken cancellationToken);
}
