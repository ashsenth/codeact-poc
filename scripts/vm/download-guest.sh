#!/usr/bin/env bash
set -euo pipefail

repo="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/../.." && pwd)"
version=0.4.0
package=hyperlight.hyperlightsandbox.guest.python
destination="${GUEST_ROOT:-$repo/artifacts/guest/$version}"
work="$(mktemp -d)"
trap 'rm -f "$work/guest.nupkg"; rmdir "$work"' EXIT
mkdir -p "$destination"
curl --fail --silent --show-error --location \
    "https://api.nuget.org/v3-flatcontainer/$package/$version/$package.$version.nupkg" \
    -o "$work/guest.nupkg"
unzip -o -q "$work/guest.nupkg" -d "$destination"
guest="$destination/runtimes/linux-x64/native/python-sandbox.aot"
[[ -f "$guest" ]] || { echo 'Version-matched linux-x64 guest missing.' >&2; exit 1; }
printf 'export HYPERLIGHT_PYTHON_GUEST_PATH=%q\n' "$(realpath "$guest")"
