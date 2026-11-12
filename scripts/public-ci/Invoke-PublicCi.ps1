[CmdletBinding()]
param(
    [ValidateSet('Offline','PostgreSql','Aggregate')][string]$Mode='Offline',
    [string]$RepositoryRoot=(Join-Path $PSScriptRoot '../..'),
    [string]$ProofDirectory,
    [string]$ExpectedCommit,
    [string]$RunId=$env:GITHUB_RUN_ID,
    [string]$Attempt=$env:GITHUB_RUN_ATTEMPT,
    [string]$PublicRepository,
    [string]$NeedsJson
)
Set-StrictMode -Version Latest
$ErrorActionPreference='Stop'
$PSNativeCommandUseErrorActionPreference=$false
. (Join-Path $PSScriptRoot 'PublicCiProof.ps1')
$RepositoryRoot=[IO.Path]::GetFullPath($RepositoryRoot)
if(!$ExpectedCommit){$ExpectedCommit=(Invoke-PublicCiGit $RepositoryRoot @('rev-parse','HEAD')) -join ''}
if($ExpectedCommit -cnotmatch '^[0-9a-f]{40}$' -or ($PublicRepository -and $PublicRepository -cnotmatch '^[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+$')){throw 'SF-CI-PROVENANCE'}
if(!$RunId){$RunId=[Guid]::NewGuid().ToString('N')}
if(!$Attempt){$Attempt='1'}
if(!$ProofDirectory){throw 'SF-CI-PROOF-DIRECTORY'}
$ProofDirectory=[IO.Path]::GetFullPath($ProofDirectory)
if($Mode -ceq 'Aggregate'){
    $needs=$NeedsJson|ConvertFrom-Json -AsHashtable
    if(!(Assert-PublicPostgreSqlExact @($needs.Keys) @('public','postgresql')) -or $needs.public.result -cne 'success' -or $needs.postgresql.result -cne 'success'){throw 'SF-CI-JOB'}
    $jobs=@(foreach($directory in Get-ChildItem -LiteralPath $ProofDirectory -Directory){
        $job=[IO.File]::ReadAllText((Join-Path $directory.FullName 'report.json'))|ConvertFrom-Json -AsHashtable
        $job.result=if($job.name -ceq 'PostgreSql'){$needs.postgresql.result}else{$needs.public.result}
        $job.root=$directory.FullName
        if($job.name -ceq 'PostgreSql'){
            if($job.campaign -cnotmatch '^pg-proof-[0-9a-f]{32}$'){throw 'SF-CI-PROVENANCE'}
            $job.campaign=Join-Path $directory.FullName $job.campaign
        }
        $job
    })
    Assert-PublicCiAggregate $jobs $ExpectedCommit $RunId $Attempt
    Write-Output 'SF-CI-PASSED Windows/Linux offline proofs, SourceLink and all23 real PostgreSQL Facts verified; P03-F1 remains OPEN/UNKNOWN.'
    exit 0
}
if(Test-Path -LiteralPath $ProofDirectory){throw 'SF-CI-PROVENANCE Fresh proof directory required.'}
[void][IO.Directory]::CreateDirectory($ProofDirectory)
$scratch=Join-Path ([IO.Path]::GetTempPath()) ('sitefactory-public-ci-'+[Guid]::NewGuid().ToString('N'))
$work=Join-Path $scratch 'source'
$scratchOwned=$false
$name=if($Mode -ceq 'PostgreSql'){'PostgreSql'}elseif($IsWindows){'Windows'}else{'Linux'}
$report=[ordered]@{schemaVersion=1;name=$name;commit=$ExpectedCommit;runId=$RunId;attempt=$Attempt;status='FAILED';code='SF-CI-INCOMPLETE';phase='COPY';sourceLink='PENDING';cleanup='NotObserved';suites=@();campaign=$null;timings=@()}
$phaseTimer=[Diagnostics.Stopwatch]::StartNew()
$environment=@{}
foreach($key in @('GIT_DIR','GIT_WORK_TREE','GIT_COMMON_DIR','NUGET_PACKAGES','SITEFACTORY_TEST_PUBLIC_REPOSITORY','DOTNET_CLI_USE_MSBUILD_SERVER','MSBUILDDISABLENODEREUSE')){
    $environment[$key]=[Environment]::GetEnvironmentVariable($key)
}
function Save-CiReport {
    [IO.File]::WriteAllText((Join-Path $ProofDirectory 'report.json'),($report|ConvertTo-Json -Depth 12),[Text.UTF8Encoding]::new($false))
}
function Complete-CiTiming {
    param([ValidateSet('COPY','COMPOSITION','RESTORE','GUARDS','FORMAT','BUILD','SYMBOLS','BINARIES','DISCOVERY','TEST','POSTGRESQL','AUDIT','CLEANUP')][string]$Phase=$report.phase)
    $report.timings+=@{phase=$Phase;durationMs=[Math]::Round($phaseTimer.Elapsed.TotalMilliseconds,3)}
    $phaseTimer.Restart()
}
function Set-CiPhase {
    param([ValidateSet('COPY','COMPOSITION','RESTORE','GUARDS','FORMAT','BUILD','SYMBOLS','BINARIES','DISCOVERY','TEST','POSTGRESQL','AUDIT','CLEANUP')][string]$Phase)
    Complete-CiTiming
    $report.phase=$Phase.ToUpperInvariant()
    Save-CiReport
}
function Get-CiFailureCode([Management.Automation.ErrorRecord]$Failure) {
    # Publish only exact codes owned by this proof. Never forward exception text,
    # a matching prefix with a provider suffix, or an untrusted tool message.
    $codes=@(
        'SF-CI-ARCHIVE','SF-CI-BINARY','SF-CI-BINARY-REFERENCE','SF-CI-CASE-IDENTITY',
        'SF-CI-CLEANUP','SF-CI-COMPOSITION','SF-CI-COPY','SF-CI-COUNTERS',
        'SF-CI-CYCLE','SF-CI-DISCOVERY','SF-CI-DOTNET','SF-CI-GIT','SF-CI-GUARDS',
        'SF-CI-IDENTITY','SF-CI-INCOMING','SF-CI-INCOMPLETE','SF-CI-INVENTORY',
        'SF-CI-ISOLATION','SF-CI-JOB','SF-CI-MATRIX','SF-CI-MSBUILD',
        'SF-CI-OBSERVATIONS','SF-CI-PDB','SF-CI-PDB-CHECKSUM','SF-CI-PDB-MISSING',
        'SF-CI-PDB-ORIGIN','SF-CI-PDB-PATH','SF-CI-PDB-PROOF','SF-CI-PG','SF-CI-PG-PROOF',
        'SF-CI-PRIVATE-SOURCELINK',
        'SF-CI-PROOF-DIRECTORY','SF-CI-PROVENANCE','SF-CI-REFERENCE',
        'SF-CI-REPORT','SF-CI-REPOSITORY','SF-CI-RESOURCE','SF-CI-RESULT',
        'SF-CI-REVISION','SF-CI-SCOPE','SF-CI-SHARDS','SF-CI-SOURCELINK',
        'SF-CI-SOURCELINK-RECORD','SF-CI-SOURCELINK-DUPLICATE','SF-CI-SOURCELINK-FORMAT',
        'SF-CI-SOURCELINK-EMPTY','SF-CI-SOURCELINK-REPOSITORY','SF-CI-SOURCELINK-REVISION',
        'SF-CI-SOURCELINK-UNCOVERED','SF-CI-SOURCELINK-AMBIGUOUS','SF-CI-SOURCELINK-PATH',
        'SF-CI-SUITES','SF-CI-SYMLINK','SF-CI-TRX'
    )
    if($codes -ccontains $Failure.Exception.Message){return $Failure.Exception.Message}
    return 'SF-CI-FAILED'
}
function Run-CiDotnet([string[]]$Arguments,[string]$Log,[switch]$ReturnExitCode) {
    & dotnet @Arguments *> (Join-Path $scratch $Log)
    $exitCode=$LASTEXITCODE
    if($ReturnExitCode){return $exitCode}
    if($exitCode -ne 0){throw 'SF-CI-DOTNET'}
}
function Invoke-CiOfflineSuite($Project,[string[]]$Expected) {
    $known=@(Get-PublicCiProjects | Where-Object {$_.Offline -gt 0 -and $_.Assembly -ceq $Project.Assembly -and $_.Path -ceq $Project.Path})
    if($known.Count -ne 1 -or $Expected.Count -ne $known[0].Offline){throw 'SF-CI-INVENTORY'}
    $started=[DateTimeOffset]::UtcNow
    $raw=Join-Path $scratch ($Project.Assembly+'.trx');$exitCode=$null
    try{
        # Keep the real process result before any TRX validation can throw.
        $exitCode=Run-CiDotnet @('test',$Project.Path,'--configuration','Release','--no-build','--no-restore','--filter','Category!=PostgreSql','--logger',('trx;LogFileName='+$Project.Assembly+'.trx'),'--results-directory',$scratch) ($Project.Assembly+'.log') -ReturnExitCode
        $finished=[DateTimeOffset]::UtcNow
        if($exitCode -ne 0){throw 'SF-CI-DOTNET'}
        $validated=Assert-PublicCiOfflineTrx ([IO.File]::ReadAllBytes($raw)) $Expected $Project.Assembly $started $finished
        $safe=Join-Path $ProofDirectory ('offline/'+$Project.Assembly+'.trx')
        Save-PublicCiOfflineTrx $validated $safe $Project.Assembly
        $inventory=@($validated.Rows|ForEach-Object {Get-PublicCiArchivedCaseName $_}|Sort-Object -CaseSensitive)
        $report.suites+=@{assembly=$Project.Assembly;passed=$validated.Count;inventory=$inventory;start=$started.ToString('O');finish=$finished.ToString('O');sha256=(Get-FileHash -LiteralPath $safe).Hash}
        Save-CiReport
    }catch{
        $executionFailure=$_
        $report.status='FAILED';$report.code=Get-CiFailureCode $executionFailure
        try{$report['failure']=Get-PublicCiOfflineFailureDiagnostic $raw $Expected $Project.Assembly $exitCode}catch{
            $report['failure']=@{kind='OfflineFailure';suite=$known[0].Assembly;expectedCount=$known[0].Offline;status='FAILED';completeProof=$false;diagnostic='Unavailable';processExitCode=$exitCode}
        }
        # A diagnostic persistence failure must neither hide the original
        # failure nor prevent the outer finally from cleaning owned resources.
        try{Save-CiReport}catch{}
        throw $executionFailure
    }
}
function Set-CiGitProvenance {
    # P04 provenance only, AFTER build (--no-build). The SDK discovers public
    # metadata in the scratch itself; private builds never see checkout Git.
    $env:GIT_DIR=(Invoke-PublicCiGit $RepositoryRoot @('rev-parse','--absolute-git-dir')) -join ''
    $env:GIT_WORK_TREE=$work
    Remove-Item Env:GIT_COMMON_DIR -ErrorAction SilentlyContinue
}
try{
    Set-CiPhase 'COPY'
    if(Test-Path -LiteralPath $scratch){throw 'SF-CI-PROVENANCE'}
    [void][IO.Directory]::CreateDirectory($scratch)
    $scratchOwned=$true
    [void][IO.Directory]::CreateDirectory($work)
    foreach($key in @('GIT_DIR','GIT_WORK_TREE','GIT_COMMON_DIR')){Remove-Item -LiteralPath ('Env:\'+$key) -ErrorAction SilentlyContinue}
    foreach($key in @(Get-ChildItem Env:|Where-Object Name -like 'SITEFACTORY_TEST_POSTGRES_*')){Remove-Item -LiteralPath ('Env:\'+$key.Name)}
    $parent=[IO.DirectoryInfo]::new($work)
    while($null -ne $parent){if((Test-Path (Join-Path $parent.FullName '.git')) -or ($parent.Attributes -band [IO.FileAttributes]::ReparsePoint)){throw 'SF-CI-ISOLATION'};$parent=$parent.Parent}
    foreach($path in @(Get-PublicCiSourcePaths $RepositoryRoot)){
        $source=Join-Path $RepositoryRoot $path
        $cursor=[IO.FileInfo]::new($source)
        while($null -ne $cursor -and $cursor.FullName -cne $RepositoryRoot){
            if($cursor.Attributes -band [IO.FileAttributes]::ReparsePoint){throw 'SF-CI-SYMLINK'}
            $cursor=if($cursor -is [IO.FileInfo]){$cursor.Directory}else{$cursor.Parent}
        }
        $target=Join-Path $work $path;[void][IO.Directory]::CreateDirectory((Split-Path $target))
        [IO.File]::Copy($source,$target,$false)
        if((Get-FileHash -LiteralPath $source).Hash -cne (Get-FileHash -LiteralPath $target).Hash){throw 'SF-CI-COPY'}
    }
    Set-CiPhase 'COMPOSITION'
    Assert-PublicCiComposition $work
    $env:NUGET_PACKAGES=Join-Path $scratch 'packages'
    $env:SITEFACTORY_TEST_PUBLIC_REPOSITORY=$work
    $env:DOTNET_CLI_USE_MSBUILD_SERVER='0';$env:MSBUILDDISABLENODEREUSE='1'
    if($PublicRepository){Initialize-PublicCiGitMetadata $RepositoryRoot $work $PublicRepository $ExpectedCommit}
    Push-Location $work
    try{
        Set-CiPhase 'RESTORE'
        Run-CiDotnet @('restore','SiteFactory.slnx','--locked-mode','-nr:false') 'restore.log'
        Set-CiPhase 'GUARDS'
        $yaml=@(Get-ChildItem (Join-Path $env:NUGET_PACKAGES 'yamldotnet') -Recurse -Filter 'YamlDotNet.dll'|Where-Object FullName -match '[\\/]net8\.0[\\/]')[0].FullName
        & pwsh -NoProfile -File (Join-Path $work 'tests/PublicCi/Test-PublicCi.ps1') -RepositoryRoot $work -YamlAssembly $yaml *> (Join-Path $scratch 'guards.log')
        if($LASTEXITCODE -ne 0){throw 'SF-CI-GUARDS'}
        Set-CiPhase 'FORMAT'
        Run-CiDotnet @('format','SiteFactory.slnx','--verify-no-changes','--no-restore') 'format.log'
        Set-CiPhase 'BUILD'
        Run-CiDotnet @('build','SiteFactory.slnx','--configuration','Release','--no-restore','--disable-build-servers','-p:UseSharedCompilation=false') 'build.log'
        Set-CiPhase 'COMPOSITION'
        Assert-PublicCiComposition $work -Evaluate
        Set-CiPhase 'SYMBOLS'
        # The compiled guard invokes the same original-root symbol and binary
        # validators, then all adversarial checks. Consume its bounded result
        # instead of repeating both original-root scans around that process.
        $symbolProof=Join-Path $scratch 'symbols.json'
        & pwsh -NoProfile -File (Join-Path $work 'tests/PublicCi/Test-PublicCi.ps1') -RepositoryRoot $work -CompiledRoot $work -Commit $ExpectedCommit -PublicRepository $PublicRepository -SymbolProofFile $symbolProof *> (Join-Path $scratch 'symbols-guard.log')
        if($LASTEXITCODE -ne 0){throw 'SF-CI-PDB-PROOF'}
        $symbols=[IO.File]::ReadAllText($symbolProof)|ConvertFrom-Json
        if($symbols.pdbCount -lt 23 -or $symbols.sourceLink -cne $(if($PublicRepository){'VERIFIED'}else{'PENDING'}) -or $symbols.binariesVerified -cne $true){throw 'SF-CI-PDB-PROOF'}
        $report.sourceLink=$symbols.sourceLink
        Set-CiPhase 'BINARIES'
        $report.symbolValidationMs=$symbols.symbolValidationMs
        $report.binaryValidationMs=$symbols.binaryValidationMs
        Set-CiPhase 'DISCOVERY'
        $bridge=Join-Path $work 'tests/LogicGate.SiteFactory.Infrastructure.PostgreSql.Tests/bin/Release/net10.0/LogicGate.SiteFactory.Infrastructure.PostgreSql.Tests.dll'
        $partition=Read-PublicPostgreSqlPartition (Join-Path $work 'tests/PostgreSql/Public/shards.json')
        if($Mode -ceq 'Offline'){
            foreach($project in @(Get-PublicCiProjects|Where-Object Offline -gt 0)){
                Set-CiPhase 'DISCOVERY'
                $discovery=Invoke-PublicPostgreSqlProcess $bridge $work 'dotnet' @('test',$project.Path,'--configuration','Release','--no-build','--no-restore','--list-tests') -TimeoutSeconds 180
                Assert-PublicPostgreSqlProcess $discovery
                $all=@($discovery.Output -split '\r?\n'|Where-Object {$_ -cmatch '^    LogicGate\.'}|ForEach-Object {$_.Trim()})
                $excluded=@($partition.expectedTests|Where-Object assembly -ceq $project.Assembly|ForEach-Object fullyQualifiedName)
                if(@($excluded|Where-Object {$all -cnotcontains $_}).Count){throw 'SF-CI-DISCOVERY'}
                $expected=@($all|Where-Object {$excluded -cnotcontains $_})
                if($expected.Count -ne $project.Offline){throw 'SF-CI-DISCOVERY'}
                Set-CiPhase 'TEST'
                # R01 execution, bounded by the offline CI job; the P04 360s limit is for DB shards only.
                Invoke-CiOfflineSuite $project $expected
            }
            Set-CiPhase 'AUDIT'
            Run-CiDotnet @('list','SiteFactory.slnx','package','--vulnerable','--include-transitive','--no-restore') 'audit.log'
        }else{
            Set-CiPhase 'POSTGRESQL'
            Set-CiGitProvenance
            $required=Join-Path $scratch 'required'
            $env:SITEFACTORY_TEST_PUBLIC_REPOSITORY=$work
            & pwsh -NoProfile -File (Join-Path $work 'tests/PostgreSql/Public/Invoke-PostgreSqlTests.ps1') -RepositoryRoot $work -RunRequired -ProofDirectory $required *> (Join-Path $scratch 'required.log')
            $exitCode=$LASTEXITCODE
            $campaigns=@(Get-ChildItem -LiteralPath $required -Directory)
            if($campaigns.Count -ne 1){throw 'SF-CI-PG'}
            $campaign=$campaigns[0]
            # Stage only P04's controlled evidence. Never upload raw logs/incoming/secrets.
            $report.campaign=$campaign.Name
            $target=Join-Path $ProofDirectory $campaign.Name
            [void][IO.Directory]::CreateDirectory($target)
            $controlled=@('manifest.json','observations/p03.json','discovery/inventory.json')+@($partition.shards|ForEach-Object {'proof/'+$_.name+'.json';'discovery/'+$_.name+'.json'})
            foreach($relative in $controlled){
                $source=Join-Path $campaign.FullName $relative
                if(Test-Path -LiteralPath $source){$destination=Join-Path $target $relative;[void][IO.Directory]::CreateDirectory((Split-Path $destination));Copy-Item -LiteralPath $source -Destination $destination}
            }
            $measurements=@(Read-PublicPostgreSqlMeasurements @{Root=$campaign.FullName;Partition=$partition})
            [void][IO.Directory]::CreateDirectory((Join-Path $target 'metrics'))
            [IO.File]::WriteAllText((Join-Path $target 'metrics/summary.json'),(ConvertTo-Json -InputObject $measurements -Depth 6))
            $incoming=Join-Path $campaign.FullName 'incoming'
            if(Test-Path $incoming){if(@(Get-ChildItem $incoming -Force).Count){throw 'SF-CI-INCOMING'}}
            [void][IO.Directory]::CreateDirectory((Join-Path $target 'incoming'))
            [void][IO.Directory]::CreateDirectory((Join-Path $target 'native'))
            foreach($shard in $partition.shards){
                $native=Join-Path $campaign.FullName ('native/'+$shard.name+'.trx')
                if(!(Test-Path -LiteralPath $native)){continue}
                $document=Read-PublicPostgreSqlTrxBytes ([IO.File]::ReadAllBytes($native))
                Assert-PublicPostgreSqlNoSensitiveContent $document.OuterXml -Document $document
                Copy-Item -LiteralPath $native -Destination (Join-Path $target 'native')
            }
            if($exitCode -ne 0){throw 'SF-CI-PG'}
            [void](Assert-PublicCiPostgreSql $target $ExpectedCommit)
            $report.campaignFiles=@(Get-ChildItem -LiteralPath $target -Recurse -File|Sort-Object FullName|ForEach-Object {@{path=[IO.Path]::GetRelativePath($target,$_.FullName).Replace('\','/');sha256=(Get-FileHash -LiteralPath $_.FullName).Hash}})
        }
        $report.status='PASSED';$report.code=$null
    }finally{Pop-Location}
}catch{
    $report.status='FAILED';$report.code=Get-CiFailureCode $_
    [Console]::Error.WriteLine(($report.code+' phase='+$report.phase+'; controlled evidence retained.'))
}finally{
    $workPassed=$report.status -ceq 'PASSED'
    $reportingFailed=$false
    try{Complete-CiTiming}catch{
        $reportingFailed=$true
        if($workPassed){$report.status='FAILED';$report.code='SF-CI-REPORT'}
    }
    # A diagnostic write failure must not prevent owned-resource cleanup.
    if($workPassed){
        $report.status='FAILED';$report.code='SF-CI-INCOMPLETE'
        try{Set-CiPhase 'CLEANUP'}catch{$reportingFailed=$true;$report.code='SF-CI-REPORT'}
    }
    $cleanupFailed=$false
    try{
        foreach($key in $environment.Keys){
            if($null -eq $environment[$key]){Remove-Item -LiteralPath ('Env:\'+$key) -ErrorAction SilentlyContinue}else{[Environment]::SetEnvironmentVariable($key,$environment[$key])}
        }
    }catch{$cleanupFailed=$true}
    try{
        if($scratchOwned){
            Remove-PublicCiOwnedDirectory $scratch
            if(Test-Path -LiteralPath $scratch){throw 'SF-CI-CLEANUP'}
        }
    }catch{$cleanupFailed=$true}
    if($cleanupFailed){$report.cleanup='Failed';$report.status='FAILED';$report.code='SF-CI-CLEANUP'}else{
        $report.cleanup='Completed'
        if($workPassed -and !$reportingFailed){$report.status='PASSED';$report.code=$null}
    }
    try{Complete-CiTiming 'CLEANUP';$phaseTimer.Stop()}catch{
        if($workPassed -and !$cleanupFailed){$report.status='FAILED';$report.code='SF-CI-REPORT'}
    }
    try{Save-CiReport}catch{
        [Console]::Error.WriteLine(('SF-CI-REPORT phase='+$report.phase+'; proof could not be saved.'))
        exit 1
    }
}
if($report.status -cne 'PASSED'){exit 1}
Write-Output ('SF-CI-PASSED '+$name+'; controlled evidence ready, SourceLink '+$report.sourceLink+'.')
exit 0

