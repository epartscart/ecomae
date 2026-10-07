#!/usr/bin/env bash
# Serves the real PHP gateway pages (php -S) from a temp docroot against a throwaway database and records the goldens.
# Usage: harness.sh <repo_root> <mysql_password> <out_dir>
# content/general_pages is left out so epc_portal_apply_config cannot redirect the tenant database.
set -euo pipefail
root="$1"
password="$2"
out="$3"
db="ecomae_cpw_gw$(date +%s)"
docroot="$(mktemp -d)"
port=18099
trap 'kill "$server" 2>/dev/null || true; mysql -u ecomae -p"$password" -e "DROP DATABASE IF EXISTS \`$db\`"; rm -rf "$docroot"' EXIT

mysql -u ecomae -p"$password" -e "CREATE DATABASE \`$db\`"
mysql -u ecomae -p"$password" "$db" < "$(dirname "$0")/fixture.sql"

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

php -S 127.0.0.1:$port -t "$docroot" > /dev/null 2>&1 &
server=$!
sleep 1

lang='multilang_params={"lang":"en","lang_href":"/en","lang_href_no_slash":"en"}&check=s'
base="http://127.0.0.1:$port/content/shop/finance/payment_systems"
mkdir -p "$out"

# name path form [cookie]
capture() {
	local name="$1" path="$2" form="$3" cookie="${4:-session=cs; u_id=5}"
	curl -s -o "$out/$name.html" -D "$out/$name.headers" -b "$cookie" --data "$lang&$form" "$base/$path"
	meta "$name"
}

# name path json signature
capture_json() {
	local name="$1" path="$2" json="$3" sig="$4"
	curl -s -o "$out/$name.html" -D "$out/$name.headers" -H 'Content-Type: application/json' -H "x-nowpayments-sig: $sig" --data "$json" "$base/$path"
	meta "$name"
}

meta() {
	local headers="$out/$1.headers"
	local status type location
	status="$(head -1 "$headers" | awk '{print $2}')"
	type="$( (grep -i '^Content-Type:' "$headers" || true) | head -1 | sed 's/^[^:]*: //' | tr -d '\r')"
	location="$( (grep -i '^Location:' "$headers" || true) | head -1 | sed 's/^[^:]*: //' | tr -d '\r')"
	printf '%s|%s|%s' "$status" "$type" "$location" > "$out/$1.meta"
	rm -f "$headers"
}

capture g2p_stripe_order 'stripe/go_to_pay.php?operation=40&csrf_guard_key=ck' ''
capture g2p_stripe_topup 'stripe/go_to_pay.php?operation=41&csrf_guard_key=ck' ''
capture g2p_paypal_live 'paypal/go_to_pay.php?operation=40&csrf_guard_key=ck' ''
capture g2p_nowpayments 'nowpayments/go_to_pay.php?operation=40&csrf_guard_key=ck' ''
capture g2p_code2 'stripe/go_to_pay.php?operation=42&csrf_guard_key=ck' ''
capture g2p_csrf1 'stripe/go_to_pay.php?operation=40' ''
capture g2p_csrf4 'stripe/go_to_pay.php?operation=40&csrf_guard_key=bad' ''
capture g2p_csrf31 'stripe/go_to_pay.php?operation=40&csrf_guard_key=ck' '' 'session=nope; u_id=5'
capture g2p_no_handler 'go_to_pay.php' ''

payform='EPC_PAY_HANDLER=amazon_ps&operation_id=40&sum=1234.5&operation_description=Pay+%3Corder%3E+%22x%22&currency=AED&user_id=5'
capture pay_entry 'pay_page_entry.php' "$payform"
capture pay_demo_entry 'epc_demo/pay_page.php' "$payform"
capture pay_success 'pay_page_entry.php' "$payform&action=pay_execute&need_result=success"
capture pay_declined 'pay_page.php' "$payform&action=pay_execute&need_result=error"
capture pay_entry_zero 'pay_page_entry.php' 'EPC_PAY_HANDLER=0&sum=abc'
capture pay_direct_zero 'pay_page.php' 'EPC_PAY_HANDLER=0'
capture pay_entry_upper 'pay_page_entry.php' 'EPC_PAY_HANDLER=Stripe_X1'

cryptoform='EPC_PAY_HANDLER=nowpayments&operation_id=40&sum=150&operation_description=Order+300&currency=aed&user_id=5'
capture crypto_pick 'crypto_pay_page.php' "$cryptoform"
capture crypto_btc 'crypto_pay_page.php' "$cryptoform&action=create_invoice&pay_coin=btc"
capture crypto_btc_small 'crypto_pay_page.php' 'EPC_PAY_HANDLER=nowpayments&operation_id=41&sum=1&currency=usd&action=create_invoice&pay_coin=btc'
capture crypto_usdt 'crypto_pay_page.php' "$cryptoform&action=create_invoice&pay_coin=usdttrc20"
capture crypto_doge 'crypto_pay_page.php' "$cryptoform&action=create_invoice&pay_coin=doge"
capture crypto_bad_coin 'crypto_pay_page.php' "$cryptoform&action=create_invoice&pay_coin=eth"
capture crypto_confirm 'crypto_pay_page.php' "$cryptoform&action=confirm_demo&pay_coin=btc"
capture crypto_live_dummy 'crypto_pay_page.php' 'EPC_PAY_HANDLER=paypal&operation_id=40&sum=150&currency=usd&action=create_invoice&pay_coin=btc'
capture crypto_live_pick 'crypto_pay_page.php' 'EPC_PAY_HANDLER=paypal&operation_id=40&sum=150&currency=usd'

capture notify_already 'stripe/notification.php' 'operation_id=42&sum=99&demo_token=epc-demo-ok'
capture notify_forbidden 'paypal/notification.php' 'operation_id=40&sum=150'
capture notify_no_handler 'epc_demo/notification.php' 'operation_id=40'

rejected='{"payment_status":"waiting","order_id":"40","price_amount":150}'
capture_json ipn_rejected 'nowpayments/notification.php' "$rejected" "$(printf '%s' "$rejected" | openssl dgst -sha512 -hmac sek | awk '{print $2}')"
already='{"payment_id":7,"payment_status":"finished","order_id":"42","price_amount":99}'
capture_json ipn_already 'nowpayments/notification.php' "$already" "$(printf '%s' "$already" | openssl dgst -sha512 -hmac sek | awk '{print $2}')"
capture_json ipn_bad_sig 'nowpayments/notification.php' "$already" 'deadbeef'
