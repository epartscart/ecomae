<?php
/**
 * UAE FTA / PINT-AE tax invoice (e-invoice) for order print.
 * Expects: $db_link, $user_id, $order_id, $DP_Config
 */
defined('_INTASK_') or die('No access');

$order_id = (int) $order_id;

if (DP_User::isAdmin() || (method_exists('DP_User', 'isBackendGroup') && DP_User::isBackendGroup())) {
	$order_query = $db_link->prepare('SELECT * FROM `shop_orders` WHERE `id` = ?;');
	$order_query->execute(array($order_id));
	$order_record = $order_query->fetch(PDO::FETCH_ASSOC);
} else {
	if ($user_id <= 0) {
		http_response_code(403);
		exit('Not authorized');
	}
	$order_query = $db_link->prepare('SELECT * FROM `shop_orders` WHERE `user_id` = ? AND `id` = ?;');
	$order_query->execute(array($user_id, $order_id));
	$order_record = $order_query->fetch(PDO::FETCH_ASSOC);
}

if ($order_record === false || !is_array($order_record)) {
	http_response_code(404);
	exit('No such order');
}

require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/document_control/epc_document_control_helpers.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_einvoice.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_invoices.php';

// Ensure company profile + default FTA templates are ready for print.
epc_dc_ensure($db_link);
if (function_exists('epc_dc_sync_seller_from_einvoice')) {
	epc_dc_sync_seller_from_einvoice($db_link, true);
}

/**
 * Fill missing buyer display fields so B2C tax invoices still show Bill To.
 */
$epc_enrich_buyer = static function (array $buyer, array $order) use ($db_link): array {
	$name = trim((string) ($buyer['buyer_name'] ?? ''));
	$addr1 = trim((string) ($buyer['buyer_address_line1'] ?? $buyer['address_line1'] ?? ''));
	$city = trim((string) ($buyer['buyer_city'] ?? $buyer['city'] ?? 'Dubai'));
	$emirate = trim((string) ($buyer['buyer_emirate'] ?? $buyer['emirate'] ?? 'Dubai'));
	$country = strtoupper(trim((string) ($buyer['buyer_country_code'] ?? $buyer['country_code'] ?? 'AE')));
	$email = trim((string) ($buyer['buyer_email'] ?? $buyer['email'] ?? ''));
	$phone = trim((string) ($buyer['buyer_phone'] ?? $buyer['phone'] ?? ''));

	$uid = (int) ($order['user_id'] ?? 0);
	if ($uid > 0 && ($email === '' || $phone === '' || $name === '')) {
		try {
			$uq = $db_link->prepare('SELECT `email`, `phone` FROM `users` WHERE `user_id` = ? LIMIT 1');
			$uq->execute(array($uid));
			$u = $uq->fetch(PDO::FETCH_ASSOC) ?: array();
			if ($email === '') {
				$email = trim((string) ($u['email'] ?? ''));
			}
			if ($phone === '') {
				$phone = trim((string) ($u['phone'] ?? ''));
			}
		} catch (Throwable $e) {
			// ignore
		}
	}
	if ($email === '') {
		$email = trim((string) ($order['email_not_auth'] ?? ''));
	}
	if ($phone === '') {
		$phone = trim((string) ($order['phone_not_auth'] ?? ''));
	}
	if ($name === '') {
		$name = $email !== '' ? $email : ('Customer #' . $uid);
	}
	if ($addr1 === '') {
		// Keep city/emirate/country separate — print HTML appends those fields.
		$parts = array_filter(array($email, $phone));
		$addr1 = $parts ? implode(' / ', $parts) : 'Address on customer file';
	}

	$buyer['buyer_name'] = $name;
	$buyer['buyer_address_line1'] = $addr1;
	$buyer['buyer_city'] = $city !== '' ? $city : 'Dubai';
	$buyer['buyer_emirate'] = $emirate !== '' ? $emirate : 'Dubai';
	$buyer['buyer_country_code'] = $country !== '' ? $country : 'AE';
	$buyer['buyer_email'] = $email;
	$buyer['buyer_phone'] = $phone;
	if (empty($buyer['buyer_peppol_endpoint'])) {
		$const = epc_einvoice_constants();
		$buyer['buyer_peppol_endpoint'] = $const['endpoint_not_onboarded'] ?? '0235:NOT_ONBOARDED';
	}
	if (empty($buyer['buyer_electronic_id'])) {
		$buyer['buyer_electronic_id'] = '0235';
	}
	return $buyer;
};

$HTML = '';

// 1) Reuse saved e-invoice for this order when present.
try {
	$st = $db_link->prepare(
		'SELECT `id` FROM `epc_einvoice_documents`
		 WHERE `order_id` = ? AND `active` = 1
		 ORDER BY `id` DESC LIMIT 1'
	);
	$st->execute(array($order_id));
	$existingId = (int) $st->fetchColumn();
	if ($existingId > 0) {
		$saved = epc_einvoice_get_document($db_link, $existingId);
		if (is_array($saved) && !empty($saved['lines'])) {
			$HTML = epc_erp_invoice_print_html($saved);
		}
	}
} catch (Throwable $e) {
	$HTML = '';
}

// 2) Build PINT-AE tax invoice from order (print even if not yet persisted).
if ($HTML === '') {
	try {
		$doc = epc_einvoice_build_from_order($db_link, $order_id);
		$doc['buyer'] = $epc_enrich_buyer(is_array($doc['buyer'] ?? null) ? $doc['buyer'] : array(), $order_record);
		$HTML = epc_erp_invoice_print_html($doc);

		// Persist when validation passes (full UAE e-invoice record + XML).
		try {
			$validation = epc_einvoice_validate_document($doc, $doc['lines'] ?? array(), true);
			if (!empty($validation['ok'])) {
				epc_einvoice_save_document($db_link, $doc, 0);
			}
		} catch (Throwable $e) {
			// Print still succeeds; persistence is best-effort.
		}
	} catch (Throwable $e) {
		$HTML = '';
	}
}

// 3) Fallback: Document Control FTA Tax Invoice template.
if ($HTML === '') {
	try {
		$HTML = epc_dc_render_template($db_link, 'fta_tax_invoice', $order_id);
	} catch (Throwable $e) {
		http_response_code(500);
		exit('Unable to generate UAE tax invoice: ' . htmlspecialchars($e->getMessage(), ENT_QUOTES, 'UTF-8'));
	}
}
