using Xunit;

namespace LogicGate.SiteFactory.Infrastructure.Tests;

public sealed class AssemblyMarkerTests
{
    [Fact]
    public void InfrastructureAssemblyLoadsAndContainsExpectedMarker()
    {
        Type markerType = typeof(Infrastructure.AssemblyMarker);

        Assert.Equal("LogicGate.SiteFactory.Infrastructure", markerType.Assembly.GetName().Name);
        Assert.Equal(
            markerType,
            markerType.Assembly.GetType("LogicGate.SiteFactory.Infrastructure.AssemblyMarker"));
    }
}
