# R02: reusable R01 public-solution proof; PostgreSQL parsing/provisioning remains P04.
Set-StrictMode -Version Latest
. (Join-Path $PSScriptRoot '../../tests/PostgreSql/Public/PostgreSqlProof.ps1')

function Get-PublicCiProjects {
    $production=@('Application','Cli','Domain','Infrastructure','Infrastructure.PostgreSql','Migrator','ModuleSdk','Modules.Documentation','Runtime','Runtime.AspNetCore','Runtime.Contracts','Runtime.Hosting')
    $offline=[ordered]@{Application=716;Cli=172;Domain=65;Infrastructure=836;'Infrastructure.PostgreSql'=510;ModuleSdk=92;'Runtime.Contracts'=105;'Runtime.AspNetCore'=53;'Runtime.Hosting'=52;'Runtime.PostgreSql.Hosts'=70;Runtime=55}
    foreach($name in $production){$assembly='LogicGate.SiteFactory.'+$name;[pscustomobject]@{Assembly=$assembly;Path="src/$assembly/$assembly.csproj";Offline=0}}
    foreach($name in $offline.Keys){$assembly='LogicGate.SiteFactory.'+$name+'.Tests';[pscustomobject]@{Assembly=$assembly;Path="tests/$assembly/$assembly.csproj";Offline=$offline[$name]}}
}

function Remove-PublicCiOwnedDirectory([string]$Path) {
    $full=[IO.Path]::GetFullPath($Path)
    $parent=[IO.Path]::TrimEndingDirectorySeparator([IO.Path]::GetTempPath())
    $name=[IO.Path]::GetFileName($full)
    if([IO.Path]::GetDirectoryName($full) -cne $parent -or $name -cnotmatch '^sitefactory-(public-ci|ci-tests)-[0-9a-f]{32}$'){throw 'SF-CI-CLEANUP'}
    [void](Resolve-PublicPostgreSqlEvidencePath $parent ($name+'/ownership'))
    [IO.Directory]::Delete($full,$true)
    if([IO.Path]::Exists($full)){throw 'SF-CI-CLEANUP'}
}

function Invoke-PublicCiGit([string]$Root,[string[]]$Arguments) {
    $output=@(& git -C $Root @Arguments 2>$null)
    if($LASTEXITCODE -ne 0){throw 'SF-CI-GIT'}
    $output
}

function Initialize-PublicCiGitMetadata([string]$Checkout,[string]$Work,[string]$Repository,[string]$Commit) {
    if($Commit -cnotmatch '^[0-9a-f]{40}$' -or $Repository -cnotmatch '^[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+$'){throw 'SF-CI-PROVENANCE'}
    $head=(Invoke-PublicCiGit $Checkout @('rev-parse','HEAD')) -join ''
    if($head -cne $Commit){throw 'SF-CI-REVISION'}
    $origin=(Invoke-PublicCiGit $Checkout @('remote','get-url','origin')) -join ''
    $url='https://github.com/'+$Repository
    if($origin -cne $url -and $origin -cne ($url+'.git')){throw 'SF-CI-REPOSITORY'}
    $workRoot=[IO.Path]::GetFullPath($Work)
    $checkoutRoot=[IO.Path]::GetFullPath($Checkout)
    if($workRoot -eq $checkoutRoot -or $workRoot.StartsWith($checkoutRoot+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw 'SF-CI-ISOLATION'}
    $metadata=Join-Path $workRoot '.git'
    if([IO.Path]::Exists($metadata)){throw 'SF-CI-ISOLATION'}
    # Source Link's documented minimal metadata, NOT a new Git history:
    # https://github.com/dotnet/sourcelink/blob/main/docs/README.md#minimal-git-repository-metadata
    # Only verified public identity is written. No objects, index, credentials,
    # global/private config, hooks or links to the checkout's .git are copied.
    # The enclosing owned scratch cleanup removes these two temporary files.
    [void][IO.Directory]::CreateDirectory($metadata)
    [IO.File]::WriteAllText((Join-Path $metadata 'HEAD'),$head+"`n",[Text.UTF8Encoding]::new($false))
    [IO.File]::WriteAllText((Join-Path $metadata 'config'),('[remote "origin"]'+"`n"+'url = '+$url+"`n"),[Text.UTF8Encoding]::new($false))
}

function Get-PublicCiRoute([string[]]$Paths,[string]$Event,[bool]$PrivateRepository=$false) {
    # Classify project membership structurally; no excluded source blob is needed.
    $projects=@(Get-PublicCiProjects)
    $sourceRoots=@($projects|Where-Object Path -like 'src/*'|ForEach-Object {$_.Path.Substring(0,$_.Path.LastIndexOf('/')+1)})
    $mixed=@($Paths|Where-Object {
        ($_.EndsWith('.csproj',[StringComparison]::Ordinal) -and $projects.Path -cnotcontains $_) -or
        ($_.StartsWith('src/',[StringComparison]::Ordinal) -and !$(foreach($root in $sourceRoots){if($_.StartsWith($root,[StringComparison]::Ordinal)){$true;break}}))
    }).Count -gt 0
    if(!$PrivateRepository -and $mixed){throw 'SF-CI-PRIVATE-SOURCE'}
    if(!$mixed -and @($Paths|Where-Object {$_.StartsWith('.github/workflows/',[StringComparison]::Ordinal) -and $_ -cne '.github/workflows/ci.yml'}).Count){throw 'SF-CI-PRIVATE-WORKFLOW'}
    return (!$mixed -or $Event -ceq 'workflow_dispatch')
}

function Get-PublicCiSourcePaths([string]$Root) {
    & git -C $Root rev-parse --show-toplevel *> $null
    $paths=if($LASTEXITCODE -eq 0){@(Invoke-PublicCiGit $Root @('ls-files','--cached','--others','--exclude-standard'))}else{@(Get-ChildItem -LiteralPath $Root -Recurse -File -Force|Where-Object FullName -notmatch '[\\/](bin|obj|\.git)[\\/]'|ForEach-Object {[IO.Path]::GetRelativePath($Root,$_.FullName).Replace('\','/')})}
    $selected=[Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    $roots=@('tests/PostgreSql/Public/','tests/fixtures/public/','templates/documentation-minimal/')
    $fixed=@('SiteFactory.slnx','global.json','Directory.Build.props','Directory.Packages.props','NuGet.config','.editorconfig','.gitignore','.gitattributes','.github/workflows/ci.yml','scripts/public-ci/Invoke-PublicCi.ps1','scripts/public-ci/PublicCiProof.ps1','tests/PublicCi/Test-PublicCi.ps1')
    foreach($project in @(Get-PublicCiProjects)){
        $directory=$project.Path.Substring(0,$project.Path.LastIndexOf('/')+1)
        $projectFile=Join-Path $Root $project.Path
        $xmlText=if(Test-Path -LiteralPath $projectFile){[IO.File]::ReadAllText($projectFile)}else{(Invoke-PublicCiGit $Root @('show',('HEAD:'+$project.Path))) -join [Environment]::NewLine}
        [xml]$xml=$xmlText
        $removed=@($xml.SelectNodes('//Compile[@Remove]')|ForEach-Object {$_.GetAttribute('Remove').Replace('\','/')})
        $explicit=@($xml.SelectNodes('//Compile[@Include]')|ForEach-Object {$_.GetAttribute('Include').Replace('\','/')})
        $closed=@($xml.SelectNodes('//EnableDefaultCompileItems')|Where-Object InnerText -ceq 'false').Count -gt 0
        if($closed -and @($explicit|Where-Object {$_ -match '[*?]'}).Count){throw 'SF-CI-SCOPE'}
        foreach($remove in $removed){if($remove -match '[*?]'){throw 'SF-CI-SCOPE'}}
        foreach($path in $paths){
            if($path.StartsWith($directory,[StringComparison]::Ordinal)){
                $relative=$path.Substring($directory.Length)
                if($removed -cnotcontains $relative -and (!$closed -or !$relative.EndsWith('.cs',[StringComparison]::Ordinal) -or $explicit -ccontains $relative)){[void]$selected.Add($path)}
            }
        }
    }
    foreach($path in $paths){
        if($fixed -ccontains $path -or @($roots|Where-Object {$path.StartsWith($_,[StringComparison]::Ordinal)}).Count){[void]$selected.Add($path)}
    }
    foreach($path in $selected){
        if($path -match '(^|/)(bin|obj|\.git)(/|$)|(^|/)\.\.(/|$)|[\\\r\n*?\[\]]'){throw 'SF-CI-SCOPE'}
    }
    foreach($required in @((Get-PublicCiProjects).Path)+$fixed){if(!$selected.Contains($required)){throw 'SF-CI-SCOPE'}}
    @($selected|Sort-Object -CaseSensitive)
}

function Get-PublicCiLockedPackageMember([string]$Project,[string]$Package,[string]$Relative,[string]$Import) {
    # Callers supply their closed member list. This is not a cache-directory
    # exemption: both the member and its importer must match the locked archive.
    $directory=Split-Path $Project
    $assetsPath=Join-Path $directory 'obj/project.assets.json'
    if(!(Test-Path -LiteralPath $assetsPath)){return $null}
    try{
        $assets=[IO.File]::ReadAllText($assetsPath)|ConvertFrom-Json -AsHashtable
        $lock=[IO.File]::ReadAllText((Join-Path $directory 'packages.lock.json'))|ConvertFrom-Json -AsHashtable
        $pin=$lock.dependencies['net10.0'][$Package]
        if(!$pin -or $pin.resolved -cnotmatch '^\d+\.\d+\.\d+$'){return $null}
        $library=$assets.libraries[$Package+'/'+$pin.resolved]
        $packagePath=$Package.ToLowerInvariant()+'/'+$pin.resolved
        if(!$library -or $library.type -cne 'package' -or $library.path -cne $packagePath -or $library.sha512 -cne $pin.contentHash -or $library.files -cnotcontains $Relative -or $library.files -cnotcontains $Import){return $null}
        foreach($folder in $assets.packageFolders.Keys){
            if(![IO.Directory]::Exists((Join-Path $folder $packagePath))){continue}
            $packageRoot=Resolve-PublicPostgreSqlEvidencePath $folder $packagePath
            $payload=Resolve-PublicPostgreSqlEvidencePath $packageRoot $Relative
            $importPath=Resolve-PublicPostgreSqlEvidencePath $packageRoot $Import
            if(![IO.File]::Exists($payload) -or ![IO.File]::Exists($importPath)){continue}
            $archiveName=$Package.ToLowerInvariant()+'.'+$pin.resolved+'.nupkg'
            $hash=Resolve-PublicPostgreSqlEvidencePath $packageRoot ($archiveName+'.sha512')
            $metadataPath=Join-Path $packageRoot '.nupkg.metadata'
            if([IO.File]::GetAttributes($metadataPath) -band [IO.FileAttributes]::ReparsePoint){return $null}
            $metadata=[IO.File]::ReadAllText($metadataPath)|ConvertFrom-Json -AsHashtable
            if($metadata.contentHash -cne $pin.contentHash){return $null}
            $archivePath=Resolve-PublicPostgreSqlEvidencePath $packageRoot $archiveName
            if([Convert]::ToBase64String([Security.Cryptography.SHA512]::HashData([IO.File]::ReadAllBytes($archivePath))) -cne [IO.File]::ReadAllText($hash).Trim()){return $null}
            $archive=[IO.Compression.ZipFile]::OpenRead($archivePath)
            try{
                foreach($member in @($Relative,$Import)){
                    $entries=@($archive.Entries|Where-Object FullName -ceq $member)
                    if($entries.Count -ne 1){return $null}
                    $stream=$entries[0].Open()
                    try{$expected=[Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($stream))}finally{$stream.Dispose()}
                    if((Get-FileHash -LiteralPath (Join-Path $packageRoot $member)).Hash -cne $expected){return $null}
                }
            }finally{$archive.Dispose()}
            return @{Path=$payload;ImportPath=$importPath;Canonical=('/_/'+$packagePath+'/'+$Relative);Checksum=(Get-FileHash -LiteralPath $payload).Hash}
        }
    }catch{return $null}
    return $null
}

function Test-PublicCiLockedTestInput([string]$Project,[string]$Kind,$Item) {
    # Keep the evaluated-input exemption closed to the four runner payloads.
    # Compile and EmbeddedResource are never admitted by this exemption.
    $runner=@{Kind='None';Package='xunit.runner.visualstudio';Import='build/net8.0/xunit.runner.visualstudio.props';Files=@('build/net8.0/xunit.abstractions.dll','build/net8.0/xunit.runner.visualstudio.testadapter.dll')}
    $testHost=@{Kind='Content';Package='Microsoft.TestPlatform.TestHost';Import='build/net8.0/Microsoft.TestPlatform.TestHost.props';Files=@('build/net8.0/x64/testhost.exe','build/net8.0/x64/testhost.dll')}
    foreach($spec in @($runner,$testHost)|Where-Object Kind -ceq $Kind){
        foreach($relative in $spec.Files){
            $member=Get-PublicCiLockedPackageMember $Project $spec.Package $relative $spec.Import
            if($member -and $Item.FullPath -ceq $member.Path -and $Item.DefiningProjectFullPath -ceq $member.ImportPath){return $true}
        }
    }
    return $false
}

function Assert-PublicCiComposition([string]$Root,[switch]$Evaluate) {
    $projects=@(Get-PublicCiProjects)
    $actual=@(Get-ChildItem (Join-Path $Root 'src'),(Join-Path $Root 'tests') -Recurse -Filter '*.csproj' -File|Where-Object FullName -notmatch '[\\/](bin|obj)[\\/]')
    $expected=@($projects.Path|Sort-Object -CaseSensitive)
    $observed=@($actual|ForEach-Object {[IO.Path]::GetRelativePath($Root,$_.FullName).Replace('\','/')}|Sort-Object -CaseSensitive)
    if(@(Compare-Object $expected $observed -CaseSensitive).Count){throw 'SF-CI-COMPOSITION'}
    $graph=@{}
    $sources=if($Evaluate){[Collections.Generic.HashSet[string]]::new([string[]]@(Get-PublicCiSourcePaths $Root),[StringComparer]::Ordinal)}else{$null}
    foreach($project in $projects){
        $path=Join-Path $Root $project.Path
        [xml]$xml=[IO.File]::ReadAllText($path)
        $edges=@($xml.SelectNodes('//ProjectReference')|ForEach-Object {
            $target=[IO.Path]::GetFullPath((Join-Path (Split-Path $path) $_.GetAttribute('Include')))
            $relative=[IO.Path]::GetRelativePath($Root,$target).Replace('\','/')
            if($expected -cnotcontains $relative){throw 'SF-CI-REFERENCE'}
            $relative
        })
        $graph[$project.Path]=$edges
        if($Evaluate){
            $output=& dotnet msbuild $path -property:Configuration=Release -getItem:Compile,ProjectReference,EmbeddedResource,None,Content
            if($LASTEXITCODE -ne 0){throw 'SF-CI-MSBUILD'}
            $items=($output -join [Environment]::NewLine)|ConvertFrom-Json
            foreach($kind in @('Compile','ProjectReference','EmbeddedResource','None','Content')){
                foreach($item in $items.Items.$kind){
                    $full=[IO.Path]::GetFullPath($item.FullPath)
                    $comparison=if($IsWindows){[StringComparison]::OrdinalIgnoreCase}else{[StringComparison]::Ordinal}
                    $relative=[IO.Path]::GetRelativePath($Root,$full).Replace('\','/')
                    $generated=$full.StartsWith((Split-Path $path)+[IO.Path]::DirectorySeparatorChar+'obj'+[IO.Path]::DirectorySeparatorChar,$comparison)
                    if(!$full.StartsWith($Root+[IO.Path]::DirectorySeparatorChar,$comparison) -or (!$sources.Contains($relative) -and !$generated)){
                        if($project.Offline -gt 0 -and (Test-PublicCiLockedTestInput $path $kind $item)){continue}
                        throw 'SF-CI-RESOURCE'
                    }
                }
            }
        }
    }
    $visited=[Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    $active=[Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    function Visit-PublicCiProject([string]$Path) {
        if($active.Contains($Path)){throw 'SF-CI-CYCLE'}
        if($visited.Contains($Path)){return}
        [void]$active.Add($Path)
        foreach($edge in $graph[$Path]){Visit-PublicCiProject $edge}
        [void]$active.Remove($Path);[void]$visited.Add($Path)
    }
    foreach($project in $projects){Visit-PublicCiProject $project.Path}
}

function Get-PublicCiCanonicalName([string]$Name) {
    $builder=[Text.StringBuilder]::new()
    foreach($character in $Name.ToCharArray()){
        if([char]::IsSurrogate($character)){[void]$builder.Append('\u').Append(([int]$character).ToString('x4'))}else{[void]$builder.Append($character)}
    }
    $builder.ToString()
}

function Get-PublicCiCaseHash([string]$Name) {
    $canonical=Get-PublicCiCanonicalName $Name
    [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes($canonical))).ToLowerInvariant()
}

function Get-PublicCiArchivedCaseName($Row) {
    $id=Convert-PublicPostgreSqlGuid $Row.id
    if($Row.caseHash -cnotmatch '^[0-9a-f]{64}$'){throw 'SF-CI-CASE-IDENTITY'}
    return $Row.class+'.'+$Row.method+'(case-sha256:'+$Row.caseHash+';id:'+$id+')'
}

function Get-PublicCiOfflineFailureDiagnostic([string]$TrxPath,[string[]]$Expected,[string]$Assembly,[Nullable[int]]$ExitCode) {
    $project=@(Get-PublicCiProjects | Where-Object {$_.Offline -gt 0 -and $_.Assembly -ceq $Assembly})
    if($project.Count -ne 1 -or $Expected.Count -ne $project[0].Offline){throw 'SF-CI-INVENTORY'}
    # Observation only: never a complete/successful TRX proof. Missing values
    # stay unknown. No XML, streams, exception text or attachments are exported.
    $diagnostic=[ordered]@{schemaVersion=1;kind='OfflineFailure';suite=$project[0].Assembly;expectedCount=$project[0].Offline;processExitCode=$ExitCode;status='FAILED';completeProof=$false;trx='Missing';counters=$null;observedResults=$null;outcomes=$null;failedCases=@();unidentifiedFailedResults=$null}
    if(![IO.Path]::Exists($TrxPath)){return $diagnostic}
    $diagnostic.trx='Invalid'
    try{
        if(![IO.File]::Exists($TrxPath) -or ([IO.File]::GetAttributes($TrxPath) -band [IO.FileAttributes]::ReparsePoint) -or [IO.FileInfo]::new($TrxPath).Length -gt 4194304){return $diagnostic}
        $document=Read-PublicPostgreSqlTrxBytes ([IO.File]::ReadAllBytes($TrxPath))
    }catch{return $diagnostic}
    if($document.DocumentElement.LocalName -cne 'TestRun' -or $document.DocumentElement.NamespaceURI -cne 'http://microsoft.com/schemas/VisualStudio/TeamTest/2010'){return $diagnostic}
    $diagnostic.trx='Present'
    $counterNames=@('total','executed','passed','failed','error','timeout','aborted','inconclusive','passedButRunAborted','notRunnable','notExecuted','disconnected','warning','completed','inProgress','pending')
    $counter=@($document.SelectNodes("/*[local-name()='TestRun']/*[local-name()='ResultSummary']/*[local-name()='Counters']"))
    if($counter.Count -eq 1){
        $diagnostic.counters=[ordered]@{}
        foreach($attribute in $counter[0].Attributes){
            $value=0
            if($counterNames -ccontains $attribute.Name -and [int]::TryParse($attribute.Value,[ref]$value) -and $value -ge 0){$diagnostic.counters[$attribute.Name]=$value}
        }
    }
    $authorized=[Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    $pattern='\A'+[regex]::Escape($Assembly)+'\.[A-Za-z0-9_.+]+\.[A-Za-z0-9_]+\z'
    foreach($name in $Expected){$method=$name.Split('(')[0];if($method -cmatch $pattern){[void]$authorized.Add($method)}}
    $definitions=[Collections.Generic.Dictionary[string,object]]::new([StringComparer]::Ordinal)
    foreach($definition in $document.SelectNodes("/*[local-name()='TestRun']/*[local-name()='TestDefinitions']/*[local-name()='UnitTest']")){
        try{$id=Convert-PublicPostgreSqlGuid $definition.GetAttribute('id')}catch{continue}
        if(!$definitions.TryAdd($id,$definition)){$definitions[$id]=$null}
    }
    $results=@($document.SelectNodes("/*[local-name()='TestRun']/*[local-name()='Results']/*[local-name()='UnitTestResult']"))
    $diagnostic.observedResults=$results.Count;$diagnostic.outcomes=[ordered]@{};$failed=0
    $outcomes=@('Passed','Failed','Error','Timeout','Aborted','Inconclusive','NotRunnable','NotExecuted','Disconnected','Warning','Completed','InProgress','Pending','PassedButRunAborted')
    $ids=[Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    foreach($result in $results){
        $outcome=$result.GetAttribute('outcome');$bucket=if($outcomes -ccontains $outcome){$outcome}else{'Unrecognized'}
        if(!$diagnostic.outcomes.Contains($bucket)){$diagnostic.outcomes[$bucket]=0};$diagnostic.outcomes[$bucket]++
        if($outcome -cne 'Failed'){continue};$failed++
        try{
            $id=Convert-PublicPostgreSqlGuid $result.GetAttribute('testId')
            $execution=Convert-PublicPostgreSqlGuid $result.GetAttribute('executionId')
            if(!$ids.Add($id) -or !$definitions.ContainsKey($id) -or $null -eq $definitions[$id]){continue}
            $method=@($definitions[$id].SelectNodes("*[local-name()='TestMethod']"))
            $definitionExecution=@($definitions[$id].SelectNodes("*[local-name()='Execution']"))
            if($method.Count -ne 1 -or $definitionExecution.Count -ne 1 -or $definitionExecution[0].GetAttribute('id') -cne $execution -or [IO.Path]::GetFileName($method[0].GetAttribute('codeBase').Replace('\','/')) -ine ($Assembly+'.dll')){continue}
            $qualified=$method[0].GetAttribute('className')+'.'+$method[0].GetAttribute('name')
            if(!$authorized.Contains($qualified) -or $result.GetAttribute('testName').Split('(')[0] -cne $qualified){continue}
            $diagnostic.failedCases+=@{test=$qualified;id=$id}
        }catch{continue}
    }
    $diagnostic.unidentifiedFailedResults=$failed-$diagnostic.failedCases.Count
    return $diagnostic
}

function Assert-PublicCiOfflineTrx([byte[]]$Bytes,[string[]]$Expected,[string]$Assembly,[DateTimeOffset]$Start,[DateTimeOffset]$Finish,[switch]$Archived) {
    # Reuse P04's secure bounded XML parser, not a second PostgreSQL parser.
    $document=Read-PublicPostgreSqlTrxBytes $Bytes
    $namespaces=@((Get-PublicCiProjects|Where-Object Offline -gt 0).Assembly)
    if($namespaces -cnotcontains $Assembly){throw 'SF-CI-IDENTITY'}
    $results=@($document.SelectNodes("//*[local-name()='UnitTestResult']"))
    $definitions=@($document.SelectNodes("//*[local-name()='UnitTest']"))
    $entries=@($document.SelectNodes("//*[local-name()='TestEntry']"))
    $counter=@($document.SelectNodes("//*[local-name()='Counters']"))
    $summary=@($document.SelectNodes("//*[local-name()='ResultSummary']"))
    if($Expected.Count -eq 0 -or $results.Count -ne $Expected.Count -or $definitions.Count -ne $Expected.Count -or $entries.Count -ne $Expected.Count -or $counter.Count -ne 1 -or $summary.Count -ne 1 -or $summary[0].GetAttribute('outcome') -cne 'Completed'){throw 'SF-CI-TRX'}
    $actual=@($results|ForEach-Object {Get-PublicCiCanonicalName $_.GetAttribute('testName')}|Sort-Object -CaseSensitive)
    $names=@($Expected|ForEach-Object {Get-PublicCiCanonicalName $_}|Sort-Object -CaseSensitive)
    if(@(Compare-Object $names $actual -CaseSensitive).Count){throw 'SF-CI-INVENTORY'}
    $counterNames=@('total','executed','passed','failed','error','timeout','aborted','inconclusive','passedButRunAborted','notRunnable','notExecuted','disconnected','warning','completed','inProgress','pending')
    foreach($attribute in $counter[0].Attributes){
        $value=0
        if($attribute.Name -cnotin $counterNames -or ![int]::TryParse($attribute.Value,[ref]$value) -or $value -ne $(if($attribute.Name -cin @('total','executed','passed')){$Expected.Count}else{0})){throw 'SF-CI-COUNTERS'}
    }
    foreach($name in @('total','executed','passed')){if($counter[0].GetAttribute($name) -cne [string]$Expected.Count){throw 'SF-CI-COUNTERS'}}
    $ids=[Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    $executions=[Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    $definitionMap=[Collections.Generic.Dictionary[string,object]]::new([StringComparer]::Ordinal)
    $entryMap=[Collections.Generic.Dictionary[string,object]]::new([StringComparer]::Ordinal)
    foreach($definition in $definitions){if(!$definitionMap.TryAdd($definition.GetAttribute('id'),$definition)){throw 'SF-CI-IDENTITY'}}
    foreach($entry in $entries){if(!$entryMap.TryAdd($entry.GetAttribute('testId'),$entry)){throw 'SF-CI-IDENTITY'}}
    $rows=@(foreach($result in $results){
        $id=Convert-PublicPostgreSqlGuid $result.GetAttribute('testId')
        $execution=Convert-PublicPostgreSqlGuid $result.GetAttribute('executionId')
        if(!$ids.Add($id) -or !$executions.Add($execution) -or !$definitionMap.ContainsKey($id) -or !$entryMap.ContainsKey($id) -or $entryMap[$id].GetAttribute('executionId') -cne $execution -or $result.GetAttribute('outcome') -cne 'Passed'){throw 'SF-CI-RESULT'}
        $method=@($definitionMap[$id].SelectNodes("*[local-name()='TestMethod']"))
        $definitionExecution=@($definitionMap[$id].SelectNodes("*[local-name()='Execution']"))
        if($method.Count -ne 1 -or $definitionExecution.Count -ne 1 -or $definitionExecution[0].GetAttribute('id') -cne $execution -or
            $method[0].GetAttribute('className') -cnotmatch '^LogicGate\.SiteFactory\.[A-Za-z0-9_.+]+$' -or !@($namespaces|Where-Object {$method[0].GetAttribute('className').StartsWith($_+'.',[StringComparison]::Ordinal)}).Count -or $method[0].GetAttribute('name') -cnotmatch '^[A-Za-z0-9_]+$' -or
            [IO.Path]::GetFileName($method[0].GetAttribute('codeBase').Replace('\','/')) -ine ($Assembly+'.dll')){throw 'SF-CI-IDENTITY'}
        $canonicalName=Get-PublicCiCanonicalName $result.GetAttribute('testName')
        $qualifiedMethod=$method[0].GetAttribute('className')+'.'+$method[0].GetAttribute('name')
        if($canonicalName.Split('(')[0] -cne $qualifiedMethod){throw 'SF-CI-IDENTITY'}
        if($Archived){
            # Archived names contain a digest, never the original inline data.
            # Do not hash this transport name again when revalidating evidence.
            $pattern='\A'+[regex]::Escape($qualifiedMethod)+'\(case-sha256:(?<digest>[0-9a-f]{64});id:(?<identity>[0-9a-f-]{36})\)\z'
            if($canonicalName -cnotmatch $pattern -or $Matches.identity -cne $id){throw 'SF-CI-CASE-IDENTITY'}
            $caseHash=$Matches.digest
        }else{$caseHash=Get-PublicCiCaseHash $canonicalName}
        $startTime=Convert-PublicPostgreSqlTime $result.GetAttribute('startTime')
        $endTime=Convert-PublicPostgreSqlTime $result.GetAttribute('endTime')
        $duration=Convert-PublicPostgreSqlDuration $result.GetAttribute('duration')
        if($startTime -lt $Start -or $endTime -gt $Finish -or $endTime -lt $startTime){throw 'SF-CI-PROVENANCE'}
        [ordered]@{id=$id;execution=$execution;caseHash=$caseHash;class=$method[0].GetAttribute('className');method=$method[0].GetAttribute('name');start=$startTime.ToString('O');end=$endTime.ToString('O');duration=$result.GetAttribute('duration');outcome='Passed'}
    })
    return @{Document=$document;Rows=$rows;Count=$results.Count}
}

function Save-PublicCiOfflineTrx($Validated,[string]$Path,[string]$Assembly) {
    # Complete per-case proof, not raw inline arguments, paths, stdout or provider text.
    $xml=[Xml.XmlDocument]::new()
    $ns='http://microsoft.com/schemas/VisualStudio/TeamTest/2010'
    $root=$xml.CreateElement('TestRun',$ns);[void]$xml.AppendChild($root)
    $root.SetAttribute('id',[Guid]::NewGuid().ToString('D'))
    $results=$xml.CreateElement('Results',$ns);[void]$root.AppendChild($results)
    $definitions=$xml.CreateElement('TestDefinitions',$ns);[void]$root.AppendChild($definitions)
    $entries=$xml.CreateElement('TestEntries',$ns);[void]$root.AppendChild($entries)
    foreach($row in $Validated.Rows){
        $result=$xml.CreateElement('UnitTestResult',$ns)
        foreach($pair in @(@('testId',$row.id),@('executionId',$row.execution),@('testName',(Get-PublicCiArchivedCaseName $row)),@('outcome','Passed'),@('startTime',$row.start),@('endTime',$row.end),@('duration',$row.duration))){$result.SetAttribute($pair[0],$pair[1])}
        [void]$results.AppendChild($result)
        $unit=$xml.CreateElement('UnitTest',$ns);$unit.SetAttribute('id',$row.id)
        $method=$xml.CreateElement('TestMethod',$ns);$method.SetAttribute('className',$row.class);$method.SetAttribute('name',$row.method);$method.SetAttribute('codeBase',($Assembly+'.dll'));[void]$unit.AppendChild($method)
        $execution=$xml.CreateElement('Execution',$ns);$execution.SetAttribute('id',$row.execution);[void]$unit.AppendChild($execution);[void]$definitions.AppendChild($unit)
        $entry=$xml.CreateElement('TestEntry',$ns);$entry.SetAttribute('testId',$row.id);$entry.SetAttribute('executionId',$row.execution);[void]$entries.AppendChild($entry)
    }
    $summary=$xml.CreateElement('ResultSummary',$ns);$summary.SetAttribute('outcome','Completed')
    $counters=$xml.CreateElement('Counters',$ns)
    foreach($field in @('total','executed','passed','failed','error','timeout','aborted','notExecuted')){$counters.SetAttribute($field,[string]$(if($field -cin @('total','executed','passed')){$Validated.Count}else{0}))}
    [void]$summary.AppendChild($counters);[void]$root.AppendChild($summary)
    Assert-PublicPostgreSqlNoSensitiveContent $xml.OuterXml -Document $xml
    if(Test-Path -LiteralPath $Path){throw 'SF-CI-PROVENANCE'}
    [void][IO.Directory]::CreateDirectory((Split-Path $Path));$xml.Save($Path)
}

function Read-PublicCiSourceLink($Reader,[string]$Repository,[string]$Commit) {
    $records=@(foreach($handle in $Reader.CustomDebugInformation){
        $info=$Reader.GetCustomDebugInformation($handle)
        if($Reader.GetGuid($info.Kind) -eq [guid]'CC110556-A091-4D38-9FEC-25AB9A351A6A'){$info}
    })
    if(!$Repository){if($records.Count){throw 'SF-CI-PRIVATE-SOURCELINK'};return $null}
    if(!$records.Count){throw 'SF-CI-SOURCELINK-RECORD'}
    if($records.Count -ne 1){throw 'SF-CI-SOURCELINK-DUPLICATE'}
    if($records[0].Parent.Kind -ne [Reflection.Metadata.HandleKind]::ModuleDefinition){throw 'SF-CI-SOURCELINK-RECORD'}
    try{
        $json=[Text.Json.JsonDocument]::Parse([Text.Encoding]::UTF8.GetString($Reader.GetBlobBytes($records[0].Value)))
    }catch{throw 'SF-CI-SOURCELINK-FORMAT'}
    try{
        if($json.RootElement.ValueKind -ne [Text.Json.JsonValueKind]::Object){throw 'SF-CI-SOURCELINK-FORMAT'}
        $properties=@($json.RootElement.EnumerateObject())
        if($properties.Count -ne 1 -or $properties[0].Name -cne 'documents' -or $properties[0].Value.ValueKind -ne [Text.Json.JsonValueKind]::Object){throw 'SF-CI-SOURCELINK-FORMAT'}
        $entries=@($properties[0].Value.EnumerateObject())
        if(!$entries.Count){throw 'SF-CI-SOURCELINK-EMPTY'}
        $keys=[Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
        $mappings=@(foreach($entry in $entries){
            if(!$keys.Add($entry.Name)){throw 'SF-CI-SOURCELINK-AMBIGUOUS'}
            if($entry.Value.ValueKind -ne [Text.Json.JsonValueKind]::String){throw 'SF-CI-SOURCELINK-FORMAT'}
            $key=$entry.Name;$url=$entry.Value.GetString()
            if(!$key.StartsWith('/_/',[StringComparison]::Ordinal) -or $key -cmatch '(^|/)\.{1,2}(/|$)|[\\\r\n?]' -or $key.Contains('//') -or $key -cmatch '\*.+|\*.*\*'){throw 'SF-CI-SOURCELINK-PATH'}
            if($url -cnotmatch '^https://raw\.githubusercontent\.com/([A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+)/([^/]+)/(.+)$' -or $Matches[1] -cne $Repository){throw 'SF-CI-SOURCELINK-REPOSITORY'}
            if($Matches[2] -cne $Commit){throw 'SF-CI-SOURCELINK-REVISION'}
            $path=$Matches[3]
            if($path -cmatch '(^|/)\.{1,2}(/|$)|[\\\r\n?#%:]' -or $path.StartsWith('/') -or $path.Contains('//') -or $path -cmatch '\*.+|\*.*\*' -or $key.EndsWith('*') -ne $path.EndsWith('*')){throw 'SF-CI-SOURCELINK-PATH'}
            @{key=$key;url=$url;wildcard=$key.EndsWith('*')}
        })
        return ,$mappings
    }finally{$json.Dispose()}
}

function Resolve-PublicCiSourceLink([object[]]$Mappings,[string]$Document,[string]$Repository,[string]$Commit) {
    $matches=@(foreach($mapping in $Mappings){
        if($mapping.wildcard){
            $prefix=$mapping.key.Substring(0,$mapping.key.Length-1)
            if($Document.StartsWith($prefix,[StringComparison]::Ordinal)){$mapping.url.Substring(0,$mapping.url.Length-1)+$Document.Substring($prefix.Length)}
        }elseif($mapping.key -ceq $Document){$mapping.url}
    })
    if(!$matches.Count){throw 'SF-CI-SOURCELINK-UNCOVERED'}
    if($matches.Count -ne 1){throw 'SF-CI-SOURCELINK-AMBIGUOUS'}
    $prefix="https://raw.githubusercontent.com/$Repository/$Commit/"
    if($matches[0] -cne ($prefix+$Document.Substring(3))){throw 'SF-CI-SOURCELINK-PATH'}
    # Resolve the public URL to bytes in the verified public source copy. No
    # network/HTTP-200 assertion or local absolute PDB path is trusted here.
    return $matches[0].Substring($prefix.Length)
}

function Assert-PublicCiSymbols([string]$Root,[string]$Repository,[string]$Commit) {
    $projects=@(Get-PublicCiProjects)
    $sources=[Collections.Generic.HashSet[string]]::new([string[]]@(Get-PublicCiSourcePaths $Root),[StringComparer]::Ordinal)
    $generatedRoots=@($projects.Path|ForEach-Object {$_.Substring(0,$_.LastIndexOf('/')+1)+'obj/'})
    foreach($project in $projects){
        $pdb=Join-Path (Split-Path (Join-Path $Root $project.Path)) ('bin/Release/net10.0/'+$project.Assembly+'.pdb')
        if(![IO.File]::Exists($pdb)){throw 'SF-CI-PDB-MISSING'}
    }
    $pdbs=@(Get-ChildItem (Join-Path $Root 'src'),(Join-Path $Root 'tests') -Recurse -File -Filter '*.pdb'|Where-Object FullName -match '[\\/](bin|obj)[\\/]Release[\\/]')
    $links=0;$packageSources=@{}
    foreach($file in $pdbs){
        $owners=@($projects|Where-Object Assembly -ceq $file.BaseName)
        if($owners.Count -ne 1){throw 'SF-CI-PDB-ORIGIN'}
        $stream=[IO.File]::OpenRead($file.FullName)
        try{
            $provider=[Reflection.Metadata.MetadataReaderProvider]::FromPortablePdbStream($stream)
            try{
                $reader=$provider.GetMetadataReader()
                $mappings=Read-PublicCiSourceLink $reader $Repository $Commit
                if($Repository){$links++}
                foreach($handle in $reader.Documents){
                    $document=$reader.GetDocument($handle)
                    $name=$reader.GetString($document.Name)
                    # Git discovery makes the repository /_/ and the NuGet
                    # source root /_1/. Only the locked xUnit member below may
                    # use the latter; it never becomes a repository document.
                    $relative=if($name.StartsWith('/_/',[StringComparison]::Ordinal)){$name.Substring(3)}else{''}
                    if($relative -cmatch '(^|/)\.{1,2}(/|$)|[\\\r\n]' -or $relative.Contains('//')){throw 'SF-CI-PDB-PATH'}
                    if($sources.Contains($relative)){
                        if($Repository){$relative=Resolve-PublicCiSourceLink $mappings $name $Repository $Commit}
                        if($reader.GetGuid($document.HashAlgorithm) -ne [guid]'8829d00f-11b8-4213-878b-770e8597ac16' -or
                            (Get-FileHash -LiteralPath (Join-Path $Root $relative)).Hash -cne [Convert]::ToHexString($reader.GetBlobBytes($document.Hash))){throw 'SF-CI-PDB-CHECKSUM'}
                        continue
                    }
                    if($relative -and @($generatedRoots|Where-Object {$relative.StartsWith($_,[StringComparison]::Ordinal)}).Count){continue}
                    # xUnit attaches this precise locked source in BeforeCompile,
                    # after MSBuild's evaluation-only item inventory. Its NuGet
                    # canonical document is neither a repository file nor obj/.
                    if($owners[0].Offline -gt 0){
                        $owner=$owners[0]
                        if(!$packageSources.ContainsKey($owner.Assembly)){
                            $packageSources[$owner.Assembly]=Get-PublicCiLockedPackageMember (Join-Path $Root $owner.Path) 'xunit.v3.core.mtp-v1' '_content/DefaultRunnerReporters.cs' 'buildTransitive/xunit.v3.core.mtp-v1.targets'
                        }
                        $member=$packageSources[$owner.Assembly]
                        if($member -and ($name -ceq $member.Canonical -or ($Repository -and $name -ceq ('/_1/'+$member.Canonical.Substring(3))))){
                            if($reader.GetGuid($document.HashAlgorithm) -ne [guid]'8829d00f-11b8-4213-878b-770e8597ac16' -or $member.Checksum -cne [Convert]::ToHexString($reader.GetBlobBytes($document.Hash))){throw 'SF-CI-PDB-CHECKSUM'}
                            continue
                        }
                    }
                    throw 'SF-CI-PDB-PATH'
                }
            }finally{$provider.Dispose()}
        }finally{$stream.Dispose()}
    }
    if($Repository -and $links -ne $pdbs.Count){throw 'SF-CI-SOURCELINK'}
    return @{pdbCount=$pdbs.Count;sourceLink=$(if($Repository){'VERIFIED'}else{'PENDING'});embeddedRecords=$links}
}

function Assert-PublicCiBinaries([string]$Root,[string]$Repository,[string]$Commit) {
    $assemblies=@((Get-PublicCiProjects).Assembly)
    foreach($file in @(Get-ChildItem (Join-Path $Root 'src'),(Join-Path $Root 'tests') -Recurse -File -Filter '*.dll'|Where-Object FullName -match '[\\/]bin[\\/]Release[\\/]')){
        if($file.Name -like 'LogicGate.SiteFactory.*' -and $assemblies -cnotcontains $file.BaseName){throw 'SF-CI-BINARY'}
        $stream=[IO.File]::OpenRead($file.FullName)
        try{
            $pe=[Reflection.PortableExecutable.PEReader]::new($stream)
            try{
                if(!$pe.HasMetadata){continue}
                $reader=[Reflection.Metadata.PEReaderExtensions]::GetMetadataReader($pe)
                foreach($handle in $reader.AssemblyReferences){$reference=$reader.GetString($reader.GetAssemblyReference($handle).Name);if($reference -like 'LogicGate.SiteFactory.*' -and $assemblies -cnotcontains $reference){throw 'SF-CI-BINARY-REFERENCE'}}
                if($file.Name -notlike 'LogicGate.SiteFactory.*'){continue}
                foreach($handle in $reader.GetAssemblyDefinition().GetCustomAttributes()){
                    $attribute=$reader.GetCustomAttribute($handle)
                    if($attribute.Constructor.Kind.ToString() -cne 'MemberReference'){continue}
                    $member=$reader.GetMemberReference([Reflection.Metadata.MemberReferenceHandle]$attribute.Constructor)
                    if($member.Parent.Kind.ToString() -cne 'TypeReference'){continue}
                    $type=$reader.GetTypeReference([Reflection.Metadata.TypeReferenceHandle]$member.Parent)
                    $name=$reader.GetString($type.Name)
                    if($name -cnotin @('AssemblyMetadataAttribute','AssemblyInformationalVersionAttribute')){continue}
                    $blob=$reader.GetBlobReader($attribute.Value)
                    if($blob.ReadUInt16() -ne 1){throw 'SF-CI-BINARY'}
                    $value=$blob.ReadSerializedString()
                    if($name -ceq 'AssemblyMetadataAttribute'){
                        $metadata=$blob.ReadSerializedString()
                        if($value -ceq 'RepositoryUrl' -and $metadata -and (!$Repository -or ($metadata -creplace '\.git$','') -cne ('https://github.com/'+$Repository))){throw 'SF-CI-REPOSITORY'}
                    }elseif(!$Repository -and $value -match '[0-9a-f]{40}') {throw 'SF-CI-REVISION'}
                }
            }finally{$pe.Dispose()}
        }finally{$stream.Dispose()}
    }
}

function Assert-PublicCiPostgreSql([string]$Root,[string]$Commit) {
    $manifest=[IO.File]::ReadAllText((Join-Path $Root 'manifest.json'))|ConvertFrom-Json -AsHashtable
    $partition=Read-PublicPostgreSqlPartition (Join-Path $PSScriptRoot '../../tests/PostgreSql/Public/shards.json')
    if($manifest.status -cne 'PASSED' -or !$manifest.databaseProof -or $manifest.provenance -cne 'RealPostgreSql' -or $manifest.commit -cne $Commit -or $manifest.cleanup -cne 'Completed' -or !$manifest.observationsComplete -or $manifest.postgreSql -ne 180006 -or $manifest.imageDigest -cne 'sha256:3725f4e2499eef5134592b3b4ab79a543ed7f8e533b05b5b637af926630f6650'){throw 'SF-CI-PG'}
    foreach($key in @('database','container','secrets','preexisting')){if($manifest.resourceCleanup[$key] -cne 'Completed'){throw 'SF-CI-CLEANUP'}}
    if(@(Get-ChildItem (Join-Path $Root 'native') -Filter '*.trx' -File).Count -ne 5){throw 'SF-CI-TRX'}
    if(!(Assert-PublicPostgreSqlExact @($manifest.shards.shard) @($partition.shards.name))){throw 'SF-CI-SHARDS'}
    foreach($shard in $partition.shards){
        $record=@($manifest.shards|Where-Object shard -ceq $shard.name)[0]
        if($record.status -cne 'PASSED' -or $record.proof -cne ('proof/'+$shard.name+'.json')){throw 'SF-CI-PG'}
        $proof=[IO.File]::ReadAllText((Join-Path $Root $record.proof))|ConvertFrom-Json -AsHashtable
        if($proof.status -cne 'PASSED' -or $proof.campaignId -cne $manifest.campaignId -or $proof.provenance -cne 'RealPostgreSql' -or $proof.completeness -cne 'Complete' -or $proof.discovery -cne 'Passed' -or $proof.incomingCleanup -cne 'Completed' -or $proof.processCleanup -cne 'Completed' -or $proof.processStatus -cne 'Completed' -or $proof.processExitCode -ne 0 -or $proof.processDurationMs -gt 360000 -or $proof.native -cne ('native/'+$shard.name+'.trx')){throw 'SF-CI-PG-PROOF'}
        if(!(Assert-PublicPostgreSqlExact @($proof.expectedTests) @($shard.expectedTests))){throw 'SF-CI-INVENTORY'}
        $bytes=[IO.File]::ReadAllBytes((Join-Path $Root $proof.native))
        $document=Read-PublicPostgreSqlTrxBytes $bytes
        $times=$document.SelectSingleNode("//*[local-name()='Times']")
        $invocation=@{Shard=$shard;StartedUtc=(Convert-PublicPostgreSqlTime $times.GetAttribute('start'))}
        $validated=Assert-PublicPostgreSqlTrx $document $invocation (Convert-PublicPostgreSqlTime $times.GetAttribute('finish'))
        Assert-PublicPostgreSqlNoSensitiveContent $document.OuterXml -Document $document
        if($validated.Results.Count -ne $shard.expectedCount){throw 'SF-CI-INVENTORY'}
    }
    # File-only transport omits empty directories; completed cleanup is checked above.
    $incoming=Resolve-PublicPostgreSqlEvidencePath $Root 'incoming'
    if([IO.Path]::Exists($incoming) -and (![IO.Directory]::Exists($incoming) -or
        @(Get-ChildItem -LiteralPath $incoming -Force).Count)){throw 'SF-CI-INCOMING'}
    # P04 already validates each request, pair of Activity IDs and every provider lifecycle.
    $observations=[IO.File]::ReadAllText((Join-Path $Root 'observations/p03.json'))|ConvertFrom-Json -AsHashtable
    if($observations.campaignId -cne $manifest.campaignId -or $observations.workers -ne 32 -or $observations.requestsPerWorker -ne 24 -or $observations.completeness -cne 'Complete' -or $observations.http200 -ne 768 -or $observations.http503 -ne 0 -or $observations.http.Count -ne 768 -or $observations.providerEvents.Count -ne 12288 -or $observations.lifecycle.Count -ne 3 -or $observations.providerStatistics.remainingRequestCorrelatedConnections -ne 0 -or $observations.providerStatistics.droppedEvents -ne 0){throw 'SF-CI-OBSERVATIONS'}
    $requests=[Collections.Generic.HashSet[long]]::new();$activities=[Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    $routes=@{'/'=0;'/health'=0;'/health/ready'=0};$providers=@{}
    foreach($event in $observations.providerEvents){
        if(!$providers.ContainsKey($event.requestId)){$providers[$event.requestId]=[Collections.Generic.List[object]]::new()}
        $providers[$event.requestId].Add($event)
    }
    $sequence=@(1000,1001,2000,2001,2000,2001,2000,2001,2000,2001,2000,2001,2000,2001,1003,1004)
    foreach($request in $observations.http){
        if($request.status -ne 200 -or $request.requestId -lt 0 -or $request.requestId -ge 768 -or !$requests.Add($request.requestId) -or !$routes.ContainsKey($request.route) -or
            $request.traceId -cnotmatch '^[0-9a-f]{32}$' -or $request.spanId -cnotmatch '^[0-9a-f]{16}$' -or !$activities.Add($request.traceId+'/'+$request.spanId) -or
            $request.readinessEvents.Count -ne 2 -or !$providers.ContainsKey($request.requestId)){throw 'SF-CI-OBSERVATIONS'}
        $routes[$request.route]++
        $refresh=@($request.readinessEvents|Where-Object name -ceq 'sitefactory.readiness.refresh.completed')
        $decision=@($request.readinessEvents|Where-Object name -ceq 'sitefactory.readiness.http.decision')
        if($refresh.Count -ne 1 -or $decision.Count -ne 1 -or $refresh[0].tags.outcome -cne 'READY' -or $decision[0].tags.outcome -cne 'ACCEPTED' -or
            @($request.readinessEvents|Where-Object {$_.traceId -cne $request.traceId -or $_.spanId -cne $request.spanId}).Count){throw 'SF-CI-OBSERVATIONS'}
        $events=$providers[$request.requestId]
        if($events.Count -ne 16){throw 'SF-CI-OBSERVATIONS'}
        for($i=0;$i -lt 16;$i++){if($events[$i].eventId -ne $sequence[$i] -or $events[$i].exceptionKind -cne 'NONE'){throw 'SF-CI-OBSERVATIONS'}}
    }
    if(@($routes.Values|Where-Object {$_ -ne 256}).Count -or ($observations.lifecycle.kind -join ',') -cne 'APPLICATION_STARTED,APPLICATION_STOPPING,APPLICATION_STOPPED'){throw 'SF-CI-OBSERVATIONS'}
    return $manifest.campaignId
}

function Assert-PublicCiAggregate([object[]]$Jobs,[string]$Commit,[string]$RunId,[string]$Attempt) {
    if($Jobs.Count -ne 3 -or !(Assert-PublicPostgreSqlExact @($Jobs.name) @('Windows','Linux','PostgreSql'))){throw 'SF-CI-MATRIX'}
    foreach($job in $Jobs){
        if($job.result -cne 'success' -or $job.commit -cne $Commit -or $job.runId -cne $RunId -or $job.attempt -cne $Attempt -or $job.status -cne 'PASSED' -or $job.cleanup -cne 'Completed' -or $job.sourceLink -cnotin @('PENDING','VERIFIED')){throw 'SF-CI-JOB'}
        if($job.name -ceq 'PostgreSql'){
            $files=@(Get-ChildItem -LiteralPath $job.campaign -Recurse -File)
            if($files.Count -ne $job.campaignFiles.Count -or !(Assert-PublicPostgreSqlExact @($files|ForEach-Object {[IO.Path]::GetRelativePath($job.campaign,$_.FullName).Replace('\','/')}) @($job.campaignFiles.path))){throw 'SF-CI-ARCHIVE'}
            foreach($file in $job.campaignFiles){$path=Resolve-PublicPostgreSqlEvidencePath $job.campaign $file.path;if((Get-FileHash -LiteralPath $path).Hash -cne $file.sha256){throw 'SF-CI-ARCHIVE'}}
            [void](Assert-PublicCiPostgreSql $job.campaign $Commit);continue
        }
        $projects=@(Get-PublicCiProjects|Where-Object Offline -gt 0)
        if($job.suites.Count -ne 11 -or !(Assert-PublicPostgreSqlExact @($job.suites.assembly) @($projects.Assembly))){throw 'SF-CI-SUITES'}
        foreach($project in $projects){
            $suite=@($job.suites|Where-Object assembly -ceq $project.Assembly)[0]
            $path=Join-Path $job.root ('offline/'+$project.Assembly+'.trx')
            if($suite.passed -ne $project.Offline -or (Get-FileHash -LiteralPath $path).Hash -cne $suite.sha256){throw 'SF-CI-TRX'}
            $document=Read-PublicPostgreSqlTrxBytes ([IO.File]::ReadAllBytes($path))
            if($suite.inventory.Count -ne $project.Offline){throw 'SF-CI-INVENTORY'}
            $validated=Assert-PublicCiOfflineTrx ([IO.File]::ReadAllBytes($path)) $suite.inventory $project.Assembly ([DateTimeOffset]$suite.start) ([DateTimeOffset]$suite.finish) -Archived
            if($validated.Count -ne $project.Offline){throw 'SF-CI-INVENTORY'}
        }
        if(@(Get-ChildItem (Join-Path $job.root 'offline') -Filter '*.trx').Count -ne 11){throw 'SF-CI-SUITES'}
    }
}

