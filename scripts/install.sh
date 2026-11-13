#!/bin/sh
# Downloads the latest Tammi release for this machine into the per-user versions folder and runs it.
# Usage: curl -fsSL https://github.com/deosrc/Tammi/releases/latest/download/install.sh | sh

set -eu

base_url="${TAMMI_RELEASE_BASE_URL:-https://github.com/deosrc/Tammi/releases/latest/download}"
api_url="${TAMMI_RELEASE_API_URL:-https://api.github.com/repos/deosrc/Tammi/releases/latest}"

case "$(uname -s):$(uname -m)" in
  Linux:x86_64|Linux:amd64)
    rid=linux-x64
    archive=tar.gz
    executable=Deosrc.Tammi.Linux
    ;;
  Linux:aarch64|Linux:arm64)
    rid=linux-arm64
    archive=tar.gz
    executable=Deosrc.Tammi.Linux
    ;;
  *)
    echo "Tammi isn't released for $(uname -s) on $(uname -m)." >&2
    exit 1
    ;;
esac

if ! command -v curl >/dev/null 2>&1; then
  echo "Install curl and run this script again." >&2
  exit 1
fi

release="$(curl -fsSL -H 'Accept: application/vnd.github+json' "$api_url")"
tag="$(printf '%s' "$release" | sed -n 's/.*"tag_name"[[:space:]]*:[[:space:]]*"\([^"]*\)".*/\1/p' | head -n 1)"
if [ -z "$tag" ]; then
  echo "Couldn't find the latest Tammi release for Linux $rid." >&2
  exit 1
fi

version=${tag#v}
xdg_data_home="${XDG_DATA_HOME:-}"
case "$xdg_data_home" in /*) ;; *) xdg_data_home="$HOME/.local/share" ;; esac
root="$xdg_data_home/tammi/versions"
destination="$root/$version"
archive_name="tammi-$tag-fd-$rid.$archive"
work="$(mktemp -d)"
trap 'rm -rf "$work"' EXIT

mkdir -p "$root"
echo "Downloading Tammi $version..."
if ! curl -fsSL "$base_url/$archive_name" -o "$work/release.$archive"; then
  echo "Couldn't download the Tammi release for Linux $rid ($archive_name)." >&2
  exit 1
fi

mkdir "$work/unpacked"
tar -xzf "$work/release.$archive" -C "$work/unpacked"
mkdir -p "$destination"

cp -R "$work/unpacked/." "$destination/"
chmod +x "$destination/$executable"

"$destination/$executable"
