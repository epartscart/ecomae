<?php
/**
 * Shared financial-dimensions component (Module 1: Business Unit + sub-modules).
 *
 * Wires the Business Unit master and its sub-dimensions (Legal entity, Class,
 * Cost centre and any custom financial dimensions a tenant defines) into ALL
 * transactional entry forms — sales orders, purchase orders, customers,
 * vendors, journals, cash/bank and general entries — so every transaction can
 * be tagged and segregated by dimension.
 *
 * Nothing is hard-coded: the selectable dimensions are read live from each
 * tenant's own master tables (epc_erp_pm_*). Assignments are stored in a single
 * polymorphic table so the same picker works on every entity type with no
 * per-table schema changes.
 */
defined('_ASTEXE_') or die('No access');

require_once __DIR__ . '/epc_erp_pdf_modules.php';

/** Create the polymorphic dimension-assignment table (idempotent). */
function epc_erp_dim_ensure_schema(PDO $db): void
{
	static $done = false;
	if ($done) {
		return;
	}
	epc_erp_pm_ensure_schema($db);
	$db->exec("CREATE TABLE IF NOT EXISTS `epc_erp_dim_links` (
		`id` int(11) NOT NULL AUTO_INCREMENT,
		`entity_type` varchar(40) NOT NULL DEFAULT '',
		`entity_id` int(11) NOT NULL DEFAULT 0,
		`dim_key` varchar(40) NOT NULL DEFAULT '',
		`ref_id` int(11) NOT NULL DEFAULT 0,
		`value_code` varchar(60) NOT NULL DEFAULT '',
		`value_label` varchar(190) NOT NULL DEFAULT '',
		`time_created` int(11) NOT NULL DEFAULT 0,
		PRIMARY KEY (`id`),
		KEY `ent` (`entity_type`,`entity_id`),
		KEY `dimkey` (`entity_type`,`dim_key`,`ref_id`)
	) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='ERP financial dimension assignments (polymorphic)';");
	$done = true;
}

/**
 * Active dimension specs to show on entry forms, read from the tenant DB.
 * Returns an ordered list of:
 *   array('key'=>..., 'label'=>..., 'options'=>array(array('id','code','name'),...))
 * A dimension is only included when it has at least one active value, so empty
 * masters never clutter the forms.
 *
 * @return array<int,array{key:string,label:string,options:array}>
 */
function epc_erp_dim_specs(PDO $db): array
{
	static $cache = null;
	if ($cache !== null) {
		return $cache;
	}
	epc_erp_dim_ensure_schema($db);
	$specs = array();
	$fixed = array(
		'business_unit' => array('Business unit', 'epc_erp_pm_business_units'),
		'legal_entity'  => array('Legal entity', 'epc_erp_pm_legal_entities'),
		'class_unit'    => array('Class', 'epc_erp_pm_class_units'),
	);
	foreach ($fixed as $key => $info) {
		try {
			$rows = $db->query("SELECT `id`,`code`,`name` FROM `{$info[1]}` WHERE `active`=1 ORDER BY `code`,`name`")
				->fetchAll(PDO::FETCH_ASSOC);
		} catch (Exception $e) {
			$rows = array();
		}
		if (!empty($rows)) {
			$specs[] = array('key' => $key, 'label' => $info[0], 'options' => $rows);
		}
	}
	// Custom financial dimensions (cost centre, department, project, …).
	try {
		$dims = $db->query("SELECT `id`,`code`,`name` FROM `epc_erp_pm_dimensions` WHERE `active`=1 ORDER BY `code`,`name`")
			->fetchAll(PDO::FETCH_ASSOC);
	} catch (Exception $e) {
		$dims = array();
	}
	foreach ($dims as $d) {
		$st = $db->prepare("SELECT `id`,`code`,`name` FROM `epc_erp_pm_dimension_values` WHERE `dimension_id`=? AND `active`=1 ORDER BY `code`,`name`");
		$st->execute(array((int) $d['id']));
		$rows = $st->fetchAll(PDO::FETCH_ASSOC);
		if (!empty($rows)) {
			$specs[] = array('key' => 'dim' . (int) $d['id'], 'label' => (string) $d['name'], 'options' => $rows);
		}
	}
	$cache = $specs;
	return $specs;
}

/** Friendly label for a dimension key (for list/summary display). */
function epc_erp_dim_key_label(PDO $db, string $key): string
{
	static $cache = null;
	if ($cache === null) {
		$cache = array();
		foreach (epc_erp_dim_specs($db) as $sp) {
			$cache[$sp['key']] = $sp['label'];
		}
	}
	if (isset($cache[$key])) {
		return $cache[$key];
	}
	$fallback = array('business_unit' => 'Business unit', 'legal_entity' => 'Legal entity', 'class_unit' => 'Class');
	return $fallback[$key] ?? $key;
}

/**
 * Render dimension <select> fields for an entry form.
 *
 * @param array $current  key => ref_id (to pre-select on edit)
 * @param array $opts     layout: 'horizontal' (default) | 'inline';
 *                        title: heading for the horizontal block (optional)
 */
function epc_erp_dim_render_fields(PDO $db, array $current = array(), array $opts = array()): string
{
	$specs = epc_erp_dim_specs($db);
	if (empty($specs)) {
		return '';
	}
	$layout = isset($opts['layout']) ? (string) $opts['layout'] : 'horizontal';
	$h = function ($s) {
		return htmlspecialchars((string) $s, ENT_QUOTES, 'UTF-8');
	};
	$optionHtml = function (array $sp) use ($current, $h, $layout) {
		$out = $layout === 'inline'
			? '<option value="">' . $h($sp['label']) . '…</option>'
			: '<option value="">—</option>';
		$cur = (int) ($current[$sp['key']] ?? 0);
		foreach ($sp['options'] as $o) {
			$label = ((string) $o['code'] !== '' ? $o['code'] . ' — ' : '') . (string) $o['name'];
			$sel = ($cur === (int) $o['id']) ? ' selected' : '';
			$out .= '<option value="' . (int) $o['id'] . '"' . $sel . '>' . $h($label) . '</option>';
		}
		return $out;
	};
	ob_start();
	if ($layout === 'inline') {
		foreach ($specs as $sp) {
			echo '<div class="form-group epc-erp-dim-field">';
			echo '<select name="dim[' . $h($sp['key']) . ']" class="form-control input-sm" title="' . $h($sp['label']) . '">';
			echo $optionHtml($sp);
			echo '</select></div>';
		}
		return (string) ob_get_clean();
	}
	$title = isset($opts['title']) ? (string) $opts['title'] : 'Financial dimensions';
	echo '<div class="epc-erp-dim-fields">';
	if ($title !== '') {
		echo '<div class="form-group"><div class="col-sm-offset-3 col-sm-9"><p class="help-block" style="margin:0 0 4px;"><i class="fa fa-sitemap"></i> <strong>' . $h($title) . '</strong> — tag this entry to a business unit &amp; dimensions for reporting.</p></div></div>';
	}
	foreach ($specs as $sp) {
		echo '<div class="form-group"><label class="col-sm-3">' . $h($sp['label']) . '</label><div class="col-sm-9">';
		echo '<select name="dim[' . $h($sp['key']) . ']" class="form-control input-sm">';
		echo $optionHtml($sp);
		echo '</select></div></div>';
	}
	echo '</div>';
	return (string) ob_get_clean();
}

/**
 * Persist dimension assignments for an entity (replace-on-save).
 *
 * @param array $dim  key => ref_id (typically $_POST['dim'])
 */
function epc_erp_dim_save(PDO $db, string $entityType, int $entityId, array $dim): void
{
	epc_erp_dim_ensure_schema($db);
	$entityType = trim($entityType);
	if ($entityType === '' || $entityId <= 0) {
		return;
	}
	$byKey = array();
	foreach (epc_erp_dim_specs($db) as $sp) {
		$byKey[$sp['key']] = $sp;
	}
	$db->prepare('DELETE FROM `epc_erp_dim_links` WHERE `entity_type`=? AND `entity_id`=?')
		->execute(array($entityType, $entityId));
	$now = time();
	$ins = $db->prepare('INSERT INTO `epc_erp_dim_links` (`entity_type`,`entity_id`,`dim_key`,`ref_id`,`value_code`,`value_label`,`time_created`) VALUES (?,?,?,?,?,?,?)');
	foreach ($dim as $key => $refId) {
		$key = (string) $key;
		$refId = (int) $refId;
		if ($refId <= 0 || !isset($byKey[$key])) {
			continue;
		}
		$code = '';
		$label = '';
		foreach ($byKey[$key]['options'] as $o) {
			if ((int) $o['id'] === $refId) {
				$code = (string) $o['code'];
				$label = (string) $o['name'];
				break;
			}
		}
		if ($code === '' && $label === '') {
			continue;
		}
		$ins->execute(array($entityType, $entityId, $key, $refId, $code, $label, $now));
	}
}

/** Convenience: save dimensions straight from a POST array ($post['dim']). */
function epc_erp_dim_save_from_post(PDO $db, string $entityType, int $entityId, array $post): void
{
	$dim = (isset($post['dim']) && is_array($post['dim'])) ? $post['dim'] : array();
	epc_erp_dim_save($db, $entityType, $entityId, $dim);
}

/**
 * Load dimension assignments for an entity.
 *
 * @return array<string,array{ref_id:int,code:string,label:string}>
 */
function epc_erp_dim_load(PDO $db, string $entityType, int $entityId): array
{
	epc_erp_dim_ensure_schema($db);
	if (trim($entityType) === '' || $entityId <= 0) {
		return array();
	}
	$st = $db->prepare('SELECT `dim_key`,`ref_id`,`value_code`,`value_label` FROM `epc_erp_dim_links` WHERE `entity_type`=? AND `entity_id`=? ORDER BY `id`');
	$st->execute(array($entityType, $entityId));
	$out = array();
	foreach ($st->fetchAll(PDO::FETCH_ASSOC) as $r) {
		$out[(string) $r['dim_key']] = array(
			'ref_id' => (int) $r['ref_id'],
			'code'   => (string) $r['value_code'],
			'label'  => (string) $r['value_label'],
		);
	}
	return $out;
}

/** key => ref_id map, for pre-filling a form on edit. */
function epc_erp_dim_current(PDO $db, string $entityType, int $entityId): array
{
	$out = array();
	foreach (epc_erp_dim_load($db, $entityType, $entityId) as $k => $v) {
		$out[$k] = $v['ref_id'];
	}
	return $out;
}

/** Short text summary: "Business unit: Retail · Cost centre: CC-OPS". */
function epc_erp_dim_summary(PDO $db, string $entityType, int $entityId): string
{
	$parts = array();
	foreach (epc_erp_dim_load($db, $entityType, $entityId) as $key => $v) {
		$parts[] = epc_erp_dim_key_label($db, $key) . ': ' . $v['label'];
	}
	return implode(' · ', $parts);
}

/** Badge HTML for list/grid cells. */
function epc_erp_dim_badges(PDO $db, string $entityType, int $entityId): string
{
	$links = epc_erp_dim_load($db, $entityType, $entityId);
	if (empty($links)) {
		return '<span class="text-muted">—</span>';
	}
	$h = function ($s) {
		return htmlspecialchars((string) $s, ENT_QUOTES, 'UTF-8');
	};
	$out = '';
	foreach ($links as $key => $v) {
		$out .= '<span class="label label-default" title="' . $h(epc_erp_dim_key_label($db, $key)) . '" style="margin:0 2px 2px 0;display:inline-block;">' . $h($v['label']) . '</span>';
	}
	return $out;
}

/** Entity ids tagged to a given dimension value — for reports/filtering. */
function epc_erp_dim_entity_ids(PDO $db, string $entityType, string $dimKey, int $refId): array
{
	epc_erp_dim_ensure_schema($db);
	$st = $db->prepare('SELECT `entity_id` FROM `epc_erp_dim_links` WHERE `entity_type`=? AND `dim_key`=? AND `ref_id`=?');
	$st->execute(array($entityType, $dimKey, $refId));
	return array_map('intval', array_column($st->fetchAll(PDO::FETCH_ASSOC), 'entity_id'));
}
