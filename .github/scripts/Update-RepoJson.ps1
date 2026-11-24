# Adds or updates one plugin's entry in repo.json, keeping every other plugin's entry.
# A testing release only changes the testing fields; a stable release updates both channels
# (unless a newer testing build is already out).
param(
    [Parameter(Mandatory)] [string] $ManifestPath,
    [Parameter(Mandatory)] [string] $DownloadUrl,
    [Parameter(Mandatory)] [string] $Version,
    [Parameter(Mandatory)] [bool] $Testing,
    [string] $NotesPath,
    [string] $RepoJson = 'repo.json'
)

$manifest = Get-Content $ManifestPath -Raw | ConvertFrom-Json -AsHashtable
$name = $manifest['InternalName']

$entries = @()
if (Test-Path $RepoJson) { $entries = @(Get-Content $RepoJson -Raw | ConvertFrom-Json -AsHashtable) }
$entry = $entries | Where-Object { $_['InternalName'] -eq $name } | Select-Object -First 1
$others = @($entries | Where-Object { $_['InternalName'] -ne $name })

if ($Testing) {
    if ($null -eq $entry) {
        # first release ever is a testing one: list it, but only testing users can install it
        $entry = $manifest
        $entry['IsTestingExclusive'] = $true
        $entry['DownloadLinkInstall'] = $DownloadUrl
        $entry['DownloadLinkUpdate'] = $DownloadUrl
    }
    $entry['TestingAssemblyVersion'] = $Version
    $entry['TestingDalamudApiLevel'] = $manifest['DalamudApiLevel']
    $entry['DownloadLinkTesting'] = $DownloadUrl
}
else {
    $testingVersion = if ($entry) { $entry['TestingAssemblyVersion'] } else { $null }
    $testingUrl = if ($entry) { $entry['DownloadLinkTesting'] } else { $null }
    $entry = $manifest
    $entry['DownloadLinkInstall'] = $DownloadUrl
    $entry['DownloadLinkUpdate'] = $DownloadUrl
    if ($testingVersion -and ([version]$testingVersion -gt [version]$Version)) {
        $entry['TestingAssemblyVersion'] = $testingVersion
        $entry['DownloadLinkTesting'] = $testingUrl
    } else {
        $entry['TestingAssemblyVersion'] = $Version
        $entry['DownloadLinkTesting'] = $DownloadUrl
    }
    $entry['TestingDalamudApiLevel'] = $manifest['DalamudApiLevel']
    $entry.Remove('IsTestingExclusive')
    if ($NotesPath -and (Test-Path $NotesPath)) { $entry['Changelog'] = (Get-Content $NotesPath -Raw).Trim() }
}

$entry['LastUpdate'] = [DateTimeOffset]::UtcNow.ToUnixTimeSeconds()

$all = @($others + $entry) | Sort-Object { $_['Name'] }
ConvertTo-Json @($all) -Depth 5 | Set-Content -Encoding utf8 $RepoJson
