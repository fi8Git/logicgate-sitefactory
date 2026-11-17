using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using LogicGate.SiteFactory.Application.Configuration;
using LogicGate.SiteFactory.Application.Generation;
using LogicGate.SiteFactory.Application.Templates;
using LogicGate.SiteFactory.Infrastructure.Generation;
using LogicGate.SiteFactory.Infrastructure.Generation.V2;
using Xunit;

namespace LogicGate.SiteFactory.Infrastructure.Tests.Generation;

public sealed class GenerationV2ContractTests
{
    private static readonly byte[] BinaryFixture = [0, 255, 13, 10];
    [Fact]
    public void V2ControlContractsExistWithoutAddingPublicApi()
    {
        foreach (string name in new[] { "SiteFactoryLockV2", "GenerationStateV2" })
        {
            Type? type = typeof(InitialGenerationExecutor).Assembly.GetType(
                "LogicGate.SiteFactory.Infrastructure.Generation.V2." + name);
            Assert.NotNull(type);
            Assert.False(type.IsVisible);
        }
        Assert.All(typeof(InitialGenerationExecutor).Assembly.GetTypes().Where(static type =>
            type.Namespace == "LogicGate.SiteFactory.Infrastructure.Generation.V2"), static type => Assert.False(type.IsVisible));
    }

    [Theory]
    [InlineData("module")]
    [InlineData("module-version")]
    [InlineData("module-build-identity")]
    [InlineData("provider-type")]
    [InlineData("provider-type-case")]
    [InlineData("artifact")]
    [InlineData("template")]
    [InlineData("template-version")]
    [InlineData("generator")]
    [InlineData("product")]
    public void DifferentValidBindingsCannotAdoptAnExistingLockOrState(string change)
    {
        SiteFactoryLockV2 changed = change switch
        {
            "module" => GenerationV2Fixture.Lock(moduleId: "other-docs"),
            "module-version" => GenerationV2Fixture.Lock(moduleVersion: "0.2.0"),
            "module-build-identity" => GenerationV2Fixture.Lock(moduleVersion: "0.1.0+other"),
            "provider-type" => GenerationV2Fixture.Lock(type: "Example.OtherProvider"),
            "provider-type-case" => GenerationV2Fixture.Lock(type: "example.Docs.TemplateProvider"),
            "artifact" => GenerationV2Fixture.Lock(artifactHash: new string('D', 64)),
            "template" => GenerationV2Fixture.Lock(template: "Docs"),
            "template-version" => GenerationV2Fixture.Lock(templateVersion: "2.0.0"),
            "generator" => GenerationV2Fixture.Lock(generatorVersion: "0.1.0-dev+other"),
            _ => GenerationV2Fixture.Lock(product: "other-product"),
        };
        Assert.NotEqual(GenerationV2Fixture.Lock(), changed);
        Assert.Null(changed.ReadMatching(GenerationV2Fixture.LockBytes));
        Assert.Null(GenerationV2Fixture.State(changed).ReadMatchingState(GenerationV2Fixture.StateBytes, GenerationV2Fixture.LockBytes));
        Assert.NotNull(SiteFactoryLockV2.Read(changed.Serialize()));
    }

    [Fact]
    public void OnlyPreviousSdkGeneratedHashesAndReportHashMayDifferFromTheCandidate()
    {
        GenerationStateFileV2[] files = GenerationV2Fixture.Files();
        files[0] = GenerationStateFileV2.Sdk(files[0].Id, files[0].Path, files[0].Strategy, files[0].Source.Value, new(new string('D', 64)));
        GenerationStateV2 candidate = GenerationV2Fixture.State(files: files);
        byte[] before = candidate.Serialize(new(new string('E', 64)));
        GenerationStateV2.Reference previous = Assert.IsType<GenerationStateV2.Reference>(candidate.ReadMatchingState(GenerationV2Fixture.StateBytes, GenerationV2Fixture.LockBytes));

        Assert.Equal(GenerationV2Fixture.TextHash, previous.State.Files[0].Hash);
        Assert.NotEqual(previous.State.Files[0].Hash, candidate.Files[0].Hash);
        Assert.Same(candidate.Files[0].Hash, files[0].Hash);
        Assert.Equal(GenerationV2Fixture.ReportHash, previous.ReportHash);
        Assert.Equal(GenerationV2Fixture.StateBytes, previous.State.Serialize(previous.ReportHash));
        Assert.Equal(before, candidate.Serialize(new(new string('E', 64))));
        Assert.Equal(candidate.Files[^1].Hash, previous.State.Files[^1].Hash);
    }

    [Fact]
    public void SourcesAreClosedAndSdkKeysRemainOpaqueOrdinalStrings()
    {
        const string key = " urn:docs/../page?name=Été\\part ";
        GenerationStateFileV2 file = GenerationStateFileV2.Sdk(new("opaque"), "file.txt", FileGenerationStrategy.Generated,
            key, GenerationV2Fixture.TextHash);
        GenerationStateV2 state = GenerationV2Fixture.State(files: [file]);
        byte[] bytes = state.Serialize(GenerationV2Fixture.ReportHash);
        GenerationStateV2.Reference previous = Assert.IsType<GenerationStateV2.Reference>(state.ReadMatchingState(bytes, GenerationV2Fixture.LockBytes));
        Assert.Equal(key, previous.State.Files[0].Source.Value);
        GenerationStateFileV2 other = GenerationStateFileV2.Sdk(file.Id, file.Path, file.Strategy, key.Trim(), file.Hash);
        Assert.Null(GenerationV2Fixture.State(files: [other]).ReadMatchingState(bytes, GenerationV2Fixture.LockBytes));
        Assert.All(typeof(GenerationContentSourceV2).GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic),
            static constructor => Assert.True(constructor.IsPrivate));
        Assert.True(typeof(GenerationContentSourceV2).IsSealed);
        JsonNode root = JsonNode.Parse(bytes)!;
        Assert.Equal("composition", root["files"]![1]!["source"]!["source"]!.GetValue<string>());
        Assert.Null(root["files"]![1]!["source"]!["contentKey"]);
    }

    [Theory]
    [InlineData("sdk-as-generator")]
    [InlineData("generator-as-sdk")]
    [InlineData("unknown-source")]
    [InlineData("wrong-key")]
    [InlineData("wrong-profile")]
    [InlineData("text-inference")]
    [InlineData("client-hash")]
    [InlineData("client-profile")]
    [InlineData("lock-hash")]
    [InlineData("internal-source")]
    [InlineData("file-id")]
    [InlineData("file-path")]
    [InlineData("strategy")]
    [InlineData("missing-entry")]
    [InlineData("reordered-files")]
    public void IncoherentSourcesOwnershipAndStructuralLockCannotProduceAReference(string change)
    {
        JsonNode root = JsonNode.Parse(GenerationV2Fixture.StateBytes)!;
        JsonArray files = Assert.IsType<JsonArray>(root["files"]);
        switch (change)
        {
            case "sdk-as-generator": files[0]!["source"]!["kind"] = "generator"; break;
            case "generator-as-sdk": files[3]!["source"]!["kind"] = "sdk"; break;
            case "unknown-source": files[0]!["source"]!["kind"] = "other"; break;
            case "wrong-key": files[0]!["source"]!["contentKey"] = "page:Readme"; break;
            case "wrong-profile": files[0]!["contentProfile"] = null; break;
            case "text-inference": files[0]!["contentProfile"] = "utf8-lf"; break;
            case "client-hash": files[2]!["lastGeneratedSha256"] = GenerationV2Fixture.TextHash.Value; break;
            case "client-profile": files[2]!["contentProfile"] = "exact-bytes"; break;
            case "lock-hash": files[3]!["lastGeneratedSha256"] = new string('0', 64); break;
            case "internal-source": files[3]!["source"]!["source"] = "other"; break;
            case "file-id": files[0]!["id"] = "other"; break;
            case "file-path": files[0]!["path"] = "elsewhere.md"; break;
            case "strategy": files[0]!["strategy"] = "create-once"; break;
            case "missing-entry": files.RemoveAt(0); break;
            default: (files[0], files[1]) = (files[1]!.DeepClone(), files[0]!.DeepClone()); break;
        }
        string changed = root.ToJsonString(new() { WriteIndented = true }) + "\n";
        Assert.NotEqual(GenerationV2Fixture.GoldenState + "\n", changed);
        Assert.Null(GenerationV2Fixture.State().ReadMatchingState(GenerationV2Fixture.Bytes(changed), GenerationV2Fixture.LockBytes));
    }

    [Fact]
    public void TextAndBinaryHashesAreRawAndCreateOnceHasNoOwnershipReference()
    {
        Assert.Equal(GenerationV2Fixture.TextHash.Value, Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes("Bonjour\r\n"))));
        Assert.Equal(GenerationV2Fixture.BinaryHash.Value, Convert.ToHexString(SHA256.HashData(BinaryFixture)));
        Assert.NotEqual(GenerationV2Fixture.TextHash.Value, Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes("Bonjour\n"))));
        GenerationStateV2 state = GenerationV2Fixture.State();
        Assert.Null(state.Files[2].Hash);
        Assert.Null(state.Files[2].ContentProfile);
        Assert.Equal("exact-bytes", state.Files[0].ContentProfile);
        Assert.Equal("utf8-lf", state.Files[^1].ContentProfile);
    }

    [Fact]
    public void InputCollectionsAreSnapshottedSortedAndNotExposedForMutation()
    {
        GenerationStateFileV2[] files = GenerationV2Fixture.Files().Reverse().ToArray();
        GenerationStateV2 state = GenerationV2Fixture.State(files: files);
        files[0] = files[1];
        Assert.Equal(GenerationV2Fixture.StateBytes, state.Serialize(GenerationV2Fixture.ReportHash));
        Assert.Throws<NotSupportedException>(() => ((IList<GenerationStateFileV2>)state.Files)[0] = files[0]);
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(254, true)]
    [InlineData(255, true)]
    [InlineData(256, false)]
    public void FileLimitIncludesExactlyOneReservedLock(int count, bool accepted)
    {
        GenerationStateFileV2[] files = Enumerable.Range(0, count).Select(static index => GenerationStateFileV2.Sdk(
            new("file-" + index), FormattableString.Invariant($"files/{index:D3}.txt"), FileGenerationStrategy.CreateOnce, "seed", null)).ToArray();
        if (!accepted) Assert.Throws<ArgumentException>(() => GenerationV2Fixture.State(files: files));
        else
        {
            GenerationStateV2 state = GenerationV2Fixture.State(files: files);
            Assert.Equal(count + 1, state.Files.Count);
            byte[] bytes = state.Serialize(GenerationV2Fixture.ReportHash);
            Assert.NotNull(state.ReadMatchingState(bytes, GenerationV2Fixture.LockBytes));
        }
    }

    [Fact]
    public void ExcessDeclarationsAreRejectedWithBoundedEnumeration()
    {
        int enumerated = 0;
        IEnumerable<GenerationStateFileV2> Unbounded()
        {
            while (true) { enumerated++; yield return GenerationV2Fixture.Files()[0]; }
        }
        Assert.Throws<ArgumentException>(() => GenerationV2Fixture.State(files: Unbounded()));
        Assert.Equal(GenerationStateV2.MaximumFiles, enumerated);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("line\nvalue")]
    [InlineData("nul\0value")]
    [InlineData("high-surrogate")]
    [InlineData("low-surrogate")]
    public void MetadataRejectsMissingControlOrInvalidUnicode(string value)
    {
        if (value == "high-surrogate") value = new string((char)0xD800, 1);
        if (value == "low-surrogate") value = new string((char)0xDC00, 1);
        Assert.ThrowsAny<ArgumentException>(() => GenerationV2Fixture.Lock(type: value));
        Assert.ThrowsAny<ArgumentException>(() => GenerationContentSourceV2.Sdk(value));
        Assert.ThrowsAny<ArgumentException>(() => GenerationV2Fixture.State(planId: value));
    }

    [Theory]
    [InlineData(FileGenerationStrategy.Custom)]
    [InlineData(FileGenerationStrategy.Protected)]
    [InlineData(FileGenerationStrategy.Merge)]
    [InlineData((FileGenerationStrategy)99)]
    public void SdkContractsCannotIntroduceOtherPolicies(FileGenerationStrategy strategy) =>
        Assert.Throws<ArgumentException>(() => GenerationStateFileV2.Sdk(new("file"), "file.txt", strategy, "key", null));

    [Theory]
    [InlineData("../outside")]
    [InlineData("/absolute")]
    [InlineData("bad\\path")]
    [InlineData(".git/config")]
    [InlineData("CON.txt")]
    [InlineData(".sitefactory/other")]
    [InlineData("sitefactory.lock.json")]
    [InlineData("GENERATION-REPORT.json")]
    public void MetadataCannotClaimUnsafeOrReservedTargets(string path) => Assert.Throws<ArgumentException>(() =>
        GenerationV2Fixture.State(files: [GenerationStateFileV2.Sdk(new("file"), path, FileGenerationStrategy.CreateOnce, "key", null)]));

    [Fact]
    public void DuplicateIdsPortableCaseAndAncestorCollisionsAreRejected()
    {
        foreach ((string first, string second, bool duplicateId) in new[]
        {
            ("one.txt", "two.txt", true), ("docs/a.txt", "Docs/b.txt", false),
            ("docs", "docs/a.txt", false), ("same.txt", "same.txt", false),
        })
            Assert.Throws<ArgumentException>(() => GenerationV2Fixture.State(files: [
                GenerationStateFileV2.Sdk(new("first"), first, FileGenerationStrategy.CreateOnce, "key", null),
                GenerationStateFileV2.Sdk(new(duplicateId ? "first" : "second"), second, FileGenerationStrategy.CreateOnce, "key", null)]));
        Assert.Throws<ArgumentException>(() => GenerationV2Fixture.State(files: [GenerationStateFileV2.Lock(GenerationV2Fixture.Lock())]));
        Assert.Throws<ArgumentException>(() => GenerationStateFileV2.Sdk(new("file"), "file.txt", FileGenerationStrategy.Generated, "key", null));
        Assert.Throws<ArgumentException>(() => GenerationStateFileV2.Sdk(new("file"), "file.txt", FileGenerationStrategy.CreateOnce, "key", GenerationV2Fixture.TextHash));
    }

    [Fact]
    public void ConfigurationPlanAndClientRemainPartOfExactStateRecognition()
    {
        Assert.Null(GenerationV2Fixture.State(configurationHash: new(new string('D', 64))).ReadMatchingState(GenerationV2Fixture.StateBytes, GenerationV2Fixture.LockBytes));
        Assert.Null(GenerationV2Fixture.State(planId: "another-plan").ReadMatchingState(GenerationV2Fixture.StateBytes, GenerationV2Fixture.LockBytes));
        Assert.Null(GenerationV2Fixture.State(client: "another-client").ReadMatchingState(GenerationV2Fixture.StateBytes, GenerationV2Fixture.LockBytes));
    }

    [Theory]
    [InlineData("foreign")]
    [InlineData("corrupt")]
    [InlineData("noncanonical")]
    [InlineData("v1")]
    [InlineData("absent")]
    public void StateReferenceRequiresTheActualMatchingCanonicalLockBytes(string change)
    {
        byte[] actual = change switch
        {
            "foreign" => GenerationV2Fixture.Lock(moduleId: "other-docs").Serialize(),
            "corrupt" => GenerationV2Fixture.Bytes("{"),
            "noncanonical" => GenerationV2Fixture.Bytes(GenerationV2Fixture.GoldenLock),
            "v1" => GenerationV2Fixture.Bytes(GenerationV2Fixture.GoldenLock.Replace("\"schemaVersion\": 2", "\"schemaVersion\": 1", StringComparison.Ordinal) + "\n"),
            _ => [],
        };
        Assert.Null(GenerationV2Fixture.State().ReadMatchingState(GenerationV2Fixture.StateBytes, actual));
    }

    [Theory]
    [InlineData("artifact-kind", "package")]
    [InlineData("configuration-version", "2")]
    [InlineData("module-id", "InvalidModule")]
    [InlineData("module-version", "1.*")]
    [InlineData("template-version", "1.0.0-preview")]
    [InlineData("generator-version", "latest")]
    [InlineData("provider-type", "")]
    public void UnsupportedLockValuesAreRefusedWithoutBindingOrFallback(string field, string value)
    {
        JsonNode root = JsonNode.Parse(GenerationV2Fixture.LockBytes)!;
        switch (field)
        {
            case "artifact-kind": root["provider"]!["artifact"]!["kind"] = value; break;
            case "configuration-version": root["configurationSchemaVersion"] = int.Parse(value, System.Globalization.CultureInfo.InvariantCulture); break;
            case "module-id": root["provider"]!["moduleId"] = value; break;
            case "module-version": root["provider"]!["moduleVersion"] = value; break;
            case "template-version": root["template"]!["version"] = value; break;
            case "generator-version": root["siteFactoryVersion"] = value; break;
            default: root["provider"]!["type"] = value; break;
        }
        byte[] bytes = GenerationV2Fixture.Bytes(root.ToJsonString(new() { WriteIndented = true }) + "\n");
        Assert.Null(SiteFactoryLockV2.Read(bytes));
        Assert.Null(GenerationV2Fixture.State().ReadMatchingState(GenerationV2Fixture.StateBytes, bytes));
    }

    [Fact]
    public void GenuineV1AndV2DocumentsCannotMigrateOrDowngradeEachOther()
    {
        ClientConfigurationV1 config = new(1, new("legacy-client", "Legacy Client"),
            new("legacy-product", "legacy-template", "fr-FR", "https://example.test"), new([]), new("docker-compose-vps"), new(true, false));
        TemplateManifest template = new(new("legacy-template"), new("1.0.0"), [new("legacy-product")],
            [new(new("file"), "file.txt", "source/file", FileGenerationStrategy.Generated)]);
        GenerationPlan plan = GenerationPlanBuilder.Build(new("legacy-plan"), config, template);
        SiteFactoryLock composition = new(new("0.1.0-dev"), config, template);
        GenerationState legacy = GenerationState.Create(new(plan, "legacy-client"), config, template,
            [(plan.Files[0], new GeneratedFileContent("Legacy\r\n"))], composition);
        byte[] v1State = legacy.Serialize(GenerationV2Fixture.ReportHash);
        byte[] v1Lock = SiteFactoryLockJsonSerializer.Serialize(composition);
        GenerationStateV2 sdk = GenerationV2Fixture.State();

        Assert.NotNull(legacy.ReadMatchingState(v1State));
        Assert.Null(legacy.ReadMatchingState(GenerationV2Fixture.StateBytes));
        Assert.Null(SiteFactoryLockV2.Read(v1Lock));
        Assert.Null(sdk.ReadMatchingState(v1State, v1Lock));
        Assert.Null(sdk.ReadMatchingState(v1State, GenerationV2Fixture.LockBytes));
        Assert.Null(sdk.ReadMatchingState(GenerationV2Fixture.StateBytes, v1Lock));
        Assert.Equal(v1State, legacy.Serialize(GenerationV2Fixture.ReportHash));
        Assert.Equal(v1Lock, SiteFactoryLockJsonSerializer.Serialize(composition));
    }
}
