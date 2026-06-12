<?php
/**
 * BOS — VAT refund / tourist tax-free scheme register.
 *
 * Records retail sales made to overseas tourists under a country's VAT refund
 * scheme (UAE Tourist Refund Scheme operated by Planet; other jurisdictions get
 * their own/generic scheme). The scheme that applies is resolved from the
 * tenant's country (tax area) setting — nothing is hard-coded to one tenant.
 *
 * Accounting note: under the UAE TRS the retail sale remains a standard-rated
 * supply in the retailer's VAT return (output VAT is still charged); the refund
 * is settled to the tourist by the scheme operator at the point of exit, not
 * deducted from the retailer's VAT liability. This module is therefore a
 * recording / reconciliation register (against the operator & the FTA), plus the
 * refund computation per the active scheme.
 */

require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_helpers.php';
if (is_file($_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_advanced.php')) {
	require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_advanced.php';
}

/** Tenant country (tax area). Mirrors the compliance module's resolution. */
function epc_bos_vat_refund_company_country(PDO $db): string
{
	if (function_exists('epc_erp_adv_get_setting')) {
		$c = trim((string) epc_erp_adv_get_setting($db, 'erp_company_country', ''));
		if ($c !== '') {
			return strtoupper($c);
		}
	}
	return 'AE';
}

/**
 * Per-country VAT refund-scheme catalog. Config-driven and editable: each entry
 * carries the operator, authority, eligibility and the refund formula.
 *
 * - refund_rate : fraction of the VAT paid that is refundable to the tourist.
 * - fee_per_tag : fixed administrative fee deducted per tax-free tag/transaction.
 * - min_spend   : minimum purchase (incl. VAT) to qualify.
 * - cash_cap    : maximum cash refund (above which refund is card-only).
 * - export_days : days within which goods must be exported.
 *
 * @return array<string,array<string,mixed>>
 */
function epc_bos_vat_refund_schemes(): array
{
	return array(
		'AE' => array(
			'enabled' => true,
			'name' => 'Tourist Refund Scheme (Tax-Free)',
			'operator' => 'Planet',
			'authority' => 'UAE FTA',
			'currency' => 'AED',
			'vat_rate' => 5.0,
			'min_spend' => 250.0,
			'refund_rate' => 0.85,
			'fee_per_tag' => 4.80,
			'cash_cap' => 35000.0,
			'export_days' => 90,
			'note' => 'Overseas tourists (non-residents) only; goods exported within 90 days. Refund = 85% of VAT less AED 4.80 per Tax-Free tag; cash refunds capped at AED 35,000.',
		),
		'SA' => array(
			'enabled' => true,
			'name' => 'Tourist VAT Refund',
			'operator' => 'Authorised operator',
			'authority' => 'ZATCA (Saudi Arabia)',
			'currency' => 'SAR',
			'vat_rate' => 15.0,
			'min_spend' => 0.0,
			'refund_rate' => 0.85,
			'fee_per_tag' => 0.0,
			'cash_cap' => 0.0,
			'export_days' => 90,
			'note' => 'Tourist VAT refund administered via an authorised operator. Thresholds/fees are configurable as the scheme rolls out.',
		),
		'_default' => array(
			'enabled' => false,
			'name' => 'VAT refund scheme',
			'operator' => 'Authorised operator',
			'authority' => 'Tax authority',
			'currency' => '',
			'vat_rate' => 0.0,
			'min_spend' => 0.0,
			'refund_rate' => 1.0,
			'fee_per_tag' => 0.0,
			'cash_cap' => 0.0,
			'export_days' => 90,
			'note' => 'No country-specific tourist VAT refund scheme is configured for this tax area. You can still record refund transactions; amounts use a 100% refund with no fee until a scheme is configured.',
		),
	);
}

/** Resolve the active scheme for a country (falls back to the default). */
function epc_bos_vat_refund_scheme_for(string $country): array
{
	$schemes = epc_bos_vat_refund_schemes();
	$country = strtoupper($country);
	$out = isset($schemes[$country]) ? $schemes[$country] : $schemes['_default'];
	$out['country'] = isset($schemes[$country]) ? $country : '';
	return $out;
}

/** Active scheme for the current tenant. */
function epc_bos_vat_refund_active_scheme(PDO $db): array
{
	return epc_bos_vat_refund_scheme_for(epc_bos_vat_refund_company_country($db));
}

/**
 * Compute the refund split for a given VAT amount under a scheme.
 *
 * @return array{refund:float,fee:float,retained:float}
 */
function epc_bos_vat_refund_calc(array $scheme, float $vatAmount): array
{
	$vatAmount = max(0.0, $vatAmount);
	$rate = isset($scheme['refund_rate']) ? (float) $scheme['refund_rate'] : 1.0;
	$fee = isset($scheme['fee_per_tag']) ? (float) $scheme['fee_per_tag'] : 0.0;
	$refund = ($vatAmount * $rate) - $fee;
	if ($refund < 0) {
		$refund = 0.0;
	}
	$refund = round($refund, 2);
	$retained = round($vatAmount - $refund, 2);
	return array('refund' => $refund, 'fee' => round($fee, 2), 'retained' => $retained);
}

function epc_bos_vat_refund_ensure_schema(PDO $db): void
{
	$db->exec("CREATE TABLE IF NOT EXISTS `epc_bos_vat_refunds` (
		`id` int(11) NOT NULL AUTO_INCREMENT,
		`tag_ref` varchar(64) DEFAULT NULL,
		`country` varchar(4) NOT NULL DEFAULT '',
		`scheme` varchar(80) DEFAULT NULL,
		`operator` varchar(80) DEFAULT NULL,
		`invoice_ref` varchar(120) DEFAULT NULL,
		`customer_name` varchar(160) DEFAULT NULL,
		`passport_no` varchar(64) DEFAULT NULL,
		`nationality` varchar(80) DEFAULT NULL,
		`sale_amount` decimal(16,2) NOT NULL DEFAULT 0.00,
		`vat_amount` decimal(16,2) NOT NULL DEFAULT 0.00,
		`refund_amount` decimal(16,2) NOT NULL DEFAULT 0.00,
		`fee_amount` decimal(16,2) NOT NULL DEFAULT 0.00,
		`retained_amount` decimal(16,2) NOT NULL DEFAULT 0.00,
		`status` enum('recorded','validated','exported','refunded','void') NOT NULL DEFAULT 'recorded',
		`sale_date` int(11) NOT NULL DEFAULT 0,
		`notes` text,
		`admin_id` int(11) NOT NULL DEFAULT 0,
		`time` int(11) NOT NULL DEFAULT 0,
		PRIMARY KEY (`id`),
		KEY `x_country` (`country`),
		KEY `x_status` (`status`),
		KEY `x_saledate` (`sale_date`)
	) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='BOS tourist/VAT refund register';");
}

/**
 * Record (or update) a tax-free / tourist VAT refund transaction.
 *
 * @param array<string,mixed> $post
 * @return array<string,mixed>
 */
function epc_bos_vat_refund_save(PDO $db, array $post): array
{
	epc_bos_vat_refund_ensure_schema($db);
	$scheme = epc_bos_vat_refund_active_scheme($db);
	$country = (string) ($scheme['country'] !== '' ? $scheme['country'] : epc_bos_vat_refund_company_country($db));

	$sale = (float) ($post['sale_amount'] ?? 0);
	$vat = isset($post['vat_amount']) && $post['vat_amount'] !== '' ? (float) $post['vat_amount'] : 0.0;
	if ($vat <= 0 && (float) ($scheme['vat_rate'] ?? 0) > 0 && $sale > 0) {
		$vat = round($sale * ((float) $scheme['vat_rate'] / 100.0), 2);
	}
	$calc = epc_bos_vat_refund_calc($scheme, $vat);

	$saleDate = !empty($post['sale_date']) ? (int) strtotime((string) $post['sale_date'] . ' 00:00:00') : time();
	$adminId = class_exists('DP_User') ? (int) DP_User::getAdminId() : 0;
	$now = time();
	$id = (int) ($post['id'] ?? 0);

	$fields = array(
		'tag_ref' => trim((string) ($post['tag_ref'] ?? '')),
		'country' => $country,
		'scheme' => (string) $scheme['name'],
		'operator' => (string) $scheme['operator'],
		'invoice_ref' => trim((string) ($post['invoice_ref'] ?? '')),
		'customer_name' => trim((string) ($post['customer_name'] ?? '')),
		'passport_no' => trim((string) ($post['passport_no'] ?? '')),
		'nationality' => trim((string) ($post['nationality'] ?? '')),
		'sale_amount' => round($sale, 2),
		'vat_amount' => round($vat, 2),
		'refund_amount' => $calc['refund'],
		'fee_amount' => $calc['fee'],
		'retained_amount' => $calc['retained'],
		'status' => in_array((string) ($post['status'] ?? ''), array('recorded', 'validated', 'exported', 'refunded', 'void'), true) ? (string) $post['status'] : 'recorded',
		'sale_date' => $saleDate,
		'notes' => trim((string) ($post['notes'] ?? '')),
	);

	if ($id > 0) {
		$set = array();
		$vals = array();
		foreach ($fields as $k => $v) {
			$set[] = "`$k` = ?";
			$vals[] = $v;
		}
		$vals[] = $id;
		$st = $db->prepare('UPDATE `epc_bos_vat_refunds` SET ' . implode(', ', $set) . ' WHERE `id` = ?');
		$st->execute($vals);
	} else {
		$cols = array_keys($fields);
		$cols[] = 'admin_id';
		$cols[] = 'time';
		$ph = implode(', ', array_fill(0, count($cols), '?'));
		$vals = array_values($fields);
		$vals[] = $adminId;
		$vals[] = $now;
		$st = $db->prepare('INSERT INTO `epc_bos_vat_refunds` (`' . implode('`, `', $cols) . '`) VALUES (' . $ph . ')');
		$st->execute($vals);
		$id = (int) $db->lastInsertId();
	}

	return array('id' => $id, 'refund' => $calc['refund'], 'fee' => $calc['fee'], 'retained' => $calc['retained'], 'vat' => round($vat, 2));
}

/** Update only the status of a refund record. */
function epc_bos_vat_refund_set_status(PDO $db, int $id, string $status): bool
{
	epc_bos_vat_refund_ensure_schema($db);
	if (!in_array($status, array('recorded', 'validated', 'exported', 'refunded', 'void'), true)) {
		return false;
	}
	$st = $db->prepare('UPDATE `epc_bos_vat_refunds` SET `status` = ? WHERE `id` = ?');
	return $st->execute(array($status, $id));
}

/**
 * List refund records within a date range (by sale date).
 *
 * @return array<int,array<string,mixed>>
 */
function epc_bos_vat_refund_list(PDO $db, int $from = 0, int $to = 0, int $limit = 500): array
{
	epc_bos_vat_refund_ensure_schema($db);
	$where = array();
	$args = array();
	if ($from > 0) { $where[] = '`sale_date` >= ?'; $args[] = $from; }
	if ($to > 0) { $where[] = '`sale_date` <= ?'; $args[] = $to; }
	$sql = 'SELECT * FROM `epc_bos_vat_refunds`';
	if ($where) { $sql .= ' WHERE ' . implode(' AND ', $where); }
	$sql .= ' ORDER BY `sale_date` DESC, `id` DESC LIMIT ' . (int) $limit;
	$st = $db->prepare($sql);
	$st->execute($args);
	return $st->fetchAll();
}

/**
 * Totals for the register within a date range.
 *
 * @return array<string,float|int>
 */
function epc_bos_vat_refund_summary(PDO $db, int $from = 0, int $to = 0): array
{
	epc_bos_vat_refund_ensure_schema($db);
	$where = array("`status` <> 'void'");
	$args = array();
	if ($from > 0) { $where[] = '`sale_date` >= ?'; $args[] = $from; }
	if ($to > 0) { $where[] = '`sale_date` <= ?'; $args[] = $to; }
	$sql = 'SELECT COUNT(*) c, COALESCE(SUM(`sale_amount`),0) s, COALESCE(SUM(`vat_amount`),0) v, COALESCE(SUM(`refund_amount`),0) r, COALESCE(SUM(`fee_amount`),0) f, COALESCE(SUM(`retained_amount`),0) t FROM `epc_bos_vat_refunds` WHERE ' . implode(' AND ', $where);
	$st = $db->prepare($sql);
	$st->execute($args);
	$row = $st->fetch();
	return array(
		'count' => (int) ($row['c'] ?? 0),
		'sales' => (float) ($row['s'] ?? 0),
		'vat' => (float) ($row['v'] ?? 0),
		'refund' => (float) ($row['r'] ?? 0),
		'fee' => (float) ($row['f'] ?? 0),
		'retained' => (float) ($row['t'] ?? 0),
	);
}
