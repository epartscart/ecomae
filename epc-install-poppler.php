<?php
/**
 * Install poppler-utils (pdftotext) on the platform VPS.
 * https://www.ecomae.com/epc-install-poppler.php?token=epartscart-deploy-2026
 * Also callable via platform-fix: run_action=install_poppler
 */
declare(strict_types=1);

require_once __DIR__ . '/epc_deploy_auth.php';
epc_deploy_require_token();
header('Content-Type: text/plain; charset=utf-8');
set_time_limit(300);

require_once __DIR__ . '/content/general_pages/epc_cloudpanel_helpers.php';

function epc_poppler_pdftotext_path(): string
{
	foreach (array(
		'command -v pdftotext 2>/dev/null',
		'which pdftotext 2>/dev/null',
		'type -p pdftotext 2>/dev/null',
	) as $cmd) {
		$r = trim((string) @shell_exec($cmd));
		if ($r !== '' && stripos($r, 'not found') === false && stripos($r, 'no ') !== 0) {
			return $r;
		}
	}
	foreach (array('/usr/bin/pdftotext', '/usr/local/bin/pdftotext') as $p) {
		if (is_executable($p)) {
			return $p;
		}
	}
	return '';
}

$path = epc_poppler_pdftotext_path();
echo "pdftotext before: " . ($path !== '' ? $path : '(missing)') . "\n";
if ($path !== '') {
	echo trim((string) @shell_exec(escapeshellarg($path) . ' -v 2>&1')) . "\n";
	echo "Already installed — nothing to do.\n";
	exit;
}

$installCmds = array(
	'runuser -u root -- env DEBIAN_FRONTEND=noninteractive apt-get update -qq 2>&1 && runuser -u root -- env DEBIAN_FRONTEND=noninteractive apt-get install -y poppler-utils 2>&1',
	'DEBIAN_FRONTEND=noninteractive apt-get update -qq 2>&1 && DEBIAN_FRONTEND=noninteractive apt-get install -y poppler-utils 2>&1',
	'sudo -n DEBIAN_FRONTEND=noninteractive apt-get update -qq 2>&1 && sudo -n DEBIAN_FRONTEND=noninteractive apt-get install -y poppler-utils 2>&1',
	'sudo DEBIAN_FRONTEND=noninteractive apt-get update -qq 2>&1 && sudo DEBIAN_FRONTEND=noninteractive apt-get install -y poppler-utils 2>&1',
);
$installed = false;
foreach ($installCmds as $cmd) {
	echo "\n--- trying: {$cmd}\n";
	$r = epc_clp_run_cmd($cmd);
	echo "exit={$r['code']}\n{$r['output']}\n";
	if ($r['code'] === 0) {
		$installed = true;
		break;
	}
}

$path = epc_poppler_pdftotext_path();
echo "\npdftotext after: " . ($path !== '' ? $path : '(still missing)') . "\n";
if ($path !== '') {
	echo trim((string) @shell_exec(escapeshellarg($path) . ' -v 2>&1')) . "\n";
	echo $installed ? "Install OK.\n" : "Found without apt (pre-existing?).\n";
	exit;
}

echo "Install failed — PHP stream fallback will be used.\n";
