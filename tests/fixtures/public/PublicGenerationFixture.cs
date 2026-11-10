using LogicGate.SiteFactory.Application.Configuration;
using LogicGate.SiteFactory.Application.Generation;
using LogicGate.SiteFactory.Application.Templates;
using LogicGate.SiteFactory.ModuleSdk;

namespace LogicGate.SiteFactory.Tests.Fixtures;

// Test module only: real public contracts, no physical template or production implementation replaced.
internal sealed class PublicGenerationFixture : ISiteFactoryModule, ITemplateCatalog, ITemplateContentProvider
{
    internal const string Product = "fixture-product";
    internal const string Template = "fixture-template";
    internal const string Version = "1.0.0";

    public ModuleDescriptor Descriptor { get; } = new(new("fixture-module"), new(Version), []);

    internal static TemplateManifest Manifest { get; } = new(new(Template), new(Version), [new(Product)],
    [
        new(new("attributes"), ".gitattributes", "fixture/attributes", FileGenerationStrategy.CreateOnce),
        new(new("ignore"), ".gitignore", "fixture/ignore", FileGenerationStrategy.CreateOnce),
        new(new("solution"), "Client.slnx", "fixture/solution", FileGenerationStrategy.Generated),
        new(new("props"), "Directory.Build.props", "fixture/props", FileGenerationStrategy.Generated),
        new(new("readme"), "README.md", "structure/readme", FileGenerationStrategy.CreateOnce),
        new(new("program"), "src/Client.Web/Program.cs", "fixture/program", FileGenerationStrategy.Generated),
        new(new("test-lock"), "tests/Client.Tests/packages.lock.json", "fixture/test-lock", FileGenerationStrategy.Generated),
    ]);

    internal GenerationContribution CreateContribution() => new(Descriptor.Id, Descriptor.Version, Manifest.TemplateId, this, this);

    public Task<TemplateCatalogReadResult> ReadAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(TemplateCatalogReadResult.Success([Manifest]));
    }

    public Task<string?> ReadAsync(TemplateManifest manifest, ClientConfigurationV1 configuration,
        string source, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentNullException.ThrowIfNull(configuration);
        string? content = manifest.TemplateId.Value != Template || manifest.TemplateVersion.Value != Version ? null : source switch
        {
            "fixture/attributes" => "* text=auto eol=lf\n",
            "fixture/ignore" => "bin/\nobj/\n",
            "fixture/solution" => "<Solution />\n",
            "fixture/props" => "<Project />\n",
            "structure/readme" => "Fictitious public fixture.\n",
            "fixture/program" => "// Fictitious planned source.\n",
            "fixture/test-lock" => "{}\n",
            _ => null,
        };
        return Task.FromResult(content);
    }
}
