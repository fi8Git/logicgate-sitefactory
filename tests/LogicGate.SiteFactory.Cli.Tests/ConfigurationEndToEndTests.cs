using Xunit;

namespace LogicGate.SiteFactory.Cli.Tests;

[Trait("Category", "EndToEnd")]
public sealed class ConfigurationEndToEndTests : IDisposable
{
    private const string SuccessOutput = "La configuration SiteFactory est valide.\n";
    private readonly string directory = Directory.CreateTempSubdirectory("sitefactory-configuration-e2e-").FullName;

    [Theory]
    [InlineData("valid-minimal", 0, SuccessOutput, "")]
    [InlineData("invalid-schema-version", 15, "",
        "SF-CONFIG-SCHEMA-002 schemaVersion Seule la version 1 du schéma est prise en charge.\n")]
    [InlineData("invalid-unknown-property", 3, "",
        "SF-CONFIG-SCHEMA-004 - Une propriété inconnue est présente dans la configuration.\n")]
    [InlineData("invalid-product", 4, "",
        "SF-TEMPLATE-002 site.product Le template ne prend pas en charge le produit demandé.\n")]
    [InlineData("invalid-public-url", 3, "",
        "SF-CONFIG-SEMANTIC-011 site.publicBaseUrl L'URL publique doit être une URI HTTP ou HTTPS absolue, bien formée, avec un hôte et sans identifiants intégrés.\n")]
    public async Task VersionedFixtureProducesTheExactStableCliResultWithoutWritingFiles(
        string scenario, int exitCode, string standardOutput, string standardError)
    {
        string sourcePath = Path.Combine(AppContext.BaseDirectory, "fixtures", "configuration", scenario, "client.sitefactory.yml");

        await AssertConfigurationAsync(sourcePath, new(exitCode, standardOutput, standardError));
    }

    [Fact]
    public async Task DocumentedSamplePassesTheSameRealConfigurationPipeline()
    {
        string sourcePath = Path.Combine(AppContext.BaseDirectory, "samples", "configuration", "valid-minimal", "client.sitefactory.yml");

        await AssertConfigurationAsync(sourcePath, new(0, SuccessOutput, string.Empty));
    }

    [Fact]
    public async Task MissingFileProducesTheExactStableReadDiagnosticWithoutCreatingItsParent()
    {
        string path = Path.Combine(directory, "missing", "client.sitefactory.yml");
        CliResult expected = new(3, string.Empty, "SF-CONFIG-YAML-001 - Le fichier de configuration est introuvable.\n");

        await AssertStableProcessResultAsync(path, expected);

        Assert.Empty(Directory.GetFileSystemEntries(directory));
    }

    [Fact]
    public async Task PublicDocumentationConfigurationUsesTheRealPublicExecutableTwiceWithoutWrites()
    {
        string path = Path.Combine(directory, "documentation.sitefactory.yml");
        await File.WriteAllTextAsync(path, """
            schemaVersion: 1
            client: { slug: fictitious-documentation, displayName: Fictitious Documentation }
            site:
              product: documentation
              template: documentation-minimal
              defaultLocale: fr-FR
              publicBaseUrl: https://example.test
            modules: { enabled: [] }
            deployment: { profile: docker-compose-vps }
            git: { initializeLocalRepository: true, publishRemote: false }
            """, TestContext.Current.CancellationToken);
        byte[] before = await File.ReadAllBytesAsync(path, TestContext.Current.CancellationToken);
        string[] arguments = ["validate", "--config", Path.GetFileName(path)];

        CliResult first = await CliProcessRunner.RunAsync(directory, arguments, TestContext.Current.CancellationToken);
        CliResult second = await CliProcessRunner.RunAsync(directory, arguments, TestContext.Current.CancellationToken);

        Assert.Equal(new CliResult(0, SuccessOutput, string.Empty), first);
        Assert.Equal(first, second);
        Assert.Equal(before, await File.ReadAllBytesAsync(path, TestContext.Current.CancellationToken));
        Assert.Equal([path], Directory.GetFileSystemEntries(directory));
    }

    public void Dispose()
    {
        Directory.Delete(directory, recursive: true);
        GC.SuppressFinalize(this);
    }

    private async Task AssertConfigurationAsync(string sourcePath, CliResult expected)
    {
        // Copy the versioned bytes, never regenerate YAML or mock the loader/validator.
        byte[] original = await File.ReadAllBytesAsync(sourcePath, TestContext.Current.CancellationToken);
        string path = Path.Combine(directory, "configuration démo.sitefactory.yml");
        File.Copy(sourcePath, path);

        await AssertStableProcessResultAsync(path, expected);

        Assert.Equal(original, await File.ReadAllBytesAsync(path, TestContext.Current.CancellationToken));
        Assert.Equal(original, await File.ReadAllBytesAsync(sourcePath, TestContext.Current.CancellationToken));
        Assert.Equal([path], Directory.GetFileSystemEntries(directory));
    }

    private async Task AssertStableProcessResultAsync(string path, CliResult expected)
    {
        string[] arguments = ["validate", "--config", Path.GetRelativePath(directory, path)];
        CliResult first = await CliProcessRunner.RunAsync(directory, arguments, TestContext.Current.CancellationToken);
        CliResult second = await CliProcessRunner.RunAsync(directory, arguments, TestContext.Current.CancellationToken);

        Assert.Equal(expected, first);
        Assert.Equal(first, second);
        Assert.DoesNotContain(directory, first.StandardOutput, StringComparison.Ordinal);
        Assert.DoesNotContain(directory, first.StandardError, StringComparison.Ordinal);
    }
}
