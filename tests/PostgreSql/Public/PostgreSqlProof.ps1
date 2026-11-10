# Public test-only inventories, evidence validation and owned execution (P04-A/B/C).
Set-StrictMode -Version Latest
if ($null -eq (Get-Variable PublicPostgreSqlRunOwners -Scope Script -ErrorAction SilentlyContinue)) {
    $script:PublicPostgreSqlRunOwners = [Collections.Generic.Dictionary[string, string]]::new([StringComparer]::Ordinal)
}

function Assert-PublicPostgreSqlKeys($Value, [string[]] $Keys) {
    $allowed = [Collections.Generic.HashSet[string]]::new($Keys, [StringComparer]::Ordinal)
    if ($Value -isnot [Collections.IDictionary] -or $Value.Count -ne $Keys.Count -or
        @($Value.Keys | Where-Object { !$allowed.Contains($_) }).Count -ne 0) {
        throw 'SF-TEST-PG-PARTITION Unrecognized manifest structure.'
    }
}

function Assert-PublicPostgreSqlExact([string[]] $Actual, [string[]] $Expected) {
    $unique = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    if ($Expected.Count -eq 0 -or $Actual.Count -ne $Expected.Count) { return $false }
    foreach ($name in $Actual) { if (!$unique.Add($name)) { return $false } }
    foreach ($name in $Expected) { if (!$unique.Remove($name)) { return $false } }
    return $unique.Count -eq 0
}

function Read-PublicPostgreSqlPartition([string] $Path) {
    try { $partition = [IO.File]::ReadAllText($Path) | ConvertFrom-Json -AsHashtable -ErrorAction Stop }
    catch { throw 'SF-TEST-PG-PARTITION Manifest is missing, unreadable or invalid JSON.' }
    Assert-PublicPostgreSqlKeys $partition @('schemaVersion', 'assemblies', 'expectedTests', 'shards')
    if ($partition.schemaVersion -isnot [long] -or $partition.schemaVersion -ne 1 -or
        $partition.assemblies -isnot [array] -or $partition.assemblies.Count -ne 2 -or
        $partition.expectedTests -isnot [array] -or $partition.expectedTests.Count -ne 23 -or
        $partition.shards -isnot [array] -or $partition.shards.Count -ne 5) {
        throw 'SF-TEST-PG-PARTITION Expected schema 1, two assemblies, 23 identities and five shards.'
    }
    $infrastructure = 'LogicGate.SiteFactory.Infrastructure.PostgreSql.Tests'
    $hosts = 'LogicGate.SiteFactory.Runtime.PostgreSql.Hosts.Tests'
    $projects = @{
        $infrastructure = "tests/$infrastructure/$infrastructure.csproj"
        $hosts = "tests/$hosts/$hosts.csproj"
    }
    $counts = @{ g04 = 5; g05 = 3; p01 = 8; p02 = 4; p03 = 3 }
    $assemblyNames = @()
    foreach ($assembly in $partition.assemblies) {
        Assert-PublicPostgreSqlKeys $assembly @('name', 'project', 'expectedCount')
        if ($assembly.name -isnot [string] -or !$projects.ContainsKey($assembly.name) -or
            $assembly.project -cne $projects[$assembly.name] -or $assembly.expectedCount -isnot [long] -or
            $assembly.expectedCount -ne $(if ($assembly.name -ceq $hosts) { 3 } else { 20 })) {
            throw 'SF-TEST-PG-PARTITION Invalid assembly or project.'
        }
        $assemblyNames += $assembly.name
    }
    if (!(Assert-PublicPostgreSqlExact $assemblyNames @($infrastructure, $hosts))) {
        throw 'SF-TEST-PG-PARTITION Assemblies must be distinct and complete.'
    }
    $identities = [Collections.Generic.Dictionary[string, object]]::new([StringComparer]::Ordinal)
    foreach ($test in $partition.expectedTests) {
        Assert-PublicPostgreSqlKeys $test @('assembly', 'fullyQualifiedName', 'lot', 'shard')
        if ($test.shard -isnot [string] -or !$counts.ContainsKey($test.shard) -or
            $test.shard -cnotmatch '^(g04|g05|p01|p02|p03)$' -or $test.lot -cne $test.shard.ToUpperInvariant() -or
            $test.assembly -cne $(if ($test.shard -ceq 'p03') { $hosts } else { $infrastructure }) -or
            $test.fullyQualifiedName -isnot [string] -or
            $test.fullyQualifiedName -cnotmatch ('^' + [regex]::Escape($test.assembly) + '\.PostgreSql\.[A-Za-z0-9]+\.[A-Za-z0-9]+$') -or
            !$identities.TryAdd($test.fullyQualifiedName, $test)) {
            throw 'SF-TEST-PG-PARTITION Invalid, duplicate or misallocated test identity.'
        }
    }
    $shardNames = @()
    $members = @()
    foreach ($shard in $partition.shards) {
        Assert-PublicPostgreSqlKeys $shard @('name', 'assembly', 'project', 'lot', 'expectedCount', 'filter', 'expectedTests')
        if ($shard.name -isnot [string] -or $shard.name -cnotmatch '^(g04|g05|p01|p02|p03)$' -or
            $shard.assembly -cne $(if ($shard.name -ceq 'p03') { $hosts } else { $infrastructure }) -or
            $shard.project -cne $projects[$shard.assembly] -or $shard.lot -cne $shard.name.ToUpperInvariant() -or
            $shard.expectedCount -isnot [long] -or $shard.expectedCount -ne $counts[$shard.name] -or
            $shard.expectedTests -isnot [array] -or $shard.expectedTests.Count -ne $counts[$shard.name]) {
            throw 'SF-TEST-PG-PARTITION Invalid shard allocation or count.'
        }
        $expected = @($partition.expectedTests | Where-Object shard -CEQ $shard.name | ForEach-Object fullyQualifiedName)
        if (!(Assert-PublicPostgreSqlExact $shard.expectedTests $expected)) {
            throw 'SF-TEST-PG-PARTITION Shard membership does not match its exact inventory.'
        }
        $filter = if ($shard.name -ceq 'g04') {
            'Category=PostgreSql&(' + (($shard.expectedTests | ForEach-Object { "FullyQualifiedName=$_" }) -join '|') + ')'
        } else { 'Category=PostgreSql&ExtractionLot=' + $shard.lot }
        if ($shard.filter -cne $filter) { throw 'SF-TEST-PG-PARTITION Incorrect selection filter.' }
        $shardNames += $shard.name
        $members += $shard.expectedTests
    }
    if (!(Assert-PublicPostgreSqlExact $shardNames @('g04', 'g05', 'p01', 'p02', 'p03')) -or
        !(Assert-PublicPostgreSqlExact $members @($partition.expectedTests | ForEach-Object fullyQualifiedName))) {
        throw 'SF-TEST-PG-PARTITION Expected a complete, disjoint partition.'
    }
    return $partition
}

function Assert-PublicPostgreSqlDiscovery([string] $OutputText, [string[]] $ExpectedTests, [int] $ExitCode = 0) {
    if ($ExitCode -ne 0) { throw 'SF-TEST-PG-DISCOVERY Discovery process failed.' }
    # VSTest --list-tests prints fully qualified identities, one indented line per Fact.
    # Do not shorten names, discard duplicates or publish captured native output.
    $discovered = @([regex]::Matches($OutputText, '(?m)^ +(\S[^\r\n]*)\r?$') |
        ForEach-Object { $_.Groups[1].Value })
    if (@($discovered | Where-Object { $_ -cnotmatch '^[A-Za-z0-9_]+(?:\.[A-Za-z0-9_]+)+$' }).Count -ne 0 -or
        !(Assert-PublicPostgreSqlExact $discovered $ExpectedTests)) {
        throw 'SF-TEST-PG-DISCOVERY Discovered identities do not match the exact nonempty inventory.'
    }
}

# These paths are owned by the caller's fresh campaign. Reject links at EVERY existing
# ancestor, not just lexical traversal. This is not protection against a concurrent
# hostile filesystem writer; P04-C must keep this private area under its ownership.
function Resolve-PublicPostgreSqlEvidencePath([string] $Root, [string] $Relative) {
    try {
        if ($Relative -cnotmatch '^[A-Za-z0-9_-]+(?:\.[A-Za-z0-9_-]+)*(?:/[A-Za-z0-9_-]+(?:\.[A-Za-z0-9_-]+)*)*$') { throw 'Invalid path.' }
        $rootPath = [IO.Path]::TrimEndingDirectorySeparator([IO.Path]::GetFullPath($Root))
        if (![IO.Directory]::Exists($rootPath)) { throw 'Missing root.' }
        $path = [IO.Path]::GetFullPath([IO.Path]::Combine($rootPath, $Relative.Replace('/', [IO.Path]::DirectorySeparatorChar)))
        $comparison = if ($IsWindows) { [StringComparison]::OrdinalIgnoreCase } else { [StringComparison]::Ordinal }
        if (!$path.StartsWith($rootPath + [IO.Path]::DirectorySeparatorChar, $comparison)) { throw 'Escape.' }
        for ($cursor = $path; $cursor; $cursor = [IO.Path]::GetDirectoryName($cursor)) {
            if ([IO.Path]::Exists($cursor) -and ([IO.File]::GetAttributes($cursor) -band [IO.FileAttributes]::ReparsePoint)) { throw 'Link.' }
        }
        return $path
    } catch { throw 'SF-TEST-PG-PATH' }
}

function Write-PublicPostgreSqlJson([string] $Path, $Value, [switch] $CreateNew) {
    $mode = if ($CreateNew) { [IO.FileMode]::CreateNew } else { [IO.FileMode]::Create }
    $stream = [IO.FileStream]::new($Path, $mode, [IO.FileAccess]::Write, [IO.FileShare]::None)
    try {
        $bytes = [Text.UTF8Encoding]::new($false).GetBytes(($Value | ConvertTo-Json -Depth 16) + "`n")
        $stream.Write($bytes, 0, $bytes.Length)
    } finally { $stream.Dispose() }
}

function New-PublicPostgreSqlCampaign([string] $Parent, [string] $ManifestPath,
    [string] $Commit = '', [string] $GitState = 'Unknown',
    [string] $Sdk = '', [string] $Runtime = '') {
    if ($GitState -cnotin @('Unknown', 'Clean', 'Dirty') -or ($Commit -and $Commit -cnotmatch '^[0-9a-f]{40}$') -or
        ($Sdk -and $Sdk -cnotmatch '^\d{1,3}\.\d{1,3}\.\d{1,4}$') -or
        ($Runtime -and $Runtime -cnotmatch '^\d{1,3}\.\d{1,3}\.\d{1,4}$')) { throw 'SF-TEST-PG-PROVENANCE' }
    $partition = Read-PublicPostgreSqlPartition $ManifestPath
    $id = [Guid]::NewGuid().ToString('N')
    $root = Resolve-PublicPostgreSqlEvidencePath $Parent ('pg-proof-' + $id)
    if ([IO.Path]::Exists($root)) { throw 'SF-TEST-PG-PROVENANCE' }
    $null = [IO.Directory]::CreateDirectory($root)
    if (!$IsWindows) { [IO.File]::SetUnixFileMode($root, [IO.UnixFileMode]::UserRead -bor [IO.UnixFileMode]::UserWrite -bor [IO.UnixFileMode]::UserExecute) }
    foreach ($directory in @('incoming', 'native', 'proof', 'metrics', 'discovery', 'observations')) {
        $null = [IO.Directory]::CreateDirectory((Resolve-PublicPostgreSqlEvidencePath $root $directory))
    }
    $campaign = @{
        Id = $id; Root = $root; Partition = $partition
        Invocations = [Collections.Generic.Dictionary[string, object]]::new([StringComparer]::Ordinal)
        RunIds = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
        Manifest = [ordered]@{
            schemaVersion = 1; campaignId = $id; provenance = 'SyntheticFixture'; databaseProof = $false
            commit = $(if ($Commit) { $Commit } else { $null }); gitState = $GitState
            platform = $(if ($IsWindows) { 'Windows' } elseif ($IsLinux) { 'Linux' } else { 'Other' })
            sdk = $(if ($Sdk) { $Sdk } else { $null }); runtime = $(if ($Runtime) { $Runtime } else { $null })
            postgreSql = $null; imageDigest = $null; cleanup = 'NotObserved'
            status = 'FAILED'; code = $null; shards = @($partition.shards | ForEach-Object {
                [ordered]@{ shard = $_.name; assembly = $_.assembly; expectedTests = @($_.expectedTests)
                    status = 'NOT_EXECUTED'; code = 'SF-TEST-PG-NOT-EXECUTED'; proof = $null }
            })
        }
    }
    Write-PublicPostgreSqlJson (Resolve-PublicPostgreSqlEvidencePath $root 'manifest.json') $campaign.Manifest -CreateNew
    return $campaign
}

# No subprocess is started here. P04-B only exercises receipts and windows on fixtures.
# P04-C must supply receipts from owned, supervised invocations; neither a TRX nor
# its modification time authenticates that association.
function Start-PublicPostgreSqlShardProof($Campaign, [string] $Shard, [DateTimeOffset] $StartedUtc = [DateTimeOffset]::UtcNow) {
    $selected = @($Campaign.Partition.shards | Where-Object name -CEQ $Shard)
    if ($selected.Count -ne 1 -or $Campaign.Invocations.ContainsKey($Shard)) { throw 'SF-TEST-PG-PROVENANCE' }
    $path = Resolve-PublicPostgreSqlEvidencePath $Campaign.Root ('incoming/' + $Shard + '.trx')
    if ([IO.Path]::Exists($path)) { throw 'SF-TEST-PG-PROVENANCE' }
    $invocation = @{ CampaignId = $Campaign.Id; Id = [Guid]::NewGuid().ToString('N')
        Shard = $selected[0]; StartedUtc = $StartedUtc; TrxRelative = 'incoming/' + $Shard + '.trx'; Finished = $false }
    $Campaign.Invocations.Add($Shard, $invocation)
    return $invocation
}

function Read-PublicPostgreSqlTrxBytes([byte[]] $Bytes) {
    if ($Bytes.Length -eq 0 -or $Bytes.Length -gt 4194304) { throw 'SF-TEST-PG-XML' }
    $settings = [Xml.XmlReaderSettings]::new()
    $settings.DtdProcessing = [Xml.DtdProcessing]::Prohibit
    $settings.XmlResolver = $null
    $settings.MaxCharactersInDocument = 4194304
    $settings.MaxCharactersFromEntities = 1024
    $stream = [IO.MemoryStream]::new($Bytes, $false)
    $reader = $null
    try {
        $reader = [Xml.XmlReader]::Create($stream, $settings)
        $document = [Xml.XmlDocument]::new()
        $document.XmlResolver = $null
        $document.Load($reader)
        return ,$document
    } catch { throw 'SF-TEST-PG-XML' }
    finally { if ($null -ne $reader) { $reader.Dispose() }; $stream.Dispose() }
}

function Convert-PublicPostgreSqlTime([string] $Text) {
    $value = [DateTimeOffset]::MinValue
    if ($Text -cnotmatch '^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(?:\.\d{1,7})?(?:Z|[+-]\d{2}:\d{2})$' -or
        ![DateTimeOffset]::TryParse($Text, [Globalization.CultureInfo]::InvariantCulture, [Globalization.DateTimeStyles]::None, [ref] $value)) {
        throw 'SF-TEST-PG-TIME'
    }
    return $value
}

function Convert-PublicPostgreSqlDuration([string] $Text) {
    $value = [TimeSpan]::Zero
    if (![TimeSpan]::TryParseExact($Text, 'c', [Globalization.CultureInfo]::InvariantCulture, [ref] $value) -or $value -lt [TimeSpan]::Zero) {
        throw 'SF-TEST-PG-TIME'
    }
    return $value
}

function Convert-PublicPostgreSqlGuid([string] $Text) {
    $value = [Guid]::Empty
    if (![Guid]::TryParseExact($Text, 'D', [ref] $value)) { throw 'SF-TEST-PG-LINK' }
    return $value.ToString('D')
}

function Assert-PublicPostgreSqlTrx($Document, $Invocation, [DateTimeOffset] $FinishedUtc) {
    $ns = 'http://microsoft.com/schemas/VisualStudio/TeamTest/2010'
    $root = $Document.DocumentElement
    if ($null -eq $root -or $root.LocalName -cne 'TestRun' -or $root.NamespaceURI -cne $ns) { throw 'SF-TEST-PG-STRUCTURE' }
    $manager = [Xml.XmlNamespaceManager]::new($Document.NameTable)
    $manager.AddNamespace('t', $ns)
    foreach ($name in @('Times', 'TestDefinitions', 'Results', 'TestEntries', 'ResultSummary')) {
        if ($root.SelectNodes('t:' + $name, $manager).Count -ne 1) { throw 'SF-TEST-PG-STRUCTURE' }
    }
    $runId = [Guid]::Empty
    if (![Guid]::TryParseExact($root.GetAttribute('id'), 'D', [ref] $runId)) { throw 'SF-TEST-PG-PROVENANCE' }
    $times = $root.SelectSingleNode('t:Times', $manager)
    $start = Convert-PublicPostgreSqlTime $times.GetAttribute('start')
    $finish = Convert-PublicPostgreSqlTime $times.GetAttribute('finish')
    if ($start -lt $Invocation.StartedUtc -or $finish -lt $start -or $finish -gt $FinishedUtc) { throw 'SF-TEST-PG-PROVENANCE' }
    $definitions = @($root.SelectNodes('t:TestDefinitions/t:UnitTest', $manager))
    $results = @($root.SelectNodes('t:Results/t:UnitTestResult', $manager))
    $entries = @($root.SelectNodes('t:TestEntries/t:TestEntry', $manager))
    $expected = @($Invocation.Shard.expectedTests)
    # No hidden duplicate results/definitions outside their native containers or foreign namespaces.
    foreach ($pair in @(@('UnitTest', $definitions.Count), @('UnitTestResult', $results.Count), @('TestEntry', $entries.Count))) {
        if ($Document.SelectNodes('//*[local-name()="' + $pair[0] + '"]').Count -ne $pair[1] -or $pair[1] -ne $expected.Count) { throw 'SF-TEST-PG-INVENTORY' }
    }
    $identities = [Collections.Generic.Dictionary[string, object]]::new([StringComparer]::Ordinal)
    $executionIds = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    $names = @()
    foreach ($definition in $definitions) {
        $methods = $definition.SelectNodes('t:TestMethod', $manager)
        $executions = $definition.SelectNodes('t:Execution', $manager)
        if ($methods.Count -ne 1 -or $executions.Count -ne 1) { throw 'SF-TEST-PG-STRUCTURE' }
        $method = $methods[0]
        $name = $method.GetAttribute('className') + '.' + $method.GetAttribute('name')
        # VSTest writes storage lower-case on Windows; compare DLL basenames, not free display names.
        foreach ($location in @($method.GetAttribute('codeBase'), $definition.GetAttribute('storage'))) {
            if (($location.Replace('\', '/') -split '/')[-1] -ine ($Invocation.Shard.assembly + '.dll')) { throw 'SF-TEST-PG-ASSEMBLY' }
        }
        $id = Convert-PublicPostgreSqlGuid $definition.GetAttribute('id')
        $executionId = Convert-PublicPostgreSqlGuid $executions[0].GetAttribute('id')
        if (!$identities.TryAdd($id, @{ Name = $name; ExecutionId = $executionId }) -or !$executionIds.Add($executionId)) { throw 'SF-TEST-PG-LINK' }
        $names += $name
    }
    if (!(Assert-PublicPostgreSqlExact $names $expected)) { throw 'SF-TEST-PG-INVENTORY' }
    $seen = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    $rows = @()
    foreach ($result in $results) {
        $id = Convert-PublicPostgreSqlGuid $result.GetAttribute('testId')
        $executionId = Convert-PublicPostgreSqlGuid $result.GetAttribute('executionId')
        if (!$identities.ContainsKey($id) -or !$seen.Add($id) -or $executionId -cne $identities[$id].ExecutionId) { throw 'SF-TEST-PG-LINK' }
        $duration = Convert-PublicPostgreSqlDuration $result.GetAttribute('duration')
        $resultStart = Convert-PublicPostgreSqlTime $result.GetAttribute('startTime')
        $resultEnd = Convert-PublicPostgreSqlTime $result.GetAttribute('endTime')
        if ($resultStart -lt $start -or $resultEnd -lt $resultStart -or $resultEnd -gt $finish -or $duration -gt ($finish - $start)) { throw 'SF-TEST-PG-TIME' }
        $rows += [ordered]@{ fullyQualifiedName = $identities[$id].Name; outcome = $result.GetAttribute('outcome'); durationMs = [decimal] $duration.TotalMilliseconds }
    }
    $seen.Clear()
    foreach ($entry in $entries) {
        $id = Convert-PublicPostgreSqlGuid $entry.GetAttribute('testId')
        $executionId = Convert-PublicPostgreSqlGuid $entry.GetAttribute('executionId')
        if (!$identities.ContainsKey($id) -or !$seen.Add($id) -or $executionId -cne $identities[$id].ExecutionId) { throw 'SF-TEST-PG-LINK' }
    }
    $summary = $root.SelectSingleNode('t:ResultSummary', $manager)
    $counterNodes = $summary.SelectNodes('t:Counters', $manager)
    if ($counterNodes.Count -ne 1) { throw 'SF-TEST-PG-COUNTERS' }
    $counters = [ordered]@{}
    $counterNames = @('total', 'executed', 'passed', 'failed', 'error', 'timeout', 'aborted', 'inconclusive', 'passedButRunAborted',
        'notRunnable', 'notExecuted', 'disconnected', 'warning', 'completed', 'inProgress', 'pending')
    if ($counterNodes[0].Attributes.Count -ne $counterNames.Count) { throw 'SF-TEST-PG-COUNTERS' }
    foreach ($counter in $counterNames) {
        $text = $counterNodes[0].GetAttribute($counter)
        $number = 0
        if ($text -cnotmatch '^[0-9]{1,9}$' -or ![int]::TryParse($text, [ref] $number)) { throw 'SF-TEST-PG-COUNTERS' }
        $target = if ($counter -cin @('total', 'executed', 'passed')) { $expected.Count } else { 0 }
        if ($number -ne $target) { throw 'SF-TEST-PG-COUNTERS' }
        $counters[$counter] = $number
    }
    if ($summary.GetAttribute('outcome') -cne 'Completed' -or @($rows | Where-Object outcome -CNE Passed).Count -ne 0) { throw 'SF-TEST-PG-OUTCOME' }
    [string[]] $orderedNames = $names
    [Array]::Sort($orderedNames, [StringComparer]::Ordinal)
    return @{ RunId = $runId.ToString('D'); Counters = $counters; DurationMs = [decimal] ($finish - $start).TotalMilliseconds
        Results = @($orderedNames | ForEach-Object { $name = $_; $rows | Where-Object fullyQualifiedName -CEQ $name }) }
}

function Assert-PublicPostgreSqlNoSensitiveContent([string] $Text, [string[]] $KnownSensitiveMarkers = @(),
    [Xml.XmlDocument] $Document = $null) {
    foreach ($marker in $KnownSensitiveMarkers) {
        if (!$marker -or $Text.Contains($marker, [StringComparison]::Ordinal)) { throw 'SF-TEST-PG-SENSITIVE' }
    }
    # Conservative detectable patterns, not a claim to recognize all possible secrets.
    if ($Text -match '(?i)(\b(password|pwd|connection\s*string|credential|secret|token)\s*[:=]|postgres(?:ql)?://|-----BEGIN .*PRIVATE KEY|\bSELECT\b[^\r\n]{0,160}\bFROM\b|\bINSERT\s+INTO\b|\bUPDATE\b[^\r\n]{0,80}\bSET\b|Npgsql\.[A-Za-z]*Exception|\bSQLSTATE\b|\bat System\.)') {
        throw 'SF-TEST-PG-SENSITIVE'
    }
    if ($null -ne $Document) {
        # Keep the serialized scan above (including names/markup). Entity escaping
        # there is NOT sufficient: compare literal markers with the already-secure
        # DOM's decoded values. InnerText also joins text/CDATA across child nodes,
        # without recursive reserialization or another parser/entity expansion.
        Assert-PublicPostgreSqlNoSensitiveContent $Document.get_DocumentElement().get_InnerText() $KnownSensitiveMarkers
        # XPath's attribute axis omits xmlns declarations; use the DOM collection
        # so ALL attribute values are inspected, including those namespaces.
        foreach ($element in $Document.SelectNodes('//*')) {
            foreach ($attribute in $element.get_Attributes()) {
                Assert-PublicPostgreSqlNoSensitiveContent $attribute.get_Value() $KnownSensitiveMarkers
            }
        }
        foreach ($node in $Document.SelectNodes('//text() | //comment() | //processing-instruction()')) {
            # Comments and PIs are not included in the aggregate InnerText.
            Assert-PublicPostgreSqlNoSensitiveContent $node.get_Value() $KnownSensitiveMarkers
        }
    }
}

function Read-PublicPostgreSqlMeasurements($Campaign) {
    $directory = Resolve-PublicPostgreSqlEvidencePath $Campaign.Root 'metrics'
    $allowed = @($Campaign.Partition.expectedTests | Where-Object { $_.lot -cin @('G04', 'P01', 'P02') } | ForEach-Object fullyQualifiedName)
    if ($allowed.Count -ne 17) { throw 'SF-TEST-PG-METRICS' }
    [string[]] $names = $allowed
    [Array]::Sort($names, [StringComparer]::Ordinal)
    $measurements = @()
    foreach ($name in $names) {
        $method = ($name -split '\.')[-1]
        $pattern = '^' + [regex]::Escape($method) + '\.([1-9][0-9]{0,9})\.metrics$'
        [string[]] $files = @([IO.Directory]::GetFiles($directory) | Where-Object { [IO.Path]::GetFileName($_) -cmatch $pattern })
        [Array]::Sort($files, [StringComparer]::Ordinal)
        if ($files.Count -gt 128) { throw 'SF-TEST-PG-METRICS' }
        $totals = @{}
        foreach ($file in $files) {
            $leaf = [IO.Path]::GetFileName($file)
            $null = $leaf -cmatch $pattern
            if ([long] $Matches[1] -gt [int]::MaxValue) { throw 'SF-TEST-PG-METRICS' }
            $path = Resolve-PublicPostgreSqlEvidencePath $Campaign.Root ('metrics/' + $leaf)
            if ([IO.FileInfo]::new($path).Length -gt 1048576) { throw 'SF-TEST-PG-METRICS' }
            $lines = [IO.File]::ReadAllLines($path)
            if ($lines.Count -eq 0 -or $lines.Count -gt 10000) { throw 'SF-TEST-PG-METRICS' }
            $pending = @{}
            foreach ($line in $lines) {
                if ($line -cnotmatch '^(Test|Apply|Bootstrap)\|(Start|End)\|([0-9]{1,15})\|([0-9]{1,12})$') { throw 'SF-TEST-PG-METRICS' }
                $phase = $Matches[1]; $state = $Matches[2]; $utcMs = [long] $Matches[3]; $ms = [long] $Matches[4]
                if ($utcMs -gt 253402300799999 -or ($state -ceq 'Start' -and $ms -ne 0)) { throw 'SF-TEST-PG-METRICS' }
                if (!$totals.ContainsKey($phase)) { $totals[$phase] = @{ started = 0; ended = 0; durationMs = 0L; startUtcMs = $utcMs; endUtcMs = $null } }
                if (!$pending.ContainsKey($phase)) { $pending[$phase] = [Collections.Generic.Queue[long]]::new() }
                $total = $totals[$phase]
                if ($state -ceq 'Start') {
                    $pending[$phase].Enqueue($utcMs); $total.started++
                    $total.startUtcMs = [Math]::Min($total.startUtcMs, $utcMs)
                } else {
                    if ($pending[$phase].Count -eq 0 -or $utcMs -lt $pending[$phase].Dequeue()) { throw 'SF-TEST-PG-METRICS' }
                    $total.ended++; $total.durationMs += $ms
                    $total.endUtcMs = if ($null -eq $total.endUtcMs) { $utcMs } else { [Math]::Max($total.endUtcMs, $utcMs) }
                }
            }
        }
        foreach ($phase in @('Apply', 'Bootstrap', 'Test')) {
            if ($totals.ContainsKey($phase)) {
                $total = $totals[$phase]
                $measurements += [ordered]@{ fullyQualifiedName = $name; phase = $phase; started = $total.started; ended = $total.ended
                    durationMs = $total.durationMs; startUtcMs = $total.startUtcMs; endUtcMs = $total.endUtcMs
                    completeness = $(if ($total.started -eq $total.ended) { 'Complete' } else { 'Partial' }) }
            }
        }
    }
    return $measurements
}

function Save-PublicPostgreSqlProof($Campaign, $Invocation, $Receipt, [string] $DiscoveryOutput,
    [int] $DiscoveryExitCode = 0, [string[]] $KnownSensitiveMarkers = @()) {
    # Only reconstructed, typed, fixed/allowlisted data is returned or saved. Never serialize
    # caller objects, receipt extras, exceptions, raw XML, streams or unapproved metric names.
    $shard = $Invocation.Shard.name
    if (!$Campaign.Invocations.ContainsKey($shard) -or ![object]::ReferenceEquals($Campaign.Invocations[$shard], $Invocation) -or $Invocation.Finished) {
        throw 'SF-TEST-PG-PROVENANCE'
    }
    $proof = [ordered]@{ schemaVersion = 1; campaignId = $Campaign.Id; invocationId = $Invocation.Id
        provenance = $Campaign.Manifest.provenance; databaseProof = $false; shard = $shard; assembly = $Invocation.Shard.assembly
        expectedTests = @($Invocation.Shard.expectedTests); status = 'FAILED'; code = 'SF-TEST-PG-PROVENANCE'
        stage = 'Provenance'; discovery = 'NotObserved'; completeness = 'Incomplete'; counters = $null; durationMs = $null
        results = @(); metrics = @(); native = $null; cleanup = 'NotObserved'; incomingCleanup = 'NotObserved' }
    $path = $null; $bytes = $null
    try {
        if ($Invocation.CampaignId -cne $Campaign.Id -or $Receipt -isnot [Collections.IDictionary] -or
            $Receipt.campaignId -cne $Campaign.Id -or $Receipt.invocationId -cne $Invocation.Id -or
            $Receipt.finishedUtc -isnot [DateTimeOffset] -or $Receipt.finishedUtc -lt $Invocation.StartedUtc -or
            $Receipt.exitCode -isnot [int] -or $Receipt.status -cnotin @('Completed', 'Timeout', 'Cancelled')) { throw 'SF-TEST-PG-PROVENANCE' }
        $proof.durationMs = [decimal] ($Receipt.finishedUtc - $Invocation.StartedUtc).TotalMilliseconds
        $proof.stage = 'Process'
        if ($Receipt.status -ceq 'Timeout') { throw 'SF-TEST-PG-TIMEOUT' }
        if ($Receipt.status -ceq 'Cancelled') { throw 'SF-TEST-PG-CANCELLED' }
        if ($Receipt.exitCode -ne 0) { throw 'SF-TEST-PG-EXIT' }
        if ($Campaign.Manifest.provenance -ceq 'RealPostgreSql' -and
            (!$Receipt.Contains('processCleanup') -or $Receipt.processCleanup -cne 'Completed' -or
             !$Receipt.Contains('elapsedMs') -or $Receipt.elapsedMs -isnot [ValueType] -or
             ![double]::IsFinite([double] $Receipt.elapsedMs) -or $Receipt.elapsedMs -lt 0 -or $Receipt.elapsedMs -gt 360000)) { throw 'SF-TEST-PG-PROVENANCE' }
        if ($Campaign.Manifest.provenance -ceq 'RealPostgreSql') {
            $proof.processDurationMs = $Receipt.elapsedMs; $proof.processCleanup = $Receipt.processCleanup
        }
        $proof.processStatus=$Receipt.status;$proof.processExitCode=$Receipt.exitCode
        $proof.stage = 'Discovery'
        Assert-PublicPostgreSqlDiscovery $DiscoveryOutput $Invocation.Shard.expectedTests $DiscoveryExitCode
        $proof.discovery = 'Passed'
        $proof.stage = 'Xml'
        if ($Invocation.TrxRelative -cne ('incoming/' + $shard + '.trx')) { throw 'SF-TEST-PG-PATH' }
        $path = Resolve-PublicPostgreSqlEvidencePath $Campaign.Root $Invocation.TrxRelative
        if (![IO.File]::Exists($path)) { throw 'SF-TEST-PG-MISSING' }
        # Bounded read, with no concurrent sharing. Do not use ReadAllBytes on an unbounded input.
        $stream = [IO.FileStream]::new($path, [IO.FileMode]::Open, [IO.FileAccess]::Read, [IO.FileShare]::None)
        try {
            if ($stream.Length -eq 0 -or $stream.Length -gt 4194304) { throw 'SF-TEST-PG-XML' }
            $bytes = [byte[]]::new([int] $stream.Length)
            $stream.ReadExactly($bytes, 0, $bytes.Length)
        } finally { $stream.Dispose() }
        $document = Read-PublicPostgreSqlTrxBytes $bytes
        $proof.stage = 'Trx'
        $validated = Assert-PublicPostgreSqlTrx $document $Invocation $Receipt.finishedUtc
        if (!$Campaign.RunIds.Add($validated.RunId) -or !$script:PublicPostgreSqlRunOwners.TryAdd($validated.RunId, $Campaign.Id)) { throw 'SF-TEST-PG-PROVENANCE' }
        $proof.counters = $validated.Counters; $proof.results = $validated.Results
        $proof.durationMs = $validated.DurationMs; $proof.completeness = 'Complete'
        $proof.stage = 'SensitiveContent'
        Assert-PublicPostgreSqlNoSensitiveContent $document.OuterXml $KnownSensitiveMarkers -Document $document
        if ($Invocation.Shard.name -ceq 'p03' -and $Campaign.Manifest.Contains('observationsComplete') -and !$Campaign.Manifest.observationsComplete) {
            $proof.stage='Observations';throw 'SF-TEST-PG-OBSERVATIONS'
        }
        $proof.stage = 'Metrics'
        $proof.metrics = @(Read-PublicPostgreSqlMeasurements $Campaign)
        $proof.stage = 'Archive'; $proof.status = 'PASSED'; $proof.code = $null
    } catch {
        $code = $_.Exception.Message
        $allowedCodes = @('SF-TEST-PG-PROVENANCE', 'SF-TEST-PG-PATH', 'SF-TEST-PG-TIMEOUT', 'SF-TEST-PG-CANCELLED',
            'SF-TEST-PG-EXIT', 'SF-TEST-PG-DISCOVERY', 'SF-TEST-PG-MISSING', 'SF-TEST-PG-XML', 'SF-TEST-PG-STRUCTURE',
            'SF-TEST-PG-INVENTORY', 'SF-TEST-PG-ASSEMBLY', 'SF-TEST-PG-LINK', 'SF-TEST-PG-TIME',
            'SF-TEST-PG-COUNTERS', 'SF-TEST-PG-OUTCOME', 'SF-TEST-PG-SENSITIVE', 'SF-TEST-PG-METRICS', 'SF-TEST-PG-OBSERVATIONS')
        $proof.code = if ($code -cin $allowedCodes) { $code } elseif ($code.StartsWith('SF-TEST-PG-DISCOVERY', [StringComparison]::Ordinal)) { 'SF-TEST-PG-DISCOVERY' } else { 'SF-TEST-PG-PROOF' }
        $proof.status = 'FAILED'
    }
    # Remove only our exact incoming file, including on ordinary red campaigns. Never
    # retain sensitive temporaries or claim this is secure physical erasure.
    try {
        $incoming = Resolve-PublicPostgreSqlEvidencePath $Campaign.Root ('incoming/' + $shard + '.trx')
        if ([IO.File]::Exists($incoming)) { [IO.File]::Delete($incoming) }
        $proof.incomingCleanup = 'Completed'
    } catch { $proof.status = 'FAILED'; $proof.incomingCleanup = 'Failed'; if ($null -eq $proof.code) { $proof.code = 'SF-TEST-PG-CLEANUP' } }
    if ($proof.status -ceq 'PASSED') {
        try {
            $native = Resolve-PublicPostgreSqlEvidencePath $Campaign.Root ('native/' + $shard + '.trx')
            $destination = [IO.FileStream]::new($native, [IO.FileMode]::CreateNew, [IO.FileAccess]::Write, [IO.FileShare]::None)
            try { $destination.Write($bytes, 0, $bytes.Length) } finally { $destination.Dispose() }
            $proof.native = 'native/' + $shard + '.trx'
        } catch { $proof.status = 'FAILED'; $proof.code = 'SF-TEST-PG-ARCHIVE' }
    }
    $relative = 'proof/' + $shard + '.json'
    Write-PublicPostgreSqlJson (Resolve-PublicPostgreSqlEvidencePath $Campaign.Root $relative) $proof -CreateNew
    $record = @($Campaign.Manifest.shards | Where-Object shard -CEQ $shard)[0]
    $record.status = $proof.status; $record.code = $proof.code; $record.proof = $relative
    $Campaign.Manifest.status = if ($Campaign.Manifest.provenance -ceq 'SyntheticFixture' -and
        @($Campaign.Manifest.shards | Where-Object status -CNE PASSED).Count -eq 0) { 'PASSED' } else { 'FAILED' }
    Write-PublicPostgreSqlJson (Resolve-PublicPostgreSqlEvidencePath $Campaign.Root 'manifest.json') $Campaign.Manifest
    $Invocation.Finished = $true
    return $proof
}

# Private redirected-pipe protocol. No shell interpolation of arguments; EOF asks the
# bridge to cancel, then its existing job/subreaper cleans ALL owned descendants.
function Invoke-PublicPostgreSqlProcess([string] $Bridge, [string] $Directory, [string] $Executable,
    [string[]] $Arguments, [hashtable] $EnvironmentValues = @{}, [ValidateRange(1,360)] [int] $TimeoutSeconds = 180) {
    $start = [Diagnostics.ProcessStartInfo]::new('dotnet')
    $start.WorkingDirectory = $Directory; $start.UseShellExecute = $false; $start.CreateNoWindow = $true
    $start.RedirectStandardInput = $true; $start.RedirectStandardOutput = $true; $start.RedirectStandardError = $true
    $start.ArgumentList.Add($Bridge); $start.ArgumentList.Add('--sitefactory-postgresql-harness')
    $process = $null
    try {
        $process = [Diagnostics.Process]::Start($start)
        $output = $process.StandardOutput.ReadLineAsync()
        $errorRead = $process.StandardError.ReadToEndAsync()
        $request = @{ Directory = $Directory; Executable = $Executable; Arguments = @($Arguments)
            TimeoutSeconds = $TimeoutSeconds; Environment = $EnvironmentValues } | ConvertTo-Json -Depth 4 -Compress
        $process.StandardInput.WriteLine($request); $process.StandardInput.Flush()
        # 10s bridge/handshake + command budget + existing 10s scope cleanup + 5s transport.
        $line = $output.WaitAsync([TimeSpan]::FromSeconds($TimeoutSeconds + 25)).GetAwaiter().GetResult()
        if (!$line -or $line.Length -gt 24000000) { throw 'Invalid transport.' }
        $result = $line | ConvertFrom-Json -AsHashtable
        Assert-PublicPostgreSqlKeys $result @('Status','ExitCode','Output','Error','ElapsedMs','Cleanup','Code')
        if ($result.Status -cnotin @('Completed','Timeout','Cancelled','Failed') -or $result.ExitCode -isnot [long] -or
            $result.Cleanup -cnotin @('Completed','Failed') -or $result.Output -isnot [string] -or $result.Error -isnot [string] -or
            $result.ElapsedMs -lt 0 -or $result.ElapsedMs -gt ($TimeoutSeconds + 10) * 1000) { throw 'Invalid transport.' }
        if (!$process.WaitForExit(5000) -or $process.ExitCode -ne 0 -or $errorRead.GetAwaiter().GetResult().Length -ne 0) { throw 'Invalid transport.' }
        return $result
    } catch {
        return @{ Status = 'Failed'; ExitCode = -1; Output = ''; Error = ''; ElapsedMs = 0; Cleanup = 'Failed'; Code = 'SF-TEST-PG-PROCESS' }
    } finally {
        if ($null -ne $process) {
            $process.StandardInput.Close() # cancellation independent of caller tokens
            if (!$process.WaitForExit(15000)) {
                $process.Kill() # exact owned bridge only; its death closes the private supervisor pipe/job
                if (!$process.WaitForExit(5000)) { throw 'SF-TEST-PG-CLEANUP' }
                throw 'SF-TEST-PG-CLEANUP'
            }
            $process.Dispose()
        }
    }
}

function Assert-PublicPostgreSqlProcess($Result) {
    if ($Result.Status -cne 'Completed' -or $Result.ExitCode -ne 0 -or $Result.Cleanup -cne 'Completed') {
        if ($Result.Status -ceq 'Timeout') { throw 'SF-TEST-PG-TIMEOUT' }
        if ($Result.Status -ceq 'Cancelled') { throw 'SF-TEST-PG-CANCELLED' }
        throw 'SF-TEST-PG-PROCESS'
    }
}

function Invoke-PublicPostgreSqlInventories($Partition, [scriptblock] $Runner) {
    $observations = @()
    foreach ($assembly in $Partition.assemblies) {
        $expected = @($Partition.expectedTests | Where-Object assembly -CEQ $assembly.name | ForEach-Object fullyQualifiedName)
        $observations += Invoke-PublicPostgreSqlDiscoveryQuery $assembly.project 'Category=PostgreSql' $expected $Runner
    }
    foreach ($shard in $Partition.shards) {
        $observations += Invoke-PublicPostgreSqlDiscoveryQuery $shard.project $shard.filter $shard.expectedTests $Runner
        if ($shard.name -cne 'g04') {
            $observations += Invoke-PublicPostgreSqlDiscoveryQuery $shard.project ('ExtractionLot=' + $shard.lot) $shard.expectedTests $Runner
        }
    }
    return $observations
}

function Invoke-PublicPostgreSqlDiscoveryQuery([string] $Project, [string] $Filter, [string[]] $Expected, [scriptblock] $Runner) {
    $result = & $Runner $Project $Filter
    try {
        Assert-PublicPostgreSqlProcess $result
        Assert-PublicPostgreSqlDiscovery $result.Output $Expected ([int] $result.ExitCode)
    } catch { throw 'SF-TEST-PG-DISCOVERY' }
    # Only fixed identities/filters survive; raw discovery transport remains in memory.
    return @{ project = $Project; filter = $Filter; identities = @($Expected); status = 'PASSED'; durationMs = $result.ElapsedMs }
}

function Protect-PublicPostgreSqlDirectory([string] $Directory) {
    if ($IsWindows) {
        $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
        try {
            $acl = [Security.AccessControl.DirectorySecurity]::new()
            $acl.SetOwner($identity.User); $acl.SetAccessRuleProtection($true, $false)
            $acl.AddAccessRule([Security.AccessControl.FileSystemAccessRule]::new($identity.User,
                [Security.AccessControl.FileSystemRights]::FullControl,
                ([Security.AccessControl.InheritanceFlags]::ContainerInherit -bor [Security.AccessControl.InheritanceFlags]::ObjectInherit),
                [Security.AccessControl.PropagationFlags]::None, [Security.AccessControl.AccessControlType]::Allow))
            [IO.FileSystemAclExtensions]::SetAccessControl([IO.DirectoryInfo]::new($Directory), $acl)
        } finally { $identity.Dispose() }
    } else {
        [IO.File]::SetUnixFileMode($Directory, [IO.UnixFileMode]::UserRead -bor [IO.UnixFileMode]::UserWrite -bor [IO.UnixFileMode]::UserExecute)
    }
}

function Remove-PublicPostgreSqlOwnedDirectory([string] $Directory, [string] $Parent, [string] $Name) {
    $path = [IO.Path]::GetFullPath($Directory)
    if ([IO.Path]::GetDirectoryName($path) -cne [IO.Path]::TrimEndingDirectorySeparator([IO.Path]::GetFullPath($Parent)) -or
        [IO.Path]::GetFileName($path) -cne $Name -or $Name -cnotmatch '^sitefactory-p04c-[0-9a-f]{32}$') { throw 'SF-TEST-PG-CLEANUP' }
    $null = Resolve-PublicPostgreSqlEvidencePath $Parent ($Name + '/ownership')
    [IO.Directory]::Delete($path, $true)
    if ([IO.Path]::Exists($path)) { throw 'SF-TEST-PG-CLEANUP' }
}

# P04-B itself runs inside the SAME owned-process mechanism as dotnet tests.
# The private payload is not an evidence artifact. No secret value appears in argv.
function Remove-PublicPostgreSqlOwnedEvidence($Campaign, [string] $Relative) {
    # One local, non-sharing deletion attempt; no retries, waiting for locks,
    # recursion, caller cancellation or deletion through a link. The owned root
    # is not a guarantee against a concurrent attacker with the same OS identity.
    $path=Resolve-PublicPostgreSqlEvidencePath $Campaign.Root $Relative
    if([IO.Directory]::Exists($path)){throw 'SF-TEST-PG-CLEANUP'}
    if([IO.File]::Exists($path)) {
        $file=[IO.FileStream]::new($path,[IO.FileMode]::Open,[IO.FileAccess]::ReadWrite,[IO.FileShare]::None,1,[IO.FileOptions]::DeleteOnClose)
        $file.Dispose()
    }
    if([IO.Path]::Exists($path)){throw 'SF-TEST-PG-CLEANUP'}
}

function Save-PublicPostgreSqlArchiveFailure($Campaign, $Invocation, $Receipt, [string] $Code) {
    if(!$Campaign.Invocations.ContainsKey($Invocation.Shard.name) -or
        ![object]::ReferenceEquals($Campaign.Invocations[$Invocation.Shard.name],$Invocation)){throw 'SF-TEST-PG-PROVENANCE'}
    $allowed=@('SF-TEST-PG-PROCESS','SF-TEST-PG-TIMEOUT','SF-TEST-PG-CANCELLED','SF-TEST-PG-PATH','SF-TEST-PG-ARCHIVE','SF-TEST-PG-CLEANUP')
    if($Code -cnotin $allowed){$Code='SF-TEST-PG-ARCHIVE'}
    $record=@($Campaign.Manifest.shards|Where-Object shard -CEQ $Invocation.Shard.name)[0]
    $record.status='FAILED';$record.code=$Code;$record.proof=$null
    $Campaign.Manifest.status='FAILED';$Campaign.Manifest.databaseProof=$false;$Campaign.Manifest.code=$Code
    $proof=[ordered]@{schemaVersion=1;campaignId=$Campaign.Id;invocationId=$Invocation.Id;provenance=$Campaign.Manifest.provenance;
        databaseProof=$false;shard=$record.shard;assembly=$record.assembly;expectedTests=@($record.expectedTests);
        status='FAILED';code=$Code;stage='Archive';discovery='NotObserved';completeness='Incomplete';counters=$null;
        durationMs=$null;results=@();metrics=@();native=$null;cleanup='NotObserved';incomingCleanup='NotObserved'}
    # The successful test process is a different observation from failed proof
    # finalization. Never serialize its free streams, exceptions or receipt extras.
    if($Receipt -is [Collections.IDictionary]) {
        if($Receipt.status -cin @('Completed','Timeout','Cancelled')){$proof.processStatus=$Receipt.status}
        if($Receipt.exitCode -is [int]){$proof.processExitCode=$Receipt.exitCode}
        if($Receipt.Contains('processCleanup') -and $Receipt.processCleanup -cin @('Completed','Failed')){$proof.processCleanup=$Receipt.processCleanup}
        if($Receipt.Contains('elapsedMs') -and $Receipt.elapsedMs -is [ValueType] -and [double]::IsFinite([double]$Receipt.elapsedMs) -and
            $Receipt.elapsedMs -ge 0 -and $Receipt.elapsedMs -le 370000){$proof.processDurationMs=$Receipt.elapsedMs}
    }
    try {
        Remove-PublicPostgreSqlOwnedEvidence $Campaign ('incoming/'+$record.shard+'.trx')
        Remove-PublicPostgreSqlOwnedEvidence $Campaign ('native/'+$record.shard+'.trx')
        $proof.incomingCleanup='Completed'
    } catch {
        $proof.incomingCleanup='Failed';$proof.code='SF-TEST-PG-CLEANUP';$record.code=$proof.code;$Campaign.Manifest.code=$proof.code;$Campaign.Manifest.cleanup='Failed'
    }
    $relative='proof/'+$record.shard+'.json'
    try {Write-PublicPostgreSqlJson (Resolve-PublicPostgreSqlEvidencePath $Campaign.Root $relative) $proof;$record.proof=$relative}
    catch {$record.code='SF-TEST-PG-ARCHIVE';$Campaign.Manifest.code=$record.code;$proof.code=$record.code}
    # Persistence failures are explicit, not a successful archive or raw exception.
    try {Write-PublicPostgreSqlJson (Resolve-PublicPostgreSqlEvidencePath $Campaign.Root 'manifest.json') $Campaign.Manifest}
    catch {$Campaign.Manifest.code='SF-TEST-PG-FINALIZATION';$proof.code='SF-TEST-PG-FINALIZATION'}
    $Invocation.Finished=$true
    return $proof
}

function Invoke-PublicPostgreSqlShardArchive($Campaign, $Invocation, $Receipt, [string] $Bridge,
    [string] $Repository, [string] $PrivateDirectory, [string[]] $KnownSensitiveMarkers, [string] $ObservationsSource = '',
    [Threading.CancellationToken] $CancellationToken = [Threading.CancellationToken]::None) {
    $payloadPath=$null;$archiveAccepted=$false;$failureCode='SF-TEST-PG-ARCHIVE';$proof=$null
    if(!$Campaign.Invocations.ContainsKey($Invocation.Shard.name) -or $Invocation.Finished -or
        ![object]::ReferenceEquals($Campaign.Invocations[$Invocation.Shard.name],$Invocation)){throw 'SF-TEST-PG-PROVENANCE'}
    $Invocation.Launched=$true
    $record=@($Campaign.Manifest.shards|Where-Object shard -CEQ $Invocation.Shard.name)[0]
    $record.status='FAILED';$record.code='SF-TEST-PG-ARCHIVE'
    try {
    $CancellationToken.ThrowIfCancellationRequested()
    $payloadPath = Resolve-PublicPostgreSqlEvidencePath $PrivateDirectory 'archive.json'
    $payload = @{ id = $Campaign.Id; root = $Campaign.Root; partition = $Campaign.Partition; manifest = $Campaign.Manifest
        runIds = @($Campaign.RunIds); invocation = $Invocation; receipt = $Receipt; markers = @($KnownSensitiveMarkers); observationsSource=$ObservationsSource }
    Write-PublicPostgreSqlJson $payloadPath $payload
    $scriptPath = Join-Path $PSScriptRoot 'PostgreSqlProof.ps1'
    $quoted = "'" + $payloadPath.Replace("'", "''") + "'"
    $script = ". '" + $scriptPath.Replace("'", "''") + "'`n" + @'
        $ErrorActionPreference = 'Stop'
        try {
            $data = [IO.File]::ReadAllText(PAYLOAD) | ConvertFrom-Json -AsHashtable -DateKind String
            $invocation = $data.invocation; $invocation.StartedUtc = [DateTimeOffset]::Parse($invocation.StartedUtc)
            $receipt = $data.receipt; $receipt.finishedUtc = [DateTimeOffset]::Parse($receipt.finishedUtc); $receipt.exitCode = [int] $receipt.exitCode
            $campaign = @{ Id=$data.id; Root=$data.root; Partition=$data.partition; Manifest=$data.manifest
                Invocations=[Collections.Generic.Dictionary[string,object]]::new([StringComparer]::Ordinal)
                RunIds=[Collections.Generic.HashSet[string]]::new([string[]] $data.runIds, [StringComparer]::Ordinal) }
            $campaign.Invocations.Add($invocation.Shard.name, $invocation)
            if ($invocation.Shard.name -ceq 'p03') {
                try { $campaign.Manifest.observationsComplete = Save-PublicPostgreSqlObservations $campaign $data.observationsSource $data.markers }
                catch { $campaign.Manifest.observationsComplete = $false }
            }
            $discovery = ($invocation.Shard.expectedTests | ForEach-Object { '    ' + $_ }) -join "`n"
            $proof = Save-PublicPostgreSqlProof $campaign $invocation $receipt $discovery -KnownSensitiveMarkers $data.markers
            Write-PublicPostgreSqlJson PAYLOAD @{manifest=$campaign.Manifest;runIds=@($campaign.RunIds);proof=$proof}
            exit 0
        } catch { exit 1 }
'@.Replace('PAYLOAD', $quoted)
    $result = Invoke-PublicPostgreSqlProcess $Bridge $Repository 'pwsh' @('-NoProfile','-NonInteractive','-OutputFormat','Text',
        '-EncodedCommand', [Convert]::ToBase64String([Text.Encoding]::Unicode.GetBytes($script))) -TimeoutSeconds 30
    Assert-PublicPostgreSqlProcess $result
    $CancellationToken.ThrowIfCancellationRequested()
    $saved = [IO.File]::ReadAllText($payloadPath) | ConvertFrom-Json -AsHashtable
    Assert-PublicPostgreSqlKeys $saved @('manifest','runIds','proof')
    if($saved.proof.campaignId -cne $Campaign.Id -or $saved.proof.invocationId -cne $Invocation.Id -or
        $saved.proof.shard -cne $Invocation.Shard.name -or $saved.proof.status -cnotin @('PASSED','FAILED')){throw 'SF-TEST-PG-ARCHIVE'}
    $proofPath=Resolve-PublicPostgreSqlEvidencePath $Campaign.Root ('proof/'+$Invocation.Shard.name+'.json')
    if(![IO.File]::Exists($proofPath)){throw 'SF-TEST-PG-ARCHIVE'}
    $persisted=[IO.File]::ReadAllText($proofPath)|ConvertFrom-Json -AsHashtable
    if(($persisted|ConvertTo-Json -Depth 16 -Compress) -cne ($saved.proof|ConvertTo-Json -Depth 16 -Compress)){throw 'SF-TEST-PG-ARCHIVE'}
    if($saved.proof.status -ceq 'PASSED' -and ($saved.proof.native -cne ('native/'+$Invocation.Shard.name+'.trx') -or
        ![IO.File]::Exists((Resolve-PublicPostgreSqlEvidencePath $Campaign.Root $saved.proof.native)))){throw 'SF-TEST-PG-ARCHIVE'}
    $Campaign.Manifest = $saved.manifest
    if($saved.proof.incomingCleanup -ceq 'Failed'){$Campaign.Manifest.cleanup='Failed'}
    foreach ($id in $saved.runIds) { $null = $Campaign.RunIds.Add($id) }
    $Invocation.Finished = $true
    $proof=$saved.proof;$archiveAccepted=$true
    } catch {
        if($_.Exception.GetBaseException() -is [OperationCanceledException]){$failureCode='SF-TEST-PG-CANCELLED'}
        elseif($_.Exception.Message -cin @('SF-TEST-PG-PROCESS','SF-TEST-PG-TIMEOUT','SF-TEST-PG-CANCELLED','SF-TEST-PG-PATH')){$failureCode=$_.Exception.Message}
    } finally {
        if($archiveAccepted) {
            try {Remove-PublicPostgreSqlOwnedEvidence $Campaign ('incoming/'+$Invocation.Shard.name+'.trx')}
            catch {$archiveAccepted=$false;$failureCode='SF-TEST-PG-CLEANUP'}
        }
        if(!$archiveAccepted){$proof=Save-PublicPostgreSqlArchiveFailure $Campaign $Invocation $Receipt $failureCode}
        if($payloadPath) {
            try {Remove-PublicPostgreSqlOwnedEvidence @{Root=$PrivateDirectory} 'archive.json'}
            catch {$proof=Save-PublicPostgreSqlArchiveFailure $Campaign $Invocation $Receipt 'SF-TEST-PG-CLEANUP'}
        }
    }
    return $proof
}

function Complete-PublicPostgreSqlCampaign($Campaign, [string] $Cleanup, [bool] $ObservationsComplete) {
    if ($Cleanup -cnotin @('Completed','Failed')) { throw 'SF-TEST-PG-CLEANUP' }
    if($Campaign.Manifest.cleanup -ceq 'Failed'){$Cleanup='Failed'}
    $Campaign.Manifest.cleanup = $Cleanup
    if($Campaign.Manifest.provenance -ceq 'RealPostgreSql' -and !$ObservationsComplete -and !$Campaign.Manifest.code){$Campaign.Manifest.code='SF-TEST-PG-OBSERVATIONS'}
    $complete = $Campaign.Manifest.provenance -ceq 'RealPostgreSql' -and $Cleanup -ceq 'Completed' -and $ObservationsComplete -and
        $Campaign.Manifest.postgreSql -eq 180006 -and
        $Campaign.Manifest.imageDigest -ceq 'sha256:3725f4e2499eef5134592b3b4ab79a543ed7f8e533b05b5b637af926630f6650' -and
        @($Campaign.Manifest.shards | Where-Object status -CNE PASSED).Count -eq 0 -and !$Campaign.Manifest.code
    $Campaign.Manifest.databaseProof = $complete
    $Campaign.Manifest.status = if ($complete) { 'PASSED' } else { 'FAILED' }
    Write-PublicPostgreSqlJson (Resolve-PublicPostgreSqlEvidencePath $Campaign.Root 'manifest.json') $Campaign.Manifest
}

function Invoke-PublicPostgreSqlFinalization($Campaign, [string] $Bridge, [string] $Repository,
    [string] $Cleanup, [bool] $ObservationsComplete) {
    $payloadPath=$null
    try {
    $payloadPath = Resolve-PublicPostgreSqlEvidencePath $Campaign.Root 'incoming/finalization.json'
    Write-PublicPostgreSqlJson $payloadPath @{Root=$Campaign.Root;Manifest=$Campaign.Manifest;Cleanup=$Cleanup;ObservationsComplete=$ObservationsComplete} -CreateNew
    $script = ". '" + (Join-Path $PSScriptRoot 'PostgreSqlProof.ps1').Replace("'","''") + "'`n" + @'
        $ErrorActionPreference='Stop'
        try {
            $data=[IO.File]::ReadAllText(PAYLOAD)|ConvertFrom-Json -AsHashtable
            $campaign=@{Root=$data.Root;Manifest=$data.Manifest}
            Complete-PublicPostgreSqlCampaign $campaign $data.Cleanup $data.ObservationsComplete
            [IO.File]::Delete(PAYLOAD)
            exit 0
        } catch {exit 1}
'@.Replace('PAYLOAD', "'"+$payloadPath.Replace("'","''")+"'")
    $result=Invoke-PublicPostgreSqlProcess $Bridge $Repository 'pwsh' @('-NoProfile','-NonInteractive','-OutputFormat','Text',
        '-EncodedCommand',[Convert]::ToBase64String([Text.Encoding]::Unicode.GetBytes($script))) -TimeoutSeconds 30
    Assert-PublicPostgreSqlProcess $result
    $Campaign.Manifest=[IO.File]::ReadAllText((Resolve-PublicPostgreSqlEvidencePath $Campaign.Root 'manifest.json'))|ConvertFrom-Json -AsHashtable
    } catch {
        # Do not retry the unavailable archiver. The orchestrator can still record
        # red state using only fixed metadata, without parsing any incoming TRX.
        $Campaign.Manifest.status='FAILED';$Campaign.Manifest.databaseProof=$false
        if(!$Campaign.Manifest.code){$Campaign.Manifest.code='SF-TEST-PG-FINALIZATION'}
        if($Campaign.Manifest.cleanup -cne 'Failed'){$Campaign.Manifest.cleanup=$Cleanup}
        try {Write-PublicPostgreSqlJson (Resolve-PublicPostgreSqlEvidencePath $Campaign.Root 'manifest.json') $Campaign.Manifest}
        catch {throw 'SF-TEST-PG-FINALIZATION'}
    } finally {
        if($payloadPath) {
            try {Remove-PublicPostgreSqlOwnedEvidence $Campaign 'incoming/finalization.json'}
            catch {$Campaign.Manifest.status='FAILED';$Campaign.Manifest.databaseProof=$false;$Campaign.Manifest.code='SF-TEST-PG-CLEANUP';$Campaign.Manifest.cleanup='Failed'
                try {Write-PublicPostgreSqlJson (Resolve-PublicPostgreSqlEvidencePath $Campaign.Root 'manifest.json') $Campaign.Manifest}
                catch {throw 'SF-TEST-PG-FINALIZATION'}}
        }
    }
}

function Convert-PublicPostgreSqlObservationFields($Value, [hashtable] $Rules) {
    $safe = [ordered]@{}
    $keys = [string[]] @($Rules.Keys); [Array]::Sort($keys, [StringComparer]::Ordinal)
    foreach ($key in $keys) {
        if (!$Value.Contains($key)) { throw 'SF-TEST-PG-OBSERVATIONS' }
        $field = $Value[$key]; $rule = $Rules[$key]
        $valid = if ($rule -is [array]) { $field -is [string] -and $field -cin $rule } else { switch ($rule) {
            integer { $field -is [long] -and $field -ge 0 -and $field -le [long]::MaxValue }
            number { $field -is [ValueType] -and $field -isnot [bool] -and [double]::IsFinite([double] $field) -and $field -ge 0 -and $field -le 1e15 }
            boolean { $field -is [bool] }
            optionalBoolean { $null -eq $field -or $field -is [bool] }
            optionalStatus { $null -eq $field -or ($field -is [long] -and $field -ge 100 -and $field -le 599) }
            trace { $null -eq $field -or ($field -is [string] -and $field -cmatch '^[0-9a-f]{32}$') }
            span { $null -eq $field -or ($field -is [string] -and $field -cmatch '^[0-9a-f]{16}$') }
            default { $field -is [string] -and $field -cin $rule }
        } }
        if (!$valid) { throw 'SF-TEST-PG-OBSERVATIONS' }
        $safe[$key] = $field
    }
    return $safe
}

function Save-PublicPostgreSqlObservations($Campaign, [string] $Source, [string[]] $KnownSensitiveMarkers = @()) {
    $safePath = Resolve-PublicPostgreSqlEvidencePath ([IO.Path]::GetDirectoryName($Source)) ([IO.Path]::GetFileName($Source))
    $file = [IO.FileInfo]::new($safePath)
    if (!$file.Exists -or $file.Length -eq 0 -or $file.Length -gt 8388608) { throw 'SF-TEST-PG-OBSERVATIONS' }
    $text = [IO.File]::ReadAllText($safePath)
    Assert-PublicPostgreSqlNoSensitiveContent $text $KnownSensitiveMarkers
    try { $data = $text | ConvertFrom-Json -AsHashtable }
    catch { throw 'SF-TEST-PG-OBSERVATIONS' }
    if ($data.schemaVersion -ne 1 -or $data.correction -cne 'P03-CORR-004' -or $data.workers -ne 32 -or $data.requestsPerWorker -ne 24 -or
        $data.stopwatchFrequency -isnot [long] -or $data.stopwatchFrequency -le 0 -or $data.http -isnot [array] -or $data.http.Count -gt 768) { throw 'SF-TEST-PG-OBSERVATIONS' }
    # P03 has no per-event campaign UUID. Bind its fresh, owned run directory and
    # process-local timestamp window instead; never claim a DB session correlation.
    if (!$Campaign.Manifest.Contains('observationRun') -or $Campaign.Manifest.observationRun -cnotmatch '^[0-9a-f]{32}$' -or
        [IO.Path]::GetFileName([IO.Path]::GetDirectoryName($safePath)) -cne $Campaign.Manifest.observationRun -or
        [IO.Path]::GetFileName($safePath) -cne 'observations.json' -or $data.resources -isnot [Collections.IDictionary]) { throw 'SF-TEST-PG-OBSERVATIONS' }
    $before = Convert-PublicPostgreSqlObservationFields $data.resources.before @{timestamp='integer';processId='integer'}
    $after = Convert-PublicPostgreSqlObservationFields $data.resources.after @{timestamp='integer';processId='integer'}
    $windowValid = $before.processId -gt 0 -and $before.processId -eq $after.processId -and $before.timestamp -gt 0 -and $after.timestamp -gt $before.timestamp
    $exceptionKinds = @('NONE','UNKNOWN','CANCELLED','TIMEOUT_EXCEPTION','HTTP_REQUEST_EXCEPTION','POSTGRES_EXCEPTION','NPGSQL_EXCEPTION','IO_EXCEPTION','INVALID_OPERATION_EXCEPTION','OTHER_EXCEPTION')
    $httpRules = @{ requestId='integer'; route=@('/','/health','/health/ready'); startTimestamp='integer'; endTimestamp='integer'; durationMilliseconds='number'
        completionOrder='integer'; status='optionalStatus'; exceptionKind=$exceptionKinds; serverStartTimestamp='integer'; serverEndTimestamp='integer'
        serverExceptionKind=$exceptionKinds; startedBefore='optionalBoolean'; startedAfter='optionalBoolean'; stoppingBefore='optionalBoolean'; stoppingAfter='optionalBoolean'
        requestAborted='boolean'; openAttempts='integer'; openedConnections='integer'; closedConnections='integer'; traceId='trace'; spanId='span'; serverActivity='boolean'
        refusalCategory=@('NONE','UNKNOWN','CANCELLED','HOST_NOT_STARTED','HOST_STOPPING','QUEUE_TIMEOUT','PROBE_BLOCKED','NOT_INITIALIZED','UNEXPECTED_ERROR') }
    $requests = @(); $ids = [Collections.Generic.HashSet[long]]::new(); $complete = $data.http.Count -eq 768 -and $windowValid
    $activities = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    $orders = [Collections.Generic.HashSet[long]]::new()
    $byId = @{}; $routeCounts = @{'/health/ready'=0;'/health'=0;'/'=0}
    foreach ($request in @($data.http | Sort-Object requestId)) {
        $safe = Convert-PublicPostgreSqlObservationFields $request $httpRules
        if ($safe.requestId -ge 768 -or !$ids.Add($safe.requestId) -or $request.readinessEvents -isnot [array] -or $request.readinessEvents.Count -gt 3) { throw 'SF-TEST-PG-OBSERVATIONS' }
        $events = @()
        foreach ($event in $request.readinessEvents) {
            $safeEvent = Convert-PublicPostgreSqlObservationFields $event @{name=@('sitefactory.readiness.refresh.completed','sitefactory.readiness.http.decision');traceId='trace';spanId='span'}
            $rules = if ($event.name -ceq 'sitefactory.readiness.refresh.completed') {
                @{startTicks='integer';endTicks='integer';result='optionalBoolean';outcome=@('NOT_INITIALIZED','QUEUE_TIMEOUT','READY','BLOCKED','CANCELLED','UNEXPECTED_ERROR')
                    diagnosticCode=@('SF-DB-STARTUP-001','SF-DB-STARTUP-003','SF-DB-STARTUP-004','SF-DB-STARTUP-005','SF-DB-STARTUP-006','SF-DB-STARTUP-007','SF-DB-STARTUP-008','SF-DB-STARTUP-009',
                        'SF-SCHEMA-STARTUP-001','SF-SCHEMA-STARTUP-004','SF-SCHEMA-STARTUP-005','SF-SCHEMA-STARTUP-006','SF-SCHEMA-STARTUP-007','SF-SCHEMA-STARTUP-008','SF-SCHEMA-STARTUP-009') }
            } else { @{site=@('HEALTH_READY','HEALTH_LEGACY','TRAFFIC_GATE');outcome=@('HOST_NOT_STARTED','HOST_STOPPING','REFRESH_REJECTED','ACCEPTED','THREW');refreshResult='optionalBoolean'} }
            $tags = @{} + $event.tags
            if ($event.name -ceq 'sitefactory.readiness.refresh.completed' -and $null -eq $tags.diagnosticCode) {
                $tags.diagnosticCode = 'NONE'; $rules.diagnosticCode += 'NONE'
            }
            $safeEvent.tags = Convert-PublicPostgreSqlObservationFields $tags $rules
            if ($safeEvent.tags.Contains('diagnosticCode') -and $safeEvent.tags.diagnosticCode -ceq 'NONE') { $safeEvent.tags.diagnosticCode = $null }
            $events += $safeEvent
        }
        $safe.readinessEvents = $events
        $refresh = @($events | Where-Object name -CEQ 'sitefactory.readiness.refresh.completed')
        $decision = @($events | Where-Object name -CEQ 'sitefactory.readiness.http.decision')
        $site = switch ($safe.route) { '/' { 'TRAFFIC_GATE' }; '/health' { 'HEALTH_LEGACY' }; '/health/ready' { 'HEALTH_READY' } }
        $routeCounts[$safe.route]++
        $expectedRoute = @('/health/ready','/health','/')[([int][Math]::Floor($safe.requestId / 24) + ($safe.requestId % 24)) % 3]
        $ready = $safe.status -eq 200 -and $safe.exceptionKind -ceq 'NONE' -and $safe.serverExceptionKind -ceq 'NONE' -and $safe.serverActivity -and
            $safe.route -ceq $expectedRoute -and $safe.traceId -and $safe.spanId -and
            $safe.traceId -cne ('0'*32) -and $safe.spanId -cne ('0'*16) -and $activities.Add($safe.traceId+'/'+$safe.spanId) -and
            $safe.completionOrder -ge 1 -and $safe.completionOrder -le 768 -and $orders.Add($safe.completionOrder) -and
            $safe.startTimestamp -ge $before.timestamp -and $safe.endTimestamp -ge $safe.startTimestamp -and $safe.endTimestamp -le $after.timestamp -and
            $safe.serverStartTimestamp -gt 0 -and $safe.serverEndTimestamp -ge $safe.serverStartTimestamp -and
            $safe.serverStartTimestamp -ge $before.timestamp -and $safe.serverEndTimestamp -le $after.timestamp -and
            $safe.startedBefore -ceq $true -and $safe.startedAfter -ceq $true -and $safe.stoppingBefore -ceq $false -and $safe.stoppingAfter -ceq $false -and
            !$safe.requestAborted -and $safe.refusalCategory -ceq 'NONE' -and
            $safe.openAttempts -eq 1 -and $safe.openedConnections -eq 1 -and $safe.closedConnections -eq 1 -and
            $events.Count -eq 2 -and $refresh.Count -eq 1 -and $decision.Count -eq 1
        if ($ready) {
            $ready = $refresh[0].tags.result -ceq $true -and $refresh[0].tags.outcome -ceq 'READY' -and $null -eq $refresh[0].tags.diagnosticCode -and
                $decision[0].tags.refreshResult -ceq $true -and $decision[0].tags.outcome -ceq 'ACCEPTED' -and $decision[0].tags.site -ceq $site -and
                $refresh[0].tags.startTicks -ge $safe.serverStartTimestamp -and $refresh[0].tags.endTicks -ge $refresh[0].tags.startTicks -and
                $refresh[0].tags.endTicks -le $safe.serverEndTimestamp -and
                @($events | Where-Object { $_.traceId -cne $safe.traceId -or $_.spanId -cne $safe.spanId }).Count -eq 0
        }
        $complete = $complete -and [bool] $ready
        $byId[$safe.requestId] = $safe
        $requests += $safe
    }
    $complete = $complete -and @($routeCounts.Values | Where-Object {$_ -ne 256}).Count -eq 0
    $provider = @()
    if ($data.providerEvents -isnot [array] -or $data.providerEvents.Count -gt 32768) { throw 'SF-TEST-PG-OBSERVATIONS' }
    foreach ($event in $data.providerEvents) {
        $safe = Convert-PublicPostgreSqlObservationFields $event @{requestId='integer';category=@('Npgsql.Connection','Npgsql.Command');eventId='integer';timestamp='integer';exceptionKind=$exceptionKinds}
        if ($safe.requestId -ge 768 -or $safe.eventId -notin @(1000,1001,1003,1004,1900,2000,2001,2002)) { throw 'SF-TEST-PG-OBSERVATIONS' }
        $provider += $safe
    }
    # Six reader commands, each executing/completed, between opening/opened and
    # closing/closed. A correct aggregate does not compensate for a missing request.
    $sequence = @(1000,1001,2000,2001,2000,2001,2000,2001,2000,2001,2000,2001,2000,2001,1003,1004)
    $groups = @{}
    foreach ($event in $provider) {
        if (!$byId.ContainsKey($event.requestId)) {$complete=$false;continue}
        if (!$groups.ContainsKey($event.requestId)) {$groups[$event.requestId]=[Collections.Generic.List[object]]::new()}
        $groups[$event.requestId].Add($event)
    }
    $complete = $complete -and $provider.Count -eq 12288
    foreach ($request in $requests) {
        $eventsForRequest = @(if ($groups.ContainsKey($request.requestId)) {$groups[$request.requestId].ToArray()})
        $complete = $complete -and $eventsForRequest.Count -eq $sequence.Count
        $previous = $request.serverStartTimestamp
        $seenProvider = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
        for ($index=0;$index -lt $eventsForRequest.Count;$index++) {
            $event=$eventsForRequest[$index]
            $category=if($index -lt $sequence.Count -and $sequence[$index] -lt 2000){'Npgsql.Connection'}else{'Npgsql.Command'}
            $valid=$index -lt $sequence.Count -and $event.eventId -eq $sequence[$index] -and $event.category -ceq $category -and
                $event.exceptionKind -ceq 'NONE' -and $event.timestamp -ge $previous -and $event.timestamp -le $request.serverEndTimestamp -and
                $seenProvider.Add($event.category+'/'+$event.eventId+'/'+$event.timestamp)
            $complete=$complete -and $valid;$previous=$event.timestamp
        }
    }
    $statistics = Convert-PublicPostgreSqlObservationFields $data.providerStatistics @{maximumRequestCorrelatedConnections='integer';remainingRequestCorrelatedConnections='integer';droppedEvents='integer'}
    $complete = $complete -and $statistics.maximumRequestCorrelatedConnections -eq 1 -and $statistics.remainingRequestCorrelatedConnections -eq 0 -and $statistics.droppedEvents -eq 0
    $lifecycle = @($data.lifecycle | ForEach-Object { Convert-PublicPostgreSqlObservationFields $_ @{kind=@('APPLICATION_STARTED','APPLICATION_STOPPING','APPLICATION_STOPPED');timestamp='integer'} })
    if($lifecycle.Count -ne 3 -or $requests.Count -eq 0){$complete=$false}
    else {
        $firstServer=($requests.serverStartTimestamp|Measure-Object -Minimum).Minimum
        $lastServer=($requests.serverEndTimestamp|Measure-Object -Maximum).Maximum
        $complete=$complete -and $lifecycle[0].kind -ceq 'APPLICATION_STARTED' -and $lifecycle[1].kind -ceq 'APPLICATION_STOPPING' -and
            $lifecycle[2].kind -ceq 'APPLICATION_STOPPED' -and $lifecycle[0].timestamp -ge $before.timestamp -and
            $lifecycle[0].timestamp -le $firstServer -and $lifecycle[1].timestamp -ge $lastServer -and
            $lifecycle[2].timestamp -ge $lifecycle[1].timestamp -and $lifecycle[2].timestamp -le $after.timestamp
    }
    $proof = @{schemaVersion=1;campaignId=$Campaign.Id;observationRun=$Campaign.Manifest.observationRun;workers=32;requestsPerWorker=24;stopwatchFrequency=$data.stopwatchFrequency
        completeness=$(if ($complete) { 'Complete' } else { 'Partial' });http=$requests;providerEvents=$provider;providerStatistics=$statistics;lifecycle=$lifecycle
        http200=@($requests|Where-Object status -EQ 200).Count;http503=@($requests|Where-Object status -EQ 503).Count
        limitation='Historical P03-F1 remains OPEN; cause UNKNOWN. Missing attribution remains UNKNOWN.'}
    Write-PublicPostgreSqlJson (Resolve-PublicPostgreSqlEvidencePath $Campaign.Root 'observations/p03.json') $proof -CreateNew
    return $complete
}
