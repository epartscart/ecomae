# ECOM AE hybrid Windows setup. No leftover runtime. Does not invent a password or a send.
param(
    [string]$SiteKey = "",
    [string]$CloudUrl = "https://www.ecomae.com",
    [string]$Enrollment = "",
    [string]$Installation = "",
    [string]$DbPassword = ""
)

$ErrorActionPreference = "Stop"
Set-Location -LiteralPath $PSScriptRoot

function Fail([string]$Message) {
    Write-Host "[error] $Message" -ForegroundColor Red
    exit 1
}

if (-not $SiteKey) { $SiteKey = Read-Host "Site key (from Super CP register)" }
if (-not $Enrollment) { $Enrollment = Read-Host "Enrollment (enr_…)" }
if (-not $Installation) { $Installation = Read-Host "Installation (inst_…)" }
if (-not $CloudUrl) { $CloudUrl = "https://www.ecomae.com" }

if (-not $SiteKey -or -not $Enrollment -or -not $Installation) {
    Fail "Required: site key, enrollment, installation. Register first. Do not invent tokens."
}
if (-not $CloudUrl.StartsWith("https://")) {
    Fail "Cloud URL must be HTTPS. The agent is outbound only."
}
if (-not $Enrollment.StartsWith("enr_")) {
    Fail "Enrollment must start with enr_."
}

$envPath = Join-Path $PSScriptRoot ".env"
@"
SITE_KEY=$SiteKey
INSTALLATION_ID=$Installation
ENROLLMENT_REQUEST_ID=$Enrollment
CLOUD_BASE_URL=$CloudUrl
LOCAL_DSN=$DbPassword
CONFIRM_WRITES=0
CYCLE_SECONDS=300
STATE_DIR=./state
"@ | Set-Content -LiteralPath $envPath -Encoding ascii
icacls $envPath /inheritance:r /grant:r "$env:USERNAME:R" | Out-Null
New-Item -ItemType Directory -Force -Path (Join-Path $PSScriptRoot "state") | Out-Null
Write-Host "[hybrid] Wrote .env. Secrets are not printed."

if (Get-Command docker -ErrorAction SilentlyContinue) {
    Write-Host "[hybrid] Starting outbound sync agent (Docker)..."
    docker compose -f (Join-Path $PSScriptRoot "docker-compose.hybrid.yml") --env-file $envPath up -d
    if ($LASTEXITCODE -ne 0) { Fail "docker compose failed." }
    Write-Host "[hybrid] Agent running. Conflicts stay queued. This setup does not invent a send."
    exit 0
}

if (Get-Command python -ErrorAction SilentlyContinue) {
    Write-Host "[hybrid] Docker not found — running one sync cycle with python."
    python (Join-Path $PSScriptRoot "sync-agent\sync-agent.py") cycle
    exit $LASTEXITCODE
}

Fail "Install Docker Desktop (or python), then double-click setup.exe / setup.cmd again."
