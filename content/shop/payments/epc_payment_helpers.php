<?php
/**
 * Payment gateways registry — GCC, Pakistan, crypto, international + legacy helpers.
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

function epc_payment_base_params($currency = 'AED')
{
	return array(
		array('name' => 'demo_mode', 'type' => 'checkbox', 'caption' => 'Demo mode (simulated checkout — no live charge)'),
		array('name' => 'currency', 'type' => 'text', 'caption' => 'Currency (' . $currency . ')'),
	);
}

/**
 * Full modern gateway catalog (excludes legacy CIS handlers already in DB).
 * region: gcc | pakistan | crypto | international
 */
function epc_payment_gateway_defs()
{
	$aed = epc_payment_base_params('AED');
	$sar = epc_payment_base_params('SAR');
	$pkr = epc_payment_base_params('PKR');
	$usd = epc_payment_base_params('USD');

	$defs = array(
		// —— Existing international / UAE ——
		'stripe' => array(
			'name' => 'epc_pay_stripe',
			'description' => 'epc_pay_stripe_desc',
			'region' => 'international',
			'countries' => array('AE', 'SA', 'PK', 'GLOBAL'),
			'parameters' => array_merge($aed, array(
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
			'region' => 'gcc',
			'countries' => array('AE', 'SA', 'BH', 'OM', 'QA', 'KW'),
			'parameters' => array_merge($aed, array(
				array('name' => 'store_id', 'type' => 'text', 'caption' => 'Store ID'),
				array('name' => 'auth_key', 'type' => 'password', 'caption' => 'Auth key'),
			)),
			'dummy' => array('demo_mode' => 1, 'currency' => 'AED', 'store_id' => 'EPC-DEMO-TELR', 'auth_key' => 'telr_dummy_auth_key'),
		),
		'paytabs' => array(
			'name' => 'epc_pay_paytabs',
			'description' => 'epc_pay_paytabs_desc',
			'region' => 'gcc',
			'countries' => array('AE', 'SA', 'BH', 'OM', 'QA', 'KW', 'EG', 'JO'),
			'parameters' => array_merge($aed, array(
				array('name' => 'profile_id', 'type' => 'text', 'caption' => 'Profile ID'),
				array('name' => 'server_key', 'type' => 'password', 'caption' => 'Server key'),
				array('name' => 'client_key', 'type' => 'text', 'caption' => 'Client key'),
			)),
			'dummy' => array('demo_mode' => 1, 'currency' => 'AED', 'profile_id' => 'EPC-DEMO-PAYTABS', 'server_key' => 'paytabs_dummy_server', 'client_key' => 'paytabs_dummy_client'),
		),
		'twocheckout' => array(
			'name' => 'epc_pay_twocheckout',
			'description' => 'epc_pay_twocheckout_desc',
			'region' => 'international',
			'countries' => array('GLOBAL'),
			'parameters' => array_merge($usd, array(
				array('name' => 'merchant_code', 'type' => 'text', 'caption' => 'Merchant code'),
				array('name' => 'secret_key', 'type' => 'password', 'caption' => 'Secret key'),
			)),
			'dummy' => array('demo_mode' => 1, 'currency' => 'USD', 'merchant_code' => 'EPC2CO', 'secret_key' => '2co_dummy_secret'),
		),
		'ccavenue' => array(
			'name' => 'epc_pay_ccavenue',
			'description' => 'epc_pay_ccavenue_desc',
			'region' => 'international',
			'countries' => array('AE', 'IN', 'GLOBAL'),
			'parameters' => array_merge($aed, array(
				array('name' => 'merchant_id', 'type' => 'text', 'caption' => 'Merchant ID'),
				array('name' => 'access_code', 'type' => 'password', 'caption' => 'Access code'),
				array('name' => 'working_key', 'type' => 'password', 'caption' => 'Working key'),
			)),
			'dummy' => array('demo_mode' => 1, 'currency' => 'AED', 'merchant_id' => 'EPCCCA', 'access_code' => 'cca_dummy_access', 'working_key' => 'cca_dummy_working'),
		),
		'amazon_ps' => array(
			'name' => 'epc_pay_amazon_ps',
			'description' => 'epc_pay_amazon_ps_desc',
			'region' => 'gcc',
			'countries' => array('AE', 'SA'),
			'parameters' => array_merge($aed, array(
				array('name' => 'merchant_id', 'type' => 'text', 'caption' => 'Merchant ID'),
				array('name' => 'access_code', 'type' => 'password', 'caption' => 'Access code'),
				array('name' => 'sha_request_phrase', 'type' => 'password', 'caption' => 'SHA request phrase'),
			)),
			'dummy' => array('demo_mode' => 1, 'currency' => 'AED', 'merchant_id' => 'EPC-AMZPS', 'access_code' => 'amzps_dummy_access', 'sha_request_phrase' => 'amzps_dummy_sha'),
		),
		'cashu' => array(
			'name' => 'epc_pay_cashu',
			'description' => 'epc_pay_cashu_desc',
			'region' => 'gcc',
			'countries' => array('AE', 'SA', 'EG'),
			'parameters' => array_merge($aed, array(
				array('name' => 'merchant_id', 'type' => 'text', 'caption' => 'Merchant ID'),
				array('name' => 'encryption_key', 'type' => 'password', 'caption' => 'Encryption key'),
			)),
			'dummy' => array('demo_mode' => 1, 'currency' => 'AED', 'merchant_id' => 'EPC-CASHU', 'encryption_key' => 'cashu_dummy_key'),
		),
		'cybersource' => array(
			'name' => 'epc_pay_cybersource',
			'description' => 'epc_pay_cybersource_desc',
			'region' => 'international',
			'countries' => array('AE', 'GLOBAL'),
			'parameters' => array_merge($aed, array(
				array('name' => 'merchant_id', 'type' => 'text', 'caption' => 'Merchant ID'),
				array('name' => 'api_key', 'type' => 'password', 'caption' => 'API key'),
				array('name' => 'shared_secret', 'type' => 'password', 'caption' => 'Shared secret'),
			)),
			'dummy' => array('demo_mode' => 1, 'currency' => 'AED', 'merchant_id' => 'epc_cybersource', 'api_key' => 'cybs_dummy_api', 'shared_secret' => 'cybs_dummy_secret'),
		),
		'razorpay' => array(
			'name' => 'epc_pay_razorpay',
			'description' => 'epc_pay_razorpay_desc',
			'region' => 'international',
			'countries' => array('AE', 'IN'),
			'parameters' => array_merge($aed, array(
				array('name' => 'key_id', 'type' => 'text', 'caption' => 'Key ID'),
				array('name' => 'key_secret', 'type' => 'password', 'caption' => 'Key secret'),
			)),
			'dummy' => array('demo_mode' => 1, 'currency' => 'AED', 'key_id' => 'rzp_test_epc_dummy', 'key_secret' => 'razorpay_dummy_secret'),
		),
		'paypal' => array(
			'name' => 'epc_pay_paypal',
			'description' => 'epc_pay_paypal_desc',
			'region' => 'international',
			'countries' => array('GLOBAL'),
			'parameters' => array_merge($usd, array(
				array('name' => 'client_id', 'type' => 'text', 'caption' => 'Client ID'),
				array('name' => 'client_secret', 'type' => 'password', 'caption' => 'Client secret'),
				array('name' => 'sandbox', 'type' => 'checkbox', 'caption' => 'Sandbox mode'),
			)),
			'dummy' => array('demo_mode' => 1, 'currency' => 'USD', 'client_id' => 'paypal_dummy_client_id', 'client_secret' => 'paypal_dummy_secret', 'sandbox' => 1),
		),
		'skrill' => array(
			'name' => 'epc_pay_skrill',
			'description' => 'epc_pay_skrill_desc',
			'region' => 'international',
			'countries' => array('GLOBAL'),
			'parameters' => array_merge($usd, array(
				array('name' => 'merchant_email', 'type' => 'text', 'caption' => 'Merchant email'),
				array('name' => 'secret_word', 'type' => 'password', 'caption' => 'Secret word'),
			)),
			'dummy' => array('demo_mode' => 1, 'currency' => 'USD', 'merchant_email' => 'payments@epartscart.demo', 'secret_word' => 'skrill_dummy_secret'),
		),
		'payoneer' => array(
			'name' => 'epc_pay_payoneer',
			'description' => 'epc_pay_payoneer_desc',
			'region' => 'international',
			'countries' => array('GLOBAL'),
			'parameters' => array_merge($usd, array(
				array('name' => 'program_id', 'type' => 'text', 'caption' => 'Program ID'),
				array('name' => 'api_token', 'type' => 'password', 'caption' => 'API token'),
			)),
			'dummy' => array('demo_mode' => 1, 'currency' => 'USD', 'program_id' => 'EPC-PAYONEER', 'api_token' => 'payoneer_dummy_token'),
		),
		'authorize_net' => array(
			'name' => 'epc_pay_authorize_net',
			'description' => 'epc_pay_authorize_net_desc',
			'region' => 'international',
			'countries' => array('US', 'GLOBAL'),
			'parameters' => array_merge($usd, array(
				array('name' => 'api_login_id', 'type' => 'text', 'caption' => 'API login ID'),
				array('name' => 'transaction_key', 'type' => 'password', 'caption' => 'Transaction key'),
			)),
			'dummy' => array('demo_mode' => 1, 'currency' => 'USD', 'api_login_id' => 'EPC-AUTHNET', 'transaction_key' => 'authnet_dummy_key'),
		),
		'adyen' => array(
			'name' => 'epc_pay_adyen',
			'description' => 'epc_pay_adyen_desc',
			'region' => 'international',
			'countries' => array('AE', 'GLOBAL'),
			'parameters' => array_merge($aed, array(
				array('name' => 'merchant_account', 'type' => 'text', 'caption' => 'Merchant account'),
				array('name' => 'api_key', 'type' => 'password', 'caption' => 'API key'),
				array('name' => 'client_key', 'type' => 'text', 'caption' => 'Client key'),
			)),
			'dummy' => array('demo_mode' => 1, 'currency' => 'AED', 'merchant_account' => 'EpartscartECOM', 'api_key' => 'adyen_dummy_api', 'client_key' => 'adyen_dummy_client'),
		),

		// —— New GCC ——
		'tabby' => array(
			'name' => 'epc_pay_tabby',
			'description' => 'epc_pay_tabby_desc',
			'region' => 'gcc',
			'countries' => array('AE', 'SA', 'KW', 'BH'),
			'parameters' => array_merge($aed, array(
				array('name' => 'public_key', 'type' => 'text', 'caption' => 'Public key'),
				array('name' => 'secret_key', 'type' => 'password', 'caption' => 'Secret key'),
				array('name' => 'merchant_code', 'type' => 'text', 'caption' => 'Merchant code'),
			)),
			'dummy' => array('demo_mode' => 1, 'currency' => 'AED', 'public_key' => 'pk_test_tabby_epc', 'secret_key' => 'sk_test_tabby_epc', 'merchant_code' => 'EPC-TABBY'),
		),
		'tamara' => array(
			'name' => 'epc_pay_tamara',
			'description' => 'epc_pay_tamara_desc',
			'region' => 'gcc',
			'countries' => array('AE', 'SA', 'KW'),
			'parameters' => array_merge($aed, array(
				array('name' => 'api_token', 'type' => 'password', 'caption' => 'API token'),
				array('name' => 'notification_token', 'type' => 'password', 'caption' => 'Notification token'),
				array('name' => 'public_key', 'type' => 'text', 'caption' => 'Public key'),
			)),
			'dummy' => array('demo_mode' => 1, 'currency' => 'AED', 'api_token' => 'tamara_dummy_api', 'notification_token' => 'tamara_dummy_notify', 'public_key' => 'tamara_dummy_public'),
		),
		'myfatoorah' => array(
			'name' => 'epc_pay_myfatoorah',
			'description' => 'epc_pay_myfatoorah_desc',
			'region' => 'gcc',
			'countries' => array('KW', 'SA', 'BH', 'AE', 'OM', 'QA', 'EG'),
			'parameters' => array_merge($aed, array(
				array('name' => 'api_key', 'type' => 'password', 'caption' => 'API token / key'),
				array('name' => 'api_url', 'type' => 'text', 'caption' => 'API base URL (sandbox or live)'),
			)),
			'dummy' => array(
				'demo_mode' => 1,
				'currency' => 'AED',
				'api_key' => 'myfatoorah_dummy_token',
				'api_url' => 'https://apitest.myfatoorah.com',
			),
		),
		'tap' => array(
			'name' => 'epc_pay_tap',
			'description' => 'epc_pay_tap_desc',
			'region' => 'gcc',
			'countries' => array('AE', 'SA', 'KW', 'BH', 'OM', 'QA', 'EG'),
			'parameters' => array_merge($aed, array(
				array('name' => 'secret_key', 'type' => 'password', 'caption' => 'Secret key'),
				array('name' => 'public_key', 'type' => 'text', 'caption' => 'Public key'),
			)),
			'dummy' => array('demo_mode' => 1, 'currency' => 'AED', 'secret_key' => 'sk_test_tap_epc', 'public_key' => 'pk_test_tap_epc'),
		),
		'hyperpay' => array(
			'name' => 'epc_pay_hyperpay',
			'description' => 'epc_pay_hyperpay_desc',
			'region' => 'gcc',
			'countries' => array('SA', 'AE', 'EG'),
			'parameters' => array_merge($sar, array(
				array('name' => 'entity_id', 'type' => 'text', 'caption' => 'Entity ID'),
				array('name' => 'access_token', 'type' => 'password', 'caption' => 'Access token'),
				array('name' => 'api_url', 'type' => 'text', 'caption' => 'OPPWA API URL'),
			)),
			'dummy' => array(
				'demo_mode' => 1,
				'currency' => 'SAR',
				'entity_id' => '8ac7a4c7epc_entity',
				'access_token' => 'hyperpay_dummy_token',
				'api_url' => 'https://eu-test.oppwa.com',
			),
		),
		'checkout_com' => array(
			'name' => 'epc_pay_checkout_com',
			'description' => 'epc_pay_checkout_com_desc',
			'region' => 'gcc',
			'countries' => array('AE', 'SA', 'GLOBAL'),
			'parameters' => array_merge($aed, array(
				array('name' => 'public_key', 'type' => 'text', 'caption' => 'Public key'),
				array('name' => 'secret_key', 'type' => 'password', 'caption' => 'Secret key'),
				array('name' => 'processing_channel', 'type' => 'text', 'caption' => 'Processing channel ID'),
			)),
			'dummy' => array(
				'demo_mode' => 1,
				'currency' => 'AED',
				'public_key' => 'pk_sbox_checkout_epc',
				'secret_key' => 'sk_sbox_checkout_epc',
				'processing_channel' => 'pc_epc_demo',
			),
		),
		'network_intl' => array(
			'name' => 'epc_pay_network_intl',
			'description' => 'epc_pay_network_intl_desc',
			'region' => 'gcc',
			'countries' => array('AE', 'SA', 'EG'),
			'parameters' => array_merge($aed, array(
				array('name' => 'outlet_ref', 'type' => 'text', 'caption' => 'Outlet reference'),
				array('name' => 'api_key', 'type' => 'password', 'caption' => 'API key'),
				array('name' => 'api_url', 'type' => 'text', 'caption' => 'N-Genius API base URL'),
			)),
			'dummy' => array(
				'demo_mode' => 1,
				'currency' => 'AED',
				'outlet_ref' => 'epc-outlet-demo',
				'api_key' => 'network_intl_dummy_key',
				'api_url' => 'https://api-gateway.sandbox.ngenius-payments.com',
			),
		),

		// —— Pakistan ——
		'jazzcash' => array(
			'name' => 'epc_pay_jazzcash',
			'description' => 'epc_pay_jazzcash_desc',
			'region' => 'pakistan',
			'countries' => array('PK'),
			'parameters' => array_merge($pkr, array(
				array('name' => 'merchant_id', 'type' => 'text', 'caption' => 'Merchant ID'),
				array('name' => 'password', 'type' => 'password', 'caption' => 'Password'),
				array('name' => 'integrity_salt', 'type' => 'password', 'caption' => 'Integrity salt'),
				array('name' => 'return_url', 'type' => 'text', 'caption' => 'Return URL (optional override)'),
			)),
			'dummy' => array(
				'demo_mode' => 1,
				'currency' => 'PKR',
				'merchant_id' => 'MC12345',
				'password' => 'jazzcash_dummy_pass',
				'integrity_salt' => 'jazzcash_dummy_salt',
				'return_url' => '',
			),
		),
		'easypaisa' => array(
			'name' => 'epc_pay_easypaisa',
			'description' => 'epc_pay_easypaisa_desc',
			'region' => 'pakistan',
			'countries' => array('PK'),
			'parameters' => array_merge($pkr, array(
				array('name' => 'store_id', 'type' => 'text', 'caption' => 'Store ID'),
				array('name' => 'account_num', 'type' => 'text', 'caption' => 'Account number'),
				array('name' => 'hash_key', 'type' => 'password', 'caption' => 'Hash key'),
			)),
			'dummy' => array(
				'demo_mode' => 1,
				'currency' => 'PKR',
				'store_id' => 'EPC-EP-STORE',
				'account_num' => '03001234567',
				'hash_key' => 'easypaisa_dummy_hash',
			),
		),

		// —— Crypto ——
		'nowpayments' => array(
			'name' => 'epc_pay_nowpayments',
			'description' => 'epc_pay_nowpayments_desc',
			'region' => 'crypto',
			'countries' => array('GLOBAL', 'AE', 'SA', 'PK'),
			'parameters' => array_merge($usd, array(
				array('name' => 'api_key', 'type' => 'password', 'caption' => 'NOWPayments API key'),
				array('name' => 'ipn_secret', 'type' => 'password', 'caption' => 'IPN secret'),
				array('name' => 'allowed_coins', 'type' => 'text', 'caption' => 'Allowed coins (comma: usdttrc20,btc,eth,usdtbsc)'),
				array('name' => 'sandbox', 'type' => 'checkbox', 'caption' => 'Sandbox / test API host'),
			)),
			'dummy' => array(
				'demo_mode' => 1,
				'currency' => 'USD',
				'api_key' => 'NOWPAYMENTS_DUMMY_API_KEY',
				'ipn_secret' => 'nowpayments_dummy_ipn',
				'allowed_coins' => 'usdttrc20,btc,eth,ltc',
				'sandbox' => 1,
			),
		),
	);

	return $defs;
}

/** @deprecated use epc_payment_gateway_defs() */
function epc_payment_uae_gateway_defs()
{
	return epc_payment_gateway_defs();
}

function epc_payment_region_labels()
{
	return array(
		'gcc' => 'GCC & MENA',
		'pakistan' => 'Pakistan',
		'crypto' => 'Cryptocurrency',
		'international' => 'International',
		'legacy' => 'Legacy (CIS)',
	);
}

function epc_payment_lang_seed(PDO $db)
{
	$strings = array(
		'epc_cp_group_payments' => array('Payment gateways', 'Платёжные системы'),
		'epc_payments_cp' => array('Payment gateways', 'Платёжные системы'),
		'epc_payments_guide_cp' => array('Payments guide', 'Гид по оплате'),
		'epc_pay_stripe' => array('Stripe', 'Stripe'),
		'epc_pay_stripe_desc' => array('Cards, Apple Pay, Google Pay via Stripe — UAE & global.', 'Карты и кошельки Stripe.'),
		'epc_pay_telr' => array('Telr', 'Telr'),
		'epc_pay_telr_desc' => array('UAE aggregator — Visa, MC, Apple Pay, local methods.', 'Агрегатор ОАЭ.'),
		'epc_pay_paytabs' => array('PayTabs', 'PayTabs'),
		'epc_pay_paytabs_desc' => array('MENA cards, wallets, Apple Pay, invoicing.', 'Платежи MENA.'),
		'epc_pay_twocheckout' => array('2Checkout (Verifone)', '2Checkout'),
		'epc_pay_twocheckout_desc' => array('Global checkout — 200+ countries.', 'Глобальный checkout.'),
		'epc_pay_ccavenue' => array('CCAvenue', 'CCAvenue'),
		'epc_pay_ccavenue_desc' => array('Cards, net banking, bank transfer.', 'Карты и переводы.'),
		'epc_pay_amazon_ps' => array('Amazon Payment Services', 'Amazon Payment Services'),
		'epc_pay_amazon_ps_desc' => array('Amazon Payment Services for UAE/KSA merchants.', 'Amazon Pay для ОАЭ/КСА.'),
		'epc_pay_cashu' => array('CashU', 'CashU'),
		'epc_pay_cashu_desc' => array('Digital wallet — MENA region.', 'Кошелёк MENA.'),
		'epc_pay_cybersource' => array('CyberSource', 'CyberSource'),
		'epc_pay_cybersource_desc' => array('Visa infrastructure — enterprise fraud tools.', 'Visa CyberSource.'),
		'epc_pay_razorpay' => array('Razorpay', 'Razorpay'),
		'epc_pay_razorpay_desc' => array('Cards, BNPL, net banking.', 'Razorpay.'),
		'epc_pay_paypal' => array('PayPal', 'PayPal'),
		'epc_pay_paypal_desc' => array('PayPal checkout — export & guest buyers.', 'PayPal.'),
		'epc_pay_skrill' => array('Skrill', 'Skrill'),
		'epc_pay_skrill_desc' => array('Digital wallet — multi-currency.', 'Skrill.'),
		'epc_pay_payoneer' => array('Payoneer', 'Payoneer'),
		'epc_pay_payoneer_desc' => array('Cross-border collections.', 'Payoneer.'),
		'epc_pay_authorize_net' => array('Authorize.net', 'Authorize.net'),
		'epc_pay_authorize_net_desc' => array('US gateway — cards.', 'Authorize.net.'),
		'epc_pay_adyen' => array('Adyen', 'Adyen'),
		'epc_pay_adyen_desc' => array('Unified online payments at scale.', 'Adyen.'),
		'epc_pay_tabby' => array('Tabby', 'Tabby'),
		'epc_pay_tabby_desc' => array('Pay-in-4 BNPL — UAE, KSA, Kuwait, Bahrain.', 'BNPL Tabby для GCC.'),
		'epc_pay_tamara' => array('Tamara', 'Tamara'),
		'epc_pay_tamara_desc' => array('Sharia-compliant BNPL — UAE, KSA, Kuwait.', 'BNPL Tamara.'),
		'epc_pay_myfatoorah' => array('MyFatoorah', 'MyFatoorah'),
		'epc_pay_myfatoorah_desc' => array('GCC multi-country gateway — KNET, MADA, cards, Apple Pay.', 'MyFatoorah GCC.'),
		'epc_pay_tap' => array('Tap Payments', 'Tap Payments'),
		'epc_pay_tap_desc' => array('GCC cards, Apple Pay, Google Pay, benefit.', 'Tap Payments GCC.'),
		'epc_pay_hyperpay' => array('HyperPay', 'HyperPay'),
		'epc_pay_hyperpay_desc' => array('OPPWA / HyperPay — strong in KSA (MADA) & UAE.', 'HyperPay / OPPWA.'),
		'epc_pay_checkout_com' => array('Checkout.com', 'Checkout.com'),
		'epc_pay_checkout_com_desc' => array('Enterprise cards & wallets — UAE DIFC ready.', 'Checkout.com.'),
		'epc_pay_network_intl' => array('Network International (N-Genius)', 'Network International'),
		'epc_pay_network_intl_desc' => array('N-Genius Online — major UAE acquirer.', 'N-Genius ОАЭ.'),
		'epc_pay_jazzcash' => array('JazzCash', 'JazzCash'),
		'epc_pay_jazzcash_desc' => array('Pakistan mobile wallet & card checkout (PKR).', 'JazzCash Пакистан.'),
		'epc_pay_easypaisa' => array('Easypaisa', 'Easypaisa'),
		'epc_pay_easypaisa_desc' => array('Pakistan Easypaisa wallet & OTC (PKR).', 'Easypaisa Пакистан.'),
		'epc_pay_nowpayments' => array('Crypto (NOWPayments)', 'Крипто (NOWPayments)'),
		'epc_pay_nowpayments_desc' => array('Pay with USDT, BTC, ETH and 100+ coins via NOWPayments.', 'Оплата USDT/BTC/ETH через NOWPayments.'),
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
	return epc_payment_seed_all_gateways($db);
}

function epc_payment_seed_all_gateways(PDO $db)
{
	epc_payment_lang_seed($db);
	$ids = array();
	foreach (epc_payment_gateway_defs() as $handler => $def) {
		$ids[$handler] = epc_payment_upsert_gateway($db, $handler, $def);
	}
	return $ids;
}

function epc_payment_enable_legacy(PDO $db)
{
	$db->exec("UPDATE `shop_payment_systems` SET `anable` = 1 WHERE `handler` <> '' AND `handler` IS NOT NULL");
	$rows = $db->query('SELECT `id`, `handler`, `parameters`, `parameters_values` FROM `shop_payment_systems`')->fetchAll(PDO::FETCH_ASSOC);
	$modern = epc_payment_gateway_defs();
	foreach ($rows as $r) {
		if (isset($modern[(string)$r['handler']])) {
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
			if (($p['type'] ?? '') === 'checkbox') {
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

function epc_payment_list_selectable(PDO $db)
{
	$st = $db->query('SELECT `id`, `name`, `handler`, `description`, `active`, `parameters_values` FROM `shop_payment_systems` WHERE `anable` = 1 AND `handler` <> \'\' ORDER BY `active` DESC, `id` ASC');
	$rows = $st ? $st->fetchAll(PDO::FETCH_ASSOC) : array();
	$out = array();
	foreach ($rows as $r) {
		$out[] = array(
			'id' => (int)$r['id'],
			'handler' => (string)$r['handler'],
			'name' => epc_payment_gateway_label($r),
			'active' => (int)$r['active'],
			'region' => epc_payment_handler_region((string)$r['handler']),
		);
	}
	return $out;
}

function epc_payment_handler_region($handler)
{
	$defs = epc_payment_gateway_defs();
	if (isset($defs[$handler]['region'])) {
		return (string)$defs[$handler]['region'];
	}
	return 'legacy';
}

function epc_payment_is_uae($handler)
{
	$region = epc_payment_handler_region($handler);
	return in_array($region, array('gcc', 'international', 'crypto', 'pakistan'), true);
}

function epc_payment_is_modern($handler)
{
	return isset(epc_payment_gateway_defs()[(string)$handler]);
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
	$byRegion = array('gcc' => 0, 'pakistan' => 0, 'crypto' => 0, 'international' => 0, 'legacy' => 0);
	foreach ($all as $g) {
		if ((int)$g['active'] === 1) {
			$active = $g;
		}
		$region = epc_payment_handler_region($g['handler']);
		if (!isset($byRegion[$region])) {
			$byRegion[$region] = 0;
		}
		$byRegion[$region]++;
	}
	$uae = (int)$byRegion['gcc'] + (int)$byRegion['international'];
	return array(
		'total' => count($all),
		'uae_gateways' => $uae,
		'gcc_gateways' => (int)$byRegion['gcc'],
		'pakistan_gateways' => (int)$byRegion['pakistan'],
		'crypto_gateways' => (int)$byRegion['crypto'],
		'international_gateways' => (int)$byRegion['international'],
		'legacy_gateways' => (int)$byRegion['legacy'],
		'by_region' => $byRegion,
		'active_handler' => $active ? (string)$active['handler'] : '',
		'active_name' => $active ? epc_payment_gateway_label($active) : '',
		'gateways' => $all,
	);
}

function epc_payment_handler_title($handler)
{
	$defs = epc_payment_gateway_defs();
	if (isset($defs[$handler])) {
		return function_exists('translate_str_by_id') ? translate_str_by_id($defs[$handler]['name']) : $handler;
	}
	return ucfirst(str_replace('_', ' ', (string)$handler));
}

function epc_payment_resolve_handler(PDO $db, $requested = '')
{
	$requested = preg_replace('/[^a-z0-9_]/', '', (string)$requested);
	if ($requested !== '') {
		$st = $db->prepare('SELECT `handler` FROM `shop_payment_systems` WHERE `handler` = ? AND `anable` = 1 LIMIT 1');
		$st->execute(array($requested));
		$h = $st->fetchColumn();
		if ($h) {
			return (string)$h;
		}
	}
	$st = $db->query('SELECT `handler` FROM `shop_payment_systems` WHERE `active` = 1 LIMIT 1');
	$h = $st ? $st->fetchColumn() : '';
	return $h ? (string)$h : '';
}
