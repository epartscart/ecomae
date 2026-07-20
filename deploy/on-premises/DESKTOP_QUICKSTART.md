# Desktop Quick Start (macOS / Windows)

Try ecomae ERP locally on your own laptop with Docker Desktop — no domain,
no certbot, no production sizing. Good for evaluation before committing to a
server deployment.

## Prerequisites

- **Docker Desktop** ([macOS](https://www.docker.com/products/docker-desktop) / [Windows](https://www.docker.com/products/docker-desktop)), with WSL2 backend enabled on Windows
- At least 4 GB RAM and 20 GB free disk assigned to Docker Desktop (Settings → Resources)
- `git`
  - macOS: `xcode-select --install` (or via [Homebrew](https://brew.sh): `brew install git`)
  - Windows: [Git for Windows](https://git-scm.com/download/win), run commands below in **Git Bash**
- A license key (ask your ecomae account manager)

## Steps

Open a terminal (macOS Terminal, or Git Bash on Windows) and run:

```bash
git clone https://github.com/epartscart/ecomae.git ecomae-deploy
cd ecomae-deploy/deploy/on-premises
cp .env.example .env
```

Edit `.env` in any text editor:
- Set `LICENSE_KEY` to your key
- Set `DB_PASSWORD` and `DB_ROOT_PASSWORD` to any strong values
- Leave `APP_URL=https://erp.yourcompany.com` as-is, or change to `https://localhost`
- Leave `HTTP_PORT=80` / `HTTPS_PORT=443` unless something else on your machine already uses those ports (change both if so, e.g. `HTTPS_PORT=8443`)

Fetch the application code and start the stack:

```bash
git clone --depth 1 https://github.com/epartscart/ecomae.git app
docker compose build
docker compose up -d
```

Wait ~30 seconds for MySQL to finish initializing, then activate your license
and run the setup wizard:

```bash
docker compose exec app php /var/www/html/deploy/on-premises/activate-license.php YOUR_LICENSE_KEY
docker compose exec app php /var/www/html/deploy/on-premises/setup-wizard.php
```

Since this is a self-signed certificate, your browser will warn about it —
click through ("Advanced → Proceed") the first time.

- **Admin:** `https://localhost/cp/` (finish company + admin setup here)
- **ERP:** `https://localhost/erp/`

## Stopping / restarting

```bash
docker compose stop      # pause everything, keep data
docker compose up -d     # resume
docker compose down -v   # full teardown, including the database volume
```

## Notes for a laptop trial

- This is not sized for production load — see `README.md` → System Requirements for real deployments.
- Backups still run on the same cron schedule inside the `cron` container; they land in `./backups` on your host.
- If port 80/443 is already in use (common on macOS with other local dev tools), change `HTTP_PORT`/`HTTPS_PORT` in `.env` before running `docker compose up -d`, then use `https://localhost:<HTTPS_PORT>/cp/`.
