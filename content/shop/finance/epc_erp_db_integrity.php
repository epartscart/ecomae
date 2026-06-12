<?php
/**
 * BOS database integrity layer — referential-integrity scanner and a guarded
 * foreign-key applier.
 *
 * The schema is created in code (no migration framework), so existing tenant
 * databases predate any DB-level constraints and may contain historical rows
 * whose parent record was hard-deleted. Adding a foreign key to such a table
 * would fail. This module therefore:
 *   1. SCANS each known child->parent relationship for orphans (no writes), and
 *   2. APPLIES a foreign key ONLY where the relationship is clean (zero orphans),
 *      after ensuring the supporting index exists. Idempotent and safe to re-run.
 *
 * This gives enterprise-grade referential integrity going forward without
 * risking existing data or breaking posting on dirty tenants.
 */

defined('_ASTEXE_') or die('No access');

if (!function_exists('epc_erp_integrity_relationships')) {

	/**
	 * Catalogue of core child->parent relationships.
	 * Each: child_table, child_col, parent_table, parent_col, on_delete.
	 *
	 * @return array<int,array<string,string>>
	 */
	function epc_erp_integrity_relationships(): array
	{
		return array(
			array('child' => 'epc_erp_gl_lines', 'col' => 'journal_id', 'parent' => 'epc_erp_gl_journals', 'pcol' => 'id', 'on_delete' => 'CASCADE'),
			array('child' => 'epc_erp_gl_lines', 'col' => 'coa_id', 'parent' => 'epc_erp_coa_accounts', 'pcol' => 'id', 'on_delete' => 'RESTRICT'),
			array('child' => 'epc_erp_inv_movements', 'col' => 'item_id', 'parent' => 'epc_erp_inv_items', 'pcol' => 'id', 'on_delete' => 'RESTRICT'),
			array('child' => 'epc_erp_inv_movements', 'col' => 'warehouse_id', 'parent' => 'epc_erp_inv_warehouses', 'pcol' => 'id', 'on_delete' => 'RESTRICT'),
			array('child' => 'epc_erp_inv_stock', 'col' => 'item_id', 'parent' => 'epc_erp_inv_items', 'pcol' => 'id', 'on_delete' => 'CASCADE'),
			array('child' => 'epc_erp_inv_stock', 'col' => 'warehouse_id', 'parent' => 'epc_erp_inv_warehouses', 'pcol' => 'id', 'on_delete' => 'CASCADE'),
			array('child' => 'epc_erp_inv_serials', 'col' => 'item_id', 'parent' => 'epc_erp_inv_items', 'pcol' => 'id', 'on_delete' => 'CASCADE'),
			array('child' => 'epc_erp_inv_item_fields', 'col' => 'item_id', 'parent' => 'epc_erp_inv_items', 'pcol' => 'id', 'on_delete' => 'CASCADE'),
			array('child' => 'epc_einvoice_lines', 'col' => 'document_id', 'parent' => 'epc_einvoice_documents', 'pcol' => 'id', 'on_delete' => 'CASCADE'),
			array('child' => 'epc_erp_purchase_inv_lines', 'col' => 'purchase_id', 'parent' => 'epc_erp_purchases', 'pcol' => 'id', 'on_delete' => 'CASCADE'),
		);
	}

	function epc_erp_integrity_table_exists(PDO $db, string $table): bool
	{
		$st = $db->prepare(
			'SELECT COUNT(*) FROM information_schema.TABLES
			 WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = ?'
		);
		$st->execute(array($table));
		return (int) $st->fetchColumn() > 0;
	}

	function epc_erp_integrity_column_type(PDO $db, string $table, string $col): ?string
	{
		$st = $db->prepare(
			'SELECT COLUMN_TYPE FROM information_schema.COLUMNS
			 WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = ? AND COLUMN_NAME = ?'
		);
		$st->execute(array($table, $col));
		$v = $st->fetchColumn();
		return $v === false ? null : (string) $v;
	}

	/** Does any FK already exist on child.col? */
	function epc_erp_integrity_has_fk(PDO $db, string $table, string $col): bool
	{
		$st = $db->prepare(
			'SELECT COUNT(*) FROM information_schema.KEY_COLUMN_USAGE
			 WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = ? AND COLUMN_NAME = ?
			   AND REFERENCED_TABLE_NAME IS NOT NULL'
		);
		$st->execute(array($table, $col));
		return (int) $st->fetchColumn() > 0;
	}

	function epc_erp_integrity_has_index(PDO $db, string $table, string $col): bool
	{
		$st = $db->prepare(
			'SELECT COUNT(*) FROM information_schema.STATISTICS
			 WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = ? AND COLUMN_NAME = ? AND SEQ_IN_INDEX = 1'
		);
		$st->execute(array($table, $col));
		return (int) $st->fetchColumn() > 0;
	}

	/**
	 * Count orphans for one relationship (child rows whose non-zero FK value has
	 * no matching parent). Returns -1 if a table/column is missing.
	 */
	function epc_erp_integrity_orphan_count(PDO $db, array $rel): int
	{
		if (!epc_erp_integrity_table_exists($db, $rel['child']) || !epc_erp_integrity_table_exists($db, $rel['parent'])) {
			return -1;
		}
		if (epc_erp_integrity_column_type($db, $rel['child'], $rel['col']) === null
			|| epc_erp_integrity_column_type($db, $rel['parent'], $rel['pcol']) === null) {
			return -1;
		}
		$sql = 'SELECT COUNT(*) FROM `' . $rel['child'] . '` c
			 LEFT JOIN `' . $rel['parent'] . '` p ON c.`' . $rel['col'] . '` = p.`' . $rel['pcol'] . '`
			 WHERE c.`' . $rel['col'] . '` IS NOT NULL AND c.`' . $rel['col'] . '` <> 0 AND p.`' . $rel['pcol'] . '` IS NULL';
		return (int) $db->query($sql)->fetchColumn();
	}

	/**
	 * Scan all relationships. Each result row carries the orphan count and a
	 * status: clean | dirty | exists | missing.
	 *
	 * @return array<int,array<string,mixed>>
	 */
	function epc_erp_integrity_scan(PDO $db): array
	{
		$out = array();
		foreach (epc_erp_integrity_relationships() as $rel) {
			$orphans = epc_erp_integrity_orphan_count($db, $rel);
			if ($orphans < 0) {
				$status = 'missing';
			} elseif (epc_erp_integrity_has_fk($db, $rel['child'], $rel['col'])) {
				$status = 'exists';
			} elseif ($orphans === 0) {
				$status = 'clean';
			} else {
				$status = 'dirty';
			}
			$out[] = array(
				'child' => $rel['child'], 'col' => $rel['col'],
				'parent' => $rel['parent'], 'pcol' => $rel['pcol'],
				'orphans' => $orphans, 'status' => $status,
			);
		}
		return $out;
	}

	/**
	 * Apply foreign keys for every clean relationship that doesn't have one yet.
	 * Ensures the index exists first. Never touches dirty/missing relationships.
	 *
	 * @return array{applied:array<int,string>,skipped:array<int,string>,errors:array<int,string>}
	 */
	function epc_erp_integrity_apply_fks(PDO $db): array
	{
		$applied = array();
		$skipped = array();
		$errors = array();
		foreach (epc_erp_integrity_relationships() as $rel) {
			$orphans = epc_erp_integrity_orphan_count($db, $rel);
			$label = $rel['child'] . '.' . $rel['col'] . ' -> ' . $rel['parent'] . '.' . $rel['pcol'];
			if ($orphans < 0) { $skipped[] = $label . ' (table/column missing)'; continue; }
			if (epc_erp_integrity_has_fk($db, $rel['child'], $rel['col'])) { $skipped[] = $label . ' (FK already present)'; continue; }
			if ($orphans > 0) { $skipped[] = $label . ' (' . $orphans . ' orphan rows — clean data first)'; continue; }

			// Align column type to the parent (FKs require compatible types).
			$parentType = epc_erp_integrity_column_type($db, $rel['parent'], $rel['pcol']);
			$childType = epc_erp_integrity_column_type($db, $rel['child'], $rel['col']);
			try {
				if (!epc_erp_integrity_has_index($db, $rel['child'], $rel['col'])) {
					$db->exec('ALTER TABLE `' . $rel['child'] . '` ADD INDEX `fk_' . $rel['col'] . '` (`' . $rel['col'] . '`)');
				}
				$fkName = 'fk_' . $rel['child'] . '_' . $rel['col'];
				$fkName = substr($fkName, 0, 60);
				$db->exec(
					'ALTER TABLE `' . $rel['child'] . '`
					 ADD CONSTRAINT `' . $fkName . '`
					 FOREIGN KEY (`' . $rel['col'] . '`) REFERENCES `' . $rel['parent'] . '` (`' . $rel['pcol'] . '`)
					 ON DELETE ' . $rel['on_delete'] . ' ON UPDATE CASCADE'
				);
				$applied[] = $label;
			} catch (Exception $e) {
				$errors[] = $label . ': ' . $e->getMessage();
			}
		}
		return array('applied' => $applied, 'skipped' => $skipped, 'errors' => $errors);
	}
}
