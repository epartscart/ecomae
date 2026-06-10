<?php
/**
 * Auto Price AI — image import demo probe (deploy token).
 * GET /epc-auto-price-image-demo.php?token=…&site_key=electronicae&approve=1
 */
declare(strict_types=1);

require_once __DIR__ . '/epc_deploy_auth.php';
epc_deploy_require_token();
header('Content-Type: application/json; charset=utf-8');

define('_ASTEXE_', 1);
require_once __DIR__ . '/config.php';
require_once __DIR__ . '/content/general_pages/epc_portal.php';
require_once __DIR__ . '/content/shop/price_engine/epc_auto_price_engine.php';
require_once __DIR__ . '/content/shop/price_engine/epc_discovery_adapters.php';
require_once __DIR__ . '/content/shop/price_engine/epc_auto_price_images.php';

$cfg = new DP_Config();
epc_portal_apply_config($cfg);
global $DP_Config;
$DP_Config = $cfg;

$siteKey = preg_replace('/[^a-z0-9_]/', '', strtolower(trim((string) ($_GET['site_key'] ?? 'electronicae'))));
$doApprove = !empty($_GET['approve']);
$doRefresh = !empty($_GET['refresh']);

try {
	$pdo = new PDO(
		'mysql:host=' . $cfg->host . ';dbname=' . $cfg->db . ';charset=utf8',
		(string) $cfg->user,
		(string) $cfg->password,
		array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION)
	);
	epc_ape_ensure_schema($pdo);

	$out = array('ok' => true, 'site_key' => $siteKey, 'actions' => array());

	if (!empty($_GET['list'])) {
		$stmt = $pdo->prepare('SELECT `id`, `title`, `status`, `image_urls`, `local_image_paths`, `product_id` FROM `epc_product_discovery_queue` WHERE `site_key` = ? ORDER BY `id` DESC LIMIT 20');
		$stmt->execute(array($siteKey));
		$out['queue'] = $stmt->fetchAll(PDO::FETCH_ASSOC) ?: array();
	}

	if (!empty($_GET['patch_demo_images'])) {
		$demoImg = 'https://m.media-amazon.com/images/I/714Rq4k05UL._AC_SL1000_.jpg';
		$patch = $pdo->prepare(
			'UPDATE `epc_product_discovery_queue` SET `image_urls` = ?, `updated_at` = ?
			 WHERE `site_key` = ? AND `status` = \'suggested\' AND (`image_urls` IS NULL OR `image_urls` = \'\' OR `image_urls` = \'[]\')'
		);
		$patch->execute(array(json_encode(array($demoImg)), time(), $siteKey));
		$out['actions']['patch_demo_images'] = (int) $patch->rowCount();
	}

	if ($doApprove) {
		$qid = (int) ($_GET['queue_id'] ?? 0);
		if ($qid <= 0) {
			$stmt = $pdo->prepare('SELECT `id` FROM `epc_product_discovery_queue` WHERE `site_key` = ? AND `status` = \'suggested\' ORDER BY `id` DESC LIMIT 1');
			$stmt->execute(array($siteKey));
			$qid = (int) $stmt->fetchColumn();
		}
		if ($qid <= 0) {
			epc_disc_run_for_taxonomy($pdo, $siteKey, 'cell-phones', '');
			$stmt = $pdo->prepare('SELECT `id` FROM `epc_product_discovery_queue` WHERE `site_key` = ? AND `status` = \'suggested\' ORDER BY `id` DESC LIMIT 1');
			$stmt->execute(array($siteKey));
			$qid = (int) $stmt->fetchColumn();
		}
		if ($qid > 0) {
			$res = epc_disc_queue_approve_import($pdo, $siteKey, $qid);
			$out['actions']['approve'] = $res;
			$out['queue_id'] = $qid;
		} else {
			$out['actions']['approve'] = array('ok' => false, 'message' => 'No suggested queue item');
		}
	}

	if ($doRefresh) {
		$qid = (int) ($_GET['queue_id'] ?? 0);
		if ($qid <= 0) {
			$stmt = $pdo->prepare('SELECT `id` FROM `epc_product_discovery_queue` WHERE `site_key` = ? AND `status` = \'imported\' ORDER BY `id` DESC LIMIT 1');
			$stmt->execute(array($siteKey));
			$qid = (int) $stmt->fetchColumn();
		}
		if ($qid > 0) {
			$out['actions']['refresh'] = epc_disc_queue_refresh_images($pdo, $siteKey, $qid);
		}
	}

	$imported = $pdo->prepare('SELECT * FROM `epc_product_discovery_queue` WHERE `site_key` = ? AND `status` = \'imported\' ORDER BY `id` DESC LIMIT 1');
	if (!empty($_GET['product_id'])) {
		$imported = $pdo->prepare('SELECT * FROM `epc_product_discovery_queue` WHERE `site_key` = ? AND `status` = \'imported\' AND `product_id` = ? LIMIT 1');
		$imported->execute(array($siteKey, (int) $_GET['product_id']));
	} else {
		$imported->execute(array($siteKey));
	}
	$row = $imported->fetch(PDO::FETCH_ASSOC) ?: null;
	$verify = array();
	if ($row) {
		$productId = (int) ($row['product_id'] ?? 0);
		$localPaths = json_decode((string) ($row['local_image_paths'] ?? ''), true);
		if (!is_array($localPaths)) {
			$localPaths = array();
		}
		$docRoot = rtrim((string) ($_SERVER['DOCUMENT_ROOT'] ?? ''), '/\\');
		foreach ($localPaths as $webPath) {
			$fs = $docRoot . (string) $webPath;
			$verify[] = array(
				'web' => $webPath,
				'exists' => is_file($fs),
				'bytes' => is_file($fs) ? filesize($fs) : 0,
			);
		}
		$imgStmt = $pdo->prepare('SELECT `file_name` FROM `shop_products_images` WHERE `product_id` = ?');
		$imgStmt->execute(array($productId));
		$dbImages = $imgStmt->fetchAll(PDO::FETCH_COLUMN) ?: array();
		$out['latest_import'] = array(
			'queue_id' => (int) $row['id'],
			'product_id' => $productId,
			'title' => (string) ($row['title'] ?? ''),
			'local_image_paths' => $localPaths,
			'storefront_url' => $productId > 0 ? epc_ape_catalogue_product_url($pdo, $productId) : '',
			'shop_products_images' => $dbImages,
			'file_verify' => $verify,
		);
	}

	echo json_encode($out, JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES | JSON_PRETTY_PRINT);
} catch (Throwable $e) {
	http_response_code(500);
	echo json_encode(array('ok' => false, 'error' => $e->getMessage()));
}
