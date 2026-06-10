<?php
/**
 * Verify guest price hiding + CP storage redirect fix on epartscart.
 * GET ?token=epartscart-deploy-2026
 */
declare(strict_types=1);

require_once __DIR__ . '/epc_deploy_auth.php';
epc_deploy_require_token();
header('Content-Type: text/html; charset=utf-8');

$token = 'epartscart-deploy-2026';
$host = 'www.epartscart.com';
$platform = 'www.ecomae.com';

function epc_spv_fetch(string $url, array $opts = array()): array
{
	$follow = !empty($opts['follow']);
	$ctx = stream_context_create(array(
		'http' => array(
			'timeout' => (int) ($opts['timeout'] ?? 45),
			'ignore_errors' => true,
			'follow_location' => $follow ? 1 : 0,
			'header' => "User-Agent: EPC-Storefront-Price-Guest-Verify/1.0\r\n",
		),
		'ssl' => array('verify_peer' => false, 'verify_peer_name' => false),
	));
	$body = @file_get_contents($url, false, $ctx);
	$code = 0;
	$location = '';
	if (isset($http_response_header[0]) && preg_match('/\s(\d{3})\s/', $http_response_header[0], $m)) {
		$code = (int) $m[1];
	}
	foreach ((array) $http_response_header as $hdr) {
		if (stripos($hdr, 'Location:') === 0) {
			$location = trim(substr($hdr, 9));
		}
	}
	return array(
		'code' => $code,
		'location' => $location,
		'body' => is_string($body) ? $body : '',
	);
}

function epc_spv_has_price_amount(string $html): bool
{
	if (preg_match('/epc-price-login-cta|to see prices/i', $html)) {
		return false;
	}
	return (bool) preg_match('/class="(?:td_price|epc-price-value|price_div_text|price)"[^>]*>[\s\S]{0,400}?\d[\d\s.,]{2,}/i', $html);
}

function epc_spv_row(string $feature, string $status, string $detail): string
{
	$cls = $status === 'PASS' ? 'pass' : ($status === 'WARN' ? 'warn' : 'fail');
	return '<tr class="' . $cls . '"><td>' . htmlspecialchars($feature, ENT_QUOTES, 'UTF-8')
		. '</td><td><strong>' . htmlspecialchars($status, ENT_QUOTES, 'UTF-8')
		. '</strong></td><td>' . htmlspecialchars($detail, ENT_QUOTES, 'UTF-8') . '</td></tr>';
}

$rows = array();

// 1 — Helper deployed locally
$helper = __DIR__ . '/content/shop/docpart/epc_storefront_prices_helpers.php';
$helperSrc = is_file($helper) ? (string) file_get_contents($helper) : '';
$rows[] = epc_spv_row(
	'Shared helper epc_storefront_prices_visible_for_user()',
	is_file($helper) && stripos($helperSrc, 'function epc_storefront_prices_visible_for_user') !== false ? 'PASS' : 'FAIL',
	is_file($helper) ? 'stage/content/shop/docpart/epc_storefront_prices_helpers.php' : 'missing'
);

// 2 — Guest GUT21 parts page
$gut21 = epc_spv_fetch('https://' . $host . '/en/parts/GMB/GUT21', array('timeout' => 60));
$gut21HasCta = stripos($gut21['body'], 'epc-price-login-cta') !== false
	|| stripos($gut21['body'], 'to see prices') !== false
	|| stripos($gut21['body'], 'epc_storefront_prices_visible = false') !== false;
$gut21HasLeakedPrice = epc_spv_has_price_amount($gut21['body']);
$rows[] = epc_spv_row(
	'Guest /en/parts/GMB/GUT21 hides prices',
	($gut21['code'] === 200 && ($gut21HasCta || !$gut21HasLeakedPrice)) ? 'PASS' : 'FAIL',
	'HTTP ' . $gut21['code'] . '; login CTA=' . ($gut21HasCta ? 'yes' : 'no') . '; leaked=' . ($gut21HasLeakedPrice ? 'yes' : 'no')
);

// 3 — Guest available-brands
$brands = epc_spv_fetch('https://' . $host . '/en/available-brands', array('timeout' => 45));
$brandsHasFlag = stripos($brands['body'], 'data-prices-visible="0"') !== false;
$rows[] = epc_spv_row(
	'Guest /en/available-brands hides prices',
	($brands['code'] === 200 && $brandsHasFlag) ? 'PASS' : 'FAIL',
	'HTTP ' . $brands['code'] . '; data-prices-visible=0 ' . ($brandsHasFlag ? 'present' : 'missing')
);

// 4 — brand_parts API guest redaction
$api = epc_spv_fetch('https://' . $host . '/api/umapi_proxy.php?action=brand_parts&brand=GMB&limit=3', array('timeout' => 45));
$apiJson = json_decode($api['body'], true);
$apiPricesHidden = is_array($apiJson)
	&& array_key_exists('prices_visible', $apiJson)
	&& $apiJson['prices_visible'] === false;
$apiLeaked = false;
if (is_array($apiJson) && !empty($apiJson['data'][0]['price'])) {
	$apiLeaked = ((float) $apiJson['data'][0]['price']) > 0;
}
$rows[] = epc_spv_row(
	'Guest brand_parts API redacts prices',
	($api['code'] === 200 && $apiPricesHidden && !$apiLeaked) ? 'PASS' : 'FAIL',
	'HTTP ' . $api['code'] . '; prices_visible=' . (is_array($apiJson) ? json_encode($apiJson['prices_visible'] ?? null) : 'n/a')
);

// 5 — CP storage page (no PHP in URL)
$storage = epc_spv_fetch('https://' . $host . '/cp/shop/logistics/storages/storage', array('timeout' => 45, 'follow' => true));
$storageBadUrl = stripos($storage['body'], '%3C?php') !== false
	|| stripos($storage['body'], '<?php echo') !== false;
$storageOk = in_array($storage['code'], array(200, 302, 303), true) && !$storageBadUrl;
$rows[] = epc_spv_row(
	'CP /shop/logistics/storages/storage loads cleanly',
	$storageOk ? 'PASS' : 'FAIL',
	'HTTP ' . $storage['code'] . '; php-in-url=' . ($storageBadUrl ? 'yes' : 'no')
);

// 6 — Storage redirect source fix
$storagePhp = __DIR__ . '/cp/content/shop/logistics/storage.php';
$storageSrc = is_file($storagePhp) ? (string) file_get_contents($storagePhp) : '';
$redirectFixed = stripos($storageSrc, 'epc_cp_redirect(') !== false
	&& stripos($storageSrc, 'location="<?php echo $DP_Config->domain_path') === false;
$rows[] = epc_spv_row(
	'Storage module uses epc_cp_redirect()',
	$redirectFixed ? 'PASS' : 'FAIL',
	is_file($storagePhp) ? 'cp/content/shop/logistics/storage.php' : 'missing'
);

// 7 — Demo DB removal
$demoRemove = epc_spv_fetch('https://' . $platform . '/epc-tenant-remove-demo.php?token=' . urlencode($token) . '&site_key=demo_260607_ap_2', array('timeout' => 45));
$demoJson = json_decode($demoRemove['body'], true);
$demoGone = is_array($demoJson) && (!empty($demoJson['ok']) && empty($demoJson['found']));
$rows[] = epc_spv_row(
	'Orphan demo DB demo_260607_ap_2 removed',
	$demoGone ? 'PASS' : 'WARN',
	is_array($demoJson) ? (string) ($demoJson['message'] ?? 'unknown') : 'probe failed'
);

// 8 — Demo hub columns
$demoHub = epc_spv_fetch('https://' . $platform . '/epc-demo-hub-verify.php?token=' . urlencode($token), array('timeout' => 45));
$hubJson = json_decode($demoHub['body'], true);
$hubOk = !empty($hubJson['ok']);
$rows[] = epc_spv_row(
	'Super CP demos tab password + CP login columns',
	$hubOk ? 'PASS' : 'FAIL',
	$hubOk ? 'epc-demo-hub-verify ok' : 'see epc_demo_tenants_manage.php'
);

$pass = 0;
$fail = 0;
foreach ($rows as $row) {
	if (strpos($row, 'class="pass"') !== false) {
		$pass++;
	} elseif (strpos($row, 'class="fail"') !== false) {
		$fail++;
	}
}

?><!DOCTYPE html>
<html lang="en">
<head>
<meta charset="utf-8">
<title>EPC storefront price guest verify</title>
<style>
body{font-family:Segoe UI,system-ui,sans-serif;margin:24px;background:#f8fafc;color:#0f172a}
h1{font-size:22px;margin:0 0 8px}
p{color:#475569;margin:0 0 18px}
table{border-collapse:collapse;width:100%;max-width:980px;background:#fff;box-shadow:0 1px 3px rgba(15,23,42,.08)}
th,td{border:1px solid #e2e8f0;padding:10px 12px;text-align:left;font-size:13px;vertical-align:top}
th{background:#f1f5f9}
tr.pass td:nth-child(2){color:#15803d}
tr.fail td:nth-child(2){color:#b91c1c}
tr.warn td:nth-child(2){color:#b45309}
.summary{margin-top:16px;font-weight:600}
</style>
</head>
<body>
<h1>Storefront guest price + CP storage verify</h1>
<p>Host: <?php echo htmlspecialchars($host, ENT_QUOTES, 'UTF-8'); ?> — <?php echo gmdate('Y-m-d H:i:s'); ?> UTC</p>
<table>
<thead><tr><th>Feature</th><th>Status</th><th>Detail</th></tr></thead>
<tbody>
<?php echo implode("\n", $rows); ?>
</tbody>
</table>
<p class="summary">Summary: pass=<?php echo (int) $pass; ?> fail=<?php echo (int) $fail; ?></p>
</body>
</html>
