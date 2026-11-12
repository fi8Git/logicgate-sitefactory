# Public test-only harness. Default is offline discovery; database execution is explicit.
[CmdletBinding()]
param(
    [string] $RepositoryRoot,
    [string] $ManifestPath = (Join-Path $PSScriptRoot 'shards.json'),
    [switch] $RunRequired,
    [string] $ProofDirectory
)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $false
. (Join-Path $PSScriptRoot 'PostgreSqlProof.ps1')
$campaign=$null; $temporary=$null; $container=$null; $creationAttempted=$false
$cleanupCompleted=$true; $beforeContainers=$null; $exitCode=1; $diagnostic='SF-TEST-PG-HARNESS'
$image='docker.io/library/postgres:18.6-bookworm@sha256:3725f4e2499eef5134592b3b4ab79a543ed7f8e533b05b5b637af926630f6650'
$preparationTimer=[Diagnostics.Stopwatch]::StartNew()

try {
    $partition=Read-PublicPostgreSqlPartition $ManifestPath
    if (!$RepositoryRoot) {
        $directory=[IO.DirectoryInfo]::new($PSScriptRoot)
        while ($null -ne $directory -and ![IO.File]::Exists((Join-Path $directory.FullName 'SiteFactory.slnx'))) { $directory=$directory.Parent }
        if ($null -eq $directory) { throw 'SF-TEST-PG-DISCOVERY' }
        $RepositoryRoot=$directory.FullName
    }
    $RepositoryRoot=[IO.Path]::GetFullPath($RepositoryRoot)
    $bridge=Join-Path $RepositoryRoot 'tests/LogicGate.SiteFactory.Infrastructure.PostgreSql.Tests/bin/Release/net10.0/LogicGate.SiteFactory.Infrastructure.PostgreSql.Tests.dll'
    if ($RunRequired) {
        if (!$ProofDirectory) { $ProofDirectory=Join-Path $RepositoryRoot 'artifacts/postgresql-public' }
        $ProofDirectory=[IO.Path]::GetFullPath($ProofDirectory)
        for ($cursor=$ProofDirectory; $cursor; $cursor=[IO.Path]::GetDirectoryName($cursor)) {
            if ([IO.Path]::Exists($cursor) -and ([IO.File]::GetAttributes($cursor) -band [IO.FileAttributes]::ReparsePoint)) { throw 'SF-TEST-PG-PATH' }
        }
        $null=[IO.Directory]::CreateDirectory($ProofDirectory)
        $campaign=New-PublicPostgreSqlCampaign $ProofDirectory $ManifestPath
        $campaign.Manifest.resourceCleanup=@{database='NotObserved';container='NotObserved';secrets='NotObserved';preexisting='NotObserved'}
        $campaign.Manifest.provenance='RealPostgreSql'
        $campaign.Manifest.budgets=@{shardSeconds=360;discoverySeconds=180;dockerSeconds=180;readinessSeconds=60;configurationSeconds=30;archiveSeconds=30;cleanupOperationSeconds=30;scopeCleanupSeconds=10;bridgeTransportAllowanceSeconds=25}
        $campaign.Manifest.observationsComplete=$false
        Protect-PublicPostgreSqlDirectory $campaign.Root
        Write-PublicPostgreSqlJson (Resolve-PublicPostgreSqlEvidencePath $campaign.Root 'manifest.json') $campaign.Manifest
    }
    if (![IO.File]::Exists($bridge)) { throw 'SF-TEST-PG-DISCOVERY' }
    $runner={
        param($project,$filter)
        Invoke-PublicPostgreSqlProcess $bridge $RepositoryRoot 'dotnet' @('test',(Join-Path $RepositoryRoot $project),
            '--configuration','Release','--no-build','--no-restore','--list-tests','--filter',$filter) -TimeoutSeconds 180
    }
    $discovery=@(Invoke-PublicPostgreSqlInventories $partition $runner)
    if (!$RunRequired) {
        Write-Output 'SF-TEST-PG-INVENTORY Offline discovery verified: Infrastructure=20, Hosts=3, disjoint shards=5. No PostgreSQL test executed; no database proof.'
        $exitCode=0
    } else {
        Write-PublicPostgreSqlJson (Resolve-PublicPostgreSqlEvidencePath $campaign.Root 'discovery/inventory.json') $discovery -CreateNew
        function Invoke-RequiredCommand([string] $Executable,[string[]] $Arguments,[int] $Seconds=180,[hashtable] $EnvironmentValues=@{}) {
            $result=Invoke-PublicPostgreSqlProcess $bridge $RepositoryRoot $Executable $Arguments $EnvironmentValues -TimeoutSeconds $Seconds
            Assert-PublicPostgreSqlProcess $result
            return $result.Output.Trim()
        }
        $gitEnvironment=@{}
        $gitDirectory=[Environment]::GetEnvironmentVariable('GIT_DIR')
        $gitWorkTree=[Environment]::GetEnvironmentVariable('GIT_WORK_TREE')
        if ($null -ne $gitDirectory -or $null -ne $gitWorkTree) {
            try {
                if ([string]::IsNullOrWhiteSpace($gitDirectory) -or [string]::IsNullOrWhiteSpace($gitWorkTree) -or
                    ![IO.Path]::IsPathFullyQualified($gitDirectory) -or ![IO.Path]::IsPathFullyQualified($gitWorkTree)) {
                    throw 'SF-TEST-PG-PROVENANCE'
                }
                $gitDirectory=[IO.Path]::TrimEndingDirectorySeparator([IO.Path]::GetFullPath($gitDirectory))
                $gitWorkTree=[IO.Path]::TrimEndingDirectorySeparator([IO.Path]::GetFullPath($gitWorkTree))
                $comparison=if ($IsWindows) {[StringComparison]::OrdinalIgnoreCase} else {[StringComparison]::Ordinal}
                if (![IO.Directory]::Exists($gitDirectory) -or ![IO.File]::Exists((Join-Path $gitDirectory 'HEAD')) -or
                    ![IO.Directory]::Exists($gitWorkTree) -or
                    ![string]::Equals($gitWorkTree,[IO.Path]::TrimEndingDirectorySeparator($RepositoryRoot),$comparison)) {
                    throw 'SF-TEST-PG-PROVENANCE'
                }
                $gitEnvironment=@{GIT_DIR=$gitDirectory;GIT_WORK_TREE=$gitWorkTree}
            } catch { throw 'SF-TEST-PG-PROVENANCE' }
        }
        $campaign.Manifest.commit=Invoke-RequiredCommand 'git' @('rev-parse','HEAD') -EnvironmentValues $gitEnvironment
        if ($campaign.Manifest.commit -cnotmatch '^[0-9a-f]{40}$') { throw 'SF-TEST-PG-PROVENANCE' }
        $state=Invoke-RequiredCommand 'git' @('--no-optional-locks','-c','core.fsmonitor=false','status','--porcelain=v1') -EnvironmentValues $gitEnvironment
        $campaign.Manifest.gitState=if ($state) {'Dirty'} else {'Clean'}
        $campaign.Manifest.sdk=Invoke-RequiredCommand 'dotnet' @('--version')
        if ($campaign.Manifest.sdk -cnotmatch '^\d{1,3}\.\d{1,3}\.\d{1,4}$') { throw 'SF-TEST-PG-PROVENANCE' }
        $runtimes=Invoke-RequiredCommand 'dotnet' @('--list-runtimes')
        $versions=[regex]::Matches($runtimes,'(?m)^Microsoft\.NETCore\.App (10\.\d{1,3}\.\d{1,4}) ')
        if ($versions.Count -eq 0) { throw 'SF-TEST-PG-PROVENANCE' }
        $campaign.Manifest.runtime=$versions[$versions.Count-1].Groups[1].Value
        if ([Environment]::GetEnvironmentVariable('SITEFACTORY_TEST_POSTGRES_ADMIN_FILE')) { throw 'SF-TEST-PG-CONFIG' }
        $beforeContainers=Invoke-RequiredCommand 'docker' @('ps','--all','--quiet','--no-trunc')
        $null=Invoke-RequiredCommand 'docker' @('version','--format','{{.Server.Version}}')
        $null=Invoke-RequiredCommand 'docker' @('pull',$image)
        $name='sitefactory-p04c-'+$campaign.Id
        $temporary=Join-Path ([IO.Path]::GetTempPath()) $name
        if ([IO.Path]::Exists($temporary)) { throw 'SF-TEST-PG-PROVENANCE' }
        $null=[IO.Directory]::CreateDirectory($temporary)
        Protect-PublicPostgreSqlDirectory $temporary
        $prepare=@'
$ErrorActionPreference='Stop'
$directory=DIRECTORY
$key=[Security.Cryptography.RSA]::Create(2048);$certificate=$null
try {
    $request=[Security.Cryptography.X509Certificates.CertificateRequest]::new('CN=sitefactory-postgresql-test',$key,
        [Security.Cryptography.HashAlgorithmName]::SHA256,[Security.Cryptography.RSASignaturePadding]::Pkcs1)
    $san=[Security.Cryptography.X509Certificates.SubjectAlternativeNameBuilder]::new();$san.AddIpAddress([Net.IPAddress]::Loopback)
    $request.CertificateExtensions.Add($san.Build())
    $request.CertificateExtensions.Add([Security.Cryptography.X509Certificates.X509BasicConstraintsExtension]::new($true,$false,0,$true))
    $certificate=$request.CreateSelfSigned([DateTimeOffset]::UtcNow.AddMinutes(-5),[DateTimeOffset]::UtcNow.AddDays(1))
    [IO.File]::WriteAllText((Join-Path $directory 'server.crt'),$certificate.ExportCertificatePem())
    [IO.File]::WriteAllText((Join-Path $directory 'server.key'),$key.ExportPkcs8PrivateKeyPem())
    [IO.File]::WriteAllText((Join-Path $directory 'password'),[Convert]::ToHexString([Security.Cryptography.RandomNumberGenerator]::GetBytes(32)))
    if (!$IsWindows) { foreach ($file in 'server.crt','server.key','password') {
        [IO.File]::SetUnixFileMode((Join-Path $directory $file),[IO.UnixFileMode]::UserRead -bor [IO.UnixFileMode]::UserWrite)
    } }
} finally { if ($null -ne $certificate) {$certificate.Dispose()};$key.Dispose() }
'@.Replace('DIRECTORY',"'" + $temporary.Replace("'","''") + "'")
        $null=Invoke-RequiredCommand 'pwsh' @('-NoProfile','-NonInteractive','-OutputFormat','Text','-EncodedCommand',
            [Convert]::ToBase64String([Text.Encoding]::Unicode.GetBytes($prepare))) 30
        $bootstrap='set -eu; mkdir /tmp/sf-secrets; cp /run/sf-test/server.crt /run/sf-test/server.key /run/sf-test/password /tmp/sf-secrets/; chown -R postgres:postgres /tmp/sf-secrets; chmod 700 /tmp/sf-secrets; chmod 600 /tmp/sf-secrets/*; exec docker-entrypoint.sh postgres -c ssl=on -c ssl_cert_file=/tmp/sf-secrets/server.crt -c ssl_key_file=/tmp/sf-secrets/server.key'
        $creationAttempted=$true
        $container=Invoke-RequiredCommand 'docker' @('run','--detach','--name',$name,'--label',"sitefactory.test-run=$name",
            '--security-opt','no-new-privileges:true','--publish','127.0.0.1::5432','--tmpfs','/var/lib/postgresql:rw,nosuid,size=256m',
            '--tmpfs','/tmp:rw,noexec,nosuid,size=16m','--mount',"type=bind,source=$temporary,target=/run/sf-test,readonly",
            '--env','POSTGRES_USER=sitefactory_test_admin','--env','POSTGRES_DB=sitefactory_test_admin',
            '--env','POSTGRES_PASSWORD_FILE=/tmp/sf-secrets/password','--env','POSTGRES_INITDB_ARGS=--auth-host=scram-sha-256',
            '--entrypoint','bash',$image,'-c',$bootstrap)
        if ($container -cnotmatch '^[0-9a-f]{64}$') { throw 'SF-TEST-PG-CONTAINER' }
        $ready=$false;$readiness=[Diagnostics.Stopwatch]::StartNew()
        while ($readiness.Elapsed.TotalSeconds -lt 60) {
            $seconds=[Math]::Min(5,[Math]::Max(1,[int][Math]::Floor(60-$readiness.Elapsed.TotalSeconds)))
            $probe=Invoke-PublicPostgreSqlProcess $bridge $RepositoryRoot 'docker' @('exec',$container,'pg_isready','-h','127.0.0.1','-U','sitefactory_test_admin','-d','sitefactory_test_admin') -TimeoutSeconds $seconds
            if ($probe.Status -cne 'Completed' -or $probe.Cleanup -cne 'Completed') { throw 'SF-TEST-PG-START' }
            if ($probe.ExitCode -eq 0) {$ready=$true;break}
            Start-Sleep -Milliseconds 100
        }
        if (!$ready -or $readiness.Elapsed.TotalSeconds -gt 60) { throw 'SF-TEST-PG-START' }
        $psql=@('exec',$container,'psql','--no-psqlrc','--username=sitefactory_test_admin','--dbname=sitefactory_test_admin','--tuples-only','--no-align','--set=ON_ERROR_STOP=1','--command')
        $null=Invoke-RequiredCommand 'docker' ($psql+'REVOKE ALL ON DATABASE postgres, template0, template1, sitefactory_test_admin FROM PUBLIC;')
        $version=Invoke-RequiredCommand 'docker' ($psql+"SELECT pg_catalog.current_setting('server_version_num');")
        if ($version -cne '180006') { throw 'SF-TEST-PG-VERSION' }
        $actualImage=Invoke-RequiredCommand 'docker' @('inspect','--format','{{.Config.Image}}',$container)
        if ($actualImage -cne $image) { throw 'SF-TEST-PG-CONTAINER' }
        $campaign.Manifest.postgreSql=180006;$campaign.Manifest.imageDigest=($image -split '@')[1]
        $binding=Invoke-RequiredCommand 'docker' @('port',$container,'5432/tcp')
        if ($binding -cnotmatch '^127\.0\.0\.1:([0-9]{1,5})$') { throw 'SF-TEST-PG-PORT' }
        $port=[int]$Matches[1]
        $password = [IO.File]::ReadAllText((Join-Path $temporary 'password'))
        $connection=[Data.Common.DbConnectionStringBuilder]::new()
        $connection['Host']='127.0.0.1';$connection['Port']=$port;$connection['Database']='sitefactory_test_admin'
        $connection['Username']='sitefactory_test_admin';$connection['Password']=$password
        $connection['SSL Mode']='VerifyFull';$connection['Root Certificate']=[string](Join-Path $temporary 'server.crt')
        $adminFile=Resolve-PublicPostgreSqlEvidencePath $temporary 'admin-connection.txt'
        [IO.File]::WriteAllText($adminFile,$connection.ConnectionString)
        if (!$IsWindows) {[IO.File]::SetUnixFileMode($adminFile,[IO.UnixFileMode]::UserRead -bor [IO.UnixFileMode]::UserWrite)}
        $observationRun=[Guid]::NewGuid().ToString('N')
        $observationParent=Join-Path $RepositoryRoot 'artifacts/p03-f1-observability'
        $null=[IO.Directory]::CreateDirectory($observationParent)
        $observationSource=Resolve-PublicPostgreSqlEvidencePath $observationParent ($observationRun+'/observations.json')
        if ([IO.Path]::Exists([IO.Path]::GetDirectoryName($observationSource))) {throw 'SF-TEST-PG-PROVENANCE'}
        $campaign.Manifest.observationRun=$observationRun
        # Initial discovery, provenance, image/TLS/container readiness and
        # credentials are ready. Missing measurements remain absent on failure.
        $campaign.Manifest.preparationMs=[Math]::Round($preparationTimer.Elapsed.TotalMilliseconds,3)
        $preparationTimer.Stop()
        foreach ($shardName in @('g04','g05','p01','p02','p03')) {
            $shard=@($partition.shards|Where-Object name -CEQ $shardName)[0]
            $environment=@{SITEFACTORY_TEST_POSTGRES_ADMIN_FILE=$adminFile;SITEFACTORY_TEST_POSTGRES_REQUIRED='1';
                SITEFACTORY_TEST_POSTGRES_METRICS_DIRECTORY=(Resolve-PublicPostgreSqlEvidencePath $campaign.Root 'metrics');SITEFACTORY_P03_OBSERVABILITY_RUN=$observationRun}
            $null=Invoke-RequiredCommand 'dotnet' @($bridge,'--sitefactory-postgresql-harness-configuration') 30 $environment
            $query=Invoke-PublicPostgreSqlDiscoveryQuery $shard.project $shard.filter $shard.expectedTests $runner
            Write-PublicPostgreSqlJson (Resolve-PublicPostgreSqlEvidencePath $campaign.Root ('discovery/'+$shardName+'.json')) $query -CreateNew
            $invocation=Start-PublicPostgreSqlShardProof $campaign $shardName
            $invocation.Launched=$true
            $result=$null
            try {
            $result=Invoke-PublicPostgreSqlProcess $bridge $RepositoryRoot 'dotnet' @('test',(Join-Path $RepositoryRoot $shard.project),
                '--configuration','Release','--no-build','--no-restore','--filter',$shard.filter,'--logger',('trx;LogFileName='+$shardName+'.trx'),
                '--results-directory',(Resolve-PublicPostgreSqlEvidencePath $campaign.Root 'incoming')) $environment -TimeoutSeconds 360
            $receipt=@{campaignId=$campaign.Id;invocationId=$invocation.Id;finishedUtc=[DateTimeOffset]::UtcNow;exitCode=[int]$result.ExitCode;
                status=$(if ($result.Status -cin @('Completed','Timeout','Cancelled')) {$result.Status} else {'Completed'});
                processCleanup=$result.Cleanup;elapsedMs=$result.ElapsedMs}
            $proof=Invoke-PublicPostgreSqlShardArchive $campaign $invocation $receipt $bridge $RepositoryRoot $temporary @($password,$connection.ConnectionString) $observationSource
            } catch {
                $receipt=if($null -ne $result){@{status=$result.Status;exitCode=[int]$result.ExitCode;processCleanup=$result.Cleanup;elapsedMs=$result.ElapsedMs}}
                    else {@{status='Completed';exitCode=-1;processCleanup='Failed';elapsedMs=0}}
                $proof=Save-PublicPostgreSqlArchiveFailure $campaign $invocation $receipt 'SF-TEST-PG-PROCESS'
            }
            Write-Output ('SF-TEST-PG-SHARD '+$shardName+' '+$proof.status)
            if ($proof.status -cne 'PASSED') {throw $proof.code}
        }
        $exitCode=0
    }
} catch {
    $message=$_.Exception.Message
    $codes=@('SF-TEST-PG-PARTITION','SF-TEST-PG-DISCOVERY','SF-TEST-PG-PATH','SF-TEST-PG-PROVENANCE','SF-TEST-PG-PROCESS','SF-TEST-PG-TIMEOUT','SF-TEST-PG-CANCELLED',
        'SF-TEST-PG-CONFIG','SF-TEST-PG-CONTAINER','SF-TEST-PG-START','SF-TEST-PG-VERSION','SF-TEST-PG-PORT','SF-TEST-PG-SENSITIVE','SF-TEST-PG-EXIT','SF-TEST-PG-OBSERVATIONS',
        'SF-TEST-PG-ARCHIVE','SF-TEST-PG-CLEANUP','SF-TEST-PG-FINALIZATION')
    foreach ($code in $codes) {if ($message.StartsWith($code,[StringComparison]::Ordinal)) {$diagnostic=$code;break}}
    if ($null -ne $campaign) {$campaign.Manifest.code=$diagnostic}
} finally {
    if ($RunRequired -and $null -ne $campaign) {
        # Read-only residual verification BEFORE destruction, including red campaigns.
        if (!$container -and $creationAttempted) {
            try {
                $lookup=Invoke-PublicPostgreSqlProcess $bridge $RepositoryRoot 'docker' @('inspect','--format','{{.Id}}',$name) -TimeoutSeconds 30
                if ($lookup.Status -ceq 'Completed' -and $lookup.ExitCode -eq 0) {$container=$lookup.Output.Trim()}
            } catch {$cleanupCompleted=$false}
        }
        if ($container) {
            try {
                if ($container -cnotmatch '^[0-9a-f]{64}$') {throw 'SF-TEST-PG-CLEANUP'}
                $label=Invoke-RequiredCommand 'docker' @('inspect','--format','{{ index .Config.Labels "sitefactory.test-run" }}',$container) 30
                if ($label -cne $name) {throw 'SF-TEST-PG-CLEANUP'}
                try {
                    $sql="SET statement_timeout='3s'; SELECT pg_catalog.count(*) FROM pg_catalog.pg_database WHERE datname NOT IN ('postgres','template0','template1','sitefactory_test_admin'); SELECT pg_catalog.count(*) FROM pg_catalog.pg_roles WHERE rolname <> 'sitefactory_test_admin' AND NOT pg_catalog.starts_with(rolname::pg_catalog.text,'pg_'); SELECT pg_catalog.count(*) FROM pg_catalog.pg_stat_activity WHERE datname NOT IN ('postgres','template0','template1','sitefactory_test_admin') OR usename <> 'sitefactory_test_admin';"
                    $counts=Invoke-RequiredCommand 'docker' (@('exec',$container,'psql','--no-psqlrc','--username=sitefactory_test_admin','--dbname=sitefactory_test_admin','--tuples-only','--no-align','--set=ON_ERROR_STOP=1','--command')+$sql) 30
                    if ($counts -cnotmatch '^SET\r?\n0\r?\n0\r?\n0$') {throw 'SF-TEST-PG-CLEANUP'}
                    $campaign.Manifest.resourceCleanup.database='Completed'
                } catch {$cleanupCompleted=$false;$campaign.Manifest.resourceCleanup.database='Failed'}
                $null=Invoke-RequiredCommand 'docker' @('rm','--force','--volumes',$container) 30
                $absent=Invoke-PublicPostgreSqlProcess $bridge $RepositoryRoot 'docker' @('inspect','--format','{{.Id}}',$container) -TimeoutSeconds 30
                if ($absent.Status -cne 'Completed' -or $absent.ExitCode -eq 0 -or $absent.Cleanup -cne 'Completed') {throw 'SF-TEST-PG-CLEANUP'}
                $campaign.Manifest.resourceCleanup.container='Completed'
            } catch {$cleanupCompleted=$false;$campaign.Manifest.resourceCleanup.container='Failed'}
        } elseif ($creationAttempted) {$cleanupCompleted=$false;$campaign.Manifest.resourceCleanup.container='Failed'}
        else {$campaign.Manifest.resourceCleanup.container='NotCreated';$campaign.Manifest.resourceCleanup.database='NotCreated'}
        if ($null -ne $beforeContainers) {
            try {
                $after=Invoke-RequiredCommand 'docker' @('ps','--all','--quiet','--no-trunc') 30
                if (!(Assert-PublicPostgreSqlExact @($after -split '\s+'|Where-Object {$_}) @($beforeContainers -split '\s+'|Where-Object {$_})) -and $after -cne $beforeContainers) {throw 'SF-TEST-PG-CLEANUP'}
                $campaign.Manifest.resourceCleanup.preexisting='Completed'
            } catch {$cleanupCompleted=$false;$campaign.Manifest.resourceCleanup.preexisting='Failed'}
        }
        if ($temporary) {
            try {
                $clean=". '"+(Join-Path $PSScriptRoot 'PostgreSqlProof.ps1').Replace("'","''")+"'"+[Environment]::NewLine+
                    "Remove-PublicPostgreSqlOwnedDirectory '"+$temporary.Replace("'","''")+"' '"+[IO.Path]::GetDirectoryName($temporary).Replace("'","''")+"' '"+$name+"'"
                $null=Invoke-RequiredCommand 'pwsh' @('-NoProfile','-NonInteractive','-OutputFormat','Text','-EncodedCommand',
                    [Convert]::ToBase64String([Text.Encoding]::Unicode.GetBytes($clean))) 30
                if ([IO.Path]::Exists($temporary)) {throw 'SF-TEST-PG-CLEANUP'}
                $campaign.Manifest.resourceCleanup.secrets='Completed'
            } catch {$cleanupCompleted=$false;$campaign.Manifest.resourceCleanup.secrets='Failed'}
        } else {$campaign.Manifest.resourceCleanup.secrets='NotCreated'}
        foreach ($shard in $campaign.Manifest.shards) {
            if ($shard.status -ceq 'NOT_EXECUTED') {
                $launched=$campaign.Invocations.ContainsKey($shard.shard) -and $campaign.Invocations[$shard.shard].Contains('Launched') -and $campaign.Invocations[$shard.shard].Launched
                $shard.status=if($launched){'FAILED'}else{'NOT_RUN'}
            }
        }
        if($campaign.Manifest.cleanup -ceq 'Failed'){$cleanupCompleted=$false}
        if (!$cleanupCompleted) {$campaign.Manifest.code='SF-TEST-PG-CLEANUP'}
        try {
            Invoke-PublicPostgreSqlFinalization $campaign $bridge $RepositoryRoot $(if ($cleanupCompleted) {'Completed'} else {'Failed'}) $campaign.Manifest.observationsComplete
        } catch {
            $campaign.Manifest.status='FAILED';$campaign.Manifest.databaseProof=$false;$campaign.Manifest.code='SF-TEST-PG-FINALIZATION'
            # Fixed, owned fallback in this orchestrator, not the same unavailable child.
            $redPath=Resolve-PublicPostgreSqlEvidencePath $campaign.Root 'manifest.json'
            try {Write-PublicPostgreSqlJson $redPath $campaign.Manifest}
            catch {[Console]::Error.WriteLine('SF-TEST-PG-FINALIZATION Red campaign persistence failed.')}
            $exitCode=1
        }
        if ($campaign.Manifest.status -cne 'PASSED') {$exitCode=1}
    }
}
if ($exitCode -ne 0) {
    if (!$RunRequired -and $diagnostic -cne 'SF-TEST-PG-PARTITION') {
        [Console]::Error.WriteLine('SF-TEST-PG-DISCOVERY Public discovery failed or differed from the inventory.')
    } elseif (!$RunRequired) {[Console]::Error.WriteLine('SF-TEST-PG-PARTITION Public inventory or partition rejected.')}
    else {[Console]::Error.WriteLine('SF-TEST-PG-HARNESS REQUIRED campaign FAILED; redacted evidence retained.')}
} elseif ($RunRequired) {Write-Output 'SF-TEST-PG-HARNESS REQUIRED campaign PASSED: 20 Infrastructure + 3 Hosts; cleanup verified.'}
exit $exitCode
