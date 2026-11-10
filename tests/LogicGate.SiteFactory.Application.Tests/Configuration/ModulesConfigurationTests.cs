using LogicGate.SiteFactory.Application.Configuration;
using Xunit;

namespace LogicGate.SiteFactory.Application.Tests.Configuration;

public sealed class ModulesConfigurationTests
{
    [Fact]
    public void EmptySelectionIsAllowedAndDoesNotAcquireModulesLater()
    {
        List<string> source = [];
        ModulesConfiguration configuration = new(source);

        source.Add("content");

        Assert.Empty(configuration.Enabled);
    }

    [Fact]
    public void InputArrayCannotMutateTheSelection()
    {
        string[] source = ["seo", "content", "seo"];
        ModulesConfiguration configuration = new(source);

        source[0] = "media";

        Assert.Equal(["seo", "content", "seo"], configuration.Enabled);
    }

    [Fact]
    public void ExposedSelectionCannotBeModified()
    {
        ModulesConfiguration configuration = new(["content"]);
        IList<string> selection = Assert.IsAssignableFrom<IList<string>>(configuration.Enabled);

        Assert.True(selection.IsReadOnly);
        Assert.Throws<NotSupportedException>(() => selection.Add("media"));
        Assert.Throws<NotSupportedException>(() => selection[0] = "media");
        Assert.Throws<NotSupportedException>(() => selection.Clear());
        Assert.Equal(["content"], configuration.Enabled);
    }

    [Fact]
    public void SelectionIsMaterializedOnceAtConstruction()
    {
        int enumerationCount = 0;

        IEnumerable<string> ReadModules()
        {
            enumerationCount++;
            yield return "content";
            yield return "seo";
        }

        ModulesConfiguration configuration = new(ReadModules());

        Assert.Equal(1, enumerationCount);
        Assert.Equal(["content", "seo"], configuration.Enabled);
        Assert.Equal(["content", "seo"], configuration.Enabled);
        Assert.Equal(1, enumerationCount);
    }

    [Fact]
    public void NullSelectionIsRejected()
    {
        Assert.Throws<ArgumentNullException>("enabled", () => new ModulesConfiguration(null!));
    }

    [Fact]
    public void NullModuleIdentifierIsRejected()
    {
        Assert.Throws<ArgumentException>("enabled", () => new ModulesConfiguration(["content", null!]));
    }
}
