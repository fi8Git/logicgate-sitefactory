using System.Text;
using Xunit;

namespace LogicGate.SiteFactory.ModuleSdk.Tests;

public sealed class TemplateProviderTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;
    private static TemplateRenderContext Context => new("Example", "fr-FR");

    [Fact]
    public async Task IndependentProviderSuppliesTwoTemplatesWithoutImplementingModuleMetadata()
    {
        ISiteFactoryTemplateProvider provider = new SampleProvider();
        Assert.IsNotAssignableFrom<ISiteFactoryModule>(provider);
        Assert.NotEqual(typeof(ISiteFactoryTemplateProvider).Assembly, provider.GetType().Assembly);
        TemplateCatalogResult catalog = await provider.ReadCatalogAsync(Token);
        Assert.True(catalog.IsSuccess);
        Assert.Equal(["docs", "starter"], catalog.Templates.Select(static template => template.Identity.Id));
        Assert.Equal(["1.0.0", "2.0.0"], catalog.Templates.Select(static template => template.Identity.Version));
        foreach (TemplateDefinition template in catalog.Templates)
        {
            TemplateFileDefinition file = Assert.Single(template.Files);
            Assert.NotEqual(file.RelativePath, file.ContentKey);
            TemplateContentResult content = await provider.ReadContentAsync(template.Identity, file.ContentKey, Context, Token);
            Assert.True(content.IsSuccess);
            Assert.Equal($"{template.Identity.Id}:Example:fr-FR", Encoding.UTF8.GetString(content.Content.Span));
        }
    }

    [Fact]
    public async Task ExactVersionAndSourceAreRequiredWithoutFallback()
    {
        ISiteFactoryTemplateProvider provider = new SampleProvider();
        TemplateContentResult wrongVersion = await provider.ReadContentAsync(new("docs", "2.0.0"), "home", Context, Token);
        Assert.False(wrongVersion.IsSuccess);
        Assert.Equal(TemplateReadFailure.UnsupportedTemplate, wrongVersion.Error);
        TemplateContentResult unknown = await provider.ReadContentAsync(new("docs", "1.0.0"), "index.html", Context, Token);
        Assert.False(unknown.IsSuccess);
        Assert.Equal(TemplateReadFailure.UnknownContent, unknown.Error);
        Assert.True(unknown.Content.IsEmpty);
    }

    [Fact]
    public async Task BothOperationsPropagatePreCancelledTokens()
    {
        ISiteFactoryTemplateProvider provider = new SampleProvider();
        using CancellationTokenSource cancelled = new();
        cancelled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => provider.ReadCatalogAsync(cancelled.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => provider.ReadContentAsync(new("docs", "1.0.0"), "home", Context, cancelled.Token));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CancellationDuringAnAwaitIsNotConvertedToAResult(bool content)
    {
        TaskCompletionSource gate = new(TaskCreationOptions.RunContinuationsAsynchronously);
        ISiteFactoryTemplateProvider provider = new SampleProvider(gate.Task);
        using CancellationTokenSource cancellation = new();
        Task pending = content
            ? provider.ReadContentAsync(new("docs", "1.0.0"), "home", Context, cancellation.Token)
            : provider.ReadCatalogAsync(cancellation.Token);
        Assert.False(pending.IsCompleted);
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await pending);
    }

    private sealed class SampleProvider(Task? gate = null) : ISiteFactoryTemplateProvider
    {
        private static readonly TemplateDefinition[] Definitions =
        [
            new(new("docs", "1.0.0"), ["documentation"], [new("home-file", "index.html", "home", TemplateFilePolicy.Generated)]),
            new(new("starter", "2.0.0"), ["documentation"], [new("readme-file", "README.md", "introduction", TemplateFilePolicy.CreateOnce)]),
        ];

        public async Task<TemplateCatalogResult> ReadCatalogAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await (gate ?? Task.CompletedTask).WaitAsync(cancellationToken);
            return TemplateCatalogResult.Success(Definitions);
        }

        public async Task<TemplateContentResult> ReadContentAsync(TemplateIdentity template, string contentKey,
            TemplateRenderContext context, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(template);
            ArgumentNullException.ThrowIfNull(contentKey);
            ArgumentNullException.ThrowIfNull(context);
            cancellationToken.ThrowIfCancellationRequested();
            await (gate ?? Task.CompletedTask).WaitAsync(cancellationToken);
            TemplateDefinition? definition = Definitions.SingleOrDefault(item => item.Identity == template);
            if (definition is null) return TemplateContentResult.Failure(TemplateReadFailure.UnsupportedTemplate);
            if (!definition.Files.Any(file => file.ContentKey == contentKey))
                return TemplateContentResult.Failure(TemplateReadFailure.UnknownContent);
            return TemplateContentResult.Success(Encoding.UTF8.GetBytes($"{template.Id}:{context.DisplayName}:{context.DefaultLocale}"));
        }
    }
}
