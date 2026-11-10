using System.Diagnostics;
using System.Text;
using System.Text.Json;
using LogicGate.SiteFactory.Cli.Tests;
using Xunit;

namespace LogicGate.SiteFactory.Infrastructure.PostgreSql.Tests.PostgreSql;

// P04-A/B: actual copied PowerShell functions, without database execution.
public sealed class PostgreSqlProofTests
{
    private static readonly string[] ExpectedShards = ["g04", "g05", "p01", "p02", "p03"];
    private static readonly int[] ExpectedAssemblyCounts = [20, 3];

    [Fact]
    public void LiteralInventoriesKeepTheGateAndHostsSeparate()
    {
        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(Resource("shards.json")));
        JsonElement manifest = document.RootElement;
        Dictionary<string, string[]> inventory = manifest.GetProperty("shards").EnumerateArray().ToDictionary(
            static shard => shard.GetProperty("name").GetString()!,
            static shard => shard.GetProperty("expectedTests").EnumerateArray().Select(static test => test.GetString()!).ToArray(),
            StringComparer.Ordinal);
        Assert.Equal(ExpectedShards, inventory.Keys.Order(StringComparer.Ordinal));
        Assert.Equal(PostgreSqlTestGate.ExpectedTests, inventory["g04"]);
        Assert.Equal(PostgreSqlTestGate.ExpectedG05Tests, inventory["g05"]);
        Assert.Equal(PostgreSqlTestGate.ExpectedP01Tests, inventory["p01"]);
        Assert.Equal(PostgreSqlTestGate.ExpectedP02Tests, inventory["p02"]);
        PostgreSqlTestGate.ValidateInventories(inventory["g04"], inventory["g05"], inventory["p01"], inventory["p02"],
            inventory["g04"].Concat(inventory["g05"]).Concat(inventory["p01"]).Concat(inventory["p02"]));
        const string hosts = "LogicGate.SiteFactory.Runtime.PostgreSql.Hosts.Tests.PostgreSql.";
        Assert.Equal(new[] {
            hosts + "PostgreSqlWebReadinessTests.RealWebReadinessUsesPostgreSqlWithoutWritingAndKeepsLivenessIndependent",
            hosts + "PostgreSqlWorkerReadinessTests.RealWorkerValidatesItsOwnDatabaseBeforeStartingAndRemainsIdleWithoutWrites",
            hosts + "PostgreSqlWebConcurrencyTests.RealWebReadinessHasNoFalseFailuresUnderConcurrentTraffic",
        }, inventory["p03"]);
        Assert.Equal(23, manifest.GetProperty("expectedTests").GetArrayLength());
        Assert.Equal(ExpectedAssemblyCounts, manifest.GetProperty("assemblies").EnumerateArray()
            .Select(static assembly => assembly.GetProperty("expectedCount").GetInt32()));
    }

    [Fact]
    public async Task ActualFunctionsAcceptOnlyTheCompleteDisjointPartitionAndExactDiscovery()
    {
        CliBytesResult result = await RunAsync("""
            $partition = Read-PublicPostgreSqlPartition $manifest
            foreach ($assembly in $partition.assemblies) {
                $expected = @($partition.expectedTests | Where-Object assembly -CEQ $assembly.name | ForEach-Object fullyQualifiedName)
                $output = "VSTest header`r`n" + (($expected | ForEach-Object { '    ' + $_ }) -join "`r`n") + "`r`n"
                Assert-PublicPostgreSqlDiscovery $output $expected
            }
            foreach ($shard in $partition.shards) {
                $output = ($shard.expectedTests | ForEach-Object { '    ' + $_ }) -join "`n"
                Assert-PublicPostgreSqlDiscovery $output $shard.expectedTests
            }
            """);
        AssertResult(result, 0, "", "");
    }

    [Theory]
    [InlineData("missing-file")]
    [InlineData("invalid-json")]
    [InlineData("schema-version")]
    [InlineData("unknown-schema-key")]
    [InlineData("missing-shard")]
    [InlineData("unknown-shard")]
    [InlineData("empty-shard")]
    [InlineData("duplicate-shard")]
    [InlineData("missing-member")]
    [InlineData("unexpected-member")]
    [InlineData("duplicate-member")]
    [InlineData("intersection")]
    [InlineData("missing-identity")]
    [InlineData("extra-identity")]
    [InlineData("duplicate-identity")]
    [InlineData("wrong-assembly")]
    [InlineData("wrong-project")]
    [InlineData("wrong-lot")]
    [InlineData("wrong-identity-owner")]
    [InlineData("wrong-filter")]
    public async Task InvalidPartitionsFailForThePartitionReasonWithoutDisclosure(string scenario)
    {
        CliBytesResult result = await RunAsync("$case = " + Quote(scenario) + "\n" + """
            $broken = [IO.File]::ReadAllText($manifest) | ConvertFrom-Json -AsHashtable
            $path = Join-Path $working 'broken.json'
            switch ($case) {
                schema-version { $broken.schemaVersion = 2 }
                unknown-schema-key { $broken.extra = 'SENSITIVE_TEST_MARKER' }
                missing-shard { $broken.shards = @($broken.shards | Where-Object name -CNE g04) }
                unknown-shard { $broken.shards[0].name = 'unknown' }
                empty-shard { $broken.shards[0].expectedTests = @() }
                duplicate-shard { $broken.shards[1] = $broken.shards[0] }
                missing-member { $broken.shards[0].expectedTests = @($broken.shards[0].expectedTests | Select-Object -Skip 1) }
                unexpected-member { $broken.shards[0].expectedTests[0] = 'SENSITIVE_TEST_MARKER.Unknown.Fact' }
                duplicate-member { $broken.shards[0].expectedTests[1] = $broken.shards[0].expectedTests[0] }
                intersection { $broken.shards[1].expectedTests[0] = $broken.shards[0].expectedTests[0] }
                missing-identity { $broken.expectedTests = @($broken.expectedTests | Select-Object -Skip 1) }
                extra-identity { $broken.expectedTests += $broken.expectedTests[0] }
                duplicate-identity { $broken.expectedTests[1] = $broken.expectedTests[0] }
                wrong-assembly { $broken.shards[0].assembly = $broken.assemblies[1].name }
                wrong-project { $broken.shards[0].project = '../SENSITIVE_TEST_MARKER.csproj' }
                wrong-lot { $broken.shards[0].lot = 'P03' }
                wrong-identity-owner { $broken.expectedTests[0].assembly = $broken.assemblies[1].name }
                wrong-filter { $broken.shards[0].filter = 'Category!=PostgreSql' }
            }
            if ($case -cne 'missing-file') {
                $json = if ($case -ceq 'invalid-json') { 'SENSITIVE_TEST_MARKER{' } else { $broken | ConvertTo-Json -Depth 10 }
                [IO.File]::WriteAllText($path, $json)
            }
            try { $null = Read-PublicPostgreSqlPartition $path; throw 'Unexpected acceptance.' }
            catch {
                if (!$_.Exception.Message.StartsWith('SF-TEST-PG-PARTITION', [StringComparison]::Ordinal)) { throw }
                if ($_.Exception.Message.Contains('SENSITIVE_TEST_MARKER')) { throw 'Disclosure.' }
                [Console]::Error.WriteLine('SF-TEST-PG-PARTITION Rejected.')
                exit 1
            }
            """);
        AssertResult(result, 1, "", "SF-TEST-PG-PARTITION Rejected.\n");
    }

    [Theory]
    [InlineData("empty")]
    [InlineData("missing")]
    [InlineData("unexpected")]
    [InlineData("duplicate")]
    [InlineData("wrong-assembly")]
    [InlineData("contradictory-lot")]
    [InlineData("nonzero")]
    [InlineData("unrecognized-display-name")]
    [InlineData("malformed-extra-line")]
    public async Task DiscoveryRejectsEveryMismatchAndNonzeroExitWithoutDisclosure(string scenario)
    {
        CliBytesResult result = await RunAsync("$case = " + Quote(scenario) + "\n" + """
            $partition = Read-PublicPostgreSqlPartition $manifest
            $expected = @($partition.shards[1].expectedTests)
            $actual = @($expected)
            $code = 0
            switch ($case) {
                empty { $actual = @() }
                missing { $actual = @($actual | Select-Object -Skip 1) }
                unexpected { $actual += 'SENSITIVE_TEST_MARKER.Unknown.Fact' }
                duplicate { $actual += $actual[0] }
                wrong-assembly { $actual[0] = $partition.shards[4].expectedTests[0] }
                contradictory-lot { $actual = @($partition.shards[2].expectedTests) }
                nonzero { $code = 7 }
                unrecognized-display-name { $actual += $actual[0] + '(SENSITIVE_TEST_MARKER)' }
                malformed-extra-line { $actual += 'SENSITIVE_TEST_MARKER' }
            }
            $output = "provider SENSITIVE_TEST_MARKER`n" + (($actual | ForEach-Object { '    ' + $_ }) -join "`n")
            try { Assert-PublicPostgreSqlDiscovery $output $expected $code; throw 'Unexpected acceptance.' }
            catch {
                if (!$_.Exception.Message.StartsWith('SF-TEST-PG-DISCOVERY', [StringComparison]::Ordinal)) { throw }
                if ($_.Exception.Message.Contains('SENSITIVE_TEST_MARKER')) { throw 'Disclosure.' }
                [Console]::Error.WriteLine('SF-TEST-PG-DISCOVERY Rejected.')
                exit 1
            }
            """);
        AssertResult(result, 1, "", "SF-TEST-PG-DISCOVERY Rejected.\n");
    }

    [Theory]
    [InlineData("valid", 0)]
    [InlineData("nonzero", 1)]
    [InlineData("contradictory-lot", 1)]
    [InlineData("launch-failure", 1)]
    public async Task EntryPointUsesOnlyOfflineDiscoveryAndNeverPublishesAFalseProof(string scenario, int exitCode)
    {
        CliBytesResult result = await RunAsync("$case = " + Quote(scenario) + "\n" + """
            $global:queries = 0
            $global:partition = Read-PublicPostgreSqlPartition $manifest
            function global:dotnet {
                $global:queries++
                $commandArgs = @($args)
                if ($commandArgs[0] -cne 'test' -or $commandArgs -cnotcontains '--list-tests' -or
                    $commandArgs -cnotcontains '--no-build' -or $commandArgs -cnotcontains '--no-restore') { throw 'Execution attempted.' }
                if ($case -ceq 'launch-failure') { throw 'SENSITIVE_TEST_MARKER' }
                $filter = $commandArgs[[array]::IndexOf($commandArgs, '--filter') + 1]
                $assembly = @($global:partition.assemblies | Where-Object { (Join-Path $working $_.project) -ceq $commandArgs[1] })
                if ($assembly.Count -ne 1) { throw 'Unexpected project.' }
                $expected = if ($filter -ceq 'Category=PostgreSql') {
                    @($global:partition.expectedTests | Where-Object assembly -CEQ $assembly[0].name | ForEach-Object fullyQualifiedName)
                } else {
                    $shard = @($global:partition.shards | Where-Object { $_.filter -ceq $filter -or ('ExtractionLot=' + $_.lot) -ceq $filter })
                    if ($shard.Count -ne 1) { throw 'Unexpected filter.' }
                    if ($case -ceq 'contradictory-lot' -and $filter -ceq 'ExtractionLot=G05') {
                        @($global:partition.shards[2].expectedTests)
                    } else { @($shard[0].expectedTests) }
                }
                $global:LASTEXITCODE = if ($case -ceq 'nonzero') { 7 } else { 0 }
                'SENSITIVE_TEST_MARKER'
                $expected | ForEach-Object { '    ' + $_ }
            }
            # The entrypoint delegates these same eleven queries to this function.
            # Inject only controlled process transport, never a copy of the inventory decision.
            try {
                $null = Invoke-PublicPostgreSqlInventories $global:partition {
                    param($project,$filter)
                    $output = (& dotnet test (Join-Path $working $project) --configuration Release --no-build --no-restore --list-tests --filter $filter | Out-String)
                    return @{Status='Completed';ExitCode=$LASTEXITCODE;Output=$output;Error='';Cleanup='Completed';ElapsedMs=1}
                }
                Write-Output 'SF-TEST-PG-INVENTORY Offline discovery verified: Infrastructure=20, Hosts=3, disjoint shards=5. No PostgreSQL test executed; no database proof.'
                $code = 0
            } catch {
                [Console]::Error.WriteLine('SF-TEST-PG-DISCOVERY Public discovery failed or differed from the inventory.')
                $code = 1
            }
            if ($case -ceq 'valid' -and $global:queries -ne 11) { throw 'Missing discovery query.' }
            exit $code
            """);
        AssertResult(result, exitCode, exitCode == 0
            ? "SF-TEST-PG-INVENTORY Offline discovery verified: Infrastructure=20, Hosts=3, disjoint shards=5. No PostgreSQL test executed; no database proof.\n" : "",
            exitCode == 0 ? "" : "SF-TEST-PG-DISCOVERY Public discovery failed or differed from the inventory.\n");
    }

    [Fact]
    public async Task TrxProofRequiresEveryExactResultAndStripsSensitiveTransportDetails()
    {
        CliBytesResult result = await RunAsync(ProofFixtures + """
            $campaign = New-PublicPostgreSqlCampaign $working $manifest -Commit ('a' * 40) -GitState Clean -Sdk '10.0.301' -Runtime '10.0.9'
            foreach ($shard in $campaign.Partition.shards) {
                $invocation = Start-PublicPostgreSqlShardProof $campaign $shard.name $windowStart
                $document = New-FixtureTrx $invocation
                # The display names are deliberately all identical/truncated: identity comes from definitions.
                $document.TestRun.TestDefinitions.UnitTest | ForEach-Object { $_.SetAttribute('storage', ($shard.assembly.ToLowerInvariant() + '.dll')) }
                # GUID casing is not identity: alternate representations still link correctly.
                $document.TestRun.TestDefinitions.UnitTest | ForEach-Object { $_.SetAttribute('id', $_.GetAttribute('id').ToUpperInvariant()) }
                $original = $document.OuterXml
                Write-FixtureTrx $campaign $invocation $document
                $proof = Save-PublicPostgreSqlProof $campaign $invocation (New-FixtureReceipt $campaign $invocation) (Fixture-Discovery $invocation)
                if ($proof.status -cne 'PASSED' -or $proof.completeness -cne 'Complete' -or
                    $proof.results.Count -ne $shard.expectedCount -or !(Assert-PublicPostgreSqlExact @($proof.results.fullyQualifiedName) $shard.expectedTests)) { throw 'Incomplete proof.' }
                $native = Resolve-PublicPostgreSqlEvidencePath $campaign.Root $proof.native
                if ([IO.File]::ReadAllText($native) -cne $original) { throw 'Native archive changed.' }
                if ([IO.File]::Exists((Join-Path $campaign.Root $invocation.TrxRelative))) { throw 'Incoming retained.' }
            }
            $saved = [IO.File]::ReadAllText((Join-Path $campaign.Root 'manifest.json')) | ConvertFrom-Json -AsHashtable
            if ($saved.status -cne 'PASSED' -or $saved.provenance -cne 'SyntheticFixture' -or $saved.databaseProof -or
                $null -ne $saved.postgreSql -or $null -ne $saved.imageDigest -or $saved.cleanup -cne 'NotObserved') { throw 'False database proof.' }
            """);
        AssertResult(result, 0, "", "");
    }

    [Theory]
    [InlineData("missing", "SF-TEST-PG-MISSING")]
    [InlineData("empty", "SF-TEST-PG-XML")]
    [InlineData("malformed", "SF-TEST-PG-XML")]
    [InlineData("dtd", "SF-TEST-PG-XML")]
    [InlineData("oversized", "SF-TEST-PG-XML")]
    [InlineData("wrong-root", "SF-TEST-PG-STRUCTURE")]
    [InlineData("wrong-namespace", "SF-TEST-PG-STRUCTURE")]
    [InlineData("missing-definition", "SF-TEST-PG-INVENTORY")]
    [InlineData("duplicate-definition", "SF-TEST-PG-LINK")]
    [InlineData("guid-case-alias", "SF-TEST-PG-LINK")]
    [InlineData("missing-result", "SF-TEST-PG-INVENTORY")]
    [InlineData("duplicate-result", "SF-TEST-PG-LINK")]
    [InlineData("extra-result", "SF-TEST-PG-INVENTORY")]
    [InlineData("unexpected-test", "SF-TEST-PG-INVENTORY")]
    [InlineData("wrong-id", "SF-TEST-PG-LINK")]
    [InlineData("wrong-execution", "SF-TEST-PG-LINK")]
    [InlineData("wrong-entry", "SF-TEST-PG-LINK")]
    [InlineData("hidden-result", "SF-TEST-PG-INVENTORY")]
    [InlineData("wrong-assembly", "SF-TEST-PG-ASSEMBLY")]
    [InlineData("wrong-storage", "SF-TEST-PG-ASSEMBLY")]
    [InlineData("Failed", "SF-TEST-PG-OUTCOME")]
    [InlineData("Skipped", "SF-TEST-PG-OUTCOME")]
    [InlineData("NotExecuted", "SF-TEST-PG-OUTCOME")]
    [InlineData("Aborted", "SF-TEST-PG-OUTCOME")]
    [InlineData("unknown-outcome", "SF-TEST-PG-OUTCOME")]
    [InlineData("summary-aborted", "SF-TEST-PG-OUTCOME")]
    [InlineData("counter", "SF-TEST-PG-COUNTERS")]
    [InlineData("missing-counter", "SF-TEST-PG-COUNTERS")]
    [InlineData("counter-overflow", "SF-TEST-PG-COUNTERS")]
    [InlineData("duration", "SF-TEST-PG-TIME")]
    [InlineData("negative-duration", "SF-TEST-PG-TIME")]
    [InlineData("invalid-time", "SF-TEST-PG-TIME")]
    [InlineData("stale-run", "SF-TEST-PG-PROVENANCE")]
    [InlineData("mixed-runs", "SF-TEST-PG-TIME")]
    [InlineData("wrong-campaign", "SF-TEST-PG-PROVENANCE")]
    [InlineData("wrong-invocation", "SF-TEST-PG-PROVENANCE")]
    [InlineData("nonzero", "SF-TEST-PG-EXIT")]
    [InlineData("timeout", "SF-TEST-PG-TIMEOUT")]
    [InlineData("cancelled", "SF-TEST-PG-CANCELLED")]
    [InlineData("bad-discovery", "SF-TEST-PG-DISCOVERY")]
    [InlineData("path-escape", "SF-TEST-PG-PATH")]
    public async Task TrxCounterproofsRemainRedAndNeverPublishUntrustedContent(string scenario, string code)
    {
        CliBytesResult result = await RunAsync(ProofFixtures + "$case = " + Quote(scenario) + "\n$expectedCode = " + Quote(code) + "\n" + """
            $campaign = New-PublicPostgreSqlCampaign $working $manifest
            $invocation = Start-PublicPostgreSqlShardProof $campaign g05 $windowStart
            $document = New-FixtureTrx $invocation
            $receipt = New-FixtureReceipt $campaign $invocation
            $discovery = Fixture-Discovery $invocation
            $definitions = $document.TestRun.TestDefinitions
            $results = $document.TestRun.Results
            $entries = $document.TestRun.TestEntries
            $first = $results.UnitTestResult[0]
            switch ($case) {
                wrong-root { $document = [xml] '<Other/>' }
                missing-definition { $null = $definitions.RemoveChild($definitions.UnitTest[0]) }
                duplicate-definition { $null = $definitions.ReplaceChild($definitions.UnitTest[0].CloneNode($true), $definitions.UnitTest[1]) }
                guid-case-alias {
                    $alias = $definitions.UnitTest[0].GetAttribute('id').ToUpperInvariant()
                    $definitions.UnitTest[1].SetAttribute('id', $alias)
                    $results.UnitTestResult[1].SetAttribute('testId', $alias)
                    $entries.TestEntry[1].SetAttribute('testId', $alias)
                }
                missing-result { $null = $results.RemoveChild($first) }
                duplicate-result { $null = $results.ReplaceChild($first.CloneNode($true), $results.UnitTestResult[1]) }
                extra-result { $null = $results.AppendChild($first.CloneNode($true)) }
                unexpected-test { $definitions.UnitTest[0].TestMethod.SetAttribute('name', 'Unexpected') }
                wrong-id { $first.SetAttribute('testId', [Guid]::NewGuid().ToString('D')) }
                wrong-execution { $first.SetAttribute('executionId', [Guid]::NewGuid().ToString('D')) }
                wrong-entry { $entries.TestEntry[0].SetAttribute('executionId', [Guid]::NewGuid().ToString('D')) }
                hidden-result { $null = $document.TestRun.AppendChild($first.CloneNode($true)) }
                wrong-assembly { $definitions.UnitTest[0].TestMethod.SetAttribute('codeBase', 'Other.dll') }
                wrong-storage { $definitions.UnitTest[0].SetAttribute('storage', 'Other.dll') }
                Failed { $first.SetAttribute('outcome', 'Failed') }
                Skipped { $first.SetAttribute('outcome', 'Skipped') }
                NotExecuted { $first.SetAttribute('outcome', 'NotExecuted') }
                Aborted { $first.SetAttribute('outcome', 'Aborted') }
                unknown-outcome { $first.SetAttribute('outcome', 'SENSITIVE_TEST_MARKER') }
                summary-aborted { $document.TestRun.ResultSummary.SetAttribute('outcome', 'Aborted') }
                counter { $document.TestRun.ResultSummary.Counters.SetAttribute('executed', '0') }
                missing-counter { $document.TestRun.ResultSummary.Counters.RemoveAttribute('notExecuted') }
                counter-overflow { $document.TestRun.ResultSummary.Counters.SetAttribute('total', '99999999999999999999') }
                duration { $first.SetAttribute('duration', 'SENSITIVE_TEST_MARKER') }
                negative-duration { $first.SetAttribute('duration', '-00:00:01') }
                invalid-time { $first.SetAttribute('startTime', 'SENSITIVE_TEST_MARKER') }
                stale-run { $document.TestRun.Times.SetAttribute('start', '2026-10-11T23:59:59Z') }
                mixed-runs { $first.SetAttribute('endTime', '2026-10-12T00:00:04Z') }
                wrong-campaign { $receipt.campaignId = [Guid]::NewGuid().ToString('N') }
                wrong-invocation { $receipt.invocationId = [Guid]::NewGuid().ToString('N') }
                nonzero { $receipt.exitCode = 7 }
                timeout { $receipt.status = 'Timeout' }
                cancelled { $receipt.status = 'Cancelled' }
                bad-discovery { $discovery += "`n    SENSITIVE_TEST_MARKER.Unexpected.Fact" }
                path-escape { $invocation.TrxRelative = '../outside.trx'; [IO.File]::WriteAllText((Join-Path $working 'outside.trx'), 'outside untouched') }
            }
            Write-FixtureTrx $campaign @{ TrxRelative = 'incoming/g05.trx' } $document
            $path = Join-Path $campaign.Root 'incoming/g05.trx'
            switch ($case) {
                missing { [IO.File]::Delete($path) }
                empty { [IO.File]::WriteAllText($path, '') }
                malformed { [IO.File]::WriteAllText($path, 'SENSITIVE_TEST_MARKER<') }
                dtd { [IO.File]::WriteAllText($path, '<!DOCTYPE TestRun [<!ENTITY external SYSTEM "file:///SENSITIVE_TEST_MARKER">]><TestRun>&external;</TestRun>') }
                oversized { [IO.File]::WriteAllText($path, ('x' * 4194305)) }
                wrong-namespace { [IO.File]::WriteAllText($path, $document.OuterXml.Replace('http://microsoft.com/schemas/VisualStudio/TeamTest/2010', 'urn:other')) }
            }
            $proof = Save-PublicPostgreSqlProof $campaign $invocation $receipt $discovery
            if ($proof.status -cne 'FAILED' -or $proof.code -cne $expectedCode -or $null -ne $proof.native -or
                [IO.File]::Exists((Join-Path $campaign.Root 'native/g05.trx')) -or [IO.File]::Exists($path)) { throw ('Incorrect red proof: ' + $proof.code) }
            Assert-FixtureRedacted $campaign
            if ($case -ceq 'path-escape' -and [IO.File]::ReadAllText((Join-Path $working 'outside.trx')) -cne 'outside untouched') { throw 'Outside mutated.' }
            """);
        AssertResult(result, 0, "", "");
    }

    [Theory]
    [InlineData("output")]
    [InlineData("attribute")]
    [InlineData("comment")]
    [InlineData("path")]
    [InlineData("password")]
    [InlineData("connection-string")]
    [InlineData("sql")]
    [InlineData("provider")]
    public async Task SensitiveNativeTrxIsNotArchivedAndOnlyAFixedRedReasonSurvives(string surface)
    {
        CliBytesResult result = await RunAsync(ProofFixtures + "$surface = " + Quote(surface) + "\n" + """
            $campaign = New-PublicPostgreSqlCampaign $working $manifest
            $invocation = Start-PublicPostgreSqlShardProof $campaign g05 $windowStart
            $document = New-FixtureTrx $invocation
            switch ($surface) {
                output { $output = $document.CreateElement('Output', $document.DocumentElement.NamespaceURI); $output.InnerText = 'SENSITIVE_TEST_MARKER'; $null = $document.TestRun.Results.UnitTestResult[0].AppendChild($output) }
                attribute { $document.TestRun.SetAttribute('name', 'SENSITIVE_TEST_MARKER') }
                comment { $null = $document.TestRun.AppendChild($document.CreateComment('SENSITIVE_TEST_MARKER')) }
                path { $document.TestRun.TestDefinitions.UnitTest[0].TestMethod.SetAttribute('codeBase', 'SENSITIVE_TEST_MARKER/' + $invocation.Shard.assembly + '.dll') }
                password {
                    $connection=[Data.Common.DbConnectionStringBuilder]::new()
                    $connection['Password']='NOT_A_REAL_PASSWORD'
                    $document.TestRun.SetAttribute('name', $connection.ConnectionString)
                }
                connection-string { $document.TestRun.SetAttribute('name', 'postgresql://NOT_A_REAL_CREDENTIAL') }
                sql { $document.TestRun.SetAttribute('name', 'SELECT private_data FROM hidden_table') }
                provider { $document.TestRun.SetAttribute('name', 'Npgsql.PostgresException: confidential detail') }
            }
            Write-FixtureTrx $campaign $invocation $document
            $proof = Save-PublicPostgreSqlProof $campaign $invocation (New-FixtureReceipt $campaign $invocation) (Fixture-Discovery $invocation) -KnownSensitiveMarkers @('SENSITIVE_TEST_MARKER')
            if ($proof.status -cne 'FAILED' -or $proof.code -cne 'SF-TEST-PG-SENSITIVE' -or $null -ne $proof.native -or
                [IO.Directory]::GetFiles((Join-Path $campaign.Root 'native')).Count -ne 0 -or
                [IO.Directory]::GetFiles((Join-Path $campaign.Root 'incoming')).Count -ne 0) { throw 'Sensitive native retained.' }
            Assert-FixtureRedacted $campaign
            $saved = [IO.File]::ReadAllText((Join-Path $campaign.Root 'proof/g05.json'))
            foreach ($forbidden in @('NOT_A_REAL_PASSWORD', 'NOT_A_REAL_CREDENTIAL', 'private_data', 'confidential detail')) {
                if ($saved.Contains($forbidden)) { throw 'Disclosure.' }
            }
            """);
        AssertResult(result, 0, "", "");
    }

    [Theory]
    [InlineData("simple", true)]
    [InlineData("element", true)]
    [InlineData("attribute", true)]
    [InlineData("numeric-decimal", true)]
    [InlineData("numeric-hex", true)]
    [InlineData("numeric-attribute", true)]
    [InlineData("cdata", true)]
    [InlineData("fragmented", true)]
    [InlineData("fragmented-elements", true)]
    [InlineData("fragmented-comment", true)]
    [InlineData("comment", true)]
    [InlineData("processing-instruction", true)]
    [InlineData("namespace-attribute", true)]
    [InlineData("serialized-name", true)]
    [InlineData("ordinary-ampersand", false)]
    [InlineData("ordinal-case", false)]
    public async Task DecodedKnownMarkersControlTheCompleteCampaignAndNativeArchive(string surface, bool sensitive)
    {
        CliBytesResult result = await RunAsync(ProofFixtures + "$surface = " + Quote(surface)
            + "\n$sensitive = " + (sensitive ? "$true" : "$false") + "\n" + """
            $marker = 'P04B_CANARY_A&B_2026'
            if ($surface -ceq 'simple') { $marker = 'P04B_CANARY_SIMPLE_2026' }
            if ($surface -ceq 'serialized-name') { $marker = 'P04B_CANARY_SURFACE_2026' }
            $campaign = New-PublicPostgreSqlCampaign $working $manifest
            $originals = @{}
            # Complete the other FOUR shards, so accepting the last TRX would turn the WHOLE campaign green.
            foreach ($shard in @($campaign.Partition.shards | Where-Object name -CNE g05)) {
                $other = Start-PublicPostgreSqlShardProof $campaign $shard.name $windowStart
                Write-FixtureTrx $campaign $other (New-FixtureTrx $other)
                $originals[$shard.name] = [Convert]::ToBase64String([IO.File]::ReadAllBytes((Join-Path $campaign.Root $other.TrxRelative)))
                $baseline = Save-PublicPostgreSqlProof $campaign $other (New-FixtureReceipt $campaign $other) (Fixture-Discovery $other) -KnownSensitiveMarkers @($marker)
                if ($baseline.status -cne 'PASSED') { throw 'Baseline shard refused.' }
            }
            $invocation = Start-PublicPostgreSqlShardProof $campaign g05 $windowStart
            $document = New-FixtureTrx $invocation
            $output = $document.CreateElement('Output', $document.DocumentElement.NamespaceURI)
            $null = $document.DocumentElement.AppendChild($output)
            switch ($surface) {
                simple { $output.InnerText = $marker }
                element { $output.InnerText = $marker }
                attribute { $output.SetAttribute('detail', $marker) }
                numeric-decimal { $output.InnerXml = 'P04B_CANARY_A&#38;B_2026' }
                numeric-hex { $output.InnerXml = 'P04B_CANARY_A&#x26;B_2026' }
                numeric-attribute { $output.InnerXml = '<Detail value="P04B_CANARY_A&#x26;B_2026"/>' }
                cdata { $null = $output.AppendChild($document.CreateCDataSection($marker)) }
                fragmented {
                    $null = $output.AppendChild($document.CreateTextNode('P04B_CANARY_A&'))
                    $null = $output.AppendChild($document.CreateCDataSection('B_2026'))
                }
                fragmented-elements { $output.InnerXml = '<Part>P04B_CANARY_A&amp;</Part><Part>B_2026</Part>' }
                fragmented-comment { $output.InnerXml = 'P04B_CANARY_A&amp;<!--separator-->B_2026' }
                comment { $null = $output.AppendChild($document.CreateComment($marker)) }
                processing-instruction { $null = $output.AppendChild($document.CreateProcessingInstruction('canary', $marker)) }
                namespace-attribute { $output.SetAttribute('xmlns:canary', $marker) }
                serialized-name { $null = $output.AppendChild($document.CreateElement($marker)) }
                ordinary-ampersand { $output.InnerText = 'Ordinary A&B text, not a known marker.' }
                ordinal-case { $output.InnerText = $marker.ToLowerInvariant() }
            }
            # The ordinary XML serializer, not a replacement of escaped bytes, produces the fixture.
            Write-FixtureTrx $campaign $invocation $document
            $incoming = Join-Path $campaign.Root $invocation.TrxRelative
            if ($surface -cin @('numeric-decimal', 'numeric-hex', 'numeric-attribute')) {
                # Keep literal numeric references in these fixtures: a DOM reserialization normalizes them.
                # Only this fixed test-data fragment is raw; the rest of the native TRX uses XmlWriter.
                $fragment = switch ($surface) {
                    numeric-decimal { 'P04B_CANARY_A&#38;B_2026' }
                    numeric-hex { 'P04B_CANARY_A&#x26;B_2026' }
                    numeric-attribute { '<Detail value="P04B_CANARY_A&#x26;B_2026"/>' }
                }
                $settings = [Xml.XmlWriterSettings]::new(); $settings.OmitXmlDeclaration = $true
                $writer = [Xml.XmlWriter]::Create($incoming, $settings)
                try {
                    $writer.WriteStartElement('TestRun', $document.DocumentElement.NamespaceURI)
                    $writer.WriteAttributeString('id', $document.DocumentElement.GetAttribute('id'))
                    foreach ($child in $document.DocumentElement.ChildNodes) {
                        if ([object]::ReferenceEquals($child, $output)) {
                            $writer.WriteStartElement('Output', $document.DocumentElement.NamespaceURI)
                            $writer.WriteRaw($fragment); $writer.WriteEndElement()
                        } else { $child.WriteTo($writer) }
                    }
                    $writer.WriteEndElement()
                } finally { $writer.Dispose() }
                if (![IO.File]::ReadAllText($incoming).Contains($fragment, [StringComparison]::Ordinal)) { throw 'Numeric reference fixture lost.' }
            }
            $original = [IO.File]::ReadAllBytes($incoming)
            $parsed = Read-PublicPostgreSqlTrxBytes $original
            $null = Assert-PublicPostgreSqlTrx $parsed $invocation (New-FixtureReceipt $campaign $invocation).finishedUtc
            if ($surface -ceq 'element' -and (!$parsed.DocumentElement.InnerText.Contains($marker, [StringComparison]::Ordinal) -or
                ![Text.Encoding]::UTF8.GetString($original).Contains('P04B_CANARY_A&amp;B_2026', [StringComparison]::Ordinal))) { throw 'Escaped reproduction missing.' }
            $proof = Save-PublicPostgreSqlProof $campaign $invocation (New-FixtureReceipt $campaign $invocation) (Fixture-Discovery $invocation) -KnownSensitiveMarkers @($marker)
            $saved = [IO.File]::ReadAllText((Join-Path $campaign.Root 'proof/g05.json')) | ConvertFrom-Json -AsHashtable
            $manifestSaved = [IO.File]::ReadAllText((Join-Path $campaign.Root 'manifest.json')) | ConvertFrom-Json -AsHashtable
            $record = @($manifestSaved.shards | Where-Object shard -CEQ g05)[0]
            $native = Join-Path $campaign.Root 'native/g05.trx'
            if ($sensitive) {
                foreach ($decision in @($proof, $saved, $record)) {
                    if ($decision.status -cne 'FAILED' -or $decision.code -cne 'SF-TEST-PG-SENSITIVE') { throw 'Sensitive campaign incorrectly accepted.' }
                }
                if ($manifestSaved.status -cne 'FAILED' -or $proof.stage -cne 'SensitiveContent' -or $proof.completeness -cne 'Complete' -or
                    $null -ne $proof.native -or $null -ne $saved.native -or [IO.File]::Exists($native)) { throw 'Sensitive native archived.' }
                Assert-FixtureRedacted $campaign
                foreach ($file in @([IO.Directory]::GetFiles((Join-Path $campaign.Root 'proof'))) + @((Join-Path $campaign.Root 'manifest.json'))) {
                    if ([IO.File]::ReadAllText($file).Contains('P04B_CANARY', [StringComparison]::Ordinal)) { throw 'Diagnostic disclosure.' }
                }
            } else {
                if ($proof.status -cne 'PASSED' -or $saved.status -cne 'PASSED' -or $record.status -cne 'PASSED' -or
                    $manifestSaved.status -cne 'PASSED' -or $null -ne $proof.code -or $proof.native -cne 'native/g05.trx' -or
                    [Convert]::ToBase64String([IO.File]::ReadAllBytes($native)) -cne [Convert]::ToBase64String($original)) { throw 'Ordinary native refused or changed.' }
            }
            if ([IO.File]::Exists($incoming) -or $proof.incomingCleanup -cne 'Completed' -or $manifestSaved.databaseProof -or
                $manifestSaved.provenance -cne 'SyntheticFixture' -or [IO.Directory]::GetFiles((Join-Path $campaign.Root 'native')).Count -ne $(if ($sensitive) { 4 } else { 5 })) { throw 'Incorrect archive or cleanup.' }
            foreach ($shard in $originals.Keys) {
                if ([Convert]::ToBase64String([IO.File]::ReadAllBytes((Join-Path $campaign.Root ('native/' + $shard + '.trx')))) -cne $originals[$shard]) { throw 'Other native changed.' }
            }
            """);
        Assert.DoesNotContain("P04B_CANARY", Encoding.UTF8.GetString(result.StandardOutput) + Encoding.UTF8.GetString(result.StandardError), StringComparison.Ordinal);
        AssertResult(result, 0, "", "");
    }

    [Fact]
    public async Task CampaignProvenanceRejectsReuseAndKeepsEveryRedCampaignSeparate()
    {
        CliBytesResult result = await RunAsync(ProofFixtures + """
            $red = New-PublicPostgreSqlCampaign $working $manifest
            $invocation = Start-PublicPostgreSqlShardProof $red g05 $windowStart
            $proof = Save-PublicPostgreSqlProof $red $invocation (New-FixtureReceipt $red $invocation) (Fixture-Discovery $invocation)
            $original = [IO.File]::ReadAllText((Join-Path $red.Root 'proof/g05.json'))
            $other = New-PublicPostgreSqlCampaign $working $manifest
            $next = Start-PublicPostgreSqlShardProof $other g05 $windowStart
            $document = New-FixtureTrx $next
            Write-FixtureTrx $other $next $document
            $null = Save-PublicPostgreSqlProof $other $next (New-FixtureReceipt $other $next) (Fixture-Discovery $next)
            if ([IO.File]::ReadAllText((Join-Path $red.Root 'proof/g05.json')) -cne $original -or $red.Id -ceq $other.Id) { throw 'Red campaign overwritten.' }
            try { $null = Start-PublicPostgreSqlShardProof $other g05 $windowStart; throw 'Reused invocation.' }
            catch { if ($_.Exception.Message -cne 'SF-TEST-PG-PROVENANCE') { throw } }
            try { $null = Save-PublicPostgreSqlProof $other $next (New-FixtureReceipt $other $next) (Fixture-Discovery $next); throw 'Proof overwritten.' }
            catch { if ($_.Exception.Message -cne 'SF-TEST-PG-PROVENANCE') { throw } }
            $third = New-PublicPostgreSqlCampaign $working $manifest
            $thirdInvocation = Start-PublicPostgreSqlShardProof $third g05 $windowStart
            Write-FixtureTrx $third $thirdInvocation $document
            # Reloading the helper in the SAME proof process must not forget known campaigns.
            . (Join-Path ([IO.Path]::GetDirectoryName($manifest)) 'PostgreSqlProof.ps1')
            $duplicate = Save-PublicPostgreSqlProof $third $thirdInvocation (New-FixtureReceipt $third $thirdInvocation) (Fixture-Discovery $thirdInvocation)
            if ($duplicate.status -cne 'FAILED' -or $duplicate.code -cne 'SF-TEST-PG-PROVENANCE') { throw 'Known run reused in another campaign.' }
            $fourth = New-PublicPostgreSqlCampaign $working $manifest
            [IO.File]::WriteAllText((Join-Path $fourth.Root 'incoming/g05.trx'), 'old TRX')
            try { $null = Start-PublicPostgreSqlShardProof $fourth g05 $windowStart; throw 'Pre-existing TRX accepted.' }
            catch { if ($_.Exception.Message -cne 'SF-TEST-PG-PROVENANCE') { throw } }
            foreach ($campaign in @($red, $other, $third, $fourth)) {
                if (([IO.File]::ReadAllText((Join-Path $campaign.Root 'manifest.json')) | ConvertFrom-Json).status -cne 'FAILED') { throw 'Partial campaign falsely green.' }
            }
            """);
        AssertResult(result, 0, "", "");
    }

    [Fact]
    public async Task EvidencePathsRejectTraversalAndLinksWithoutTouchingOutsideFiles()
    {
        CliBytesResult result = await RunAsync(ProofFixtures + """
            $campaign = New-PublicPostgreSqlCampaign $working $manifest
            foreach ($relative in @('../outside.trx', '/outside.trx', 'C:\outside.trx', 'incoming\\outside.trx', 'incoming/./outside.trx')) {
                try { $null = Resolve-PublicPostgreSqlEvidencePath $campaign.Root $relative; throw 'Unsafe path accepted.' }
                catch { if ($_.Exception.Message -cne 'SF-TEST-PG-PATH') { throw } }
            }
            $outside = [IO.Directory]::CreateDirectory((Join-Path $working 'outside')).FullName
            $outsideFile = Join-Path $outside 'g05.trx'
            [IO.File]::WriteAllText($outsideFile, 'outside untouched')
            $invocation = Start-PublicPostgreSqlShardProof $campaign g05 $windowStart
            $incoming = Join-Path $campaign.Root 'incoming'
            [IO.Directory]::Delete($incoming)
            try {
                if ($IsWindows) { $null = New-Item -ItemType Junction -Path $incoming -Target $outside }
                else { $null = [IO.Directory]::CreateSymbolicLink($incoming, $outside) }
                $proof = Save-PublicPostgreSqlProof $campaign $invocation (New-FixtureReceipt $campaign $invocation) (Fixture-Discovery $invocation)
                if ($proof.status -cne 'FAILED' -or $proof.code -cne 'SF-TEST-PG-PATH' -or
                    $proof.incomingCleanup -cne 'Failed' -or [IO.File]::ReadAllText($outsideFile) -cne 'outside untouched') { throw 'Link escape.' }
                Assert-FixtureRedacted $campaign
            } finally {
                if ([IO.Path]::Exists($incoming)) { [IO.Directory]::Delete($incoming) }
                $null = [IO.Directory]::CreateDirectory($incoming)
            }
            """);
        AssertResult(result, 0, "", "");
    }

    [Fact]
    public async Task ManifestUsesOnlyBoundedMetadataAndNeverSerializesReceiptExtras()
    {
        CliBytesResult result = await RunAsync(ProofFixtures + """
            foreach ($argument in @('Commit', 'Sdk', 'Runtime', 'GitState')) {
                $values = @{ Parent = $working; ManifestPath = $manifest; $argument = 'SENSITIVE_TEST_MARKER' }
                try { $null = New-PublicPostgreSqlCampaign @values; throw 'Unbounded metadata accepted.' }
                catch { if ($_.Exception.Message -cne 'SF-TEST-PG-PROVENANCE') { throw } }
            }
            $campaign = New-PublicPostgreSqlCampaign $working $manifest
            $invocation = Start-PublicPostgreSqlShardProof $campaign g05 $windowStart
            Write-FixtureTrx $campaign $invocation (New-FixtureTrx $invocation)
            $receipt = New-FixtureReceipt $campaign $invocation
            $receipt.output = 'SENSITIVE_TEST_MARKER'; $receipt.error = 'SENSITIVE_TEST_MARKER'
            $receipt.connectionString = 'SENSITIVE_TEST_MARKER'; $receipt.secretPath = 'SENSITIVE_TEST_MARKER'
            $proof = Save-PublicPostgreSqlProof $campaign $invocation $receipt (Fixture-Discovery $invocation)
            if ($proof.status -cne 'PASSED') { throw 'Valid fixture refused.' }
            # The aggregate is still red: the other four shards were never executed.
            Assert-FixtureRedacted $campaign
            """);
        AssertResult(result, 0, "", "");
    }

    [Fact]
    public async Task MeasurementsOnlyPublishAllowlistedPhasesNumbersAndNames()
    {
        CliBytesResult result = await RunAsync(ProofFixtures + """
            $campaign = New-PublicPostgreSqlCampaign $working $manifest
            $allowed = @($campaign.Partition.expectedTests | Where-Object { $_.lot -cin @('G04', 'P01', 'P02') } | ForEach-Object fullyQualifiedName)
            if ($allowed.Count -ne 17) { throw 'Wrong emitter inventory.' }
            foreach ($name in $allowed) {
                [IO.File]::WriteAllText((Join-Path $campaign.Root ('metrics/' + ($name -split '\.')[-1] + '.1.metrics')),
                    "Test|Start|1000|0`nTest|End|2000|1000`nApply|Start|3000|0`nApply|End|3500|500`nBootstrap|Start|4000|0`n")
            }
            foreach ($name in @($campaign.Partition.expectedTests | Where-Object { $_.lot -cin @('G05', 'P03') } | ForEach-Object fullyQualifiedName)) {
                [IO.File]::WriteAllText((Join-Path $campaign.Root ('metrics/' + ($name -split '\.')[-1] + '.1.metrics')), 'SENSITIVE_TEST_MARKER')
            }
            [IO.File]::WriteAllText((Join-Path $campaign.Root 'metrics/SENSITIVE_TEST_MARKER.1.metrics'), 'SENSITIVE_TEST_MARKER')
            $measurements = @(Read-PublicPostgreSqlMeasurements $campaign)
            if ($measurements.Count -ne 51 -or @($measurements | Where-Object completeness -CEQ Partial).Count -ne 17) { throw 'Wrong complete/partial measurements.' }
            $keys = @($measurements | ForEach-Object { $_.fullyQualifiedName + '/' + $_.phase })
            [string[]] $ordered = $keys.Clone(); [Array]::Sort($ordered, [StringComparer]::Ordinal)
            if (($keys -join '|') -cne ($ordered -join '|')) { throw 'Nonordinal ordering.' }
            if (($measurements | ConvertTo-Json -Depth 5).Contains('SENSITIVE_TEST_MARKER')) { throw 'Disclosure.' }
            # A second process contributes to the SAME logical measurement, deterministically.
            $first = $allowed[0]
            [IO.File]::WriteAllText((Join-Path $campaign.Root ('metrics/' + ($first -split '\.')[-1] + '.2.metrics')), "Test|Start|5000|0`nTest|End|5500|500`n")
            $next = @(Read-PublicPostgreSqlMeasurements $campaign)
            $total = @($next | Where-Object { $_.fullyQualifiedName -ceq $first -and $_.phase -ceq 'Test' })[0]
            if ($total.started -ne 2 -or $total.ended -ne 2 -or $total.durationMs -ne 1500 -or $total.completeness -cne 'Complete') { throw 'Incorrect aggregate.' }
            if ($campaign.Manifest.status -cne 'FAILED') { throw 'Metrics alone became a proof.' }
            """);
        AssertResult(result, 0, "", "");
    }

    [Theory]
    [InlineData("phase")]
    [InlineData("malformed")]
    [InlineData("number")]
    [InlineData("timestamp")]
    [InlineData("duration")]
    [InlineData("start-duration")]
    [InlineData("orphan-end")]
    [InlineData("reversed-time")]
    [InlineData("sensitive")]
    [InlineData("empty")]
    [InlineData("pid")]
    public async Task InvalidAllowlistedMetricsFailWithoutPublishingRawLines(string scenario)
    {
        CliBytesResult result = await RunAsync(ProofFixtures + "$case = " + Quote(scenario) + "\n" + """
            $campaign = New-PublicPostgreSqlCampaign $working $manifest
            $name = ($campaign.Partition.shards[0].expectedTests[0] -split '\.')[-1]
            $line = switch ($case) {
                phase { "Probe|Start|1000|0`n" }
                malformed { 'Test,Start,1000,0' }
                number { 'Test|Start|NaN|0' }
                timestamp { 'Test|Start|253402300800000|0' }
                duration { "Test|Start|1000|0`nTest|End|2000|9999999999999`n" }
                start-duration { 'Test|Start|1000|1' }
                orphan-end { 'Test|End|2000|1000' }
                reversed-time { "Test|Start|2000|0`nTest|End|1000|1000`n" }
                sensitive {
                    $connection=[Data.Common.DbConnectionStringBuilder]::new()
                    $connection['Password']='SENSITIVE_TEST_MARKER'
                    $connection.ConnectionString
                }
                empty { '' }
                pid { "Test|Start|1000|0`n" }
            }
            $pidPart = if ($case -ceq 'pid') { '2147483648' } else { '1' }
            [IO.File]::WriteAllText((Join-Path $campaign.Root ('metrics/' + $name + '.' + $pidPart + '.metrics')), $line)
            try { $null = Read-PublicPostgreSqlMeasurements $campaign; throw 'Accepted invalid metrics.' }
            catch { if ($_.Exception.Message -cne 'SF-TEST-PG-METRICS') { throw } }
            $invocation = Start-PublicPostgreSqlShardProof $campaign g05 $windowStart
            Write-FixtureTrx $campaign $invocation (New-FixtureTrx $invocation)
            $proof = Save-PublicPostgreSqlProof $campaign $invocation (New-FixtureReceipt $campaign $invocation) (Fixture-Discovery $invocation)
            if ($proof.status -cne 'FAILED' -or $proof.code -cne 'SF-TEST-PG-METRICS' -or $null -ne $proof.native) { throw 'Metrics failure masked.' }
            Assert-FixtureRedacted $campaign
            """);
        AssertResult(result, 0, "", "");
    }

    // Only test data/receipts are built here. Every acceptance decision is made by the real PS script.
    internal const string ProofFixtures = """
        $windowStart = [DateTimeOffset]::Parse('2026-10-12T00:00:00Z')
        function New-FixtureReceipt($campaign, $invocation) {
            return @{ campaignId = $campaign.Id; invocationId = $invocation.Id; exitCode = 0; status = 'Completed'; finishedUtc = [DateTimeOffset]::Parse('2026-10-12T00:00:03Z') }
        }
        function Fixture-Discovery($invocation) { return ($invocation.Shard.expectedTests | ForEach-Object { '    ' + $_ }) -join "`n" }
        function New-FixtureTrx($invocation) {
            $ns = 'http://microsoft.com/schemas/VisualStudio/TeamTest/2010'
            [xml] $document = '<TestRun xmlns="http://microsoft.com/schemas/VisualStudio/TeamTest/2010"><Times start="2026-10-12T00:00:01Z" finish="2026-10-12T00:00:02Z"/><TestDefinitions/><Results/><TestEntries/><ResultSummary outcome="Completed"><Counters/></ResultSummary></TestRun>'
            $document.TestRun.SetAttribute('id', [Guid]::NewGuid().ToString('D'))
            $definitions = $document.SelectSingleNode('//*[local-name()="TestDefinitions"]')
            $results = $document.SelectSingleNode('//*[local-name()="Results"]')
            $entries = $document.SelectSingleNode('//*[local-name()="TestEntries"]')
            $counters = $document.SelectSingleNode('//*[local-name()="Counters"]')
            foreach ($name in $invocation.Shard.expectedTests) {
                $id = [Guid]::NewGuid().ToString('D'); $executionId = [Guid]::NewGuid().ToString('D')
                $definition = $document.CreateElement('UnitTest', $ns)
                $definition.SetAttribute('id', $id); $definition.SetAttribute('name', 'display...'); $definition.SetAttribute('storage', $invocation.Shard.assembly + '.dll')
                $execution = $document.CreateElement('Execution', $ns); $execution.SetAttribute('id', $executionId); $null = $definition.AppendChild($execution)
                $method = $document.CreateElement('TestMethod', $ns)
                $method.SetAttribute('className', $name.Substring(0, $name.LastIndexOf('.'))); $method.SetAttribute('name', ($name -split '\.')[-1])
                $method.SetAttribute('codeBase', $invocation.Shard.assembly + '.dll'); $null = $definition.AppendChild($method)
                $null = $definitions.AppendChild($definition)
                $result = $document.CreateElement('UnitTestResult', $ns)
                foreach ($pair in @(@('testId',$id), @('executionId',$executionId), @('testName','display...'), @('outcome','Passed'),
                    @('duration','00:00:01'), @('startTime','2026-10-12T00:00:01Z'), @('endTime','2026-10-12T00:00:02Z'))) { $result.SetAttribute($pair[0], $pair[1]) }
                $null = $results.AppendChild($result)
                $entry = $document.CreateElement('TestEntry', $ns); $entry.SetAttribute('testId', $id); $entry.SetAttribute('executionId', $executionId)
                $null = $entries.AppendChild($entry)
            }
            foreach ($name in @('total','executed','passed','failed','error','timeout','aborted','inconclusive','passedButRunAborted','notRunnable','notExecuted','disconnected','warning','completed','inProgress','pending')) {
                $counters.SetAttribute($name, [string] $(if ($name -cin @('total','executed','passed')) { $invocation.Shard.expectedTests.Count } else { 0 }))
            }
            return ,$document
        }
        function Write-FixtureTrx($campaign, $invocation, $document) {
            [IO.File]::WriteAllText((Join-Path $campaign.Root $invocation.TrxRelative), $document.OuterXml)
        }
        function Assert-FixtureRedacted($campaign) {
            foreach ($file in @([IO.Directory]::GetFiles((Join-Path $campaign.Root 'proof'))) + @((Join-Path $campaign.Root 'manifest.json'))) {
                $text = [IO.File]::ReadAllText($file)
                if ($text.Contains('SENSITIVE_TEST_MARKER') -or $text.Contains($working) -or $text.Contains('connectionString')) { throw 'Disclosure.' }
            }
            $saved = [IO.File]::ReadAllText((Join-Path $campaign.Root 'manifest.json')) | ConvertFrom-Json -AsHashtable
            if ($saved.status -cne 'FAILED' -or $saved.databaseProof -or $saved.cleanup -cne 'NotObserved') { throw 'False green campaign.' }
        }

        """;

    private static string Resource(string file) => Path.Combine(AppContext.BaseDirectory, "PostgreSql", "Public", file);
    private static string Quote(string value) => "'" + value.Replace("'", "''", StringComparison.Ordinal) + "'";

    private static async Task<CliBytesResult> RunAsync(string test)
    {
        string working = Directory.CreateTempSubdirectory("sitefactory-p04a-proof-").FullName;
        try
        {
            string script = "$ErrorActionPreference = 'Stop'\n$working = " + Quote(working)
                + "\n$manifest = " + Quote(Resource("shards.json")) + "\n$entry = " + Quote(Resource("Invoke-PostgreSqlTests.ps1"))
                + "\n. " + Quote(Resource("PostgreSqlProof.ps1")) + "\n" + test;
            ProcessStartInfo start = MigratorProcessRunner.CreateIsolatedStartInfo(working);
            start.FileName = "pwsh";
            foreach (string argument in new[] { "-NoProfile", "-NonInteractive", "-EncodedCommand",
                Convert.ToBase64String(Encoding.Unicode.GetBytes(script)), "-OutputFormat", "Text" }) start.ArgumentList.Add(argument);
            return await MigratorProcessRunner.RunOwnedAsync(start, TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);
        }
        finally
        {
            Assert.Equal(Path.TrimEndingDirectorySeparator(Path.GetTempPath()), Path.GetDirectoryName(working));
            Assert.StartsWith("sitefactory-p04a-proof-", Path.GetFileName(working), StringComparison.Ordinal);
            Directory.Delete(working, recursive: true);
            Assert.False(Directory.Exists(working));
        }
    }

    private static void AssertResult(CliBytesResult result, int code, string output, string error)
    {
        Assert.Equal(code, result.ExitCode);
        string stdout = Encoding.UTF8.GetString(result.StandardOutput).Replace("\r\n", "\n", StringComparison.Ordinal);
        string stderr = Encoding.UTF8.GetString(result.StandardError).Replace("\r\n", "\n", StringComparison.Ordinal);
        Assert.DoesNotContain("SENSITIVE_TEST_MARKER", stdout + stderr, StringComparison.Ordinal);
        Assert.Equal(output, stdout);
        Assert.Equal(error, stderr);
    }
}
