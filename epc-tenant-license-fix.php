<?php
/**
 * Repair tenant domain_path for Docpart license check (dp_core 1.01/1.02).
 * https://www.taxofinca.com/epc-tenant-license-fix.php?token=...&host=www.taxofinca.com&apply=1
 */
declare(strict_types=1);

require_once __DIR__ . '/epc_deploy_auth.php';
epc_deploy_require_token();
header('Content-Type: text/plain; charset=utf-8');
set_time_limit(120);

$apply = !empty($_GET['apply']);
$hostParam = strtolower(trim((string) ($_GET['host'] ?? '')));
if ($hostParam === '') {
	$hostParam = function_exists('epc_portal_host')
		? epc_portal_host()
		: strtolower(trim((string) ($_SERVER['HTTP_HOST'] ?? '')));
}
if ($hostParam !== '' && strpos($hostParam, ':') !== false) {
	$hostParam = explode(':', $hostParam, 2)[0];
}
if ($hostParam === '') {
	exit("host required (pass host=www.example.com or call on tenant vhost)\n");
}

$bareHost = preg_replace('/^www\./', '', $hostParam);
$expectedDomain = 'https://' . $hostParam . '/';

function epc_tlf_probe(string $url): string
{
	$ctx = stream_context_create(array(
		'http' => array('timeout' => 25, 'ignore_errors' => true),
		'ssl' => array('verify_peer' => false, 'verify_peer_name' => false),
	));
	$body = @file_get_contents($url, false, $ctx);
	$code = 0;
	if (isset($http_response_header) && is_array($http_response_header)) {
		foreach ($http_response_header as $h) {
			if (preg_match('/^\s*HTTP\/\S+\s+(\d{3})/', $h, $m)) {
				$code = (int) $m[1];
			}
		}
	}
	$hint = '';
	if (is_string($body) && $body !== '') {
		if (stripos($body, 'License error') !== false) {
			$hint = ' [' . trim(preg_replace('/\s+/', ' ', substr(strip_tags($body), 0, 80))) . ']';
		} elseif (stripos($body, 'Taxofin') !== false) {
			$hint = ' [taxofinca]';
		} elseif (stripos($body, 'eParts Cart') !== false) {
			$hint = ' [epartscart]';
		} elseif (stripos($body, '<html') !== false) {
			$hint = ' [html]';
		}
	}
	return "HTTP {$code}{$hint}";
}

echo "=== Tenant license / domain_path fix ===\n";
echo "host={$hostParam}\n";
echo "expected_domain_path={$expectedDomain}\n";
echo "apply=" . ($apply ? 'yes' : 'no') . "\n\n";

echo "Before probes:\n";
echo '  https://' . $hostParam . '/: ' . epc_tlf_probe('https://' . $hostParam . '/') . "\n";
echo '  https://' . $hostParam . '/cp/: ' . epc_tlf_probe('https://' . $hostParam . '/cp/') . "\n";
echo '  https://' . $hostParam . '/erp/: ' . epc_tlf_probe('https://' . $hostParam . '/erp/') . "\n\n";

define('_ASTEXE_', 1);
require_once __DIR__ . '/config.php';
$cfg = new DP_Config();
require_once __DIR__ . '/content/general_pages/epc_portal.php';
require_once __DIR__ . '/content/general_pages/epc_portal_db.php';

$_SERVER['HTTP_HOST'] = $hostParam;
$_SERVER['SERVER_NAME'] = $hostParam;

epc_portal_apply_config($cfg);

echo "Runtime (simulated Host {$hostParam}):\n";
echo '  HTTP_HOST=' . ($_SERVER['HTTP_HOST'] ?? '') . "\n";
echo '  SERVER_NAME=' . ($_SERVER['SERVER_NAME'] ?? '') . "\n";
echo '  domain_path=' . $cfg->domain_path . "\n";
$domainHost = parse_url($cfg->domain_path, PHP_URL_HOST);
echo '  domain_host=' . (string) $domainHost . "\n";
echo '  is_client=' . (epc_portal_is_client_hostname($hostParam) ? 'yes' : 'no') . "\n";
echo '  license_1.01=' . ((is_string($domainHost) && $domainHost === $hostParam) ? 'ok' : 'FAIL') . "\n\n";

$pdo = null;
try {
	$pdo = new PDO(
		'mysql:host=' . $cfg->host . ';dbname=' . $cfg->db . ';charset=utf8',
		$cfg->user,
		$cfg->password,
		array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION)
	);
} catch (Exception $e) {
	echo 'DB connect failed: ' . $e->getMessage() . "\n";
	exit(1);
}

echo "DB: {$cfg->user}@{$cfg->db}\n";

if ($apply) {
	try {
		epc_portal_db_ensure($pdo);
		$st = $pdo->prepare('SELECT `host`, `domain_path` FROM `epc_portal_site_settings` WHERE `host` IN (?, ?)');
		$st->execute(array($hostParam, 'www.' . $bareHost));
		$before = $st->fetchAll(PDO::FETCH_ASSOC);
		echo "site_settings before: " . json_encode($before) . "\n";

		$industry = 'auto_parts';
		if (strpos($hostParam, 'taxofinca') !== false && function_exists('epc_portal_industry')) {
			$industry = 'tax_advisory';
		}
		$save = array(
			'host' => $hostParam,
			'industry_code' => $industry,
			'domain_path' => $expectedDomain,
		);
		if (strpos($hostParam, 'taxofinca') !== false) {
			$ind = epc_portal_industry('tax_advisory');
			$save['access_mode'] = 'erp_only';
			$save['theme_template'] = 'classic';
			$save['system_name'] = 'Taxofin';
			$save['hub_name'] = 'Taxofin';
			$save['tagline'] = 'Tax & advisory services';
			$save['enabled_packs'] = isset($ind['cp_packs']) ? $ind['cp_packs'] : array('core', 'erp', 'professional', 'tax_advisory');
		}
		epc_portal_save_site_settings($pdo, $save);
		echo "site_settings saved for {$hostParam}\n";

		$stUp = $pdo->prepare(
			'UPDATE `epc_portal_site_settings` SET `domain_path` = ? WHERE `host` = ? OR `host` = ? OR `host` = ?'
		);
		$stUp->execute(array($expectedDomain, $hostParam, $bareHost, 'www.' . $bareHost));
		echo 'domain_path rows updated=' . $stUp->rowCount() . "\n";
	} catch (Exception $e) {
		echo 'site_settings error: ' . $e->getMessage() . "\n";
	}

	epc_portal_apply_config($cfg);
	echo "after apply domain_path={$cfg->domain_path}\n";
}

echo "\nAfter probes:\n";
echo '  https://' . $hostParam . '/: ' . epc_tlf_probe('https://' . $hostParam . '/') . "\n";
echo '  https://' . $hostParam . '/cp/: ' . epc_tlf_probe('https://' . $hostParam . '/cp/') . "\n";
echo '  https://' . $hostParam . '/erp/: ' . epc_tlf_probe('https://' . $hostParam . '/erp/') . "\n";

echo "\nDone.\n";
