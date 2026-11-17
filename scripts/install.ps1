<#
.SYNOPSIS
Downloads the latest Tammi release for this machine into the per-user versions folder and runs it.
.EXAMPLE
irm https://github.com/deosrc/Tammi/releases/latest/download/install.ps1 | iex
.NOTES
Set TAMMI_RELEASE_BASE_URL or TAMMI_RELEASE_API_URL to use another release source.
#>
$ErrorActionPreference = 'Stop'

$baseUrl = if ($env:TAMMI_RELEASE_BASE_URL) { $env:TAMMI_RELEASE_BASE_URL } else { 'https://github.com/deosrc/Tammi/releases/latest/download' }
$apiUrl = if ($env:TAMMI_RELEASE_API_URL) { $env:TAMMI_RELEASE_API_URL } else { 'https://api.github.com/repos/deosrc/Tammi/releases/latest' }

if (-not [Environment]::Is64BitOperatingSystem -or $env:PROCESSOR_ARCHITECTURE -notin @('AMD64', 'ARM64')) {
    throw "Tammi isn't released for Windows architecture '$env:PROCESSOR_ARCHITECTURE'."
}

$rid = if ($env:PROCESSOR_ARCHITECTURE -eq 'ARM64') { 'win-arm64' } else { 'win-x64' }
if ($rid -eq 'win-arm64') {
    throw "Tammi isn't released for Windows ARM64 yet."
}

$release = Invoke-RestMethod -Uri $apiUrl -Headers @{ Accept = 'application/vnd.github+json' }
$tag = $release.tag_name
if (-not $tag) {
    throw "Couldn't find the latest Tammi release for Windows $rid."
}

$version = $tag.TrimStart('v')
$root = Join-Path $env:LOCALAPPDATA 'Tammi\Versions'
$destination = Join-Path $root $version
$work = Join-Path ([IO.Path]::GetTempPath()) ("tammi-install-" + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $work | Out-Null

try {
    $archive = Join-Path $work 'release.zip'
    try {
        Invoke-WebRequest -Uri "$baseUrl/tammi-$tag-fd-$rid.zip" -OutFile $archive
    } catch {
        throw "Couldn't download the Tammi release for Windows $rid (tammi-$tag-fd-$rid.zip): $($_.Exception.Message)"
    }
    Expand-Archive -Path $archive -DestinationPath (Join-Path $work 'unpacked')
    New-Item -ItemType Directory -Path $destination -Force | Out-Null
    Copy-Item -Path (Join-Path $work 'unpacked\*') -Destination $destination -Recurse -Force
    & (Join-Path $destination 'Deosrc.Tammi.Windows.exe')
} finally {
    Remove-Item -Recurse -Force $work -ErrorAction SilentlyContinue
}
