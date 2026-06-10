<?php
/**
 * Unified probe + repair for platform tenant domains on the shared Model C host.
 * https://www.ecomae.com/epc-tenants-connectivity-fix.php?token=...&clp_pass=...&apply=1
 */
declare(strict_types=1);

require_once __DIR__ . '/epc_deploy_auth.php';
epc_deploy_require_token();
header('Content-Type: text/plain; charset=utf-8');
set_time_limit(0);

require_once __DIR__ . '/content/general_pages/epc_cloudpanel_helpers.php';
require_once __DIR__ . '/content/general_pages/epc_portal.php';
require_once __DIR__ . '/content/general_pages/epc_portal_db.php';
require_once __DIR__ . '/content/general_pages/epc_portal_tenant.php';
require_once __DIR__ . '/content/general_pages/epc_portal_theme_templates.php';

$clpPass = trim((string) ($_GET['clp_pass'] ?? getenv('CLP_PASS') ?: ''));
$ecomaeDbPass = trim((string) ($_GET['db_password'] ?? ''));
$hostingerToken = trim((string) ($_GET['hostinger_token'] ?? getenv('HOSTINGER_API_TOKEN') ?: ''));
$apply = !empty($_GET['apply']);
$nginxOnly = !empty($_GET['nginx_only']);

$platformSite = 'www.ecomae.com';
$platformDocroot = '/home/ecomae/htdocs/www.ecomae.com';
$platformIp = epc_portal_platform_ip();

$tenants = array(
	'epartscart' => array(
		'www' => 'www.epartscart.com',
		'bare' => 'epartscart.com',
		'site_key' => 'epartscart',
		'industry' => 'auto_parts',
		'access_mode' => 'full',
		'db_name' => 'docpart',
		'db_user' => 'docpart',
	),
	'taxofinca' => array(
		'www' => 'www.taxofinca.com',
		'bare' => 'taxofinca.com',
		'site_key' => 'taxofinca',
		'industry' => 'tax_advisory',
		'access_mode' => 'consultancy',
		'db_name' => 'docpart',
		'db_user' => 'docpart',
	),
	'electronicae' => array(
		'www' => 'www.electronicae.com',
		'bare' => 'electronicae.com',
		'site_key' => 'electronicae',
		'industry' => 'electronics',
		'access_mode' => 'full',
		'db_name' => 'docpart',
		'db_user' => 'docpart',
	),
	'stylenlook' => array(
		'www' => 'www.stylenlook.com',
		'bare' => 'stylenlook.com',
		'site_key' => 'stylenlook',
		'industry' => 'fashion',
		'access_mode' => 'full',
		'db_name' => 'docpart',
		'db_user' => 'docpart',
	),
	'thejewellerytrend' => array(
		'www' => 'www.thejewellerytrend.com',
		'bare' => 'thejewellerytrend.com',
		'site_key' => 'thejewellerytrend',
		'industry' => 'jewellery',
		'access_mode' => 'full',
		'db_name' => 'docpart',
		'db_user' => 'docpart',
	),
);

$allAliases = array();
foreach ($tenants as $t) {
	$allAliases[] = $t['www'];
	$allAliases[] = $t['bare'];
}
$allAliases = array_values(array_unique($allAliases));
$deprecatedHosts = array(
	'www.thethejewellerytrend.com',
	'thethejewellerytrend.com',
);

function epc_tcf_run(string $cmd): string
{
	$r = epc_clp_run_cmd($cmd);
	$out = isset($r['output']) ? trim((string) $r['output']) : '';
	$code = isset($r['code']) ? (int) $r['code'] : -1;
	return ($out !== '' ? $out : '(empty)') . ' [exit=' . $code . ']';
}

function epc_tcf_probe(string $url, string $hostHeader = ''): array
{
	$headers = $hostHeader !== '' ? ("Host: {$hostHeader}\r\n") : '';
	$ctx = stream_context_create(array(
		'http' => array('timeout' => 20, 'ignore_errors' => true, 'header' => $headers),
		'ssl' => array('verify_peer' => false, 'verify_peer_name' => false),
	));
	$t0 = microtime(true);
	$body = @file_get_contents($url, false, $ctx);
	$ms = (int) round((microtime(true) - $t0) * 1000);
	$code = 0;
	$location = '';
	if (isset($http_response_header) && is_array($http_response_header)) {
		foreach ($http_response_header as $h) {
			if (preg_match('/^\s*HTTP\/\S+\s+(\d{3})/', $h, $m)) {
				$code = (int) $m[1];
			}
			if (stripos($h, 'Location:') === 0) {
				$location = trim(substr($h, 9));
			}
		}
	}
	$hint = '';
	if (is_string($body) && $body !== '') {
		if (stripos($body, 'No DB connect') !== false) {
			$hint = ' [no-db]';
		} elseif (stripos($body, 'License') !== false && stripos($body, '1.01') !== false) {
			$hint = ' [license-1.01]';
		} elseif (stripos($body, '525') !== false && stripos($body, 'SSL') !== false) {
			$hint = ' [ssl525]';
		} elseif (stripos($body, '403 Forbidden') !== false) {
			$hint = ' [nginx403]';
		} elseif (stripos($body, '404 Not Found') !== false) {
			$hint = ' [nginx404]';
		} elseif (stripos($body, 'Taxofin') !== false || stripos($body, 'taxofinca') !== false) {
			$hint = ' [taxofinca]';
		} elseif (stripos($body, 'eParts Cart') !== false || stripos($body, 'epartscart') !== false) {
			$hint = ' [epartscart]';
		} elseif (stripos($body, 'ECOM AE') !== false) {
			$hint = ' [ecomae]';
		}
	}
	if ($body === false && $code === 0) {
		return array('label' => 'TIMEOUT', 'code' => 0, 'ms' => $ms, 'hint' => '', 'ok' => false);
	}
	$ok = $code >= 200 && $code < 400;
	if ($ok && is_string($body) && (stripos($body, 'No DB connect') !== false || stripos($body, 'License error') !== false)) {
		$ok = false;
	}
	return array(
		'label' => "HTTP {$code}{$hint}" . ($location !== '' ? " -> {$location}" : ''),
		'code' => $code,
		'ms' => $ms,
		'hint' => $hint,
		'ok' => $ok,
	);
}

function epc_tcf_probe_line(string $url, string $hostHeader = ''): string
{
	$p = epc_tcf_probe($url, $hostHeader);
	return $p['label'] . ' ' . $p['ms'] . 'ms';
}

function epc_tcf_resolve_db_creds(string $platformDocroot): array
{
	$dbPass = '';
	$dbUser = 'docpart';
	$dbName = 'docpart';
	$legacyDocroot = '/home/epartscart/htdocs/www.epartscart.com';
	foreach (array(
		$legacyDocroot . '/config.local.php',
		$legacyDocroot . '/config.php',
		$platformDocroot . '/config.php',
		__DIR__ . '/config.php',
	) as $path) {
		if (!is_file($path)) {
			continue;
		}
		if (substr($path, -13) === 'config.local.php') {
			$epc_config_local = null;
			include $path;
			if (isset($epc_config_local['password']) && (string) $epc_config_local['password'] !== '') {
				$dbPass = (string) $epc_config_local['password'];
			}
			if (isset($epc_config_local['user']) && (string) $epc_config_local['user'] !== '') {
				$dbUser = (string) $epc_config_local['user'];
			}
			if (isset($epc_config_local['db']) && (string) $epc_config_local['db'] !== '') {
				$dbName = (string) $epc_config_local['db'];
			}
			continue;
		}
		if (!class_exists('DP_Config', false)) {
			include_once $path;
		}
		if (!class_exists('DP_Config', false)) {
			continue;
		}
		$cfg = new DP_Config();
		$dbPass = (string) $cfg->password;
		$dbUser = (string) $cfg->user;
		$dbName = (string) $cfg->db;
		break;
	}
	if ($dbName !== 'docpart') {
		$dbName = 'docpart';
		$dbUser = 'docpart';
		$dbPass = '';
	}
	return array('pass' => $dbPass, 'user' => $dbUser, 'name' => $dbName);
}

function epc_tcf_fw_api(string $token, string $method, string $path, ?array $body = null): array
{
	$url = 'https://developers.hostinger.com' . $path;
	$headers = "Authorization: Bearer {$token}\r\nAccept: application/json\r\n";
	$content = '';
	if ($body !== null) {
		$content = json_encode($body);
		$headers .= "Content-Type: application/json\r\n";
	}
	$ctx = stream_context_create(array(
		'http' => array(
			'method' => $method,
			'header' => $headers,
			'content' => $content,
			'timeout' => 60,
			'ignore_errors' => true,
		),
		'ssl' => array('verify_peer' => true),
	));
	$raw = @file_get_contents($url, false, $ctx);
	$status = 0;
	if (isset($http_response_header[0]) && preg_match('/\s(\d{3})\s/', $http_response_header[0], $m)) {
		$status = (int) $m[1];
	}
	$json = is_string($raw) ? json_decode($raw, true) : null;
	return array('status' => $status, 'body' => $json !== null ? $json : $raw);
}

echo "=== Platform tenants connectivity fix ===\n";
echo 'apply=' . ($apply ? 'yes' : 'no') . "\n";
echo 'nginx_only=' . ($nginxOnly ? 'yes' : 'no') . "\n";
echo 'platform_ip=' . $platformIp . "\n";
echo 'platform_site=' . $platformSite . "\n";
echo 'platform_docroot=' . $platformDocroot . "\n";
echo 'server_time=' . gmdate('c') . "\n\n";

echo "=== DNS (server resolver) ===\n";
foreach ($allAliases as $h) {
	$ip = gethostbyname($h);
	$ok = ($ip !== $h && $ip === $platformIp);
	echo '  ' . $h . ' → ' . ($ip === $h ? 'UNRESOLVED' : $ip) . ($ok ? ' OK' : ($ip !== $h ? ' MISMATCH' : '')) . "\n";
}
echo "\n";

$checkUrls = array();
foreach ($tenants as $key => $t) {
	$checkUrls[$key . '_root'] = 'https://' . $t['www'] . '/';
	$checkUrls[$key . '_cp'] = 'https://' . $t['www'] . '/cp/';
}

echo "=== BEFORE (origin + public) ===\n";
foreach ($tenants as $key => $t) {
	echo "[{$key}]\n";
	echo '  origin / Host ' . $t['www'] . ': ' . epc_tcf_probe_line('http://127.0.0.1/', $t['www']) . "\n";
	echo '  origin /cp/ Host ' . $t['www'] . ': ' . epc_tcf_probe_line('http://127.0.0.1/cp/', $t['www']) . "\n";
	echo '  hairpin https://' . $platformIp . '/ Host ' . $t['www'] . ': ' . epc_tcf_probe_line('https://' . $platformIp . '/', $t['www']) . "\n";
	echo '  public https://' . $t['www'] . '/: ' . epc_tcf_probe_line('https://' . $t['www'] . '/') . "\n";
	echo '  public https://' . $t['www'] . '/cp/: ' . epc_tcf_probe_line('https://' . $t['www'] . '/cp/') . "\n";
}
echo "\n";

if (!$apply) {
	echo "Dry run. Re-run with apply=1&clp_pass=... to repair nginx, SSL, registry, firewall.\n";
	echo "Optional: db_password= (ecomae MySQL), hostinger_token= (hPanel API).\n";
	exit;
}

if ($clpPass === '') {
	exit("apply=1 requires clp_pass=\n");
}

if ($nginxOnly) {
	goto epc_tcf_nginx_repair;
}

$dbCreds = epc_tcf_resolve_db_creds($platformDocroot);
$dbPass = $dbCreds['pass'];
$dbUser = $dbCreds['user'];
$dbName = $dbCreds['name'];
echo "Tenant DB: {$dbUser}@{$dbName}\n";

$docpartProbeOk = false;
try {
	$probePdo = new PDO(
		'mysql:host=127.0.0.1;dbname=' . $dbName . ';charset=utf8',
		$dbUser,
		$dbPass,
		array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION)
	);
	$docpartProbeOk = true;
} catch (Exception $e) {
	echo "docpart PDO probe: FAIL — " . $e->getMessage() . "\n";
	$fixUrl = 'https://' . $platformSite . '/epc-docpart-db-fix.php?token=' . urlencode(epc_deploy_token())
		. '&apply=1&clp_pass=' . urlencode($clpPass);
	echo "Running epc-docpart-db-fix.php …\n";
	$fixBody = @file_get_contents($fixUrl, false, stream_context_create(array(
		'http' => array('timeout' => 120, 'ignore_errors' => true),
		'ssl' => array('verify_peer' => false, 'verify_peer_name' => false),
	)));
	if (is_string($fixBody) && $fixBody !== '') {
		echo $fixBody . "\n";
	}
	$dbCreds = epc_tcf_resolve_db_creds($platformDocroot);
	$dbPass = $dbCreds['pass'];
	try {
		$probePdo = new PDO(
			'mysql:host=127.0.0.1;dbname=' . $dbName . ';charset=utf8',
			$dbUser,
			$dbPass,
			array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION)
		);
		$docpartProbeOk = true;
		echo "docpart PDO after fix: OK\n";
	} catch (Exception $e2) {
		echo "docpart PDO after fix: still FAIL — " . $e2->getMessage() . "\n";
	}
}

if ($ecomaeDbPass === '' && is_file($platformDocroot . '/config.local.php')) {
	$epc_config_local = null;
	include $platformDocroot . '/config.local.php';
	if (isset($epc_config_local['password'])) {
		$ecomaeDbPass = (string) $epc_config_local['password'];
	}
}

echo "\n=== 1) OS firewall (80/443) ===\n";
foreach (array(
	'sudo -n ufw allow 80/tcp comment "epc-tenants"',
	'sudo -n ufw allow 443/tcp comment "epc-tenants"',
	'sudo -n ufw reload',
	'sudo -n iptables -C INPUT -p tcp --dport 80 -j ACCEPT 2>/dev/null || sudo -n iptables -I INPUT -p tcp --dport 80 -j ACCEPT',
	'sudo -n iptables -C INPUT -p tcp --dport 443 -j ACCEPT 2>/dev/null || sudo -n iptables -I INPUT -p tcp --dport 443 -j ACCEPT',
) as $cmd) {
	echo $cmd . ': ' . epc_tcf_run($cmd) . "\n";
}

if ($hostingerToken !== '') {
	$vmId = (int) ($_GET['vm_id'] ?? 0);
	$firewallId = (int) ($_GET['firewall_id'] ?? 0);
	$vms = epc_tcf_fw_api($hostingerToken, 'GET', '/api/vps/v1/virtual-machines');
	if ($vmId <= 0 && is_array($vms['body'])) {
		$list = $vms['body']['data'] ?? $vms['body'];
		if (is_array($list)) {
			foreach ($list as $vm) {
				$ips = $vm['ipv4'] ?? array();
				$ip = is_array($ips) && isset($ips[0]['address']) ? (string) $ips[0]['address'] : '';
				if ($ip === $platformIp) {
					$vmId = (int) ($vm['id'] ?? 0);
					break;
				}
			}
		}
	}
	$fws = epc_tcf_fw_api($hostingerToken, 'GET', '/api/vps/v1/firewall');
	if ($firewallId <= 0 && is_array($fws['body'])) {
		$flist = $fws['body']['data'] ?? $fws['body'];
		if (is_array($flist) && isset($flist[0]['id'])) {
			$firewallId = (int) $flist[0]['id'];
		}
	}
	echo 'Hostinger vm_id=' . $vmId . ' firewall_id=' . $firewallId . "\n";
	if ($firewallId > 0) {
		foreach (array('80', '443') as $port) {
			$res = epc_tcf_fw_api($hostingerToken, 'POST', '/api/vps/v1/firewall/' . $firewallId . '/rules', array(
				'protocol' => 'TCP',
				'port' => $port,
				'source' => 'any',
				'sourceDetail' => 'any',
			));
			echo "  API allow TCP {$port}: HTTP {$res['status']}\n";
		}
		if ($vmId > 0) {
			epc_tcf_fw_api($hostingerToken, 'POST', '/api/vps/v1/firewall/' . $firewallId . '/sync/' . $vmId);
			echo "  firewall synced to VM {$vmId}\n";
		}
	}
} else {
	echo "Hostinger API skipped — set hostinger_token= or open TCP 80+443 in hPanel manually.\n";
}

try {
	$tenantPdo = new PDO(
		'mysql:host=127.0.0.1;dbname=' . $dbName . ';charset=utf8',
		$dbUser,
		$dbPass,
		array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION)
	);
	epc_portal_db_ensure($tenantPdo);

	$tenantSettings = array(
		'epartscart' => array(
			'access_mode' => 'full',
			'enabled_packs' => array('core', 'commerce', 'auto_parts', 'logistics', 'erp', 'professional', 'marketing'),
			'system_name' => 'eParts Cart',
			'hub_name' => 'Electronic World Group',
			'tagline' => 'Auto parts & commerce',
			'trade_name' => 'eParts Cart',
			'from_email' => 'partsdoc2025@gmail.com',
		),
		'taxofinca' => array(
			'access_mode' => 'consultancy',
			'enabled_packs' => array('core', 'erp', 'professional', 'tax_advisory'),
			'system_name' => 'Taxofin',
			'hub_name' => 'Taxofinca',
			'tagline' => 'Tax & advisory services — UAE corporate tax, VAT and business compliance',
			'trade_name' => 'Taxofinca',
			'from_email' => 'info@taxofinca.com',
		),
		'electronicae' => array(
			'access_mode' => 'full',
			'enabled_packs' => array('core', 'commerce', 'catalogue'),
			'system_name' => 'Electronicae',
			'hub_name' => 'Electronic World Group',
			'tagline' => 'Electronics & gadgets storefront',
			'trade_name' => 'Electronicae',
			'from_email' => 'hello@electronicae.com',
		),
		'stylenlook' => array(
			'access_mode' => 'full',
			'enabled_packs' => array('core', 'commerce', 'catalogue'),
			'system_name' => 'Stylenlook',
			'hub_name' => 'Electronic World Group',
			'tagline' => 'Fashion & apparel storefront',
			'trade_name' => 'Stylenlook',
			'from_email' => 'hello@stylenlook.com',
		),
		'thejewellerytrend' => array(
			'access_mode' => 'full',
			'enabled_packs' => array('core', 'commerce', 'catalogue'),
			'system_name' => 'The Jewellery Trend',
			'hub_name' => 'Electronic World Group',
			'tagline' => 'Fine jewellery collections and offers',
			'trade_name' => 'The Jewellery Trend',
			'from_email' => 'hello@thejewellerytrend.com',
		),
	);

	foreach ($tenants as $key => $tenantRow) {
		$industryCode = (string) $tenantRow['industry'];
		$themeTemplate = epc_portal_default_theme_template($industryCode);
		$base = $tenantSettings[$key];
		$hostsToSave = array_unique(array($tenantRow['www'], $tenantRow['bare']));
		foreach ($hostsToSave as $hostToSave) {
			$siteSettings = array(
				'host' => $hostToSave,
				'industry_code' => $industryCode,
				'access_mode' => $base['access_mode'],
				'enabled_packs' => $base['enabled_packs'],
				'theme_template' => $themeTemplate,
				'system_name' => $base['system_name'],
				'hub_name' => $base['hub_name'],
				'tagline' => $base['tagline'],
				'domain_path' => 'https://' . $hostToSave . '/',
				'contact' => epc_portal_default_contact(array(
					'trade_name' => $base['trade_name'],
					'hub_name' => $base['hub_name'],
					'from_email' => $base['from_email'],
				)),
				'theme' => epc_portal_style_template_theme($industryCode, $themeTemplate),
			);
			epc_portal_save_site_settings($tenantPdo, $siteSettings);
		}
		echo $key . " site_settings (theme=" . $themeTemplate . "): OK\n";
	}

	if (!empty($deprecatedHosts)) {
		$stDelSettings = $tenantPdo->prepare('DELETE FROM `site_settings` WHERE `host` = ?');
		$removedSettings = 0;
		foreach ($deprecatedHosts as $deprecatedHost) {
			$stDelSettings->execute(array($deprecatedHost));
			$removedSettings += (int) $stDelSettings->rowCount();
		}
		echo "Removed deprecated tenant site_settings rows: {$removedSettings}\n";
	}
} catch (Exception $e) {
	echo 'Tenant DB site_settings: FAIL — ' . $e->getMessage() . "\n";
}

if ($ecomaeDbPass !== '') {
	try {
		$pdo = new PDO(
			'mysql:host=127.0.0.1;dbname=ecomae;charset=utf8',
			'ecomae',
			$ecomaeDbPass,
			array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION)
		);
		epc_portal_db_ensure($pdo);
		foreach ($tenants as $key => $t) {
			$tpl = epc_portal_tenant_templates()[$key];
			$save = epc_portal_save_tenant($pdo, array(
				'site_key' => $t['site_key'],
				'hostname' => $t['www'],
				'industry_code' => $tpl['industry'],
				'status' => 'live',
				'trade_name' => $tpl['trade_name'],
				'hub_name' => $tpl['hub_name'],
				'from_email' => $tpl['from_email'],
				'db_name' => $dbName,
				'db_user' => $dbUser,
				'db_password' => $dbPass,
				'notes' => 'epc-tenants-connectivity-fix.php',
			));
			echo "Super CP {$key}: " . ($save['ok'] ? 'OK' : 'FAIL') . ' — ' . ($save['message'] ?? '') . "\n";
		}
		if (!empty($deprecatedHosts)) {
			$stDelTenant = $pdo->prepare('DELETE FROM `epc_portal_tenants` WHERE `hostname` = ?');
			$removedTenants = 0;
			foreach ($deprecatedHosts as $deprecatedHost) {
				$stDelTenant->execute(array($deprecatedHost));
				$removedTenants += (int) $stDelTenant->rowCount();
			}
			echo "Super CP deprecated host cleanup: removed {$removedTenants} row(s)\n";
		}
	} catch (Exception $e) {
		echo 'Super CP registry: FAIL — ' . $e->getMessage() . "\n";
	}
} else {
	echo "Super CP registry skipped — pass db_password= for ecomae MySQL\n";
}

epc_tcf_nginx_repair:
$cookie = '';
if (empty(epc_clp_web_login('admin', $clpPass, $cookie)['ok'])) {
	exit("\nCloudPanel login failed\n");
}
echo "\n=== 2) CloudPanel Model C nginx + SSL ===\n";
echo "CloudPanel login: OK\n";

if (!$nginxOnly) {
	foreach (array_values(array_unique(array_merge($allAliases, $deprecatedHosts))) as $orphan) {
		if ($orphan === $platformSite || $orphan === 'ecomae.com') {
			continue;
		}
		$del = epc_clp_web_delete_site($cookie, $orphan);
		echo 'Remove orphan CLP site ' . $orphan . ': ' . implode(' ', array_slice($del['log'], 0, 2)) . "\n";
	}
}

$vf = epc_clp_vhost_fetch($cookie, $platformSite);
if ($vf['vhost'] !== '' && $vf['token'] !== '') {
	$scrubbed = epc_clp_vhost_scrub_tenant_misroutes($vf['vhost'], $allAliases);
	$removedReject = 0;
	$scrubbed = epc_clp_vhost_strip_ssl_reject_for_hosts($scrubbed, $allAliases, $removedReject);
	$removed444 = 0;
	$removed3000 = 0;
	$scrubbed = epc_clp_vhost_strip_tenant_standalone_blocks($scrubbed, $allAliases, $removed444, $removed3000);
	echo "Tenant ssl_reject_handshake removed: {$removedReject}\n";
	echo "Tenant standalone server blocks removed (return 444): {$removed444}\n";
	echo "Tenant standalone server blocks removed (proxy :3000): {$removed3000}\n";
	if ($scrubbed !== $vf['vhost']) {
		epc_clp_vhost_save($cookie, $platformSite, $scrubbed, $vf['token']);
		echo "Scrubbed misroutes on {$platformSite} vhost\n";
	}
}

$tenantGroups = array();
foreach ($tenants as $key => $t) {
	$tenantGroups[] = array('key' => $key, 'hosts' => array($t['www'], $t['bare']));
}
$vh = epc_clp_vhost_configure_model_c_tenants($cookie, $platformSite, $tenantGroups);
foreach ($vh['log'] as $line) {
	echo '  vhost: ' . $line . "\n";
}

$vfAudit = epc_clp_vhost_fetch($cookie, $platformSite);
if ($vfAudit['vhost'] !== '') {
	echo "\n=== nginx server_name audit ({$platformSite}) ===\n";
	foreach (epc_clp_vhost_audit_server_names($vfAudit['vhost']) as $snLine) {
		echo '  ' . $snLine . "\n";
	}
}

$vf2 = epc_clp_vhost_fetch($cookie, $platformSite);
if ($vf2['vhost'] !== '' && $vf2['token'] !== '') {
	$patched = epc_clp_vhost_patch_tenant_direct_root($vf2['vhost'], $platformDocroot);
	if ($patched !== $vf2['vhost']) {
		epc_clp_vhost_save($cookie, $platformSite, $patched, $vf2['token']);
		echo "Patched tenant direct root → {$platformDocroot}\n";
		$vf2['vhost'] = $patched;
	}
	if (substr_count($vf2['vhost'], '{{root}}') > 0) {
		$rootLine = '  root ' . rtrim($platformDocroot, '/') . ';';
		$allRoots = (string) preg_replace_callback(
			'/# EPC_TENANT_DIRECT_START[\s\S]*?# EPC_TENANT_DIRECT_END/',
			function (array $m) use ($rootLine) {
				$block = (string) preg_replace('/\{\{root\}\}/', $rootLine, $m[0]);
				return (string) preg_replace('/^\s*root\s+[^;]+;/m', $rootLine, $block);
			},
			$vf2['vhost']
		);
		if ($allRoots !== $vf2['vhost']) {
			epc_clp_vhost_save($cookie, $platformSite, $allRoots, $vf2['token']);
			echo "Patched all tenant {{root}} placeholders\n";
		}
	}
}

$perm = epc_clp_run('system:permissions:reset --directories=755 --files=644 --path=' . escapeshellarg($platformDocroot));
echo 'permissions reset code=' . $perm['code'] . "\n";
@exec('chmod -R o+rX ' . escapeshellarg($platformDocroot) . ' 2>&1');

echo "\n=== Quarantine orphan per-tenant nginx files ===\n";
$quarantineHosts = array_values(array_unique(array_merge($allAliases, $deprecatedHosts)));
$quarantine = epc_clp_nginx_quarantine_orphan_configs($quarantineHosts, $platformSite);
foreach ($quarantine['log'] as $line) {
	echo '  ' . $line . "\n";
}

if (!$nginxOnly) {
	$tenantSslRows = array();
	foreach ($tenants as $t) {
		$tenantSslRows[] = array('www' => $t['www'], 'bare' => $t['bare']);
	}
	$perTenantSsl = epc_clp_vhost_install_per_tenant_ssl($cookie, $platformSite, $platformDocroot, $tenantSslRows);
	foreach ($perTenantSsl['log'] as $line) {
		echo 'SSL: ' . $line . "\n";
	}

	$purgeHosts = array();
	foreach ($tenants as $t) {
		$purgeHosts[] = 'https://' . $t['www'] . '/';
	}
	$purge = epc_clp_run("varnish-cache:purge --purge='" . implode(',', $purgeHosts) . "'");
	echo 'Varnish purge code=' . $purge['code'] . "\n";
} else {
	echo "SSL + varnish skipped (nginx_only=1)\n";
}

echo "\n=== nginx reload ===\n";
$reload = epc_clp_nginx_reload_with_pass($clpPass);
foreach ($reload['log'] as $line) {
	echo $line . "\n";
}
if (!$reload['ok']) {
	foreach (epc_clp_nginx_reload()['log'] as $line) {
		echo 'fallback: ' . $line . "\n";
	}
}

echo "\n=== AFTER (origin + public) ===\n";
$results = array();
foreach ($tenants as $key => $t) {
	echo "[{$key}]\n";
	$oRoot = epc_tcf_probe('http://127.0.0.1/', $t['www']);
	$oCp = epc_tcf_probe('http://127.0.0.1/cp/', $t['www']);
	$pRoot = epc_tcf_probe('https://' . $t['www'] . '/');
	$pCp = epc_tcf_probe('https://' . $t['www'] . '/cp/');
	echo '  origin /: ' . $oRoot['label'] . ' ' . $oRoot['ms'] . "ms\n";
	echo '  origin /cp/: ' . $oCp['label'] . ' ' . $oCp['ms'] . "ms\n";
	echo '  public /: ' . $pRoot['label'] . ' ' . $pRoot['ms'] . "ms\n";
	echo '  public /cp/: ' . $pCp['label'] . ' ' . $pCp['ms'] . "ms\n";
	$results[$key] = array('origin_root' => $oRoot, 'origin_cp' => $oCp, 'public_root' => $pRoot, 'public_cp' => $pCp);
}

echo "\n=== GoDaddy DNS (direct — NO Cloudflare on tenants) ===\n";
foreach ($tenants as $key => $t) {
	echo "{$key}: A @ → {$platformIp}, A www → {$platformIp} (TTL 600)\n";
}
echo "ecomae.com may stay on Cloudflare; tenant domains must NOT be proxied.\n";
$cfMismatchHosts = array();
foreach ($tenants as $key => $t) {
	foreach (array($t['bare'], $t['www']) as $host) {
		$ip = gethostbyname($host);
		if ($ip !== $host && $ip !== $platformIp) {
			$cfMismatchHosts[$key] = true;
			break;
		}
	}
}
if (!empty($cfMismatchHosts)) {
	echo 'Cloudflare/proxy mismatch detected for: ' . implode(', ', array_keys($cfMismatchHosts)) . "\n";
	echo "Set both @ and www A records to {$platformIp}, DNS-only (no orange cloud / no 104.x / 172.x).\n\n";
} else {
	echo "All tenant DNS currently points direct to origin {$platformIp}.\n\n";
}

$originOk = true;
$publicOk = true;
foreach ($results as $key => $r) {
	if (!$r['origin_root']['ok'] || !$r['origin_cp']['ok']) {
		$originOk = false;
	}
	if (!$r['public_root']['ok'] || !$r['public_cp']['ok']) {
		$publicOk = false;
	}
}

echo "=== Summary ===\n";
echo 'origin probes (127.0.0.1): ' . ($originOk ? 'PASS' : 'FAIL — check nginx server_name aliases') . "\n";
echo 'public probes (from server): ' . ($publicOk ? 'PASS' : 'FAIL') . "\n";
if (!$publicOk && $originOk) {
	echo "BLOCKER: Origin OK but public HTTPS fails — Hostinger VPS firewall.\n";
	echo "hPanel → VPS → Security → Firewall → allow TCP 80 and 443 from Anywhere → Sync.\n";
	echo "Or: epc-hostinger-firewall-open-web.php?token=" . epc_deploy_token() . "&apply=1&hostinger_token=...\n";
}
echo "\nVerify externally:\n";
foreach ($checkUrls as $label => $url) {
	echo "  curl -skI {$url}\n";
}
echo "\nUnified fix complete.\n";
