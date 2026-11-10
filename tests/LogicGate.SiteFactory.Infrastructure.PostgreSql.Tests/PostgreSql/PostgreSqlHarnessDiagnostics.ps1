# Test-only diagnostics extracted from Invoke-PostgreSqlTests.ps1; no provisioning or proof orchestration.
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Write-TestProgress([string] $OutputText, [string[]] $ExpectedTests) {
    # Never echo captured lines, display names, arguments, stderr or assertion details.
    # Only fixed method names from the proof allowlist and fixed status labels can escape.
    Write-Host 'SF-TEST-PG-PROGRESS Observed console results (not a completed TRX proof):'
    foreach ($test in $ExpectedTests) {
        $pattern = '(?m)^  (Passed|Failed|Skipped) [A-Za-z0-9_.]+\.' + [regex]::Escape($test) + ' \[[^\r\n]*\]\r?$'
        $outcomes = @([regex]::Matches($OutputText, $pattern) | ForEach-Object { $_.Groups[1].Value } | Sort-Object -Unique)
        $status = if ($outcomes.Count -eq 1) { $outcomes[0] } elseif ($outcomes.Count -gt 1) { 'ConflictingResults' } else { 'NotObserved' }
        Write-Host "SF-TEST-PG-PROGRESS $test $status"
    }
    Write-Host 'SF-TEST-PG-PROGRESS NotObserved does not distinguish queued, running or unreported tests.'
}

function Invoke-TestProcess([string] $Executable, [string[]] $Arguments, [hashtable] $EnvironmentValues = @{}, [switch] $AllowFailure, [int] $TimeoutSeconds = 180, [string[]] $ExpectedTests = @()) {
    $start = [Diagnostics.ProcessStartInfo]::new($Executable)
    # The caller owns the working directory; there is no dependency on a repository layout.
    $start.WorkingDirectory = (Get-Location).Path
    $start.UseShellExecute = $false
    $start.CreateNoWindow = $true
    $start.RedirectStandardOutput = $true
    $start.RedirectStandardError = $true
    foreach ($argument in $Arguments) { $start.ArgumentList.Add($argument) }
    foreach ($entry in $EnvironmentValues.GetEnumerator()) { $start.Environment[$entry.Key] = $entry.Value }
    $process = [Diagnostics.Process]::Start($start)
    try {
        $outputTask = $process.StandardOutput.ReadToEndAsync()
        $errorTask = $process.StandardError.ReadToEndAsync()
        if (!$process.WaitForExit($TimeoutSeconds * 1000)) {
            $process.Kill($true)
            $process.WaitForExit()
            if ($ExpectedTests.Count -gt 0) {
                # Drain after termination, but never wait indefinitely for an inherited pipe.
                # Keep diagnostics on the information stream even when the caller assigns the result.
                $null = [Threading.Tasks.Task]::WaitAny([Threading.Tasks.Task[]] @($outputTask), 1000)
                $outputAvailable = $outputTask.IsCompletedSuccessfully
                $partialOutput = if ($outputAvailable) { $outputTask.GetAwaiter().GetResult() } else { '' }
                if (!$outputAvailable) { Write-Host 'SF-TEST-PG-PROGRESS Captured stdout unavailable after termination.' }
                Write-TestProgress -OutputText $partialOutput -ExpectedTests $ExpectedTests
            }
            throw 'SF-TEST-PG-PROCESS Process timeout.'
        }
        $outputText = $outputTask.GetAwaiter().GetResult()
        $errorText = $errorTask.GetAwaiter().GetResult()
        if ($process.ExitCode -ne 0 -and !$AllowFailure) {
            # Do not echo native arguments/output: they may contain a path or provider diagnostics.
            throw "SF-TEST-PG-PROCESS $Executable failed with exit $($process.ExitCode)."
        }
        return @{ Code = $process.ExitCode; Output = $outputText; Error = $errorText }
    }
    finally { $process.Dispose() }
}
