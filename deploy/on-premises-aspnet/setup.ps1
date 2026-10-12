# ECOM AE ASP.NET Core Windows setup. No PHP.
# Double-click setup.cmd or run: powershell -File setup.ps1
param(
    [string]$HostName = "",
    [string]$DbPassword = "",
    [int]$Port = 5100
)

$ErrorActionPreference = "Stop"
Set-Location -LiteralPath $PSScriptRoot

function Fail([string]$Message) {
    Write-Host "[error] $Message" -ForegroundColor Red
    exit 1
}

if (-not (Get-Command docker -ErrorAction SilentlyContinue)) {
    Fail "Docker Desktop is required. Install it, start it, then double-click setup.cmd again."
}

if (-not $HostName) {
    $HostName = Read-Host "Public host (example erp.company.local)"
}
if (-not $HostName) { Fail "Host is required." }

if (-not $DbPassword) {
    $secure = Read-Host "MariaDB password (you choose; the installer will not invent one)" -AsSecureString
    $DbPassword = [Runtime.InteropServices.Marshal]::PtrToStringAuto(
        [Runtime.InteropServices.Marshal]::SecureStringToBSTR($secure))
}
if (-not $DbPassword) { Fail "Database password is required. The installer will not invent one." }

$src = Join-Path $PSScriptRoot "src"
if (-not (Test-Path (Join-Path $src "aspnet\src\EcomAE.Platform\EcomAE.Platform.csproj"))) {
    if (-not (Get-Command git -ErrorAction SilentlyContinue)) {
        Fail "git is required once to fetch the ASP.NET source."
    }
    Write-Host "[ecomae-aspnet] Fetching ASP.NET source (no PHP runtime)..."
    git clone --depth 1 --branch main https://github.com/epartscart/ecomae.git $src
}

if (-not (Test-Path (Join-Path $src "aspnet\src\EcomAE.Platform\EcomAE.Platform.csproj"))) {
    Fail "ASP.NET project missing. Refusing to fall back to PHP."
}

$envPath = Join-Path $PSScriptRoot ".env"
@"
PUBLIC_HOST=$HostName
ASPNETCORE_URLS=http://0.0.0.0:5100
HTTP_PORT=$Port
DB_HOST=mariadb
DB_PORT=3306
DB_NAME=ecomae
DB_USER=ecomae
DB_PASSWORD=$DbPassword
LICENSE_KEY=
"@ | Set-Content -LiteralPath $envPath -Encoding ascii
icacls $envPath /inheritance:r /grant:r "$env:USERNAME:R" | Out-Null
Write-Host "[ecomae-aspnet] Wrote .env. Password is not printed."

Write-Host "[ecomae-aspnet] Starting ASP.NET Core + MariaDB (no PHP)..."
docker compose -f (Join-Path $PSScriptRoot "docker-compose.yml") --env-file $envPath up -d --build
if ($LASTEXITCODE -ne 0) { Fail "docker compose failed." }

$ok = $false
for ($i = 0; $i -lt 40; $i++) {
    try {
        $r = Invoke-WebRequest -UseBasicParsing -TimeoutSec 3 "http://127.0.0.1:$Port/health"
        if ($r.StatusCode -eq 200) { $ok = $true; break }
    } catch { Start-Sleep -Seconds 3 }
}
if (-not $ok) { Fail "Platform did not become healthy on :$Port. Run: docker compose logs platform" }

Write-Host "[ecomae-aspnet] Health OK  http://127.0.0.1:$Port/health"
Write-Host "[ecomae-aspnet] ERP login  http://${HostName}:$Port/erp/login"
Write-Host "[ecomae-aspnet] PHP was not installed."
