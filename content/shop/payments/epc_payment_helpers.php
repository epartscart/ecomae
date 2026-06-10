<?php
/**
 * Payment gateways registry — UAE + legacy, dummy defaults, CP hub helpers.
 */
defined('_ASTEXE_') or die('No access');

function epc_payment_h($s)
{
	return htmlspecialchars((string)$s, ENT_QUOTES, 'UTF-8');
}

function epc_payment_money($n)
{
	return number_format((float)$n, 2, '.', ',');
}

function epc_payment_base_params()
{
	return array(
		array('name' => 'demo_mode', 'type' => 'checkbox', 'caption' => 'Demo mode (simulated checkout — no live charge)'),
		array('name' => 'currency', 'type' => 'text', 'caption' => 'Currency (AED)'),
	);
}

function epc_payment_uae_gateway_defs()
{
	$base = epc_payment_base_params();
	$notify = 'Webhook URL: https://YOUR_DOMAIN/content/shop/finance/payment_systems/HANDLER/notification.php';

	return array(
		'stripe' => array(
			'name' => 'epc_pay_stripe',
			'description' => 'epc_pay_stripe_desc',
			'region' => 'uae',
			'parameters' => array_merge($base, array(
				array('name' => 'publishable_key', 'type' => 'text', 'caption' => 'Publishable key'),
				array('name' => 'secret_key', 'type' => 'password', 'caption' => 'Secret key'),
				array('name' => 'webhook_secret', 'type' => 'password', 'caption' => 'Webhook signing secret'),
			)),
			'dummy' => array(
				'demo_mode' => 1,
				'currency' => 'AED',
				'publishable_key' => 'pk_test_epc_dummy_publishable_key',
				'secret_key' => 'sk_test_epc_dummy_secret_key',
				'webhook_secret' => 'whsec_epc_dummy_webhook',
			),
		),
		'telr' => array(
			'name' => 'epc_pay_telr',
			'description' => 'epc_pay_telr_desc',
			'region' => 'uae',
			'parameters' => array_merge($base, array(
				array('name' => 'store_id', 'type' => 'text', 'caption' => 'Store ID'),
				array('name' => 'auth_key', 'type' => 'password', 'caption' => 'Auth key'),
			)),
			'dummy' => array('demo_mode' => 1, 'currency' => 'AED', 'store_id' => 'EPC-DEMO-TELR', 'auth_key' => 'telr_dummy_auth_key'),
		),
		'paytabs' => array(
			'name' => 'epc_pay_paytabs',
			'description' => 'epc_pay_paytabs_desc',
			'region' => 'uae',
			'parameters' => array_merge($base, array(
				array('name' => 'profile_id', 'type' => 'text', 'caption' => 'Profile ID'),
				array('name' => 'server_key', 'type' => 'password', 'caption' => 'Server key'),
				array('name' => 'client_key', 'type' => 'text', 'caption' => 'Client key'),
			)),
			'dummy' => array('demo_mode' => 1, 'currency' => 'AED', 'profile_id' => 'EPC-DEMO-PAYTABS', 'server_key' => 'paytabs_dummy_server', 'client_key' => 'paytabs_dummy_client'),
		),
		'twocheckout' => array(
			'name' => 'epc_pay_twocheckout',
			'description' => 'epc_pay_twocheckout_desc',
			'region' => 'uae',
			'parameters' => array_merge($base, array(
				array('name' => 'merchant_code', 'type' => 'text', 'caption' => 'Merchant code'),
				array('name' => 'secret_key', 'type' => 'password', 'caption' => 'Secret key'),
			)),
			'dummy' => array('demo_mode' => 1, 'currency' => 'AED', 'merchant_code' => 'EPC2CO', 'secret_key' => '2co_dummy_secret'),
		),
		'ccavenue' => array(
			'name' => 'epc_pay_ccavenue',
			'description' => 'epc_pay_ccavenue_desc',
			'region' => 'uae',
			'parameters' => array_merge($base, array(
				array('name' => 'merchant_id', 'type' => 'text', 'caption' => 'Merchant ID'),
				array('name' => 'access_code', 'type' => 'password', 'caption' => 'Access code'),
				array('name' => 'working_key', 'type' => 'password', 'caption' => 'Working key'),
			)),
			'dummy' => array('demo_mode' => 1, 'currency' => 'AED', 'merchant_id' => 'EPCCCA', 'access_code' => 'cca_dummy_access', 'working_key' => 'cca_dummy_working'),
		),
		'amazon_ps' => array(
			'name' => 'epc_pay_amazon_ps',
			'description' => 'epc_pay_amazon_ps_desc',
			'region' => 'uae',
			'parameters' => array_merge($base, array(
				array('name' => 'merchant_id', 'type' => 'text', 'caption' => 'Merchant ID'),
				array('name' => 'access_code', 'type' => 'password', 'caption' => 'Access code'),
				array('name' => 'sha_request_phrase', 'type' => 'password', 'caption' => 'SHA request phrase'),
			)),
			'dummy' => array('demo_mode' => 1, 'currency' => 'AED', 'merchant_id' => 'EPC-AMZPS', 'access_code' => 'amzps_dummy_access', 'sha_request_phrase' => 'amzps_dummy_sha'),
		),
		'cashu' => array(
			'name' => 'epc_pay_cashu',
			'description' => 'epc_pay_cashu_desc',
			'region' => 'uae',
			'parameters' => array_merge($base, array(
				array('name' => 'merchant_id', 'type' => 'text', 'caption' => 'Merchant ID'),
				array('name' => 'encryption_key', 'type' => 'password', 'caption' => 'Encryption key'),
			)),
			'dummy' => array('demo_mode' => 1, 'currency' => 'AED', 'merchant_id' => 'EPC-CASHU', 'encryption_key' => 'cashu_dummy_key'),
		),
		'cybersource' => array(
			'name' => 'epc_pay_cybersource',
			'description' => 'epc_pay_cybersource_desc',
			'region' => 'uae',
			'parameters' => array_merge($base, array(
				array('name' => 'merchant_id', 'type' => 'text', 'caption' => 'Merchant ID'),
				array('name' => 'api_key', 'type' => 'password', 'caption' => 'API key'),
				array('name' => 'shared_secret', 'type' => 'password', 'caption' => 'Shared secret'),
			)),
			'dummy' => array('demo_mode' => 1, 'currency' => 'AED', 'merchant_id' => 'epc_cybersource', 'api_key' => 'cybs_dummy_api', 'shared_secret' => 'cybs_dummy_secret'),
		),
		'razorpay' => array(
			'name' => 'epc_pay_razorpay',
			'description' => 'epc_pay_razorpay_desc',
			'region' => 'uae',
			'parameters' => array_merge($base, array(
				array('name' => 'key_id', 'type' => 'text', 'caption' => 'Key ID'),
				array('name' => 'key_secret', 'type' => 'password', 'caption' => 'Key secret'),
			)),
			'dummy' => array('demo_mode' => 1, 'currency' => 'AED', 'key_id' => 'rzp_test_epc_dummy', 'key_secret' => 'razorpay_dummy_secret'),
		),
		'paypal' => array(
			'name' => 'epc_pay_paypal',
			'description' => 'epc_pay_paypal_desc',
			'region' => 'uae',
			'parameters' => array_merge($base, array(
				array('name' => 'client_id', 'type' => 'text', 'caption' => 'Client ID'),
				array('name' => 'client_secret', 'type' => 'password', 'caption' => 'Client secret'),
				array('name' => 'sandbox', 'type' => 'checkbox', 'caption' => 'Sandbox mode'),
			)),
			'dummy' => array('demo_mode' => 1, 'currency' => 'AED', 'client_id' => 'paypal_dummy_client_id', 'client_secret' => 'paypal_dummy_secret', 'sandbox' => 1),
		),
		'skrill' => array(
			'name' => 'epc_pay_skrill',
			'description' => 'epc_pay_skrill_desc',
			'region' => 'uae',
			'parameters' => array_merge($base, array(
				array('name' => 'merchant_email', 'type' => 'text', 'caption' => 'Merchant email'),
				array('name' => 'secret_word', 'type' => 'password', 'caption' => 'Secret word'),
			)),
			'dummy' => array('demo_mode' => 1, 'currency' => 'AED', 'merchant_email' => 'payments@epartscart.demo', 'secret_word' => 'skrill_dummy_secret'),
		),
		'payoneer' => array(
			'name' => 'epc_pay_payoneer',
			'description' => 'epc_pay_payoneer_desc',
			'region' => 'uae',
			'parameters' => array_merge($base, array(
				array('name' => 'program_id', 'type' => 'text', 'caption' => 'Program ID'),
				array('name' => 'api_token', 'type' => 'password', 'caption' => 'API token'),
			)),
			'dummy' => array('demo_mode' => 1, 'currency' => 'AED', 'program_id' => 'EPC-PAYONEER', 'api_token' => 'payoneer_dummy_token'),
		),
		'authorize_net' => array(
			'name' => 'epc_pay_authorize_net',
			'description' => 'epc_pay_authorize_net_desc',
			'region' => 'uae',
			'parameters' => array_merge($base, array(
				array('name' => 'api_login_id', 'type' => 'text', 'caption' => 'API login ID'),
				array('name' => 'transaction_key', 'type' => 'password', 'caption' => 'Transaction key'),
			)),
			'dummy' => array('demo_mode' => 1, 'currency' => 'AED', 'api_login_id' => 'EPC-AUTHNET', 'transaction_key' => 'authnet_dummy_key'),
		),
		'adyen' => array(
			'name' => 'epc_pay_adyen',
			'description' => 'epc_pay_adyen_desc',
			'region' => 'uae',
			'parameters' => array_merge($base, array(
				array('name' => 'merchant_account', 'type' => 'text', 'caption' => 'Merchant account'),
				array('name' => 'api_key', 'type' => 'password', 'caption' => 'API key'),
				array('name' => 'client_key', 'type' => 'text', 'caption' => 'Client key'),
			)),
			'dummy' => array('demo_mode' => 1, 'currency' => 'AED', 'merchant_account' => 'EpartscartECOM', 'api_key' => 'adyen_dummy_api', 'client_key' => 'adyen_dummy_client'),
		),
	);
}

function epc_payment_lang_seed(PDO $db)
{
	$strings = array(
		'epc_cp_group_payments' => array('Payment gateways', 'Платёжные системы'),
		'epc_payments_cp' => array('Payment gateways', 'Платёжные системы'),
		'epc_payments_guide_cp' => array('Payments guide', 'Гид по оплате'),
		'epc_pay_stripe' => array('Stripe', 'Stripe'),
		'epc_pay_stripe_desc' => array('Cards, Apple Pay, international — replace dummy keys when live.', 'Карты, Apple Pay — замените ключи для production.'),
		'epc_pay_telr' => array('Telr', 'Telr'),
		'epc_pay_telr_desc' => array('UAE payment aggregator — Visa, MC, Apple Pay, SADAD.', 'Агрегатор ОАЭ.'),
		'epc_pay_paytabs' => array('PayTabs', 'PayTabs'),
		'epc_pay_paytabs_desc' => array('MENA payments — cards, wallets, invoicing.', 'Платежи MENA.'),
		'epc_pay_twocheckout' => array('2Checkout (Verifone)', '2Checkout'),
		'epc_pay_twocheckout_desc' => array('Global checkout — 200+ countries.', 'Глобальный checkout.'),
		'epc_pay_ccavenue' => array('CCAvenue', 'CCAvenue'),
		'epc_pay_ccavenue_desc' => array('Cards, net banking, bank transfer.', 'Карты и переводы.'),
		'epc_pay_amazon_ps' => array('Amazon Payment Services', 'Amazon Payment Services'),
		'epc_pay_amazon_ps_desc' => array('Amazon Pay checkout for UAE merchants.', 'Amazon Pay для ОАЭ.'),
		'epc_pay_cashu' => array('CashU', 'CashU'),
		'epc_pay_cashu_desc' => array('Digital wallet — MENA region.', 'Кошелёк MENA.'),
		'epc_pay_cybersource' => array('CyberSource', 'CyberSource'),
		'epc_pay_cybersource_desc' => array('Visa infrastructure — enterprise fraud tools.', 'Visa CyberSource.'),
		'epc_pay_razorpay' => array('Razorpay', 'Razorpay'),
		'epc_pay_razorpay_desc' => array('Cards, BNPL, net banking.', 'Razorpay UAE.'),
		'epc_pay_paypal' => array('PayPal', 'PayPal'),
		'epc_pay_paypal_desc' => array('PayPal checkout — good for export customers.', 'PayPal для экспорта.'),
		'epc_pay_skrill' => array('Skrill', 'Skrill'),
		'epc_pay_skrill_desc' => array('Digital wallet — multi-currency.', 'Skrill кошелёк.'),
		'epc_pay_payoneer' => array('Payoneer', 'Payoneer'),
		'epc_pay_payoneer_desc' => array('Cross-border payouts & collections.', 'Payoneer.'),
		'epc_pay_authorize_net' => array('Authorize.net', 'Authorize.net'),
		'epc_pay_authorize_net_desc' => array('US gateway — cards, Apple Pay via partner.', 'Authorize.net.'),
		'epc_pay_adyen' => array('Adyen', 'Adyen'),
		'epc_pay_adyen_desc' => array('Unified POS + online — Uber-scale volume.', 'Adyen unified.'),
	);
	foreach ($strings as $key => $pair) {
		$db->prepare('INSERT IGNORE INTO `lang_text_strings` (`str_key`, `description`, `same`, `is_error`, `is_custom`, `used_found`) VALUES (?, ?, NULL, 0, 1, 1)')->execute(array($key, $pair[0]));
		$db->prepare('INSERT INTO `lang_text_strings_translation` (`str_key`, `lang_code`, `value`) VALUES (?, ?, ?) ON DUPLICATE KEY UPDATE `value` = VALUES(`value`)')->execute(array($key, 'en', $pair[0]));
		$db->prepare('INSERT INTO `lang_text_strings_translation` (`str_key`, `lang_code`, `value`) VALUES (?, ?, ?) ON DUPLICATE KEY UPDATE `value` = VALUES(`value`)')->execute(array($key, 'ru', $pair[1]));
	}
}

function epc_payment_upsert_gateway(PDO $db, $handler, $def)
{
	$st = $db->prepare('SELECT `id`, `parameters_values`, `active` FROM `shop_payment_systems` WHERE `handler` = ? LIMIT 1');
	$st->execute(array($handler));
	$row = $st->fetch(PDO::FETCH_ASSOC);
	$paramsJson = json_encode($def['parameters'], JSON_UNESCAPED_UNICODE);
	$dummyJson = json_encode($def['dummy'], JSON_UNESCAPED_UNICODE);
	if ($row) {
		$vals = trim((string)$row['parameters_values']);
		if ($vals === '' || $vals === '[]' || $vals === 'null') {
			$db->prepare('UPDATE `shop_payment_systems` SET `parameters_values` = ?, `anable` = 1 WHERE `id` = ?')->execute(array($dummyJson, (int)$row['id']));
		}
		$db->prepare('UPDATE `shop_payment_systems` SET `name` = ?, `description` = ?, `parameters` = ?, `anable` = 1 WHERE `id` = ?')
			->execute(array($def['name'], $def['description'], $paramsJson, (int)$row['id']));
		return (int)$row['id'];
	}
	$db->prepare(
		'INSERT INTO `shop_payment_systems` (`name`, `parameters`, `parameters_values`, `anable`, `description`, `active`, `handler`)
		 VALUES (?, ?, ?, 1, ?, 0, ?)'
	)->execute(array($def['name'], $paramsJson, $dummyJson, $def['description'], $handler));
	return (int)$db->lastInsertId();
}

function epc_payment_seed_uae_gateways(PDO $db)
{
	epc_payment_lang_seed($db);
	$ids = array();
	foreach (epc_payment_uae_gateway_defs() as $handler => $def) {
		$ids[$handler] = epc_payment_upsert_gateway($db, $handler, $def);
	}
	return $ids;
}

function epc_payment_enable_legacy(PDO $db)
{
	$db->exec("UPDATE `shop_payment_systems` SET `anable` = 1 WHERE `handler` <> '' AND `handler` IS NOT NULL");
	$rows = $db->query('SELECT `id`, `handler`, `parameters`, `parameters_values` FROM `shop_payment_systems`')->fetchAll(PDO::FETCH_ASSOC);
	foreach ($rows as $r) {
		if (isset(epc_payment_uae_gateway_defs()[(string)$r['handler']])) {
			continue;
		}
		$vals = trim((string)$r['parameters_values']);
		if ($vals !== '' && $vals !== '[]') {
			continue;
		}
		$params = json_decode((string)$r['parameters'], true);
		if (!is_array($params)) {
			continue;
		}
		$dummy = array('demo_mode' => 1);
		foreach ($params as $p) {
			if (!isset($p['name'])) {
				continue;
			}
			$n = (string)$p['name'];
			if ($p['type'] === 'checkbox') {
				$dummy[$n] = 1;
			} else {
				$dummy[$n] = 'DUMMY_' . strtoupper(str_replace('.', '_', $n));
			}
		}
		$db->prepare('UPDATE `shop_payment_systems` SET `parameters_values` = ? WHERE `id` = ?')->execute(array(json_encode($dummy), (int)$r['id']));
	}
}

function epc_payment_set_active(PDO $db, $handler)
{
	$db->exec('UPDATE `shop_payment_systems` SET `active` = 0');
	$db->prepare('UPDATE `shop_payment_systems` SET `active` = 1 WHERE `handler` = ? LIMIT 1')->execute(array($handler));
}

function epc_payment_list_all(PDO $db)
{
	return $db->query('SELECT * FROM `shop_payment_systems` WHERE `handler` <> \'\' ORDER BY `active` DESC, `id` ASC')->fetchAll(PDO::FETCH_ASSOC);
}

function epc_payment_is_uae($handler)
{
	return isset(epc_payment_uae_gateway_defs()[(string)$handler]);
}

function epc_payment_gateway_label($row)
{
	if (function_exists('translate_str_by_id')) {
		return translate_str_by_id($row['name']);
	}
	return (string)$row['name'];
}

function epc_payment_demo_report(PDO $db)
{
	$all = epc_payment_list_all($db);
	$active = null;
	$uae = 0;
	$legacy = 0;
	foreach ($all as $g) {
		if ((int)$g['active'] === 1) {
			$active = $g;
		}
		if (epc_payment_is_uae($g['handler'])) {
			$uae++;
		} else {
			$legacy++;
		}
	}
	return array(
		'total' => count($all),
		'uae_gateways' => $uae,
		'legacy_gateways' => $legacy,
		'active_handler' => $active ? (string)$active['handler'] : '',
		'active_name' => $active ? epc_payment_gateway_label($active) : '',
		'gateways' => $all,
	);
}

function epc_payment_handler_title($handler)
{
	$defs = epc_payment_uae_gateway_defs();
	if (isset($defs[$handler])) {
		return function_exists('translate_str_by_id') ? translate_str_by_id($defs[$handler]['name']) : $handler;
	}
	return ucfirst(str_replace('_', ' ', (string)$handler));
}
