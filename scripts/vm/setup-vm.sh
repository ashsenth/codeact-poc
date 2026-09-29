#!/usr/bin/env bash
# One-time toolchain install on the Hyperlight VM. Logs to /tmp/setup.log; idempotent-ish.
set -uo pipefail
exec > /tmp/setup.log 2>&1
echo "=== $(date -u) install start ==="
export DEBIAN_FRONTEND=noninteractive
apt-get update -y
apt-get install -y curl git build-essential pkg-config libssl-dev unzip nodejs npm cpu-checker
echo "--- kvm-ok ---"; kvm-ok || true

# .NET 9 SDK -> /opt/dotnet
curl -sSL https://dot.net/v1/dotnet-install.sh -o /tmp/dotnet-install.sh
bash /tmp/dotnet-install.sh --channel 9.0 --install-dir /opt/dotnet
ln -sf /opt/dotnet/dotnet /usr/local/bin/dotnet

# Rust -> /root/.cargo
curl --proto '=https' --tlsv1.2 -sSf https://sh.rustup.rs | sh -s -- -y

# just -> /usr/local/bin
curl --proto '=https' --tlsv1.2 -sSf https://just.systems/install.sh | bash -s -- --to /usr/local/bin

# uv -> /root/.local/bin
curl -LsSf https://astral.sh/uv/install.sh | sh

echo "=== versions ==="
/usr/local/bin/dotnet --version || echo "dotnet FAIL"
/root/.cargo/bin/rustc --version || echo "rustc FAIL"
/usr/local/bin/just --version || echo "just FAIL"
(/root/.local/bin/uv --version || uv --version) || echo "uv FAIL"
echo "=== $(date -u) install done ==="
