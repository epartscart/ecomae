<?php
/**
 * Product Lifecycle Management (PLM) — engineering change management,
 * product versioning, design documents, compliance certifications,
 * and product phase tracking (concept → launch → mature → sunset).
 *
 * Closes the SAP PLM partial gap (PIM covers attributes; PLM covers
 * the full lifecycle from design through end-of-life).
 *
 * Tables: epc_erp_plm_products, epc_erp_plm_change_orders,
 *         epc_erp_plm_documents, epc_erp_plm_certifications
 */
defined('_ASTEXE_') or die('No access');

function epc_plm_ensure_schema(PDO $db): void
{
	$db->exec('CREATE TABLE IF NOT EXISTS `epc_erp_plm_products` (
		`id`            INT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
		`company_id`    INT UNSIGNED NOT NULL DEFAULT 0,
		`item_id`       INT UNSIGNED NOT NULL DEFAULT 0,
		`sku`           VARCHAR(40) NOT NULL DEFAULT "",
		`version`       VARCHAR(20) NOT NULL DEFAULT "1.0",
		`phase`         ENUM("concept","design","prototype","pilot","launch","growth","mature","decline","sunset","eol") NOT NULL DEFAULT "concept",
		`phase_date`    DATE DEFAULT NULL,
		`launch_date`   DATE DEFAULT NULL,
		`eol_date`      DATE DEFAULT NULL,
		`design_owner`  VARCHAR(100) NOT NULL DEFAULT "",
		`product_manager` VARCHAR(100) NOT NULL DEFAULT "",
		`notes`         TEXT,
		`created_at`    INT UNSIGNED NOT NULL DEFAULT 0,
		`updated_at`    INT UNSIGNED NOT NULL DEFAULT 0,
		UNIQUE KEY `uk_item` (`company_id`,`item_id`),
		INDEX `idx_phase` (`phase`),
		INDEX `idx_sku` (`sku`)
	) ENGINE=InnoDB DEFAULT CHARSET=utf8');

	$db->exec('CREATE TABLE IF NOT EXISTS `epc_erp_plm_change_orders` (
		`id`            INT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
		`company_id`    INT UNSIGNED NOT NULL DEFAULT 0,
		`eco_number`    VARCHAR(30) NOT NULL DEFAULT "",
		`plm_product_id` INT UNSIGNED NOT NULL DEFAULT 0,
		`type`          ENUM("design","bom","process","specification","packaging","regulatory","other") NOT NULL DEFAULT "design",
		`title`         VARCHAR(200) NOT NULL DEFAULT "",
		`description`   TEXT,
		`reason`        TEXT,
		`old_version`   VARCHAR(20) NOT NULL DEFAULT "",
		`new_version`   VARCHAR(20) NOT NULL DEFAULT "",
		`priority`      ENUM("low","medium","high","critical") NOT NULL DEFAULT "medium",
		`requested_by`  VARCHAR(100) NOT NULL DEFAULT "",
		`approved_by`   VARCHAR(100) NOT NULL DEFAULT "",
		`status`        ENUM("draft","submitted","review","approved","implemented","rejected","cancelled") NOT NULL DEFAULT "draft",
		`target_date`   DATE DEFAULT NULL,
		`implemented_date` DATE DEFAULT NULL,
		`created_at`    INT UNSIGNED NOT NULL DEFAULT 0,
		`updated_at`    INT UNSIGNED NOT NULL DEFAULT 0,
		UNIQUE KEY `uk_eco` (`company_id`,`eco_number`),
		INDEX `idx_product` (`plm_product_id`),
		INDEX `idx_status` (`status`)
	) ENGINE=InnoDB DEFAULT CHARSET=utf8');

	$db->exec('CREATE TABLE IF NOT EXISTS `epc_erp_plm_documents` (
		`id`            INT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
		`company_id`    INT UNSIGNED NOT NULL DEFAULT 0,
		`plm_product_id` INT UNSIGNED NOT NULL DEFAULT 0,
		`eco_id`        INT UNSIGNED DEFAULT NULL,
		`type`          ENUM("drawing","specification","test_report","manual","datasheet","photo","cad","other") NOT NULL DEFAULT "specification",
		`title`         VARCHAR(200) NOT NULL DEFAULT "",
		`file_path`     VARCHAR(500) NOT NULL DEFAULT "",
		`file_size`     INT UNSIGNED NOT NULL DEFAULT 0,
		`version`       VARCHAR(20) NOT NULL DEFAULT "1.0",
		`uploaded_by`   VARCHAR(100) NOT NULL DEFAULT "",
		`status`        ENUM("draft","active","superseded","archived") NOT NULL DEFAULT "active",
		`created_at`    INT UNSIGNED NOT NULL DEFAULT 0,
		INDEX `idx_product` (`plm_product_id`),
		INDEX `idx_eco` (`eco_id`),
		INDEX `idx_type` (`type`)
	) ENGINE=InnoDB DEFAULT CHARSET=utf8');

	$db->exec('CREATE TABLE IF NOT EXISTS `epc_erp_plm_certifications` (
		`id`            INT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
		`company_id`    INT UNSIGNED NOT NULL DEFAULT 0,
		`plm_product_id` INT UNSIGNED NOT NULL DEFAULT 0,
		`standard`      VARCHAR(100) NOT NULL DEFAULT "",
		`cert_number`   VARCHAR(100) NOT NULL DEFAULT "",
		`issuing_body`  VARCHAR(200) NOT NULL DEFAULT "",
		`issue_date`    DATE DEFAULT NULL,
		`expiry_date`   DATE DEFAULT NULL,
		`scope`         TEXT,
		`status`        ENUM("active","expired","suspended","pending","revoked") NOT NULL DEFAULT "pending",
		`created_at`    INT UNSIGNED NOT NULL DEFAULT 0,
		INDEX `idx_product` (`plm_product_id`),
		INDEX `idx_expiry` (`expiry_date`),
		INDEX `idx_status` (`status`)
	) ENGINE=InnoDB DEFAULT CHARSET=utf8');
}

function epc_plm_product_save(PDO $db, array $data, int $id = 0): int
{
	$now = time();
	if ($id > 0) {
		$db->prepare('UPDATE `epc_erp_plm_products` SET `item_id`=?,`sku`=?,`version`=?,`phase`=?,`phase_date`=?,`launch_date`=?,`eol_date`=?,`design_owner`=?,`product_manager`=?,`notes`=?,`updated_at`=? WHERE `id`=?')
			->execute(array((int) ($data['item_id'] ?? 0), $data['sku'] ?? '', $data['version'] ?? '1.0', $data['phase'] ?? 'concept', $data['phase_date'] ?? date('Y-m-d'), $data['launch_date'] ?? null, $data['eol_date'] ?? null, $data['design_owner'] ?? '', $data['product_manager'] ?? '', $data['notes'] ?? '', $now, $id));
		return $id;
	}
	$db->prepare('INSERT INTO `epc_erp_plm_products` (`company_id`,`item_id`,`sku`,`version`,`phase`,`phase_date`,`launch_date`,`design_owner`,`product_manager`,`notes`,`created_at`,`updated_at`) VALUES (?,?,?,?,?,?,?,?,?,?,?,?)')
		->execute(array((int) ($data['company_id'] ?? 0), (int) ($data['item_id'] ?? 0), $data['sku'] ?? '', $data['version'] ?? '1.0', $data['phase'] ?? 'concept', date('Y-m-d'), $data['launch_date'] ?? null, $data['design_owner'] ?? '', $data['product_manager'] ?? '', $data['notes'] ?? '', $now, $now));
	return (int) $db->lastInsertId();
}

function epc_plm_product_list(PDO $db, int $companyId = 0, string $phase = ''): array
{
	$sql = 'SELECT p.*, (SELECT COUNT(*) FROM `epc_erp_plm_change_orders` c WHERE c.`plm_product_id` = p.`id` AND c.`status` NOT IN ("implemented","rejected","cancelled")) AS pending_ecos, (SELECT COUNT(*) FROM `epc_erp_plm_certifications` ct WHERE ct.`plm_product_id` = p.`id` AND ct.`status` = "active") AS active_certs FROM `epc_erp_plm_products` p WHERE 1=1';
	if ($companyId > 0) {
		$sql .= ' AND p.`company_id` = ' . (int) $companyId;
	}
	if ($phase !== '') {
		$sql .= ' AND p.`phase` = "' . preg_replace('/[^a-z_]/', '', $phase) . '"';
	}
	$sql .= ' ORDER BY p.`updated_at` DESC';
	return $db->query($sql)->fetchAll(PDO::FETCH_ASSOC);
}

function epc_plm_phase_advance(PDO $db, int $productId, string $newPhase): bool
{
	$phases = array('concept', 'design', 'prototype', 'pilot', 'launch', 'growth', 'mature', 'decline', 'sunset', 'eol');
	if (!in_array($newPhase, $phases, true)) {
		return false;
	}
	$st = $db->prepare('UPDATE `epc_erp_plm_products` SET `phase` = ?, `phase_date` = ?, `updated_at` = ? WHERE `id` = ?');
	$st->execute(array($newPhase, date('Y-m-d'), time(), $productId));
	return $st->rowCount() > 0;
}

function epc_plm_eco_save(PDO $db, array $data, int $id = 0): int
{
	$now = time();
	if ($id > 0) {
		$db->prepare('UPDATE `epc_erp_plm_change_orders` SET `type`=?,`title`=?,`description`=?,`reason`=?,`new_version`=?,`priority`=?,`approved_by`=?,`status`=?,`target_date`=?,`implemented_date`=?,`updated_at`=? WHERE `id`=?')
			->execute(array($data['type'] ?? 'design', $data['title'] ?? '', $data['description'] ?? '', $data['reason'] ?? '', $data['new_version'] ?? '', $data['priority'] ?? 'medium', $data['approved_by'] ?? '', $data['status'] ?? 'draft', $data['target_date'] ?? null, $data['implemented_date'] ?? null, $now, $id));
		return $id;
	}
	$db->prepare('INSERT INTO `epc_erp_plm_change_orders` (`company_id`,`eco_number`,`plm_product_id`,`type`,`title`,`description`,`reason`,`old_version`,`new_version`,`priority`,`requested_by`,`status`,`target_date`,`created_at`,`updated_at`) VALUES (?,?,?,?,?,?,?,?,?,?,?,?,?,?,?)')
		->execute(array((int) ($data['company_id'] ?? 0), $data['eco_number'] ?? '', (int) ($data['plm_product_id'] ?? 0), $data['type'] ?? 'design', $data['title'] ?? '', $data['description'] ?? '', $data['reason'] ?? '', $data['old_version'] ?? '', $data['new_version'] ?? '', $data['priority'] ?? 'medium', $data['requested_by'] ?? '', 'draft', $data['target_date'] ?? null, $now, $now));
	return (int) $db->lastInsertId();
}

function epc_plm_eco_implement(PDO $db, int $ecoId): void
{
	$eco = $db->prepare('SELECT * FROM `epc_erp_plm_change_orders` WHERE `id` = ?');
	$eco->execute(array($ecoId));
	$row = $eco->fetch(PDO::FETCH_ASSOC);
	if (!$row) {
		return;
	}
	$db->prepare('UPDATE `epc_erp_plm_change_orders` SET `status` = "implemented", `implemented_date` = ?, `updated_at` = ? WHERE `id` = ?')
		->execute(array(date('Y-m-d'), time(), $ecoId));
	if ($row['new_version'] !== '' && (int) $row['plm_product_id'] > 0) {
		$db->prepare('UPDATE `epc_erp_plm_products` SET `version` = ?, `updated_at` = ? WHERE `id` = ?')
			->execute(array($row['new_version'], time(), (int) $row['plm_product_id']));
	}
}

function epc_plm_document_save(PDO $db, array $data): int
{
	$db->prepare('INSERT INTO `epc_erp_plm_documents` (`company_id`,`plm_product_id`,`eco_id`,`type`,`title`,`file_path`,`file_size`,`version`,`uploaded_by`,`status`,`created_at`) VALUES (?,?,?,?,?,?,?,?,?,?,?)')
		->execute(array((int) ($data['company_id'] ?? 0), (int) ($data['plm_product_id'] ?? 0), $data['eco_id'] ?? null, $data['type'] ?? 'specification', $data['title'] ?? '', $data['file_path'] ?? '', (int) ($data['file_size'] ?? 0), $data['version'] ?? '1.0', $data['uploaded_by'] ?? '', 'active', time()));
	return (int) $db->lastInsertId();
}

function epc_plm_cert_save(PDO $db, array $data): int
{
	$db->prepare('INSERT INTO `epc_erp_plm_certifications` (`company_id`,`plm_product_id`,`standard`,`cert_number`,`issuing_body`,`issue_date`,`expiry_date`,`scope`,`status`,`created_at`) VALUES (?,?,?,?,?,?,?,?,?,?)')
		->execute(array((int) ($data['company_id'] ?? 0), (int) ($data['plm_product_id'] ?? 0), $data['standard'] ?? '', $data['cert_number'] ?? '', $data['issuing_body'] ?? '', $data['issue_date'] ?? null, $data['expiry_date'] ?? null, $data['scope'] ?? '', $data['status'] ?? 'pending', time()));
	return (int) $db->lastInsertId();
}

function epc_plm_dashboard(PDO $db, int $companyId): array
{
	$phases = $db->prepare('SELECT `phase`, COUNT(*) AS cnt FROM `epc_erp_plm_products` WHERE `company_id` = ? GROUP BY `phase` ORDER BY FIELD(`phase`,"concept","design","prototype","pilot","launch","growth","mature","decline","sunset","eol")');
	$phases->execute(array($companyId));
	$phaseData = $phases->fetchAll(PDO::FETCH_ASSOC);

	$ecos = $db->prepare('SELECT COUNT(*) AS pending_ecos FROM `epc_erp_plm_change_orders` WHERE `company_id` = ? AND `status` NOT IN ("implemented","rejected","cancelled")');
	$ecos->execute(array($companyId));
	$ecoRow = $ecos->fetch(PDO::FETCH_ASSOC);

	$certs = $db->prepare('SELECT COUNT(*) AS expiring_certs FROM `epc_erp_plm_certifications` WHERE `company_id` = ? AND `status` = "active" AND `expiry_date` BETWEEN CURDATE() AND DATE_ADD(CURDATE(), INTERVAL 60 DAY)');
	$certs->execute(array($companyId));
	$certRow = $certs->fetch(PDO::FETCH_ASSOC);

	return array(
		'phases' => $phaseData,
		'pending_ecos' => (int) ($ecoRow['pending_ecos'] ?? 0),
		'expiring_certs' => (int) ($certRow['expiring_certs'] ?? 0),
	);
}
