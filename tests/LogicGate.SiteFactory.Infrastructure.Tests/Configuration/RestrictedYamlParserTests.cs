using LogicGate.SiteFactory.Infrastructure.Configuration;
using Xunit;
using YamlDotNet.Core;
using YamlDotNet.Core.Events;

namespace LogicGate.SiteFactory.Infrastructure.Tests.Configuration;

public sealed class RestrictedYamlParserTests
{
    [Fact]
    public void Utf32RangeFailureDuringMoveNextBecomesASyntaxDiagnostic()
    {
        RestrictedYamlParser parser = new(
            new FailingParser(new ArgumentOutOfRangeException("utf32", "UNTRUSTED_PARSER_DETAIL")),
            TestContext.Current.CancellationToken);

        ConfigurationYamlException exception = Assert.Throws<ConfigurationYamlException>(() => parser.MoveNext());

        Assert.Equal("SF-CONFIG-YAML-004", exception.Diagnostic.Code.Value);
        Assert.Equal("La syntaxe du document YAML est invalide.", exception.Diagnostic.Message);
        Assert.Null(exception.Diagnostic.PropertyPath);
        Assert.Null(exception.InnerException);
    }

    [Fact]
    public void UnrelatedParserFailuresAreNotTranslated()
    {
        Exception[] failures =
        [
            new ArgumentOutOfRangeException("index"),
            new ArgumentOutOfRangeException(),
            new ArgumentException("Programming error", "utf32"),
            new ArgumentOutOfRangeException("UTF32"),
            new NotSupportedException(),
            new OperationCanceledException(TestContext.Current.CancellationToken),
        ];

        foreach (Exception failure in failures)
        {
            RestrictedYamlParser parser = new(new FailingParser(failure), TestContext.Current.CancellationToken);

            Assert.Same(failure, Record.Exception(() => parser.MoveNext()));
        }
    }

    [Fact]
    public void Utf32RangeFailureOutsideMoveNextIsNotTranslated()
    {
        ArgumentOutOfRangeException failure = new("utf32");
        RestrictedYamlParser parser = new(new FailingParser(failure, failOnCurrent: true), TestContext.Current.CancellationToken);

        Assert.Same(failure, Record.Exception(() => parser.MoveNext()));
    }

    private sealed class FailingParser(Exception failure, bool failOnCurrent = false) : IParser
    {
        public ParsingEvent? Current => failOnCurrent ? throw failure : null;

        public bool MoveNext() => failOnCurrent ? true : throw failure;
    }
}
