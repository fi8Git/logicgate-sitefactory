[CmdletBinding()]
param([string]$RepositoryRoot=(Join-Path $PSScriptRoot '../..'),[string]$YamlAssembly,
    [string]$CompiledRoot,[string]$Commit,[string]$PublicRepository)
Set-StrictMode -Version Latest
$ErrorActionPreference='Stop'
$RepositoryRoot=[IO.Path]::GetFullPath($RepositoryRoot)
. (Join-Path $RepositoryRoot 'scripts/public-ci/PublicCiProof.ps1')
$script:checks=0
function Check([bool]$Condition,[string]$Name){if(!$Condition){throw ('SF-CI-TEST '+$Name)};$script:checks++}
function Reject([scriptblock]$Action,[string]$Name,[string]$Code){
    $rejected=$false;try{& $Action|Out-Null}catch{$rejected=(!$Code -or $_.Exception.Message -ceq $Code)}
    Check $rejected $Name
}
if($CompiledRoot){
    # This mode consumes a real build only. Never call Offline, restore, build or
    # the YAML guard here: the outer harness invokes it after its own build.
    $symbolRoot=$null;$symbolFailure=$false
    try{
        if($Commit -cnotmatch '^[0-9a-f]{40}$'){throw 'SF-CI-PROVENANCE'}
        $CompiledRoot=[IO.Path]::GetFullPath($CompiledRoot)
        $symbolProof=Assert-PublicCiSymbols $CompiledRoot $PublicRepository $Commit
        Check ($symbolProof.pdbCount -ge 23) 'real compiled symbols accepted'
        Assert-PublicCiBinaries $CompiledRoot $PublicRepository $Commit
        Check $true 'real compiled binaries accepted'
        $symbolRoot=Join-Path ([IO.Path]::GetTempPath()) ('sitefactory-ci-tests-'+[Guid]::NewGuid().ToString('N'))
        if(Test-Path -LiteralPath $symbolRoot){throw 'SF-CI-PROVENANCE'}
        [void][IO.Directory]::CreateDirectory($symbolRoot)
        $files=@(Get-PublicCiSourcePaths $CompiledRoot)+@(
            Get-ChildItem (Join-Path $CompiledRoot 'src'),(Join-Path $CompiledRoot 'tests') -Recurse -File -Filter '*.pdb'|
                Where-Object FullName -match '[\\/](bin|obj)[\\/]Release[\\/]'|
                ForEach-Object {[IO.Path]::GetRelativePath($CompiledRoot,$_.FullName).Replace('\','/')}
        )+@((Get-PublicCiProjects).Path|ForEach-Object {($_ -replace '[^/]+$','')+'obj/project.assets.json'})
        foreach($relative in $files){
            $source=Join-Path $CompiledRoot $relative
            if(![IO.File]::Exists($source)){throw 'SF-CI-PDB-PROOF'}
            $target=Join-Path $symbolRoot $relative
            [void][IO.Directory]::CreateDirectory((Split-Path $target));[IO.File]::Copy($source,$target,$false)
        }
        $copied=Assert-PublicCiSymbols $symbolRoot $PublicRepository $Commit
        Check ($copied.pdbCount -eq $symbolProof.pdbCount) 'real PDB copy retains all documents and provenance'
        $domain=Join-Path $symbolRoot 'src/LogicGate.SiteFactory.Domain/bin/Release/net10.0/LogicGate.SiteFactory.Domain.pdb'
        $domainBytes=[IO.File]::ReadAllBytes($domain)
        $duplicate=Join-Path (Split-Path $domain) 'duplicate/LogicGate.SiteFactory.Domain.pdb'
        [void][IO.Directory]::CreateDirectory((Split-Path $duplicate));[IO.File]::WriteAllBytes($duplicate,$domainBytes)
        [IO.File]::Delete($domain)
        Reject {Assert-PublicCiSymbols $symbolRoot $PublicRepository $Commit} 'missing canonical PDB cannot be replaced by another copy' 'SF-CI-PDB-MISSING'
        [IO.File]::WriteAllBytes($domain,$domainBytes);[IO.File]::Delete($duplicate)
        $unknown=Join-Path (Split-Path $domain) 'unapproved.pdb';[IO.File]::WriteAllBytes($unknown,$domainBytes)
        Reject {Assert-PublicCiSymbols $symbolRoot $PublicRepository $Commit} 'unexpected real PDB provenance rejected' 'SF-CI-PDB-ORIGIN'
        [IO.File]::Delete($unknown)
        $sourcePath=Join-Path $symbolRoot 'src/LogicGate.SiteFactory.Domain/AssemblyMarker.cs'
        $sourceBytes=[IO.File]::ReadAllBytes($sourcePath);[IO.File]::AppendAllText($sourcePath,[Environment]::NewLine)
        Reject {Assert-PublicCiSymbols $symbolRoot $PublicRepository $Commit} 'changed original source fails PDB checksum' 'SF-CI-PDB-CHECKSUM'
        [IO.File]::WriteAllBytes($sourcePath,$sourceBytes)
        Reject {Assert-PublicCiSymbols $symbolRoot 'unapproved/expected-public' $Commit} 'missing or mismatched SourceLink rejected' 'SF-CI-SOURCELINK'
        [IO.File]::WriteAllText($domain,'invalid portable PDB')
        Reject {Assert-PublicCiSymbols $symbolRoot $PublicRepository $Commit} 'corrupt real PDB rejected'
        [IO.File]::WriteAllBytes($domain,$domainBytes)

        # Change only the checksum blob in a copied real PDB. The original
        # package/archive/cache and embedded source remain entirely untouched.
        $testPdb=Join-Path $symbolRoot 'tests/LogicGate.SiteFactory.Domain.Tests/bin/Release/net10.0/LogicGate.SiteFactory.Domain.Tests.pdb'
        $testBytes=[IO.File]::ReadAllBytes($testPdb);$stream=[IO.MemoryStream]::new($testBytes,$false)
        try{
            $provider=[Reflection.Metadata.MetadataReaderProvider]::FromPortablePdbStream($stream)
            try{
                $reader=$provider.GetMetadataReader();$hashes=@(foreach($handle in $reader.Documents){
                    $document=$reader.GetDocument($handle)
                    if($reader.GetString($document.Name) -cmatch '^/_/xunit\.v3\.core\.mtp-v1/[0-9.]+/_content/DefaultRunnerReporters\.cs$'){
                        [Convert]::ToHexString($reader.GetBlobBytes($document.Hash))
                    }
                })
            }finally{$provider.Dispose()}
        }finally{$stream.Dispose()}
        Check ($hashes.Count -eq 1 -and $hashes[0].Length -eq 64) 'real locked xUnit source document found'
        $hex=[Convert]::ToHexString($testBytes);$offset=$hex.IndexOf($hashes[0],[StringComparison]::Ordinal)
        Check ($offset -ge 0 -and $offset%2 -eq 0 -and $hex.LastIndexOf($hashes[0],[StringComparison]::Ordinal) -eq $offset) 'checksum blob location is unambiguous'
        $mutated=[byte[]]$testBytes.Clone();$mutated[$offset/2]=$mutated[$offset/2] -bxor 1
        [IO.File]::WriteAllBytes($testPdb,$mutated)
        Reject {Assert-PublicCiSymbols $symbolRoot $PublicRepository $Commit} 'changed package-source document checksum rejected' 'SF-CI-PDB-CHECKSUM'
        [IO.File]::WriteAllBytes($testPdb,$testBytes)
    }catch{$symbolFailure=$true}finally{
        if($symbolRoot -and [IO.Directory]::Exists($symbolRoot)){
            try{Remove-PublicCiOwnedDirectory $symbolRoot}catch{$symbolFailure=$true}
        }
    }
    if($symbolFailure){[Console]::Error.WriteLine('SF-CI-PDB-PROOF');exit 1}
    Write-Output ('Public CI compiled checks: '+$script:checks+' Passed; real PDB/binary proof, no rebuild.')
    return
}
if(!$YamlAssembly){
    [xml]$packages=Get-Content (Join-Path $RepositoryRoot 'Directory.Packages.props') -Raw
    $version=@($packages.SelectNodes("//PackageVersion[@Include='YamlDotNet']"))[0].GetAttribute('Version')
    $profile=if($env:USERPROFILE){$env:USERPROFILE}else{[Environment]::GetFolderPath('UserProfile')}
    $cache=if($env:NUGET_PACKAGES){$env:NUGET_PACKAGES}else{Join-Path $profile '.nuget/packages'}
    $YamlAssembly=Join-Path $cache ("yamldotnet/$version/lib/net8.0/YamlDotNet.dll")
}
Add-Type -Path $YamlAssembly
function Node($Map,[string]$Key){
    $matches=@($Map.Children.GetEnumerator()|Where-Object {$_.Key.Value -ceq $Key})
    if($matches.Count -ne 1){throw 'SF-CI-YAML'}
    return ,$matches[0].Value
}
function Assert-Workflow([string]$Text){
    $stream=[YamlDotNet.RepresentationModel.YamlStream]::new();$stream.Load([IO.StringReader]::new($Text))
    if($stream.Documents.Count -ne 1){throw 'SF-CI-YAML'}
    $yaml=$stream.Documents[0].RootNode
    if((Node (Node $yaml 'permissions') 'contents').Value -cne 'read' -or (Node $yaml 'permissions').Children.Count -ne 1){throw 'SF-CI-PERMISSIONS'}
    if($Text -match 'pull_request_target|continue-on-error|secrets\.|packages: write|uses:\s+\./'){throw 'SF-CI-SECURITY'}
    foreach($event in @('pull_request','push','workflow_dispatch')){[void](Node (Node $yaml 'on') $event)}
    $jobs=Node $yaml 'jobs'
    if(!(Assert-PublicPostgreSqlExact @($jobs.Children.Keys|ForEach-Object Value) @('scope','public','postgresql','proof'))){throw 'SF-CI-JOBS'}
    $offline=Node $jobs 'public';$pg=Node $jobs 'postgresql';$proof=Node $jobs 'proof'
    $matrix=Node (Node (Node $offline 'strategy') 'matrix') 'include'
    $pairs=@($matrix.Children|ForEach-Object {((Node $_ 'platform').Value)+'/'+((Node $_ 'os').Value)})
    if(!(Assert-PublicPostgreSqlExact $pairs @('Windows/windows-latest','Linux/ubuntu-latest'))){throw 'SF-CI-MATRIX'}
    foreach($job in @($offline,$pg)){if((Node $job 'if').Value -cne "needs.scope.outputs.run == 'true'"){throw 'SF-CI-ROUTE'}}
    if((Node $pg 'runs-on').Value -cne 'ubuntu-latest' -or (Node $proof 'if').Value -cne "always() && needs.scope.outputs.run == 'true'"){throw 'SF-CI-AGGREGATE'}
    if(!(Assert-PublicPostgreSqlExact @((Node $proof 'needs').Children|ForEach-Object Value) @('scope','public','postgresql'))){throw 'SF-CI-NEEDS'}
    $expected=@(Get-PublicCiSourcePaths $RepositoryRoot)
    $roots=@((Get-PublicCiProjects).Path|ForEach-Object {'/'+$_.Substring(0,$_.LastIndexOf('/')+1)})+@('/scripts/public-ci/','/tests/PostgreSql/Public/','/tests/fixtures/public/','/templates/documentation-minimal/')
    foreach($job in @($offline,$pg)){
        $checkout=@((Node $job 'steps').Children|Where-Object {try{(Node $_ 'uses').Value -like 'actions/checkout@*'}catch{$false}})
        if($checkout.Count -ne 1){throw 'SF-CI-CHECKOUT'}
        $with=Node $checkout[0] 'with'
        if((Node $with 'persist-credentials').Value -cne 'false' -or (Node $with 'filter').Value -cne 'blob:none' -or (Node $with 'sparse-checkout-cone-mode').Value -cne 'false'){throw 'SF-CI-CHECKOUT'}
        $patterns=@((Node $with 'sparse-checkout').Value -split '\r?\n'|Where-Object {$_})
        if(@($patterns|Where-Object {$_ -ceq '/SiteFactory.slnx'}).Count -ne 1){throw 'SF-CI-CHECKOUT'}
        foreach($pattern in $patterns){
            if(!$pattern.StartsWith('!') -and $roots -cnotcontains $pattern -and $expected -cnotcontains $pattern.TrimStart('/')){throw 'SF-CI-CHECKOUT'}
        }
        foreach($source in $expected){
            $include=$false
            foreach($pattern in $patterns){$negative=$pattern.StartsWith('!');$path=$pattern.TrimStart('!').TrimStart('/');if(($path.EndsWith('/') -and $source.StartsWith($path,[StringComparison]::Ordinal)) -or $source -ceq $path){$include=!$negative}}
            if(!$include){throw 'SF-CI-CHECKOUT'}
        }
        foreach($project in @(Get-PublicCiProjects)){
            [xml]$xml=Get-Content (Join-Path $RepositoryRoot $project.Path) -Raw
            $directory=$project.Path.Substring(0,$project.Path.LastIndexOf('/')+1)
            foreach($removed in $xml.SelectNodes('//Compile[@Remove]')){
                if($patterns -cnotcontains ('!/'+$directory+$removed.GetAttribute('Remove').Replace('\','/'))){throw 'SF-CI-CHECKOUT'}
            }
            if(@($xml.SelectNodes('//EnableDefaultCompileItems')|Where-Object InnerText -ceq 'false').Count -and $patterns -ccontains ('/'+$directory)){throw 'SF-CI-CHECKOUT'}
        }
    }
    foreach($step in @($jobs.Children.Values|ForEach-Object {(Node $_ 'steps').Children})){
        $use=try{(Node $step 'uses').Value}catch{''}
        if($use -and $use -cnotmatch '^actions/(checkout|setup-dotnet|upload-artifact|download-artifact)@[0-9a-f]{40}$'){throw 'SF-CI-PIN'}
    }
}
$workflow=Get-Content (Join-Path $RepositoryRoot '.github/workflows/ci.yml') -Raw
Assert-Workflow $workflow;Check $true 'public workflow matrix, scope and permissions'
foreach($case in @(
    @{Old='platform: Windows';New='platform: Missing'},
    @{Old='platform: Linux';New='platform: Windows'},
    @{Old='[scope, public, postgresql]';New='[scope, public]'},
    @{Old='contents: read';New='contents: write'},
    @{Old='sparse-checkout-cone-mode: false';New='sparse-checkout-cone-mode: true'},
    @{Old='/SiteFactory.slnx';New='/Unapproved.slnx'},
    @{Old='/SiteFactory.slnx';New=''},
    @{Old='/SiteFactory.slnx';New=('/SiteFactory.slnx'+[Environment]::NewLine+'            /SiteFactory.slnx')},
    @{Old='/tests/PublicCi/Test-PublicCi.ps1';New='/tests/Unapproved/Test.ps1'},
    @{Old='actions/checkout@3d3c42e5aac5ba805825da76410c181273ba90b1';New='actions/checkout@main'},
    @{Old="always() && needs.scope.outputs.run == 'true'";New="needs.scope.outputs.run == 'true'"}
)){Reject {Assert-Workflow $workflow.Replace($case.Old,$case.New)} ('workflow rejects '+$case.New)}
Reject {Assert-Workflow 'jobs: [ unclosed'} 'corrupt YAML'
Check (!(Get-PublicCiRoute @('src/Unapproved/Program.cs') 'push' $true)) 'mixed automatic suites not duplicated'
Check (Get-PublicCiRoute @('src/Unapproved/Program.cs') 'workflow_dispatch' $true) 'mixed explicit proof'
Reject {Get-PublicCiRoute @('src/Unapproved/Program.cs') 'pull_request' $false} 'unknown source project cannot disable public CI'
Reject {Get-PublicCiRoute @('.github/workflows/extra.yml') 'push'} 'unknown workflow rejected'
Check (Get-PublicCiRoute @('src/LogicGate.SiteFactory.Domain/AssemblyMarker.cs') 'push') 'public automatic proof'
$projects=@(Get-PublicCiProjects)
Check ($projects.Count -eq 23 -and @($projects|Where-Object Offline -gt 0).Count -eq 11 -and ($projects|Measure-Object Offline -Sum).Sum -eq 2726) 'closed project inventory'
$partition=Read-PublicPostgreSqlPartition (Join-Path $RepositoryRoot 'tests/PostgreSql/Public/shards.json')
Check ($partition.expectedTests.Count -eq 23 -and $partition.shards.Count -eq 5 -and ($partition.shards.expectedCount -join ',') -ceq '5,3,8,4,3') 'unchanged P04 partition'
$sources=@(Get-PublicCiSourcePaths $RepositoryRoot)
Check ($sources.Count -eq 383 -and @($sources|Where-Object {$_ -clike '*.slnx'}).Count -eq 1 -and $sources -ccontains 'SiteFactory.slnx') 'exact public solution is the only added fixed source'
[xml]$publicSolution=Get-Content (Join-Path $RepositoryRoot 'SiteFactory.slnx') -Raw
$solutionProjects=@($publicSolution.SelectNodes('//Project')|ForEach-Object {$_.GetAttribute('Path')})
Check (Assert-PublicPostgreSqlExact $solutionProjects @($projects.Path)) 'checked-out public solution contains exactly the closed23 projects'
foreach($script in @('scripts/public-ci/Invoke-PublicCi.ps1','scripts/public-ci/PublicCiProof.ps1','tests/PublicCi/Test-PublicCi.ps1')){Check ($sources -ccontains $script) 'required public tool'}
Check (@($sources|Where-Object {$_ -like 'tests/PublicExtraction/*'}).Count -eq 0) 'no extraction-policy test imported'
$temporary=Join-Path ([IO.Path]::GetTempPath()) ('sitefactory-ci-tests-'+[Guid]::NewGuid().ToString('N'))
if(Test-Path -LiteralPath $temporary){throw 'SF-CI-PROVENANCE'}
[void][IO.Directory]::CreateDirectory($temporary)
$start=[DateTimeOffset]::Parse('2026-01-01T00:00:00Z');$finish=$start.AddSeconds(1);$commit='a'*40
function Write-Json($Value,[string]$Path){[void][IO.Directory]::CreateDirectory((Split-Path $Path));[IO.File]::WriteAllText($Path,($Value|ConvertTo-Json -Depth 16))}
function Rows([string]$Assembly,[int]$Count,[string[]]$Names=@()){
    @(for($i=0;$i -lt $Count;$i++){$name=if($Names.Count){$Names[$i]}else{$Assembly+'.Fixture.Case'+$i};$qualified=$name.Split('(')[0];$dot=$qualified.LastIndexOf('.');@{id=[Guid]::NewGuid().ToString('D');execution=[Guid]::NewGuid().ToString('D');caseHash=(Get-PublicCiCaseHash $name);class=$qualified.Substring(0,$dot);method=$qualified.Substring($dot+1);start=$start.ToString('O');end=$finish.ToString('O');duration='00:00:01';outcome='Passed'}})
}
try{
    # Exercise the real report writer, closed phase setter, catch and finally without
    # running dotnet, providers or a campaign. Only the failing work is synthetic.
    $scriptPath=Join-Path $RepositoryRoot 'scripts/public-ci/Invoke-PublicCi.ps1'
    $parseTokens=$null;$parseErrors=$null
    $scriptAst=[Management.Automation.Language.Parser]::ParseFile($scriptPath,[ref]$parseTokens,[ref]$parseErrors)
    Check ($parseErrors.Count -eq 0) 'diagnostic script syntax'
    $execution=@($scriptAst.EndBlock.Statements|Where-Object {$_ -is [Management.Automation.Language.TryStatementAst]})
    Check ($execution.Count -eq 1 -and $execution[0].CatchClauses.Count -eq 1) 'single controlled failure boundary'
    $functions=@($scriptAst.FindAll({param($node) $node -is [Management.Automation.Language.FunctionDefinitionAst]},$true))
    $failureCode=@($functions|Where-Object Name -ceq 'Get-CiFailureCode')
    Check ($failureCode.Count -eq 1) 'one closed failure-code classifier'
    . ([scriptblock]::Create($failureCode[0].Extent.Text))
    foreach($code in @('SF-CI-PDB-PATH','SF-CI-PDB-CHECKSUM','SF-CI-PDB-MISSING','SF-CI-PDB-ORIGIN','SF-CI-PDB-PROOF','SF-CI-CASE-IDENTITY','SF-CI-TRX')){
        try{throw $code}catch{$classified=Get-CiFailureCode $_}
        Check ($classified -ceq $code) 'exact owned failure code retained'
    }
    foreach($message in @('untrusted failure','SF-CI-PDB-PATH untrusted provider details','sf-ci-pdb-path','SF-CI-UNAPPROVED')){
        try{throw $message}catch{$classified=Get-CiFailureCode $_}
        Check ($classified -ceq 'SF-CI-FAILED') 'unknown or augmented failure text stays undisclosed'
    }
    $reportAssignment=@($scriptAst.EndBlock.Statements|Where-Object {
        $_ -is [Management.Automation.Language.AssignmentStatementAst] -and $_.Left.Extent.Text -ceq '$report'
    })[0]
    $phaseFunction=@($functions|Where-Object Name -ceq 'Set-CiPhase')[0]
    $phases=@('COPY','COMPOSITION','RESTORE','GUARDS','FORMAT','BUILD','SYMBOLS','BINARIES','DISCOVERY','TEST','POSTGRESQL','AUDIT','CLEANUP')
    $allowed=@($phaseFunction.FindAll({param($node)
        $node -is [Management.Automation.Language.AttributeAst] -and $node.TypeName.Name -ceq 'ValidateSet'
    },$true)[0].PositionalArguments|ForEach-Object Value)
    Check (Assert-PublicPostgreSqlExact $allowed $phases) 'phase diagnostic has a closed allowlist'
    $calls=@($execution[0].FindAll({param($node)
        $node -is [Management.Automation.Language.CommandAst] -and $node.GetCommandName() -ceq 'Set-CiPhase'
    },$true)|ForEach-Object {$_.CommandElements[1].Value}|Sort-Object -Unique)
    Check (Assert-PublicPostgreSqlExact $calls $phases) 'every real execution phase is marked'
    $exercise=[scriptblock]::Create('try { Set-CiPhase $RequestedPhase; if($WorkFails){throw $privateMessage}; $report.status=''PASSED'';$report.code=$null } '+
        $execution[0].CatchClauses[0].Extent.Text+' finally '+$execution[0].Finally.Extent.Text)
    $realCleanup=${function:Remove-PublicCiOwnedDirectory}
    function Test-PhaseReport([string]$RequestedPhase,[string]$name,[bool]$WorkFails=$true,[bool]$CleanupFails=$false,[bool]$ReportFails=$false){
        $ExpectedCommit=$commit;$RunId='diagnostic';$Attempt='2';$environment=@{}
        $ProofDirectory=Join-Path $temporary ('phases/'+[Guid]::NewGuid().ToString('N'))
        [void][IO.Directory]::CreateDirectory($ProofDirectory)
        $scratch=Join-Path ([IO.Path]::GetTempPath()) ('sitefactory-public-ci-'+[Guid]::NewGuid().ToString('N'))
        if(Test-Path -LiteralPath $scratch){throw 'SF-CI-TEST fresh diagnostic scratch'}
        [void][IO.Directory]::CreateDirectory($scratch);$scratchOwned=$true
        $secretMarker=[Guid]::NewGuid().ToString('N');$toolMarker='untrusted-tool-output'
        $privateMessage=$secretMarker+' '+$scratch+' '+$toolMarker
        foreach($definition in $functions|Where-Object Name -cin @('Save-CiReport','Set-CiPhase','Get-CiFailureCode')){
            . ([scriptblock]::Create($definition.Extent.Text))
        }
        . ([scriptblock]::Create($reportAssignment.Extent.Text))
        $productionSave=${function:Save-CiReport};$reportFailure=@{Injected=$false}
        function Save-CiReport {
            if($ReportFails -and $report.phase -ceq 'CLEANUP' -and !$reportFailure.Injected){
                $reportFailure.Injected=$true;throw $privateMessage
            }
            & $productionSave
        }
        function Remove-PublicCiOwnedDirectory([string]$Path){
            $persisted=Get-Content (Join-Path $ProofDirectory 'report.json') -Raw|ConvertFrom-Json
            Check ($persisted.status -ceq 'FAILED') 'no PASS persisted before cleanup'
            if($CleanupFails){throw $privateMessage}
            & $realCleanup $Path
        }
        $previousError=[Console]::Error;$errorCapture=[IO.StringWriter]::new()
        try{
            [Console]::SetError($errorCapture)
            $captured=@(& $exercise)
            $json=Get-Content (Join-Path $ProofDirectory 'report.json') -Raw
            $actual=$json|ConvertFrom-Json
            $expectedStatus=if($WorkFails -or $CleanupFails -or $ReportFails){'FAILED'}else{'PASSED'}
            $expectedPhase=if($WorkFails){$RequestedPhase}else{'CLEANUP'}
            Check ($actual.status -ceq $expectedStatus -and $actual.phase -ceq $expectedPhase) 'phase and terminal status retained'
            Check ($actual.commit -ceq $commit -and $actual.runId -ceq $RunId -and $actual.attempt -ceq $Attempt -and $actual.name -ceq $name) 'phase report provenance preserved'
            Check ($actual.cleanup -ceq $(if($CleanupFails){'Failed'}else{'Completed'})) 'phase report records actual cleanup'
            Check ($actual.code -ceq $(if($CleanupFails){'SF-CI-CLEANUP'}elseif($ReportFails){'SF-CI-REPORT'}elseif($WorkFails){'SF-CI-FAILED'}else{$null})) 'failure code remains controlled'
            $published=$json+$errorCapture.ToString()+($captured -join '')
            foreach($forbidden in @($secretMarker,$scratch,$toolMarker)){
                Check (!$published.Contains($forbidden,[StringComparison]::Ordinal)) 'no exception, tool output or personal path published'
            }
            Check ((Test-Path -LiteralPath $scratch) -eq $CleanupFails) 'owned scratch cleanup is real'
            Reject {Set-CiPhase 'UNAPPROVED_PHASE'} 'uncontrolled phase rejected'
        }finally{
            [Console]::SetError($previousError);$errorCapture.Dispose()
            if(Test-Path -LiteralPath $scratch){& $realCleanup $scratch}
        }
    }
    foreach($platform in @('Windows','Linux','PostgreSql')){
        foreach($phase in $phases){
            Test-PhaseReport $phase $platform ($phase -cne 'CLEANUP') ($phase -ceq 'CLEANUP')
        }
    }
    Test-PhaseReport 'RESTORE' 'Windows' $true $true
    Test-PhaseReport 'BUILD' 'Windows' $false $false
    Test-PhaseReport 'BUILD' 'Windows' $false $false $true

    # One real script invocation fails during COPY, before any restore or provider.
    $emptyRoot=Join-Path $temporary 'empty-source';[void][IO.Directory]::CreateDirectory($emptyRoot)
    $proof=Join-Path $temporary 'copy-failure-proof'
    $info=[Diagnostics.ProcessStartInfo]::new('pwsh')
    $info.UseShellExecute=$false;$info.RedirectStandardOutput=$true;$info.RedirectStandardError=$true
    foreach($argument in @('-NoProfile','-File',$scriptPath,'-Mode','Offline','-RepositoryRoot',$emptyRoot,'-ProofDirectory',$proof,'-ExpectedCommit',$commit,'-RunId','diagnostic-child','-Attempt','1')){[void]$info.ArgumentList.Add($argument)}
    $child=[Diagnostics.Process]::new();$child.StartInfo=$info
    try{
        [void]$child.Start();$stdout=$child.StandardOutput.ReadToEndAsync();$stderr=$child.StandardError.ReadToEndAsync()
        if(!$child.WaitForExit(20000)){$child.Kill($true);$child.WaitForExit();throw 'SF-CI-TEST bounded diagnostic child'}
        $childReport=Get-Content (Join-Path $proof 'report.json') -Raw|ConvertFrom-Json
        Check ($child.ExitCode -eq 1 -and $childReport.status -ceq 'FAILED' -and $childReport.phase -ceq 'COPY' -and $childReport.code -ceq 'SF-CI-GIT' -and $childReport.cleanup -ceq 'Completed') 'real script failure keeps a red COPY report, precise code and exit code'
        Check ($childReport.commit -ceq $commit -and $childReport.runId -ceq 'diagnostic-child' -and $childReport.attempt -ceq '1') 'real script failure provenance'
        $published=$stdout.GetAwaiter().GetResult()+$stderr.GetAwaiter().GetResult()
        Check ($published.Trim() -ceq 'SF-CI-GIT phase=COPY; controlled evidence retained.') 'real script failure publishes only its controlled diagnostic'
    }finally{$child.Dispose()}

    $composition=Join-Path $temporary 'composition'
    foreach($source in $sources){$target=Join-Path $composition $source;[void][IO.Directory]::CreateDirectory((Split-Path $target));[IO.File]::Copy((Join-Path $RepositoryRoot $source),$target,$false)}
    $unapprovedSolution=Join-Path $composition 'Unapproved.slnx'
    [IO.File]::WriteAllText($unapprovedSolution,'<Solution />')
    Check (@(Get-PublicCiSourcePaths $composition) -cnotcontains 'Unapproved.slnx') 'an unapproved root solution never enters the public scope'
    [IO.File]::Delete($unapprovedSolution)
    # The real CI work directory has no Git metadata. Dotfiles must remain in
    # scope on Linux too; mark one hidden on Windows to reproduce that omission.
    $dotfile=Join-Path $composition '.editorconfig'
    $attributes=[IO.File]::GetAttributes($dotfile)
    try{
        if($IsWindows){[IO.File]::SetAttributes($dotfile,($attributes -bor [IO.FileAttributes]::Hidden))}
        $isolatedSources=@(Get-PublicCiSourcePaths $composition)
        Check (Assert-PublicPostgreSqlExact $isolatedSources $sources) 'Git-free discovery preserves every public source including hidden files'
        foreach($required in @('.editorconfig','.gitignore','.gitattributes','.github/workflows/ci.yml')){
            Check ($isolatedSources -ccontains $required) 'required dotfile survives Git-free discovery'
        }
    }finally{[IO.File]::SetAttributes($dotfile,$attributes)}
    Assert-PublicCiComposition $composition;Check $true 'real public project graph'
    Assert-PublicCiComposition $composition -Evaluate;Check $true 'evaluated public inputs only'

    # Offline package fixtures exercise the production provenance check. They do
    # not restore packages or replace any compiled production/test dependency.
    $packageCache=Join-Path $temporary 'locked-packages'
    $fixtureDirectory=Join-Path $temporary 'locked-project'
    [void][IO.Directory]::CreateDirectory($fixtureDirectory)
    $fixtureProject=Join-Path $fixtureDirectory 'Fixture.csproj'
    foreach($case in @(
        @{Kind='None';Package='xunit.runner.visualstudio';Version='3.1.5';Import='build/net8.0/xunit.runner.visualstudio.props';Files=@('build/net8.0/xunit.abstractions.dll','build/net8.0/xunit.runner.visualstudio.testadapter.dll')},
        @{Kind='Content';Package='Microsoft.TestPlatform.TestHost';Version='18.8.1';Import='build/net8.0/Microsoft.TestPlatform.TestHost.props';Files=@('build/net8.0/x64/testhost.exe','build/net8.0/x64/testhost.dll')}
    )){
        $packageRelative=$case.Package.ToLowerInvariant()+'/'+$case.Version
        $packageDirectory=Join-Path $packageCache $packageRelative
        $members=@($case.Import)+$case.Files
        foreach($member in $members){$target=Join-Path $packageDirectory $member;[void][IO.Directory]::CreateDirectory((Split-Path $target));[IO.File]::WriteAllText($target,'synthetic package member')}
        $archivePath=Join-Path $packageDirectory ($case.Package.ToLowerInvariant()+'.'+$case.Version+'.nupkg')
        $archive=[IO.Compression.ZipFile]::Open($archivePath,[IO.Compression.ZipArchiveMode]::Create)
        try{foreach($member in $members){[void][IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive,(Join-Path $packageDirectory $member),$member)}}finally{$archive.Dispose()}
        $archiveHash=[Convert]::ToBase64String([Security.Cryptography.SHA512]::HashData([IO.File]::ReadAllBytes($archivePath)))
        $pin=[Convert]::ToBase64String([Security.Cryptography.SHA512]::HashData([Text.Encoding]::UTF8.GetBytes('restored content pin')))
        [IO.File]::WriteAllText(($archivePath+'.sha512'),$archiveHash)
        $metadataPath=Join-Path $packageDirectory '.nupkg.metadata';Write-Json @{contentHash=$pin} $metadataPath
        $lock=@{dependencies=@{'net10.0'=@{$case.Package=@{resolved=$case.Version;contentHash=$pin}}}}
        $assets=@{packageFolders=@{$packageCache=@{}};libraries=@{($case.Package+'/'+$case.Version)=@{type='package';path=$packageRelative;sha512=$pin;files=$members}}}
        $lockPath=Join-Path $fixtureDirectory 'packages.lock.json';Write-Json $lock $lockPath
        $assetsPath=Join-Path $fixtureDirectory 'obj/project.assets.json';Write-Json $assets $assetsPath
        foreach($member in $case.Files){
            $item=@{FullPath=(Join-Path $packageDirectory $member);DefiningProjectFullPath=(Join-Path $packageDirectory $case.Import)}
            Check (Test-PublicCiLockedTestInput $fixtureProject $case.Kind $item) 'exact locked runner payload and package import accepted'
            foreach($kind in @('Compile','EmbeddedResource')){
                Check (!(Test-PublicCiLockedTestInput $fixtureProject $kind $item)) 'package provenance never admits source or embedded resources'
            }
            $saved=$item.DefiningProjectFullPath;$item.DefiningProjectFullPath=$fixtureProject
            Check (!(Test-PublicCiLockedTestInput $fixtureProject $case.Kind $item)) 'project-authored cache include rejected'
            $item.DefiningProjectFullPath=$saved
            $saved=$item.FullPath;$item.FullPath=Join-Path $packageDirectory 'unapproved.dll'
            Check (!(Test-PublicCiLockedTestInput $fixtureProject $case.Kind $item)) 'arbitrary package-cache payload rejected'
            $item.FullPath=$saved
            $bytes=[IO.File]::ReadAllBytes($item.FullPath);[IO.File]::WriteAllText($item.FullPath,'altered payload')
            Check (!(Test-PublicCiLockedTestInput $fixtureProject $case.Kind $item)) 'modified installed payload rejected'
            [IO.File]::WriteAllBytes($item.FullPath,$bytes)
            $import=$item.DefiningProjectFullPath;$bytes=[IO.File]::ReadAllBytes($import);[IO.File]::WriteAllText($import,'altered import')
            Check (!(Test-PublicCiLockedTestInput $fixtureProject $case.Kind $item)) 'modified package import rejected'
            [IO.File]::WriteAllBytes($import,$bytes)
        }
        [IO.File]::WriteAllText(($archivePath+'.sha512'),'mismatched pin')
        Check (!(Test-PublicCiLockedTestInput $fixtureProject $case.Kind $item)) 'cache archive hash must match the archive'
        [IO.File]::WriteAllText(($archivePath+'.sha512'),$archiveHash)
        Write-Json @{contentHash='mismatched pin'} $metadataPath
        Check (!(Test-PublicCiLockedTestInput $fixtureProject $case.Kind $item)) 'restored package content pin must match the lock'
        Write-Json @{contentHash=$pin} $metadataPath
        $bytes=[IO.File]::ReadAllBytes($archivePath);[IO.File]::WriteAllText($archivePath,'altered archive')
        Check (!(Test-PublicCiLockedTestInput $fixtureProject $case.Kind $item)) 'modified package archive rejected'
        [IO.File]::WriteAllBytes($archivePath,$bytes)
        $assets.libraries[($case.Package+'/'+$case.Version)].sha512='mismatched pin';Write-Json $assets $assetsPath
        Check (!(Test-PublicCiLockedTestInput $fixtureProject $case.Kind $item)) 'resolved assets must match the lock'
        $assets.libraries[($case.Package+'/'+$case.Version)].sha512=$pin
        $assets.libraries[($case.Package+'/'+$case.Version)].files=@($case.Import);Write-Json $assets $assetsPath
        Check (!(Test-PublicCiLockedTestInput $fixtureProject $case.Kind $item)) 'payload missing from package inventory rejected'
        [IO.File]::Delete($assetsPath)
        Check (!(Test-PublicCiLockedTestInput $fixtureProject $case.Kind $item)) 'missing restore metadata rejected'
    }
    $domain=Join-Path $composition 'src/LogicGate.SiteFactory.Domain/LogicGate.SiteFactory.Domain.csproj';$original=[IO.File]::ReadAllText($domain)
    [xml]$xml=$original;$group=$xml.CreateElement('ItemGroup');$reference=$xml.CreateElement('ProjectReference');$reference.SetAttribute('Include','../Unapproved/Unapproved.csproj');[void]$group.AppendChild($reference);[void]$xml.DocumentElement.AppendChild($group);$xml.Save($domain)
    Reject {Assert-PublicCiComposition $composition} 'unapproved project dependency'
    $reference.SetAttribute('Include','../../../outside.csproj');$xml.Save($domain);Reject {Assert-PublicCiComposition $composition} 'escaping project dependency'
    $reference.SetAttribute('Include','../LogicGate.SiteFactory.Application/LogicGate.SiteFactory.Application.csproj');$xml.Save($domain);Reject {Assert-PublicCiComposition $composition} 'reference cycle'
    [IO.File]::WriteAllText($domain,$original)
    [xml]$xml=$original;$group=$xml.CreateElement('ItemGroup');$resource=$xml.CreateElement('EmbeddedResource');$resource.SetAttribute('Include','../../unapproved/payload.txt');[void]$group.AppendChild($resource);[void]$xml.DocumentElement.AppendChild($group);$xml.Save($domain)
    $payload=Join-Path $composition 'unapproved/payload.txt';[void][IO.Directory]::CreateDirectory((Split-Path $payload));[IO.File]::WriteAllText($payload,'synthetic')
    Reject {Assert-PublicCiComposition $composition -Evaluate} 'unapproved embedded resource' 'SF-CI-RESOURCE'
    [IO.File]::WriteAllText($domain,$original)
    $application=Join-Path $composition 'tests/LogicGate.SiteFactory.Application.Tests/LogicGate.SiteFactory.Application.Tests.csproj'
    [xml]$xml=Get-Content $application -Raw
    Check ($xml.Project.PropertyGroup.EnableDefaultCompileItems -ceq 'false' -and @($xml.SelectNodes('//Compile[@Remove]')).Count -eq 0) 'positive application compilation'
    $includes=@($xml.SelectNodes('//Compile[@Include]')|ForEach-Object {$_.GetAttribute('Include')})
    Check ($includes.Count -eq 34 -and @($includes|Sort-Object -Unique).Count -eq 34 -and !@($includes|Where-Object {$_ -match '[*?]'}).Count) '34 exact public compilation inputs'
    # Preserve exact native case identity without archiving inline arguments.
    $caseAssembly='LogicGate.SiteFactory.Domain.Tests'
    $qualifiedCase=$caseAssembly+'.Fixture.Theory'
    foreach($displayCollision in @($false,$true)){
        $marker='inline-argument-must-not-be-archived'
        $nativeNames=@(($qualifiedCase+'(value: "'+$marker+'-one")'),($qualifiedCase+'(value: "'+$marker+'-two")'))
        if($displayCollision){$nativeNames[1]=$nativeNames[0]}
        $caseRows=@(Rows $caseAssembly 2 $nativeNames)
        $seed=Join-Path $temporary ('case-seed-'+$displayCollision+'.trx')
        Save-PublicCiOfflineTrx @{Rows=$caseRows;Count=2} $seed $caseAssembly
        $native=Read-PublicPostgreSqlTrxBytes ([IO.File]::ReadAllBytes($seed))
        $nativeResults=@($native.SelectNodes("//*[local-name()='UnitTestResult']"))
        for($i=0;$i -lt 2;$i++){$nativeResults[$i].SetAttribute('testName',$nativeNames[$i])}
        $nativeBytes=[Text.Encoding]::UTF8.GetBytes($native.OuterXml)
        $validated=Assert-PublicCiOfflineTrx $nativeBytes $nativeNames $caseAssembly $start $finish
        Check ($validated.Count -eq 2 -and @($validated.Rows.id|Sort-Object -Unique).Count -eq 2) 'native case IDs stay distinct even for duplicate display names'
        if(!$displayCollision){
            Check (@($validated.Rows.caseHash|Sort-Object -Unique).Count -eq 2) 'different parameters retain different canonical-name digests'
            Reject {Assert-PublicCiOfflineTrx $nativeBytes @($nativeNames[0],$nativeNames[0]) $caseAssembly $start $finish} 'native discovery multiplicity mismatch'
        }
        $safe=Join-Path $temporary ('case-safe-'+$displayCollision+'.trx')
        Save-PublicCiOfflineTrx $validated $safe $caseAssembly
        $safeBytes=[IO.File]::ReadAllBytes($safe)
        $safeNames=@($validated.Rows|ForEach-Object {Get-PublicCiArchivedCaseName $_})
        $revalidated=Assert-PublicCiOfflineTrx $safeBytes $safeNames $caseAssembly $start $finish -Archived
        Check ($revalidated.Count -eq 2 -and @($safeNames|Sort-Object -Unique).Count -eq 2) 'archived cases retain an exact unique hash and native-ID identity'
        Check (($validated.Rows.caseHash -join ',') -ceq ($revalidated.Rows.caseHash -join ',')) 'archive revalidation does not rehash already hashed names'
        Check (![Text.Encoding]::UTF8.GetString($safeBytes).Contains($marker,[StringComparison]::Ordinal)) 'inline arguments are absent from archived evidence'
        foreach($mutation in @('digest','identity','duplicate','inline','newline')){
            $bad=$revalidated.Document.CloneNode($true);$results=@($bad.SelectNodes("//*[local-name()='UnitTestResult']"));$expected=$safeNames.Clone()
            switch($mutation){
                digest {$results[0].SetAttribute('testName',$safeNames[0].Replace($validated.Rows[0].caseHash,('z'*64)));$expected[0]=$results[0].GetAttribute('testName')}
                identity {$results[0].SetAttribute('testName',$safeNames[0].Replace($validated.Rows[0].id,[Guid]::NewGuid().ToString('D')));$expected[0]=$results[0].GetAttribute('testName')}
                duplicate {$results[1].SetAttribute('testName',$safeNames[0])}
                inline {$results[0].SetAttribute('testName',$safeNames[0].Replace('(case-sha256:','(inline-canary)(case-sha256:'));$expected[0]=$results[0].GetAttribute('testName')}
                newline {$results[0].SetAttribute('testName',($safeNames[0]+"`n"));$expected[0]=$results[0].GetAttribute('testName')}
            }
            Reject {Assert-PublicCiOfflineTrx ([Text.Encoding]::UTF8.GetBytes($bad.OuterXml)) $expected $caseAssembly $start $finish -Archived} ('archived case '+$mutation)
        }
    }

    $jobs=@()
    foreach($platform in @('Windows','Linux')){
        $root=Join-Path $temporary $platform;$suites=@()
        foreach($project in @($projects|Where-Object Offline -gt 0)){
            $path=Join-Path $root ('offline/'+$project.Assembly+'.trx');$rows=@(Rows $project.Assembly $project.Offline)
            Save-PublicCiOfflineTrx @{Rows=$rows;Count=$rows.Count} $path $project.Assembly
            $suites+=@{assembly=$project.Assembly;passed=$rows.Count;inventory=@($rows|ForEach-Object {Get-PublicCiArchivedCaseName $_});sha256=(Get-FileHash $path).Hash;start=$start;finish=$finish}
        }
        $jobs+=@{name=$platform;result='success';status='PASSED';commit=$commit;runId='synthetic';attempt='1';cleanup='Completed';sourceLink='PENDING';root=$root;suites=$suites}
    }
    # Synthetic data exercises the existing P04 parser without any DB execution.
    $pg=Join-Path $temporary 'pg';[void][IO.Directory]::CreateDirectory((Join-Path $pg 'incoming'))
    $manifest=@{campaignId='b'*32;status='PASSED';databaseProof=$true;provenance='RealPostgreSql';commit=$commit;cleanup='Completed';observationsComplete=$true;postgreSql=180006;imageDigest='sha256:3725f4e2499eef5134592b3b4ab79a543ed7f8e533b05b5b637af926630f6650';resourceCleanup=@{database='Completed';container='Completed';secrets='Completed';preexisting='Completed'};shards=@()}
    foreach($shard in $partition.shards){
        $path=Join-Path $pg ('native/'+$shard.name+'.trx');$rows=@(Rows $shard.assembly $shard.expectedCount $shard.expectedTests)
        Save-PublicCiOfflineTrx @{Rows=$rows;Count=$rows.Count} $path $shard.assembly
        [xml]$native=Get-Content $path -Raw;$ns=$native.DocumentElement.NamespaceURI
        # P04 consumes native Fact names, not offline archival transport names.
        $nativeResults=@($native.SelectNodes("//*[local-name()='UnitTestResult']"))
        for($i=0;$i -lt $rows.Count;$i++){$nativeResults[$i].SetAttribute('testName',($rows[$i].class+'.'+$rows[$i].method))}
        $times=$native.CreateElement('Times',$ns);$times.SetAttribute('start',$start.ToString('O'));$times.SetAttribute('finish',$finish.ToString('O'));[void]$native.DocumentElement.AppendChild($times)
        foreach($unit in $native.SelectNodes("//*[local-name()='UnitTest']")){$unit.SetAttribute('storage',($shard.assembly+'.dll'))}
        $counters=$native.SelectSingleNode("//*[local-name()='Counters']")
        foreach($key in @('inconclusive','passedButRunAborted','notRunnable','disconnected','warning','completed','inProgress','pending')){$counters.SetAttribute($key,'0')}
        $native.Save($path);$manifest.shards+=@{shard=$shard.name;status='PASSED';proof=('proof/'+$shard.name+'.json')}
        Write-Json @{campaignId=$manifest.campaignId;status='PASSED';provenance='RealPostgreSql';completeness='Complete';discovery='Passed';incomingCleanup='Completed';processCleanup='Completed';processStatus='Completed';processExitCode=0;processDurationMs=1000;expectedTests=$shard.expectedTests;native=('native/'+$shard.name+'.trx')} (Join-Path $pg ('proof/'+$shard.name+'.json'))
    }
    Write-Json $manifest (Join-Path $pg 'manifest.json')
    $requests=@();$providers=[Collections.Generic.List[object]]::new();$sequence=@(1000,1001,2000,2001,2000,2001,2000,2001,2000,2001,2000,2001,2000,2001,1003,1004)
    for($i=0;$i -lt 768;$i++){
        $trace='1'*32;$span=($i+1).ToString('x16')
        $requests+=@{requestId=$i;status=200;route=@('/','/health','/health/ready')[$i%3];traceId=$trace;spanId=$span;readinessEvents=@(@{name='sitefactory.readiness.refresh.completed';traceId=$trace;spanId=$span;tags=@{outcome='READY'}},@{name='sitefactory.readiness.http.decision';traceId=$trace;spanId=$span;tags=@{outcome='ACCEPTED'}})}
        foreach($id in $sequence){$providers.Add(@{requestId=$i;eventId=$id;exceptionKind='NONE'})}
    }
    $observations=@{campaignId=$manifest.campaignId;workers=32;requestsPerWorker=24;completeness='Complete';http200=768;http503=0;http=$requests;providerEvents=@($providers.ToArray());lifecycle=@(@{kind='APPLICATION_STARTED'},@{kind='APPLICATION_STOPPING'},@{kind='APPLICATION_STOPPED'});providerStatistics=@{remainingRequestCorrelatedConnections=0;droppedEvents=0}}
    Write-Json $observations (Join-Path $pg 'observations/p03.json')

    [void](Assert-PublicCiPostgreSql $pg $commit);Check $true 'completed proof accepts an existing empty incoming directory'
    # The artifact uploader transports files, not empty directories. Exercise
    # the real validator after that exact ZIP round-trip, without any DB work.
    $transportZip=Join-Path $temporary 'pg-file-only.zip'
    $archive=[IO.Compression.ZipFile]::Open($transportZip,[IO.Compression.ZipArchiveMode]::Create)
    try{
        foreach($file in Get-ChildItem -LiteralPath $pg -Recurse -File -Force){
            $relative=[IO.Path]::GetRelativePath($pg,$file.FullName).Replace('\','/')
            [void][IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive,$file.FullName,$relative)
        }
    }finally{$archive.Dispose()}
    $archive=[IO.Compression.ZipFile]::OpenRead($transportZip)
    try{Check (@($archive.Entries|Where-Object {$_.FullName.EndsWith('/') -or $_.FullName.StartsWith('incoming/',[StringComparison]::Ordinal)}).Count -eq 0) 'file-only ZIP contains no incoming directory placeholder'}finally{$archive.Dispose()}
    $transportRoot=Join-Path $temporary 'pg-round-trip'
    [IO.Compression.ZipFile]::ExtractToDirectory($transportZip,$transportRoot)
    $incoming=Resolve-PublicPostgreSqlEvidencePath $transportRoot 'incoming'
    Check (![IO.Path]::Exists($incoming)) 'file-only ZIP drops the empty incoming directory'
    [void](Assert-PublicCiPostgreSql $transportRoot $commit);Check $true 'completed proof survives the real file-only ZIP round-trip'
    $transportManifestPath=Join-Path $transportRoot 'manifest.json';$manifestBytes=[IO.File]::ReadAllBytes($transportManifestPath)
    foreach($case in @('manifest','resource')){
        $transportManifest=[Text.Encoding]::UTF8.GetString($manifestBytes)|ConvertFrom-Json -AsHashtable
        if($case -ceq 'manifest'){$transportManifest.cleanup='NotObserved'}else{$transportManifest.resourceCleanup.secrets='NotObserved'}
        Write-Json $transportManifest $transportManifestPath
        Reject {Assert-PublicCiPostgreSql $transportRoot $commit} ('absent incoming does not replace '+$case+' cleanup') $(if($case -ceq 'manifest'){'SF-CI-PG'}else{'SF-CI-CLEANUP'})
    }
    [IO.File]::WriteAllBytes($transportManifestPath,$manifestBytes)
    $transportProofPath=Join-Path $transportRoot 'proof/g04.json';$proofBytes=[IO.File]::ReadAllBytes($transportProofPath)
    $transportProof=[Text.Encoding]::UTF8.GetString($proofBytes)|ConvertFrom-Json -AsHashtable
    $transportProof.incomingCleanup='NotObserved';Write-Json $transportProof $transportProofPath
    Reject {Assert-PublicCiPostgreSql $transportRoot $commit} 'absent incoming does not replace shard cleanup' 'SF-CI-PG-PROOF'
    [IO.File]::WriteAllBytes($transportProofPath,$proofBytes)
    [void][IO.Directory]::CreateDirectory($incoming)
    foreach($name in @('residual.trx','.hidden-residual.trx')){
        $entry=Join-Path $incoming $name;[IO.File]::WriteAllText($entry,'unprocessed fixture')
        if($IsWindows -and $name.StartsWith('.')){[IO.File]::SetAttributes($entry,[IO.FileAttributes]::Hidden)}
        Reject {Assert-PublicCiPostgreSql $transportRoot $commit} ('incoming entry '+$name) 'SF-CI-INCOMING'
        [IO.File]::Delete($entry)
    }
    [IO.Directory]::Delete($incoming)
    [IO.File]::WriteAllText($incoming,'not a directory')
    Reject {Assert-PublicCiPostgreSql $transportRoot $commit} 'incoming cannot be a file' 'SF-CI-INCOMING'
    [IO.File]::Delete($incoming)
    $linkTarget=Join-Path $temporary 'incoming-link-target';[void][IO.Directory]::CreateDirectory($linkTarget)
    $witness=Join-Path $linkTarget 'witness.txt';[IO.File]::WriteAllText($witness,'preserved fixture')
    $witnessHash=(Get-FileHash -LiteralPath $witness).Hash
    $null=New-Item -ItemType $(if($IsWindows){'Junction'}else{'SymbolicLink'}) -Path $incoming -Target $linkTarget
    try{
        Check (([IO.File]::GetAttributes($incoming) -band [IO.FileAttributes]::ReparsePoint) -ne 0) 'incoming fixture is a real filesystem redirection'
        Reject {Assert-PublicCiPostgreSql $transportRoot $commit} 'incoming redirection is rejected before enumeration' 'SF-TEST-PG-PATH'
    }finally{[IO.Directory]::Delete($incoming)}
    Check ((Get-FileHash -LiteralPath $witness).Hash -ceq $witnessHash) 'incoming redirection target remains untouched'

    $files=@(Get-ChildItem $pg -Recurse -File|ForEach-Object {@{path=[IO.Path]::GetRelativePath($pg,$_.FullName).Replace('\','/');sha256=(Get-FileHash $_.FullName).Hash}})
    $jobs+=@{name='PostgreSql';result='success';status='PASSED';commit=$commit;runId='synthetic';attempt='1';cleanup='Completed';sourceLink='PENDING';campaign=$pg;campaignFiles=$files}
    Assert-PublicCiAggregate $jobs $commit 'synthetic' '1';Check $true 'complete synthetic aggregation'
    Reject {Assert-PublicCiAggregate $jobs[0..1] $commit 'synthetic' '1'} 'missing job'
    Reject {Assert-PublicCiAggregate @($jobs[0],$jobs[0],$jobs[2]) $commit 'synthetic' '1'} 'duplicate matrix job'
    foreach($state in @('failure','skipped','cancelled','unknown')){$jobs[0].result=$state;Reject {Assert-PublicCiAggregate $jobs $commit 'synthetic' '1'} ('job '+$state)};$jobs[0].result='success'
    $jobs[0].commit='c'*40;Reject {Assert-PublicCiAggregate $jobs $commit 'synthetic' '1'} 'wrong commit';$jobs[0].commit=$commit
    $jobs[0].runId='other';Reject {Assert-PublicCiAggregate $jobs $commit 'synthetic' '1'} 'wrong campaign';$jobs[0].runId='synthetic'
    $jobs[0].cleanup='Failed';Reject {Assert-PublicCiAggregate $jobs $commit 'synthetic' '1'} 'cleanup failure';$jobs[0].cleanup='Completed'
    $saved=$jobs[0].suites;$jobs[0].suites=$saved[0..9];Reject {Assert-PublicCiAggregate $jobs $commit 'synthetic' '1'} 'missing suite';$jobs[0].suites=$saved
    $suite=$jobs[0].suites[0];$path=Join-Path $jobs[0].root ('offline/'+$suite.assembly+'.trx');$bytes=[IO.File]::ReadAllBytes($path)
    [IO.File]::Delete($path);Reject {Assert-PublicCiAggregate $jobs $commit 'synthetic' '1'} 'missing TRX';[IO.File]::WriteAllBytes($path,$bytes)
    $base=Read-PublicPostgreSqlTrxBytes $bytes;$names=$suite.inventory
    foreach($case in @('missing','duplicate','extra','failed','skipped','counter','identity','time','namespace')){
        $doc=$base.CloneNode($true);$result=$doc.SelectSingleNode("//*[local-name()='UnitTestResult']");$caseNames=$names.Clone()
        switch($case){missing {[void]$result.ParentNode.RemoveChild($result)};duplicate {[void]$result.ParentNode.AppendChild($result.CloneNode($true))};extra {$result.SetAttribute('testName','Unexpected.Test')};failed {$result.SetAttribute('outcome','Failed')};skipped {$result.SetAttribute('outcome','NotExecuted')};counter {$doc.SelectSingleNode("//*[local-name()='Counters']").SetAttribute('passed','0')};identity {$result.SetAttribute('executionId',[Guid]::NewGuid().ToString('D'))};time {$result.SetAttribute('startTime',$start.AddDays(-1).ToString('O'))};namespace {$method=$doc.SelectSingleNode("//*[local-name()='TestMethod']");$method.SetAttribute('className','LogicGate.SiteFactory.Unapproved.Tests.Case');$caseNames[0]=$method.GetAttribute('className')+'.'+$method.GetAttribute('name');$result.SetAttribute('testName',$caseNames[0])}}
        Reject {Assert-PublicCiOfflineTrx ([Text.Encoding]::UTF8.GetBytes($doc.OuterXml)) $caseNames $suite.assembly $start $finish -Archived} ('TRX '+$case)
    }
    Reject {Assert-PublicCiOfflineTrx ([Text.Encoding]::UTF8.GetBytes('<broken')) $names $suite.assembly $start $finish -Archived} 'corrupt TRX'
    foreach($case in @('missing-shard','extra-shard','cleanup','status','version')){
        $bad=($manifest|ConvertTo-Json -Depth 12)|ConvertFrom-Json -AsHashtable
        switch($case){missing-shard {$bad.shards=@($bad.shards|Select-Object -Skip 1)};extra-shard {$bad.shards+=@{shard='extra';status='PASSED'}};cleanup {$bad.resourceCleanup.database='Failed'};status {$bad.status='FAILED'};version {$bad.postgreSql=180005}}
        Write-Json $bad (Join-Path $pg 'manifest.json');Reject {Assert-PublicCiPostgreSql $pg $commit} ('PG '+$case)
    }
    Write-Json $manifest (Join-Path $pg 'manifest.json')
    $observations.http503=1;Write-Json $observations (Join-Path $pg 'observations/p03.json');Reject {Assert-PublicCiPostgreSql $pg $commit} 'HTTP503';$observations.http503=0
    $saved=$observations.http[0].readinessEvents;$observations.http[0].readinessEvents=@();Write-Json $observations (Join-Path $pg 'observations/p03.json');Reject {Assert-PublicCiPostgreSql $pg $commit} 'missing Activity';$observations.http[0].readinessEvents=$saved
    $saved=$observations.http[1].spanId;$observations.http[1].spanId=$observations.http[0].spanId;Write-Json $observations (Join-Path $pg 'observations/p03.json');Reject {Assert-PublicCiPostgreSql $pg $commit} 'reused Activity';$observations.http[1].spanId=$saved
    Write-Json $observations (Join-Path $pg 'observations/p03.json')
    [IO.File]::Delete((Join-Path $pg 'native/p03.trx'));Reject {Assert-PublicCiPostgreSql $pg $commit} 'missing shard TRX'
}finally{Remove-PublicCiOwnedDirectory $temporary}
Write-Output ('Public CI checks: '+$script:checks+' Passed; offline synthetic proof only.')
