using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using LogicGate.SiteFactory.Infrastructure.Generation.V2;
using Xunit;

namespace LogicGate.SiteFactory.Infrastructure.Tests.Generation;

public sealed class GenerationV2CodecTests
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private static readonly string[] Mutations = ["missing", "unknown", "duplicate", "order", "type"];

    [Theory]
    [InlineData("en-US")]
    [InlineData("tr-TR")]
    [InlineData("ar-SA")]
    public void GoldenBytesRoundTripWithoutPhysicalResourcesOrCultureDependence(string culture)
    {
        CultureInfo previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
            SiteFactoryLockV2 composition = GenerationV2Fixture.Lock();
            Assert.Equal(GenerationV2Fixture.LockBytes, composition.Serialize());
            SiteFactoryLockV2 decoded = Assert.IsType<SiteFactoryLockV2>(SiteFactoryLockV2.Read(GenerationV2Fixture.LockBytes));
            Assert.Equal(composition, decoded);
            Assert.Equal(GenerationV2Fixture.LockBytes, decoded.Serialize());
            Assert.Equal(composition, composition.ReadMatching(GenerationV2Fixture.LockBytes));

            GenerationStateV2 state = GenerationV2Fixture.State();
            Assert.Equal(GenerationV2Fixture.StateBytes, state.Serialize(GenerationV2Fixture.ReportHash));
            GenerationStateV2.Reference reference = Assert.IsType<GenerationStateV2.Reference>(state.ReadMatchingState(GenerationV2Fixture.StateBytes, GenerationV2Fixture.LockBytes));
            Assert.Equal(GenerationV2Fixture.StateBytes, reference.State.Serialize(reference.ReportHash));
            Assert.Equal("032987868DFDF78E727689089B5E87944378D8A9C602BE6F84EC38C083F29842",
                Convert.ToHexString(SHA256.HashData(GenerationV2Fixture.LockBytes)));
        }
        finally { CultureInfo.CurrentCulture = previous; }
    }

    public static TheoryData<string, string, string> ObjectMutations()
    {
        TheoryData<string, string, string> data = new();
        foreach (string path in new[] { "", "template", "provider", "provider.artifact" })
            foreach (string mutation in Mutations) data.Add("lock", path, mutation);
        foreach (string path in new[] { "", "files.0", "files.0.source", "files.1", "files.1.source",
            "files.2", "files.2.source", "files.3", "files.3.source" })
            foreach (string mutation in Mutations) data.Add("state", path, mutation);
        return data;
    }

    [Theory]
    [MemberData(nameof(ObjectMutations))]
    public void EveryObjectRejectsMissingUnknownDuplicateReorderedAndWrongTypedFields(string scope, string path, string mutation)
    {
        byte[] original = scope == "lock" ? GenerationV2Fixture.LockBytes : GenerationV2Fixture.StateBytes;
        JsonObject template = Assert.IsType<JsonObject>(JsonNode.Parse(original));
        string[] names = ObjectAt(template, path).Select(static item => item.Key).ToArray();
        foreach (string name in mutation == "unknown" ? names.Take(1) : names)
        {
            JsonObject root = Assert.IsType<JsonObject>(JsonNode.Parse(original));
            JsonObject target = ObjectAt(root, path);
            KeyValuePair<string, JsonNode?>[] items = target.ToArray();
            if (mutation == "missing") Assert.True(target.Remove(name));
            else if (mutation == "unknown") target.Add("__unknown__", true);
            else if (mutation == "type") target[name] = WrongType(target[name]);
            else
            {
                target.Clear();
                if (mutation == "order")
                {
                    // Move each property to a different position, retaining its name and value.
                    int offset = Array.FindIndex(items, pair => pair.Key == name);
                    int other = (offset + 1) % items.Length;
                    (items[offset], items[other]) = (items[other], items[offset]);
                }
                foreach ((string key, JsonNode? value) in items)
                {
                    target.Add(key, value);
                    if (mutation == "duplicate" && key == name) target.Add("__duplicate__", value?.DeepClone());
                }
            }
            string changed = root.ToJsonString(JsonOptions) + "\n";
            if (mutation == "duplicate")
                changed = changed.Replace("\"__duplicate__\":", JsonSerializer.Serialize(name) + ":", StringComparison.Ordinal);
            Assert.NotEqual(Encoding.UTF8.GetString(original), changed);
            AssertRejected(scope, GenerationV2Fixture.Bytes(changed));
        }
    }

    public static TheoryData<string, string> TransportMutations()
    {
        TheoryData<string, string> data = new();
        foreach (string scope in new[] { "lock", "state" })
            foreach (string mutation in new[] { "bom", "crlf", "no-lf", "two-lfs", "leading-space", "trailing-space", "tab",
                "compact", "null", "array", "empty", "broken", "comment", "comma", "utf8", "surrogate-value", "surrogate-name",
                "escaped-name", "schema-v1", "schema-v3", "schema-float", "schema-string", "lower-hash", "short-hash",
                "long-hash", "bad-hash", "pair-v1", "deep" }) data.Add(scope, mutation);
        return data;
    }

    [Theory]
    [MemberData(nameof(TransportMutations))]
    public void StrictTransportVersionsUnicodeAndHashesCannotProduceAReference(string scope, string mutation)
    {
        string original = scope == "lock" ? GenerationV2Fixture.GoldenLock + "\n" : GenerationV2Fixture.GoldenState + "\n";
        string hash = new(scope == "lock" ? 'A' : 'C', 64);
        string changed = mutation switch
        {
            "bom" => "\uFEFF" + original,
            "crlf" => original.Replace("\n", "\r\n", StringComparison.Ordinal),
            "no-lf" => original.TrimEnd(),
            "two-lfs" => original + "\n",
            "leading-space" => " " + original,
            "trailing-space" => original + " ",
            "tab" => original.Replace("  ", "\t", StringComparison.Ordinal),
            "compact" => JsonNode.Parse(original)!.ToJsonString(),
            "null" => "null\n",
            "array" => "[]\n",
            "empty" => "",
            "broken" => "{",
            "comment" => "/*comment*/" + original,
            "comma" => original.TrimEnd()[..^1] + ",}\n",
            "surrogate-value" => original.Replace(scope == "lock" ? "documentation" : "demo", "\\uD800", StringComparison.Ordinal),
            "surrogate-name" => original.Replace("schemaVersion", "schemaVersion\\uD800", StringComparison.Ordinal),
            "escaped-name" => original.Replace("schemaVersion", "\\u0073chemaVersion", StringComparison.Ordinal),
            "schema-v1" => original.Replace("\"schemaVersion\": 2", "\"schemaVersion\": 1", StringComparison.Ordinal),
            "schema-v3" => original.Replace("\"schemaVersion\": 2", "\"schemaVersion\": 3", StringComparison.Ordinal),
            "schema-float" => original.Replace("\"schemaVersion\": 2", "\"schemaVersion\": 2.0", StringComparison.Ordinal),
            "schema-string" => original.Replace("\"schemaVersion\": 2", "\"schemaVersion\": \"2\"", StringComparison.Ordinal),
            "lower-hash" => original.Replace(hash, hash.ToLowerInvariant(), StringComparison.Ordinal),
            "short-hash" => original.Replace(hash, hash[..^1], StringComparison.Ordinal),
            "long-hash" => original.Replace(hash, hash + "A", StringComparison.Ordinal),
            "bad-hash" => original.Replace(hash, new string('Z', 64), StringComparison.Ordinal),
            "pair-v1" => original.Replace(scope == "lock" ? "\"generationStateSchemaVersion\": 2" : "\"lockSchemaVersion\": 2",
                scope == "lock" ? "\"generationStateSchemaVersion\": 1" : "\"lockSchemaVersion\": 1", StringComparison.Ordinal),
            "deep" => new string('[', 17) + "0" + new string(']', 17),
            _ => original,
        };
        byte[] bytes = GenerationV2Fixture.Bytes(changed);
        if (mutation == "utf8")
        {
            int index = bytes.AsSpan().IndexOf("\"schemaVersion\""u8);
            Assert.True(index > 0);
            bytes[index + 2] = 0xFF;
        }
        else Assert.NotEqual(original, changed);
        AssertRejected(scope, bytes);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(1)]
    public void LockEnforcesItsActualEncodedByteBoundary(int delta)
    {
        int baseLength = GenerationV2Fixture.LockBytes.Length;
        string type = new('T', "Example.Docs.TemplateProvider".Length + GenerationV2Json.MaximumLockLength - baseLength + delta);
        byte[] input = GenerationV2Fixture.Bytes((GenerationV2Fixture.GoldenLock + "\n").Replace(
            "Example.Docs.TemplateProvider", type, StringComparison.Ordinal));
        Assert.Equal(GenerationV2Json.MaximumLockLength + delta, input.Length);
        SiteFactoryLockV2 candidate = GenerationV2Fixture.Lock(type: type);
        if (delta > 0)
        {
            Assert.Throws<ArgumentException>(() => candidate.Serialize());
            Assert.Null(SiteFactoryLockV2.Read(input));
        }
        else
        {
            Assert.Equal(input, candidate.Serialize());
            Assert.Equal(candidate, candidate.ReadMatching(input));
        }
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(1)]
    public void StateEnforcesItsActualEncodedByteBoundary(int delta)
    {
        const string originalPlan = "plan:demo:documentation:docs";
        string plan = new('p', originalPlan.Length + GenerationV2Json.MaximumStateLength - GenerationV2Fixture.StateBytes.Length + delta);
        byte[] input = GenerationV2Fixture.Bytes((GenerationV2Fixture.GoldenState + "\n").Replace(originalPlan, plan, StringComparison.Ordinal));
        Assert.Equal(GenerationV2Json.MaximumStateLength + delta, input.Length);
        GenerationStateV2 candidate = GenerationV2Fixture.State(planId: plan);
        if (delta > 0)
        {
            Assert.Throws<ArgumentException>(() => candidate.Serialize(GenerationV2Fixture.ReportHash));
            Assert.Null(candidate.ReadMatchingState(input, GenerationV2Fixture.LockBytes));
        }
        else
        {
            Assert.Equal(input, candidate.Serialize(GenerationV2Fixture.ReportHash));
            GenerationStateV2.Reference reference = Assert.IsType<GenerationStateV2.Reference>(candidate.ReadMatchingState(input, GenerationV2Fixture.LockBytes));
            Assert.Equal(input, reference.State.Serialize(reference.ReportHash));
        }
    }

    [Theory]
    [InlineData(16, true)]
    [InlineData(17, false)]
    public void JsonDepthLimitIsInclusive(int depth, bool accepted)
    {
        using JsonDocument? parsed = GenerationV2Json.Parse(GenerationV2Fixture.Bytes(new string('[', depth) + "0" + new string(']', depth)), 1024);
        Assert.Equal(accepted, parsed is not null);
    }

    [Fact]
    public void EscapedUnicodeBudgetCountsEncodedBytesRatherThanCharacters()
    {
        string type = new('é', GenerationV2Json.MaximumLockLength / 3);
        Assert.True(Encoding.UTF8.GetByteCount(type) < GenerationV2Json.MaximumLockLength);
        Assert.Throws<ArgumentException>(() => GenerationV2Fixture.Lock(type: type).Serialize());
    }

    private static JsonObject ObjectAt(JsonObject root, string path)
    {
        JsonNode current = root;
        if (path.Length != 0)
            foreach (string part in path.Split('.'))
                current = current is JsonArray array ? array[int.Parse(part, CultureInfo.InvariantCulture)]! : current[part]!;
        return Assert.IsType<JsonObject>(current);
    }

    private static JsonNode WrongType(JsonNode? value) => value switch
    {
        JsonObject => new JsonArray(),
        JsonArray => new JsonObject(),
        JsonValue scalar when scalar.TryGetValue<int>(out _) => JsonValue.Create("wrong")!,
        _ => JsonValue.Create(true)!,
    };

    private static void AssertRejected(string scope, byte[] bytes)
    {
        if (scope == "lock")
        {
            Assert.Null(SiteFactoryLockV2.Read(bytes));
            Assert.Null(GenerationV2Fixture.Lock().ReadMatching(bytes));
        }
        else Assert.Null(GenerationV2Fixture.State().ReadMatchingState(bytes, GenerationV2Fixture.LockBytes));
    }
}
