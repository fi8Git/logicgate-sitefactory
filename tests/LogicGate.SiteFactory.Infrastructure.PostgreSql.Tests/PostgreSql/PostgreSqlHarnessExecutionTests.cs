using System.Diagnostics;
using System.Globalization;
using System.IO.Pipes;
using System.Text;
using LogicGate.SiteFactory.Cli.Tests;
using Xunit;

namespace LogicGate.SiteFactory.Infrastructure.PostgreSql.Tests.PostgreSql;

[Collection("P01 diagnostics environment")]
public sealed class PostgreSqlHarnessExecutionTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(7)]
    public async Task ActualPowerShellBridgeKeepsExitCodesAndWithholdsCapturedStreams(int exit)
    {
        CliBytesResult result = await ScriptAsync("$expected=" + exit.ToString(CultureInfo.InvariantCulture) + "\n" + """
            $result = Invoke-PublicPostgreSqlProcess $bridge $working 'pwsh' @('-NoProfile','-NonInteractive','-Command',
                "[Console]::WriteLine('P04C_CANARY_A&B');[Console]::Error.WriteLine('P04C_CANARY_A&B');exit $expected") -TimeoutSeconds 10
            if ($result.Status -cne 'Completed' -or $result.ExitCode -ne $expected -or $result.Cleanup -cne 'Completed' -or
                !$result.Output.Contains('P04C_CANARY_A&B') -or !$result.Error.Contains('P04C_CANARY_A&B')) {throw 'Transport changed.'}
            if ($expected -eq 0) {Assert-PublicPostgreSqlProcess $result}
            else {
                try {Assert-PublicPostgreSqlProcess $result;throw 'Nonzero accepted.'}
                catch {if ($_.Exception.Message -cne 'SF-TEST-PG-PROCESS') {throw}}
            }
            if ($expected -eq 0) {
                $entry=Join-Path ([IO.Path]::GetDirectoryName($manifest)) 'Invoke-PostgreSqlTests.ps1'
                $source=[IO.File]::ReadAllText($entry)
                $tokens=$null;$errors=$null
                $ast=[Management.Automation.Language.Parser]::ParseInput($source,[ref]$tokens,[ref]$errors)
                $commands=@($ast.FindAll({param($node) $node -is [Management.Automation.Language.FunctionDefinitionAst] -and
                    $node.Name -ceq 'Invoke-RequiredCommand'},$true))
                $begin=$source.IndexOf('$gitEnvironment=@{}',[StringComparison]::Ordinal)
                $end=$source.IndexOf('$campaign.Manifest.sdk=',$begin,[StringComparison]::Ordinal)
                if ($errors.Count -ne 0 -or $commands.Count -ne 1 -or $begin -lt 0 -or $end -le $begin) {throw 'Actual provenance unavailable.'}
                . ([ScriptBlock]::Create($commands[0].Extent.Text))
                $provenance=[ScriptBlock]::Create($source.Substring($begin,$end-$begin))
                function Read-ActualProvenance {
                    $campaign=@{Manifest=@{}}
                    . $provenance
                    return @{Commit=$campaign.Manifest.commit;State=$campaign.Manifest.gitState;Environment=$gitEnvironment}
                }
                function Read-OwnedRepositorySnapshot([string] $Directory) {
                    $snapshot=@{}
                    foreach ($file in Get-ChildItem -LiteralPath $Directory -Force -Recurse -File) {
                        $snapshot[[IO.Path]::GetRelativePath($Directory,$file.FullName)]=(Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash
                    }
                    return $snapshot
                }
                $upstream=[IO.Directory]::CreateDirectory((Join-Path $working 'upstream')).FullName
                $retainedUpstream=Join-Path $working 'retained-upstream'
                $origin=Join-Path $working 'origin'
                $missingSolution=Join-Path $working 'missing-solution'
                $copy=[IO.Directory]::CreateDirectory((Join-Path $working 'copy')).FullName
                $hooks=[IO.Directory]::CreateDirectory((Join-Path $working 'empty-hooks')).FullName
                $originalGitDirectory=$env:GIT_DIR;$originalGitWorkTree=$env:GIT_WORK_TREE
                $originalWitness=$env:SITEFACTORY_GIT_CONTEXT_WITNESS
                try {
                    $env:GIT_DIR=$null;$env:GIT_WORK_TREE=$null
                    $null=& git -C $upstream init --quiet 2>$null
                    if ($LASTEXITCODE -ne 0) {throw 'Owned Git initialization failed.'}
                    [IO.File]::WriteAllText((Join-Path $upstream '.gitattributes'),"* text=auto eol=lf`n")
                    [IO.File]::WriteAllText((Join-Path $upstream 'public.txt'),'public fixture')
                    [IO.File]::WriteAllText((Join-Path $upstream 'SiteFactory.slnx'),"<Solution />`n")
                    [IO.File]::WriteAllText((Join-Path $upstream 'private-witness.txt'),'excluded private fixture')
                    $null=& git -C $upstream add -- .gitattributes public.txt SiteFactory.slnx private-witness.txt 2>$null
                    if ($LASTEXITCODE -ne 0) {throw 'Owned Git staging failed.'}
                    $null=& git -C $upstream -c user.name=SiteFactory-Test -c user.email=test@example.invalid -c commit.gpgSign=false `
                        -c "core.hooksPath=$hooks" commit --quiet -m fixture 2>$null
                    if ($LASTEXITCODE -ne 0) {throw 'Owned Git commit failed.'}
                    $sha=(& git -C $upstream rev-parse HEAD 2>$null)
                    if ($LASTEXITCODE -ne 0 -or $sha -cnotmatch '^[0-9a-f]{40}$') {throw 'Owned Git identity missing.'}
                    $solutionBlob=(& git -C $upstream rev-parse HEAD:SiteFactory.slnx 2>$null)
                    $privateBlob=(& git -C $upstream rev-parse HEAD:private-witness.txt 2>$null)
                    $null=& git -C $upstream config uploadpack.allowFilter true 2>$null
                    if ($LASTEXITCODE -ne 0) {throw 'Owned filter support unavailable.'}
                    # file:// uses upload-pack, unlike a local-path clone which may silently ignore --filter.
                    $remote=[Uri]::new($upstream+[IO.Path]::DirectorySeparatorChar).AbsoluteUri
                    foreach ($clone in @($missingSolution,$origin)) {
                        $null=& git -c protocol.file.allow=always clone --quiet --filter=blob:none --no-checkout $remote $clone 2>$null
                        if ($LASTEXITCODE -ne 0) {throw 'Partial clone failed.'}
                        $filter=(& git -C $clone config --get remote.origin.partialclonefilter 2>$null)
                        $promisor=(& git -C $clone config --get remote.origin.promisor 2>$null)
                        if ($filter -cne 'blob:none' -or $promisor -cne 'true') {throw 'Clone is not partial.'}
                        $patterns=@('/.gitattributes','/public.txt')
                        if ($clone -ceq $origin) {$patterns+='/SiteFactory.slnx'}
                        $null=& git -C $clone sparse-checkout set --no-cone @patterns 2>$null
                        if ($LASTEXITCODE -ne 0) {throw 'Sparse checkout failed.'}
                        $null=& git -C $clone checkout --quiet 2>$null
                        if ($LASTEXITCODE -ne 0) {throw 'Initial sparse materialization failed.'}
                        # Enumerate only objects already on disk: this check must never cause a lazy fetch.
                        $objects=@(& git -C $clone cat-file --batch-all-objects '--batch-check=%(objectname)' 2>$null)
                        if ($LASTEXITCODE -ne 0 -or $objects -ccontains $privateBlob -or
                            (($objects -ccontains $solutionBlob) -ne ($clone -ceq $origin)) -or
                            [IO.File]::Exists((Join-Path $clone 'private-witness.txt')) -or
                            ([IO.File]::Exists((Join-Path $clone 'SiteFactory.slnx')) -ne ($clone -ceq $origin))) {throw 'Sparse object boundary changed.'}
                    }
                    # The remote is no longer reachable. A passing status cannot hide any subsequent download.
                    [IO.Directory]::Move($upstream,$retainedUpstream)
                    if ([IO.Directory]::Exists($upstream)) {throw 'Owned origin still reachable.'}
                    foreach ($file in @('.gitattributes','public.txt')) {
                        [IO.File]::Copy((Join-Path $origin $file),(Join-Path $copy $file))
                    }
                    $regenerated=[xml]'<Solution />'
                    $regenerated.Save((Join-Path $copy 'SiteFactory.slnx'))
                    $RepositoryRoot=$copy
                    $env:GIT_DIR=Join-Path $missingSolution '.git';$env:GIT_WORK_TREE=$copy
                    $negativeEnvironment=@{GIT_DIR=$env:GIT_DIR;GIT_WORK_TREE=$env:GIT_WORK_TREE}
                    $negativeSha=Invoke-RequiredCommand 'git' @('rev-parse','HEAD') -EnvironmentValues $negativeEnvironment
                    if ($negativeSha -cne $sha) {throw 'Partial provenance identity changed.'}
                    $negative=Invoke-PublicPostgreSqlProcess $bridge $copy 'git' @('--no-optional-locks','-c','core.fsmonitor=false','status','--porcelain=v1') $negativeEnvironment -TimeoutSeconds 10
                    if ($negative.Status -cne 'Completed' -or $negative.ExitCode -eq 0 -or $negative.Cleanup -cne 'Completed' -or
                        !$negative.Error.Contains($solutionBlob) -or !$negative.Error.Contains('promisor')) {throw 'Missing solution blob counterproof unavailable.'}
                    try {$null=Read-ActualProvenance;throw 'Actual provenance accepted a missing solution blob.'}
                    catch {if ($_.Exception.Message -cne 'SF-TEST-PG-PROCESS') {throw}}
                    # The corrected initial checkout provides the real tracked solution before regeneration.
                    [IO.File]::Copy((Join-Path $origin 'SiteFactory.slnx'),(Join-Path $copy 'SiteFactory.slnx'),$true)
                    $before=Read-OwnedRepositorySnapshot $origin
                    $RepositoryRoot=$origin
                    $env:GIT_DIR=$null;$env:GIT_WORK_TREE=$null
                    $normal=Read-ActualProvenance
                    if ($normal.Commit -cne $sha -or $normal.State -cne 'Clean' -or $normal.Environment.Count -ne 0) {throw 'Normal repository provenance changed.'}
                    $RepositoryRoot=$copy
                    $env:GIT_DIR=Join-Path $origin '.git';$env:GIT_WORK_TREE=$copy
                    $env:SITEFACTORY_GIT_CONTEXT_WITNESS='P04C_CANARY_A&B'
                    $without=Invoke-PublicPostgreSqlProcess $bridge $copy 'git' @('rev-parse','HEAD') -TimeoutSeconds 10
                    if ($without.Status -cne 'Completed' -or $without.ExitCode -eq 0 -or $without.Cleanup -cne 'Completed') {throw 'Inherited Git context unexpectedly reached bridge.'}
                    $explicit=Read-ActualProvenance
                    if ($explicit.Commit -cne $sha -or $explicit.State -cne 'Clean' -or $explicit.Environment.Count -ne 2 -or
                        !$explicit.Environment.ContainsKey('GIT_DIR') -or !$explicit.Environment.ContainsKey('GIT_WORK_TREE') -or
                        $explicit.Environment.GIT_DIR -cne $env:GIT_DIR -or $explicit.Environment.GIT_WORK_TREE -cne $copy) {throw 'Explicit provenance changed.'}
                    [IO.File]::WriteAllText((Join-Path $copy 'public.txt'),'changed public fixture')
                    $dirty=Read-ActualProvenance
                    if ($dirty.Commit -cne $sha -or $dirty.State -cne 'Dirty') {throw 'Actual status ignored explicit work tree.'}
                    [IO.File]::Copy((Join-Path $origin 'public.txt'),(Join-Path $copy 'public.txt'),$true)
                    $regenerated.Save((Join-Path $copy 'SiteFactory.slnx'))
                    $solutionDirty=Read-ActualProvenance
                    if ($solutionDirty.Commit -cne $sha -or $solutionDirty.State -cne 'Dirty') {throw 'Regenerated solution change hidden.'}
                    $null=Invoke-RequiredCommand 'pwsh' @('-NoProfile','-NonInteractive','-Command',
                        'if ($env:GIT_DIR -or $env:GIT_WORK_TREE -or $env:SITEFACTORY_GIT_CONTEXT_WITNESS) {exit 7}') 10
                    # A normal repository must not become a silent fallback for an invalid explicit context.
                    $RepositoryRoot=$origin
                    foreach ($invalid in @(
                        @{Directory=(Join-Path $origin '.git');Tree=$null},
                        @{Directory=$null;Tree=$origin},
                        @{Directory=' ';Tree=$origin},
                        @{Directory=(Join-Path $origin '.git');Tree=' '},
                        @{Directory='.git';Tree=$origin},
                        @{Directory=(Join-Path $origin '.git');Tree='.'},
                        @{Directory=(Join-Path $origin '.git');Tree=$copy},
                        @{Directory=(Join-Path $working 'missing');Tree=$origin},
                        @{Directory=$copy;Tree=$origin})) {
                        $env:GIT_DIR=$invalid.Directory;$env:GIT_WORK_TREE=$invalid.Tree
                        try {$null=Read-ActualProvenance;throw 'Invalid explicit context accepted.'}
                        catch {if ($_.Exception.Message -cne 'SF-TEST-PG-PROVENANCE') {throw}}
                    }
                    $malformed=[IO.Directory]::CreateDirectory((Join-Path $working 'malformed-git')).FullName
                    [IO.File]::WriteAllText((Join-Path $malformed 'HEAD'),'ref: refs/heads/missing')
                    $env:GIT_DIR=$malformed;$env:GIT_WORK_TREE=$origin
                    try {$null=Read-ActualProvenance;throw 'Malformed context fell back to normal repository.'}
                    catch {if ($_.Exception.Message -cne 'SF-TEST-PG-PROCESS') {throw}}
                    $after=Read-OwnedRepositorySnapshot $origin
                    if ($before.Count -ne $after.Count -or [IO.Directory]::Exists((Join-Path $copy '.git'))) {throw 'Git resources changed.'}
                    foreach ($path in $before.Keys) {
                        if (!$after.ContainsKey($path) -or $after[$path] -cne $before[$path]) {throw 'Original Git repository mutated.'}
                    }
                    $env:GIT_DIR=$null;$env:GIT_WORK_TREE=$null
                    $objects=@(& git -C $origin cat-file --batch-all-objects '--batch-check=%(objectname)' 2>$null)
                    if ($LASTEXITCODE -ne 0 -or $objects -ccontains $privateBlob -or $objects -cnotcontains $solutionBlob -or
                        [IO.File]::Exists((Join-Path $copy 'private-witness.txt')) -or [IO.Directory]::Exists($upstream)) {throw 'Unexpected object download or private materialization.'}
                } finally {
                    $env:GIT_DIR=$originalGitDirectory;$env:GIT_WORK_TREE=$originalGitWorkTree
                    $env:SITEFACTORY_GIT_CONTEXT_WITNESS=$originalWitness
                    # Git marks owned object files read-only on Windows; only release this fixture's attributes after inspection.
                    foreach ($file in Get-ChildItem -LiteralPath $working -Force -Recurse -File) {
                        if ($file.Attributes -band [IO.FileAttributes]::ReadOnly) {
                            $file.Attributes=$file.Attributes -band (-bnot [IO.FileAttributes]::ReadOnly)
                        }
                    }
                }
            }
            """);
        AssertSilentSuccess(result);
    }

    [Fact]
    public async Task ActualPowerShellBridgeBoundsTimeoutAndDoesNotPublishStreams()
    {
        CliBytesResult result = await ScriptAsync("""
            $result=Invoke-PublicPostgreSqlProcess $bridge $working 'pwsh' @('-NoProfile','-NonInteractive','-Command',
                "[Console]::WriteLine('P04C_CANARY_A&B');Start-Sleep -Seconds 60") -TimeoutSeconds 2
            if ($result.Status -cne 'Timeout' -or $result.Cleanup -cne 'Completed' -or $result.ElapsedMs -gt 12000) {throw 'Timeout hidden.'}
            try {Assert-PublicPostgreSqlProcess $result;throw 'Timeout accepted.'}
            catch {if ($_.Exception.Message -cne 'SF-TEST-PG-TIMEOUT') {throw}}
            """);
        AssertSilentSuccess(result);
    }

    [Theory]
    [InlineData("exit")]
    [InlineData("timeout")]
    [InlineData("cancellation")]
    public async Task OwnedFamilyIsCleanedAfterParentExitTimeoutOrCancellationWithoutTouchingWitness(string completion)
    {
        string directory = Directory.CreateTempSubdirectory("sitefactory-p04c-execution-").FullName;
        using CancellationTokenSource deadline = new(TimeSpan.FromSeconds(40));
        using CancellationTokenSource cancel = CancellationTokenSource.CreateLinkedTokenSource(deadline.Token);
        await using Connection parent = new();
        await using Connection child = new();
        await using Connection witness = new();
        ProcessStartInfo witnessStart = new("dotnet") { UseShellExecute = false, CreateNoWindow = true };
        foreach (string argument in new[] { typeof(G04TestEntryPoint).Assembly.Location, G04TestEntryPoint.FixtureMode,
            "child", witness.Name, Path.Combine(directory, "witness") }) witnessStart.ArgumentList.Add(argument);
        using Process unrelated = Process.Start(witnessStart)!;
        await witness.ConnectAsync(deadline.Token);
        Task<PostgreSqlHarnessProcess.Result> run = PostgreSqlHarnessProcess.RunAsync(new(directory, "dotnet",
            [typeof(G04TestEntryPoint).Assembly.Location, G04TestEntryPoint.FixtureMode, "parent", parent.Name, child.Name,
                Path.Combine(directory, "held")], completion == "timeout" ? 5 : 20, []), cancel.Token);
        try
        {
            await parent.ConnectAsync(deadline.Token);
            await child.ConnectAsync(deadline.Token);
            Assert.False(parent.Process!.HasExited);
            Assert.False(child.Process!.HasExited);
            if (completion == "exit")
            {
                await parent.ReleaseAsync();
                await parent.Process.WaitForExitAsync(deadline.Token);
                // Deterministic orphan: the parent really exited while the child still holds a file/pipe.
            }
            if (completion == "cancellation") await cancel.CancelAsync();
            PostgreSqlHarnessProcess.Result result = await run.WaitAsync(deadline.Token);
            Assert.Equal(completion switch { "exit" => "Completed", "timeout" => "Timeout", _ => "Cancelled" }, result.Status);
            Assert.Equal("Completed", result.Cleanup);
            Assert.True(parent.Process.WaitForExit(3000));
            Assert.True(child.Process.WaitForExit(3000));
            Assert.False(unrelated.HasExited);
            using (FileStream file = new(Path.Combine(directory, "held"), FileMode.Open, FileAccess.ReadWrite, FileShare.None)) { }
        }
        finally
        {
            await cancel.CancelAsync();
            using CancellationTokenSource cleanupDeadline = new(TimeSpan.FromSeconds(15));
            await run.WaitAsync(cleanupDeadline.Token);
            await witness.ReleaseAsync();
            await unrelated.WaitForExitAsync(deadline.Token);
            await child.DisposeAsync();
            await parent.DisposeAsync();
            await witness.DisposeAsync();
            Directory.Delete(directory, recursive: true);
            Assert.False(Directory.Exists(directory));
        }
    }

    [Fact]
    public async Task AlreadyCancelledCommandStartsNothingAndKeepsCancellationDistinct()
    {
        using CancellationTokenSource cancelled = new();
        await cancelled.CancelAsync();
        PostgreSqlHarnessProcess.Result result = await PostgreSqlHarnessProcess.RunAsync(new(Path.GetTempPath(),
            "executable-that-does-not-exist", [], 2, []), cancelled.Token);
        Assert.Equal("Cancelled", result.Status);
        Assert.Equal("Completed", result.Cleanup);
        Assert.Empty(result.Output + result.Error);
    }

    [Theory]
    [InlineData("valid", "PASSED")]
    [InlineData("missing", "FAILED")]
    [InlineData("incomplete", "FAILED")]
    [InlineData("sensitive", "FAILED")]
    [InlineData("nonzero", "FAILED")]
    [InlineData("timeout", "FAILED")]
    [InlineData("cleanup", "FAILED")]
    [InlineData("over-budget", "FAILED")]
    public async Task ActualOwnedInvocationAndP04BArchiveCannotBeMadeGreenByExitCodeAlone(string scenario, string status)
    {
        CliBytesResult result = await ScriptAsync(PostgreSqlProofTests.ProofFixtures + "$case=" + Quote(scenario)
            + ";$expectedStatus=" + Quote(status) + "\n" + """
            $campaign=New-PublicPostgreSqlCampaign $working $manifest
            $invocation=Start-PublicPostgreSqlShardProof $campaign g05
            $document=New-FixtureTrx $invocation
            $now=[DateTimeOffset]::UtcNow.ToString('o')
            $document.TestRun.Times.SetAttribute('start',$now);$document.TestRun.Times.SetAttribute('finish',$now)
            foreach ($test in $document.TestRun.Results.UnitTestResult) {
                $test.SetAttribute('startTime',$now);$test.SetAttribute('endTime',$now);$test.SetAttribute('duration','00:00:00')
            }
            if ($case -ceq 'incomplete') {$null=$document.TestRun.Results.RemoveChild($document.TestRun.Results.FirstChild)}
            if ($case -ceq 'sensitive') {
                $node=$document.CreateElement('StdOut',$document.DocumentElement.NamespaceURI);$node.InnerText='P04C_CANARY_A&B'
                $null=$document.TestRun.Results.FirstChild.AppendChild($node)
            }
            $source=Join-Path $working 'fixture.xml';[IO.File]::WriteAllText($source,$document.OuterXml)
            $target=Resolve-PublicPostgreSqlEvidencePath $campaign.Root $invocation.TrxRelative
            $command=if ($case -ceq 'missing') {'exit 0'} else {"[IO.File]::Copy('"+$source.Replace("'","''")+"','"+$target.Replace("'","''")+"');"}
            if ($case -ceq 'nonzero') {$command+='exit 7'}
            if ($case -ceq 'timeout') {$command+='Start-Sleep -Seconds 60'}
            $process=Invoke-PublicPostgreSqlProcess $bridge $working 'pwsh' @('-NoProfile','-NonInteractive','-OutputFormat','Text',
                '-EncodedCommand',[Convert]::ToBase64String([Text.Encoding]::Unicode.GetBytes($command))) -TimeoutSeconds $(if ($case -ceq 'timeout') {2} else {10})
            $receipt=@{campaignId=$campaign.Id;invocationId=$invocation.Id;finishedUtc=[DateTimeOffset]::UtcNow;exitCode=[int]$process.ExitCode;
                status=$process.Status;processCleanup=$process.Cleanup;elapsedMs=$process.ElapsedMs}
            if ($case -ceq 'cleanup') {$campaign.Manifest.provenance='RealPostgreSql';$receipt.processCleanup='Failed'}
            if ($case -ceq 'over-budget') {$campaign.Manifest.provenance='RealPostgreSql';$receipt.elapsedMs=360001}
            $proof=Invoke-PublicPostgreSqlShardArchive $campaign $invocation $receipt $bridge $working $working @('P04C_CANARY_A&B')
            if ($case -ceq 'sensitive' -and $proof.code -cne 'SF-TEST-PG-SENSITIVE') {[Console]::Error.WriteLine($proof.code);throw 'Decoded XML guard not used.'}
            if ($proof.status -cne $expectedStatus -or $campaign.Manifest.status -cne 'FAILED' -or $campaign.Manifest.databaseProof) {[Console]::Error.WriteLine($proof.code);throw 'False complete database proof.'}
            if ($expectedStatus -ceq 'FAILED' -and [IO.File]::Exists((Join-Path $campaign.Root 'native/g05.trx'))) {throw 'Red native archived.'}
            foreach ($path in @((Join-Path $campaign.Root 'manifest.json'),(Join-Path $campaign.Root 'proof/g05.json'))) {
                if ([IO.File]::ReadAllText($path).Contains('P04C_CANARY_A&B')) {throw 'Disclosure.'}
            }
            if ([IO.File]::Exists($target)) {throw 'Incoming retained.'}
            """);
        AssertSilentSuccess(result);
    }

    [Theory]
    [InlineData("cleanup")]
    [InlineData("observations")]
    [InlineData("missing-shard")]
    public async Task WholeCampaignNeedsCleanupObservationsAndAllFiveShards(string missing)
    {
        CliBytesResult result = await ScriptAsync("$missing=" + Quote(missing) + "\n" + """
            $campaign=New-PublicPostgreSqlCampaign $working $manifest
            $campaign.Manifest.provenance='RealPostgreSql';$campaign.Manifest.postgreSql=180006
            $campaign.Manifest.imageDigest='sha256:3725f4e2499eef5134592b3b4ab79a543ed7f8e533b05b5b637af926630f6650'
            foreach ($shard in $campaign.Manifest.shards) {$shard.status='PASSED'}
            if ($missing -ceq 'missing-shard') {$campaign.Manifest.shards[4].status='NOT_RUN'}
            Complete-PublicPostgreSqlCampaign $campaign $(if ($missing -ceq 'cleanup') {'Failed'} else {'Completed'}) ($missing -cne 'observations')
            if ($campaign.Manifest.status -cne 'FAILED' -or $campaign.Manifest.databaseProof) {throw 'Missing condition ignored.'}
            """);
        AssertSilentSuccess(result);
    }

    [Fact]
    public async Task CleanupRejectsAnUnownedDirectoryAndPreservesExistingEvidence()
    {
        CliBytesResult result = await ScriptAsync("""
            $witness=Join-Path $working 'witness';$null=[IO.Directory]::CreateDirectory($witness)
            [IO.File]::WriteAllText((Join-Path $witness 'proof.json'),'existing')
            try {Remove-PublicPostgreSqlOwnedDirectory $witness $working 'witness';throw 'Unowned directory removed.'}
            catch {if ($_.Exception.Message -cne 'SF-TEST-PG-CLEANUP') {throw}}
            if ([IO.File]::ReadAllText((Join-Path $witness 'proof.json')) -cne 'existing') {throw 'Existing proof changed.'}
            $first=New-PublicPostgreSqlCampaign $working $manifest
            $bytes=[IO.File]::ReadAllBytes((Join-Path $first.Root 'manifest.json'))
            $second=New-PublicPostgreSqlCampaign $working $manifest
            if ($first.Id -ceq $second.Id -or ![Linq.Enumerable]::SequenceEqual[byte]($bytes,[IO.File]::ReadAllBytes((Join-Path $first.Root 'manifest.json')))) {throw 'Campaign reused.'}
            """);
        AssertSilentSuccess(result);
    }

    private static string Quote(string value) => "'" + value.Replace("'", "''", StringComparison.Ordinal) + "'";

    [Fact]
    public async Task ActualHarnessConnectionPreparationAcceptsTheCertificatePathWithoutPublishingCredentials()
    {
        CliBytesResult result = await ScriptAsync("""
            $entry=Join-Path ([IO.Path]::GetDirectoryName($manifest)) 'Invoke-PostgreSqlTests.ps1'
            $source=[IO.File]::ReadAllText($entry)
            $begin=$source.IndexOf('$connection=[Data.Common.DbConnectionStringBuilder]::new()',[StringComparison]::Ordinal)
            $end=$source.IndexOf('$adminFile=Resolve-PublicPostgreSqlEvidencePath',$begin,[StringComparison]::Ordinal)
            if ($begin -lt 0 -or $end -le $begin) {throw 'Actual preparation missing.'}
            $fixture=[Data.Common.DbConnectionStringBuilder]::new()
            $fixture['Password']='P04C_CANARY_A&B'
            $temporary=$working;$port=45678
            $password = $fixture['Password']
            . ([ScriptBlock]::Create($source.Substring($begin,$end-$begin)))
            if ($connection['Root Certificate'] -cne (Join-Path $working 'server.crt') -or $connection['Port'] -ne 45678 -or
                $connection['SSL Mode'] -cne 'VerifyFull' -or $connection['Host'] -cne '127.0.0.1' -or
                $connection['Database'] -cne 'sitefactory_test_admin' -or $connection['Username'] -cne 'sitefactory_test_admin' -or
                $connection['Password'] -cne $password) {throw 'Temporary connection changed.'}
            """);
        AssertSilentSuccess(result);
    }

    [Theory]
    [InlineData("valid", true)]
    [InlineData("linked-trace", true)]
    [InlineData("503", false)]
    [InlineData("missing-request", false)]
    [InlineData("missing-event", false)]
    [InlineData("wrong-activity", false)]
    [InlineData("invalid-type", false)]
    [InlineData("sensitive", false)]
    [InlineData("shared-activity", false)]
    [InlineData("zero-provider", false)]
    [InlineData("zero-lifecycle", false)]
    [InlineData("single-route", false)]
    [InlineData("duplicate-request", false)]
    [InlineData("foreign-campaign", false)]
    [InlineData("orphan-provider", false)]
    [InlineData("unclosed", false)]
    [InlineData("wrong-balanced-routes", false)]
    [InlineData("missing-provider-pair", false)]
    [InlineData("foreign-event", false)]
    public async Task P03ArchiveKeepsExactLoadCorrelationAndFilteredObservations(string scenario, bool complete)
    {
        CliBytesResult result = await ScriptAsync(ObservationFixture + "$case=" + Quote(scenario) + ";$expected=" + (complete ? "$true" : "$false") + "\n" + """
            $campaign=New-PublicPostgreSqlCampaign $working $manifest
            $data=New-ObservationFixture $campaign
            switch ($case) {
                '503' {$data.http[767].status=503}
                'missing-request' {$data.http=$data.http[0..766]}
                'missing-event' {$data.http[767].readinessEvents=@($data.http[767].readinessEvents[0])}
                'wrong-activity' {$data.http[767].readinessEvents[0].traceId='00000000000000000000000000000000'}
                'invalid-type' {$data.http[767].route=@('/health/ready','UNAPPROVED')}
                'sensitive' {$data.unapproved='P04C_CANARY_A&B'}
                'shared-activity' {foreach($request in $data.http) {$request.traceId=$data.http[0].traceId;$request.spanId=$data.http[0].spanId;
                    foreach($event in $request.readinessEvents){$event.traceId=$request.traceId;$event.spanId=$request.spanId}}}
                'linked-trace' {foreach($request in $data.http){$request.traceId=$data.http[0].traceId;
                    foreach($event in $request.readinessEvents){$event.traceId=$request.traceId}}}
                'zero-provider' {$data.providerEvents=@()}
                'zero-lifecycle' {$data.lifecycle=@()}
                'single-route' {foreach($request in $data.http){$request.route='/health/ready';$request.readinessEvents[1].tags.site='HEALTH_READY'}}
                'duplicate-request' {$data.http[767]=$data.http[766]}
                'foreign-campaign' {$campaign.Manifest.observationRun=[Guid]::NewGuid().ToString('N')}
                'orphan-provider' {$data.providerEvents[0].requestId=768}
                'unclosed' {$data.providerStatistics.remainingRequestCorrelatedConnections=1}
                'wrong-balanced-routes' {$first=$data.http[0];$second=$data.http[1];$route=$first.route;$site=$first.readinessEvents[1].tags.site;
                    $first.route=$second.route;$first.readinessEvents[1].tags.site=$second.readinessEvents[1].tags.site;
                    $second.route=$route;$second.readinessEvents[1].tags.site=$site}
                'missing-provider-pair' {$data.providerEvents[3].eventId=2000}
                'foreign-event' {$data.providerEvents[0].timestamp=90001}
            }
            Write-PublicPostgreSqlJson $source $data
            $actual=$false;$fixedCode=$null
            try {$actual=Save-PublicPostgreSqlObservations $campaign $source @('P04C_CANARY_A&B')}
            catch {$fixedCode=$_.Exception.Message;if($fixedCode -cnotin @('SF-TEST-PG-OBSERVATIONS','SF-TEST-PG-SENSITIVE')){throw}}
            if ($actual -cne $expected) {throw 'Incomplete observations accepted.'}
            if ([IO.File]::Exists((Join-Path $campaign.Root 'observations/p03.json'))) {
                $path=Join-Path $campaign.Root 'observations/p03.json'
                $text=[IO.File]::ReadAllText($path);$saved=$text|ConvertFrom-Json -AsHashtable
                if ($text.Contains('not-archived') -or $saved.workers -ne 32 -or $saved.requestsPerWorker -ne 24 -or
                    $saved.http.Count -ne $data.http.Count -or
                    ($expected -and ($saved.http200 -ne 768 -or $saved.http503 -ne 0 -or $saved.completeness -cne 'Complete'))) {throw 'Observation contract changed.'}
                if (!$expected -and $saved.completeness -cne 'Partial') {throw 'Partial presented as complete.'}
            }
            $campaign.Manifest.observationsComplete=$actual
            $campaign.Manifest.provenance='RealPostgreSql';$campaign.Manifest.postgreSql=180006
            $campaign.Manifest.imageDigest='sha256:3725f4e2499eef5134592b3b4ab79a543ed7f8e533b05b5b637af926630f6650'
            foreach($record in $campaign.Manifest.shards){$record.status='PASSED'}
            Invoke-PublicPostgreSqlFinalization $campaign $bridge $working Completed $actual
            if($campaign.Manifest.status -cne $(if($expected){'PASSED'}else{'FAILED'})){throw 'Observation decision ignored by finalizer.'}
            if(!$expected -and $campaign.Manifest.code -cne 'SF-TEST-PG-OBSERVATIONS'){throw 'Missing fixed observation diagnostic.'}
            """);
        AssertSilentSuccess(result);
    }

    [Theory]
    [InlineData("valid", true)]
    [InlineData("zero-provider", false)]
    [InlineData("shared-activity", false)]
    public async Task ActualP03ShardArchiveAndFinalizerRequireCompleteObservations(string scenario, bool complete)
    {
        CliBytesResult result = await ScriptAsync(PostgreSqlProofTests.ProofFixtures + ObservationFixture
            + "$case=" + Quote(scenario) + ";$expected=" + (complete ? "$true" : "$false") + "\n" + """
            $campaign=New-PublicPostgreSqlCampaign $working $manifest
            $data=New-ObservationFixture $campaign
            if($case -ceq 'zero-provider'){$data.providerEvents=@()}
            if($case -ceq 'shared-activity'){foreach($request in $data.http){$request.traceId=$data.http[0].traceId;$request.spanId=$data.http[0].spanId;
                foreach($event in $request.readinessEvents){$event.traceId=$request.traceId;$event.spanId=$request.spanId}}}
            Write-PublicPostgreSqlJson $source $data
            # Exercise real finalization in an isolated synthetic fixture, never a database proof campaign.
            $campaign.Manifest.provenance='RealPostgreSql';$campaign.Manifest.postgreSql=180006
            $campaign.Manifest.imageDigest='sha256:3725f4e2499eef5134592b3b4ab79a543ed7f8e533b05b5b637af926630f6650'
            foreach($record in $campaign.Manifest.shards){if($record.shard -cne 'p03'){$record.status='PASSED'}}
            $invocation=Start-PublicPostgreSqlShardProof $campaign p03
            $document=New-FixtureTrx $invocation;$now=[DateTimeOffset]::UtcNow.ToString('o')
            $document.TestRun.Times.SetAttribute('start',$now);$document.TestRun.Times.SetAttribute('finish',$now)
            foreach($test in $document.TestRun.Results.UnitTestResult){$test.SetAttribute('startTime',$now);$test.SetAttribute('endTime',$now);$test.SetAttribute('duration','00:00:00')}
            $sourceTrx=Join-Path $working 'fixture.xml';[IO.File]::WriteAllText($sourceTrx,$document.OuterXml)
            $target=Resolve-PublicPostgreSqlEvidencePath $campaign.Root $invocation.TrxRelative
            $command="[IO.File]::Copy('"+$sourceTrx.Replace("'","''")+"','"+$target.Replace("'","''")+"');exit 0"
            $process=Invoke-PublicPostgreSqlProcess $bridge $working 'pwsh' @('-NoProfile','-NonInteractive','-EncodedCommand',
                [Convert]::ToBase64String([Text.Encoding]::Unicode.GetBytes($command))) -TimeoutSeconds 10
            Assert-PublicPostgreSqlProcess $process
            $receipt=@{campaignId=$campaign.Id;invocationId=$invocation.Id;finishedUtc=[DateTimeOffset]::UtcNow;exitCode=[int]$process.ExitCode;
                status=$process.Status;processCleanup=$process.Cleanup;elapsedMs=$process.ElapsedMs}
            $proof=Invoke-PublicPostgreSqlShardArchive $campaign $invocation $receipt $bridge $working $working @() $source
            Invoke-PublicPostgreSqlFinalization $campaign $bridge $working Completed $campaign.Manifest.observationsComplete
            if($campaign.Manifest.observationsComplete -cne $expected -or $campaign.Manifest.status -cne $(if($expected){'PASSED'}else{'FAILED'}) -or
                $proof.status -cne $(if($expected){'PASSED'}else{'FAILED'}) -or $proof.processExitCode -ne 0 -or
                $proof.processStatus -cne 'Completed'){throw 'Real archiver ignored observations or changed test outcome.'}
            if(!$expected -and ($proof.code -cne 'SF-TEST-PG-OBSERVATIONS' -or [IO.File]::Exists((Join-Path $campaign.Root 'native/p03.trx')))){throw 'Incomplete P03 archived as valid.'}
            if([IO.File]::Exists($target)){throw 'Incoming retained.'}
            """);
        AssertSilentSuccess(result);
    }

    // The real P03 route formula, six read-only commands and passive provider sequence.
    // Synthetic data only: no host, database, native SQL or provider messages.
    private const string ObservationFixture = """
        function New-ObservationFixture($campaign) {
            $run=[Guid]::NewGuid().ToString('N');$campaign.Manifest.observationRun=$run
            $global:source=Join-Path ([IO.Directory]::CreateDirectory((Join-Path $working $run)).FullName) 'observations.json'
            $requests=@();$provider=[Collections.Generic.List[object]]::new()
            $routes=@('/health/ready','/health','/');$sites=@('HEALTH_READY','HEALTH_LEGACY','TRAFFIC_GATE')
            $sequence=@(1000,1001,2000,2001,2000,2001,2000,2001,2000,2001,2000,2001,2000,2001,1003,1004)
            for($id=0;$id -lt 768;$id++) {
                $tick=100+$id*100;$routeIndex=([int][Math]::Floor($id/24)+($id%24))%3
                $trace=([long]($id+1)).ToString('x32');$span=([long]($id+1)).ToString('x16')
                $requests+=@{requestId=$id;route=$routes[$routeIndex];startTimestamp=$tick;endTimestamp=$tick+99;durationMilliseconds=0.0099;
                    completionOrder=$id+1;status=200;exceptionKind='NONE';serverStartTimestamp=$tick+1;serverEndTimestamp=$tick+90;
                    serverExceptionKind='NONE';startedBefore=$true;startedAfter=$true;stoppingBefore=$false;stoppingAfter=$false;
                    requestAborted=$false;openAttempts=1;openedConnections=1;closedConnections=1;traceId=$trace;spanId=$span;
                    serverActivity=$true;refusalCategory='NONE';readinessEvents=@(
                        @{name='sitefactory.readiness.refresh.completed';traceId=$trace;spanId=$span;
                            tags=@{startTicks=$tick+2;endTicks=$tick+80;result=$true;outcome='READY';diagnosticCode=$null}},
                        @{name='sitefactory.readiness.http.decision';traceId=$trace;spanId=$span;
                            tags=@{site=$sites[$routeIndex];outcome='ACCEPTED';refreshResult=$true}})}
                for($step=0;$step -lt $sequence.Count;$step++) {$eventId=$sequence[$step];$provider.Add(@{requestId=$id;
                    category=$(if($eventId -lt 2000){'Npgsql.Connection'}else{'Npgsql.Command'});eventId=$eventId;
                    timestamp=$tick+5+$step;exceptionKind='NONE'})}
            }
            return @{schemaVersion=1;correction='P03-CORR-004';workers=32;requestsPerWorker=24;stopwatchFrequency=10000000;
                http=$requests;providerEvents=$provider.ToArray();providerStatistics=@{maximumRequestCorrelatedConnections=1;
                    remainingRequestCorrelatedConnections=0;droppedEvents=0};lifecycle=@(
                        @{kind='APPLICATION_STARTED';timestamp=50},@{kind='APPLICATION_STOPPING';timestamp=80000},@{kind='APPLICATION_STOPPED';timestamp=80001});
                resources=@{before=@{timestamp=1;processId=42};after=@{timestamp=90000;processId=42}};unapproved='not-archived'}
        }
        """;

    [Theory]
    [InlineData("valid")]
    [InlineData("missing-pwsh")]
    [InlineData("canary")]
    [InlineData("archive-exit")]
    [InlineData("missing-archive")]
    [InlineData("cancellation")]
    [InlineData("archive-timeout")]
    [InlineData("proof-write")]
    [InlineData("cleanup-link")]
    [InlineData("next-not-launched")]
    public async Task ExecutedShardSurvivesArchiveFailureAsRedEvidenceWithoutIncoming(string scenario)
    {
        CliBytesResult result = await ScriptAsync(PostgreSqlProofTests.ProofFixtures + "$case=" + Quote(scenario) + "\n" + """
            $campaign=New-PublicPostgreSqlCampaign $working $manifest
            $invocation=Start-PublicPostgreSqlShardProof $campaign g05
            $document=New-FixtureTrx $invocation;$now=[DateTimeOffset]::UtcNow.ToString('o')
            $document.TestRun.Times.SetAttribute('start',$now);$document.TestRun.Times.SetAttribute('finish',$now)
            foreach($test in $document.TestRun.Results.UnitTestResult){$test.SetAttribute('startTime',$now);$test.SetAttribute('endTime',$now);$test.SetAttribute('duration','00:00:00')}
            if($case -ceq 'canary'){$node=$document.CreateElement('StdOut',$document.DocumentElement.NamespaceURI);$node.InnerText='P04C_CANARY_A&B';$null=$document.TestRun.Results.FirstChild.AppendChild($node)}
            $sourceTrx=Join-Path $working 'fixture.xml';[IO.File]::WriteAllText($sourceTrx,$document.OuterXml)
            $target=Resolve-PublicPostgreSqlEvidencePath $campaign.Root $invocation.TrxRelative
            $command="[IO.File]::Copy('"+$sourceTrx.Replace("'","''")+"','"+$target.Replace("'","''")+"');exit 0"
            $process=Invoke-PublicPostgreSqlProcess $bridge $working 'pwsh' @('-NoProfile','-NonInteractive','-EncodedCommand',
                [Convert]::ToBase64String([Text.Encoding]::Unicode.GetBytes($command))) -TimeoutSeconds 10
            Assert-PublicPostgreSqlProcess $process
            if(![IO.File]::Exists($target)){throw 'Fixture did not produce incoming.'}
            $receipt=@{campaignId=$campaign.Id;invocationId=$invocation.Id;finishedUtc=[DateTimeOffset]::UtcNow;exitCode=[int]$process.ExitCode;
                status=$process.Status;processCleanup=$process.Cleanup;elapsedMs=$process.ElapsedMs}
            $originalPath=$env:PATH
            $actualTransport=${function:Invoke-PublicPostgreSqlProcess};$cancel=[Threading.CancellationTokenSource]::new()
            $outside=$null;$link=$null
            try {
                if($case -cin @('archive-exit','archive-timeout','missing-archive')) {
                    # Fault only the archiver transport; still run its real child under the unchanged supervisor.
                    $script:archiveFault=$case;$script:archiveTransport=$actualTransport;$script:faultCalls=0
                    function Invoke-PublicPostgreSqlProcess($Bridge,$Directory,$Executable,$Arguments,$EnvironmentValues=@{},$TimeoutSeconds=180) {
                        $script:faultCalls++
                        if($script:faultCalls -ne 1){throw 'Unexpected archive retry.'}
                        if($script:archiveFault -cin @('archive-exit','archive-timeout')) {
                            $code=if($script:archiveFault -ceq 'archive-exit'){"[Console]::WriteLine('P04C_CANARY_A&B');exit 7"}
                                else {"[Console]::Error.WriteLine('P04C_CANARY_A&B');Start-Sleep -Seconds 60"}
                            return & $script:archiveTransport $Bridge $Directory $Executable @('-NoProfile','-NonInteractive','-Command',$code) $EnvironmentValues $(if($script:archiveFault -ceq 'archive-timeout'){1}else{10})
                        }
                        $result=& $script:archiveTransport $Bridge $Directory $Executable $Arguments $EnvironmentValues $TimeoutSeconds
                        Remove-PublicPostgreSqlOwnedEvidence $campaign 'proof/g05.json'
                        return $result
                    }
                }
                if($case -ceq 'cancellation'){$cancel.Cancel()}
                if($case -ceq 'proof-write') {
                    $proofDirectory=Resolve-PublicPostgreSqlEvidencePath $campaign.Root 'proof'
                    [IO.Directory]::Delete($proofDirectory,$false);[IO.File]::WriteAllText($proofDirectory,'owned obstruction')
                }
                if($case -ceq 'cleanup-link') {
                    $outside=[IO.Directory]::CreateDirectory((Join-Path $working 'outside-witness')).FullName
                    [IO.File]::Move($target,(Join-Path $outside 'g05.trx'))
                    $link=Resolve-PublicPostgreSqlEvidencePath $campaign.Root 'incoming'
                    [IO.Directory]::Delete($link,$false)
                    $null=New-Item -ItemType $(if($IsWindows){'Junction'}else{'SymbolicLink'}) -Path $link -Target $outside
                }
                if($case -cin @('missing-pwsh','canary','proof-write','cleanup-link','next-not-launched')) {
                    $hostFile=[IO.FileInfo]::new((Get-Command dotnet -CommandType Application).Source)
                    if($hostFile.LinkTarget){$hostFile=$hostFile.ResolveLinkTarget($true)}
                    $env:PATH=$hostFile.DirectoryName
                    if(Get-Command pwsh -CommandType Application -ErrorAction SilentlyContinue){throw 'Controlled PATH still contains archiver.'}
                }
                $proof=Invoke-PublicPostgreSqlShardArchive $campaign $invocation $receipt $bridge $working $working @('P04C_CANARY_A&B') -CancellationToken $cancel.Token
                ${function:Invoke-PublicPostgreSqlProcess}=$actualTransport
                # Exercise the actual entrypoint's remaining-shard transition, then its actual finalizer.
                $entry=[IO.File]::ReadAllText((Join-Path ([IO.Path]::GetDirectoryName($manifest)) 'Invoke-PostgreSqlTests.ps1'))
                $tokens=$null;$parseErrors=$null;$ast=[Management.Automation.Language.Parser]::ParseInput($entry,[ref]$tokens,[ref]$parseErrors)
                $loop=@($ast.FindAll({param($node) $node -is [Management.Automation.Language.ForEachStatementAst] -and
                    $node.Extent.Text.StartsWith('foreach ($shard in $campaign.Manifest.shards)',[StringComparison]::Ordinal)},$true))
                if($loop.Count -ne 1){throw 'Entrypoint transition unavailable.'}
                . ([ScriptBlock]::Create($loop[0].Extent.Text))
                Invoke-PublicPostgreSqlFinalization $campaign $bridge $working Completed $false
            } finally {$env:PATH=$originalPath;${function:Invoke-PublicPostgreSqlProcess}=$actualTransport;$cancel.Dispose()}
            $saved=Get-Content -LiteralPath (Join-Path $campaign.Root 'manifest.json') -Raw|ConvertFrom-Json -AsHashtable
            $record=@($saved.shards|Where-Object shard -CEQ g05)[0]
            if($record.status -cne $(if($case -ceq 'valid'){'PASSED'}else{'FAILED'}) -or $saved.status -cne 'FAILED'){throw 'Executed shard confused with NOT_RUN.'}
            if(@($saved.shards|Where-Object { $_.shard -cne 'g05' -and $_.status -cne 'NOT_RUN'}).Count -ne 0){throw 'Unlaunched shard changed.'}
            if($case -ceq 'cleanup-link') {
                if(![IO.File]::Exists((Join-Path $outside 'g05.trx')) -or $saved.cleanup -cne 'Failed'){throw 'Link cleanup escaped or failure hidden.'}
                # Removing the owned link after inspecting the red proof does not validate campaign cleanup.
                [IO.Directory]::Delete($link,$false)
            } elseif([IO.File]::Exists($target)){throw 'Uncontrolled incoming survived.'}
            if($case -ceq 'proof-write') {
                if($record.proof -or $record.code -cne 'SF-TEST-PG-ARCHIVE' -or $saved.code -ceq $null){throw 'Persistence failure hidden.'}
                return
            }
            $savedProof=Get-Content -LiteralPath (Join-Path $campaign.Root 'proof/g05.json') -Raw|ConvertFrom-Json -AsHashtable
            $expectedCode=switch($case){'cancellation'{'SF-TEST-PG-CANCELLED'} 'archive-timeout'{'SF-TEST-PG-TIMEOUT'}
                'missing-archive'{'SF-TEST-PG-ARCHIVE'} 'cleanup-link'{'SF-TEST-PG-CLEANUP'} default{'SF-TEST-PG-PROCESS'}}
            if($savedProof.processStatus -cne 'Completed' -or $savedProof.processExitCode -ne 0){throw 'Test process result overwritten by proof failure.'}
            if($case -cne 'valid' -and ($savedProof.status -cne 'FAILED' -or $savedProof.code -cne $expectedCode -or
                $savedProof.incomingCleanup -cne $(if($case -ceq 'cleanup-link'){'Failed'}else{'Completed'}) -or
                [IO.File]::Exists((Join-Path $campaign.Root 'native/g05.trx')))){throw 'Missing safe archive fallback.'}
            if($campaign.Invocations.Count -ne 1 -or $campaign.Invocations.ContainsKey('p01')){throw 'Next shard launched after failure.'}
            foreach($file in Get-ChildItem -LiteralPath $campaign.Root -Recurse -File){if([IO.File]::ReadAllText($file.FullName).Contains('P04C_CANARY_A&B')){throw 'Persistent disclosure.'}}
            """);
        AssertSilentSuccess(result);
    }

    private static async Task<CliBytesResult> ScriptAsync(string body)
    {
        string working = Directory.CreateTempSubdirectory("sitefactory-p04c-execution-").FullName;
        try
        {
            string directory = Path.Combine(AppContext.BaseDirectory, "PostgreSql", "Public");
            string script = "$ErrorActionPreference='Stop'\n$working=" + Quote(working) + "\n$bridge="
                + Quote(typeof(PostgreSqlHarnessProcess).Assembly.Location) + "\n$manifest=" + Quote(Path.Combine(directory, "shards.json"))
                + "\n. " + Quote(Path.Combine(directory, "PostgreSqlProof.ps1")) + "\ntry {\n" + body
                + "\nexit 0\n} catch {[Console]::Error.WriteLine('SF-TEST-PG-FIXTURE '+$_.InvocationInfo.ScriptLineNumber);exit 1}";
            // The real sparse-clone scenario exceeds Windows' command-line limit when encoded inline.
            // Keep its script in the owned directory, removed only after process-family cleanup.
            string scriptPath = Path.Combine(working, "harness-fixture.ps1");
            await File.WriteAllTextAsync(scriptPath, script, new UTF8Encoding(false), TestContext.Current.CancellationToken);
            ProcessStartInfo start = MigratorProcessRunner.CreateIsolatedStartInfo(working);
            start.FileName = "pwsh";
            foreach (string argument in new[] { "-NoProfile", "-NonInteractive", "-OutputFormat", "Text", "-File", scriptPath })
                start.ArgumentList.Add(argument);
            return await MigratorProcessRunner.RunOwnedAsync(start, TimeSpan.FromSeconds(45), TestContext.Current.CancellationToken);
        }
        finally
        {
            Assert.Equal(Path.TrimEndingDirectorySeparator(Path.GetTempPath()), Path.GetDirectoryName(working));
            Assert.StartsWith("sitefactory-p04c-execution-", Path.GetFileName(working), StringComparison.Ordinal);
            Directory.Delete(working, recursive: true);
            Assert.False(Directory.Exists(working));
        }
    }

    private static void AssertSilentSuccess(CliBytesResult result)
    {
        Assert.DoesNotContain("P04C_CANARY_A&B", Encoding.UTF8.GetString(result.StandardOutput) + Encoding.UTF8.GetString(result.StandardError), StringComparison.Ordinal);
        Assert.True(result.ExitCode == 0, Encoding.UTF8.GetString(result.StandardError));
        Assert.Empty(result.StandardOutput);
        Assert.Empty(result.StandardError);
    }

    private sealed class Connection : IAsyncDisposable
    {
        public string Name { get; } = "sf-p04c-" + Guid.NewGuid().ToString("N");
        private readonly NamedPipeServerStream pipe;
        private StreamWriter? writer;
        public Process? Process { get; private set; }
        public Connection() => pipe = new(Name, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
        public async Task ConnectAsync(CancellationToken token)
        {
            await pipe.WaitForConnectionAsync(token);
            using StreamReader reader = new(pipe, leaveOpen: true);
            Process = System.Diagnostics.Process.GetProcessById(int.Parse((await reader.ReadLineAsync(token))!, CultureInfo.InvariantCulture));
            _ = Process.SafeHandle;
            writer = new(pipe, leaveOpen: true) { AutoFlush = true };
        }
        public async Task ReleaseAsync()
        {
            if (writer is not null) { try { await writer.WriteLineAsync("release"); } catch (IOException) { } }
        }
        public async ValueTask DisposeAsync()
        {
            await ReleaseAsync();
            writer = null;
            if (Process is not null) { Assert.True(Process.WaitForExit(5000)); Process.Dispose(); Process = null; }
            await pipe.DisposeAsync();
        }
    }
}
