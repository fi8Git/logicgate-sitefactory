#requires -Version 7.4
[CmdletBinding()]
param(
    [string] $RepositoryRoot = (Join-Path $PSScriptRoot '../..'),
    [ValidatePattern('^(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)-localproof\.(0|[1-9][0-9]*)$')]
    [string] $PackageVersion = '0.1.0-localproof.1',
    [switch] $WorkingTreeSdk,
    [switch] $TemplateContracts
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$RepositoryRoot = [IO.Path]::TrimEndingDirectorySeparator([IO.Path]::GetFullPath($RepositoryRoot))
$packageId = 'LogicGate.SiteFactory.ModuleSdk'
$projectRelative = "src/$packageId/$packageId.csproj"
$temporaryParent = [IO.Path]::TrimEndingDirectorySeparator([IO.Path]::GetTempPath())
$ownership = [Guid]::NewGuid().ToString('N')
$work = Join-Path $temporaryParent ('sitefactory-modulesdk-' + $ownership)
$phase = 'INITIALIZE'
$checks = [Collections.Generic.List[string]]::new()
$rejections = [Collections.Generic.List[object]]::new()
$script:lastProcessExitCode = $null
$script:lastDiagnosticCodes = @()
$result = [ordered]@{ schemaVersion = 1; status = 'FAILED'; packageId = $packageId; version = $PackageVersion }
$failure = $null

function Require([bool] $Condition, [string] $Code) {
    if (!$Condition) { throw $Code }
}

function Same-Directory([string] $Left, [string] $Right) {
    [string]::Equals([IO.Path]::TrimEndingDirectorySeparator([IO.Path]::GetFullPath($Left)),
        [IO.Path]::TrimEndingDirectorySeparator([IO.Path]::GetFullPath($Right)), $comparison)
}

function Write-Utf8([string] $Path, [string] $Content) {
    [void][IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($Path))
    [IO.File]::WriteAllText($Path, $Content, [Text.UTF8Encoding]::new($false))
}

function Invoke-ProofProcess([string] $Executable, [string[]] $Arguments, [string] $Directory,
    [string] $Cache, [int] $TimeoutSeconds = 180) {
    $script:lastProcessExitCode = $null
    $script:lastDiagnosticCodes = @()
    $start = [Diagnostics.ProcessStartInfo]::new($Executable)
    $start.WorkingDirectory = $Directory
    $start.UseShellExecute = $false
    $start.CreateNoWindow = $true
    $start.RedirectStandardOutput = $true
    $start.RedirectStandardError = $true
    foreach ($argument in $Arguments) { $start.ArgumentList.Add($argument) }
    if ($Executable -ceq 'dotnet' -and $Arguments[0] -cin @('restore', 'pack', 'build', 'msbuild')) {
        if ($Arguments[0] -cne 'msbuild') { $start.ArgumentList.Add('--disable-build-servers') }
        $start.ArgumentList.Add('-p:UseSharedCompilation=false')
    }
    # Child-only settings: never clear a user's caches or alter the caller's environment.
    # Remove the whole inherited Git namespace, including COMMON_DIR, INDEX_FILE,
    # object/alternate stores, injected config/count/key/value and future GIT_* knobs.
    foreach ($name in @($start.Environment.Keys | Where-Object { $_.StartsWith('GIT_', [StringComparison]::OrdinalIgnoreCase) }) +
        @('NUGET_PLUGIN_PATHS', 'NUGET_CREDENTIALPROVIDERS_PATH')) {
        [void]$start.Environment.Remove($name)
    }
    $start.Environment['GIT_CONFIG_NOSYSTEM'] = '1'
    $start.Environment['GIT_CONFIG_GLOBAL'] = Join-Path $work 'empty.gitconfig'
    $start.Environment['GIT_CONFIG_SYSTEM'] = Join-Path $work 'empty.gitconfig'
    $start.Environment['GIT_ATTR_NOSYSTEM'] = '1'
    $start.Environment['GIT_TERMINAL_PROMPT'] = '0'
    $start.Environment['DOTNET_CLI_HOME'] = Join-Path $work 'dotnet-home'
    $start.Environment['DOTNET_CLI_TELEMETRY_OPTOUT'] = '1'
    $start.Environment['DOTNET_SKIP_FIRST_TIME_EXPERIENCE'] = '1'
    $start.Environment['DOTNET_NOLOGO'] = '1'
    $start.Environment['DOTNET_CLI_WORKLOAD_UPDATE_NOTIFY_DISABLE'] = 'true'
    $start.Environment['MSBUILDDISABLENODEREUSE'] = '1'
    # The SDK otherwise adds its installed library-packs directory as an extra feed.
    $start.Environment['DisableImplicitLibraryPacksFolder'] = 'true'
    $start.Environment['NUGET_PACKAGES'] = $Cache
    $start.Environment['NUGET_HTTP_CACHE_PATH'] = Join-Path $work 'http-cache'
    $start.Environment['NUGET_SCRATCH'] = Join-Path $work 'nuget-scratch'
    $start.Environment['NUGET_PLUGINS_CACHE_PATH'] = Join-Path $work 'plugin-cache'
    $process = [Diagnostics.Process]::new()
    $process.StartInfo = $start
    try {
        Require ($process.Start()) 'PROCESS'
        $stdout = $process.StandardOutput.ReadToEndAsync()
        $stderr = $process.StandardError.ReadToEndAsync()
        if (!$process.WaitForExit($TimeoutSeconds * 1000)) {
            $process.Kill($true)
            $process.WaitForExit()
            throw 'TIMEOUT'
        }
        $output = $stdout.GetAwaiter().GetResult() + $stderr.GetAwaiter().GetResult()
        $script:lastProcessExitCode = $process.ExitCode
        $script:lastDiagnosticCodes = @([regex]::Matches($output, '\b(?:NU\d{4}|CS\d{4}|CA\d{4}|MSB\d{4}|NETSDK\d{4})\b') |
            ForEach-Object Value | Sort-Object -Unique)
        Require ($output.Length -le 4194304) 'OUTPUT'
        # Output stays in memory; raw build errors and local paths are never reported.
        return @{ ExitCode = $process.ExitCode; Output = $output }
    } finally {
        if ($process.Id -and !$process.HasExited) { $process.Kill($true); $process.WaitForExit() }
        $process.Dispose()
    }
}

function Invoke-DotNet([string[]] $Arguments, [string] $Directory, [string] $Cache) {
    $command = Invoke-ProofProcess 'dotnet' $Arguments $Directory $Cache
    Require ($command.ExitCode -eq 0) 'DOTNET'
    return $command.Output
}

function Invoke-ProofGit([string[]] $Arguments, [string] $Code = 'GIT') {
    # Trust only the explicitly requested checkout, even under a different sandbox UID.
    $command = Invoke-ProofProcess 'git' (@('--no-optional-locks', '--no-replace-objects',
        '-c', "safe.directory=$RepositoryRoot", '-c', ('core.attributesFile=' + (Join-Path $work 'empty.gitconfig')),
        '-C', $RepositoryRoot) +
        $Arguments) $RepositoryRoot (Join-Path $work 'builder-cache')
    Require ($command.ExitCode -eq 0) $Code
    return $command.Output.TrimEnd("`r", "`n")
}

function Get-ProofBlobId([byte[]] $Bytes) {
    # Git SHA-1 blob identity, over the exact bytes; no index/stat cache, clean filter or textconv is consulted.
    $hash = [Security.Cryptography.IncrementalHash]::CreateHash([Security.Cryptography.HashAlgorithmName]::SHA1)
    try {
        $hash.AppendData([Text.Encoding]::ASCII.GetBytes('blob ' + $Bytes.Length.ToString([Globalization.CultureInfo]::InvariantCulture) + [char]0))
        $hash.AppendData($Bytes)
        return [Convert]::ToHexString($hash.GetHashAndReset()).ToLowerInvariant()
    } finally { $hash.Dispose() }
}

function Get-ProofSdkPaths {
    foreach ($relative in @('src', "src/$packageId")) {
        $directory = Join-Path $RepositoryRoot $relative
        Require ([IO.Directory]::Exists($directory) -and
            ([IO.File]::GetAttributes($directory) -band [IO.FileAttributes]::ReparsePoint) -eq 0) 'INVENTORY'
    }
    $pending = [Collections.Generic.Stack[string]]::new()
    $pending.Push((Join-Path $RepositoryRoot "src/$packageId"))
    while ($pending.Count) {
        foreach ($entry in [IO.Directory]::EnumerateFileSystemEntries($pending.Pop())) {
            $attributes = [IO.File]::GetAttributes($entry)
            # Build outputs are not source inputs and are never copied or followed.
            if (($attributes -band [IO.FileAttributes]::Directory) -ne 0 -and [IO.Path]::GetFileName($entry) -cin @('bin', 'obj')) { continue }
            Require (($attributes -band [IO.FileAttributes]::ReparsePoint) -eq 0) 'INVENTORY'
            if (($attributes -band [IO.FileAttributes]::Directory) -ne 0) { $pending.Push($entry); continue }
            Require ([IO.File]::Exists($entry)) 'INVENTORY'
            $relative = [IO.Path]::GetRelativePath($RepositoryRoot, $entry).Replace('\', '/')
            Require ($relative -cmatch '^[A-Za-z0-9_./-]+$') 'INVENTORY'
            $relative
        }
    }
}

function Test-ProofSnapshotSource([string] $Relative) {
    $WorkingTreeSdk -and $Relative.StartsWith("src/$packageId/", [StringComparison]::Ordinal) -and
        $Relative.EndsWith('.cs', [StringComparison]::Ordinal)
}

function Get-ProofProtectedBytes([string] $Relative, [byte[]] $Bytes, [string] $Blob) {
    if ((Get-ProofBlobId $Bytes) -ceq $Blob) { return ,$Bytes }
    # Only the bounded Git text conversion below is supported. Never execute a filter or decode/rewrite arbitrary encodings.
    $attributes = (Invoke-ProofGit @('check-attr', '-z', "--source=$commit", 'text', 'eol', 'filter', 'ident',
        'working-tree-encoding', '--', $Relative) 'CONVERSION').Split([char]0, [StringSplitOptions]::RemoveEmptyEntries)
    Require ($attributes.Count -eq 15) 'CONVERSION'
    $values = @{}
    for ($index = 0; $index -lt $attributes.Count; $index += 3) {
        Require ($attributes[$index] -ceq $Relative -and !$values.ContainsKey($attributes[$index + 1])) 'CONVERSION'
        $values[$attributes[$index + 1]] = $attributes[$index + 2]
    }
    Require ($values.text -cin @('set', 'auto') -and $values.eol -cin @('lf', 'crlf', 'unspecified') -and
        $values.filter -cin @('unspecified', 'unset') -and $values.ident -cin @('unspecified', 'unset') -and
        $values['working-tree-encoding'] -cin @('unspecified', 'unset')) 'CONVERSION'
    $encoding = [Text.UTF8Encoding]::new($false, $true)
    try { $text = $encoding.GetString($Bytes) } catch [Text.DecoderFallbackException] { throw 'CONVERSION' }
    $withoutCrLf = $text.Replace("`r`n", '')
    Require (!$text.Contains([char]0) -and !$withoutCrLf.Contains("`r") -and !$withoutCrLf.Contains("`n")) 'INPUTS'
    $canonical = $encoding.GetBytes($text.Replace("`r`n", "`n"))
    # BOM, whitespace, Unicode and every non-EOL byte remain intact; a real edit still changes the blob identity.
    Require ((Get-ProofBlobId $canonical) -ceq $Blob) 'INPUTS'
    return ,$canonical
}

function New-LocalConfig([string] $Path, [string] $Approved, [string] $Decoy) {
    $approvedXml = [Security.SecurityElement]::Escape($Approved)
    $extra = if ($Decoy) { '<add key="decoy" value="' + [Security.SecurityElement]::Escape($Decoy) + '" />' } else { '' }
    Write-Utf8 $Path @"
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources><clear /><add key="local-sdk" value="$approvedXml" />$extra</packageSources>
  <fallbackPackageFolders><clear /></fallbackPackageFolders>
  <packageSourceMapping>
    <clear />
    <packageSource key="local-sdk"><package pattern="$packageId" /></packageSource>
  </packageSourceMapping>
</configuration>
"@
}

function Restore-Consumer([string] $Directory, [string] $Cache, [string] $Config, [switch] $Locked) {
    $arguments = @('restore', 'Consumer.csproj', '--configfile', $Config, '--packages', $Cache,
        '--no-http-cache', '--verbosity', 'minimal',
        '-p:ImportDirectoryBuildProps=false', '-p:ImportDirectoryBuildTargets=false',
        '-p:ManagePackageVersionsCentrally=false', '-p:DisableImplicitLibraryPacksFolder=true',
        '-p:RestoreAdditionalProjectSources=',
        '-p:RestoreFallbackFolders=', '-p:RestoreAdditionalProjectFallbackFolders=')
    $arguments += if ($Locked) { '--locked-mode' } else { '--use-lock-file' }
    Invoke-ProofProcess 'dotnet' $arguments $Directory $Cache
}

function New-Consumer([string] $Directory, [string] $Version) {
    Write-Utf8 (Join-Path $Directory 'Consumer.csproj') @"
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <OutputType>Exe</OutputType>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
    <RestorePackagesWithLockFile>true</RestorePackagesWithLockFile>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="$packageId" Version="[$Version]" />
  </ItemGroup>
</Project>
"@
    Write-Utf8 (Join-Path $Directory 'Program.cs') @'
using System.Security.Cryptography;
using System.Text.Json;
using LogicGate.SiteFactory.ModuleSdk;

ISiteFactoryModule module = new ExternalModule();
ModuleDescriptor descriptor = module.Descriptor;
if (!ReferenceEquals(descriptor, module.Descriptor)
    || descriptor.Id.Value != "external-proof"
    || descriptor.Version.Value != "1.0.0"
    || !descriptor.Capabilities.Select(capability => capability.Value)
        .SequenceEqual(["external-proof.read", "external-proof.write"])
    || typeof(ISiteFactoryModule).Assembly == module.GetType().Assembly
    || typeof(ISiteFactoryModule).Assembly.GetName().Name != "LogicGate.SiteFactory.ModuleSdk")
    return 1;

Console.WriteLine(JsonSerializer.Serialize(new
{
    contracts = "VERIFIED",
    runtime = Environment.Version.ToString(),
    assemblySha256 = Convert.ToHexString(SHA256.HashData(
        File.ReadAllBytes(typeof(ISiteFactoryModule).Assembly.Location))).ToLowerInvariant()
}));
return 0;

sealed class ExternalModule : ISiteFactoryModule
{
    public ModuleDescriptor Descriptor { get; } = new(
        new ModuleId("external-proof"), new ModuleVersion("1.0.0"),
        [new ModuleCapability("external-proof.write"), new ModuleCapability("external-proof.read")]);
}
'@
    if ($TemplateContracts) {
        $program = Join-Path $Directory 'Program.cs'
        Write-Utf8 $program ([IO.File]::ReadAllText($program).Replace('Console.WriteLine(JsonSerializer.Serialize(new',
            "await TemplateProviderConsumer.VerifyAsync();`n`nConsole.WriteLine(JsonSerializer.Serialize(new"))
        Write-Utf8 (Join-Path $Directory 'TemplateProviderConsumer.cs') (
            [IO.File]::ReadAllText((Join-Path $PSScriptRoot 'TemplateProviderConsumer.cs.txt')))
    }
}

try {
    Require (![IO.Path]::Exists($work)) 'OWNERSHIP'
    [void][IO.Directory]::CreateDirectory($work)
    Write-Utf8 (Join-Path $work 'ownership') $ownership
    Write-Utf8 (Join-Path $work 'empty.gitconfig') ''
    $comparison = if ($IsWindows) { [StringComparison]::OrdinalIgnoreCase } else { [StringComparison]::Ordinal }
    Require (!$work.StartsWith($RepositoryRoot + [IO.Path]::DirectorySeparatorChar, $comparison)) 'ISOLATION'
    # Reject the wrong checkout before reading or copying any SDK input.
    Require ([IO.Directory]::Exists($RepositoryRoot)) 'REPOSITORY'
    $topLevel = Invoke-ProofGit @('rev-parse', '--show-toplevel') 'REPOSITORY'
    Require (Same-Directory $topLevel $RepositoryRoot) 'REPOSITORY'
    Require ((Invoke-ProofGit @('rev-parse', '--is-inside-work-tree') 'REPOSITORY') -ceq 'true') 'REPOSITORY'
    $identity = Invoke-ProofGit @('config', '--local', '--no-includes', '--get', 'remote.origin.url') 'REPOSITORY'
    Require ($identity -cin @(
        'https://github.com/fi8Git/logicgate-sitefactory',
        'https://github.com/fi8Git/logicgate-sitefactory.git')) 'REPOSITORY'
    $commit = Invoke-ProofGit @('rev-parse', '--verify', 'HEAD^{commit}')
    Require ($commit -cmatch '^[0-9a-f]{40}$') 'REPOSITORY'
    if ($WorkingTreeSdk) { $result['baseCommit'] = $commit } else { $result['repositoryCommit'] = $commit }
    $result['sourceMode'] = if ($WorkingTreeSdk) { 'WorkingTreeSdk' } else { 'Head' }
    $result['contractScope'] = if ($TemplateContracts) { 'Templates' } else { 'Metadata' }
    $checks.Add('requested-checkout-root-and-public-head-verified')
    $build = Join-Path $work 'build'
    $consumer = Join-Path $work 'consumer'
    $feed = Join-Path $work 'feed'
    $builderCache = Join-Path $work 'builder-cache'
    [void][IO.Directory]::CreateDirectory($build)
    [void][IO.Directory]::CreateDirectory($feed)
    Require ((Invoke-ProofGit @('rev-parse', '--show-object-format')) -ceq 'sha1') 'INPUTS'
    $fixedInputs = @('global.json', 'Directory.Build.props', 'Directory.Packages.props', 'LICENSE', 'NOTICE')
    $headInputs = [Collections.Generic.Dictionary[string, string]]::new([StringComparer]::Ordinal)
    $tree = Invoke-ProofGit (@('ls-tree', '-r', '-z', '--full-tree', $commit, '--') + $fixedInputs + "src/$packageId/") 'INPUTS'
    foreach ($entry in $tree.Split([char]0, [StringSplitOptions]::RemoveEmptyEntries)) {
        Require ($entry -cmatch '\A100(?:644|755) blob ([0-9a-f]{40})\t([A-Za-z0-9_./-]+)\z') 'INVENTORY'
        $headInputs.Add($Matches[2], $Matches[1])
    }
    $sdkFiles = @(Get-ProofSdkPaths)
    $actualSdk = [Collections.Generic.HashSet[string]]::new([string[]]$sdkFiles, [StringComparer]::Ordinal)
    Require ($actualSdk.Count -eq $sdkFiles.Count -and $actualSdk.Contains($projectRelative)) 'INVENTORY'
    foreach ($relative in $sdkFiles) { Require ($headInputs.ContainsKey($relative) -or (Test-ProofSnapshotSource $relative)) 'INVENTORY' }
    foreach ($relative in $headInputs.Keys | Where-Object { $_.StartsWith("src/$packageId/", [StringComparison]::Ordinal) }) {
        Require ($actualSdk.Contains($relative) -or (Test-ProofSnapshotSource $relative)) 'INVENTORY'
    }
    $inputs = $fixedInputs + $sdkFiles
    [Array]::Sort($inputs, [StringComparer]::Ordinal)
    $snapshot = [Collections.Generic.List[object]]::new()
    $normalizedTextInputs = 0
    foreach ($relative in $inputs) {
        $source = Join-Path $RepositoryRoot $relative
        Require ([IO.File]::Exists($source) -and ([IO.File]::GetAttributes($source) -band [IO.FileAttributes]::ReparsePoint) -eq 0) 'INPUTS'
        $raw = [IO.File]::ReadAllBytes($source)
        $hash = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($raw))
        $captured = $raw
        if (!(Test-ProofSnapshotSource $relative)) {
            Require ($headInputs.ContainsKey($relative)) 'INVENTORY'
            $captured = Get-ProofProtectedBytes $relative $raw $headInputs[$relative]
        }
        $target = Join-Path $build $relative
        [void][IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($target))
        [IO.File]::Copy($source, $target, $false)
        Require ((Get-FileHash -LiteralPath $source).Hash -ceq $hash -and (Get-FileHash -LiteralPath $target).Hash -ceq $hash) 'COPY'
        $targetHash = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($captured)).ToLowerInvariant()
        if ($targetHash -cne $hash.ToLowerInvariant()) {
            [IO.File]::WriteAllBytes($target, $captured)
            $normalizedTextInputs++
        }
        Require ((Get-FileHash -LiteralPath $target).Hash.ToLowerInvariant() -ceq $targetHash) 'COPY'
        $snapshot.Add([ordered]@{ path = $relative; sha256 = $targetHash; checkoutSha256 = $hash.ToLowerInvariant() })
    }
    Require ($actualSdk.SetEquals([string[]]@(Get-ProofSdkPaths))) 'INVENTORY'
    $result['normalizedTextInputs'] = $normalizedTextInputs
    $checks.Add('head-blob-inventory-and-protected-inputs-verified')
    # Preserve SDK selection for the independently located consumer as well.
    [IO.File]::Copy((Join-Path $build 'global.json'), (Join-Path $work 'global.json'), $false)
    # SourceLink minimal metadata only; no helper script is loaded or executed.
    # https://github.com/dotnet/sourcelink/blob/main/docs/README.md#minimal-git-repository-metadata
    # No objects, index, hooks, credentials or config from the checkout are copied.
    Require ((Invoke-ProofGit @('rev-parse', '--verify', 'HEAD^{commit}')) -ceq $commit) 'REPOSITORY'
    Require ((Invoke-ProofGit @('config', '--local', '--no-includes', '--get', 'remote.origin.url')) -ceq $identity) 'REPOSITORY'
    $metadataRoot = Join-Path $build '.git'
    Require (![IO.Path]::Exists($metadataRoot)) 'ISOLATION'
    if ($WorkingTreeSdk) {
        # No commit or SourceLink context is invented for uncommitted sources.
        $snapshotJson = [ordered]@{ schemaVersion = 1; sourceMode = 'WorkingTreeSdk'; baseCommit = $commit;
            inputs = @($snapshot.ToArray()) } | ConvertTo-Json -Depth 5
        Write-Utf8 (Join-Path $build 'PROOF-SOURCE.json') $snapshotJson
        $result['sourceSnapshotSha256'] = (Get-FileHash -LiteralPath (Join-Path $build 'PROOF-SOURCE.json')).Hash.ToLowerInvariant()
        $checks.Add('working-tree-sdk-snapshot-copied-and-verified')
    } else {
        Write-Utf8 (Join-Path $metadataRoot 'HEAD') ($commit + "`n")
        Write-Utf8 (Join-Path $metadataRoot 'config') ('[remote "origin"]' + "`n" +
            'url = https://github.com/fi8Git/logicgate-sitefactory' + "`n")
        $checks.Add('committed-sdk-inputs-copied-and-verified')
    }

    # Local pack overlay only. Do not add packaging imports or assets to any engine project.
    Write-Utf8 (Join-Path $build 'PACKAGE-README.md') @'
# LogicGate.SiteFactory.ModuleSdk — local prerelease proof

Immutable module metadata and declarative template provider contracts.
No engine integration, dynamic loading or Documentation integration.
This package is temporary, local and not an official release.
'@
    $packTargets = @'
<Project>
  <PropertyGroup>
    <PackageId>LogicGate.SiteFactory.ModuleSdk</PackageId>
    <Authors>LogicGate</Authors>
    <Description>SiteFactory module metadata and declarative template contracts; local prerelease consumption proof.</Description>
    <PackageLicenseExpression>Apache-2.0</PackageLicenseExpression>
    <PackageReadmeFile>README.md</PackageReadmeFile>
    <PackageTags>sitefactory modules contracts</PackageTags>
    <RepositoryUrl>https://github.com/fi8Git/logicgate-sitefactory</RepositoryUrl>
    <RepositoryType>git</RepositoryType>
    <RepositoryCommit>__COMMIT__</RepositoryCommit>
    <PackageReleaseNotes>Local proof only; no published release. See repository CHANGELOG.md.</PackageReleaseNotes>
  </PropertyGroup>
  <ItemGroup>
    <None Include="$(MSBuildThisFileDirectory)LICENSE" Pack="true" PackagePath="" />
    <None Include="$(MSBuildThisFileDirectory)NOTICE" Pack="true" PackagePath="" />
    <None Include="$(MSBuildThisFileDirectory)PACKAGE-README.md" Pack="true" PackagePath="README.md" />
  </ItemGroup>
</Project>
'@
    $packageCommit = if ($WorkingTreeSdk) { '' } else { $commit }
    $packTargets = $packTargets.Replace('__COMMIT__', $packageCommit)
    if ($WorkingTreeSdk) {
        $packTargets = $packTargets.Replace('<ItemGroup>', '<ItemGroup>' + "`n" +
            '    <None Include="$(MSBuildThisFileDirectory)PROOF-SOURCE.json" Pack="true" PackagePath="PROOF-SOURCE.json" />')
    }
    Write-Utf8 (Join-Path $build 'Directory.Build.targets') $packTargets
    $config = Join-Path $work 'NuGet.config'
    New-LocalConfig $config $feed ''
    $phase = 'PACK'
    $result['sdk'] = (Invoke-DotNet @('--version') $build $builderCache).Trim()
    $null = Invoke-DotNet @('restore', $projectRelative, '--locked-mode', '--configfile', $config,
        '--packages', $builderCache, '--no-http-cache') $build $builderCache
    $builderAssets = Get-Content -LiteralPath (Join-Path $build "src/$packageId/obj/project.assets.json") -Raw | ConvertFrom-Json -AsHashtable
    Require ($builderAssets.libraries.Count -eq 0) 'DEPENDENCIES'
    $null = Invoke-DotNet @('pack', $projectRelative, '--configuration', 'Release', '--no-restore',
        '--output', $feed, "-p:PackageVersion=$PackageVersion", "-p:RepositoryCommit=$packageCommit") $build $builderCache

    $phase = 'PACKAGE'
    $packages = @(Get-ChildItem -LiteralPath $feed -File)
    Require ($packages.Count -eq 1 -and $packages[0].Name -ceq "$packageId.$PackageVersion.nupkg") 'PACKAGE'
    $archive = [IO.Compression.ZipFile]::OpenRead($packages[0].FullName)
    try {
        $dll = "lib/net10.0/$packageId.dll"
        $required = @("$packageId.nuspec", $dll, 'LICENSE', 'NOTICE', 'README.md', '[Content_Types].xml', '_rels/.rels')
        if ($WorkingTreeSdk) { $required += 'PROOF-SOURCE.json' }
        foreach ($entry in $archive.Entries) {
            Require ($entry.FullName -cin $required -or $entry.FullName -cmatch '^package/services/metadata/core-properties/(nuget|[0-9a-f]{32})\.psmdcp$') 'CONTENT'
        }
        Require (@($archive.Entries | Where-Object FullName -CLike 'package/services/metadata/core-properties/*').Count -eq 1) 'CONTENT'
        foreach ($name in $required) { Require (@($archive.Entries | Where-Object FullName -CEQ $name).Count -eq 1) 'CONTENT' }
        $reader = [IO.StreamReader]::new($archive.GetEntry("$packageId.nuspec").Open())
        try { [xml]$nuspec = $reader.ReadToEnd() } finally { $reader.Dispose() }
        $metadata = $nuspec.package.metadata
        Require ($metadata.id -ceq $packageId -and $metadata.version -ceq $PackageVersion -and
            $metadata.authors -ceq 'LogicGate' -and $metadata.license.InnerText -ceq 'Apache-2.0' -and
            $metadata.repository.url -ceq 'https://github.com/fi8Git/logicgate-sitefactory' -and
            $metadata.repository.GetAttribute('commit') -ceq $packageCommit -and @($nuspec.SelectNodes('//*[local-name()="dependency"]')).Count -eq 0) 'METADATA'
        if ($WorkingTreeSdk) {
            $sourceStream = $archive.GetEntry('PROOF-SOURCE.json').Open()
            try { $sourceHash = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($sourceStream)).ToLowerInvariant() }
            finally { $sourceStream.Dispose() }
            Require ($sourceHash -ceq $result.sourceSnapshotSha256) 'SNAPSHOT'
        }
        $stream = $archive.GetEntry($dll).Open()
        try { $dllHash = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($stream)).ToLowerInvariant() } finally { $stream.Dispose() }
    } finally { $archive.Dispose() }
    $result['packageSha256'] = (Get-FileHash -LiteralPath $packages[0].FullName).Hash.ToLowerInvariant()
    $checks.Add('package-identity-provenance-notices-and-closed-content')

    $phase = 'RESTORE'
    New-Consumer $consumer $PackageVersion
    $warm = Restore-Consumer $consumer (Join-Path $work 'initial-cache') $config
    Require ($warm.ExitCode -eq 0) 'RESTORE'
    $lockPath = Join-Path $consumer 'packages.lock.json'
    $lockHash = (Get-FileHash -LiteralPath $lockPath).Hash
    $cache = Join-Path $work 'consumer-cache'
    $locked = Restore-Consumer $consumer $cache $config -Locked
    Require ($locked.ExitCode -eq 0 -and (Get-FileHash -LiteralPath $lockPath).Hash -ceq $lockHash) 'LOCK'
    $assets = Get-Content -LiteralPath (Join-Path $consumer 'obj/project.assets.json') -Raw | ConvertFrom-Json -AsHashtable
    Require ($assets.libraries.Count -eq 1 -and $assets.libraries["$packageId/$PackageVersion"].type -ceq 'package' -and
        $assets.packageFolders.Count -eq 1 -and (Same-Directory @($assets.packageFolders.Keys)[0] $cache)) 'ASSETS'
    foreach ($framework in $assets.project.restore.frameworks.Values) { Require ($framework.projectReferences.Count -eq 0) 'PROJECT_REFERENCE' }
    Require ($assets.project.restore.sources.Count -eq 1 -and (Same-Directory @($assets.project.restore.sources.Keys)[0] $feed)) 'SOURCE'
    $installed = Join-Path $cache ($packageId.ToLowerInvariant() + '/' + $PackageVersion)
    $origin = Get-Content -LiteralPath (Join-Path $installed '.nupkg.metadata') -Raw | ConvertFrom-Json -AsHashtable
    Require (Same-Directory $origin.source $feed) 'SOURCE'
    Require ((Get-FileHash -LiteralPath (Join-Path $installed $packages[0].Name.ToLowerInvariant())).Hash -ceq $result.packageSha256.ToUpperInvariant()) 'PACKAGE_HASH'
    $checks.Add('exact-package-only-fresh-cache-locked-restore')

    $phase = 'CONSUMER'
    $evaluated = Invoke-DotNet @('msbuild', 'Consumer.csproj', '-getProperty:MSBuildAllProjects',
        '-getItem:ProjectReference,Compile', '-p:ImportDirectoryBuildProps=false',
        '-p:ImportDirectoryBuildTargets=false', '-p:ManagePackageVersionsCentrally=false') $consumer $cache |
        ConvertFrom-Json -AsHashtable
    Require ($evaluated.Items.ProjectReference.Count -eq 0 -and $evaluated.Items.Compile.Count -gt 0) 'PROJECT_REFERENCE'
    foreach ($item in $evaluated.Items.Compile) {
        Require ([IO.Path]::GetFullPath($item.FullPath).StartsWith($consumer + [IO.Path]::DirectorySeparatorChar, $comparison)) 'SOURCE_INPUT'
    }
    foreach ($import in $evaluated.Properties.MSBuildAllProjects.Split(';', [StringSplitOptions]::RemoveEmptyEntries)) {
        Require (![IO.Path]::GetFullPath($import).StartsWith($RepositoryRoot + [IO.Path]::DirectorySeparatorChar, $comparison)) 'SOURCE_INPUT'
    }
    $checks.Add('evaluated-consumer-imports-and-compile-inputs-isolated')
    $null = Invoke-DotNet @('build', 'Consumer.csproj', '--configuration', 'Release', '--no-restore',
        '-p:ImportDirectoryBuildProps=false', '-p:ImportDirectoryBuildTargets=false',
        '-p:ManagePackageVersionsCentrally=false') $consumer $cache
    $execution = Invoke-DotNet @('bin/Release/net10.0/Consumer.dll') $consumer $cache | ConvertFrom-Json -AsHashtable
    Require ($execution.contracts -ceq 'VERIFIED' -and $execution.assemblySha256 -ceq $dllHash) 'CONTRACTS'
    $result['runtime'] = $execution.runtime
    $checks.Add('release-consumer-executed-contracts-and-package-assembly')
    if ($TemplateContracts) { $checks.Add('external-template-provider-contracts-executed') }

    $phase = 'NEGATIVE'
    $missing = Join-Path $work 'missing-version'
    New-Consumer $missing ($PackageVersion + '.missing')
    $rejected = Restore-Consumer $missing (Join-Path $work 'missing-cache') $config
    Require ($rejected.ExitCode -ne 0 -and $rejected.Output -match '\bNU1102\b') 'MISSING_VERSION'
    $rejections.Add(@{ case = 'missing-version'; exitCode = $rejected.ExitCode; code = 'NU1102' })
    $checks.Add('missing-exact-version-rejected-NU1102')

    $unmapped = Join-Path $work 'unmapped-consumer'
    $emptyFeed = Join-Path $work 'empty-approved-feed'
    [void][IO.Directory]::CreateDirectory($emptyFeed)
    $mappingConfig = Join-Path $work 'mapping.config'
    New-LocalConfig $mappingConfig $emptyFeed $feed
    New-Consumer $unmapped $PackageVersion
    $rejected = Restore-Consumer $unmapped (Join-Path $work 'unmapped-cache') $mappingConfig
    Require ($rejected.ExitCode -ne 0 -and $rejected.Output -match '\bNU1101\b') 'UNMAPPED_SOURCE'
    $rejections.Add(@{ case = 'unmapped-source'; exitCode = $rejected.ExitCode; code = 'NU1101' })
    $checks.Add('populated-unmapped-decoy-rejected-NU1101')

    $projectPath = Join-Path $consumer 'Consumer.csproj'
    $originalProject = [IO.File]::ReadAllText($projectPath)
    Write-Utf8 $projectPath ($originalProject.Replace("[$PackageVersion]", "[$PackageVersion.missing]"))
    $rejected = Restore-Consumer $consumer (Join-Path $work 'drift-cache') $config -Locked
    Require ($rejected.ExitCode -ne 0 -and $rejected.Output -match '\bNU1004\b') 'LOCK_DRIFT'
    $rejections.Add(@{ case = 'lock-drift'; exitCode = $rejected.ExitCode; code = 'NU1004' })
    Write-Utf8 $projectPath $originalProject
    Require ((Get-FileHash -LiteralPath $lockPath).Hash -ceq $lockHash) 'LOCK'
    $checks.Add('lock-drift-rejected-NU1004')
} catch {
    # Closed diagnostics only; never print the exception, tool output or a path.
    $failure = $phase
    $closedCodes = @('PROCESS', 'TIMEOUT', 'OUTPUT', 'DOTNET', 'GIT', 'OWNERSHIP', 'ISOLATION',
        'REPOSITORY', 'INPUTS', 'INVENTORY', 'CONVERSION', 'COPY', 'SNAPSHOT', 'DEPENDENCIES', 'PACKAGE', 'CONTENT', 'METADATA', 'RESTORE', 'LOCK',
        'ASSETS', 'PROJECT_REFERENCE', 'SOURCE', 'SOURCE_INPUT', 'PACKAGE_HASH', 'CONTRACTS', 'MISSING_VERSION',
        'UNMAPPED_SOURCE', 'LOCK_DRIFT')
    $result['failureCode'] = if ($_.Exception.Message -cin $closedCodes) { $_.Exception.Message } else { 'INTERNAL' }
    $result['lastProcessExitCode'] = $script:lastProcessExitCode
    $result['diagnosticCodes'] = $script:lastDiagnosticCodes
} finally {
    $result['checks'] = @($checks.ToArray())
    $result['expectedRejections'] = @($rejections.ToArray())
    $result['cleanup'] = 'Failed'
    try {
        Require ([IO.Path]::GetDirectoryName([IO.Path]::GetFullPath($work)) -ceq $temporaryParent) 'CLEANUP'
        Require ([IO.Path]::GetFileName($work) -ceq ('sitefactory-modulesdk-' + $ownership)) 'CLEANUP'
        if ([IO.Directory]::Exists($work)) {
            Require (([IO.File]::GetAttributes($work) -band [IO.FileAttributes]::ReparsePoint) -eq 0) 'CLEANUP'
            Require ([IO.File]::ReadAllText((Join-Path $work 'ownership')) -ceq $ownership) 'CLEANUP'
            [IO.Directory]::Delete($work, $true)
        }
        Require (![IO.Path]::Exists($work)) 'CLEANUP'
        $result['cleanup'] = 'Completed'
    } catch { if (!$failure) { $failure = 'CLEANUP' } }
}

if ($null -eq $failure) { $result['status'] = 'PASSED' } else { $result['failurePhase'] = $failure }
$result | ConvertTo-Json -Depth 5
if ($failure) { exit 1 }
