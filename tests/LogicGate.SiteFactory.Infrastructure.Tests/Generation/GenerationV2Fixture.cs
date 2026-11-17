using System.Text;
using LogicGate.SiteFactory.Application.Generation;
using LogicGate.SiteFactory.Domain;
using LogicGate.SiteFactory.Infrastructure.Generation.V2;

namespace LogicGate.SiteFactory.Infrastructure.Tests.Generation;

/// <summary>Fictitious metadata only. No provider, DLL, filesystem or production serializer builds these golden bytes.</summary>
internal static class GenerationV2Fixture
{
    internal static readonly GeneratedContentHash ReportHash = new(new string('C', 64));
    internal static readonly GeneratedContentHash ConfigurationHash = new(new string('B', 64));
    internal static readonly GeneratedContentHash TextHash = new("E82841E061E76656FC74B8C561FCFE22A6930FBDD7E1F419B175E918D0893634");
    internal static readonly GeneratedContentHash BinaryHash = new("E9489F37FB3051E9EFA1DC916004D7274E7B63975E3209708947267F2393A9BE");

    internal static SiteFactoryLockV2 Lock(string type = "Example.Docs.TemplateProvider", string moduleId = "example-docs",
        string moduleVersion = "0.1.0", string artifactHash = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA",
        string template = "docs", string templateVersion = "1.0.0", string generatorVersion = "0.1.0-dev", string product = "documentation") =>
        new(new(generatorVersion), new(product), new(template), new(templateVersion),
            new(new(moduleId), new(moduleVersion), type, new(artifactHash)));

    internal static GenerationStateFileV2[] Files() =>
    [
        GenerationStateFileV2.Sdk(new("readme"), "README.md", FileGenerationStrategy.Generated, "page:readme", TextHash),
        GenerationStateFileV2.Sdk(new("mark"), "assets/mark.bin", FileGenerationStrategy.Generated, "asset:mark", BinaryHash),
        GenerationStateFileV2.Sdk(new("seed"), "notes.txt", FileGenerationStrategy.CreateOnce, "client:seed", null),
    ];

    internal static GenerationStateV2 State(SiteFactoryLockV2? composition = null, IEnumerable<GenerationStateFileV2>? files = null,
        GeneratedContentHash? configurationHash = null, string planId = "plan:demo:documentation:docs", string client = "demo") =>
        new(composition ?? Lock(), configurationHash ?? ConfigurationHash, new(planId), new ClientSlug(client), files ?? Files());

    internal static byte[] Bytes(string text) => Encoding.UTF8.GetBytes(text);
    internal static byte[] LockBytes => Bytes(GoldenLock + "\n");
    internal static byte[] StateBytes => Bytes(GoldenState + "\n");

    internal const string GoldenLock = """
        {
          "schemaVersion": 2,
          "generationStateSchemaVersion": 2,
          "siteFactoryVersion": "0.1.0-dev",
          "configurationSchemaVersion": 1,
          "product": "documentation",
          "template": {
            "id": "docs",
            "version": "1.0.0"
          },
          "provider": {
            "moduleId": "example-docs",
            "moduleVersion": "0.1.0",
            "type": "Example.Docs.TemplateProvider",
            "artifact": {
              "kind": "managed-assembly",
              "sha256": "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA"
            }
          }
        }
        """;

    internal const string GoldenState = """
        {
          "schemaVersion": 2,
          "generatorVersion": "0.1.0-dev",
          "configurationSha256": "BBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBB",
          "planId": "plan:demo:documentation:docs",
          "client": "demo",
          "lockSchemaVersion": 2,
          "files": [
            {
              "id": "readme",
              "path": "README.md",
              "strategy": "generated",
              "source": {
                "kind": "sdk",
                "contentKey": "page:readme"
              },
              "contentProfile": "exact-bytes",
              "lastGeneratedSha256": "E82841E061E76656FC74B8C561FCFE22A6930FBDD7E1F419B175E918D0893634"
            },
            {
              "id": "mark",
              "path": "assets/mark.bin",
              "strategy": "generated",
              "source": {
                "kind": "sdk",
                "contentKey": "asset:mark"
              },
              "contentProfile": "exact-bytes",
              "lastGeneratedSha256": "E9489F37FB3051E9EFA1DC916004D7274E7B63975E3209708947267F2393A9BE"
            },
            {
              "id": "seed",
              "path": "notes.txt",
              "strategy": "create-once",
              "source": {
                "kind": "sdk",
                "contentKey": "client:seed"
              },
              "contentProfile": null,
              "lastGeneratedSha256": null
            },
            {
              "id": "sitefactory-lock",
              "path": "sitefactory.lock.json",
              "strategy": "generated",
              "source": {
                "kind": "generator",
                "source": "composition"
              },
              "contentProfile": "utf8-lf",
              "lastGeneratedSha256": "032987868DFDF78E727689089B5E87944378D8A9C602BE6F84EC38C083F29842"
            }
          ],
          "reportSha256": "CCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCC"
        }
        """;
}
