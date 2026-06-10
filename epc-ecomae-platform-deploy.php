<?php
/**
 * Push platform portal files to www + cp ecomae via CloudPanel file manager.
 * https://www.epartscart.com/epc-ecomae-platform-deploy.php?token=...&clp_pass=...
 */
declare(strict_types=1);

require_once __DIR__ . '/epc_deploy_auth.php';
epc_deploy_require_token();
header('Content-Type: text/plain; charset=utf-8');
set_time_limit(0);

require_once __DIR__ . '/content/general_pages/epc_cloudpanel_helpers.php';

$clpPass = trim((string) ($_GET['clp_pass'] ?? ''));
if ($clpPass === '') {
	exit("clp_pass required\n");
}

$cookie = '';
if (empty(epc_clp_web_login('admin', $clpPass, $cookie)['ok'])) {
	exit("CloudPanel login failed\n");
}
echo "CloudPanel login OK\n";

function ecomae_deploy_put(string &$cookie, string $dir, string $rel, string $content): void
{
	$panel = epc_clp_panel_url();
	$parts = explode('/', trim($rel, '/'));
	$name = array_pop($parts);
	$parent = $dir;
	foreach ($parts as $part) {
		epc_clp_web_request($panel . '/file-manager/backend/mkdir', array(
			'method' => 'POST',
			'body' => http_build_query(array('id' => $parent, 'name' => $part)),
		), $cookie);
		$parent .= '/' . $part;
	}
	epc_clp_web_request($panel . '/file-manager/backend/makefile', array(
		'method' => 'POST',
		'body' => http_build_query(array('id' => $parent, 'name' => $name)),
	), $cookie);
	epc_clp_web_request($panel . '/file-manager/backend/text', array(
		'method' => 'POST',
		'body' => http_build_query(array('id' => $parent . '/' . $name, 'content' => $content)),
	), $cookie);
}

$pushRel = str_replace('\\', '/', trim((string) ($_POST['push_rel'] ?? $_GET['push_rel'] ?? '')));
$pushB64 = (string) ($_POST['push_b64'] ?? $_GET['push_b64'] ?? '');
if ($pushRel !== '' && $pushB64 !== '' && strpos($pushRel, '..') === false && $pushRel[0] !== '/') {
	$bin = base64_decode($pushB64, true);
	if ($bin === false) {
		exit("Bad push_b64\n");
	}
	foreach (array('www.ecomae.com', 'cp.ecomae.com') as $site) {
		epc_clp_web_request(epc_clp_panel_url() . '/site/' . rawurlencode($site) . '/file-manager', array(), $cookie);
		ecomae_deploy_put($cookie, '/htdocs/' . $site, $pushRel, $bin);
		echo "Pushed {$pushRel} to {$site}\n";
	}
	if (empty($_GET['also_deploy']) && empty($_POST['also_deploy'])) {
		exit("Push done.\n");
	}
}

if (!empty($_GET['fix_epartscart'])) {
	require_once __DIR__ . '/content/general_pages/epc_portal.php';
	require_once __DIR__ . '/content/general_pages/epc_portal_db.php';
	require_once __DIR__ . '/content/general_pages/epc_portal_tenant.php';

	$hostname = 'www.epartscart.com';
	$platformSite = 'www.ecomae.com';
	$docroot = '/home/ecomae/htdocs/www.ecomae.com';

	echo "=== Fix epartscart 403 ===\n";

	$docCfg = function_exists('epc_portal_docpart_config') ? epc_portal_docpart_config() : new DP_Config();
	$ecomaeDbPass = trim((string) ($_GET['db_password'] ?? ''));
	if ($ecomaeDbPass === '' && is_file(__DIR__ . '/config.local.php')) {
		$epc_config_local = null;
		require __DIR__ . '/config.local.php';
		if (isset($epc_config_local['password'])) {
			$ecomaeDbPass = (string) $epc_config_local['password'];
		}
	}
	try {
		$pdo = new PDO(
			'mysql:host=127.0.0.1;dbname=ecomae;charset=utf8',
			'ecomae',
			$ecomaeDbPass,
			array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION)
		);
		epc_portal_db_ensure($pdo);
		$platformHosts = array('www.ecomae.com', 'ecomae.com', 'cp.ecomae.com');
		foreach ($platformHosts as $ph) {
			epc_portal_save_site_settings($pdo, epc_portal_default_site_settings($ph));
		}
		echo "Reset ecomae platform site settings (marketing, not auto parts)\n";
		$save = epc_portal_save_tenant($pdo, array(
			'site_key' => 'epartscart',
			'hostname' => $hostname,
			'industry_code' => 'auto_parts',
			'status' => 'live',
			'trade_name' => 'eParts Cart',
			'hub_name' => 'Electronic World Group',
			'from_email' => 'partsdoc2025@gmail.com',
			'db_name' => $docCfg->db,
			'db_user' => $docCfg->user,
			'db_password' => $docCfg->password,
			'notes' => 'Fixed via platform-deploy fix_epartscart',
		));
		echo 'Tenant: ' . ($save['message'] ?? '') . "\n";
	} catch (Exception $e) {
		echo 'Tenant DB: ' . $e->getMessage() . "\n";
	}

	$panel = epc_clp_panel_url();
	$vhHtml = epc_clp_web_request($panel . '/site/' . rawurlencode($platformSite) . '/vhost', array(), $cookie);
	$vhToken = '';
	$vhost = '';
	if (preg_match('/name="token" value="([^"]+)"/', $vhHtml, $vt)) {
		$vhToken = $vt[1];
	}
	if (preg_match('/<textarea[^>]*name="vhost-template"[^>]*>([\s\S]*?)<\/textarea>/', $vhHtml, $vm)) {
		$vhost = html_entity_decode($vm[1], ENT_QUOTES | ENT_HTML5, 'UTF-8');
	}
	$bare = preg_replace('/^www\./', '', $hostname);
	foreach (array_unique(array($hostname, $bare)) as $aliasHost) {
		if ($aliasHost === '' || $vhost === '' || stripos($vhost, $aliasHost) !== false) {
			continue;
		}
		if (preg_match('/^\s*server_name\s+(.+);/m', $vhost, $sm)) {
			$vhost = preg_replace('/^\s*server_name\s+.+;/m', '  server_name ' . trim($sm[1]) . ' ' . $aliasHost . ';', $vhost, 1);
			echo "Added vhost alias {$aliasHost}\n";
		}
	}
	if ($vhost !== '' && $vhToken !== '' && stripos($vhHtml, $hostname) === false) {
		epc_clp_web_request($panel . '/site/' . rawurlencode($platformSite) . '/vhost', array(
			'method' => 'POST',
			'body' => http_build_query(array(
				'vhost-update' => '1',
				'vhost-template' => $vhost,
				'token' => $vhToken,
			)),
		), $cookie);
		echo "Saved ecomae vhost\n";
	}

	$del = epc_clp_web_delete_site($cookie, $hostname);
	echo 'Delete standalone site: ' . implode(' ', $del['log']) . "\n";

	exec('chmod -R o+rX ' . escapeshellarg($docroot) . ' 2>&1', $chmodOut, $chmodCode);
	echo "chmod code={$chmodCode}\n";

	epc_clp_web_install_ssl($cookie, $platformSite);

	foreach (array(
		'https://www.epartscart.com/',
		'https://www.epartscart.com/cp/',
	) as $url) {
		$body = @file_get_contents($url, false, stream_context_create(array(
			'http' => array('timeout' => 25, 'ignore_errors' => true),
			'ssl' => array('verify_peer' => false, 'verify_peer_name' => false),
		)));
		$code = 0;
		if (isset($http_response_header[0]) && preg_match('/\s(\d{3})\s/', $http_response_header[0], $m)) {
			$code = (int) $m[1];
		}
		$snippet = $body !== false ? substr(preg_replace('/\s+/', ' ', strip_tags((string) $body)), 0, 100) : '';
		echo "Probe {$url} HTTP {$code} — {$snippet}\n";
	}
	if (empty($_GET['also_deploy'])) {
		exit("\nfix_epartscart done.\n");
	}
}

$files = array(
	'index.php',
	'cp/index.php',
	'chunk-receiver.php',
	'content/general_pages/epc_portal.php',
	'content/general_pages/epc_portal_db.php',
	'content/general_pages/epc_portal_theme_templates.php',
	'content/general_pages/epc_portal_tenant.php',
	'content/general_pages/epc_portal_tenant_intro.php',
	'content/general_pages/epc_ecomae_platform_home.php',
	'content/general_pages/epc_ecomae_platform_data.php',
	'content/general_pages/epc_ecomae_platform_router.php',
	'content/general_pages/epc_ecomae_platform_pages.php',
	'templates/nero/desktop.php',
	'content/shop/tenant_hub/epc_tenant_hub_helpers.php',
	'content/shop/tenant_hub/epc_tenant_onboard_panel.php',
	'cp/content/shop/tenant_hub/tenant_hub_main.php',
	'cp/content/shop/tenant_hub/tenant_hub_hub_page.php',
	'cp/content/shop/tenant_hub/tenant_hub_main_page.php',
	'core/dp_core.php',
	'epc-ecomae-setup.php',
	'epc-ecomae-platform-check.php',
	'epc-ecomae-platform-fix.php',
	'epc-ecomae-patch-dp-core.php',
	'epc-ecomae-register-tenant.php',
	'ecomae-register-tenant.php',
	'ecomae-tenant-vhost-fix.php',
	'ecomae-fix-epartscart.php',
	'ecomae-fix-marketing-routing.php',
	'content/general_pages/epart_catalog_front_links.php',
	'epc-ecomae-clp-probe-delete.php',
	'epc-ecomae-clp-probe-vhost.php',
	'ecomae-vhost-alias.php',
	'ecomae-client-platform-fix.php',
	'ecomae-super-cp-setup.php',
	'ecomae-fix-cp-delegate.php',
	'ecomae-fix-cp-empty.php',
	'ecomae-super-cp-file-bundle.php',
);

$restoreIndex = !empty($_GET['restore_index']);
$tarIndex = '/tmp/ecomae-full-site.tar.gz';

function ecomae_tar_extract(string $tar, string $member): string
{
	$tmp = '/tmp/ecomae-idx-' . md5($member);
	@unlink($tmp);
	exec('tar -xOzf ' . escapeshellarg($tar) . ' ' . escapeshellarg($member) . ' > ' . escapeshellarg($tmp) . ' 2>&1', $o, $c);
	if ($c === 0 && is_file($tmp) && filesize($tmp) > 100) {
		return (string) file_get_contents($tmp);
	}
	return '';
}

if ($restoreIndex && is_file($tarIndex)) {
	foreach (array(
		'www.ecomae.com' => array('index.php' => 'www.epartscart.com/index.php'),
		'cp.ecomae.com' => array(
			'index.php' => 'www.epartscart.com/index.php',
			'cp/index.php' => 'www.epartscart.com/cp/index.php',
		),
	) as $site => $map) {
		epc_clp_web_request(epc_clp_panel_url() . '/site/' . rawurlencode($site) . '/file-manager', array(), $cookie);
		foreach ($map as $rel => $tarMember) {
			$body = ecomae_tar_extract($tarIndex, $tarMember);
			if ($body === '') {
				echo "skip restore {$rel} on {$site} (tar extract failed)\n";
				continue;
			}
			ecomae_deploy_put($cookie, '/htdocs/' . $site, $rel, $body);
			echo "restored {$rel} on {$site}\n";
		}
	}
}

foreach (array('www.ecomae.com', 'cp.ecomae.com') as $site) {
	epc_clp_web_request(epc_clp_panel_url() . '/site/' . rawurlencode($site) . '/file-manager', array(), $cookie);
	$n = 0;
	foreach ($files as $rel) {
		$src = __DIR__ . '/' . $rel;
		if (!is_file($src)) {
			echo "skip missing {$rel}\n";
			continue;
		}
		ecomae_deploy_put($cookie, '/htdocs/' . $site, $rel, file_get_contents($src));
		$n++;
	}
	echo "Deployed {$n} files to {$site}\n";
}

$token = epc_deploy_token();
$setup = @file_get_contents('https://www.ecomae.com/epc-ecomae-setup.php?token=' . urlencode($token), false, stream_context_create(array(
	'http' => array('timeout' => 180),
	'ssl' => array('verify_peer' => false, 'verify_peer_name' => false),
)));
echo "\nsetup:\n" . substr((string) $setup, 0, 2000) . "\n";

$check = @file_get_contents('https://www.ecomae.com/epc-ecomae-platform-check.php?token=' . urlencode($token), false, stream_context_create(array(
	'http' => array('timeout' => 60),
	'ssl' => array('verify_peer' => false, 'verify_peer_name' => false),
)));
echo "\ncheck:\n" . substr((string) $check, 0, 4000) . "\n";
