<?php
/**
 * Sync shared storefront static assets missing from ecomae platform docroot.
 * Read-only fetch from www.epartscart.com (production reference).
 *
 * GET: token=epartscart-deploy-2026&apply=1
 * Optional: images=62,63,64,74,76,78
 */
declare(strict_types=1);

require_once __DIR__ . '/epc_deploy_auth.php';
header('Content-Type: application/json; charset=utf-8');

$token = (string) ($_GET['token'] ?? $_POST['token'] ?? '');
if ($token === '' || !hash_equals(epc_deploy_token(), $token)) {
	http_response_code(403);
	exit(json_encode(array('ok' => false, 'message' => 'Forbidden')));
}

$apply = !empty($_GET['apply']) || !empty($_POST['apply']);
$imageList = array(62, 63, 64, 74, 76, 78);
if (!empty($_GET['images']) || !empty($_POST['images'])) {
	$raw = (string) ($_GET['images'] ?? $_POST['images'] ?? '');
	$imageList = array();
	foreach (preg_split('/[^0-9]+/', $raw) as $n) {
		if ($n !== '') {
			$imageList[] = (int) $n;
		}
	}
}

$refBase = 'https://www.epartscart.com';
$docRoot = rtrim((string) ($_SERVER['DOCUMENT_ROOT'] ?? ''), '/\\');
$targets = array(
	'catalogue_images' => array(
		'dir' => $docRoot . '/content/files/images/catalogue_images',
		'files' => array(),
	),
	'vin_zapros' => array(
		'dir' => $docRoot . '/content/general_pages/vin_zapros',
		'files' => array('email.png'),
	),
);

foreach ($imageList as $id) {
	$targets['catalogue_images']['files'][] = $id . '.png';
}

$result = array(
	'ok' => true,
	'apply' => $apply,
	'synced' => array(),
	'skipped' => array(),
	'errors' => array(),
);

foreach ($targets as $group => $cfg) {
	$dir = (string) $cfg['dir'];
	if ($apply && !is_dir($dir)) {
		if (!@mkdir($dir, 0755, true) && !is_dir($dir)) {
			$result['errors'][] = array('group' => $group, 'message' => 'mkdir failed: ' . $dir);
			$result['ok'] = false;
			continue;
		}
	}
	foreach ((array) $cfg['files'] as $file) {
		$file = basename((string) $file);
		$url = $group === 'catalogue_images'
			? $refBase . '/content/files/images/catalogue_images/' . rawurlencode($file)
			: $refBase . '/content/general_pages/vin_zapros/' . rawurlencode($file);
		$dest = $dir . '/' . $file;
		$entry = array('group' => $group, 'file' => $file, 'url' => $url, 'dest' => $dest);
		if (is_file($dest) && filesize($dest) > 100) {
			$entry['status'] = 'exists';
			$result['skipped'][] = $entry;
			continue;
		}
		if (!$apply) {
			$entry['status'] = 'would_fetch';
			$result['synced'][] = $entry;
			continue;
		}
		$ctx = stream_context_create(array(
			'http' => array(
				'timeout' => 30,
				'user_agent' => 'EPC-Demo-Parity-Sync/1.0',
			),
			'ssl' => array('verify_peer' => true, 'verify_peer_name' => true),
		));
		$bin = @file_get_contents($url, false, $ctx);
		if ($bin === false || strlen($bin) < 50) {
			$entry['status'] = 'fetch_failed';
			$result['errors'][] = $entry;
			$result['ok'] = false;
			continue;
		}
		if (@file_put_contents($dest, $bin) === false) {
			$entry['status'] = 'write_failed';
			$result['errors'][] = $entry;
			$result['ok'] = false;
			continue;
		}
		$entry['status'] = 'written';
		$entry['bytes'] = strlen($bin);
		$result['synced'][] = $entry;
	}
}

$result['message'] = $apply
	? ('Asset sync complete — ' . count($result['synced']) . ' written, ' . count($result['skipped']) . ' skipped')
	: 'Dry run — pass apply=1';
echo json_encode($result, JSON_PRETTY_PRINT | JSON_UNESCAPED_UNICODE);
