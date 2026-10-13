#requires -Version 5.1
[CmdletBinding()]
param([Parameter(Mandatory = $true)][string]$Revision)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$packageRoot = 'Packages/com.simpletools.simpleblend'
$commit = & git rev-parse --verify --end-of-options ($Revision + '^{commit}')
if ($LASTEXITCODE -ne 0) { throw 'Revision must resolve to a local commit.' }
$commit = ($commit | Select-Object -First 1).Trim()
$rows = & git -c core.quotepath=false ls-tree -r -l $commit -- $packageRoot
if ($LASTEXITCODE -ne 0 -or !$rows) { throw 'No committed package tree found.' }
$files = @(
    foreach ($row in $rows) {
        if ($row -notmatch '^(?<mode>[0-9]+)\s+blob\s+(?<blob>[0-9a-f]+)\s+(?<bytes>[0-9]+)\t(?<path>.*)$') {
            throw ('Unsupported tree entry: ' + $row)
        }
        if ($Matches.mode -notin @('100644', '100755')) {
            throw 'Review symlinks or other non-file entries before archiving.'
        }
        [pscustomobject]@{
            path = $Matches.path
            gitBlob = $Matches.blob
            bytes = [long]$Matches.bytes
        }
    }
)
$manifestText = & git show ($commit + ':' + $packageRoot + '/package.json')
if ($LASTEXITCODE -ne 0) { throw 'Package manifest is missing.' }
$manifest = ($manifestText -join "`n") | ConvertFrom-Json
if ($files.path -notcontains ($packageRoot + '/LICENSE')) { throw 'Package LICENSE is missing.' }
$projectRoot = (& git rev-parse --show-toplevel).Trim()
if ($LASTEXITCODE -ne 0) { throw 'Cannot resolve the repository root.' }
$outputRoot = Join-Path $projectRoot ('TestResults/RedistributionAudit_' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $outputRoot | Out-Null
$archive = Join-Path $outputRoot 'package-snapshot.tar'
& git archive --format=tar ('--output=' + $archive) $commit -- $packageRoot
if ($LASTEXITCODE -ne 0) { throw 'Git snapshot archive failed.' }
[pscustomobject]@{
    scope = 'committed package snapshot, not a release-candidate or legal approval'
    commit = $commit
    package = $manifest.name
    version = $manifest.version
    declaredLicense = $manifest.license
    declaredDependencies = $manifest.dependencies
    fileCount = $files.Count
    byteCount = ($files | Measure-Object -Property bytes -Sum).Sum
    snapshotPath = $archive
    snapshotSha256 = (Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash
    files = $files
}
