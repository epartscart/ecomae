# ECOM AE — ASP.NET Core on-premises setup pack

**This is the on-premises installer.** Run setup on the customer PC or server. It starts ASP.NET Core + MariaDB. It does **not** install PHP.

| File | What it does |
| --- | --- |
| `setup.cmd` | Windows: double-click. Same outcome as `setup.exe`. |
| `setup.ps1` | Windows PowerShell installer (called by `setup.cmd`). |
| `setup/EcomAE.OnPremSetup.csproj` | Build `setup.exe` with `dotnet publish` (see below). |
| `install.sh` | Linux server / Docker host installer. |
| `docker-compose.yml` | ASP.NET Kestrel + MariaDB. No PHP-FPM. |
| `Dockerfile` | Publishes `EcomAE.Platform` only. |
| `.env.example` | Template. `DB_PASSWORD` stays empty until the operator supplies one. |
| `FUTURE_ASPNET_ONLY.md` | New work is ASP.NET Core only. Do not add PHP files here. |

Cloud `www.ecomae.com` still keeps PHP on disk as **fallback / reference** until the separate `ReadyToRemovePhp` gate. That is not this pack. This zip ships no `.php` files.

## Windows PC — run setup

1. Install [Docker Desktop](https://www.docker.com/products/docker-desktop/).
2. Download `ecomae-on-premises-aspnet-setup.zip` from Super CP → Deployment models.
3. Unzip. Double-click **`setup.cmd`**.
4. Enter the public host (example `erp.company.local`) and a MariaDB password **you choose**. The installer will not invent one.
5. Open `http://127.0.0.1:5100/health` then `/erp/login`.

To produce a real `setup.exe`:

```bat
cd setup
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o ..\dist
copy ..\dist\EcomAE.OnPremSetup.exe ..\setup.exe
```

Then double-click `setup.exe`. It runs the same steps as `setup.cmd`.

## Linux server

```bash
sudo ./install.sh --host erp.company.com --db-password 'YOUR_PASSWORD'
```

Optional: `--dsn 'Server=127.0.0.1;Port=3306;Database=ecomae;Uid=ecomae;Pwd=YOUR_PASSWORD;AllowUserVariables=true;'` when MariaDB already exists. The installer will not invent a MySQL user from an empty password.

## What this pack never does

- Install PHP, PHP-FPM, or `setup-wizard.php`
- Clone PHP as the application runtime
- Invent `TenantRegistry` / CloudPanel `db_password`
- Send data to the cloud unless you later install the **hybrid** pack
- Set `ReadyToRemovePhp=true` or delete PHP from GitHub / production

Operator board: `GET /migration/on-premises-parity`
Download: `/cp/deployment-models/setup-pack?kind=on-premises-aspnet`
