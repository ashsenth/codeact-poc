#!/usr/bin/env bash
set -euo pipefail

repo=$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/.." && pwd)
if [[ "${1:-}" != '--approved-install' ]]; then
    echo 'PLAN ONLY. On the approved VM, run: bash scripts/bootstrap-vm.sh --approved-install'
    echo 'Changes outside repo: apt packages/repository, pinned SDK in /usr/share/dotnet, kvm module/group membership.'
    echo 'Installs: git, curl, unzip, jq, CA certificates, Python3, libicu74, libssl3t64, libstdc++6, libgcc-s1, zlib1g, libgomp1, libfuse3-3, qemu-kvm, pwsh and Azure CLI.'
    echo 'Downloads matching Hyperlight Python 0.4.0 prebuilt guest into ignored repo artifacts/. No source toolchain needed.'
    exit 0
fi
[[ $(uname -m) == x86_64 ]] || { echo '[SKIPPED] Requires x86_64.' >&2; exit 1; }
source /etc/os-release
[[ "$ID" == ubuntu && "$VERSION_ID" == 24.04 ]] || { echo 'Requires Ubuntu 24.04.' >&2; exit 1; }
[[ $EUID -ne 0 ]] || { echo 'Run as the SSH user with sudo, not root.' >&2; exit 1; }
work=$(mktemp -d)
trap 'rm -rf "$work"' EXIT
sudo apt-get update
sudo apt-get install -y ca-certificates curl unzip jq git python3 libicu74 libssl3t64 libstdc++6 libgcc-s1 zlib1g libgomp1 libfuse3-3 qemu-kvm
curl --fail --silent --show-error --location https://packages.microsoft.com/config/ubuntu/24.04/packages-microsoft-prod.deb -o "$work/microsoft.deb"
sudo dpkg -i "$work/microsoft.deb"
sudo apt-get update
sudo apt-get install -y powershell
curl --fail --silent --show-error --location https://aka.ms/InstallAzureCLIDeb -o "$work/install-azure-cli.sh"
sudo bash "$work/install-azure-cli.sh"
sdk=$(jq -er '.sdk.version' "$repo/global.json")
curl --fail --silent --show-error --location https://dot.net/v1/dotnet-install.sh -o "$work/dotnet-install.sh"
sudo bash "$work/dotnet-install.sh" --version "$sdk" --install-dir /usr/share/dotnet --no-path
if ! command -v dotnet >/dev/null; then sudo ln -s /usr/share/dotnet/dotnet /usr/local/bin/dotnet; fi
dotnet --list-sdks | grep -F "$sdk " >/dev/null
guest_version=0.4.0
guest_package=hyperlight.hyperlightsandbox.guest.python
guest_dir="$repo/artifacts/guest/$guest_version"
mkdir -p "$guest_dir"
curl --fail --silent --show-error --location "https://api.nuget.org/v3-flatcontainer/$guest_package/$guest_version/$guest_package.$guest_version.nupkg" -o "$work/guest.nupkg"
unzip -o -q "$work/guest.nupkg" -d "$guest_dir"
guest="$guest_dir/runtimes/linux-x64/native/python-sandbox.aot"
[[ -f "$guest" ]] || { echo 'Matching guest module missing.' >&2; exit 1; }
sudo modprobe kvm
if grep -qw vmx /proc/cpuinfo; then sudo modprobe kvm_intel; elif grep -qw svm /proc/cpuinfo; then sudo modprobe kvm_amd; else echo '[SKIPPED] No nested virtualization CPU flags.' >&2; exit 1; fi
sudo usermod -aG kvm "$(id -un)"
echo "[OFFLINE-CONTROL] Installed pinned SDK $sdk, pwsh $(pwsh -NoProfile -Command '$PSVersionTable.PSVersion.ToString()'), Azure CLI and guest $guest_version."
[[ -c /dev/kvm ]] || { echo '[SKIPPED] /dev/kvm absent. Do not start tests.' >&2; exit 1; }
ls -l /dev/kvm
if [[ -r /dev/kvm && -w /dev/kvm ]]; then echo '[LIVE] KVM device permissions ready; actual execution still unverified.'; else echo '[SKIPPED] KVM permissions pending: disconnect ALL SSH/Remote-SSH sessions and reconnect.'; fi
printf 'For EACH new shell (not written to your profile):\nexport HYPERLIGHT_PYTHON_GUEST_PATH=%q\n' "$guest"
echo 'Run az login, configure Azure OpenAI in the shell, then use verify-observability.ps1 -Mode Live -PreflightOnly.'
echo 'Readiness NOT COMPLETE. No tests or live model requests executed by bootstrap.'