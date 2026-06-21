<?php
/**
 * Product Information Management — Custom Fields engine.
 *
 * Lets the operator define unlimited custom fields (attributes) for products.
 * Each field has a type (text, number, date, boolean, single_option,
 * multi_option) and an optional set of predefined option values.
 * Fields can be flagged for visibility on Inventory, Sales and Purchase modules.
 *
 * Usage:
 *   $fieldId = epc_pim_field_save($db, ['name'=>'Inventory Type', 'field_type'=>'single_option', ...]);
 *   epc_pim_field_option_save($db, $fieldId, 'Inventory');
 *   epc_pim_field_option_save($db, $fieldId, 'Non-Inventory');
 *   epc_pim_field_option_save($db, $fieldId, 'Service');
 *   epc_pim_value_save($db, $itemId, $fieldId, 'Inventory');
 *   $values = epc_pim_values_for_item($db, $itemId);  // => [{field_name, field_type, value, ...}]
 */
defined('_ASTEXE_') or die('No access');

function epc_pim_ensure_schema(PDO $db)
{
	$db->exec('CREATE TABLE IF NOT EXISTS `epc_pim_fields` (
		`id`            INT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
		`name`          VARCHAR(200) NOT NULL DEFAULT "",
		`code`          VARCHAR(60) NOT NULL DEFAULT "",
		`field_type`    ENUM("text","number","date","boolean","single_option","multi_option") NOT NULL DEFAULT "text",
		`description`   VARCHAR(500) NOT NULL DEFAULT "",
		`default_value` VARCHAR(500) NOT NULL DEFAULT "",
		`required`      TINYINT(1) NOT NULL DEFAULT 0,
		`show_inventory` TINYINT(1) NOT NULL DEFAULT 1,
		`show_sales`    TINYINT(1) NOT NULL DEFAULT 1,
		`show_purchase` TINYINT(1) NOT NULL DEFAULT 1,
		`position`      SMALLINT UNSIGNED NOT NULL DEFAULT 0,
		`active`        TINYINT(1) NOT NULL DEFAULT 1,
		`created_at`    INT UNSIGNED NOT NULL DEFAULT 0,
		`updated_at`    INT UNSIGNED NOT NULL DEFAULT 0,
		UNIQUE KEY `uk_code` (`code`),
		INDEX `idx_type` (`field_type`),
		INDEX `idx_active` (`active`)
	) ENGINE=InnoDB DEFAULT CHARSET=utf8');

	$db->exec('CREATE TABLE IF NOT EXISTS `epc_pim_field_options` (
		`id`        INT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
		`field_id`  INT UNSIGNED NOT NULL DEFAULT 0,
		`label`     VARCHAR(200) NOT NULL DEFAULT "",
		`value`     VARCHAR(200) NOT NULL DEFAULT "",
		`position`  SMALLINT UNSIGNED NOT NULL DEFAULT 0,
		`active`    TINYINT(1) NOT NULL DEFAULT 1,
		INDEX `idx_field` (`field_id`)
	) ENGINE=InnoDB DEFAULT CHARSET=utf8');

	$db->exec('CREATE TABLE IF NOT EXISTS `epc_pim_item_values` (
		`id`        INT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
		`item_id`   INT UNSIGNED NOT NULL DEFAULT 0,
		`field_id`  INT UNSIGNED NOT NULL DEFAULT 0,
		`value_text` TEXT,
		`value_number` DECIMAL(16,4) DEFAULT NULL,
		`value_date` DATE DEFAULT NULL,
		`value_bool` TINYINT(1) DEFAULT NULL,
		`value_option_ids` VARCHAR(500) NOT NULL DEFAULT "",
		`updated_at` INT UNSIGNED NOT NULL DEFAULT 0,
		UNIQUE KEY `uk_item_field` (`item_id`, `field_id`),
		INDEX `idx_item` (`item_id`),
		INDEX `idx_field` (`field_id`)
	) ENGINE=InnoDB DEFAULT CHARSET=utf8');
}

function epc_pim_field_save(PDO $db, array $data)
{
	$now = time();
	$id = isset($data['id']) ? (int) $data['id'] : 0;

	if (!isset($data['code']) || $data['code'] === '') {
		$data['code'] = strtolower(preg_replace('/[^a-zA-Z0-9]+/', '_', trim($data['name'] ?? '')));
		$data['code'] = rtrim($data['code'], '_');
	}

	if ($id > 0) {
		$db->prepare(
			'UPDATE `epc_pim_fields` SET `name`=?, `code`=?, `field_type`=?, `description`=?, `default_value`=?, `required`=?, `show_inventory`=?, `show_sales`=?, `show_purchase`=?, `position`=?, `active`=?, `updated_at`=? WHERE `id`=?'
		)->execute(array(
			$data['name'] ?? '', $data['code'] ?? '',
			$data['field_type'] ?? 'text', $data['description'] ?? '',
			$data['default_value'] ?? '',
			isset($data['required']) ? (int) $data['required'] : 0,
			isset($data['show_inventory']) ? (int) $data['show_inventory'] : 1,
			isset($data['show_sales']) ? (int) $data['show_sales'] : 1,
			isset($data['show_purchase']) ? (int) $data['show_purchase'] : 1,
			(int) ($data['position'] ?? 0),
			isset($data['active']) ? (int) $data['active'] : 1,
			$now, $id,
		));
		return $id;
	}
	$db->prepare(
		'INSERT INTO `epc_pim_fields` (`name`,`code`,`field_type`,`description`,`default_value`,`required`,`show_inventory`,`show_sales`,`show_purchase`,`position`,`active`,`created_at`,`updated_at`) VALUES (?,?,?,?,?,?,?,?,?,?,?,?,?)'
	)->execute(array(
		$data['name'] ?? '', $data['code'] ?? '',
		$data['field_type'] ?? 'text', $data['description'] ?? '',
		$data['default_value'] ?? '',
		isset($data['required']) ? (int) $data['required'] : 0,
		isset($data['show_inventory']) ? (int) $data['show_inventory'] : 1,
		isset($data['show_sales']) ? (int) $data['show_sales'] : 1,
		isset($data['show_purchase']) ? (int) $data['show_purchase'] : 1,
		(int) ($data['position'] ?? 0),
		isset($data['active']) ? (int) $data['active'] : 1,
		$now, $now,
	));
	return (int) $db->lastInsertId();
}

function epc_pim_field_delete(PDO $db, int $fieldId)
{
	$db->prepare('UPDATE `epc_pim_fields` SET `active` = 0, `updated_at` = ? WHERE `id` = ?')->execute(array(time(), $fieldId));
}

function epc_pim_field_list(PDO $db, string $module = '')
{
	$sql = 'SELECT f.*, (SELECT COUNT(*) FROM `epc_pim_field_options` o WHERE o.`field_id` = f.`id` AND o.`active` = 1) AS option_count FROM `epc_pim_fields` f WHERE f.`active` = 1';
	if ($module === 'inventory') {
		$sql .= ' AND f.`show_inventory` = 1';
	} elseif ($module === 'sales') {
		$sql .= ' AND f.`show_sales` = 1';
	} elseif ($module === 'purchase') {
		$sql .= ' AND f.`show_purchase` = 1';
	}
	$sql .= ' ORDER BY f.`position` ASC, f.`name` ASC';
	return $db->query($sql)->fetchAll(PDO::FETCH_ASSOC);
}

function epc_pim_field_get(PDO $db, int $fieldId)
{
	$st = $db->prepare('SELECT * FROM `epc_pim_fields` WHERE `id` = ?');
	$st->execute(array($fieldId));
	return $st->fetch(PDO::FETCH_ASSOC) ?: null;
}

function epc_pim_field_option_save(PDO $db, int $fieldId, string $label, string $value = '', int $position = 0)
{
	if ($value === '') {
		$value = $label;
	}
	$db->prepare(
		'INSERT INTO `epc_pim_field_options` (`field_id`,`label`,`value`,`position`) VALUES (?,?,?,?)'
	)->execute(array($fieldId, $label, $value, $position));
	return (int) $db->lastInsertId();
}

function epc_pim_field_option_update(PDO $db, int $optionId, string $label, string $value = '', int $position = 0)
{
	if ($value === '') {
		$value = $label;
	}
	$db->prepare(
		'UPDATE `epc_pim_field_options` SET `label`=?, `value`=?, `position`=? WHERE `id`=?'
	)->execute(array($label, $value, $position, $optionId));
}

function epc_pim_field_option_delete(PDO $db, int $optionId)
{
	$db->prepare('UPDATE `epc_pim_field_options` SET `active` = 0 WHERE `id` = ?')->execute(array($optionId));
}

function epc_pim_field_options(PDO $db, int $fieldId)
{
	$st = $db->prepare(
		'SELECT * FROM `epc_pim_field_options` WHERE `field_id` = ? AND `active` = 1 ORDER BY `position` ASC, `label` ASC'
	);
	$st->execute(array($fieldId));
	return $st->fetchAll(PDO::FETCH_ASSOC);
}

function epc_pim_value_save(PDO $db, int $itemId, int $fieldId, $value)
{
	$now = time();
	$field = epc_pim_field_get($db, $fieldId);
	if (!$field) {
		return;
	}

	$textVal = null;
	$numVal = null;
	$dateVal = null;
	$boolVal = null;
	$optionIds = '';

	switch ($field['field_type']) {
		case 'text':
			$textVal = (string) $value;
			break;
		case 'number':
			$numVal = (float) $value;
			break;
		case 'date':
			$dateVal = (string) $value;
			break;
		case 'boolean':
			$boolVal = $value ? 1 : 0;
			break;
		case 'single_option':
			if (is_array($value)) {
				$optionIds = implode(',', array_map('intval', $value));
			} else {
				$opts = epc_pim_field_options($db, $fieldId);
				foreach ($opts as $opt) {
					if ($opt['value'] === (string) $value || $opt['label'] === (string) $value || (int) $opt['id'] === (int) $value) {
						$optionIds = (string) $opt['id'];
						break;
					}
				}
				if ($optionIds === '') {
					$textVal = (string) $value;
				}
			}
			break;
		case 'multi_option':
			if (is_array($value)) {
				$optionIds = implode(',', array_map('intval', $value));
			} else {
				$optionIds = (string) $value;
			}
			break;
	}

	$db->prepare(
		'INSERT INTO `epc_pim_item_values` (`item_id`,`field_id`,`value_text`,`value_number`,`value_date`,`value_bool`,`value_option_ids`,`updated_at`)
		 VALUES (?,?,?,?,?,?,?,?)
		 ON DUPLICATE KEY UPDATE `value_text`=VALUES(`value_text`), `value_number`=VALUES(`value_number`), `value_date`=VALUES(`value_date`), `value_bool`=VALUES(`value_bool`), `value_option_ids`=VALUES(`value_option_ids`), `updated_at`=VALUES(`updated_at`)'
	)->execute(array($itemId, $fieldId, $textVal, $numVal, $dateVal, $boolVal, $optionIds, $now));
}

function epc_pim_value_delete(PDO $db, int $itemId, int $fieldId)
{
	$db->prepare('DELETE FROM `epc_pim_item_values` WHERE `item_id` = ? AND `field_id` = ?')->execute(array($itemId, $fieldId));
}

function epc_pim_values_for_item(PDO $db, int $itemId)
{
	$st = $db->prepare(
		'SELECT iv.*, f.`name` AS field_name, f.`code` AS field_code, f.`field_type`, f.`required`, f.`show_inventory`, f.`show_sales`, f.`show_purchase`
		 FROM `epc_pim_item_values` iv
		 INNER JOIN `epc_pim_fields` f ON f.`id` = iv.`field_id` AND f.`active` = 1
		 WHERE iv.`item_id` = ?
		 ORDER BY f.`position` ASC, f.`name` ASC'
	);
	$st->execute(array($itemId));
	$rows = $st->fetchAll(PDO::FETCH_ASSOC);

	foreach ($rows as &$row) {
		$row['display_value'] = epc_pim_resolve_display_value($db, $row);
	}
	unset($row);
	return $rows;
}

function epc_pim_values_for_item_module(PDO $db, int $itemId, string $module)
{
	$all = epc_pim_values_for_item($db, $itemId);
	$key = 'show_' . $module;
	return array_filter($all, function ($row) use ($key) {
		return isset($row[$key]) && (int) $row[$key] === 1;
	});
}

function epc_pim_resolve_display_value(PDO $db, array $row)
{
	switch ($row['field_type']) {
		case 'text':
			return (string) ($row['value_text'] ?? '');
		case 'number':
			return $row['value_number'] !== null ? (float) $row['value_number'] : '';
		case 'date':
			return (string) ($row['value_date'] ?? '');
		case 'boolean':
			return $row['value_bool'] !== null ? ((int) $row['value_bool'] ? 'Yes' : 'No') : '';
		case 'single_option':
		case 'multi_option':
			$ids = array_filter(explode(',', (string) ($row['value_option_ids'] ?? '')));
			if (empty($ids)) {
				return (string) ($row['value_text'] ?? '');
			}
			$placeholders = implode(',', array_fill(0, count($ids), '?'));
			$st = $db->prepare('SELECT `label` FROM `epc_pim_field_options` WHERE `id` IN (' . $placeholders . ') AND `active` = 1 ORDER BY `position`');
			$st->execute(array_map('intval', $ids));
			$labels = $st->fetchAll(PDO::FETCH_COLUMN);
			return implode(', ', $labels);
		default:
			return (string) ($row['value_text'] ?? '');
	}
}

function epc_pim_bulk_values_for_items(PDO $db, array $itemIds, string $module = '')
{
	if (empty($itemIds)) {
		return array();
	}
	$placeholders = implode(',', array_fill(0, count($itemIds), '?'));
	$sql = 'SELECT iv.*, f.`name` AS field_name, f.`code` AS field_code, f.`field_type`, f.`show_inventory`, f.`show_sales`, f.`show_purchase`
	        FROM `epc_pim_item_values` iv
	        INNER JOIN `epc_pim_fields` f ON f.`id` = iv.`field_id` AND f.`active` = 1
	        WHERE iv.`item_id` IN (' . $placeholders . ')';
	if ($module !== '') {
		$sql .= ' AND f.`show_' . preg_replace('/[^a-z]/', '', $module) . '` = 1';
	}
	$sql .= ' ORDER BY f.`position` ASC, f.`name` ASC';
	$st = $db->prepare($sql);
	$st->execute(array_map('intval', $itemIds));
	$rows = $st->fetchAll(PDO::FETCH_ASSOC);

	$grouped = array();
	foreach ($rows as $row) {
		$row['display_value'] = epc_pim_resolve_display_value($db, $row);
		$grouped[(int) $row['item_id']][] = $row;
	}
	return $grouped;
}

function epc_pim_render_form_fields(PDO $db, string $module, int $itemId = 0)
{
	$fields = epc_pim_field_list($db, $module);
	$existing = array();
	if ($itemId > 0) {
		$vals = epc_pim_values_for_item($db, $itemId);
		foreach ($vals as $v) {
			$existing[(int) $v['field_id']] = $v;
		}
	}

	$html = '';
	foreach ($fields as $f) {
		$fid = (int) $f['id'];
		$fname = htmlspecialchars($f['name'], ENT_QUOTES);
		$inputName = 'pim_field_' . $fid;
		$req = (int) $f['required'] ? ' <span style="color:red">*</span>' : '';
		$val = isset($existing[$fid]) ? $existing[$fid] : null;

		$html .= '<div class="form-group" style="margin-bottom:12px;">';
		$html .= '<label style="font-weight:600; display:block; margin-bottom:4px;">' . $fname . $req . '</label>';

		switch ($f['field_type']) {
			case 'text':
				$v = $val ? htmlspecialchars((string) $val['value_text'], ENT_QUOTES) : '';
				$html .= '<input type="text" name="' . $inputName . '" value="' . $v . '" class="form-control" style="width:100%; padding:6px 10px; border:1px solid #ccc; border-radius:4px;" />';
				break;

			case 'number':
				$v = $val && $val['value_number'] !== null ? (float) $val['value_number'] : '';
				$html .= '<input type="number" step="any" name="' . $inputName . '" value="' . $v . '" class="form-control" style="width:100%; padding:6px 10px; border:1px solid #ccc; border-radius:4px;" />';
				break;

			case 'date':
				$v = $val ? htmlspecialchars((string) $val['value_date'], ENT_QUOTES) : '';
				$html .= '<input type="date" name="' . $inputName . '" value="' . $v . '" class="form-control" style="width:100%; padding:6px 10px; border:1px solid #ccc; border-radius:4px;" />';
				break;

			case 'boolean':
				$checked = $val && (int) $val['value_bool'] ? ' checked' : '';
				$html .= '<label style="display:inline-flex; align-items:center; gap:6px; cursor:pointer;"><input type="checkbox" name="' . $inputName . '" value="1"' . $checked . ' /> Yes</label>';
				break;

			case 'single_option':
				$opts = epc_pim_field_options($db, $fid);
				$selIds = $val ? explode(',', (string) $val['value_option_ids']) : array();
				$html .= '<select name="' . $inputName . '" class="form-control" style="width:100%; padding:6px 10px; border:1px solid #ccc; border-radius:4px;">';
				$html .= '<option value="">-- Select --</option>';
				foreach ($opts as $opt) {
					$sel = in_array((string) $opt['id'], $selIds) ? ' selected' : '';
					$html .= '<option value="' . (int) $opt['id'] . '"' . $sel . '>' . htmlspecialchars($opt['label'], ENT_QUOTES) . '</option>';
				}
				$html .= '</select>';
				break;

			case 'multi_option':
				$opts = epc_pim_field_options($db, $fid);
				$selIds = $val ? explode(',', (string) $val['value_option_ids']) : array();
				$html .= '<div style="border:1px solid #ccc; border-radius:4px; padding:8px; max-height:160px; overflow-y:auto;">';
				foreach ($opts as $opt) {
					$chk = in_array((string) $opt['id'], $selIds) ? ' checked' : '';
					$html .= '<label style="display:block; cursor:pointer; padding:2px 0;"><input type="checkbox" name="' . $inputName . '[]" value="' . (int) $opt['id'] . '"' . $chk . ' /> ' . htmlspecialchars($opt['label'], ENT_QUOTES) . '</label>';
				}
				$html .= '</div>';
				break;
		}

		if ($f['description'] !== '') {
			$html .= '<small style="color:#888; display:block; margin-top:2px;">' . htmlspecialchars($f['description'], ENT_QUOTES) . '</small>';
		}
		$html .= '</div>';
	}
	return $html;
}

function epc_pim_save_from_post(PDO $db, int $itemId, array $postData, string $module = '')
{
	$fields = epc_pim_field_list($db, $module);
	foreach ($fields as $f) {
		$fid = (int) $f['id'];
		$key = 'pim_field_' . $fid;
		if (!array_key_exists($key, $postData)) {
			if ($f['field_type'] === 'boolean') {
				epc_pim_value_save($db, $itemId, $fid, 0);
			}
			continue;
		}
		$raw = $postData[$key];

		switch ($f['field_type']) {
			case 'multi_option':
				$ids = is_array($raw) ? $raw : array($raw);
				epc_pim_value_save($db, $itemId, $fid, $ids);
				break;
			case 'single_option':
				epc_pim_value_save($db, $itemId, $fid, array((int) $raw));
				break;
			case 'boolean':
				epc_pim_value_save($db, $itemId, $fid, (int) $raw);
				break;
			default:
				epc_pim_value_save($db, $itemId, $fid, $raw);
				break;
		}
	}
}

function epc_pim_render_display_table(PDO $db, int $itemId, string $module = '')
{
	$values = $module !== '' ? epc_pim_values_for_item_module($db, $itemId, $module) : epc_pim_values_for_item($db, $itemId);
	if (empty($values)) {
		return '';
	}
	$html = '<table style="width:100%; border-collapse:collapse; margin:10px 0;">';
	$html .= '<thead><tr style="background:#f5f5f5;"><th style="text-align:left; padding:6px 10px; border:1px solid #ddd;">Field</th><th style="text-align:left; padding:6px 10px; border:1px solid #ddd;">Value</th></tr></thead><tbody>';
	foreach ($values as $v) {
		$html .= '<tr><td style="padding:6px 10px; border:1px solid #ddd;">' . htmlspecialchars($v['field_name'], ENT_QUOTES) . '</td>';
		$html .= '<td style="padding:6px 10px; border:1px solid #ddd;">' . htmlspecialchars((string) $v['display_value'], ENT_QUOTES) . '</td></tr>';
	}
	$html .= '</tbody></table>';
	return $html;
}
