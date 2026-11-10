using LogicGate.SiteFactory.Application.Generation;
using Xunit;

namespace LogicGate.SiteFactory.Application.Tests.Generation;

public sealed class PlannedFileOriginTests
{
    [Theory]
    [InlineData(PlannedFileOriginKind.Generator, "SiteFactory.Core")]
    [InlineData(PlannedFileOriginKind.Template, "fixture-template")]
    [InlineData(PlannedFileOriginKind.Module, "test_module-1")]
    public void OriginDescribesALogicalComponentWithoutResolution(PlannedFileOriginKind kind, string componentId)
    {
        PlannedFileOrigin origin = new(kind, componentId);

        Assert.Equal(kind, origin.Kind);
        Assert.Equal(componentId, origin.ComponentId);
        Assert.Equal(origin, new PlannedFileOrigin(kind, componentId));
        Assert.Equal(origin.GetHashCode(), new PlannedFileOrigin(kind, componentId).GetHashCode());
        Assert.NotEqual(origin, new PlannedFileOrigin(kind, componentId.ToUpperInvariant()));
        Assert.NotEqual(origin, new PlannedFileOrigin(kind, "another-component"));
    }

    [Fact]
    public void ClassificationIsPartOfTheOriginIdentity()
    {
        Assert.NotEqual(
            new PlannedFileOrigin(PlannedFileOriginKind.Generator, "component"),
            new PlannedFileOrigin(PlannedFileOriginKind.Template, "component"));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("\t\r\n")]
    [InlineData(".")]
    [InlineData("..")]
    [InlineData("/templates/example")]
    [InlineData("templates/example")]
    [InlineData("templates\\example")]
    [InlineData("C:example")]
    [InlineData("~/example")]
    [InlineData("$HOME")]
    [InlineData("%USERPROFILE%")]
    [InlineData("https://example.test/template")]
    [InlineData("component\0id")]
    [InlineData(" component ")]
    [InlineData("modèle")]
    public void NonLogicalComponentIdentifiersAreRejected(string componentId)
    {
        Assert.Throws<ArgumentException>(nameof(componentId), () => new PlannedFileOrigin(PlannedFileOriginKind.Template, componentId));
    }

    [Fact]
    public void NullComponentAndUnknownClassificationAreRejected()
    {
        Assert.Throws<ArgumentNullException>("componentId", () => new PlannedFileOrigin(PlannedFileOriginKind.Generator, null!));
        Assert.Throws<ArgumentOutOfRangeException>("kind", () => new PlannedFileOrigin((PlannedFileOriginKind)99, "sitefactory"));
    }
}
