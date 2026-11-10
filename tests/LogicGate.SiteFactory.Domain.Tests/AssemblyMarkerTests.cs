using Xunit;

namespace LogicGate.SiteFactory.Domain.Tests;

public sealed class AssemblyMarkerTests
{
    [Fact]
    public void DomainAssemblyLoadsAndContainsExpectedMarker()
    {
        Type markerType = typeof(Domain.AssemblyMarker);

        Assert.Equal("LogicGate.SiteFactory.Domain", markerType.Assembly.GetName().Name);
        Assert.Equal(
            markerType,
            markerType.Assembly.GetType("LogicGate.SiteFactory.Domain.AssemblyMarker"));
    }
}
