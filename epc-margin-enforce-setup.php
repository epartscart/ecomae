<?php
/**
 * Enforce guest/retail 40% markup + sync storage-map markups to price profiles.
 * GET ?token=epartscart-deploy-2026
 */
declare(strict_types=1);

if (!isset($_GET['token']) || $_GET['token'] !== 'epartscart-deploy-2026') {
	exit('Forbidden');
}
header('Content-Type: text/plain; charset=utf-8');
ini_set('display_errors', '1');

require_once __DIR__ . '/config.php';
$cfg = new DP_Config();
$pdo = new PDO(
	'mysql:host=' . $cfg->host . ';dbname=' . $cfg->db . ';charset=utf8',
	$cfg->user,
	$cfg->password,
	array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION)
);

function epc_me_scalar(PDO $pdo, string $sql, array $args = array())
{
	$st = $pdo->prepare($sql);
	$st->execute($args);
	return $st->fetchColumn();
}

// 1) Settings + profile floors
$pdo->prepare(
	"INSERT INTO `epc_price_settings` (`setting_key`, `setting_value`) VALUES ('guest_margin_percent', '40.00')
	 ON DUPLICATE KEY UPDATE `setting_value` = IF(CAST(`setting_value` AS DECIMAL(10,2)) > 0, `setting_value`, '40.00')"
)->execute();
echo "guest_margin_percent=" . epc_me_scalar($pdo, "SELECT `setting_value` FROM `epc_price_settings` WHERE `setting_key`='guest_margin_percent'") . "\n";

$updRetail = $pdo->prepare(
	"UPDATE `epc_price_profiles` SET `margin_percent` = 40.00
	 WHERE `code` = 'retail' AND (IFNULL(`margin_percent`, 0) <= 0)"
);
$updRetail->execute();
echo "retail_floor_updated=" . $updRetail->rowCount() . "\n";

$profiles = $pdo->query("SELECT `code`, `group_id`, `margin_percent` FROM `epc_price_profiles`")->fetchAll(PDO::FETCH_ASSOC);
$guestGid = (int) epc_me_scalar($pdo, "SELECT `id` FROM `groups` WHERE `for_guests` = 1 ORDER BY `id` ASC LIMIT 1");
echo "guest_group_id={$guestGid}\n";

$targetByGroup = array();
if ($guestGid > 0) {
	$targetByGroup[$guestGid] = 40.0;
}
foreach ($profiles as $p) {
	$code = strtolower(trim((string) ($p['code'] ?? '')));
	$gid = (int) ($p['group_id'] ?? 0);
	$margin = (float) ($p['margin_percent'] ?? 0);
	if ($gid <= 0) {
		continue;
	}
	if ($code === 'retail' && $margin <= 0) {
		$margin = 40.0;
	}
	// Guest map uses guest margin; profile groups use profile overall %.
	$targetByGroup[$gid] = $margin > 0 ? $margin : 0.0;
	echo "profile {$code} group={$gid} margin={$margin}\n";
}

// 2) Ensure map rows exist for every office × storage × target group, then set markup.
$offices = $pdo->query("SELECT `id` FROM `shop_offices`")->fetchAll(PDO::FETCH_COLUMN);
$storages = $pdo->query("SELECT `id` FROM `shop_storages`")->fetchAll(PDO::FETCH_COLUMN);
$ins = $pdo->prepare(
	"INSERT INTO `shop_offices_storages_map`
		(`office_id`, `storage_id`, `group_id`, `min_point`, `max_point`, `markup`, `additional_time`)
	 SELECT ?, ?, ?, 0, 999999999, ?, 0
	 FROM DUAL
	 WHERE NOT EXISTS (
		SELECT 1 FROM `shop_offices_storages_map`
		WHERE `office_id` = ? AND `storage_id` = ? AND `group_id` = ?
		LIMIT 1
	 )"
);
$upd = $pdo->prepare(
	"UPDATE `shop_offices_storages_map` SET `markup` = ?
	 WHERE `group_id` = ?"
);

$created = 0;
foreach ($targetByGroup as $gid => $margin) {
	$gid = (int) $gid;
	$margin = (float) $margin;
	foreach ($offices as $oid) {
		foreach ($storages as $sid) {
			$ins->execute(array((int) $oid, (int) $sid, $gid, $margin, (int) $oid, (int) $sid, $gid));
			$created += $ins->rowCount();
		}
	}
	$upd->execute(array($margin, $gid));
	echo "map_synced group={$gid} markup={$margin} rows_updated=" . $upd->rowCount() . "\n";
}
echo "map_rows_created={$created}\n";

// 3) Sanity sample
foreach ($targetByGroup as $gid => $margin) {
	$row = $pdo->query(
		"SELECT MIN(`markup`) min_m, MAX(`markup`) max_m, COUNT(*) c
		 FROM `shop_offices_storages_map` WHERE `group_id` = " . (int) $gid
	)->fetch(PDO::FETCH_ASSOC);
	echo "verify group={$gid} " . json_encode($row) . "\n";
}

echo "Done\n";
