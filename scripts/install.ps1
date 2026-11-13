<#
.SYNOPSIS
Installs Tammi for the current user on Windows, without needing admin rights.

.DESCRIPTION
Checks that .NET 10 is installed, downloads the installer from the latest Tammi release, and runs it. The installer
does the rest: it checks for git, downloads Tammi and adds it to the Start menu. Arguments are passed to the installer
(e.g. --prerelease, --tag v0.2.0, --help).

.EXAMPLE
irm https://github.com/deosrc/Tammi/releases/latest/download/install.ps1 | iex

.EXAMPLE
& ([scriptblock]::Create((irm https://github.com/deosrc/Tammi/releases/latest/download/install.ps1))) --prerelease

.NOTES
$env:TAMMI_RELEASE_BASE_URL replaces the address the installer is downloaded from, e.g. to try a local build.
#>
$ErrorActionPreference = 'Stop'

$baseUrl = if ($env:TAMMI_RELEASE_BASE_URL) { $env:TAMMI_RELEASE_BASE_URL } else { 'https://github.com/deosrc/Tammi/releases/latest/download' }
$requiredMajor = 10

# .NET is needed to run the installer, and Tammi.
# Future: if a self-contained Tammi build is released, offer to download it here, or send the user to the right
# .NET download page, instead of only explaining how to install .NET.
$runtimes = if (Get-Command dotnet -ErrorAction SilentlyContinue) { & dotnet --list-runtimes 2>$null } else { @() }
if (-not ($runtimes | Where-Object { $_ -match "^Microsoft\.NETCore\.App $requiredMajor\." })) {
    [Console]::Error.WriteLine(".NET $requiredMajor runtime isn't installed.")
    [Console]::Error.WriteLine("Install it: run 'winget install Microsoft.DotNet.Runtime.$requiredMajor', or download it from https://dotnet.microsoft.com/download/dotnet/$requiredMajor.0")
    [Console]::Error.WriteLine('Then run this script again.')
    exit 1
}

$work = Join-Path ([IO.Path]::GetTempPath()) ("tammi-install-" + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $work | Out-Null
try {
    Write-Host 'Downloading the installer...'
    $zip = Join-Path $work 'installer.zip'
    try {
        Invoke-WebRequest -Uri "$baseUrl/tammi-installer.zip" -OutFile $zip -UseBasicParsing
    } catch {
        [Console]::Error.WriteLine("Couldn't download the installer from $baseUrl. $($_.Exception.Message)")
        exit 1
    }

    Expand-Archive -Path $zip -DestinationPath (Join-Path $work 'installer')
    & dotnet (Join-Path $work 'installer\tammi-installer.dll') @args
    exit $LASTEXITCODE
} finally {
    Remove-Item -Recurse -Force $work -ErrorAction SilentlyContinue
}
