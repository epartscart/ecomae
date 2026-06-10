<?php
/**
 * UAE VAT compliance probe — guest B2C inclusive, customer types, e-invoice line math.
 * GET ?token=epartscart-deploy-2026&site_key=epartscart
 */
declare(strict_types=1);

require_once __DIR__ . '/epc_deploy_auth.php';
epc_deploy_require_token();
header('Content-Type: application/json; charset=utf-8');

define('_ASTEXE_', 1);

try {
	require_once __DIR__ . '/config.php';
	$DP_Config = new DP_Config;

	$siteKey = preg_replace('/[^a-z0-9_]/', '', strtolower((string)($_GET['site_key'] ?? '')));
	$db = null;
	$tenantLabel = 'local';

	if ($siteKey !== '') {
		require_once __DIR__ . '/content/general_pages/epc_portal_shared_erp.php';
		$platDb = '';
		$platUser = '';
		$platPass = '';
		$cfgFile = __DIR__ . '/config.local.php';
		if (!is_file($cfgFile)) {
			$cfgFile = '/home/ecomae/htdocs/www.ecomae.com/config.local.php';
		}
		if (is_file($cfgFile)) {
			$epc_config_local = null;
			include $cfgFile;
			$platDb = (string)($epc_config_local['db'] ?? '');
			$platUser = (string)($epc_config_local['user'] ?? '');
			$platPass = (string)($epc_config_local['password'] ?? '');
		}
		if ($platDb === '' || $platUser === '') {
			$platCfg = new DP_Config();
			$platDb = (string)$platCfg->db;
			$platUser = (string)$platCfg->user;
			$platPass = (string)$platCfg->password;
		}
		$platPdo = new PDO(
			'mysql:host=127.0.0.1;dbname=' . $platDb . ';charset=utf8',
			$platUser,
			$platPass,
			array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION)
		);
		$row = epc_portal_shared_erp_load_by_site_key($siteKey, $platPdo);
		if (!$row) {
			echo json_encode(array('ok' => false, 'error' => 'tenant_not_found', 'site_key' => $siteKey));
			exit;
		}
		$db = epc_portal_shared_erp_tenant_pdo($row);
		$tenantLabel = $siteKey;
	} else {
		$db = new PDO(
			'mysql:host=' . $DP_Config->host . ';dbname=' . $DP_Config->db,
			$DP_Config->user,
			$DP_Config->password,
			array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION)
		);
	}

	$db->query('SET NAMES utf8;');

	require_once __DIR__ . '/content/shop/finance/epc_uae_customer_vat.php';
	require_once __DIR__ . '/content/shop/finance/epc_uae_vat.php';

	$host = (string)($_GET['host'] ?? ($_SERVER['HTTP_HOST'] ?? ''));
	$sampleEx = 100.0;

	$guest = epc_uae_customer_vat_resolve($db, 0);
	$guestApplied = epc_uae_customer_vat_apply_display_price($db, $sampleEx, 0);

	$checks = array(
		array(
			'id' => 'guest_inclusive_display',
			'pass' => ($guest['vat_type'] === 'local_b2c' && $guest['display_mode'] === 'inclusive'),
			'detail' => array('vat_type' => $guest['vat_type'], 'display_mode' => $guest['display_mode'], 'label' => $guest['price_label']),
		),
		array(
			'id' => 'guest_price_includes_vat',
			'pass' => (abs($guestApplied['display_price'] - 105.0) < 0.02 && $guestApplied['price_label'] === 'incl. VAT'),
			'detail' => $guestApplied,
		),
	);

	$sampleUsers = array();
	$st = $db->query(
		"SELECT u.`user_id`,
		MAX(CASE WHEN up.`data_key` = 'epc_customer_type' THEN up.`data_value` END) AS customer_type,
		MAX(CASE WHEN up.`data_key` = 'epc_reg_country' THEN up.`data_value` END) AS country,
		MAX(CASE WHEN up.`data_key` = 'customer_vat_type' THEN up.`data_value` END) AS stored_vat_type,
		b.`trn`
		FROM `users` u
		LEFT JOIN `users_profiles` up ON up.`user_id` = u.`user_id`
		LEFT JOIN `epc_einvoice_buyer_profiles` b ON b.`user_id` = u.`user_id`
		WHERE u.`user_id` > 0
		GROUP BY u.`user_id`
		ORDER BY u.`user_id` DESC
		LIMIT 8"
	);
	while ($row = $st->fetch(PDO::FETCH_ASSOC)) {
		$uid = (int)$row['user_id'];
		$resolved = epc_uae_customer_vat_resolve($db, $uid);
		$applied = epc_uae_customer_vat_apply_display_price($db, $sampleEx, $uid);
		$line = epc_uae_customer_vat_order_line($db, $uid, $applied['display_price'], 1, array());
		$sampleUsers[] = array(
			'user_id' => $uid,
			'customer_type' => $row['customer_type'],
			'country' => $row['country'],
			'trn' => $row['trn'],
			'stored_vat_type' => $row['stored_vat_type'],
			'resolved_vat_type' => $resolved['vat_type'],
			'display_mode' => $resolved['display_mode'],
			'price_label' => $resolved['price_label'],
			'sample_display_100ex' => $applied,
			'order_line_from_display' => $line,
		);
	}

	$einvoiceSample = null;
	$orderSt = $db->query(
		'SELECT o.`id`, o.`user_id` FROM `shop_orders` o
		WHERE o.`successfully_created` = 1
		ORDER BY o.`id` DESC LIMIT 1'
	);
	if ($ord = $orderSt->fetch(PDO::FETCH_ASSOC)) {
		require_once __DIR__ . '/content/shop/finance/epc_einvoice.php';
		$built = epc_einvoice_build_from_order($db, (int)$ord['id'], array());
		$einvoiceSample = array(
			'order_id' => (int)$ord['id'],
			'user_id' => (int)$ord['user_id'],
			'subtotal_ex_vat' => $built['subtotal_ex_vat'] ?? null,
			'total_vat' => $built['total_vat'] ?? null,
			'total_incl_vat' => $built['total_incl_vat'] ?? null,
			'line_count' => isset($built['lines']) ? count($built['lines']) : 0,
			'first_line' => isset($built['lines'][0]) ? $built['lines'][0] : null,
		);
		$checks[] = array(
			'id' => 'einvoice_latest_order',
			'pass' => !empty($built['lines']) && isset($built['total_vat']),
			'detail' => array('order_id' => (int)$ord['id'], 'total_vat' => $built['total_vat'] ?? null),
		);
	}

	$allPass = true;
	foreach ($checks as $c) {
		if (empty($c['pass'])) {
			$allPass = false;
		}
	}

	echo json_encode(array(
		'ok' => $allPass,
		'tenant' => $tenantLabel,
		'host' => $host,
		'fta_sales_enabled' => epc_uae_vat_sales_enabled($db),
		'vat_rate_percent' => epc_uae_vat_rate_percent($db),
		'checks' => $checks,
		'guest' => $guest,
		'sample_users' => $sampleUsers,
		'einvoice_sample' => $einvoiceSample,
		'timestamp' => date('c'),
	), JSON_PRETTY_PRINT | JSON_UNESCAPED_UNICODE);
} catch (Throwable $e) {
	http_response_code(500);
	echo json_encode(array(
		'ok' => false,
		'error' => $e->getMessage(),
		'file' => basename($e->getFile()),
		'line' => $e->getLine(),
	));
}
