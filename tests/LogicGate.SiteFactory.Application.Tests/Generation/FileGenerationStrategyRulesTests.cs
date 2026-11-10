using System.Globalization;
using System.Reflection;
using LogicGate.SiteFactory.Application.Generation;
using Xunit;

namespace LogicGate.SiteFactory.Application.Tests.Generation;

public sealed class FileGenerationStrategyRulesTests
{
    [Fact]
    public void GeneratedRequiresUnmodifiedPreviouslyGeneratedContentBeforeReplacement()
    {
        FileGenerationStrategyRules rules = new(FileGenerationStrategy.Generated);

        Assert.True(rules.RequiresUnmodifiedGeneratedContent);
        Assert.False(rules.CreatesOnlyWhenAbsent);
        Assert.False(rules.RequiresStructuredMerge);
        Assert.False(rules.BelongsToClientCustomizations);
        Assert.False(rules.ForbidsAutomaticWrites);
    }

    [Fact]
    public void CreateOnceCreatesOnlyWhenAbsentAndPreservesAnExistingFile()
    {
        FileGenerationStrategyRules rules = new(FileGenerationStrategy.CreateOnce);

        Assert.False(rules.RequiresUnmodifiedGeneratedContent);
        Assert.True(rules.CreatesOnlyWhenAbsent);
        Assert.False(rules.RequiresStructuredMerge);
        Assert.False(rules.BelongsToClientCustomizations);
        Assert.False(rules.ForbidsAutomaticWrites);
    }

    [Fact]
    public void MergeRequiresAFutureStructuredHandlerInsteadOfWholeFileReplacement()
    {
        FileGenerationStrategyRules rules = new(FileGenerationStrategy.Merge);

        Assert.False(rules.RequiresUnmodifiedGeneratedContent);
        Assert.False(rules.CreatesOnlyWhenAbsent);
        Assert.True(rules.RequiresStructuredMerge);
        Assert.False(rules.BelongsToClientCustomizations);
        Assert.False(rules.ForbidsAutomaticWrites);
    }

    [Fact]
    public void CustomBelongsToClientCustomizationsAndForbidsAutomaticWrites()
    {
        FileGenerationStrategyRules rules = new(FileGenerationStrategy.Custom);

        Assert.False(rules.RequiresUnmodifiedGeneratedContent);
        Assert.False(rules.CreatesOnlyWhenAbsent);
        Assert.False(rules.RequiresStructuredMerge);
        Assert.True(rules.BelongsToClientCustomizations);
        Assert.True(rules.ForbidsAutomaticWrites);
    }

    [Fact]
    public void ProtectedForbidsAutomaticWritesWithoutClaimingClientCustomizationOwnership()
    {
        FileGenerationStrategyRules rules = new(FileGenerationStrategy.Protected);

        Assert.False(rules.RequiresUnmodifiedGeneratedContent);
        Assert.False(rules.CreatesOnlyWhenAbsent);
        Assert.False(rules.RequiresStructuredMerge);
        Assert.False(rules.BelongsToClientCustomizations);
        Assert.True(rules.ForbidsAutomaticWrites);
    }

    [Fact]
    public void StrategySetContainsExactlyTheFiveAcceptedAdrValuesWithoutAliases()
    {
        Assert.Equal(
            [FileGenerationStrategy.Generated, FileGenerationStrategy.CreateOnce, FileGenerationStrategy.Merge,
                FileGenerationStrategy.Custom, FileGenerationStrategy.Protected],
            Enum.GetValues<FileGenerationStrategy>());
        Assert.Equal(["Generated", "CreateOnce", "Merge", "Custom", "Protected"], Enum.GetNames<FileGenerationStrategy>());
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(5)]
    [InlineData(99)]
    [InlineData(int.MinValue)]
    [InlineData(int.MaxValue)]
    public void UnknownStrategyCannotSupplyRulesOrEnterAPlannedFile(int strategy)
    {
        Assert.Throws<ArgumentOutOfRangeException>(nameof(strategy), () => new FileGenerationStrategyRules((FileGenerationStrategy)strategy));
        Assert.Throws<ArgumentOutOfRangeException>(nameof(strategy), () => new PlannedFile(
            new("test-readme"), "README.md", new(PlannedFileOriginKind.Generator, "sitefactory"),
            (FileGenerationStrategy)strategy, PlannedFileAction.Create));
    }

    [Theory]
    [InlineData("en-US")]
    [InlineData("fr-FR")]
    [InlineData("tr-TR")]
    public void RuleDescriptionsRemainStableAcrossCulturesAndRepeatedConstruction(string cultureName)
    {
        FileGenerationStrategyRules[] expected = Enum.GetValues<FileGenerationStrategy>()
            .Select(static strategy => new FileGenerationStrategyRules(strategy)).ToArray();
        CultureInfo previousCulture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(cultureName);

            foreach (FileGenerationStrategyRules original in expected)
            {
                FileGenerationStrategyRules repeated = new(original.Strategy);

                Assert.Equal(original, repeated);
                Assert.Equal(original.RequiresUnmodifiedGeneratedContent, repeated.RequiresUnmodifiedGeneratedContent);
                Assert.Equal(original.CreatesOnlyWhenAbsent, repeated.CreatesOnlyWhenAbsent);
                Assert.Equal(original.RequiresStructuredMerge, repeated.RequiresStructuredMerge);
                Assert.Equal(original.BelongsToClientCustomizations, repeated.BelongsToClientCustomizations);
                Assert.Equal(original.ForbidsAutomaticWrites, repeated.ForbidsAutomaticWrites);
            }
        }
        finally
        {
            CultureInfo.CurrentCulture = previousCulture;
        }
    }

    [Fact]
    public void RuleDescriptionsAreImmutableAndRemainInApplication()
    {
        Type contract = typeof(FileGenerationStrategyRules);

        Assert.Equal(typeof(PlannedFile).Assembly, contract.Assembly);
        Assert.True(contract.IsSealed);
        Assert.All(contract.GetProperties(), static property => Assert.Null(property.SetMethod));
        Assert.Empty(contract.GetFields(BindingFlags.Instance | BindingFlags.Public));
        Assert.NotEqual(new FileGenerationStrategyRules(FileGenerationStrategy.Custom),
            new FileGenerationStrategyRules(FileGenerationStrategy.Protected));
    }
}
