#!/usr/bin/env bash
# Cursor Cloud Agent / local bootstrap for pyprices + ASP.NET tooling.
# Always resolves the repo root from this script path (safe from any cwd).
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT"

sudo apt-get update
sudo DEBIAN_FRONTEND=noninteractive apt-get install -y \
  python3.12-venv python3.12-dev build-essential

if ! command -v dotnet >/dev/null 2>&1; then
  curl -fsSL https://dot.net/v1/dotnet-install.sh | bash -s -- --channel 10.0 --install-dir "${HOME}/.dotnet"
  sudo ln -sf "${HOME}/.dotnet/dotnet" /usr/local/bin/dotnet
elif [[ ! -x /usr/local/bin/dotnet ]]; then
  # Login shells (bash -lc) may miss ~/.dotnet; keep a PATH-stable symlink.
  if [[ -x "${HOME}/.dotnet/dotnet" ]]; then
    sudo ln -sf "${HOME}/.dotnet/dotnet" /usr/local/bin/dotnet
  fi
fi

rm -rf pyprices/venv
python3 -m venv pyprices/venv
pyprices/venv/bin/pip install -U pip
pyprices/venv/bin/pip install -r pyprices/requirements.txt

dotnet restore aspnet/EcomAE.AspNetCore.sln
