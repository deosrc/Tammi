#!/bin/sh
# Installs Tammi for the current user on Linux, without needing admin rights.
#
# It checks that .NET 10 is installed, downloads the installer from the latest Tammi release, and runs it. The
# installer does the rest: it checks for git, downloads Tammi and adds it to the application menu. Options are passed
# to the installer (e.g. --prerelease, --tag v0.2.0, --help).
#
# Usage: curl -fsSL https://github.com/deosrc/Tammi/releases/latest/download/install.sh | sh
#        curl -fsSL https://github.com/deosrc/Tammi/releases/latest/download/install.sh | sh -s -- --prerelease
#
# TAMMI_RELEASE_BASE_URL replaces the address the installer is downloaded from, e.g. to try a local build.

set -eu

base_url="${TAMMI_RELEASE_BASE_URL:-https://github.com/deosrc/Tammi/releases/latest/download}"
required_major=10

fail() {
  echo "$1" >&2
  exit 1
}

# .NET is needed to run the installer, and Tammi.
# Future: if a self-contained Tammi build is released, offer to download it here, or send the user to the right
# .NET download page, instead of only explaining how to install .NET.
if ! command -v dotnet >/dev/null 2>&1 \
  || ! dotnet --list-runtimes 2>/dev/null | grep -q "^Microsoft\.NETCore\.App ${required_major}\."; then
  fail ".NET ${required_major} runtime isn't installed.
Install it with your package manager (e.g. 'sudo apt install dotnet-runtime-${required_major}.0' on Debian or Ubuntu),
or download it from https://dotnet.microsoft.com/download/dotnet/${required_major}.0
Then run this script again."
fi

work="$(mktemp -d)"
trap 'rm -rf "$work"' EXIT

echo "Downloading the installer..."
if command -v curl >/dev/null 2>&1; then
  curl -fsSL "$base_url/tammi-installer.tar.gz" -o "$work/installer.tar.gz" || fail "Couldn't download the installer from $base_url."
elif command -v wget >/dev/null 2>&1; then
  wget -q "$base_url/tammi-installer.tar.gz" -O "$work/installer.tar.gz" || fail "Couldn't download the installer from $base_url."
else
  fail "Neither curl nor wget is installed, so the installer can't be downloaded. Install one of them and run this script again."
fi

mkdir "$work/installer"
tar -xzf "$work/installer.tar.gz" -C "$work/installer"
dotnet "$work/installer/tammi-installer.dll" "$@"
