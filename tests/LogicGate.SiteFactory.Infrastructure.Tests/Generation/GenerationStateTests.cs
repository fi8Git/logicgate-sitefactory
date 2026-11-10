using System.Text;
using LogicGate.SiteFactory.Application.Configuration;
using LogicGate.SiteFactory.Application.Generation;
using LogicGate.SiteFactory.Application.Templates;
using LogicGate.SiteFactory.Infrastructure.Generation;
using Xunit;

namespace LogicGate.SiteFactory.Infrastructure.Tests.Generation;

public sealed class GenerationStateTests
{
    private static readonly GeneratedContentHash ReportHash = new(new string('A', 64));

    [Theory]
    [InlineData("{")]
    [InlineData("null")]
    [InlineData("[]")]
    [InlineData("{}")]
    [InlineData("{\"reportSha256\": null}")]
    [InlineData("{\"reportSha256\": 42}")]
    [InlineData("{\"reportSha256\": true}")]
    [InlineData("{\"reportSha256\": []}")]
    [InlineData("{\"reportSha256\": {}}")]
    [InlineData("{\"reportSha256\": \"\\uD800\"}")]
    [InlineData("{\"reportSha256\": \"\\uDC00\"}")]
    [InlineData("{\"reportSha256\": \"\\uD800x\"}")]
    [InlineData("{\"reportSha256\": \"\\uD800\\uD800\"}")]
    [InlineData("{\"reportSha256\": \"\\uD83D\\uDE00\"}")]
    [InlineData("{\"reportSha256\\uD800\": \"invalid\"}")]
    [InlineData("{\"\\uD800reportSha256\": \"invalid\"}")]
    public void InvalidDocumentsReturnNoTrustedReference(string json)
    {
        Assert.Null(CreateState().ReadMatchingState(Encoding.UTF8.GetBytes(json)));
    }

    [Theory]
    [InlineData(63, 'A')]
    [InlineData(65, 'A')]
    [InlineData(64, 'Z')]
    public void InvalidHashInOtherwiseCanonicalDocumentIsRejected(int length, char character)
    {
        GenerationState state = CreateState();
        string canonical = Encoding.UTF8.GetString(state.Serialize(ReportHash));
        string changed = canonical.Replace(ReportHash.Value, new string(character, length), StringComparison.Ordinal);

        Assert.Null(state.ReadMatchingState(Encoding.UTF8.GetBytes(changed)));
    }

    [Fact]
    public void InvalidUtf8InsideAStringIsRejectedWithoutReplacement()
    {
        GenerationState state = CreateState();
        byte[] bytes = state.Serialize(ReportHash);
        ReadOnlySpan<byte> property = "\"reportSha256\": \""u8;
        int offset = bytes.AsSpan().IndexOf(property);
        Assert.True(offset >= 0);
        bytes[offset + property.Length] = 0xFF;

        Assert.Null(state.ReadMatchingState(bytes));
    }

    [Fact]
    public void ValidStateRetainsItsReferenceAndRequiresTheExpectedSchema()
    {
        GenerationState state = CreateState();
        byte[] bytes = state.Serialize(ReportHash);
        Assert.Equal(ReportHash, state.ReadMatchingState(bytes)?.ReportHash);
        string changed = Encoding.UTF8.GetString(bytes).Replace("\"schemaVersion\": 1", "\"schemaVersion\": 2", StringComparison.Ordinal);
        Assert.Null(state.ReadMatchingState(Encoding.UTF8.GetBytes(changed)));
    }

    [Fact]
    public void InternalSerializationFailureIsNotMisclassifiedAsCorruptUserState()
    {
        byte[] validDocument = CreateState().Serialize(ReportHash);
        // Simulate a programming error in the expected state, not a strategy read from untrusted JSON.
        // The normal preflight never constructs generation state for an unsupported merge operation.
        GenerationState unsupportedExpectedState = CreateState(FileGenerationStrategy.Merge);

        Assert.Throws<InvalidOperationException>(() => unsupportedExpectedState.ReadMatchingState(validDocument));
    }

    [Fact]
    public void ContentChangesRetainOnlyThePreviousHashesWithoutMutatingTheCandidate()
    {
        GenerationState previous = CreateState();
        GenerationState candidate = CreateState(content: "Changed source\n");
        byte[] candidateBytes = candidate.Serialize(ReportHash);
        GenerationState.Reference reference = Assert.IsType<GenerationState.Reference>(candidate.ReadMatchingState(previous.Serialize(ReportHash)));
        Assert.Equal(previous.Files[0].Hash, reference.State.Files[0].Hash);
        Assert.NotEqual(candidate.Files[0].Hash, reference.State.Files[0].Hash);
        Assert.Same(candidate.Files[0].File, reference.State.Files[0].File);
        Assert.Equal(candidateBytes, candidate.Serialize(ReportHash));
    }

    [Theory]
    [InlineData("null")]
    [InlineData("42")]
    [InlineData("{}")]
    [InlineData("[]")]
    [InlineData("\"invalid\"")]
    [InlineData("\"\\uD800\"")]
    [InlineData("\"\\uDC00\"")]
    public void InvalidPreviousContentHashCannotAuthorizeAnUpdate(string value)
    {
        GenerationState state = CreateState();
        string canonical = Encoding.UTF8.GetString(state.Serialize(ReportHash));
        string changed = canonical.Replace($"\"{state.Files[0].Hash}\"", value, StringComparison.Ordinal);
        Assert.Null(CreateState(content: "New content").ReadMatchingState(Encoding.UTF8.GetBytes(changed)));
    }

    [Fact]
    public void LockHashRemainsStructuralRatherThanAnUpdatableSourceHash()
    {
        GenerationState state = CreateState();
        string canonical = Encoding.UTF8.GetString(state.Serialize(ReportHash));
        string changed = canonical.Replace(state.Files[^1].Hash!.Value, new string('0', 64), StringComparison.Ordinal);
        Assert.Null(state.ReadMatchingState(Encoding.UTF8.GetBytes(changed)));
    }

    private static GenerationState CreateState(FileGenerationStrategy strategy = FileGenerationStrategy.Generated,
        string content = "Minimal test content.\n")
    {
        ClientConfigurationV1 configuration = new(1, new("public-fixture", "Public Fixture"),
            new("fixture-product", "fixture-template", "fr-FR", "https://www.example.test"), new([]), new("docker-compose-vps"), new(true, false));
        TemplateManifest template = new(new("fixture-template"), new("0.3.1"), [new("fixture-product")],
            [new(new("readme"), "README.md", "structure/readme", strategy)]);
        PlannedFile file = new(new("readme"), "README.md", new(PlannedFileOriginKind.Template, "fixture-template"), strategy, PlannedFileAction.Create);
        GenerationPlan plan = new(new("state-test"), new("public-fixture"), new("fixture-product"), template.TemplateId, [file], []);
        GenerationExecutionRequest request = new(plan, "public-fixture");
        return GenerationState.Create(request, configuration, template, [(file, content)],
            SiteFactoryLockWriter.CreateCandidate(configuration, template));
    }
}
