#!/usr/bin/env bash
# Serves the real PHP content/shop/document_control/service/print.php (php -S) from a temp docroot against a throwaway
# database and records the goldens (body, then status|content type in .meta). today.txt holds PHP date('d M Y').
# Usage: harness.sh <repo_root> <mysql_password> <out_dir>
# content/general_pages is left out so epc_portal_apply_config cannot redirect the tenant database.
set -euo pipefail
root="$1"
password="$2"
out="$3"
db="ecomae_cpw_dc$(date +%s)"
docroot="$(mktemp -d)"
port=18098
cnf="$(mktemp)"
printf '[client]\nuser=ecomae\npassword=%s\nhost=127.0.0.1\n' "$password" > "$cnf"
trap 'kill "$server" 2>/dev/null || true; mysql --defaults-extra-file="$cnf" -e "DROP DATABASE IF EXISTS \`$db\`"; rm -rf "$docroot" "$cnf"' EXIT

mysql --defaults-extra-file="$cnf" -e "CREATE DATABASE \`$db\`"
mysql --defaults-extra-file="$cnf" "$db" < "$(dirname "$0")/fixture.sql"

mkdir -p "$docroot/content"
ln -s "$root/lang" "$docroot/lang"
for entry in "$root"/content/*; do
	[ "$(basename "$entry")" = "general_pages" ] || ln -s "$entry" "$docroot/content/$(basename "$entry")"
done
cat > "$docroot/config.php" <<PHP
<?php
class DP_Config
{
	public \$host = '127.0.0.1';
	public \$db = '$db';
	public \$user = 'ecomae';
	public \$password = '$password';
	public \$domain_path = 'https://www.epartscart.com/';
	public \$backend_dir = 'cp';
	public \$secret_succession = 's';
	public \$multilang = false;
}
PHP

php -d date.timezone=UTC -S 127.0.0.1:$port -t "$docroot" > /dev/null 2>&1 &
server=$!
sleep 1
mkdir -p "$out"
php -d date.timezone=UTC -r 'echo date("d M Y");' > "$out/today.txt"

admin='admin_session=adm-sess; admin_u_id=1'
# name query cookie
capture() {
	local name="$1" query="$2" cookie="${3:-}"
	curl -s -o "$out/$name.html" -D "$out/$name.headers" -b "$cookie" "http://127.0.0.1:$port/content/shop/document_control/service/print.php?$query"
	local headers="$out/$name.headers" status type
	status="$(head -1 "$headers" | awk '{print $2}')"
	type="$( (grep -i '^Content-Type:' "$headers" || true) | head -1 | sed 's/^[^:]*: //' | tr -d '\r')"
	printf '%s|%s' "$status" "$type" > "$out/$name.meta"
	rm -f "$headers"
}

capture guest 'doc=fta_tax_invoice&order_id=40'
capture wrong_user 'preview=1' 'session=tok-21; u_id=22'
capture customer_plain 'preview=1' 'session=tok-28; u_id=28'
capture staff_inactive 'preview=1' 'session=tok-29; u_id=29'
capture backend_child 'preview=1' 'session=tok-21; u_id=21'
capture backend_tree 'preview=1' 'session=tok-22; u_id=22'
capture administrator_group 'preview=1' 'session=tok-23; u_id=23'
capture cp_erp_group 'preview=1' 'session=tok-24; u_id=24'
capture staff_profile 'preview=1' 'session=tok-25; u_id=25'
capture department_group 'preview=1' 'session=tok-26; u_id=26'
capture erp_team 'preview=1' 'session=tok-27; u_id=27'

capture admin_default '' "$admin"
capture admin_order_40 'doc=fta_tax_invoice&order_id=40' "$admin"
capture admin_preview_invoice 'doc=fta_tax_invoice&preview=1&invoice_id=7&order_id=40' "$admin"
capture invoice_7 'doc=fta_tax_invoice&invoice_id=7' "$admin"
capture invoice_7_packing 'doc=packing_slip&invoice_id=7' "$admin"
capture invoice_7_receipt 'doc=payment_receipt&invoice_id=7&order_id=40' "$admin"
capture invoice_8_delivery 'doc=delivery_note&invoice_id=8' "$admin"
capture invoice_10 'invoice_id=10' "$admin"
capture invoice_inactive 'invoice_id=9' "$admin"
capture order_missing 'order_id=999' "$admin"
capture doc_unknown 'doc=nope%3Cx%3E&order_id=40' "$admin"
