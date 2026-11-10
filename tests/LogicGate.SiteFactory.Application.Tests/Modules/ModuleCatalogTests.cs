using System.Globalization;
using LogicGate.SiteFactory.Application.Configuration;
using LogicGate.SiteFactory.Application.Modules;
using LogicGate.SiteFactory.ModuleSdk;
using Xunit;

namespace LogicGate.SiteFactory.Application.Tests.Modules;

public sealed class ModuleCatalogTests
{
    [Fact]
    public void ExplicitCompositionResolvesMetadataWithoutKnowingTheConcreteModuleClass()
    {
        ISiteFactoryModule first = new SampleModule(Descriptor("alpha", capabilities: ["read"]));
        ISiteFactoryModule second = new AnotherModule();
        IModuleCatalog catalog = Assert.IsAssignableFrom<IModuleCatalog>(new ModuleCatalog([
            new("beta", "2.0.0", second), new("alpha", "1.0.0", first),
        ]));

        Assert.True(catalog.IsValid);
        Assert.Empty(catalog.Diagnostics);
        Assert.Equal(["alpha", "beta"], catalog.Modules.Select(static module => module.Id.Value));
        Assert.Equal(["alpha.read", "beta.write"], catalog.Capabilities.Select(static capability => capability.Value));
        ModuleResolutionResult resolved = catalog.Resolve(new("beta"));
        Assert.True(resolved.IsSuccess);
        Assert.Empty(resolved.Diagnostics);
        Assert.Same(second.Descriptor, resolved.Module);
        Assert.NotNull(resolved.Module);
        Assert.Equal(new ModuleVersion("2.0.0"), resolved.Module.Version);
    }

    [Fact]
    public void EmptyCatalogIsValidButUnknownModulesReturnStableDiagnostics()
    {
        ModuleCatalog catalog = new([]);
        Assert.True(catalog.IsValid);
        Assert.Empty(catalog.Modules);
        Assert.Empty(catalog.Capabilities);
        ModuleResolutionResult first = catalog.Resolve(new("absent"));
        ModuleResolutionResult repeated = catalog.Resolve(new("absent"));

        Assert.False(first.IsSuccess);
        Assert.Null(first.Module);
        Assert.Equal(first.Diagnostics, repeated.Diagnostics);
        AssertDiagnostic(Assert.Single(first.Diagnostics), "SF-MODULE-001", "modules[absent]",
            "Le module demandé est absent du catalogue.");
    }

    [Theory]
    [InlineData("1.0.0")]
    [InlineData("2.0.0")]
    [InlineData("1.0.0+other")]
    public void DuplicateIdIsRefusedRegardlessOfVersionOrInputOrder(string version)
    {
        ModuleRegistration one = Registration("alpha");
        ModuleRegistration two = Registration("alpha", version);
        foreach (ModuleRegistration[] order in new[] { new[] { one, two }, new[] { two, one }, new[] { one, one }, new[] { one, two, one } })
        {
            ModuleCatalog catalog = new(order);
            AssertInvalid(catalog);
            AssertDiagnostic(Assert.Single(catalog.Diagnostics), "SF-MODULE-005", "modules[alpha]",
                "Plusieurs enregistrements déclarent le même identifiant de module.");
        }
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("1")]
    [InlineData("1.0")]
    [InlineData("01.0.0")]
    [InlineData("1.0.0-beta.01")]
    [InlineData("1.0.0+")]
    [InlineData("v1.0.0")]
    [InlineData("latest")]
    [InlineData("1.*")]
    [InlineData(">=1.0.0")]
    [InlineData("1.0.0\n")]
    public void InvalidDeclaredVersionUsesSdkValidationAndDoesNotCallTheModule(string? version)
    {
        SampleModule module = new(Descriptor("alpha"));
        ModuleCatalog catalog = new([new("alpha", version, module)]);
        AssertInvalid(catalog);
        Assert.Equal(0, module.Reads);
        AssertDiagnostic(Assert.Single(catalog.Diagnostics), "SF-MODULE-003", "modules[alpha].version",
            "La version déclarée du module doit être une version SemVer exacte.");
    }

    [Theory]
    [InlineData("1.0.0")]
    [InlineData("1.0.0-beta.1")]
    [InlineData("1.0.0-beta.1+build.001")]
    [InlineData("999999999999999999999999.0.0")]
    public void CatalogAcceptsTheSameExactVersionsAsTheSdk(string version)
    {
        ModuleCatalog catalog = new([Registration("alpha", version)]);
        Assert.True(catalog.IsValid);
        Assert.Equal(new ModuleVersion(version), catalog.Resolve(new("alpha")).Module!.Version);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Alpha")]
    [InlineData("alpha.beta")]
    [InlineData("alpha ")]
    [InlineData("../alpha")]
    public void InvalidDeclaredIdHasItsOwnAdrDiagnostic(string? id)
    {
        SampleModule module = new(Descriptor("alpha"));
        ModuleCatalog catalog = new([new(id, "1.0.0", module)]);
        AssertInvalid(catalog);
        Assert.Equal(0, module.Reads);
        AssertDiagnostic(Assert.Single(catalog.Diagnostics), "SF-MODULE-002", "modules.id",
            "L'identifiant déclaré du module est invalide.");
    }

    [Fact]
    public void MissingRegistrationInstanceOrDescriptorIsInvalid()
    {
        ModuleCatalog missing = new([null]);
        AssertInvalid(missing);
        AssertDiagnostic(Assert.Single(missing.Diagnostics), "SF-MODULE-029", "modules",
            "L'enregistrement du module est absent ou incohérent avec son descripteur.");

        foreach (ISiteFactoryModule? module in new ISiteFactoryModule?[] { null, new SampleModule(null) })
        {
            ModuleCatalog catalog = new([new("alpha", "1.0.0", module)]);
            AssertInvalid(catalog);
            Assert.Equal("SF-MODULE-029", Assert.Single(catalog.Diagnostics).Code.Value);
            Assert.Equal("modules[alpha]", catalog.Diagnostics[0].PropertyPath);
        }
    }

    [Theory]
    [InlineData("beta", "1.0.0", "1.0.0")]
    [InlineData("alpha", "2.0.0", "1.0.0")]
    [InlineData("alpha", "1.0.0+one", "1.0.0+two")]
    [InlineData("alpha", "1.0.0-alpha", "1.0.0-Alpha")]
    public void RegistrationMustMatchTheExactDescriptorIdentity(string actualId, string actualVersion, string expectedVersion)
    {
        ModuleCatalog catalog = new([new("alpha", expectedVersion, new SampleModule(Descriptor(actualId, actualVersion)))]);
        AssertInvalid(catalog);
        Assert.Equal("SF-MODULE-029", Assert.Single(catalog.Diagnostics).Code.Value);
    }

    [Fact]
    public void InvalidCatalogCannotExposeOrResolveEvenAnUnrelatedValidModule()
    {
        ModuleCatalog catalog = new([Registration("alpha"), new("broken", "invalid", null)]);
        AssertInvalid(catalog);
        foreach (string id in new[] { "alpha", "broken", "unknown" })
        {
            ModuleResolutionResult result = catalog.Resolve(new(id));
            Assert.False(result.IsSuccess);
            Assert.Null(result.Module);
            Assert.Equal(catalog.Diagnostics, result.Diagnostics);
        }
    }

    [Theory]
    [InlineData("en-US")]
    [InlineData("fr-FR")]
    [InlineData("tr-TR")]
    public void EveryPermutationProducesTheSameOrdinalModulesAndCapabilities(string culture)
    {
        CultureInfo previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
            ModuleRegistration[] registrations =
            [
                Registration("zeta", capabilities: ["write", "read"]),
                Registration("alpha-2", capabilities: ["z", "a"]),
                Registration("alpha1", capabilities: ["read"]),
            ];
            ModuleCatalog baseline = new(registrations);
            Assert.Equal(["alpha-2", "alpha1", "zeta"], baseline.Modules.Select(static module => module.Id.Value));
            Assert.Equal(["alpha-2.a", "alpha-2.z", "alpha1.read", "zeta.read", "zeta.write"], baseline.Capabilities.Select(static capability => capability.Value));
            foreach (ModuleRegistration?[] order in Permutations(registrations))
            {
                ModuleCatalog catalog = new(order);
                Assert.Equal(baseline.Modules, catalog.Modules);
                Assert.Equal(baseline.Capabilities, catalog.Capabilities);
                Assert.Equal(baseline.Diagnostics, catalog.Diagnostics);
                Assert.Same(baseline.Resolve(new("alpha1")).Module, catalog.Resolve(new("alpha1")).Module);
            }
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    [Fact]
    public void EveryInvalidPermutationProducesTheSameCompleteDeduplicatedDiagnostics()
    {
        ModuleRegistration?[] registrations =
        [
            Registration("zeta"), Registration("zeta", "2.0.0"),
            new("alpha", "bad", null), new("beta", "1.0.0", null), null,
        ];
        ModuleCatalog baseline = new(registrations);
        Assert.Equal(["SF-MODULE-029", "SF-MODULE-003", "SF-MODULE-029", "SF-MODULE-005"],
            baseline.Diagnostics.Select(static diagnostic => diagnostic.Code.Value));
        foreach (ModuleRegistration?[] order in Permutations(registrations))
        {
            ModuleCatalog catalog = new(order);
            AssertInvalid(catalog);
            Assert.Equal(baseline.Diagnostics, catalog.Diagnostics);
        }
        Assert.Equal(baseline.Diagnostics,
            new ModuleCatalog(registrations.Concat([registrations[0], null])).Diagnostics);
    }

    [Fact]
    public void DuplicateIdsAreDetectedEvenWhenOneVersionIsInvalid()
    {
        ModuleCatalog catalog = new([Registration("alpha"), new("alpha", "bad", null)]);
        AssertInvalid(catalog);
        Assert.Equal(["SF-MODULE-005", "SF-MODULE-003"], catalog.Diagnostics.Select(static diagnostic => diagnostic.Code.Value));
    }

    [Fact]
    public void CatalogSnapshotsRegistrationsAndDescriptorsAndNeverCallsModulesDuringLookup()
    {
        ModuleDescriptor original = Descriptor("alpha", capabilities: ["read"]);
        SampleModule module = new(original);
        ModuleRegistration[] input = [new("alpha", "1.0.0", module)];
        ModuleCatalog catalog = new(input);
        input[0] = Registration("replacement");
        module.Current = Descriptor("changed", "2.0.0");

        Assert.Same(original, catalog.Resolve(new("alpha")).Module);
        Assert.Same(original, catalog.Resolve(new("alpha")).Module);
        Assert.Equal(1, module.Reads);
        Assert.Equal([new ModuleCapability("alpha.read")], catalog.Capabilities);
        Assert.Throws<NotSupportedException>(() => Assert.IsAssignableFrom<IList<ModuleDescriptor>>(catalog.Modules).Clear());
        Assert.Throws<NotSupportedException>(() => Assert.IsAssignableFrom<IList<ModuleCapability>>(catalog.Capabilities).Clear());
        Assert.Throws<NotSupportedException>(() => Assert.IsAssignableFrom<IList<ConfigurationDiagnostic>>(catalog.Diagnostics).Clear());
    }

    [Fact]
    public void DiagnosticOutputDoesNotEchoUnvalidatedStringsOrProviderDetails()
    {
        const string marker = "TEST-ONLY-sensitive-value";
        ModuleCatalog catalog = new([new(marker, "1.0.0", null), new("alpha", marker, null)]);
        Assert.Equal(2, catalog.Diagnostics.Count);
        Assert.All(catalog.Diagnostics, static diagnostic =>
        {
            Assert.DoesNotContain(marker, diagnostic.Message, StringComparison.Ordinal);
            Assert.DoesNotContain(marker, diagnostic.PropertyPath!, StringComparison.Ordinal);
        });
    }

    [Fact]
    public void GetterFailuresAreNotArbitrarilyConvertedToInvalidRegistrations()
    {
        foreach (Exception error in new Exception[]
                 { new InvalidOperationException("provider failure"), new ArgumentException("provider argument failure"), new OperationCanceledException() })
        {
            Assert.Same(error, Assert.Throws(error.GetType(), () => new ModuleCatalog([new("alpha", "1.0.0", new ThrowingModule(error))])));
        }
    }

    [Fact]
    public void NullCatalogInputAndLookupIdRemainProgrammingErrors()
    {
        Assert.Throws<ArgumentNullException>(() => new ModuleCatalog(null!));
        Assert.Throws<ArgumentNullException>(() => new ModuleCatalog([]).Resolve(null!));
    }

    [Fact]
    public void ResolutionResultCannotClaimFailureWithoutAnErrorOrSuccessWithoutMetadata()
    {
        Assert.Throws<ArgumentNullException>(() => ModuleResolutionResult.Success(null!));
        Assert.Throws<ArgumentNullException>(() => ModuleResolutionResult.Failure(null!));
        Assert.Throws<ArgumentException>(() => ModuleResolutionResult.Failure([]));
        Assert.Throws<ArgumentException>(() => ModuleResolutionResult.Failure([new(new("TEST"), DiagnosticSeverity.Warning, "warning")]));
    }

    private static void AssertInvalid(ModuleCatalog catalog)
    {
        Assert.False(catalog.IsValid);
        Assert.NotEmpty(catalog.Diagnostics);
        Assert.Empty(catalog.Modules);
        Assert.Empty(catalog.Capabilities);
        Assert.All(catalog.Diagnostics, static diagnostic => Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity));
    }

    private static void AssertDiagnostic(ConfigurationDiagnostic diagnostic, string code, string path, string message)
    {
        Assert.Equal(code, diagnostic.Code.Value);
        Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
        Assert.Equal(path, diagnostic.PropertyPath);
        Assert.Equal(message, diagnostic.Message);
    }

    private static ModuleDescriptor Descriptor(string id, string version = "1.0.0", params string[] capabilities) =>
        new(new(id), new(version), capabilities.Select(capability => new ModuleCapability(id + "." + capability)));

    private static ModuleRegistration Registration(string id, string version = "1.0.0", params string[] capabilities) =>
        new(id, version, new SampleModule(Descriptor(id, version, capabilities)));

    private static IEnumerable<ModuleRegistration?[]> Permutations(ModuleRegistration?[] items)
    {
        if (items.Length == 0)
        {
            yield return [];
            yield break;
        }

        for (int index = 0; index < items.Length; index++)
            foreach (ModuleRegistration?[] remainder in Permutations(items.Where((_, position) => position != index).ToArray()))
                yield return [items[index], .. remainder];
    }

    private sealed class SampleModule(ModuleDescriptor? descriptor) : ISiteFactoryModule
    {
        public ModuleDescriptor? Current { get; set; } = descriptor;
        public int Reads { get; private set; }
        public ModuleDescriptor Descriptor
        {
            get
            {
                Reads++;
                return Current!;
            }
        }
    }

    private sealed class AnotherModule : ISiteFactoryModule
    {
        public ModuleDescriptor Descriptor { get; } = new(new("beta"), new("2.0.0"), [new("beta.write")]);
    }

    private sealed class ThrowingModule(Exception error) : ISiteFactoryModule
    {
        public ModuleDescriptor Descriptor => throw error;
    }
}
