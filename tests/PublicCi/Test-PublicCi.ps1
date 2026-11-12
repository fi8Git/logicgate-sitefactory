[CmdletBinding()]
param([string]$RepositoryRoot=(Join-Path $PSScriptRoot '../..'),[string]$YamlAssembly,
    [string]$CompiledRoot,[string]$Commit,[string]$PublicRepository,[string]$SymbolProofFile)
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
function Edit-CompiledSourceLink([byte[]]$Bytes,[string]$Json,[ValidateSet('Replace','Missing','Duplicate')][string]$Mode='Replace'){
    # Mutate a copy of an actual compiler-produced PDB in place. Retain its
    # document table, checksums and method data; no fabricated PDB/JSON-only proof.
    $changed=[byte[]]$Bytes.Clone();$stream=[IO.MemoryStream]::new($Bytes,$false)
    try{
        $provider=[Reflection.Metadata.MetadataReaderProvider]::FromPortablePdbStream($stream)
        try{
            $reader=$provider.GetMetadataReader();$rows=@();$row=0
            $table=[Reflection.Metadata.Ecma335.TableIndex]::CustomDebugInformation
            $offset=[Reflection.Metadata.Ecma335.MetadataReaderExtensions]::GetTableMetadataOffset($reader,$table)
            $size=[Reflection.Metadata.Ecma335.MetadataReaderExtensions]::GetTableRowSize($reader,$table)
            $blobWidth=if([Reflection.Metadata.Ecma335.MetadataReaderExtensions]::GetHeapSize($reader,[Reflection.Metadata.Ecma335.HeapIndex]::Blob) -gt 65535){4}else{2}
            $guidWidth=if([Reflection.Metadata.Ecma335.MetadataReaderExtensions]::GetHeapSize($reader,[Reflection.Metadata.Ecma335.HeapIndex]::Guid) -gt 65535){4}else{2}
            foreach($handle in $reader.CustomDebugInformation){
                $info=$reader.GetCustomDebugInformation($handle)
                $rows+=@{info=$info;offset=($offset+$row*$size);length=$reader.GetBlobBytes($info.Value).Length;sourceLink=($reader.GetGuid($info.Kind) -eq [guid]'CC110556-A091-4D38-9FEC-25AB9A351A6A')};$row++
            }
            $links=@($rows|Where-Object sourceLink)
            Check ($links.Count -eq 1) 'mutation starts from one real compiled SourceLink record'
            $link=$links[0]
            if($Mode -ceq 'Missing'){
                $guid=[guid]'CC110556-A091-4D38-9FEC-25AB9A351A6A';$hex=[Convert]::ToHexString($Bytes);$needle=[Convert]::ToHexString($guid.ToByteArray());$at=$hex.IndexOf($needle,[StringComparison]::Ordinal)
                Check ($at -ge 0 -and $at%2 -eq 0 -and $hex.LastIndexOf($needle,[StringComparison]::Ordinal) -eq $at) 'record kind has one unambiguous compiled GUID'
                [Array]::Copy(([guid]'aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee').ToByteArray(),0,$changed,($at/2),16)
            }elseif($Mode -ceq 'Duplicate'){
                $other=@($rows|Where-Object {!$_.sourceLink})[0]
                [Array]::Copy($Bytes,($link.offset+$size-$blobWidth-$guidWidth),$changed,($other.offset+$size-$blobWidth-$guidWidth),($guidWidth+$blobWidth))
            }else{
                $payload=[Text.Encoding]::UTF8.GetBytes($Json)
                # Reuse a sufficiently large existing CDI blob when the new
                # mapping is longer. Only a fixture's embedded-source blob is
                # repurposed; document names and hashes remain compiler output.
                $candidate=@($rows|Where-Object {$_.length -ge $payload.Length}|Sort-Object length)[0]
                $heap=[Reflection.Metadata.Ecma335.MetadataReaderExtensions]::GetHeapMetadataOffset($reader,[Reflection.Metadata.Ecma335.HeapIndex]::Blob)
                $blob=[Reflection.Metadata.Ecma335.MetadataTokens]::GetHeapOffset([Reflection.Metadata.BlobHandle]$candidate.info.Value)
                $prefix=if($candidate.length -lt 128){1}elseif($candidate.length -lt 16384){2}else{4}
                $start=$heap+$blob+$prefix
                for($i=0;$i -lt $candidate.length;$i++){$changed[$start+$i]=32}
                [Array]::Copy($payload,0,$changed,$start,$payload.Length)
                [Array]::Copy($Bytes,($candidate.offset+$size-$blobWidth),$changed,($link.offset+$size-$blobWidth),$blobWidth)
            }
        }finally{$provider.Dispose()}
    }finally{$stream.Dispose()}
    return ,$changed
}
if($CompiledRoot){
    # This mode consumes a real build only. Never call Offline, restore, build or
    # the YAML guard here: the outer harness invokes it after its own build.
    $symbolRoot=$null;$symbolFailure=$false
    try{
        if($Commit -cnotmatch '^[0-9a-f]{40}$'){throw 'SF-CI-PROVENANCE'}
        $CompiledRoot=[IO.Path]::GetFullPath($CompiledRoot)
        $validationTimer=[Diagnostics.Stopwatch]::StartNew()
        $symbolProof=Assert-PublicCiSymbols $CompiledRoot $PublicRepository $Commit
        $symbolValidationMs=$validationTimer.Elapsed.TotalMilliseconds;$validationTimer.Restart()
        Check ($symbolProof.pdbCount -ge 23) 'real compiled symbols accepted'
        Assert-PublicCiBinaries $CompiledRoot $PublicRepository $Commit
        $binaryValidationMs=$validationTimer.Elapsed.TotalMilliseconds;$validationTimer.Stop()
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
        # Exercise the first production assembly so malformed-record cases fail
        # promptly, while retaining the full 23-project validator in every case.
        $domain=Join-Path $symbolRoot 'src/LogicGate.SiteFactory.Application/bin/Release/net10.0/LogicGate.SiteFactory.Application.pdb'
        $domainBytes=[IO.File]::ReadAllBytes($domain)
        $duplicate=Join-Path (Split-Path $domain) 'duplicate/LogicGate.SiteFactory.Application.pdb'
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
        Check ((Assert-PublicCiSymbols $symbolRoot $PublicRepository $Commit).pdbCount -ge 23) 'restoring source bytes proves checksum state is fresh for every invocation'
        # This canonical PDB is enumerated after dependency copies of the same
        # document. A previously hashed file cannot compensate its bad checksum.
        $laterPdb=Join-Path $symbolRoot 'src/LogicGate.SiteFactory.Domain/bin/Release/net10.0/LogicGate.SiteFactory.Domain.pdb'
        $laterBytes=[IO.File]::ReadAllBytes($laterPdb);$stream=[IO.MemoryStream]::new($laterBytes,$false)
        try{
            $provider=[Reflection.Metadata.MetadataReaderProvider]::FromPortablePdbStream($stream)
            try{
                $reader=$provider.GetMetadataReader()
                $hashes=@(foreach($handle in $reader.Documents){$document=$reader.GetDocument($handle);if($reader.GetString($document.Name) -ceq '/_/src/LogicGate.SiteFactory.Domain/AssemblyMarker.cs'){[Convert]::ToHexString($reader.GetBlobBytes($document.Hash))}})
            }finally{$provider.Dispose()}
        }finally{$stream.Dispose()}
        Check ($hashes.Count -eq 1) 'real repository document has one checksum'
        $hex=[Convert]::ToHexString($laterBytes);$offset=$hex.IndexOf($hashes[0],[StringComparison]::Ordinal)
        Check ($offset -ge 0 -and $offset%2 -eq 0 -and $hex.LastIndexOf($hashes[0],[StringComparison]::Ordinal) -eq $offset) 'repository document checksum blob is unambiguous'
        $changed=[byte[]]$laterBytes.Clone();$changed[$offset/2]=$changed[$offset/2] -bxor 1;[IO.File]::WriteAllBytes($laterPdb,$changed)
        Reject {Assert-PublicCiSymbols $symbolRoot $PublicRepository $Commit} 'every PDB checksum remains checked after source hash reuse' 'SF-CI-PDB-CHECKSUM'
        [IO.File]::WriteAllBytes($laterPdb,$laterBytes)
        Reject {Assert-PublicCiSymbols $symbolRoot 'unapproved/expected-public' $Commit} 'missing or mismatched SourceLink rejected' $(if($PublicRepository){'SF-CI-SOURCELINK-REPOSITORY'}else{'SF-CI-SOURCELINK-RECORD'})
        if($PublicRepository){
            Reject {Assert-PublicCiSymbols $symbolRoot '' $Commit} 'public symbols cannot leak into private mode' 'SF-CI-PRIVATE-SOURCELINK'
            $otherCommit=if($Commit -ceq ('0'*40)){'1'*40}else{'0'*40}
            Reject {Assert-PublicCiSymbols $symbolRoot $PublicRepository $otherCommit} 'wrong executed SHA rejected' 'SF-CI-SOURCELINK-REVISION'
            $url="https://raw.githubusercontent.com/$PublicRepository/$Commit/"
            $cases=@(
                @{name='empty';json='{"documents":{}}';code='SF-CI-SOURCELINK-EMPTY'},
                @{name='malformed';json='{broken';code='SF-CI-SOURCELINK-FORMAT'},
                @{name='uncovered';json=(@{documents=@{'/_/unused/*'=($url+'*')}}|ConvertTo-Json -Compress);code='SF-CI-SOURCELINK-UNCOVERED'},
                @{name='unexpected path';json=(@{documents=@{'/_/*'=($url+'unexpected/*')}}|ConvertTo-Json -Compress);code='SF-CI-SOURCELINK-PATH'},
                @{name='encoded path';json=(@{documents=@{'/_/*'=($url+'%2e%2e/*')}}|ConvertTo-Json -Compress);code='SF-CI-SOURCELINK-PATH'},
                @{name='overlap';json=(@{documents=@{'/_/*'=($url+'*');'/_/src/*'=($url+'src/*')}}|ConvertTo-Json -Compress);code='SF-CI-SOURCELINK-AMBIGUOUS'},
                @{name='duplicate key';json=('{"documents":{"/_/*":"'+$url+'*","/_/*":"'+$url+'*"}}');code='SF-CI-SOURCELINK-AMBIGUOUS'}
            )
            foreach($case in $cases){
                [IO.File]::WriteAllBytes($domain,(Edit-CompiledSourceLink $domainBytes $case.json))
                Reject {Assert-PublicCiSymbols $symbolRoot $PublicRepository $Commit} ('compiled mapping '+$case.name) $case.code
            }
            [IO.File]::WriteAllBytes($domain,(Edit-CompiledSourceLink $domainBytes '' Missing))
            Reject {Assert-PublicCiSymbols $symbolRoot $PublicRepository $Commit} 'missing record in one real PDB' 'SF-CI-SOURCELINK-RECORD'
            [IO.File]::WriteAllBytes($domain,(Edit-CompiledSourceLink $domainBytes '' Duplicate))
            Reject {Assert-PublicCiSymbols $symbolRoot $PublicRepository $Commit} 'duplicate record in one real PDB' 'SF-CI-SOURCELINK-DUPLICATE'
            $second=Join-Path $symbolRoot 'src/LogicGate.SiteFactory.Domain/bin/Release/net10.0/LogicGate.SiteFactory.Domain.pdb'
            $secondBytes=[IO.File]::ReadAllBytes($second)
            [IO.File]::WriteAllBytes($domain,(Edit-CompiledSourceLink $domainBytes '' Missing))
            [IO.File]::WriteAllBytes($second,(Edit-CompiledSourceLink $secondBytes '' Duplicate))
            Reject {Assert-PublicCiSymbols $symbolRoot $PublicRepository $Commit} 'extra record cannot compensate another PDB without a record'
            [IO.File]::WriteAllBytes($second,$secondBytes);[IO.File]::WriteAllBytes($domain,$domainBytes)
            Check ((Assert-PublicCiSymbols $symbolRoot $PublicRepository $Commit).sourceLink -ceq 'VERIFIED') 'restored compiled mappings still pass'
        }
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
                    if($reader.GetString($document.Name) -cmatch '^/_(1)?/xunit\.v3\.core\.mtp-v1/[0-9.]+/_content/DefaultRunnerReporters\.cs$'){
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
    if($SymbolProofFile){
        if(Test-Path -LiteralPath $SymbolProofFile){throw 'SF-CI-PDB-PROOF'}
        [IO.File]::WriteAllText($SymbolProofFile,(@{pdbCount=$symbolProof.pdbCount;sourceLink=$symbolProof.sourceLink;binariesVerified=$true;
            symbolValidationMs=[Math]::Round($symbolValidationMs,3);binaryValidationMs=[Math]::Round($binaryValidationMs,3)}|ConvertTo-Json))
    }
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
    if($Text -match 'pull_request_target|continue-on-error|secrets\.|packages: write|uses:\s+\./|DOCS_ONLY|paths(-ignore)?:|sparse-checkout|blob:none'){throw 'SF-CI-SECURITY'}
    foreach($event in @('pull_request','push','workflow_dispatch')){[void](Node (Node $yaml 'on') $event)}
    $jobs=Node $yaml 'jobs'
    if(!(Assert-PublicPostgreSqlExact @($jobs.Children.Keys|ForEach-Object Value) @('public','postgresql','proof'))){throw 'SF-CI-JOBS'}
    $offline=Node $jobs 'public';$pg=Node $jobs 'postgresql';$proof=Node $jobs 'proof'
    $matrix=Node (Node (Node $offline 'strategy') 'matrix') 'include'
    $pairs=@($matrix.Children|ForEach-Object {((Node $_ 'platform').Value)+'/'+((Node $_ 'os').Value)})
    if(!(Assert-PublicPostgreSqlExact $pairs @('Windows/windows-latest','Linux/ubuntu-latest'))){throw 'SF-CI-MATRIX'}
    foreach($specification in @(@{job=$offline;mode='Offline'},@{job=$pg;mode='PostgreSql'})){
        $job=$specification.job;$mode=$specification.mode
        if(@($job.Children.Keys|ForEach-Object Value|Where-Object {$_ -cin @('if','needs')}).Count){throw 'SF-CI-JOBS'}
        $run=@((Node $job 'steps').Children|Where-Object {try{(Node $_ 'run').Value.Contains('-Mode '+$mode,[StringComparison]::Ordinal)}catch{$false}})
        if($run.Count -ne 1 -or (Node (Node $run[0] 'env') 'PUBLIC_REPOSITORY').Value -cne '${{ github.repository }}' -or
            !(Node $run[0] 'run').Value.Contains('-ExpectedCommit $env:GITHUB_SHA',[StringComparison]::Ordinal) -or
            !(Node $run[0] 'run').Value.Contains('-PublicRepository $env:PUBLIC_REPOSITORY',[StringComparison]::Ordinal)){throw 'SF-CI-PROVENANCE'}
    }
    if((Node $pg 'runs-on').Value -cne 'ubuntu-latest' -or (Node $proof 'if').Value -cne 'always()' -or
        (Node $proof 'name').Value -cne 'Require complete public proof'){throw 'SF-CI-AGGREGATE'}
    if(!(Assert-PublicPostgreSqlExact @((Node $proof 'needs').Children|ForEach-Object Value) @('public','postgresql'))){throw 'SF-CI-NEEDS'}
    foreach($job in @($offline,$pg,$proof)){
        $steps=(Node $job 'steps').Children
        $checkout=@($steps|Where-Object {try{(Node $_ 'uses').Value -like 'actions/checkout@*'}catch{$false}})
        if($checkout.Count -ne 1){throw 'SF-CI-CHECKOUT'}
        $with=Node $checkout[0] 'with'
        if(!(Assert-PublicPostgreSqlExact @($with.Children.Keys|ForEach-Object Value) @('persist-credentials','fetch-depth')) -or
            (Node $with 'persist-credentials').Value -cne 'false' -or (Node $with 'fetch-depth').Value -cne '1'){throw 'SF-CI-CHECKOUT'}
        foreach($step in $steps){
            $use=try{(Node $step 'uses').Value}catch{''}
            if($use -and $use -cnotmatch '^actions/(checkout|setup-dotnet|upload-artifact|download-artifact)@[0-9a-f]{40}$'){throw 'SF-CI-PIN'}
            if($use -like 'actions/upload-artifact@*' -and (Node $step 'if').Value -cne 'always()'){throw 'SF-CI-ARCHIVE'}
            if($use -like 'actions/setup-dotnet@*' -and ((Node (Node $step 'with') 'cache').Value -cne 'false' -or
                (Node (Node $step 'with') 'global-json-file').Value -cne 'global.json')){throw 'SF-CI-SDK'}
        }
    }
}
$workflow=Get-Content (Join-Path $RepositoryRoot '.github/workflows/ci.yml') -Raw
Assert-Workflow $workflow;Check $true 'public workflow matrix, required check, checkout and permissions'
foreach($case in @(
    @{Old='platform: Windows';New='platform: Missing'},
    @{Old='platform: Linux';New='platform: Windows'},
    @{Old='[public, postgresql]';New='[public]'},
    @{Old='contents: read';New='contents: write'},
    @{Old='persist-credentials: false';New='persist-credentials: true'},
    @{Old='fetch-depth: 1';New='fetch-depth: 0'},
    @{Old='cache: false';New='cache: true'},
    @{Old='Require complete public proof';New='Renamed proof'},
    @{Old='  public:';New='  other:'},
    @{Old='  pull_request:';New=('  pull_request:'+"`n"+'    paths: [docs/**]')},
    @{Old='actions/checkout@3d3c42e5aac5ba805825da76410c181273ba90b1';New='actions/checkout@main'},
    @{Old='if: always()';New='if: success()'},
    @{Old='${{ github.repository }}';New='UNKNOWN'}
)){Reject {Assert-Workflow $workflow.Replace($case.Old,$case.New)} ('workflow rejects '+$case.New)}
Reject {Assert-Workflow 'jobs: [ unclosed'} 'corrupt YAML'
$projects=@(Get-PublicCiProjects)
Check ($projects.Count -eq 23 -and @($projects|Where-Object Offline -gt 0).Count -eq 11 -and ($projects|Measure-Object Offline -Sum).Sum -eq 2726) 'closed project inventory'
$partition=Read-PublicPostgreSqlPartition (Join-Path $RepositoryRoot 'tests/PostgreSql/Public/shards.json')
Check ($partition.expectedTests.Count -eq 23 -and $partition.shards.Count -eq 5 -and ($partition.shards.expectedCount -join ',') -ceq '5,3,8,4,3') 'unchanged P04 partition'
$sources=@(Get-PublicCiSourcePaths $RepositoryRoot)
Check (@($sources|Where-Object {$_ -clike '*.slnx'}).Count -eq 1 -and $sources -ccontains 'SiteFactory.slnx') 'public solution is the only selected solution'
[xml]$publicSolution=Get-Content (Join-Path $RepositoryRoot 'SiteFactory.slnx') -Raw
$solutionProjects=@($publicSolution.SelectNodes('//Project')|ForEach-Object {$_.GetAttribute('Path')})
Check (Assert-PublicPostgreSqlExact $solutionProjects @($projects.Path)) 'checked-out public solution contains exactly the closed23 projects'
foreach($script in @('scripts/public-ci/Invoke-PublicCi.ps1','scripts/public-ci/PublicCiProof.ps1','tests/PublicCi/Test-PublicCi.ps1')){Check ($sources -ccontains $script) 'required public tool'}
$temporary=Join-Path ([IO.Path]::GetTempPath()) ('sitefactory-ci-tests-'+[Guid]::NewGuid().ToString('N'))
if(Test-Path -LiteralPath $temporary){throw 'SF-CI-PROVENANCE'}
[void][IO.Directory]::CreateDirectory($temporary)
$start=[DateTimeOffset]::Parse('2026-01-01T00:00:00Z');$finish=$start.AddSeconds(1);$commit='a'*40
function Write-Json($Value,[string]$Path){[void][IO.Directory]::CreateDirectory((Split-Path $Path));[IO.File]::WriteAllText($Path,($Value|ConvertTo-Json -Depth 16))}
function Rows([string]$Assembly,[int]$Count,[string[]]$Names=@()){
    @(for($i=0;$i -lt $Count;$i++){$name=if($Names.Count){$Names[$i]}else{$Assembly+'.Fixture.Case'+$i};$qualified=$name.Split('(')[0];$dot=$qualified.LastIndexOf('.');@{id=[Guid]::NewGuid().ToString('D');execution=[Guid]::NewGuid().ToString('D');caseHash=(Get-PublicCiCaseHash $name);class=$qualified.Substring(0,$dot);method=$qualified.Substring($dot+1);start=$start.ToString('O');end=$finish.ToString('O');duration='00:00:01';outcome='Passed'}})
}
try{
    # Verify scratch-only Git metadata without fabricating a commit or opening
    # another repository. Only the two read-only Git queries are substituted.
    $gitImplementation=${function:Invoke-PublicCiGit}
    $fixtureHead=$commit;$fixtureOrigin='https://github.com/fixture/public.git'
    $checkout=Join-Path $temporary 'verified-checkout';$metadataWork=Join-Path $temporary 'metadata-work'
    [void][IO.Directory]::CreateDirectory((Join-Path $checkout '.git'))
    $witness=Join-Path $checkout '.git/config';[IO.File]::WriteAllText($witness,'private-config-canary')
    $witnessHash=(Get-FileHash -LiteralPath $witness).Hash
    function Invoke-PublicCiGit([string]$Root,[string[]]$Arguments){
        Check ($Root -ceq $checkout) 'Git provenance reads only the verified checkout'
        switch -CaseSensitive ($Arguments -join ' '){
            'rev-parse HEAD' {return $fixtureHead}
            'remote get-url origin' {return $fixtureOrigin}
            default {throw 'SF-CI-TEST unexpected Git mutation/query'}
        }
    }
    try{
        Initialize-PublicCiGitMetadata $checkout $metadataWork 'fixture/public' $commit
        $metadata=Join-Path $metadataWork '.git'
        $files=@(Get-ChildItem -LiteralPath $metadata -Recurse -File -Force)
        Check ($files.Count -eq 2 -and (Assert-PublicPostgreSqlExact @($files.Name) @('HEAD','config'))) 'minimal Git metadata has no objects, history, index, hooks or credentials'
        Check ([IO.File]::ReadAllText((Join-Path $metadata 'HEAD')) -ceq ($commit+"`n")) 'scratch HEAD is the executed SHA'
        Check ([IO.File]::ReadAllText((Join-Path $metadata 'config')) -ceq ('[remote "origin"]'+"`n"+'url = https://github.com/fixture/public'+"`n")) 'scratch config contains only verified public identity'
        Reject {Initialize-PublicCiGitMetadata $checkout $metadataWork 'fixture/public' $commit} 'existing metadata cannot be overwritten' 'SF-CI-ISOLATION'
        Reject {Initialize-PublicCiGitMetadata $checkout $checkout 'fixture/public' $commit} 'checkout metadata cannot be modified' 'SF-CI-ISOLATION'
        $fixtureHead='b'*40
        Reject {Initialize-PublicCiGitMetadata $checkout (Join-Path $temporary 'wrong-sha') 'fixture/public' $commit} 'actual checkout SHA must equal campaign SHA' 'SF-CI-REVISION'
        $fixtureHead=$commit
        foreach($origin in @('https://github.com/fixture/private.git','https://credential-canary@github.com/fixture/public.git','ssh://git@github.com/fixture/public.git')){
            $fixtureOrigin=$origin
            Reject {Initialize-PublicCiGitMetadata $checkout (Join-Path $temporary 'wrong-origin') 'fixture/public' $commit} 'wrong or credential-bearing origin rejected' 'SF-CI-REPOSITORY'
        }
        Check ((Get-FileHash -LiteralPath $witness).Hash -ceq $witnessHash) 'original checkout config remains untouched'
        Check (![IO.Path]::Exists((Join-Path $temporary 'wrong-sha')) -and ![IO.Path]::Exists((Join-Path $temporary 'wrong-origin'))) 'provenance failures create no metadata'
    }finally{Set-Item Function:Invoke-PublicCiGit $gitImplementation}
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
    foreach($code in @('SF-CI-PDB-PATH','SF-CI-PDB-CHECKSUM','SF-CI-PDB-MISSING','SF-CI-PDB-ORIGIN','SF-CI-PDB-PROOF','SF-CI-CASE-IDENTITY','SF-CI-TRX',
        'SF-CI-SOURCELINK-RECORD','SF-CI-SOURCELINK-DUPLICATE','SF-CI-SOURCELINK-FORMAT','SF-CI-SOURCELINK-EMPTY',
        'SF-CI-SOURCELINK-REPOSITORY','SF-CI-SOURCELINK-REVISION','SF-CI-SOURCELINK-UNCOVERED','SF-CI-SOURCELINK-AMBIGUOUS','SF-CI-SOURCELINK-PATH')){
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
        foreach($definition in $functions|Where-Object Name -cin @('Save-CiReport','Complete-CiTiming','Set-CiPhase','Get-CiFailureCode')){
            . ([scriptblock]::Create($definition.Extent.Text))
        }
        . ([scriptblock]::Create($reportAssignment.Extent.Text))
        $phaseTimer=[Diagnostics.Stopwatch]::StartNew()
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
            Check ($actual.timings.Count -gt 0 -and @($actual.timings|Where-Object {$_.phase -cnotin $phases -or $_.durationMs -lt 0}).Count -eq 0) 'timings contain only closed phases and measured nonnegative durations'
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

    # Real nonzero child processes feed native-shaped red/partial TRX fixtures
    # through the production suite runner and its actual catch/finally cleanup.
    # These are counter-proofs, never passed through the success TRX archiver.
    function Test-OfflineFailure([string]$Case){
        $ExpectedCommit=$commit;$RunId='offline-failure-fixture';$Attempt='1';$name='Windows';$environment=@{}
        $fixtureProject=@(Get-PublicCiProjects | Where-Object Assembly -ceq 'LogicGate.SiteFactory.Domain.Tests')[0]
        $fixtureNames=@(0..($fixtureProject.Offline-1) | ForEach-Object {$fixtureProject.Assembly+'.Fixture.Case'+$_})
        $marker='fixture-private-argument-and-exception';$fixtureNames[0]+='(value: "'+$marker+'")'
        $ProofDirectory=Join-Path $temporary ('failures/'+[Guid]::NewGuid().ToString('N'))
        [void][IO.Directory]::CreateDirectory($ProofDirectory)
        $scratch=Join-Path ([IO.Path]::GetTempPath()) ('sitefactory-public-ci-'+[Guid]::NewGuid().ToString('N'))
        [void][IO.Directory]::CreateDirectory($scratch);$scratchOwned=$true
        foreach($definition in $functions | Where-Object Name -cin @('Save-CiReport','Complete-CiTiming','Set-CiPhase','Get-CiFailureCode','Run-CiDotnet','Invoke-CiOfflineSuite')){. ([scriptblock]::Create($definition.Extent.Text))}
        . ([scriptblock]::Create($reportAssignment.Extent.Text))
        $phaseTimer=[Diagnostics.Stopwatch]::StartNew()
        $seed=Join-Path $scratch 'fixture.xml';$raw=Join-Path $scratch ($fixtureProject.Assembly+'.trx')
        $xml=[Xml.XmlDocument]::new();$ns='http://microsoft.com/schemas/VisualStudio/TeamTest/2010'
        $root=$xml.CreateElement('TestRun',$ns);[void]$xml.AppendChild($root)
        $results=$xml.CreateElement('Results',$ns);$definitions=$xml.CreateElement('TestDefinitions',$ns);$entries=$xml.CreateElement('TestEntries',$ns)
        foreach($node in @($results,$definitions,$entries)){[void]$root.AppendChild($node)}
        $rows=@(Rows $fixtureProject.Assembly $fixtureProject.Offline $fixtureNames)
        for($i=0;$i -lt $rows.Count;$i++){
            $row=$rows[$i];$result=$xml.CreateElement('UnitTestResult',$ns)
            foreach($pair in @(@('testId',$row.id),@('executionId',$row.execution),@('testName',$fixtureNames[$i]),@('outcome',$(if($i -eq 0 -and $Case -cnotin @('incomplete','nonzero-passed','success')){'Failed'}else{'Passed'})),@('duration','00:00:00'))){$result.SetAttribute($pair[0],$pair[1])}
            [void]$results.AppendChild($result)
            $unit=$xml.CreateElement('UnitTest',$ns);$unit.SetAttribute('id',$row.id)
            $method=$xml.CreateElement('TestMethod',$ns);$method.SetAttribute('className',$row.class);$method.SetAttribute('name',$row.method);$method.SetAttribute('codeBase',($fixtureProject.Assembly+'.dll'));[void]$unit.AppendChild($method)
            $nativeExecution=$xml.CreateElement('Execution',$ns);$nativeExecution.SetAttribute('id',$row.execution);[void]$unit.AppendChild($nativeExecution);[void]$definitions.AppendChild($unit)
            $entry=$xml.CreateElement('TestEntry',$ns);$entry.SetAttribute('testId',$row.id);$entry.SetAttribute('executionId',$row.execution);[void]$entries.AppendChild($entry)
        }
        $summary=$xml.CreateElement('ResultSummary',$ns);$summary.SetAttribute('outcome',$(if($Case -cin @('incomplete','nonzero-passed','success')){'Completed'}else{'Failed'}))
        $counter=$xml.CreateElement('Counters',$ns)
        foreach($pair in @(@('total',$rows.Count),@('executed',$rows.Count),@('passed',$(if($Case -cin @('incomplete','nonzero-passed','success')){$rows.Count}else{$rows.Count-1})),@('failed',$(if($Case -cin @('incomplete','nonzero-passed','success')){0}else{1})))){$counter.SetAttribute($pair[0],[string]$pair[1])}
        [void]$summary.AppendChild($counter);[void]$root.AppendChild($summary)
        $output=$xml.CreateElement('Output',$ns);$output.InnerText=$marker+' password=fixture-canary '+$scratch;[void]$root.AppendChild($output)
        $errorInfo=$xml.CreateElement('ErrorInfo',$ns);$errorMessage=$xml.CreateElement('Message',$ns);$errorMessage.InnerText=$marker
        $stackTrace=$xml.CreateElement('StackTrace',$ns);$stackTrace.InnerText=$scratch+'/'+$marker
        [void]$errorInfo.AppendChild($errorMessage);[void]$errorInfo.AppendChild($stackTrace);[void]$output.AppendChild($errorInfo)
        $attachments=$xml.CreateElement('ResultFiles',$ns);$attachment=$xml.CreateElement('ResultFile',$ns);$attachment.SetAttribute('path',($scratch+'/'+$marker));[void]$attachments.AppendChild($attachment);[void]$root.AppendChild($attachments)
        if($Case -ceq 'incomplete'){[void]$results.RemoveChild($results.LastChild)}
        if($Case -ceq 'unapproved'){
            $method=$definitions.FirstChild.SelectSingleNode("*[local-name()='TestMethod']");$method.SetAttribute('className','Unapproved.Namespace')
            $results.FirstChild.SetAttribute('testName','Unapproved.Namespace.Case0(value: "'+$marker+'")')
        }
        $xml.Save($seed)
        $fixture=Join-Path $scratch 'child.ps1'
        $quote={param($value) "'"+$value.Replace("'","''")+"'"}
        $childExit=if($Case -cin @('incomplete','invalid','success')){0}else{17}
        $body=if($Case -ceq 'missing'){''}elseif($Case -ceq 'invalid'){'[IO.File]::WriteAllText('+(& $quote $raw)+',"<broken")'}else{
            '[xml]$x=Get-Content -LiteralPath '+(& $quote $seed)+' -Raw; $now=[DateTimeOffset]::UtcNow.ToString("O"); foreach($r in $x.SelectNodes('+(& $quote "//*[local-name()='UnitTestResult']")+')){$r.SetAttribute("startTime",$now);$r.SetAttribute("endTime",$now)}; $x.Save('+(& $quote $raw)+')'
        }
        $body+="`n[Console]::WriteLine('"+$marker+"');[Console]::Error.WriteLine('"+$marker+"');exit "+$childExit
        [IO.File]::WriteAllText($fixture,$body)
        function dotnet {& pwsh -NoProfile -File $fixture;$global:LASTEXITCODE=$LASTEXITCODE}
        $previousExit=$global:LASTEXITCODE
        $productionSave=${function:Save-CiReport};$injected=@{value=$false}
        function Save-CiReport {
            if($Case -ceq 'write-failure' -and $report.Contains('failure') -and !$injected.value){$injected.value=$true;throw $marker}
            & $productionSave
        }
        function Remove-PublicCiOwnedDirectory([string]$Path){
            $persisted=Get-Content (Join-Path $ProofDirectory 'report.json') -Raw | ConvertFrom-Json -AsHashtable
            Check ($persisted.status -ceq 'FAILED') 'suite proof never passes before cleanup'
            if($Case -cne 'success'){
                if($Case -ceq 'write-failure'){Check $injected.value 'diagnostic persistence fault actually exercised'}else{
                    Check ($persisted.ContainsKey('failure') -and $persisted.failure.processExitCode -eq $childExit) 'failure and real child exit survive before scratch cleanup'
                }
                Check (!(Test-Path (Join-Path $ProofDirectory ('offline/'+$fixtureProject.Assembly+'.trx')))) 'red or incomplete TRX never uses success archive'
            }
            & $realCleanup $Path
        }
        $exercise=[scriptblock]::Create('try { Set-CiPhase ''TEST''; Invoke-CiOfflineSuite $fixtureProject $fixtureNames; $report.status=''PASSED'';$report.code=$null } '+$execution[0].CatchClauses[0].Extent.Text+' finally '+$execution[0].Finally.Extent.Text)
        $previousError=[Console]::Error;$capture=[IO.StringWriter]::new()
        try{
            [Console]::SetError($capture);$published=@(& $exercise)
            $text=Get-Content (Join-Path $ProofDirectory 'report.json') -Raw;$actual=$text | ConvertFrom-Json -AsHashtable
            Check ($actual.cleanup -ceq 'Completed' -and ![IO.Path]::Exists($scratch)) ('suite failure cleanup remains real: '+$Case)
            if($Case -ceq 'success'){
                Check ($actual.status -ceq 'PASSED' -and $actual.suites.Count -eq 1 -and $actual.suites[0].passed -eq $fixtureProject.Offline) 'unchanged success path validates and archives all cases'
            }else{
                Check ($actual.status -ceq 'FAILED' -and $actual.suites.Count -eq 0 -and !$actual.failure.completeProof) 'partial failure never becomes complete passed proof'
                Check ($actual.failure.suite -ceq $fixtureProject.Assembly -and $actual.failure.processExitCode -eq $childExit) 'closed suite and actual native exit retained'
                Check ($actual.code -ceq $(if($childExit){'SF-CI-DOTNET'}elseif($Case -ceq 'invalid'){'SF-CI-FAILED'}else{'SF-CI-TRX'})) ('original failure code retained: '+$Case)
                Check ($actual.failure.trx -ceq $(if($Case -ceq 'missing'){'Missing'}elseif($Case -ceq 'invalid'){'Invalid'}else{'Present'})) 'TRX missing, invalid or present distinguished'
                if($Case -cin @('missing','invalid')){Check ($null -eq $actual.failure.observedResults -and $null -eq $actual.failure.counters) 'unavailable results remain unknown'}else{
                    Check ($actual.failure.observedResults -eq $(if($Case -ceq 'incomplete'){$fixtureProject.Offline-1}else{$fixtureProject.Offline})) 'observed partial result count is not fabricated'
                    Check ($actual.failure.counters.total -eq $fixtureProject.Offline) 'reported TRX counters retained separately from observations'
                    Check ($actual.failure.failedCases.Count -eq $(if($Case -cin @('incomplete','nonzero-passed','unapproved')){0}else{1})) 'only authorized failed identities retained without arguments'
                }
            }
            foreach($forbidden in @($marker,$scratch,'password=fixture-canary','ResultFile','StackTrace')){Check (!(($text+$capture.ToString()+($published -join '')).Contains($forbidden,[StringComparison]::Ordinal))) 'failure observations disclose no arguments, paths, output or attachment'}
        }finally{
            [Console]::SetError($previousError);$capture.Dispose();$global:LASTEXITCODE=$previousExit
            if([IO.Directory]::Exists($scratch)){& $realCleanup $scratch}
        }
    }
    foreach($case in @('failed','missing','incomplete','invalid','nonzero-passed','unapproved','write-failure','success')){Test-OfflineFailure $case}

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
        Check ($child.ExitCode -eq 1 -and $childReport.status -ceq 'FAILED' -and $childReport.phase -ceq 'COPY' -and $childReport.code -ceq 'SF-CI-COMPOSITION' -and $childReport.cleanup -ceq 'Completed') 'real script failure keeps a red COPY report, precise code and exit code'
        Check ($childReport.commit -ceq $commit -and $childReport.runId -ceq 'diagnostic-child' -and $childReport.attempt -ceq '1') 'real script failure provenance'
        $published=$stdout.GetAwaiter().GetResult()+$stderr.GetAwaiter().GetResult()
        Check ($published.Trim() -ceq 'SF-CI-COMPOSITION phase=COPY; controlled evidence retained.') 'real script failure publishes only its controlled diagnostic'
    }finally{$child.Dispose()}

    $composition=Join-Path $temporary 'composition'
    foreach($source in $sources){$target=Join-Path $composition $source;[void][IO.Directory]::CreateDirectory((Split-Path $target));[IO.File]::Copy((Join-Path $RepositoryRoot $source),$target,$false)}
    foreach($unexpected in @('src/Unapproved/Unapproved.csproj','tests/Unapproved/Unapproved.csproj','Unapproved.csproj')){
        $path=Join-Path $composition $unexpected;[void][IO.Directory]::CreateDirectory((Split-Path $path));[IO.File]::WriteAllText($path,'<Project />')
        Reject {Get-PublicCiSourcePaths $composition} 'unknown project cannot be hidden by source selection' 'SF-CI-COMPOSITION'
        [IO.File]::Delete($path)
    }
    foreach($unexpected in @('src/Unapproved/payload.cs','.github/workflows/unapproved.yml')){
        $path=Join-Path $composition $unexpected;[void][IO.Directory]::CreateDirectory((Split-Path $path));[IO.File]::WriteAllText($path,'unapproved fixture')
        Reject {Get-PublicCiSourcePaths $composition} 'unknown source root or workflow fails before copy' 'SF-CI-SCOPE'
        [IO.File]::Delete($path)
    }
    $solutionPath=Join-Path $composition 'SiteFactory.slnx';$solutionBytes=[IO.File]::ReadAllBytes($solutionPath)
    foreach($mutation in @('project','override')){
        [xml]$changed=[Text.Encoding]::UTF8.GetString($solutionBytes)
        $project=$changed.SelectSingleNode('//Project')
        if($mutation -ceq 'project'){$project.SetAttribute('Path','src/Unapproved/Unapproved.csproj')}else{$project.SetAttribute('Build','false')}
        $changed.Save($solutionPath)
        Reject {Assert-PublicCiComposition $composition} 'checked-in solution cannot hide projects or override the build' 'SF-CI-COMPOSITION'
    }
    [IO.File]::WriteAllBytes($solutionPath,$solutionBytes)
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
    Check ([Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([IO.File]::ReadAllBytes($solutionPath))) -ceq [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($solutionBytes))) 'composition preserves the checked-in solution bytes'
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
        $jobs+=@{name=$platform;result='success';status='PASSED';commit=$commit;runId='synthetic';attempt='1';cleanup='Completed';sourceLink='VERIFIED';root=$root;suites=$suites}
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
    $jobs+=@{name='PostgreSql';result='success';status='PASSED';commit=$commit;runId='synthetic';attempt='1';cleanup='Completed';sourceLink='VERIFIED';campaign=$pg;campaignFiles=$files}
    Assert-PublicCiAggregate $jobs $commit 'synthetic' '1';Check $true 'complete synthetic aggregation'
    $jobs[0].attempt='2'
    Assert-PublicCiAggregate $jobs $commit 'synthetic' '2';Check $true 'mixed attempts 2/1/1 retain all proof validation'
    Assert-PublicCiAggregate $jobs $commit 'synthetic' '10';Check $true 'attempt ordering is numeric across digit lengths'
    $jobs[0].attempt='1'
    Assert-PublicCiAggregate $jobs $commit 'synthetic' '2';Check $true 'aggregation-only rerun accepts attempts 1/1/1'
    $invalidAttempts=@($null,'','0','-1','1.5','1e0','unknown','+1',' 1','1 ',"1`n",'9223372036854775808')
    foreach($attempt in $invalidAttempts){
        Reject {Assert-PublicCiAggregate $jobs $commit 'synthetic' $attempt} 'invalid current attempt' 'SF-CI-JOB'
    }
    for($index=0;$index -lt $jobs.Count;$index++){
        $remaining=@($jobs|Where-Object name -cne $jobs[$index].name)
        $orderedJobs=@($jobs[$index])+$remaining
        foreach($attempt in @($invalidAttempts)+@('3')){
            $jobs[$index].attempt=$attempt
            Reject {Assert-PublicCiAggregate $orderedJobs $commit 'synthetic' '2'} 'invalid or future job attempt' 'SF-CI-JOB'
        }
        $jobs[$index].attempt='1'
        # An older attempt must still satisfy every provenance/status check.
        foreach($mutation in @(
            @{key='commit';value=('c'*40)},@{key='runId';value='other'},
            @{key='status';value='FAILED'},@{key='cleanup';value='Failed'},@{key='sourceLink';value='UNKNOWN'},
            @{key='sourceLink';value='PENDING'},
            @{key='result';value='failure'},@{key='result';value='skipped'},
            @{key='result';value='cancelled'},@{key='result';value='unknown'}
        )){
            $saved=$jobs[$index][$mutation.key];$jobs[$index][$mutation.key]=$mutation.value
            Reject {Assert-PublicCiAggregate $orderedJobs $commit 'synthetic' '2'} ('older attempt '+$mutation.key) 'SF-CI-JOB'
            $jobs[$index][$mutation.key]=$saved
        }
        Reject {Assert-PublicCiAggregate $remaining $commit 'synthetic' '2'} 'missing job from mixed attempts' 'SF-CI-MATRIX'
        $duplicate=$jobs.Clone();$duplicate[$index]=$jobs[($index+1)%3]
        Reject {Assert-PublicCiAggregate $duplicate $commit 'synthetic' '2'} 'duplicate job from mixed attempts' 'SF-CI-MATRIX'
    }
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
