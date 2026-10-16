param([switch]$CheckGitHub)

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path $PSScriptRoot -Parent
$roadmap = Get-Content -Raw -LiteralPath (Join-Path $repositoryRoot 'docs/product-roadmap.json') | ConvertFrom-Json
$ledger = Get-Content -Raw -LiteralPath (Join-Path $repositoryRoot 'docs/realblend-research.md')
$milestones = @{}
$tasks = @{}
$requirements = @{}
$numbers = @{}

foreach ($milestone in $roadmap.milestones) {
    if ($milestones.ContainsKey($milestone.id)) { throw "Duplicate milestone $($milestone.id)" }
    if (-not $milestone.githubNumber -or -not $milestone.success) { throw "Incomplete milestone $($milestone.id)" }
    $milestones[$milestone.id] = $milestone
}
foreach ($task in $roadmap.issues) {
    if ($tasks.ContainsKey($task.id) -or $numbers.ContainsKey($task.githubNumber)) { throw "Duplicate task $($task.id)" }
    if (-not $milestones.ContainsKey($task.milestone)) { throw "Unknown milestone for $($task.id)" }
    foreach ($field in @('title','problem','solution','success','verification','labels','requirements','githubNumber')) {
        if (-not $task.$field) { throw "Missing $field in $($task.id)" }
    }
    if ($task.success.Count -lt 4) { throw "Insufficient acceptance criteria in $($task.id)" }
    $tasks[$task.id] = $task
    $numbers[$task.githubNumber] = $true
    foreach ($requirement in $task.requirements) {
        if ($requirements.ContainsKey($requirement.id)) { throw "Duplicate requirement $($requirement.id)" }
        if ($requirement.source -notin @('store','manual','portfolio','publisher-update','publisher-wip','improvement')) {
            throw "Unknown source for $($requirement.id)"
        }
        $pattern = '(?m)^\| ' + [regex]::Escape($requirement.id) + ' \|'
        if ([regex]::Matches($ledger, $pattern).Count -ne 1) { throw "Missing or duplicate ledger row $($requirement.id)" }
        $requirements[$requirement.id] = $true
    }
}
$states = @{}
function Visit-Task([string]$Id) {
    if (-not $tasks.ContainsKey($Id)) { throw "Unknown dependency $Id" }
    if ($states[$Id] -eq 'visiting') { throw "Dependency cycle at $Id" }
    if ($states[$Id] -eq 'done') { return }
    $states[$Id] = 'visiting'
    foreach ($dependency in $tasks[$Id].depends) { Visit-Task $dependency }
    $states[$Id] = 'done'
}
foreach ($task in $roadmap.issues) { Visit-Task $task.id }

if ($CheckGitHub) {
    $remoteIssues = gh issue list --repo DorukTan/SimpleBlend --state all --limit 200 --json number,title,body,labels,milestone | ConvertFrom-Json
    if ($LASTEXITCODE -ne 0) { throw 'GitHub issue lookup failed.' }
    foreach ($task in $roadmap.issues) {
        $remote = @($remoteIssues | Where-Object number -EQ $task.githubNumber)
        if ($remote.Count -ne 1) { throw "Missing remote task $($task.id)" }
        if ($remote[0].title -ne "[$($task.milestone)] $($task.title)") { throw "Title drift for $($task.id)" }
        if ($remote[0].milestone.number -ne $milestones[$task.milestone].githubNumber) { throw "Milestone drift for $($task.id)" }
        foreach ($label in $task.labels) {
            if ($label -notin $remote[0].labels.name) { throw "Missing label $label on $($task.id)" }
        }
        foreach ($heading in @('Problem','Solution','Requirements','Success criteria','Verification','Dependencies','Pull request')) {
            if (-not $remote[0].body.Contains("## $heading")) { throw "Missing $heading on $($task.id)" }
        }
        foreach ($dependency in $task.depends) {
            if (-not $remote[0].body.Contains("#$($tasks[$dependency].githubNumber)")) { throw "Dependency drift on $($task.id)" }
        }
    }
    $release = $remoteIssues | Where-Object number -EQ 11
    if (-not $release.body.Contains('#61')) { throw 'Release candidate does not reference the expanded gate.' }
    $final = $remoteIssues | Where-Object number -EQ 12
    if (-not $final.body.Contains('M10-M20') -or $final.body.Contains('HDRP, runtime painting, sculpting and other explicit v1 non-goals remain deferred')) {
        throw 'Final release scope is stale.'
    }
}
Write-Output "Validated $($milestones.Count) milestones, $($tasks.Count) tasks, $($requirements.Count) ledger rows and an acyclic dependency graph."
if ($CheckGitHub) { Write-Output 'Remote issue titles, milestones, labels, required sections, dependencies and release gates match.' }
