<?php
defined('_ASTEXE_') or die('No access');

/**
 * Data import / migration center — Excel/CSV import with templates for the core
 * master entities (customers, suppliers, chart of accounts, products). Paste or
 * upload a CSV, preview the parsed rows, then import. Writes only into the
 * current tenant's database.
 */

require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_helpers.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_phase8.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_gl.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_inventory.php';

epc_erp_phase8_ensure_schema($db_link);
epc_erp_gl_ensure_schema($db_link);
epc_erp_inventory_ensure_schema($db_link);

$accountTypes = function_exists('epc_erp_gl_account_types') ? array_keys(epc_erp_gl_account_types()) : array('asset', 'liability', 'equity', 'revenue', 'expense');

$entities = array(
	'customers' => array(
		'label' => 'Customers',
		'headers' => array('name', 'company', 'email', 'phone', 'trn', 'address', 'city', 'country_code', 'currency_code'),
		'required' => array('name'),
		'sample' => array(
			array('Ahmed Trading LLC', 'Ahmed Trading LLC', 'ahmed@example.com', '+97150111222', '100123456700003', 'Deira', 'Dubai', 'AE', 'AED'),
			array('Gulf Spares FZE', 'Gulf Spares FZE', 'sales@gulfspares.ae', '+97144556677', '100765432100001', 'JAFZA', 'Dubai', 'AE', 'AED'),
		),
	),
	'suppliers' => array(
		'label' => 'Suppliers',
		'headers' => array('name', 'company', 'email', 'phone', 'trn', 'address', 'city', 'country_code', 'currency_code'),
		'required' => array('name'),
		'sample' => array(
			array('Shanghai Parts Co', 'Shanghai Parts Co', 'export@shparts.cn', '+862112345678', '', 'Pudong', 'Shanghai', 'CN', 'USD'),
			array('Bosch MEA', 'Bosch MEA', 'mea@bosch.com', '+97143334444', '100222333400001', 'DIP', 'Dubai', 'AE', 'AED'),
		),
	),
	'coa' => array(
		'label' => 'Chart of accounts',
		'headers' => array('code', 'name', 'account_type', 'opening_balance'),
		'required' => array('code', 'name', 'account_type'),
		'sample' => array(
			array('1100', 'Accounts Receivable', 'asset', '0'),
			array('2100', 'Accounts Payable', 'liability', '0'),
			array('4000', 'Sales Revenue', 'revenue', '0'),
		),
	),
	'products' => array(
		'label' => 'Products / SKUs',
		'headers' => array('sku', 'name', 'unit'),
		'required' => array('sku', 'name'),
		'sample' => array(
			array('BRK-PAD-001', 'Brake Pad Set Front', 'set'),
			array('GOLD-22K', 'Gold 22K', 'gram'),
			array('DIA-VS1', 'Diamond VS1', 'carat'),
		),
	),
);

$entityKey = isset($_POST['entity']) ? (string) $_POST['entity'] : (isset($_GET['entity']) ? (string) $_GET['entity'] : 'customers');
if (!isset($entities[$entityKey])) {
	$entityKey = 'customers';
}
$entity = $entities[$entityKey];

$importMsg = '';
$importErr = '';
$previewRows = array();
$previewHeaders = array();
$rawCsv = '';
$importResult = null;

/** Parse CSV text into header + assoc rows. */
function epc_erp_import_parse_csv(string $text): array
{
	$text = str_replace(array("\r\n", "\r"), "\n", $text);
	$lines = array_values(array_filter(array_map('trim', explode("\n", $text)), function ($l) {
		return $l !== '';
	}));
	if (count($lines) < 1) {
		return array('headers' => array(), 'rows' => array());
	}
	$headers = array_map(function ($h) {
		return strtolower(trim((string) $h));
	}, str_getcsv($lines[0]));
	$rows = array();
	for ($i = 1; $i < count($lines); $i++) {
		$cols = str_getcsv($lines[$i]);
		$assoc = array();
		foreach ($headers as $idx => $h) {
			$assoc[$h] = isset($cols[$idx]) ? trim((string) $cols[$idx]) : '';
		}
		$rows[] = $assoc;
	}
	return array('headers' => $headers, 'rows' => $rows);
}

if (!empty($_POST['import_action'])) {
	$postedCsrf = isset($_POST['csrf_guard_key']) ? (string) $_POST['csrf_guard_key'] : '';
	if ($csrf !== '' && !hash_equals($csrf, $postedCsrf)) {
		$importErr = 'Security token mismatch — please reload and try again.';
	} else {
		// Source CSV: uploaded file wins, else textarea.
		$rawCsv = isset($_POST['csv_data']) ? (string) $_POST['csv_data'] : '';
		if (!empty($_FILES['csv_file']['tmp_name']) && is_uploaded_file($_FILES['csv_file']['tmp_name'])) {
			$uploaded = file_get_contents($_FILES['csv_file']['tmp_name']);
			if ($uploaded !== false && trim($uploaded) !== '') {
				$rawCsv = $uploaded;
			}
		}
		$parsed = epc_erp_import_parse_csv($rawCsv);
		$previewHeaders = $parsed['headers'];
		$previewRows = $parsed['rows'];

		if ($_POST['import_action'] === 'import') {
			if (empty($previewRows)) {
				$importErr = 'No data rows found to import.';
			} else {
				$ok = 0;
				$fail = 0;
				$errors = array();
				foreach ($previewRows as $n => $row) {
					try {
						foreach ($entity['required'] as $reqCol) {
							if (!isset($row[$reqCol]) || $row[$reqCol] === '') {
								throw new Exception('missing required "' . $reqCol . '"');
							}
						}
						if ($entityKey === 'customers' || $entityKey === 'suppliers') {
							epc_erp_contact_save($db_link, array(
								'party_type' => $entityKey === 'suppliers' ? 'supplier' : 'customer',
								'name' => $row['name'] ?? '',
								'company' => $row['company'] ?? '',
								'email' => $row['email'] ?? '',
								'phone' => $row['phone'] ?? '',
								'trn' => $row['trn'] ?? '',
								'address' => $row['address'] ?? '',
								'city' => $row['city'] ?? '',
								'country_code' => $row['country_code'] ?? 'AE',
								'currency_code' => $row['currency_code'] ?? 'AED',
							));
						} elseif ($entityKey === 'coa') {
							$atype = strtolower((string) ($row['account_type'] ?? ''));
							if (!in_array($atype, $accountTypes, true)) {
								throw new Exception('invalid account_type "' . $atype . '"');
							}
							epc_erp_gl_create_coa($db_link, array(
								'code' => $row['code'] ?? '',
								'name' => $row['name'] ?? '',
								'account_type' => $atype,
								'opening_balance' => (float) ($row['opening_balance'] ?? 0),
							));
						} elseif ($entityKey === 'products') {
							epc_erp_inventory_create_item($db_link, array(
								'sku' => $row['sku'] ?? '',
								'name' => $row['name'] ?? '',
								'unit' => $row['unit'] ?? 'pcs',
							));
						}
						$ok++;
					} catch (Exception $e) {
						$fail++;
						if (count($errors) < 12) {
							$errors[] = 'Row ' . ($n + 1) . ': ' . $e->getMessage();
						}
					}
				}
				$importResult = array('ok' => $ok, 'fail' => $fail, 'errors' => $errors);
				$importMsg = 'Imported ' . $ok . ' ' . $entity['label'] . ($fail > 0 ? (' — ' . $fail . ' row(s) failed.') : ' successfully.');
				// Clear preview after import.
				$previewRows = array();
			}
		} else {
			// preview only
			if (empty($previewRows)) {
				$importErr = 'No data rows detected. Make sure row 1 is the header line.';
			} else {
				$importMsg = 'Parsed ' . count($previewRows) . ' row(s). Review below, then click Import.';
			}
		}
	}
}

$sampleCsv = implode(',', $entity['headers']) . "\n";
foreach ($entity['sample'] as $s) {
	$sampleCsv .= implode(',', $s) . "\n";
}
$templateCsv = implode(',', $entity['headers']) . "\n";
?>
<div class="epc-erp-hero">
	<h3><i class="fa fa-upload"></i> Data import / migration</h3>
	<p>Bulk-load master data from <strong>Excel/CSV</strong> with a downloadable template. Pick an entity, download the template, fill it in Excel, then paste or upload and preview before importing. Records are written to this tenant only.</p>
</div>

<?php if ($importMsg !== ''): ?>
<div class="alert alert-success" style="margin-bottom:14px;"><i class="fa fa-check-circle"></i> <?php echo epc_erp_h($importMsg); ?></div>
<?php endif; ?>
<?php if ($importErr !== ''): ?>
<div class="alert alert-danger" style="margin-bottom:14px;"><i class="fa fa-exclamation-triangle"></i> <?php echo epc_erp_h($importErr); ?></div>
<?php endif; ?>
<?php if ($importResult !== null && !empty($importResult['errors'])): ?>
<div class="alert alert-warning" style="margin-bottom:14px;">
	<strong>Skipped rows:</strong>
	<ul style="margin:6px 0 0 18px;">
	<?php foreach ($importResult['errors'] as $er): ?>
		<li><?php echo epc_erp_h($er); ?></li>
	<?php endforeach; ?>
	</ul>
</div>
<?php endif; ?>

<form method="get" class="form-inline" style="margin-bottom:14px;">
	<input type="hidden" name="area" value="setup">
	<input type="hidden" name="tab" value="data_import">
	<?php if (!empty($_GET['epc_erp_shell'])): ?><input type="hidden" name="epc_erp_shell" value="1"><?php endif; ?>
	<label>Entity to import:</label>
	<select name="entity" class="form-control input-sm" onchange="this.form.submit()" style="min-width:240px;">
		<?php foreach ($entities as $ek => $edef): ?>
		<option value="<?php echo epc_erp_h($ek); ?>" <?php echo $entityKey === $ek ? 'selected' : ''; ?>><?php echo epc_erp_h($edef['label']); ?></option>
		<?php endforeach; ?>
	</select>
</form>

<div class="panel panel-default" style="max-width:1000px;">
	<div class="panel-heading">
		<strong><?php echo epc_erp_h($entity['label']); ?></strong> — template columns:
		<?php foreach ($entity['headers'] as $h): ?><code style="margin-right:5px;"><?php echo epc_erp_h($h); ?></code><?php endforeach; ?>
		<?php if (!empty($entity['required'])): ?><span class="text-muted">(required: <?php echo epc_erp_h(implode(', ', $entity['required'])); ?><?php echo $entityKey === 'coa' ? '; account_type one of: ' . implode('/', $accountTypes) : ''; ?>)</span><?php endif; ?>
	</div>
	<div class="panel-body">
		<p>
			<button type="button" class="btn btn-default btn-sm" onclick="epcDlTemplate()"><i class="fa fa-download"></i> Download template (.csv)</button>
			<button type="button" class="btn btn-default btn-sm" onclick="epcLoadSample()"><i class="fa fa-flask"></i> Load sample rows</button>
		</p>
		<form method="post" action="" enctype="multipart/form-data">
			<input type="hidden" name="csrf_guard_key" value="<?php echo epc_erp_h($csrf); ?>">
			<input type="hidden" name="entity" value="<?php echo epc_erp_h($entityKey); ?>">
			<div class="form-group" style="width:100%;">
				<label>Paste CSV (row 1 = header):</label>
				<textarea id="epc_csv_data" name="csv_data" class="form-control" rows="7" style="font-family:monospace;width:100%;" placeholder="<?php echo epc_erp_h(implode(',', $entity['headers'])); ?>"><?php echo epc_erp_h($rawCsv); ?></textarea>
			</div>
			<div class="form-group" style="margin-top:8px;">
				<label>…or upload a .csv file:</label>
				<input type="file" name="csv_file" accept=".csv,text/csv" class="form-control input-sm">
			</div>
			<div style="margin-top:10px;">
				<button type="submit" name="import_action" value="preview" class="btn btn-default btn-sm"><i class="fa fa-search"></i> Preview</button>
				<button type="submit" name="import_action" value="import" class="btn btn-success btn-sm" onclick="return confirm('Import these rows into <?php echo epc_erp_h($entity['label']); ?>?');"><i class="fa fa-upload"></i> Import now</button>
			</div>
		</form>
	</div>
</div>

<?php if (!empty($previewRows)): ?>
<h4 style="margin-top:18px;">Preview — <?php echo count($previewRows); ?> row(s)</h4>
<div style="overflow-x:auto;">
<table class="table table-bordered table-condensed table-striped" style="background:#fff;">
	<thead><tr><th>#</th><?php foreach ($previewHeaders as $h): ?><th><?php echo epc_erp_h($h); ?></th><?php endforeach; ?></tr></thead>
	<tbody>
	<?php foreach (array_slice($previewRows, 0, 50) as $i => $row): ?>
		<tr>
			<td><?php echo $i + 1; ?></td>
			<?php foreach ($previewHeaders as $h): ?><td><?php echo epc_erp_h($row[$h] ?? ''); ?></td><?php endforeach; ?>
		</tr>
	<?php endforeach; ?>
	</tbody>
</table>
</div>
<?php if (count($previewRows) > 50): ?><p class="text-muted">Showing first 50 of <?php echo count($previewRows); ?> rows.</p><?php endif; ?>
<?php endif; ?>

<script>
function epcDlTemplate() {
	var csv = <?php echo json_encode($templateCsv); ?>;
	epcDownloadCsv(csv, <?php echo json_encode('template_' . $entityKey . '.csv'); ?>);
}
function epcLoadSample() {
	document.getElementById('epc_csv_data').value = <?php echo json_encode($sampleCsv); ?>;
}
function epcDownloadCsv(content, filename) {
	var blob = new Blob([content], {type: 'text/csv;charset=utf-8;'});
	var url = URL.createObjectURL(blob);
	var a = document.createElement('a');
	a.href = url; a.download = filename;
	document.body.appendChild(a); a.click();
	document.body.removeChild(a); URL.revokeObjectURL(url);
}
</script>
