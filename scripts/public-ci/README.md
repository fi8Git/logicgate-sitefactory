# Public CI

The public workflow runs the Windows/Linux offline matrix and PostgreSQL
independently, followed by **Require complete public proof**. This exact check
name is stable. Every pull request and main push follows the full path; there
is no documentation-only routing.

Each job checks out the public repository at depth 1 without persisting
credentials. Actions are pinned by SHA and permissions remain `contents: read`.
A full checkout avoids maintaining sparse include/exclude lists. It is not the
build directory: the harness validates the closed 23-project inventory before
copying the allowed inputs, verifies the copied bytes and rejects redirections.
The checked-in solution is validated and preserved, not regenerated.

The owned scratch and its NuGet cache are fresh for every job. Composition,
evaluated resources, dependencies/cycles, packages, binaries, SourceLink and
per-document PDB checksums remain validated. The compiled guard performs the
original-root symbol/binary validation and its real-PDB counter-proofs; its
bounded result replaces two identical scans in the caller. A symbol validator
may reuse a repository file's SHA-256 only within that invocation against the
immutable scratch. Every PDB document is still compared separately. No cache
survives the call or is shared between jobs.

The aggregate requires the three exact public reports, GitHub success, PASSED,
SourceLink VERIFIED, matching executed SHA/run, positive attempts no later than
the current attempt, exact offline/shard inventories, checksums and all 27 TRX.
PostgreSQL still requires 23 Facts, five sequential shards, P03 768/768 HTTP 200
and complete cleanup. Partial/red reports are diagnostic evidence, never success.

`report.json` adds monotonic `timings` records with a closed phase name and
`durationMs`. Repeated phase occurrences can be summed. A failed phase's timing
does not change its failure status. Suite `start`/`finish` retain actual process
windows. `symbolValidationMs` and `binaryValidationMs` measure the original-root
validators, while the SYMBOLS phase also includes their adversarial guard.
PostgreSQL's manifest adds `preparationMs` once initial discovery/provenance,
image/TLS/container readiness and credentials are ready, before the five shards.
An absent measurement is UNKNOWN, never zero. Phase timings include orchestration
overhead; they are measurements, not correctness criteria or benchmarks.

Only controlled evidence is uploaded. No raw output, exception, argument,
attachment, credential or personal path is added to timing records. Diagnostics
are saved before cleanup, and a reporting failure cannot skip owned cleanup.
P03-F1 remains OPEN/UNKNOWN with its separate 2026-11-16 review deadline.
