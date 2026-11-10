using Xunit;

namespace LogicGate.SiteFactory.Application.Tests;

public sealed class AssemblyMarkerTests
{
    [Fact]
    public void ApplicationAssemblyLoadsAndContainsExpectedMarker()
    {
        Type markerType = typeof(Application.AssemblyMarker);

        Assert.Equal("LogicGate.SiteFactory.Application", markerType.Assembly.GetName().Name);
        Assert.Equal(
            markerType,
            markerType.Assembly.GetType("LogicGate.SiteFactory.Application.AssemblyMarker"));
    }
}
