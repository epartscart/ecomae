<?php
/**
 * Crypto checkout helpers — NOWPayments (live) + demo invoice.
 */
defined('_ASTEXE_') or die('No access');

function epc_crypto_default_coins()
{
	return array(
		'usdttrc20' => array('label' => 'USDT (TRC20)', 'network' => 'Tron'),
		'usdtbsc' => array('label' => 'USDT (BEP20)', 'network' => 'BNB Smart Chain'),
		'btc' => array('label' => 'Bitcoin', 'network' => 'BTC'),
		'eth' => array('label' => 'Ethereum', 'network' => 'ETH'),
		'ltc' => array('label' => 'Litecoin', 'network' => 'LTC'),
	);
}

function epc_crypto_allowed_coins(array $params)
{
	$raw = trim((string)($params['allowed_coins'] ?? ''));
	$all = epc_crypto_default_coins();
	if ($raw === '') {
		return $all;
	}
	$out = array();
	foreach (preg_split('/\s*,\s*/', $raw) as $code) {
		$code = strtolower(preg_replace('/[^a-z0-9]/', '', $code));
		if ($code === '') {
			continue;
		}
		if (isset($all[$code])) {
			$out[$code] = $all[$code];
		} else {
			$out[$code] = array('label' => strtoupper($code), 'network' => '');
		}
	}
	return $out ?: $all;
}

function epc_crypto_api_base(array $params)
{
	return !empty($params['sandbox'])
		? 'https://api-sandbox.nowpayments.io/v1'
		: 'https://api.nowpayments.io/v1';
}

function epc_crypto_http_json($method, $url, array $headers, $body = null)
{
	$ch = curl_init($url);
	$hdrs = array();
	foreach ($headers as $k => $v) {
		$hdrs[] = $k . ': ' . $v;
	}
	curl_setopt_array($ch, array(
		CURLOPT_RETURNTRANSFER => true,
		CURLOPT_CUSTOMREQUEST => strtoupper($method),
		CURLOPT_HTTPHEADER => $hdrs,
		CURLOPT_TIMEOUT => 30,
		CURLOPT_CONNECTTIMEOUT => 10,
	));
	if ($body !== null) {
		curl_setopt($ch, CURLOPT_POSTFIELDS, is_string($body) ? $body : json_encode($body));
	}
	$raw = curl_exec($ch);
	$code = (int)curl_getinfo($ch, CURLINFO_HTTP_CODE);
	$err = curl_error($ch);
	curl_close($ch);
	$data = json_decode((string)$raw, true);
	return array(
		'ok' => $code >= 200 && $code < 300 && is_array($data),
		'http' => $code,
		'error' => $err,
		'raw' => (string)$raw,
		'data' => is_array($data) ? $data : array(),
	);
}

function epc_crypto_create_nowpayment(array $params, array $payload)
{
	$apiKey = trim((string)($params['api_key'] ?? ''));
	if ($apiKey === '' || stripos($apiKey, 'DUMMY') !== false) {
		return array('ok' => false, 'message' => 'NOWPayments API key not configured');
	}
	$url = rtrim(epc_crypto_api_base($params), '/') . '/payment';
	$res = epc_crypto_http_json('POST', $url, array(
		'x-api-key' => $apiKey,
		'Content-Type' => 'application/json',
	), $payload);
	if (!$res['ok']) {
		$msg = $res['data']['message'] ?? $res['data']['error'] ?? ('NOWPayments HTTP ' . $res['http']);
		return array('ok' => false, 'message' => (string)$msg, 'response' => $res);
	}
	return array('ok' => true, 'payment' => $res['data']);
}

function epc_crypto_demo_invoice($operationId, $sum, $currency, $coin)
{
	$seed = substr(hash('sha256', 'epc-crypto|' . $operationId . '|' . $coin), 0, 40);
	$addr = 'TDemo' . strtoupper(substr($seed, 0, 30));
	if ($coin === 'btc') {
		$addr = 'bc1q' . substr($seed, 0, 38);
	} elseif ($coin === 'eth' || strpos($coin, 'eth') !== false || strpos($coin, 'bsc') !== false) {
		$addr = '0x' . substr($seed, 0, 40);
	}
	// Demo rate stubs (not market live)
	$rates = array(
		'btc' => 65000,
		'eth' => 3500,
		'ltc' => 85,
		'usdttrc20' => 1,
		'usdtbsc' => 1,
	);
	$rate = isset($rates[$coin]) ? $rates[$coin] : 1;
	$fiat = max(0.01, (float)$sum);
	$payAmount = $rate > 0 ? round($fiat / $rate, $rate >= 100 ? 8 : 2) : $fiat;
	return array(
		'payment_id' => 'demo_' . $operationId . '_' . $coin,
		'pay_address' => $addr,
		'pay_amount' => $payAmount,
		'pay_currency' => $coin,
		'price_amount' => $fiat,
		'price_currency' => strtolower($currency),
		'payment_status' => 'waiting',
		'demo' => true,
	);
}

function epc_crypto_verify_ipn(array $params, $rawBody, array $headers)
{
	$secret = trim((string)($params['ipn_secret'] ?? ''));
	if ($secret === '') {
		return false;
	}
	$sig = '';
	foreach ($headers as $k => $v) {
		if (strtolower((string)$k) === 'x-nowpayments-sig') {
			$sig = (string)$v;
			break;
		}
	}
	if ($sig === '' && !empty($_SERVER['HTTP_X_NOWPAYMENTS_SIG'])) {
		$sig = (string)$_SERVER['HTTP_X_NOWPAYMENTS_SIG'];
	}
	if ($sig === '') {
		return false;
	}
	$calc = hash_hmac('sha512', (string)$rawBody, $secret);
	return hash_equals($calc, $sig);
}
