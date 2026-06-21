<?php
/**
 * Effective minimum order quantity by customer price profile.
 */
defined('_ASTEXE_') or die('No access');

function epc_moq_ensure_profile_column(PDO $db): void
{
	static $done = false;
	if ($done) {
		return;
	}
	$done = true;
	try {
		$q = $db->query("SHOW COLUMNS FROM `epc_price_profiles` LIKE 'moq_multiplier';");
		if (!$q || !$q->fetch()) {
			$db->exec('ALTER TABLE `epc_price_profiles` ADD `moq_multiplier` DECIMAL(6,2) NOT NULL DEFAULT 1.00 AFTER `margin_percent`;');
		}
	} catch (Throwable $e) {
	}
}

function epc_moq_profile_multiplier(PDO $db, int $user_id): float
{
	if ($user_id <= 0) {
		return 1.0;
	}
	epc_moq_ensure_profile_column($db);
	try {
		$st = $db->prepare(
			'SELECT p.`moq_multiplier`, p.`code` FROM `users_groups_bind` b '
			. 'INNER JOIN `epc_price_profiles` p ON p.`group_id` = b.`group_id` '
			. 'WHERE b.`user_id` = ? ORDER BY b.`record_id` DESC LIMIT 1;'
		);
		$st->execute(array($user_id));
		$row = $st->fetch(PDO::FETCH_ASSOC);
		if (!$row) {
			return 1.0;
		}
		$mult = (float)($row['moq_multiplier'] ?? 1.0);
		if ($mult <= 0) {
			$mult = 1.0;
		}
		$code = strtolower(trim((string)($row['code'] ?? '')));
		if ($mult === 1.0 && $code === 'retail') {
			return 1.0;
		}
		if ($mult === 1.0 && $code === 'wholesale') {
			return 1.0;
		}
		return $mult;
	} catch (Throwable $e) {
		return 1.0;
	}
}

function epc_moq_effective(PDO $db, int $user_id, int $base_moq): int
{
	$base_moq = (int)$base_moq;
	if ($base_moq <= 0) {
		$base_moq = 1;
	}
	$mult = epc_moq_profile_multiplier($db, $user_id);
	if ($mult === 1.0) {
		return $base_moq;
	}
	$effective = (int)ceil($base_moq * $mult);
	return max(1, $effective);
}
