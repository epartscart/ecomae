#!/usr/bin/env bash
# Read-only CloudPanel disk / load report. Prints sizes. Deletes nothing.
# Does not stop PHP-FPM, nginx, MariaDB, or Kestrel.
# Run as root on srv1672837 / the live CloudPanel host.
set -euo pipefail

printf '== EcomAE CloudPanel disk/load diagnose (read-only) ==\n'
printf 'host=%s utc=%s\n' "$(hostname -f 2>/dev/null || hostname)" "$(date -u +%Y-%m-%dT%H:%M:%SZ)"
printf 'This script does not delete files, drop databases, or restart services.\n\n'

printf -- '-- load / memory --\n'
uptime || true
free -h || true
printf '\n-- filesystems --\n'
df -hT || true
printf '\n-- inodes --\n'
df -ih || true

printf '\n-- top-level disk (may take a minute) --\n'
for p in / /home /var /var/www /var/lib /var/log /opt /root /tmp /home/ecomae /home/cloudpanel /home/clp; do
  if [[ -d "$p" ]]; then
    du -xh --max-depth=1 "$p" 2>/dev/null | sort -h | tail -12
    printf '\n'
  fi
done

printf -- '-- known EcomAE / CloudPanel hogs --\n'
for p in \
  /var/www/ecomae-aspnet \
  /var/www/ecomae-aspnet/releases \
  /var/www/ecomae-aspnet/current \
  /opt/ecomae-aspnet-source \
  /root/ecomae \
  /home/ecomae/htdocs \
  /home/ecomae/htdocs/www.ecomae.com \
  /home/ecomae/htdocs/www.ecomae.com/content/files \
  /home/ecomae/htdocs/www.ecomae.com/sitemap_cache \
  /home/ecomae/htdocs/www.ecomae.com/content/files/epc_cache \
  /var/lib/mysql \
  /var/log/nginx \
  /var/log/mysql \
  /var/log/php \
  /var/log/journal \
  /home/clp/backups \
  /home/cloudpanel/backups \
  /home/ecomae/backups
do
  if [[ -e "$p" ]]; then
    du -sh "$p" 2>/dev/null || true
  fi
done

printf '\n-- ASP.NET releases (keep current + one previous; do not rm until you read this) --\n'
if [[ -d /var/www/ecomae-aspnet/releases ]]; then
  ls -ld /var/www/ecomae-aspnet/current 2>/dev/null || true
  readlink -f /var/www/ecomae-aspnet/current 2>/dev/null || true
  du -sh /var/www/ecomae-aspnet/releases/* 2>/dev/null | sort -h || true
  printf 'release_count=%s\n' "$(find /var/www/ecomae-aspnet/releases -mindepth 1 -maxdepth 1 -type d | wc -l)"
fi

printf '\n-- leftover throwaway schemas (must be 0) --\n'
if command -v mysql >/dev/null 2>&1; then
  mysql -N -e "SHOW DATABASES LIKE 'ecomae_cpw_%';" 2>/dev/null || \
    mysql --defaults-extra-file=/etc/mysql/debian.cnf -N -e "SHOW DATABASES LIKE 'ecomae_cpw_%';" 2>/dev/null || \
    printf 'mysql leftover scan skipped (no socket auth)\n'
  mysql -N -e "SELECT table_schema, ROUND(SUM(data_length+index_length)/1024/1024,1) MB FROM information_schema.tables GROUP BY table_schema ORDER BY MB DESC;" 2>/dev/null | head -20 || true
else
  printf 'mysql client missing\n'
fi

printf '\n-- PHP-FPM packages (tracker: many parallel versions eat disk) --\n'
dpkg -l 'php*-fpm' 2>/dev/null | awk '/^ii/{print $2,$3}' || true
systemctl list-units --type=service --state=running 'php*' 'ecomae*' 'nginx' 'mysql' 'mariadb' 2>/dev/null || true

printf '\n-- journal / log sizes --\n'
journalctl --disk-usage 2>/dev/null || true
du -sh /var/log/* 2>/dev/null | sort -h | tail -20 || true

printf '\n-- loopback health --\n'
curl -fsS -o /dev/null -w 'kestrel_health=%{http_code} time=%{time_total}\n' --max-time 8 http://127.0.0.1:5100/health || printf 'kestrel_health=FAIL\n'
curl -fsS -o /dev/null -w 'kestrel_ready=%{http_code} time=%{time_total}\n' --max-time 8 http://127.0.0.1:5100/ready || printf 'kestrel_ready=FAIL\n'
curl -fsS -o /dev/null -w 'nginx_local=%{http_code} time=%{time_total}\n' --max-time 8 -H 'Host: www.ecomae.com' http://127.0.0.1/health || printf 'nginx_local=FAIL\n'

printf '\n-- what this does NOT do --\n'
printf 'No rm, no DROP DATABASE, no snapshot delete, no PHP-FPM prune.\n'
printf 'Measured 2026-10-11 on srv1672837: 61G/96G (63%%), load 0.85, health 200.\n'
printf 'Biggest hog: /var/www/ecomae-aspnet/releases = 24G across 273 stamps.\n'
printf 'Current keep: 20261010085717. syslog+journal ~2.6G. nginx logs are 76K.\n'
printf 'PHP stays. After you read the sizes, safe first cuts are usually:\n'
printf '  1) Dry-run then confirm scripts/cloudpanel_prune_aspnet_releases.sh (current + one previous).\n'
printf '  2) CloudPanel snapshots: keep 1 recent, delete older from the UI (4 shown on Overview).\n'
printf '  3) journalctl --vacuum-size=200M and rotate /var/log/syslog (not nginx).\n'
printf 'Do not prune PHP-FPM versions. Do not delete /opt/ecomae-aspnet-source.\n'
printf 'Do not drop docpart / ecomae / tenant DBs. Do not invent leftover ecomae_cpw_ if any exist — list first.\n'
