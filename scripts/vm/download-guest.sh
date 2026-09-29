#!/usr/bin/env bash
# Download the version-matched Hyperlight Python guest package (0.4.0) and locate its module.
set -o pipefail
export HOME=/root
V=0.4.0
PKG=hyperlight.hyperlightsandbox.guest.python
URL="https://api.nuget.org/v3-flatcontainer/$PKG/$V/$PKG.$V.nupkg"
mkdir -p /root/guestpkg
cd /root/guestpkg
echo "downloading $URL"
curl -sSL "$URL" -o guest.nupkg
echo "size: $(wc -c < guest.nupkg)"
rm -rf extracted
python3 -c "import zipfile; zipfile.ZipFile('guest.nupkg').extractall('extracted')"
echo "=== module candidates (.wasm/.aot) ==="
find extracted \( -iname '*.wasm' -o -iname '*.aot' \) -print

# Record the linux-x64 guest module path so run-benchmark.sh can pick it up.
GUEST=$(find extracted -iname 'python-sandbox.aot' | grep linux-x64 | head -1)
[ -z "$GUEST" ] && GUEST=$(find extracted \( -iname '*.aot' -o -iname '*.wasm' \) | head -1)
if [ -n "$GUEST" ]; then
  echo "$GUEST" > /root/guest_path.txt
  echo "wrote /root/guest_path.txt -> $GUEST"
else
  echo "WARN: no guest module found" >&2
fi
