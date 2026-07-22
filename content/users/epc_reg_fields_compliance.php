<?php
/**
 * UAE e-invoice / AML / KYC registration-field compliance helpers.
 * Extends `reg_fields` for approval workflows without breaking legacy text fields.
 */
defined('_ASTEXE_') or die('No access');

/**
 * Ensure compliance columns exist on reg_fields.
 */
function epc_rf_ensure_schema(PDO $db): void
{
	static $done = false;
	if ($done) {
		return;
	}
	$done = true;

	$cols = array();
	try {
		foreach ($db->query('SHOW COLUMNS FROM `reg_fields`') as $row) {
			$cols[strtolower((string) $row['Field'])] = true;
		}
	} catch (Throwable $e) {
		return;
	}

	$alters = array(
		'field_category' => "ADD COLUMN `field_category` VARCHAR(32) NOT NULL DEFAULT 'general' AFTER `widget_options`",
		'available_for_approval' => "ADD COLUMN `available_for_approval` TINYINT(1) NOT NULL DEFAULT 1 AFTER `to_users_table`",
		'for_customer_approval' => "ADD COLUMN `for_customer_approval` TINYINT(1) NOT NULL DEFAULT 0 AFTER `available_for_approval`",
		'for_vendor_approval' => "ADD COLUMN `for_vendor_approval` TINYINT(1) NOT NULL DEFAULT 0 AFTER `for_customer_approval`",
		'compliance_tag' => "ADD COLUMN `compliance_tag` VARCHAR(48) NOT NULL DEFAULT '' AFTER `for_vendor_approval`",
	);
	foreach ($alters as $col => $ddl) {
		if (empty($cols[$col])) {
			try {
				$db->exec('ALTER TABLE `reg_fields` ' . $ddl);
			} catch (Throwable $e) {
				// Column may already exist on concurrent runs.
			}
		}
	}
}

/**
 * Category labels for the CP workbench.
 *
 * @return array<string,string>
 */
function epc_rf_categories(): array
{
	return array(
		'identity' => 'Identity & contact',
		'business' => 'Business details',
		'einvoice' => 'E-invoice / VAT (FTA)',
		'kyc_aml' => 'KYC / AML (UAE)',
		'documents' => 'Supporting documents',
		'general' => 'General',
	);
}

/**
 * Canonical UAE + e-invoice + AML/KYC field pack.
 * Names align with enhanced registration profile keys where possible.
 *
 * @return list<array<string,mixed>>
 */
function epc_rf_uae_field_pack(): array
{
	return array(
		// Identity
		array('name' => 'name', 'caption' => 'First name', 'category' => 'identity', 'widget' => 'text', 'maxlen' => 80, 'tag' => 'first_name', 'customer' => 1, 'vendor' => 0, 'required_hint' => 1),
		array('name' => 'surname', 'caption' => 'Last name', 'category' => 'identity', 'widget' => 'text', 'maxlen' => 80, 'tag' => 'last_name', 'customer' => 1, 'vendor' => 0, 'required_hint' => 1),
		array('name' => 'epc_reg_job_title', 'caption' => 'Job title / role', 'category' => 'identity', 'widget' => 'text', 'maxlen' => 80, 'tag' => 'job_title', 'customer' => 1, 'vendor' => 1, 'required_hint' => 0),
		array('name' => 'epc_emirates_id_no', 'caption' => 'Emirates ID number', 'category' => 'identity', 'widget' => 'text', 'maxlen' => 20, 'regexp' => '^[0-9-]{5,20}$', 'example' => '784-XXXX-XXXXXXX-X', 'tag' => 'emirates_id', 'customer' => 1, 'vendor' => 1, 'required_hint' => 0),
		array('name' => 'epc_passport_no', 'caption' => 'Passport number', 'category' => 'identity', 'widget' => 'text', 'maxlen' => 30, 'tag' => 'passport', 'customer' => 1, 'vendor' => 1, 'required_hint' => 0),
		array('name' => 'epc_nationality', 'caption' => 'Nationality', 'category' => 'identity', 'widget' => 'text', 'maxlen' => 60, 'tag' => 'nationality', 'customer' => 1, 'vendor' => 1, 'required_hint' => 0),

		// Business
		array('name' => 'company_name', 'caption' => 'Company / trade name', 'category' => 'business', 'widget' => 'text', 'maxlen' => 160, 'tag' => 'company', 'customer' => 1, 'vendor' => 1, 'required_hint' => 1),
		array('name' => 'epc_reg_legal_name', 'caption' => 'Legal entity name', 'category' => 'business', 'widget' => 'text', 'maxlen' => 200, 'tag' => 'legal_name', 'customer' => 1, 'vendor' => 1, 'required_hint' => 1),
		array('name' => 'epc_reg_business_type', 'caption' => 'Business type', 'category' => 'business', 'widget' => 'text', 'maxlen' => 80, 'tag' => 'business_type', 'customer' => 1, 'vendor' => 1, 'required_hint' => 1),
		array('name' => 'epc_reg_country', 'caption' => 'Country (ISO)', 'category' => 'business', 'widget' => 'text', 'maxlen' => 2, 'regexp' => '^[A-Z]{2}$', 'example' => 'AE', 'tag' => 'country', 'customer' => 1, 'vendor' => 1, 'required_hint' => 1),
		array('name' => 'epc_reg_emirate', 'caption' => 'Emirate / state', 'category' => 'business', 'widget' => 'text', 'maxlen' => 60, 'tag' => 'emirate', 'customer' => 1, 'vendor' => 1, 'required_hint' => 1),
		array('name' => 'epc_reg_city', 'caption' => 'City', 'category' => 'business', 'widget' => 'text', 'maxlen' => 80, 'tag' => 'city', 'customer' => 1, 'vendor' => 1, 'required_hint' => 1),
		array('name' => 'epc_reg_address', 'caption' => 'Business / delivery address', 'category' => 'business', 'widget' => 'text', 'maxlen' => 255, 'tag' => 'address', 'customer' => 1, 'vendor' => 1, 'required_hint' => 1),
		array('name' => 'epc_reg_postal', 'caption' => 'P.O. Box', 'category' => 'business', 'widget' => 'text', 'maxlen' => 40, 'tag' => 'postal', 'customer' => 1, 'vendor' => 1, 'required_hint' => 0),
		array('name' => 'epc_reg_website', 'caption' => 'Website', 'category' => 'business', 'widget' => 'text', 'maxlen' => 160, 'tag' => 'website', 'customer' => 1, 'vendor' => 1, 'required_hint' => 0),

		// E-invoice / VAT (FTA)
		array('name' => 'epc_reg_trn', 'caption' => 'TRN (Tax Registration Number)', 'category' => 'einvoice', 'widget' => 'text', 'maxlen' => 15, 'regexp' => '^[0-9]{15}$', 'example' => '100123456700003', 'tag' => 'trn', 'customer' => 1, 'vendor' => 1, 'required_hint' => 1),
		array('name' => 'epc_reg_trn_mode', 'caption' => 'TRN availability', 'category' => 'einvoice', 'widget' => 'text', 'maxlen' => 20, 'example' => 'has_trn / not_available', 'tag' => 'trn_mode', 'customer' => 1, 'vendor' => 1, 'required_hint' => 0),
		array('name' => 'epc_reg_trade_licence', 'caption' => 'Trade licence number', 'category' => 'einvoice', 'widget' => 'text', 'maxlen' => 60, 'tag' => 'trade_licence', 'customer' => 1, 'vendor' => 1, 'required_hint' => 1),
		array('name' => 'epc_legal_reg_type', 'caption' => 'Legal ID type (TL / EID / PAS / CDN)', 'category' => 'einvoice', 'widget' => 'text', 'maxlen' => 8, 'example' => 'TL', 'tag' => 'legal_reg_type', 'customer' => 1, 'vendor' => 1, 'required_hint' => 1),
		array('name' => 'epc_tin', 'caption' => 'TIN (if applicable)', 'category' => 'einvoice', 'widget' => 'text', 'maxlen' => 40, 'tag' => 'tin', 'customer' => 1, 'vendor' => 1, 'required_hint' => 0),

		// KYC / AML
		array('name' => 'epc_authorized_signatory', 'caption' => 'Authorized signatory name', 'category' => 'kyc_aml', 'widget' => 'text', 'maxlen' => 120, 'tag' => 'signatory', 'customer' => 1, 'vendor' => 1, 'required_hint' => 1),
		array('name' => 'epc_authorized_signatory_id', 'caption' => 'Signatory Emirates ID / passport', 'category' => 'kyc_aml', 'widget' => 'text', 'maxlen' => 40, 'tag' => 'signatory_id', 'customer' => 1, 'vendor' => 1, 'required_hint' => 1),
		array('name' => 'epc_ubo_name', 'caption' => 'Ultimate beneficial owner (UBO)', 'category' => 'kyc_aml', 'widget' => 'text', 'maxlen' => 160, 'tag' => 'ubo', 'customer' => 1, 'vendor' => 1, 'required_hint' => 1),
		array('name' => 'epc_source_of_funds', 'caption' => 'Source of funds / wealth', 'category' => 'kyc_aml', 'widget' => 'text', 'maxlen' => 255, 'tag' => 'sof', 'customer' => 1, 'vendor' => 1, 'required_hint' => 0),
		array('name' => 'epc_pep_declaration', 'caption' => 'PEP declaration (Yes / No)', 'category' => 'kyc_aml', 'widget' => 'text', 'maxlen' => 10, 'example' => 'No', 'tag' => 'pep', 'customer' => 1, 'vendor' => 1, 'required_hint' => 1),
		array('name' => 'epc_sanctions_declaration', 'caption' => 'Sanctions screening declaration', 'category' => 'kyc_aml', 'widget' => 'text', 'maxlen' => 20, 'example' => 'Clear', 'tag' => 'sanctions', 'customer' => 1, 'vendor' => 1, 'required_hint' => 0),

		// Documents (paths stored in users_profiles)
		array('name' => 'epc_doc_trade_licence', 'caption' => 'Trade licence scan (PDF/JPG)', 'category' => 'documents', 'widget' => 'file', 'maxlen' => 255, 'tag' => 'doc_tl', 'customer' => 1, 'vendor' => 1, 'required_hint' => 1),
		array('name' => 'epc_doc_vat_certificate', 'caption' => 'VAT / TRN certificate', 'category' => 'documents', 'widget' => 'file', 'maxlen' => 255, 'tag' => 'doc_vat', 'customer' => 1, 'vendor' => 1, 'required_hint' => 0),
		array('name' => 'epc_doc_emirates_id', 'caption' => 'Emirates ID copy', 'category' => 'documents', 'widget' => 'file', 'maxlen' => 255, 'tag' => 'doc_eid', 'customer' => 1, 'vendor' => 1, 'required_hint' => 1),
		array('name' => 'epc_doc_passport', 'caption' => 'Passport copy', 'category' => 'documents', 'widget' => 'file', 'maxlen' => 255, 'tag' => 'doc_passport', 'customer' => 1, 'vendor' => 1, 'required_hint' => 0),
		array('name' => 'epc_doc_power_of_attorney', 'caption' => 'Power of attorney (if applicable)', 'category' => 'documents', 'widget' => 'file', 'maxlen' => 255, 'tag' => 'doc_poa', 'customer' => 1, 'vendor' => 1, 'required_hint' => 0),
		array('name' => 'epc_doc_moa', 'caption' => 'MOA / AOA extract', 'category' => 'documents', 'widget' => 'file', 'maxlen' => 255, 'tag' => 'doc_moa', 'customer' => 1, 'vendor' => 1, 'required_hint' => 0),
		array('name' => 'epc_ubo_id_document', 'caption' => 'UBO identity document', 'category' => 'documents', 'widget' => 'file', 'maxlen' => 255, 'tag' => 'doc_ubo', 'customer' => 1, 'vendor' => 1, 'required_hint' => 0),
		array('name' => 'epc_tax_exempt_cert_path', 'caption' => 'Tax-exempt certificate', 'category' => 'documents', 'widget' => 'file', 'maxlen' => 255, 'tag' => 'doc_tax_exempt', 'customer' => 1, 'vendor' => 0, 'required_hint' => 0),
	);
}

/**
 * @return list<int>
 */
function epc_rf_variant_ids(PDO $db): array
{
	$ids = array();
	try {
		$st = $db->query('SELECT `id` FROM `reg_variants` ORDER BY `order` ASC');
		while ($st && ($row = $st->fetch(PDO::FETCH_ASSOC))) {
			$ids[] = (int) $row['id'];
		}
	} catch (Throwable $e) {
	}
	return $ids;
}

/**
 * Variant IDs that look like wholesale / business accounts.
 *
 * @return list<int>
 */
function epc_rf_wholesale_variant_ids(PDO $db): array
{
	$ids = array();
	try {
		$st = $db->query('SELECT `id`, `caption` FROM `reg_variants` ORDER BY `order` ASC');
		while ($st && ($row = $st->fetch(PDO::FETCH_ASSOC))) {
			$cap = '';
			if (function_exists('translate_str_by_id')) {
				$cap = (string) translate_str_by_id($row['caption']);
			}
			$cap = strtolower($cap . ' ' . (string) $row['caption']);
			if (strpos($cap, 'wholesale') !== false || strpos($cap, 'опт') !== false || strpos($cap, 'business') !== false || strpos($cap, 'b2b') !== false) {
				$ids[] = (int) $row['id'];
			}
		}
	} catch (Throwable $e) {
	}
	return $ids;
}

/**
 * Seed missing UAE compliance fields. Does not overwrite existing names.
 *
 * @return array{added:int,skipped:int,names:list<string>}
 */
function epc_rf_seed_uae_pack(PDO $db): array
{
	epc_rf_ensure_schema($db);
	$existing = array();
	$st = $db->query('SELECT `name` FROM `reg_fields`');
	while ($st && ($row = $st->fetch(PDO::FETCH_ASSOC))) {
		$existing[strtolower((string) $row['name'])] = true;
	}

	$maxOrder = 0;
	try {
		$maxOrder = (int) $db->query('SELECT COALESCE(MAX(`order`), 0) FROM `reg_fields`')->fetchColumn();
	} catch (Throwable $e) {
	}

	$allVariants = epc_rf_variant_ids($db);
	$wholesale = epc_rf_wholesale_variant_ids($db);
	if ($wholesale === [] && count($allVariants) === 1) {
		$wholesale = $allVariants;
	}

	$added = 0;
	$skipped = 0;
	$names = array();
	$ins = $db->prepare('INSERT INTO `reg_fields` (`main_flag`, `name`, `caption`, `show_for`, `required_for`, `maxlen`, `regexp`, `widget_type`, `widget_options`, `example`, `order`, `to_filter`, `to_users_table`, `field_category`, `available_for_approval`, `for_customer_approval`, `for_vendor_approval`, `compliance_tag`) VALUES (0,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?)');

	foreach (epc_rf_uae_field_pack() as $f) {
		$key = strtolower((string) $f['name']);
		if (!empty($existing[$key])) {
			$skipped++;
			continue;
		}
		$maxOrder++;
		$caption = (string) $f['caption'];
		$example = (string) ($f['example'] ?? '');
		if (function_exists('save_custom_translation')) {
			$caption = save_custom_translation(0, $caption);
			$example = save_custom_translation(0, $example);
		}
		$showFor = json_encode(array_values($allVariants));
		$reqFor = !empty($f['required_hint']) ? json_encode(array_values($wholesale)) : '[]';
		$widget = in_array(($f['widget'] ?? 'text'), array('text', 'file', 'select'), true) ? $f['widget'] : 'text';
		$ok = $ins->execute(array(
			(string) $f['name'],
			$caption,
			$showFor,
			$reqFor,
			(int) ($f['maxlen'] ?? 0),
			(string) ($f['regexp'] ?? ''),
			$widget,
			'[]',
			$example,
			$maxOrder,
			in_array($f['category'], array('einvoice', 'business'), true) ? 1 : 0,
			in_array($f['category'], array('einvoice', 'business', 'identity'), true) ? 1 : 0,
			(string) $f['category'],
			1,
			!empty($f['customer']) ? 1 : 0,
			!empty($f['vendor']) ? 1 : 0,
			(string) ($f['tag'] ?? ''),
		));
		if ($ok) {
			$added++;
			$names[] = (string) $f['name'];
			$existing[$key] = true;
		}
	}

	return array('added' => $added, 'skipped' => $skipped, 'names' => $names);
}

/**
 * Mark all custom fields available for approval (bulk enable).
 */
function epc_rf_mark_all_for_approval(PDO $db): int
{
	epc_rf_ensure_schema($db);
	$st = $db->prepare('UPDATE `reg_fields` SET `available_for_approval` = 1, `for_customer_approval` = 1 WHERE `main_flag` = 0');
	$st->execute();
	return (int) $st->rowCount();
}

/**
 * @return list<array<string,mixed>>
 */
function epc_rf_approval_fields(PDO $db, string $scope = 'customer'): array
{
	epc_rf_ensure_schema($db);
	$col = $scope === 'vendor' ? 'for_vendor_approval' : 'for_customer_approval';
	$sql = "SELECT * FROM `reg_fields` WHERE `main_flag` = 0 AND `available_for_approval` = 1 AND (`{$col}` = 1 OR `{$col}` = 1) ORDER BY FIELD(`field_category`,'einvoice','kyc_aml','documents','business','identity','general'), `order` ASC";
	// Simplify: available + scope flag
	$sql = "SELECT * FROM `reg_fields` WHERE `main_flag` = 0 AND `available_for_approval` = 1 AND `{$col}` = 1 ORDER BY `order` ASC";
	try {
		$st = $db->query($sql);
		$rows = $st ? $st->fetchAll(PDO::FETCH_ASSOC) : array();
	} catch (Throwable $e) {
		return array();
	}
	foreach ($rows as &$r) {
		if (function_exists('translate_str_by_id')) {
			$r['caption_label'] = (string) translate_str_by_id($r['caption']);
			$ex = translate_str_by_id($r['example']);
			$r['example_label'] = ($ex === null || $ex === false || strcasecmp((string) $ex, 'null') === 0) ? '' : (string) $ex;
		} else {
			$r['caption_label'] = (string) $r['caption'];
			$r['example_label'] = (string) $r['example'];
		}
	}
	unset($r);
	return $rows;
}

/**
 * Profile value lookup for a user.
 */
function epc_rf_profile_value(PDO $db, int $userId, string $key): string
{
	if ($userId <= 0 || $key === '') {
		return '';
	}
	try {
		$st = $db->prepare('SELECT `data_value` FROM `users_profiles` WHERE `user_id` = ? AND `data_key` = ? LIMIT 1');
		$st->execute(array($userId, $key));
		$v = $st->fetchColumn();
		return $v === false ? '' : trim((string) $v);
	} catch (Throwable $e) {
		return '';
	}
}

function epc_rf_is_file_value(string $value): bool
{
	if ($value === '') {
		return false;
	}
	if (preg_match('#^(/|https?://)#i', $value)) {
		return true;
	}
	return (bool) preg_match('/\.(pdf|jpe?g|png|webp|gif)$/i', $value);
}

/**
 * Render compliance checklist HTML for customer/vendor approval review.
 */
function epc_rf_render_approval_checklist(PDO $db, int $userId, string $scope = 'customer'): string
{
	$fields = epc_rf_approval_fields($db, $scope);
	if ($fields === []) {
		return '<p class="text-muted" style="margin:8px 0 0;">No approval fields configured yet. Open <strong>Registration fields</strong> and seed the UAE compliance pack.</p>';
	}

	$cats = epc_rf_categories();
	$grouped = array();
	foreach ($fields as $f) {
		$cat = (string) ($f['field_category'] ?? 'general');
		if (!isset($grouped[$cat])) {
			$grouped[$cat] = array();
		}
		$grouped[$cat][] = $f;
	}

	$filled = 0;
	$total = 0;
	ob_start();
	echo '<div class="epc-rf-checklist">';
	echo '<h4 style="margin:16px 0 8px;font-size:15px;">Compliance checklist <small class="text-muted">(e-invoice · KYC · AML · documents)</small></h4>';

	foreach ($grouped as $cat => $items) {
		$label = $cats[$cat] ?? ucfirst($cat);
		echo '<div class="epc-rf-checklist__group">';
		echo '<div class="epc-rf-checklist__group-h">' . htmlspecialchars($label, ENT_QUOTES, 'UTF-8') . '</div>';
		echo '<table class="table table-condensed" style="margin:0 0 12px;"><tbody>';
		foreach ($items as $f) {
			$total++;
			$key = (string) $f['name'];
			$val = epc_rf_profile_value($db, $userId, $key);
			// Aliases from enhanced registration
			if ($val === '' && $key === 'company_name') {
				$val = epc_rf_profile_value($db, $userId, 'company');
			}
			$ok = ($val !== '');
			if ($ok) {
				$filled++;
			}
			$cap = htmlspecialchars((string) ($f['caption_label'] ?? $key), ENT_QUOTES, 'UTF-8');
			$widget = (string) ($f['widget_type'] ?? 'text');
			$status = $ok
				? '<span class="label label-success">Provided</span>'
				: '<span class="label label-warning">Missing</span>';
			$display = '—';
			if ($ok) {
				if ($widget === 'file' || epc_rf_is_file_value($val)) {
					$url = $val;
					if ($url !== '' && $url[0] === '/') {
						// keep relative
					} elseif ($url !== '' && strpos($url, 'http') !== 0) {
						$url = '/' . ltrim($url, '/');
					}
					$display = '<a href="' . htmlspecialchars($url, ENT_QUOTES, 'UTF-8') . '" target="_blank" rel="noopener">View document</a>';
				} else {
					$display = htmlspecialchars($val, ENT_QUOTES, 'UTF-8');
				}
			}
			echo '<tr><td style="width:42%;">' . $cap . '</td><td>' . $display . '</td><td style="width:90px;text-align:right;">' . $status . '</td></tr>';
		}
		echo '</tbody></table></div>';
	}

	$pct = $total > 0 ? (int) round(100 * $filled / $total) : 0;
	echo '<p style="margin:0 0 8px;"><strong>Completeness:</strong> ' . (int) $filled . ' / ' . (int) $total . ' (' . $pct . '%)</p>';
	echo '</div>';
	return (string) ob_get_clean();
}
