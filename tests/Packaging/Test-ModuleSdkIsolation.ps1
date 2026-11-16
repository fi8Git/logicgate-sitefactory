#requires -Version 7.4
[CmdletBinding()]
param(
    [string] $RepositoryRoot = (Join-Path $PSScriptRoot '../..'),
    [string] $ProofScript = (Join-Path $PSScriptRoot 'Test-ModuleSdkPackage.ps1'),
    [switch] $WorkingTreeSdk,
    [switch] $TemplateContracts,
    [switch] $Provenance
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$RepositoryRoot = [IO.Path]::TrimEndingDirectorySeparator([IO.Path]::GetFullPath($RepositoryRoot))
$ProofScript = [IO.Path]::GetFullPath($ProofScript)
$parent = [IO.Path]::TrimEndingDirectorySeparator([IO.Path]::GetTempPath())
$owner = [Guid]::NewGuid().ToString('N')
$work = Join-Path $parent ('sitefactory-packaging-tests-' + $owner)
$cases = [Collections.Generic.List[object]]::new()
$report = [ordered]@{ schemaVersion = 1; status = 'FAILED' }
$failure = $null
$phase = 'INITIALIZE'

function Require([bool] $Condition, [string] $Code) {
    if (!$Condition) { throw $Code }
}

function Run([string] $Executable, [string[]] $Arguments, [hashtable] $Environment = @{}) {
    $start = [Diagnostics.ProcessStartInfo]::new($Executable)
    $start.WorkingDirectory = $work
    $start.UseShellExecute = $false
    $start.CreateNoWindow = $true
    $start.RedirectStandardOutput = $true
    $start.RedirectStandardError = $true
    foreach ($arg in $Arguments) { $start.ArgumentList.Add($arg) }
    foreach ($key in @($start.Environment.Keys | Where-Object { $_.StartsWith('GIT_', [StringComparison]::OrdinalIgnoreCase) })) {
        [void]$start.Environment.Remove($key)
    }
    $start.Environment['GIT_CONFIG_GLOBAL'] = Join-Path $work 'empty.gitconfig'
    $start.Environment['GIT_CONFIG_SYSTEM'] = Join-Path $work 'empty.gitconfig'
    $start.Environment['GIT_CONFIG_NOSYSTEM'] = '1'
    $start.Environment['GIT_TERMINAL_PROMPT'] = '0'
    foreach ($key in $Environment.Keys) { $start.Environment[$key] = $Environment[$key] }
    $process = [Diagnostics.Process]::new()
    $process.StartInfo = $start
    try {
        Require ($process.Start()) 'PROCESS'
        $stdout = $process.StandardOutput.ReadToEndAsync()
        $stderr = $process.StandardError.ReadToEndAsync()
        if (!$process.WaitForExit(240000)) { $process.Kill($true); $process.WaitForExit(); throw 'TIMEOUT' }
        $output = $stdout.GetAwaiter().GetResult()
        $errors = $stderr.GetAwaiter().GetResult()
        Require (($output.Length + $errors.Length) -le 4194304) 'OUTPUT'
        return @{ exitCode = $process.ExitCode; output = $output; errors = $errors }
    } finally {
        if ($process.Id -and !$process.HasExited) { $process.Kill($true); $process.WaitForExit() }
        $process.Dispose()
    }
}

function Git([string[]] $Arguments) {
    if ($Arguments -ccontains 'update-index') {
        Require ($Arguments[0] -ceq '-C' -and [IO.Path]::GetFullPath($Arguments[1]).StartsWith(
            $work + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) 'ISOLATION'
    }
    $command = Run 'git' (@('--no-optional-locks', '--no-replace-objects', '-c', "safe.directory=$RepositoryRoot") + $Arguments)
    Require ($command.exitCode -eq 0) 'GIT'
    return $command.output.TrimEnd("`r", "`n")
}

function Invoke-Case([string] $Name, [string] $Root, [hashtable] $Environment,
    [string] $ExpectedCode = '', [string[]] $ForbiddenFiles = @(),
    [ValidateSet('GIT_TRACE', 'HELPER_EXECUTED')] [string] $ForbiddenCode = 'GIT_TRACE',
    [switch] $HeadOnly, [switch] $ForceWorkingTree) {
    $entry = [ordered]@{ case = $Name; status = 'FAILED' }
    try {
        $arguments = @('-NoLogo', '-NoProfile', '-NonInteractive', '-File', $ProofScript, '-RepositoryRoot', $Root)
        if (($WorkingTreeSdk -and !$HeadOnly) -or $ForceWorkingTree) { $arguments += '-WorkingTreeSdk' }
        if ($TemplateContracts -and !$HeadOnly) { $arguments += '-TemplateContracts' }
        $command = Run (Join-Path $PSHOME ('pwsh' + $(if ($IsWindows) { '.exe' }))) $arguments $Environment
        $entry['exitCode'] = $command.exitCode
        # Parse the entire stdout, not a JSON substring hiding extra output.
        $proof = $command.output | ConvertFrom-Json -AsHashtable
        Require ($proof.status -cin @('PASSED', 'FAILED')) 'PROOF'
        $entry['proofStatus'] = $proof.status
        if ($proof.ContainsKey('sourceMode')) { $entry['sourceMode'] = $proof.sourceMode }
        if ($proof.ContainsKey('normalizedTextInputs')) { $entry['normalizedTextInputs'] = $proof.normalizedTextInputs }
        Require ([string]::IsNullOrWhiteSpace($command.errors)) 'DISCLOSURE'
        Require ($proof.cleanup -ceq 'Completed') 'CLEANUP'
        $entry['cleanup'] = $proof.cleanup
        Require (!$command.output.Contains($work, [StringComparison]::OrdinalIgnoreCase) -and
            !$command.output.Contains($RepositoryRoot, [StringComparison]::OrdinalIgnoreCase) -and
            !$command.output.Contains('P2_UNTRUSTED_HELPER') -and !$command.output.Contains('example.invalid')) 'DISCLOSURE'
        foreach ($file in $ForbiddenFiles) { Require (![IO.Path]::Exists($file)) $ForbiddenCode }
        if ($ExpectedCode) {
            Require ($command.exitCode -ne 0 -and $proof.status -ceq 'FAILED' -and
                $proof.failurePhase -ceq 'INITIALIZE' -and $proof.failureCode -ceq $ExpectedCode) 'EXPECTED_REJECTION'
            Require ($proof.checks -cnotcontains 'committed-sdk-inputs-copied-and-verified') 'EXPECTED_REJECTION'
            Require ($proof.checks -cnotcontains 'working-tree-sdk-snapshot-copied-and-verified') 'EXPECTED_REJECTION'
            $entry['failureCode'] = $ExpectedCode
        } else {
            Require ($command.exitCode -eq 0 -and $proof.status -ceq 'PASSED') 'PROOF'
            $sourceCheck = 'committed-sdk-inputs-copied-and-verified'
            if (($WorkingTreeSdk -and !$HeadOnly) -or $ForceWorkingTree) {
                Require ($proof.baseCommit -ceq $commit -and !$proof.ContainsKey('repositoryCommit') -and
                    $proof.sourceSnapshotSha256 -cmatch '^[0-9a-f]{64}$' -and $proof.sourceMode -ceq 'WorkingTreeSdk') 'PROOF'
                $sourceCheck = 'working-tree-sdk-snapshot-copied-and-verified'
            } else { Require ($proof.repositoryCommit -ceq $commit) 'PROOF' }
            foreach ($check in @($sourceCheck,
                'package-identity-provenance-notices-and-closed-content',
                'exact-package-only-fresh-cache-locked-restore',
                'evaluated-consumer-imports-and-compile-inputs-isolated',
                'release-consumer-executed-contracts-and-package-assembly',
                'missing-exact-version-rejected-NU1102', 'populated-unmapped-decoy-rejected-NU1101',
                'lock-drift-rejected-NU1004')) { Require ($proof.checks -ccontains $check) 'PROOF' }
            Require ($proof.expectedRejections.Count -eq 3 -and
                @($proof.expectedRejections | Where-Object exitCode -EQ 0).Count -eq 0) 'PROOF'
            if ($TemplateContracts -and !$HeadOnly) { Require ($proof.checks -ccontains 'external-template-provider-contracts-executed') 'PROOF' }
            $entry['proofChecks'] = $proof.checks.Count
        }
        $entry['status'] = 'PASSED'
    } catch {
        $codes = @('PROCESS', 'TIMEOUT', 'OUTPUT', 'DISCLOSURE', 'CLEANUP', 'GIT_TRACE', 'HELPER_EXECUTED', 'EXPECTED_REJECTION', 'PROOF')
        $entry['testFailureCode'] = if ($_.Exception.Message -cin $codes) { $_.Exception.Message } else { 'INTERNAL' }
    }
    $cases.Add($entry)
}

try {
    Require (![IO.Path]::Exists($work)) 'OWNERSHIP'
    [void][IO.Directory]::CreateDirectory($work)
    [IO.File]::WriteAllText((Join-Path $work 'ownership'), $owner)
    [IO.File]::WriteAllText((Join-Path $work 'empty.gitconfig'), '')
    $comparison = if ($IsWindows) { [StringComparison]::OrdinalIgnoreCase } else { [StringComparison]::Ordinal }
    Require (!$work.StartsWith($RepositoryRoot + [IO.Path]::DirectorySeparatorChar, $comparison)) 'ISOLATION'
    $phase = 'VERIFY_ROOT'
    $top = Git @('-C', $RepositoryRoot, 'rev-parse', '--show-toplevel')
    Require ([string]::Equals([IO.Path]::GetFullPath($top), $RepositoryRoot, $comparison)) 'REPOSITORY'
    Require ((Git @('-C', $RepositoryRoot, 'config', '--local', '--no-includes', '--get', 'remote.origin.url')) -cin @(
        'https://github.com/fi8Git/logicgate-sitefactory', 'https://github.com/fi8Git/logicgate-sitefactory.git')) 'REPOSITORY'
    $commit = Git @('-C', $RepositoryRoot, 'rev-parse', '--verify', 'HEAD^{commit}')
    Require ($commit -cmatch '^[0-9a-f]{40}$') 'REPOSITORY'
    if ($WorkingTreeSdk) { $report['baseCommit'] = $commit } else { $report['repositoryCommit'] = $commit }
    $report['sourceMode'] = if ($WorkingTreeSdk) { 'WorkingTreeSdk' } else { 'Head' }

    # Owned local clones reuse public objects read-only; no commit or source checkout metadata is changed.
    $fixture = Join-Path $work 'checkout'
    $decoy = Join-Path $work 'decoy.git'
    $phase = 'CLONE'
    $null = Git @('clone', '--quiet', '--shared', '--no-checkout', '--', $RepositoryRoot, $fixture)
    $phase = 'CHECKOUT'
    $null = Git @('-C', $fixture, 'checkout', '--quiet', '--detach', $commit)
    $null = Git @('-C', $fixture, 'config', 'remote.origin.url', 'https://github.com/fi8Git/logicgate-sitefactory.git')
    $overlayCreated = [Collections.Generic.List[string]]::new()
    if ($WorkingTreeSdk) {
        # Snapshot only SDK C# inputs in the owned fixture, never the caller's Git metadata or other projects.
        $sdkPaths = (Git @('-C', $RepositoryRoot, 'ls-files', '--cached', '--others', '--exclude-standard', '--',
            'src/LogicGate.SiteFactory.ModuleSdk/')) -split '\r?\n'
        foreach ($relative in $sdkPaths | Where-Object { $_.EndsWith('.cs', [StringComparison]::Ordinal) }) {
            $source = Join-Path $RepositoryRoot $relative
            $target = Join-Path $fixture $relative
            Require (([IO.File]::GetAttributes($source) -band [IO.FileAttributes]::ReparsePoint) -eq 0) 'INPUTS'
            if (![IO.File]::Exists($target)) { $overlayCreated.Add($relative) }
            [void][IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($target))
            [IO.File]::Copy($source, $target, $true)
            Require ((Get-FileHash -LiteralPath $source).Hash -ceq (Get-FileHash -LiteralPath $target).Hash) 'COPY'
        }
    }
    $phase = 'DECOY'
    $null = Git @('clone', '--quiet', '--shared', '--bare', '--', $RepositoryRoot, $decoy)
    $phase = 'CASES'
    $empty = Join-Path $work 'empty'
    [void][IO.Directory]::CreateDirectory($empty)
    $trace = Join-Path $work 'forbidden-git-trace'
    $poison = @{
        GIT_DIR = $decoy; GIT_COMMON_DIR = $decoy; GIT_WORK_TREE = $empty
        GIT_INDEX_FILE = (Join-Path $work 'foreign-index'); GIT_OBJECT_DIRECTORY = $empty
        GIT_ALTERNATE_OBJECT_DIRECTORIES = $empty; GIT_CONFIG = (Join-Path $work 'empty.gitconfig')
        GIT_CONFIG_GLOBAL = (Join-Path $work 'empty.gitconfig'); GIT_CONFIG_SYSTEM = (Join-Path $work 'empty.gitconfig')
        GIT_CONFIG_COUNT = '2'; GIT_CONFIG_KEY_0 = 'remote.origin.url'; GIT_CONFIG_VALUE_0 = 'https://example.invalid/foreign'
        GIT_CONFIG_KEY_1 = 'core.bare'; GIT_CONFIG_VALUE_1 = 'true'; GIT_CONFIG_PARAMETERS = "'core.bare=true'"
        GIT_NAMESPACE = 'foreign'; GIT_REPLACE_REF_BASE = 'refs/foreign/'; GIT_SHALLOW_FILE = (Join-Path $work 'foreign-shallow')
        GIT_CEILING_DIRECTORIES = $work; GIT_EXEC_PATH = $empty; GIT_ATTR_SOURCE = 'refs/foreign'
        GIT_TRACE = $trace; GIT_TRACE_SETUP = $trace
    }
    Invoke-Case 'ambient-git-redirection-and-config-isolated' $fixture $poison '' @($trace)
    Invoke-Case 'requested-subdirectory-is-not-checkout-root' (Join-Path $fixture 'src/LogicGate.SiteFactory.ModuleSdk') @{} 'REPOSITORY'

    $null = Git @('-C', $fixture, 'config', 'remote.origin.url', 'https://example.invalid/foreign')
    $spoof = @{ GIT_CONFIG_COUNT = '1'; GIT_CONFIG_KEY_0 = 'remote.origin.url';
        GIT_CONFIG_VALUE_0 = 'https://github.com/fi8Git/logicgate-sitefactory.git' }
    Invoke-Case 'foreign-origin-not-hidden-by-environment-config' $fixture $spoof 'REPOSITORY'
    $null = Git @('-C', $fixture, 'config', 'remote.origin.url', 'https://github.com/fi8Git/logicgate-sitefactory.git')

    $publicMarker = Join-Path $work 'forbidden-public-helper'
    $postgresMarker = Join-Path $work 'forbidden-postgresql-helper'
    $publicHelper = Join-Path $fixture 'scripts/public-ci/PublicCiProof.ps1'
    $postgresHelper = Join-Path $fixture 'tests/PostgreSql/Public/PostgreSqlProof.ps1'
    [IO.File]::AppendAllText($publicHelper, "`n[IO.File]::WriteAllText(`$env:SFS1_PUBLIC_HELPER_MARKER, 'P2_UNTRUSTED_HELPER')`n")
    [IO.File]::AppendAllText($postgresHelper, "`n[IO.File]::WriteAllText(`$env:SFS1_POSTGRES_HELPER_MARKER, 'P2_UNTRUSTED_HELPER')`n")
    Invoke-Case 'modified-ci-helpers-never-executed' $fixture @{
        SFS1_PUBLIC_HELPER_MARKER = $publicMarker; SFS1_POSTGRES_HELPER_MARKER = $postgresMarker
    } '' @($publicMarker, $postgresMarker) 'HELPER_EXECUTED'
    # Restore only fixture files created above, not files in the caller's checkout.
    $null = Git @('-C', $fixture, 'checkout', $commit, '--',
        'scripts/public-ci/PublicCiProof.ps1', 'tests/PostgreSql/Public/PostgreSqlProof.ps1')

    if ($WorkingTreeSdk) {
        $project = 'src/LogicGate.SiteFactory.ModuleSdk/LogicGate.SiteFactory.ModuleSdk.csproj'
        [IO.File]::AppendAllText((Join-Path $fixture $project), "`n<!-- Must remain committed even in snapshot mode. -->`n")
        Invoke-Case 'working-tree-mode-refuses-project-drift' $fixture @{} 'INPUTS'
        $null = Git @('-C', $fixture, 'checkout', $commit, '--', 'src/LogicGate.SiteFactory.ModuleSdk/')
        foreach ($relative in $overlayCreated) { [IO.File]::Delete((Join-Path $fixture $relative)) }
    }

    $sdkSource = Join-Path $fixture 'src/LogicGate.SiteFactory.ModuleSdk/ModuleDescriptor.cs'
    [IO.File]::AppendAllText($sdkSource, "`n// P2 local input drift must still be refused.`n")
    Invoke-Case 'input-drift-still-refused-with-git-poison' $fixture $poison 'INPUTS' @($trace) -HeadOnly
    $expectedCases = if ($WorkingTreeSdk) { 6 } else { 5 }
    if ($Provenance) {
        $sdkRelative = 'src/LogicGate.SiteFactory.ModuleSdk/ModuleDescriptor.cs'
        $protectedRelative = 'Directory.Build.props'
        $null = Git @('-C', $fixture, 'checkout', $commit, '--', $sdkRelative)
        Invoke-Case 'head-clean-provenance' $fixture @{} -HeadOnly
        foreach ($marker in @('assume-unchanged', 'skip-worktree')) {
            foreach ($mode in @('Head', 'WorkingTreeSdk')) {
                $relative = if ($mode -ceq 'Head') { $sdkRelative } else { $protectedRelative }
                $null = Git @('-C', $fixture, 'update-index', ('--' + $marker), '--', $relative)
                $indexBefore = (Get-FileHash -LiteralPath (Join-Path $fixture '.git/index')).Hash
                $comment = if ($mode -ceq 'Head') { "`n// Hidden source modification.`n" } else { "`n<!-- Hidden protected modification. -->`n" }
                [IO.File]::AppendAllText((Join-Path $fixture $relative), $comment)
                Invoke-Case ($mode.ToLowerInvariant() + '-hidden-' + $marker) $fixture @{} 'INPUTS' -HeadOnly -ForceWorkingTree:($mode -ceq 'WorkingTreeSdk')
                Require ((Get-FileHash -LiteralPath (Join-Path $fixture '.git/index')).Hash -ceq $indexBefore) 'INDEX_CHANGED'
                $null = Git @('-C', $fixture, 'update-index', ('--no-' + $marker), '--', $relative)
                $null = Git @('-C', $fixture, 'checkout', $commit, '--', $relative)
            }
        }
        [IO.File]::AppendAllText((Join-Path $fixture $sdkRelative), "`n// Explicit uncommitted SDK snapshot.`n")
        Invoke-Case 'working-tree-authorized-source-change' $fixture @{} -HeadOnly -ForceWorkingTree
        $null = Git @('-C', $fixture, 'checkout', $commit, '--', $sdkRelative)

        # Git itself materializes CRLF in this owned Windows-compatible checkout; no new commit is made.
        $attributes = Join-Path $fixture '.git/info/attributes'
        [IO.File]::WriteAllText($attributes, "* text eol=crlf`n")
        $null = Git @('-C', $fixture, 'config', 'core.autocrlf', 'true')
        $checkoutInputs = @('global.json', 'Directory.Build.props', 'Directory.Packages.props', 'LICENSE', 'NOTICE') +
            @((Git @('-C', $fixture, 'ls-files', '--', 'src/LogicGate.SiteFactory.ModuleSdk/')) -split '\r?\n')
        foreach ($relative in $checkoutInputs) { [IO.File]::Delete((Join-Path $fixture $relative)) }
        $null = Git (@('-C', $fixture, 'checkout-index', '--force', '--') + $checkoutInputs)
        $crlfBefore = (Get-FileHash -LiteralPath (Join-Path $fixture $sdkRelative)).Hash
        Require ([IO.File]::ReadAllText((Join-Path $fixture $sdkRelative)).Contains("`r`n")) 'CRLF'
        Invoke-Case 'head-git-crlf-checkout' $fixture @{} -HeadOnly
        Require ((Get-FileHash -LiteralPath (Join-Path $fixture $sdkRelative)).Hash -ceq $crlfBefore) 'CHECKOUT_CHANGED'
        [IO.File]::AppendAllText((Join-Path $fixture $sdkRelative), "`r`n// CRLF must not hide a real change.`r`n")
        Invoke-Case 'head-crlf-with-source-change' $fixture @{} 'INPUTS' -HeadOnly
        [IO.File]::Delete((Join-Path $fixture $sdkRelative))
        $null = Git @('-C', $fixture, 'checkout-index', '--force', '--', $sdkRelative)
        [IO.File]::WriteAllText($attributes, "* text eol=crlf filter=unapproved`n")
        Invoke-Case 'head-unsupported-filter-conversion' $fixture @{} 'CONVERSION' -HeadOnly
        [IO.File]::WriteAllText($attributes, "* text eol=crlf working-tree-encoding=UTF-8`n")
        Invoke-Case 'head-unsupported-encoding-conversion' $fixture @{} 'CONVERSION' -HeadOnly
        [IO.File]::Delete($attributes)
        [IO.File]::Delete((Join-Path $fixture $sdkRelative))
        $null = Git @('-C', $fixture, 'checkout-index', '--force', '--', $sdkRelative)
        $missing = Join-Path $fixture $sdkRelative
        $null = Git @('-C', $fixture, 'update-index', '--skip-worktree', '--', $sdkRelative)
        [IO.File]::Delete($missing)
        Invoke-Case 'head-missing-skip-worktree-source' $fixture @{} 'INVENTORY' -HeadOnly
        $null = Git @('-C', $fixture, 'update-index', '--no-skip-worktree', '--', $sdkRelative)
        $null = Git @('-C', $fixture, 'checkout-index', '--force', '--', $sdkRelative)
        [IO.File]::WriteAllText((Join-Path $fixture '.git/info/exclude'), "HiddenIgnored.cs`n")
        $hidden = Join-Path $fixture 'src/LogicGate.SiteFactory.ModuleSdk/HiddenIgnored.cs'
        [IO.File]::WriteAllText($hidden, "// Ignored by Git, still included by the SDK compiler.`n")
        Invoke-Case 'head-ignored-added-source' $fixture @{} 'INVENTORY' -HeadOnly
        [IO.File]::Delete($hidden)
        $expectedCases += 12
    }
    Require ($cases.Count -eq $expectedCases -and @($cases | Where-Object status -CNE 'PASSED').Count -eq 0) 'CASES'
} catch {
    $codes = @('PROCESS', 'TIMEOUT', 'OUTPUT', 'GIT', 'OWNERSHIP', 'ISOLATION', 'REPOSITORY', 'INPUTS', 'COPY',
        'INDEX_CHANGED', 'CHECKOUT_CHANGED', 'CRLF', 'CASES')
    $failure = if ($_.Exception.Message -cin $codes) { $_.Exception.Message } else { 'INTERNAL' }
} finally {
    $report['cases'] = @($cases.ToArray())
    $report['cleanup'] = 'Failed'
    try {
        Require ([IO.Path]::GetDirectoryName([IO.Path]::GetFullPath($work)) -ceq $parent -and
            [IO.Path]::GetFileName($work) -ceq ('sitefactory-packaging-tests-' + $owner)) 'CLEANUP'
        if ([IO.Directory]::Exists($work)) {
            Require (([IO.File]::GetAttributes($work) -band [IO.FileAttributes]::ReparsePoint) -eq 0 -and
                [IO.File]::ReadAllText((Join-Path $work 'ownership')) -ceq $owner) 'CLEANUP'
            [IO.Directory]::Delete($work, $true)
        }
        Require (![IO.Path]::Exists($work)) 'CLEANUP'
        $report['cleanup'] = 'Completed'
    } catch { if (!$failure) { $failure = 'CLEANUP' } }
}
if ($failure) { $report['failureCode'] = $failure; $report['failurePhase'] = $phase } else { $report['status'] = 'PASSED' }
$report | ConvertTo-Json -Depth 5
if ($failure) { exit 1 }
